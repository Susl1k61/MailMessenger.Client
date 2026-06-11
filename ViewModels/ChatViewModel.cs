using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailMessenger.Client.Models;
using MailMessenger.Client.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace MailMessenger.Client.ViewModels;

public partial class ChatViewModel : ObservableObject, IQueryAttributable, IDisposable
{
	private const string WallpaperKeyPref = "wallpaper_key";

	private readonly IMailService mailService;
	private readonly ILocalDb localDb;
	private readonly IAppAppearanceService appearanceService;
	private readonly List<FileResult> pendingAttachments = [];

	public ChatViewModel(IMailService mailService, ILocalDb localDb, IAppAppearanceService appearanceService)
	{
		this.mailService = mailService;
		this.localDb = localDb;
		this.appearanceService = appearanceService;
		mailService.MessageReceived += OnMessageReceived;
		mailService.ConnectionStateChanged += OnConnectionStateChanged;

		WallpaperImagePath = appearanceService.GetGlobalWallpaperPath();
		WallpaperDimOpacity = appearanceService.GetWallpaperDimOpacity();
	}

	public ObservableCollection<ChatMessage> Messages { get; } = [];

	/// <summary>Filtered view of Messages used when search is active.</summary>
	public ObservableCollection<ChatMessage> FilteredMessages { get; } = [];

	[ObservableProperty]
	private string peerEmail = string.Empty;

	[ObservableProperty]
	private string peerNickname = string.Empty;

	[ObservableProperty]
	private string messageText = string.Empty;

	[ObservableProperty]
	private bool isBusy;

	[ObservableProperty]
	private string? errorMessage;

	[ObservableProperty]
	private string wallpaperKey = Preferences.Default.Get(WallpaperKeyPref, "default");

	[ObservableProperty]
	private string? wallpaperImagePath;

	[ObservableProperty]
	private string? chatWallpaperPath;

	[ObservableProperty]
	private double wallpaperDimOpacity;

	[ObservableProperty]
	private bool isEmojiPanelVisible;

	[ObservableProperty]
	private int attachmentCount;

	/// <summary>True = IMAP is connected. Drives the status label in the chat header.</summary>
	[ObservableProperty]
	private bool isConnected;

	/// <summary>Human-readable status shown under the peer name in the header.</summary>
	[ObservableProperty]
	private string connectionStatus = "connecting…";

	/// <summary>True while the in-chat search bar is visible.</summary>
	[ObservableProperty]
	private bool isSearchVisible;

	/// <summary>Current search query typed by the user.</summary>
	[ObservableProperty]
	private string searchQuery = string.Empty;

	partial void OnSearchQueryChanged(string value) => ApplyMessageFilter(value);
	partial void OnIsSearchVisibleChanged(bool value)
	{
		if (!value)
		{
			SearchQuery = string.Empty;
			RebuildFilteredMessages(string.Empty);
		}
	}

	private void ApplyMessageFilter(string query)
	{
		RebuildFilteredMessages(query);
	}

	private void RebuildFilteredMessages(string query)
	{
		FilteredMessages.Clear();
		var source = string.IsNullOrWhiteSpace(query)
			? Messages
			: Messages.Where(m => m.Text.Contains(query, StringComparison.OrdinalIgnoreCase));
		foreach (var m in source)
			FilteredMessages.Add(m);
	}

