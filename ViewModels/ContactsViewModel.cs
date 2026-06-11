using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailMessenger.Client.Models;
using MailMessenger.Client.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Contact = MailMessenger.Client.Models.Contact;

namespace MailMessenger.Client.ViewModels;

public partial class ContactsViewModel : ObservableObject
{
	private const string WallpaperKeyPref = "wallpaper_key";
	private const string AvatarPathPref = "profile_avatar_path";
	private const string WallpaperImagePathPref = "wallpaper_image_path";
	private const string NotificationGifPathPref = "notification_gif_path";
	private const string WallpaperOpacityPref = "wallpaper_opacity";
	private const string BadgeThemePref = "badge_theme";
	private const string BadgeSizePref = "badge_size";

	private readonly ILocalDb localDb;
	private readonly ISecureCredentialStore credentialStore;
	private readonly IMailService mailService;
	private readonly IAppAppearanceService appearanceService;
	private readonly IAssetCacheService assetCacheService;

	public ContactsViewModel(
		ILocalDb localDb,
		ISecureCredentialStore credentialStore,
		IMailService mailService,
		IAppAppearanceService appearanceService,
		IAssetCacheService assetCacheService)
	{
		this.localDb = localDb;
		this.credentialStore = credentialStore;
		this.mailService = mailService;
		this.appearanceService = appearanceService;
		this.assetCacheService = assetCacheService;
		mailService.ConnectionStateChanged += OnConnectionStateChanged;
		mailService.MessageReceived += OnMessageReceived;
		mailService.UnreadReset += OnUnreadReset;

		WallpaperImagePath = appearanceService.GetGlobalWallpaperPath();
		NotificationGifPath = appearanceService.GetNotificationGifPath();
		WallpaperDimOpacity = appearanceService.GetWallpaperDimOpacity();
		BadgeTheme = appearanceService.GetBadgeTheme();
		BadgeSize = appearanceService.GetBadgeSize();

		InitializeAssetOptions();
		_ = LoadOwnEmailAsync();
	}

	[ObservableProperty]
	private string ownEmail = string.Empty;

	private async Task LoadOwnEmailAsync()
	{
		try
		{
			var creds = await credentialStore.LoadAsync();
			if (creds.HasValue)
			{
				OwnEmail = creds.Value.Email;
			}
		}
		catch
		{
			// best-effort
		}
	}

	public ObservableCollection<Contact> Contacts { get; } = [];

	public ObservableCollection<Contact> FilteredContacts { get; } = [];

	[ObservableProperty]
	private bool isRefreshing;

	[ObservableProperty]
	private bool isImapConnected;

	[ObservableProperty]
	private string imapStatusText = "IMAP: подключение…";

	[ObservableProperty]
	private string? statusMessage;

	[ObservableProperty]
	private Contact? selectedContact;

	[ObservableProperty]
	private string searchText = string.Empty;

	[ObservableProperty]
	private bool showUnreadOnly;

	[ObservableProperty]
	private bool isSettingsOpen;

	[ObservableProperty]
	private string settingsTab = "profile";

	[ObservableProperty]
	private string wallpaperKey = Preferences.Default.Get(WallpaperKeyPref, "default");

	[ObservableProperty]
	private string? wallpaperImagePath = LoadWallpaperImagePath();

	[ObservableProperty]
	private string? notificationGifPath = LoadNotificationGifPath();

	[ObservableProperty]
	private double wallpaperDimOpacity = Preferences.Default.Get(WallpaperOpacityPref, 0.12);

	[ObservableProperty]
	private string currentWallpaperTitle = "Не выбрано";

	[ObservableProperty]
	private string currentNotificationTitle = "Не выбрано";

	[ObservableProperty]
	private string badgeTheme = Preferences.Default.Get(BadgeThemePref, "red");

	[ObservableProperty]
	private double badgeSize = Preferences.Default.Get(BadgeSizePref, 20.0);

	public ObservableCollection<string> BadgeThemes { get; } = new(new[] { "red", "amber" });

	public ObservableCollection<AssetOption> WallpaperOptions { get; } = [];

	public ObservableCollection<AssetOption> NotificationGifOptions { get; } = [];

	[ObservableProperty]
	private string? avatarPath = LoadAvatarPath();

