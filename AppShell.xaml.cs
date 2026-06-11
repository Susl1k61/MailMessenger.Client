using MailMessenger.Client.Services;
using MailMessenger.Client.Views;

namespace MailMessenger.Client;

public partial class AppShell : Shell
{
	private readonly ISecureCredentialStore credentialStore;
	private readonly ILocalDb localDb;
	private readonly IMailService mailService;
	private bool initialNavigationDone;

	public AppShell(
		ISecureCredentialStore credentialStore,
		ILocalDb localDb,
		IMailService mailService)
	{
		InitializeComponent();
		this.credentialStore = credentialStore;
		this.localDb = localDb;
		this.mailService = mailService;

		Routing.RegisterRoute("chat", typeof(ChatPage));

		Loaded += OnShellLoaded;
	}

	private async void OnShellLoaded(object? sender, EventArgs e)
	{
		if (initialNavigationDone)
		{
			return;
		}

		initialNavigationDone = true;
		Loaded -= OnShellLoaded;

		await localDb.InitAsync();

		if (credentialStore.HasCredentials)
		{
			await GoToAsync("//contacts");
			// Запуск приёма после инициализации UI и БД.
			_ = Task.Run(async () =>
			{
				await Task.Delay(300);
				await mailService.StartReceivingAsync(CancellationToken.None);
			});
		}
		else
		{
			await GoToAsync("//login");
		}
	}
}