	private void OnConnectionStateChanged(bool connected)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			IsConnected = connected;
			ConnectionStatus = connected ? "online" : "offline";
		});
	}

	/// <summary>
	/// HTML for the emoji + sticker picker panel.
	/// Uses Twemoji 14 CDN for emoji rendering. Tabs: Смайлы | Жесты | Природа | Еда | Стикеры.
	/// Navigation scheme: emoji://&lt;encoded&gt; for emoji, sticker://&lt;encoded&gt; for sticker text.
	/// </summary>
	public string EmojiPanelHtml { get; } = BuildEmojiPanelHtml();

	private static string BuildEmojiPanelHtml()
	{
		// ── emoji categories ──────────────────────────────────────────────────
		var smileys = new[]
		{
			"😀","😁","😂","🤣","😃","😄","😅","😆","😉","😊",
			"😋","😎","😍","🥰","😘","🥲","😗","😙","😚","🙂",
			"🤗","🤩","🤔","🫡","🤐","🥴","😐","😑","😶","🫥",
			"😏","😒","🙄","😬","🤥","🫨","😔","😪","🤤","😴",
			"😷","🤒","🤕","🤢","🤮","🤧","🥵","🥶","🥺","😦",
			"😧","😨","😰","😥","😢","😭","😱","😖","😣","😞",
			"😓","😩","😫","😤","😠","😡","🤬","😈","👿","💀",
			"☠️","💩","🤡","👻","👽","👾","🤖","😺","😸","😹"
		};

		var gestures = new[]
		{
			"👍","👎","👏","🙌","🤝","🫶","👐","🤲","🙏","🤜",
			"🤛","✊","👊","🤞","✌️","🤟","🤘","👌","🤌","🤏",
			"👋","🤚","🖐️","✋","🖖","💪","🦾","🙋","🤷","🤦",
			"💅","🫰","👈","👉","👆","👇","☝️","🫵","🤙","🖕"
		};

		var nature = new[]
		{
			"🐶","🐱","🐭","🐹","🐰","🦊","🐻","🐼","🐨","🐯",
			"🦁","🐮","🐷","🐸","🐵","🐔","🐧","🐦","🦅","🦆",
			"🦉","🦋","🐛","🐝","🌸","🌺","🌻","🌹","🍀","🌿",
			"🌱","🌲","🌴","🌵","⭐","🌟","✨","🌈","☀️","🌙",
			"⚡","🌊","🔥","❄️","🌸","🍄","🌏","🪐","🌌","🎑"
		};

		var food = new[]
		{
			"🍎","🍊","🍋","🍇","🍓","🍒","🍑","🥭","🍕","🍔",
			"🌮","🌯","🥪","🍜","🍣","🍱","🍛","🍲","🥘","🍗",
			"🍖","🥩","🧆","🥚","🍳","🥞","🧇","🥓","🍟","🌭",
			"🍦","🍧","🍨","🍰","🎂","🧁","🍩","🍪","☕","🧃",
			"🥤","🍵","🧋","🍺","🍻","🥂","🍾","🫖","🍶","🥛"
		};

		// ── sticker packs (sent as big emoji text) ────────────────────────────
		var stickers = new[]
		{
			// Pepe-style moods
			("🐸😂","KEK"),("🐸❤️","Luv"),("🐸😭","Cry"),("🐸😤","Angry"),
			("🐸🥳","Party"),("🐸🤔","Think"),("🐸👍","OK"),("🐸😴","Sleep"),
			// Cute animals
			("🐱✨","Magic"),("🐶💕","Puppy"),("🐼🎋","Panda"),("🦊🔥","Fox"),
			// Reactions
			("💯🔥","Lit"),("🚀✨","Go!"),("😤💪","Grind"),("🤌👌","Chef"),
			// Vibes
			("🌙💜","Night"),("☀️🌈","Vibe"),("🎵🎶","Music"),("💀😂","Ded"),
			// Love
			("❤️🔥","Rizz"),("🫶✨","Love"),("💍💕","Wed"),("🥰🌸","Cute"),
		};

		var sb = new System.Text.StringBuilder();
		sb.Append(@"<!DOCTYPE html>
<html>
<head>
<meta name='viewport' content='width=device-width,initial-scale=1,maximum-scale=1'>
<script src='https://cdnjs.cloudflare.com/ajax/libs/twemoji/14.0.2/twemoji.min.js' crossorigin='anonymous'></script>
<style>
  *{margin:0;padding:0;box-sizing:border-box;-webkit-tap-highlight-color:transparent}
  html,body{height:100%;background:#0D0D0D;color:#F3F4F6;font-family:-apple-system,system-ui,sans-serif;overflow:hidden}
  /* ── tabs ── */
  .tabs{display:flex;background:#0D0D0D;border-bottom:1px solid rgba(255,255,255,0.05);position:sticky;top:0;z-index:10}
  .tab{flex:1;padding:10px 4px;text-align:center;font-size:11px;color:#6B7280;cursor:pointer;border:none;
       background:transparent;transition:color .15s;white-space:nowrap;overflow:hidden}
  .tab.active{color:#8B5CF6;border-bottom:2px solid #7C3AED}
  .tab span{font-size:18px;display:block;margin-bottom:2px}
  /* ── panels ── */
  .panel{display:none;height:244px;overflow-y:auto;padding:8px 6px}
  .panel.active{display:flex;flex-wrap:wrap;gap:4px;align-content:flex-start}
  /* ── emoji buttons  bg-[#1A1A2E] rounded-xl ── */
  .e{width:44px;height:44px;display:flex;align-items:center;justify-content:center;
     background:#1A1A2E;border-radius:12px;cursor:pointer;border:1px solid rgba(255,255,255,0.05);
     font-size:26px;flex-shrink:0;transition:background .12s,transform .1s}
  .e:active{background:rgba(139,92,246,0.30);transform:scale(.88)}
  .e img{width:28px;height:28px;pointer-events:none}
  /* ── sticker grid ── */
  .sticker-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:8px;padding:10px;width:100%}
  .stk{display:flex;flex-direction:column;align-items:center;background:#1A1A2E;border-radius:14px;
       padding:12px 6px 8px;cursor:pointer;border:1px solid rgba(255,255,255,0.05);gap:5px;
       transition:background .12s,transform .1s}
  .stk:active{background:rgba(139,92,246,0.30);transform:scale(.92)}
  .stk-face{font-size:34px;line-height:1}
  .stk-face img{width:36px;height:36px}
  .stk-label{font-size:10px;color:#8B5CF6;font-weight:700}
  /* ── scrollbar ── */
  ::-webkit-scrollbar{width:4px}
  ::-webkit-scrollbar-track{background:transparent}
  ::-webkit-scrollbar-thumb{background:rgba(139,92,246,0.20);border-radius:4px}
</style>
</head>
<body>
<div class='tabs'>
  <button class='tab active' onclick='show(0)'><span>😀</span>Smileys</button>
  <button class='tab' onclick='show(1)'><span>👋</span>Gestures</button>
  <button class='tab' onclick='show(2)'><span>🌸</span>Nature</button>
  <button class='tab' onclick='show(3)'><span>🍕</span>Food</button>
  <button class='tab' onclick='show(4)'><span>🎭</span>Stickers</button>
</div>
");

		// Helper: write emoji panel
		void WriteEmojiPanel(string id, string[] list, bool active)
		{
			sb.Append($"<div id='{id}' class='panel{(active ? " active" : "")}'>");
			foreach (var e in list)
			{
				var enc = Uri.EscapeDataString(e);
				sb.Append($"<button class='e' onclick=\"send('emoji://{enc}')\">{e}</button>");
			}
			sb.Append("</div>");
		}

		WriteEmojiPanel("p0", smileys,   true);
		WriteEmojiPanel("p1", gestures,  false);
		WriteEmojiPanel("p2", nature,    false);
		WriteEmojiPanel("p3", food,      false);

		// Sticker panel
		sb.Append("<div id='p4' class='panel'><div class='sticker-grid'>");
		foreach (var (face, label) in stickers)
		{
			var enc = Uri.EscapeDataString(face);
			sb.Append($"<button class='stk' onclick=\"send('sticker://{enc}')\"><span class='stk-face'>{face}</span><span class='stk-label'>{label}</span></button>");
		}
		sb.Append("</div></div>");

		sb.Append(@"
<script>
var panels=['p0','p1','p2','p3','p4'];
var tabs=document.querySelectorAll('.tab');
function show(i){
  panels.forEach(function(id,j){
    var el=document.getElementById(id);
    el.className='panel'+(j===i?' active':'');
    tabs[j].className='tab'+(j===i?' active':'');
  });
}
function send(url){location.href=url;}
// Twemoji parse all emoji in panels
twemoji.parse(document.body,{folder:'svg',ext:'.svg'});
</script>
</body></html>");

		return sb.ToString();
	}

	partial void OnWallpaperKeyChanged(string value)
	{
		// Settings are currently stored globally in preferences.
		Preferences.Default.Set(WallpaperKeyPref, value ?? "default");
	}

	[RelayCommand]
	private async Task SendStickerAsync(string stickerText)
	{
		if (IsBusy || string.IsNullOrWhiteSpace(PeerEmail) || string.IsNullOrWhiteSpace(stickerText))
		{
			return;
		}

		try
		{
			IsBusy = true;
			ErrorMessage = null;
			// Send sticker as a special oversized emoji message with a marker
			var sent = await mailService.SendAsync(PeerEmail, stickerText);
			Messages.Add(sent);
			IsEmojiPanelVisible = false;
			ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex)
		{
			ErrorMessage = MailErrorMessages.FromException(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private void ToggleSearch()
	{
		IsSearchVisible = !IsSearchVisible;
		if (IsSearchVisible)
			RebuildFilteredMessages(SearchQuery);
	}

	[RelayCommand]
	private static async Task GoBackAsync()
	{
		await Shell.Current.GoToAsync("..");
	}

	[RelayCommand]
	private async Task ShowMoreMenuAsync()
	{
		var action = await Shell.Current.DisplayActionSheet(
			null,
			"Отмена",
			null,
			"🖼  Обои чата",
			"✏️  Переименовать");

		switch (action)
		{
			case "🖼  Обои чата":
				await PickChatWallpaperAsync();
				break;

			case "✏️  Переименовать":
				await RenamePeerAsync();
				break;
		}
	}

	[RelayCommand]
	private async Task RenamePeerAsync()
	{
		var newName = await Shell.Current.DisplayPromptAsync(
			"Переименовать",
			"Введите новое имя собеседника:",
			"Сохранить",
			"Отмена",
			placeholder: PeerNickname,
			initialValue: PeerNickname);

		if (string.IsNullOrWhiteSpace(newName) || newName == PeerNickname)
		{
			return;
		}

		try
		{
			await localDb.UpsertContactAsync(PeerEmail, newName.Trim());
			PeerNickname = newName.Trim();
		}
		catch (Exception ex)
		{
			ErrorMessage = $"Не удалось переименовать: {ex.Message}";
		}
	}

	[RelayCommand]
	private void ToggleEmojiPanel()
	{
		IsEmojiPanelVisible = !IsEmojiPanelVisible;
	}

	[RelayCommand]
	private void AddEmoji(string emoji)
	{
		if (string.IsNullOrWhiteSpace(emoji))
		{
			return;
		}

		MessageText = (MessageText ?? string.Empty) + emoji;
	}

	[RelayCommand]
	private async Task PickAttachmentsAsync()
	{
		try
		{
			var picked = await FilePicker.Default.PickMultipleAsync(new PickOptions
			{
				PickerTitle = "Выберите файл(ы) для отправки"
			});

			if (picked is null)
			{
				return;
			}

			pendingAttachments.Clear();
			pendingAttachments.AddRange(picked);
			AttachmentCount = pendingAttachments.Count;
			ErrorMessage = null;
		}
		catch (Exception ex)
		{
			ErrorMessage = $"Не удалось выбрать вложение: {ex.Message}";
		}
	}

	[RelayCommand]
	private async Task OpenAttachmentsAsync(ChatMessage message)
	{
		if (message is null)
		{
			return;
		}

		try
		{
			var attachments = await localDb.GetAttachmentsAsync(message.Id);
			if (attachments.Count == 0)
			{
				await Shell.Current.DisplayAlert("Файлы", "В этом сообщении нет файлов.", "OK");
				return;
			}

			MessageAttachment? selected = null;
			if (attachments.Count == 1)
			{
				selected = attachments[0];
			}
			else
			{
				var names = attachments.Select(a => a.FileName).ToArray();
				var pick = await Shell.Current.DisplayActionSheet("Файлы", "Отмена", null, names);
				if (string.IsNullOrWhiteSpace(pick) || pick == "Отмена")
				{
					return;
				}

				selected = attachments.FirstOrDefault(a => a.FileName == pick);
			}

			if (selected is null || string.IsNullOrWhiteSpace(selected.LocalPath) || !File.Exists(selected.LocalPath))
			{
				await Shell.Current.DisplayAlert("Файл", "Файл не найден на устройстве.", "OK");
				return;
			}

			var action = await Shell.Current.DisplayActionSheet(
				selected.FileName,
				"Отмена",
				null,
				"Открыть",
				"Поделиться");

			if (action == "Открыть")
			{
				// На Android это также запускает установщик для .apk и т.п.
				await Launcher.Default.OpenAsync(new OpenFileRequest
				{
					File = new ReadOnlyFile(selected.LocalPath)
				});
			}
			else if (action == "Поделиться")
			{
				await Share.Default.RequestAsync(new ShareFileRequest
				{
					Title = selected.FileName,
					File = new ShareFile(selected.LocalPath)
				});
			}
		}
		catch (Exception ex)
		{
			ErrorMessage = $"Не удалось открыть файл: {ex.Message}";
		}
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("peerEmail", out var emailValue))
		{
			PeerEmail = Uri.UnescapeDataString(emailValue.ToString() ?? string.Empty).Trim().ToLowerInvariant();
		}

		if (query.TryGetValue("peerNickname", out var nicknameValue))
		{
			PeerNickname = Uri.UnescapeDataString(nicknameValue.ToString() ?? string.Empty);
		}
		else if (!string.IsNullOrWhiteSpace(PeerEmail))
		{
			PeerNickname = PeerEmail;
		}

		_ = LoadChatWallpaperAsync();
	}

	public event EventHandler? ScrollToEndRequested;

	public void Dispose()
	{
		mailService.MessageReceived -= OnMessageReceived;
		mailService.ConnectionStateChanged -= OnConnectionStateChanged;
	}

	[RelayCommand]
	private async Task LoadHistoryAsync()
	{
		if (string.IsNullOrWhiteSpace(PeerEmail))
		{
			return;
		}

		try
		{
			IsBusy = true;
			ErrorMessage = null;
			var history = await localDb.GetHistoryAsync(PeerEmail);
			Messages.Clear();
			foreach (var message in history)
			{
				Messages.Add(message);
			}
			RebuildFilteredMessages(SearchQuery);
		}
		catch (Exception ex)
		{
			ErrorMessage = $"Не удалось загрузить историю: {ex.Message}";
		}
		finally
		{
			IsBusy = false;
			ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
		}
	}

	[RelayCommand]
	private async Task MarkAsReadNowAsync()
	{
		if (string.IsNullOrWhiteSpace(PeerEmail))
		{
			return;
		}

		try
		{
			await mailService.MarkAsReadAsync(PeerEmail);
		}
		catch
		{
			// best-effort
		}
	}

	[RelayCommand]
	private async Task PickChatWallpaperAsync()
	{
		if (string.IsNullOrWhiteSpace(PeerEmail))
		{
			return;
		}

		try
		{
			var result = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "Обои для этого чата",
				FileTypes = FilePickerFileType.Images
			});

			if (result is null)
			{
				return;
			}

			var ext = Path.GetExtension(result.FileName);
			if (string.IsNullOrWhiteSpace(ext))
			{
				ext = ".img";
			}

			var safeEmail = PeerEmail.Replace("@", "_at_").Replace(".", "_");
			var targetDir = Path.Combine(FileSystem.AppDataDirectory, "chat-wallpapers");
			Directory.CreateDirectory(targetDir);
			var targetPath = Path.Combine(targetDir, $"{safeEmail}{ext}");

			await using (var src = await result.OpenReadAsync())
			await using (var dst = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				await src.CopyToAsync(dst);
			}

			await localDb.SetChatWallpaperAsync(PeerEmail, targetPath);
			ChatWallpaperPath = targetPath;
			WallpaperImagePath = targetPath;
		}
		catch (Exception ex)
		{
			ErrorMessage = $"Не удалось установить обои чата: {ex.Message}";
		}
	}

	[RelayCommand]
	private async Task ResetChatWallpaperAsync()
	{
		if (string.IsNullOrWhiteSpace(PeerEmail))
		{
			return;
		}

		await localDb.SetChatWallpaperAsync(PeerEmail, null);
		ChatWallpaperPath = null;
		WallpaperImagePath = appearanceService.GetGlobalWallpaperPath();
	}

	[RelayCommand]
	private async Task DeleteMessageAsync(ChatMessage message)
	{
		if (message is null)
		{
			return;
		}

		var confirm = await Shell.Current.DisplayAlert(
			"Удалить сообщение",
			"Удалить выбранное сообщение?",
			"Удалить",
			"Отмена");

		if (!confirm)
		{
			return;
		}

		try
		{
			ErrorMessage = null;
			await localDb.DeleteMessageAsync(message.Id);
			Messages.Remove(message);
		}
		catch (Exception ex)
		{
			ErrorMessage = $"Не удалось удалить сообщение: {ex.Message}";
		}
	}

	[RelayCommand]
	private async Task SendMessageAsync()
	{
		if (IsBusy || string.IsNullOrWhiteSpace(PeerEmail))
		{
			return;
		}

		try
		{
			IsBusy = true;
			ErrorMessage = null;
			var text = MessageText ?? string.Empty;
			ChatMessage sent;
			if (pendingAttachments.Count > 0)
			{
				sent = await mailService.SendWithAttachmentsAsync(PeerEmail, text, pendingAttachments);
			}
			else
			{
				if (string.IsNullOrWhiteSpace(text))
				{
					return;
				}

				sent = await mailService.SendAsync(PeerEmail, text);
			}

			Messages.Add(sent);
			MessageText = string.Empty;
			pendingAttachments.Clear();
			AttachmentCount = 0;
			IsEmojiPanelVisible = false;
			ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex)
		{
			ErrorMessage = MailErrorMessages.FromException(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private void OnMessageReceived(ChatMessage message)
	{
		if (!string.Equals(message.PeerEmail, PeerEmail, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (Messages.Any(m => m.Id == message.Id))
			{
				return;
			}

			Messages.Add(message);
			RebuildFilteredMessages(SearchQuery);
			ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
		});
	}

	private async Task LoadChatWallpaperAsync()
	{
		if (string.IsNullOrWhiteSpace(PeerEmail))
		{
			return;
		}

		var perChat = await localDb.GetChatWallpaperAsync(PeerEmail);
		if (!string.IsNullOrWhiteSpace(perChat) && File.Exists(perChat))
		{
			ChatWallpaperPath = perChat;
			WallpaperImagePath = perChat;
			return;
		}

		ChatWallpaperPath = null;
		WallpaperImagePath = appearanceService.GetGlobalWallpaperPath();
	}
}