	private static string? LoadAvatarPath()
	{
		var value = Preferences.Default.Get(AvatarPathPref, string.Empty);
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	private static string? LoadWallpaperImagePath()
	{
		var value = Preferences.Default.Get(WallpaperImagePathPref, string.Empty);
		return string.IsNullOrWhiteSpace(value) || !File.Exists(value) ? null : value;
	}

	private static string? LoadNotificationGifPath()
	{
		var value = Preferences.Default.Get(NotificationGifPathPref, string.Empty);
		return string.IsNullOrWhiteSpace(value) || !File.Exists(value) ? null : value;
	}

	private static bool IsKnownAsset(string? fileName, IEnumerable<string> known)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return false;
		}

		return known.Any(x => string.Equals(x, fileName, StringComparison.OrdinalIgnoreCase));
	}

	private static bool MatchesSelectedFile(string? selectedPath, string fileName)
	{
		if (string.IsNullOrWhiteSpace(selectedPath))
		{
			return false;
		}

		var selectedName = Path.GetFileName(selectedPath);
		return string.Equals(selectedName, fileName, StringComparison.OrdinalIgnoreCase);
	}

	private void RefreshSelectionState()
	{
		string? currentWallpaperFile = null;
		string? currentGifFile = null;

		foreach (var option in WallpaperOptions)
		{
			option.IsSelected = MatchesSelectedFile(WallpaperImagePath, option.FileName);
			if (option.IsSelected)
			{
				currentWallpaperFile = option.Title;
			}
		}

		foreach (var option in NotificationGifOptions)
		{
			option.IsSelected = MatchesSelectedFile(NotificationGifPath, option.FileName);
			if (option.IsSelected)
			{
				currentGifFile = option.Title;
			}
		}

		CurrentWallpaperTitle = currentWallpaperFile ?? "Не выбрано";
		CurrentNotificationTitle = currentGifFile ?? "Не выбрано";
	}

	private void InitializeAssetOptions()
	{
		var wallpaper = ImageAssets.WallpaperJpg
			.Select((file, i) => new AssetOption
			{
				FileName = file,
				Title = $"Обои {i + 1}"
			})
			.ToList();

		var gifs = ImageAssets.NotificationGif
			.Select((file, i) => new AssetOption
			{
				FileName = file,
				Title = $"GIF {i + 1}"
			})
			.ToList();

		foreach (var item in wallpaper)
		{
			WallpaperOptions.Add(item);
		}

		foreach (var item in gifs)
		{
			NotificationGifOptions.Add(item);
		}

		RefreshSelectionState();
		_ = LoadAssetPreviewsAsync();
	}

	private async Task LoadAssetPreviewsAsync()
	{
		foreach (var item in WallpaperOptions)
		{
			var path = await EnsureAssetPreviewAsync(item.FileName, "wallpapers_previews");
			MainThread.BeginInvokeOnMainThread(() => item.PreviewPath = path);
		}

		foreach (var item in NotificationGifOptions)
		{
			var path = await EnsureAssetPreviewAsync(item.FileName, "notifications_previews");
			MainThread.BeginInvokeOnMainThread(() => item.PreviewPath = path);
		}
	}

