using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailMessenger.Client.Services;

namespace MailMessenger.Client.ViewModels;

public partial class LoginViewModel : ObservableObject
{
	private readonly ISecureCredentialStore credentialStore;
	private readonly IMailService mailService;

	public LoginViewModel(ISecureCredentialStore credentialStore, IMailService mailService)
	{
		this.credentialStore = credentialStore;
		this.mailService = mailService;
	}

	[ObservableProperty]
	private string email = string.Empty;

	[ObservableProperty]
	private string appPassword = string.Empty;

	[ObservableProperty]
	private bool isBusy;

	[ObservableProperty]
	private string? errorMessage;

	public string AppPasswordHelpText { get; } =
		"Создайте пароль приложения в настройках Яндекс ID:\n" + Constants.YandexAppPasswordHelpUrl;

	[RelayCommand]
	private async Task LoginAsync()
	{
		if (IsBusy)
		{
			return;
		}

		ErrorMessage = null;
		var normalizedEmail = Email.Trim().ToLowerInvariant();

		if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(AppPassword))
		{
			ErrorMessage = "Введите email и пароль приложения.";
			return;
		}

		try
		{
			IsBusy = true;
			var verified = await mailService.VerifyCredentialsAsync(normalizedEmail, AppPassword);
			if (!verified)
			{
				ErrorMessage = "Неверный email или пароль приложения. Проверьте данные и создайте пароль приложения в Яндекс ID.";
				return;
			}

			await credentialStore.SaveAsync(normalizedEmail, AppPassword);
			AppPassword = string.Empty;

			_ = mailService.StartReceivingAsync(CancellationToken.None);
			await Shell.Current.GoToAsync("//contacts");
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
}
