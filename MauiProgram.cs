using Microsoft.Extensions.Logging;
using MailMessenger.Client.Services;
using MailMessenger.Client.ViewModels;
using MailMessenger.Client.Views;

namespace MailMessenger.Client;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Logging.SetMinimumLevel(LogLevel.Debug);
#if DEBUG
		builder.Logging.AddDebug();
#endif
		builder.Services.AddSingleton<ISecureCredentialStore, SecureCredentialStore>();
		builder.Services.AddSingleton<ILocalDb, LocalDb>();
		builder.Services.AddSingleton<IMailService, MailService>();
		builder.Services.AddSingleton<IAppAppearanceService, AppAppearanceService>();
		builder.Services.AddSingleton<IAssetCacheService, AssetCacheService>();

		builder.Services.AddSingleton<LoginViewModel>();
		builder.Services.AddSingleton<ContactsViewModel>();
		builder.Services.AddTransient<ChatViewModel>();

		builder.Services.AddSingleton<LoginPage>();
		builder.Services.AddSingleton<ContactsPage>();
		builder.Services.AddTransient<ChatPage>();
		builder.Services.AddSingleton<AppShell>();

		return builder.Build();
	}
}