private async Task<string?> EnsureAssetPreviewAsync(string fileName, string folderName)
	=> await assetCacheService.EnsureBundledPreviewAsync(fileName, folderName);

	[RelayCommand]
	private async Task LoadContactsAsync()
	{
		StatusMessage = null;
		await RefreshContactsSafeAsync();
	}

	[RelayCommand]
	private void ToggleSettings()
	{
		IsSettingsOpen = !IsSettingsOpen;
	}

	[RelayCommand]
	private void OpenProfile()
	{
		SettingsTab = "profile";
		IsSettingsOpen = true;
	}

	[RelayCommand]
	private void CloseSettings()
	{
		IsSettingsOpen = false;
	}

	[RelayCommand]
	private void ToggleShowUnreadOnly()
	{
		ShowUnreadOnly = !ShowUnreadOnly;
	}

	[RelayCommand]
	private void SelectSettingsTab(string tab)
	{
		SettingsTab = string.IsNullOrWhiteSpace(tab) ? "profile" : tab.Trim().ToLowerInvariant();
	}

	public bool IsProfileTab => SettingsTab == "profile";

	public bool IsWallpaperTab => SettingsTab == "wallpaper";

	public bool IsNotificationTab => SettingsTab == "notification";

	partial void OnSearchTextChanged(string value) => ApplyFilter();

	partial void OnSettingsTabChanged(string value)
	{
		OnPropertyChanged(nameof(IsProfileTab));
		OnPropertyChanged(nameof(IsWallpaperTab));
		OnPropertyChanged(nameof(IsNotificationTab));
	}

	partial void OnWallpaperKeyChanged(string value)
	{
		Preferences.Default.Set(WallpaperKeyPref, value ?? "default");
	}

	partial void OnAvatarPathChanged(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			Preferences.Default.Remove(AvatarPathPref);
			return;
		}

		Preferences.Default.Set(AvatarPathPref, value);
	}

	partial void OnWallpaperDimOpacityChanged(double value)
	{
		var clamped = Math.Clamp(value, 0.05, 0.35);
		if (Math.Abs(clamped - value) > 0.0001)
		{
			WallpaperDimOpacity = clamped;
			return;
		}

		appearanceService.SetWallpaperDimOpacity(clamped);
	}

	partial void OnBadgeThemeChanged(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			BadgeTheme = "red";
			return;
		}

		appearanceService.SetBadgeTheme(value);
	}

	partial void OnBadgeSizeChanged(double value)
	{
		var clamped = Math.Clamp(value, 16, 28);
		if (Math.Abs(clamped - value) > 0.0001)
		{
			BadgeSize = clamped;
			return;
		}

		appearanceService.SetBadgeSize(clamped);
	}

	partial void OnSelectedContactChanged(Contact? value)
	{
		if (value is null)
		{
			return;
		}

		SelectedContact = null;
		_ = OpenChatAsync(value);
	}

	[RelayCommand]
	private async Task OpenChatAsync(Contact contact)
	{
		await Shell.Current.GoToAsync(
			$"chat?peerEmail={Uri.EscapeDataString(contact.Email)}&peerNickname={Uri.EscapeDataString(contact.Nickname)}");
	}

	[RelayCommand]
	private async Task LogoutAsync()
	{
		await mailService.StopReceivingAsync();
		credentialStore.Clear();
		await Shell.Current.GoToAsync("//login");
	}

	[RelayCommand]
	private async Task AddContactAsync()
	{
		var email = await Shell.Current.DisplayPromptAsync(
			"Новый контакт",
			"Email собеседника:",
			"Сохранить",
			"Отмена",
			keyboard: Keyboard.Email);

		if (string.IsNullOrWhiteSpace(email))
		{
			return;
		}

		var normalizedEmail = email.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
		{
			StatusMessage = "Введите корректный email.";
			return;
		}

		var nickname = await Shell.Current.DisplayPromptAsync(
			"Новый контакт",
			"Псевдоним (необязательно):",
			"Сохранить",
			"Отмена",
			initialValue: email.Trim());

		try
		{
			var contact = await localDb.UpsertContactAsync(normalizedEmail, nickname);
			var existing = Contacts.FirstOrDefault(c => c.Id == contact.Id);
			if (existing is not null)
			{
				var index = Contacts.IndexOf(existing);
				Contacts[index] = contact;
			}
			else
			{
				Contacts.Insert(0, contact);
			}

			StatusMessage = null;
		}
		catch (Exception ex)
		{
			StatusMessage = $"Не удалось сохранить контакт: {ex.Message}";
		}
	}

	[RelayCommand]
	private async Task DeleteChatAsync(Contact contact)
	{
		if (contact is null)
		{
			return;
		}

		var confirm = await Shell.Current.DisplayAlert(
			"Удалить чат",
			$"Удалить диалог и историю с «{contact.Nickname}»?",
			"Удалить",
			"Отмена");

		if (!confirm)
		{
			return;
		}

		try
		{
			await localDb.DeleteChatAsync(contact.Email);
			Contacts.Remove(contact);
			ApplyFilter();
		}
		catch (Exception ex)
		{
			StatusMessage = $"Не удалось удалить чат: {ex.Message}";
		}
	}

	[RelayCommand]
	private async Task TogglePinAsync(Contact contact)
	{
		if (contact is null)
		{
			return;
		}

		try
		{
			var next = !contact.IsPinned;
			await localDb.SetPinnedAsync(contact.Email, next);
			contact.IsPinned = next;
			var idx = Contacts.IndexOf(contact);
			if (idx >= 0)
			{
				Contacts[idx] = contact;
			}

			SortContactsInPlace();
			ApplyFilter();
		}
		catch (Exception ex)
		{
			StatusMessage = $"Не удалось закрепить чат: {ex.Message}";
		}
	}

	[RelayCommand]
	private async Task PickContactAvatarAsync(Contact contact)
	{
		if (contact is null || string.IsNullOrWhiteSpace(contact.Email))
		{
			return;
		}

		try
		{
			var result = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = $"Аватар для {contact.Nickname}",
				FileTypes = FilePickerFileType.Images
			});

			if (result is null)
			{
				return;
			}

			var extension = Path.GetExtension(result.FileName);
			if (string.IsNullOrWhiteSpace(extension))
			{
				extension = ".img";
			}

			var safeKey = contact.Email.Trim().ToLowerInvariant()
				.Replace("@", "_at_")
				.Replace(".", "_");

			var targetDir = Path.Combine(FileSystem.AppDataDirectory, "avatars", "contacts");
			Directory.CreateDirectory(targetDir);
			var targetPath = Path.Combine(targetDir, $"{safeKey}{extension}");

			await using (var src = await result.OpenReadAsync())
			await using (var dst = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				await src.CopyToAsync(dst);
			}

			await localDb.SetContactAvatarAsync(contact.Email, targetPath);
			contact.AvatarPath = targetPath;
			ApplyFilter();
		}
		catch (Exception ex)
		{
			StatusMessage = $"Не удалось установить аватар: {ex.Message}";
		}
	}

	private void OnUnreadReset(string peerEmail)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			var contact = Contacts.FirstOrDefault(
				c => string.Equals(c.Email, peerEmail, StringComparison.OrdinalIgnoreCase));

			if (contact is null || contact.UnreadCount == 0)
			{
				return;
			}

			contact.UnreadCount = 0;
			var idx = Contacts.IndexOf(contact);
			Contacts[idx] = contact;
			ApplyFilter();
		});
	}

	private void OnConnectionStateChanged(bool connected)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			IsImapConnected = connected;
			ImapStatusText = connected ? "IMAP: подключено" : "IMAP: нет соединения";
		});
	}

	private void OnMessageReceived(ChatMessage message)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			_ = HandleNewMessageAsync(message);
		});
	}

	private async Task HandleNewMessageAsync(ChatMessage message)
	{
		// Обновляем счётчик и превью у нужного контакта точечно,
		// без полного сброса списка — так анимация на огоньке не прерывается.
		var existing = Contacts.FirstOrDefault(
			c => string.Equals(c.Email, message.PeerEmail, StringComparison.OrdinalIgnoreCase));

		if (existing is not null)
		{
			existing.LastMessage = message.Text;
			existing.LastMessageAt = message.SentAt;
			if (!message.IsMine)
			{
				existing.UnreadCount++;
			}

			// Заменяем объект в коллекции, чтобы CollectionView перерисовал строку.
			var idx = Contacts.IndexOf(existing);
			Contacts[idx] = existing;
			SortContactsInPlace();
			ApplyFilter();
		}
		else
		{
			// Новый контакт появился — делаем полный рефреш.
			await RefreshContactsSafeAsync();
		}
	}

	private async Task RefreshContactsSafeAsync()
	{
		if (IsRefreshing)
		{
			return;
		}

		try
		{
			IsRefreshing = true;
			var items = await localDb.GetContactsAsync();
			Contacts.Clear();
			foreach (var contact in items)
			{
				Contacts.Add(contact);
			}

			ApplyFilter();
		}
		catch (Exception ex)
		{
			StatusMessage = $"Не удалось обновить диалоги: {ex.Message}";
		}
		finally
		{
			IsRefreshing = false;
		}
	}

	private void ApplyFilter()
	{
		var q = SearchText?.Trim();
		IEnumerable<Contact> items = Contacts;

		if (ShowUnreadOnly)
		{
			items = items.Where(c => c.UnreadCount > 0);
		}

		if (!string.IsNullOrWhiteSpace(q))
		{
			items = items.Where(c =>
				c.Nickname.Contains(q, StringComparison.OrdinalIgnoreCase)
				|| c.Email.Contains(q, StringComparison.OrdinalIgnoreCase));
		}

		FilteredContacts.Clear();
		foreach (var c in items)
		{
			FilteredContacts.Add(c);
		}
	}

	partial void OnShowUnreadOnlyChanged(bool value) => ApplyFilter();

	private void SortContactsInPlace()
	{
		var sorted = Contacts
			.OrderByDescending(c => c.IsPinned)
			.ThenByDescending(c => c.UnreadCount > 0)
			.ThenByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
			.ThenBy(c => c.Nickname, StringComparer.OrdinalIgnoreCase)
			.ToList();

		Contacts.Clear();
		foreach (var item in sorted)
		{
			Contacts.Add(item);
		}
	}

	[RelayCommand]
	private void SetWallpaper(string key)
	{
		WallpaperKey = string.IsNullOrWhiteSpace(key) ? "default" : key;
	}

	[RelayCommand]
	private async Task SetWallpaperImageAsync(AssetOption option)
	{
		var fileName = option?.FileName;
		if (!IsKnownAsset(fileName, ImageAssets.WallpaperJpg))
		{
			StatusMessage = "Файл обоев не найден в наборе.";
			return;
		}

		try
		{
			var localPath = await CopyBundledAssetToAppDataAsync(fileName, "wallpapers");
			WallpaperImagePath = localPath;
			appearanceService.SetGlobalWallpaperPath(localPath);
			RefreshSelectionState();
			HapticFeedback.Default.Perform(HapticFeedbackType.Click);
			StatusMessage = null;
		}
		catch (Exception ex)
		{
			StatusMessage = $"Не удалось установить обои: {ex.Message}";
		}
	}

	[RelayCommand]
	private void ClearWallpaper()
	{
		WallpaperImagePath = null;
		appearanceService.SetGlobalWallpaperPath(null);
		RefreshSelectionState();
		StatusMessage = null;
	}

	[RelayCommand]
	private async Task SetNotificationGifAsync(AssetOption option)
	{
		var fileName = option?.FileName;
		if (!IsKnownAsset(fileName, ImageAssets.NotificationGif))
		{
			StatusMessage = "GIF не найден в наборе.";
			return;
		}

		try
		{
			var localPath = await CopyBundledAssetToAppDataAsync(fileName, "notifications");
			NotificationGifPath = localPath;
			appearanceService.SetNotificationGifPath(localPath);
			RefreshSelectionState();
			ApplyFilter();
			HapticFeedback.Default.Perform(HapticFeedbackType.Click);
			StatusMessage = null;
		}
		catch (Exception ex)
		{
			StatusMessage = $"Не удалось установить GIF: {ex.Message}";
		}
	}

	[RelayCommand]
	private void ClearNotificationGif()
	{
		NotificationGifPath = null;
		appearanceService.SetNotificationGifPath(null);
		RefreshSelectionState();
		ApplyFilter();
		StatusMessage = null;
	}

	[RelayCommand]
	private async Task ClearAssetCacheAsync()
	{
		var removed = await assetCacheService.ClearAssetCacheAsync();
		StatusMessage = $"Кэш очищен: {removed} файлов.";
	}

	[RelayCommand]
	private async Task RecoverAssetsAsync()
	{
		var recoveredWallpapers = await assetCacheService.RecoverMissingAssetsAsync(ImageAssets.WallpaperJpg, "wallpapers");
		var recoveredGifs = await assetCacheService.RecoverMissingAssetsAsync(ImageAssets.NotificationGif, "notifications");
		StatusMessage = $"Восстановлено: {recoveredWallpapers + recoveredGifs} файлов.";
	}

private async Task<string> CopyBundledAssetToAppDataAsync(string fileName, string folderName)
	=> await assetCacheService.EnsureBundledAssetAsync(fileName, folderName);

	[RelayCommand]
	private async Task PickAvatarAsync()
	{
		try
		{
			var result = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "Выберите аватарку",
				FileTypes = FilePickerFileType.Images
			});

			if (result is null)
			{
				return;
			}

			var extension = Path.GetExtension(result.FileName);
			if (string.IsNullOrWhiteSpace(extension))
			{
				extension = ".img";
			}

			var targetDir = Path.Combine(FileSystem.AppDataDirectory, "profile");
			Directory.CreateDirectory(targetDir);
			var targetPath = Path.Combine(targetDir, $"avatar{extension}");

			await using (var src = await result.OpenReadAsync())
			await using (var dst = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				await src.CopyToAsync(dst);
			}

			AvatarPath = targetPath;
		}
		catch (Exception ex)
		{
			StatusMessage = $"Не удалось выбрать аватар: {ex.Message}";
		}
	}
}
