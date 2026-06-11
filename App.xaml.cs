using MailMessenger.Client.Services;

namespace MailMessenger.Client;

public partial class App : Application
{
	private readonly AppShell appShell;
	private readonly IMailService mailService;

	public App(AppShell appShell, IMailService mailService)
	{
		InitializeComponent();
		this.appShell = appShell;
		this.mailService = mailService;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(appShell);
		window.Destroying += OnWindowDestroying;
		return window;
	}

	private async void OnWindowDestroying(object? sender, EventArgs e)
	{
		if (sender is Window window)
		{
			window.Destroying -= OnWindowDestroying;
		}

		try
		{
			await mailService.StopReceivingAsync();
		}
		catch (Exception)
		{
			// ignored on shutdown
		}
	}
}
