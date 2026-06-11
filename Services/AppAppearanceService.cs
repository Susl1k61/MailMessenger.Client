using Microsoft.Maui.Storage;

namespace MailMessenger.Client.Services;

public sealed class AppAppearanceService : IAppAppearanceService
{
	private const string WallpaperImagePathPref = "wallpaper_image_path";
	private const string NotificationGifPathPref = "notification_gif_path";
	private const string WallpaperOpacityPref = "wallpaper_opacity";
	private const string BadgeThemePref = "badge_theme";
	private const string BadgeSizePref = "badge_size";

	public string? GetGlobalWallpaperPath()
	{
		var value = Preferences.Default.Get(WallpaperImagePathPref, string.Empty);
		return string.IsNullOrWhiteSpace(value) || !File.Exists(value) ? null : value;
	}

	public void SetGlobalWallpaperPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			Preferences.Default.Remove(WallpaperImagePathPref);
			return;
		}

		Preferences.Default.Set(WallpaperImagePathPref, path);
	}

	public string? GetNotificationGifPath()
	{
		var value = Preferences.Default.Get(NotificationGifPathPref, string.Empty);
		return string.IsNullOrWhiteSpace(value) || !File.Exists(value) ? null : value;
	}

	public void SetNotificationGifPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			Preferences.Default.Remove(NotificationGifPathPref);
			return;
		}

		Preferences.Default.Set(NotificationGifPathPref, path);
	}

	public double GetWallpaperDimOpacity()
		=> Math.Clamp(Preferences.Default.Get(WallpaperOpacityPref, 0.12), 0.05, 0.35);

	public void SetWallpaperDimOpacity(double value)
		=> Preferences.Default.Set(WallpaperOpacityPref, Math.Clamp(value, 0.05, 0.35));

	public string GetBadgeTheme()
	{
		var value = Preferences.Default.Get(BadgeThemePref, "red");
		return string.IsNullOrWhiteSpace(value) ? "red" : value;
	}

	public void SetBadgeTheme(string theme)
		=> Preferences.Default.Set(BadgeThemePref, string.IsNullOrWhiteSpace(theme) ? "red" : theme);

	public double GetBadgeSize()
		=> Math.Clamp(Preferences.Default.Get(BadgeSizePref, 20.0), 16, 28);

	public void SetBadgeSize(double size)
		=> Preferences.Default.Set(BadgeSizePref, Math.Clamp(size, 16, 28));
}

