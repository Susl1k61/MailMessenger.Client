namespace MailMessenger.Client.Services;

public interface IAppAppearanceService
{
	string? GetGlobalWallpaperPath();
	void SetGlobalWallpaperPath(string? path);

	string? GetNotificationGifPath();
	void SetNotificationGifPath(string? path);

	double GetWallpaperDimOpacity();
	void SetWallpaperDimOpacity(double value);

	string GetBadgeTheme();
	void SetBadgeTheme(string theme);

	double GetBadgeSize();
	void SetBadgeSize(double size);
}

