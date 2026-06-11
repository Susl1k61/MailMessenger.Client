using Microsoft.Maui.Storage;

namespace MailMessenger.Client.Services;

public sealed class AssetCacheService : IAssetCacheService
{
	public async Task<string> EnsureBundledAssetAsync(string fileName, string folderName)
	{
		var targetDir = Path.Combine(FileSystem.AppDataDirectory, folderName);
		Directory.CreateDirectory(targetDir);
		var targetPath = Path.Combine(targetDir, fileName);

		if (!File.Exists(targetPath))
		{
			await using var src = await FileSystem.OpenAppPackageFileAsync(fileName);
			await using var dst = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
			await src.CopyToAsync(dst);
		}

		return targetPath;
	}

	public async Task<string?> EnsureBundledPreviewAsync(string fileName, string folderName)
	{
		try
		{
			return await EnsureBundledAssetAsync(fileName, folderName);
		}
		catch
		{
			return null;
		}
	}

	public Task<int> ClearAssetCacheAsync()
	{
		var root = FileSystem.AppDataDirectory;
		var folders = new[]
		{
			"wallpapers",
			"notifications",
			"wallpapers_previews",
			"notifications_previews",
			"chat-wallpapers"
		};

		var removed = 0;
		foreach (var folder in folders)
		{
			var path = Path.Combine(root, folder);
			if (!Directory.Exists(path))
			{
				continue;
			}

			foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
			{
				try
				{
					File.Delete(file);
					removed++;
				}
				catch
				{
					// skip locked files
				}
			}
		}

		return Task.FromResult(removed);
	}

	public async Task<int> RecoverMissingAssetsAsync(IEnumerable<string> expectedFileNames, string folderName)
	{
		var recovered = 0;
		foreach (var file in expectedFileNames.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			var targetDir = Path.Combine(FileSystem.AppDataDirectory, folderName);
			Directory.CreateDirectory(targetDir);
			var targetPath = Path.Combine(targetDir, file);
			if (File.Exists(targetPath))
			{
				continue;
			}

			try
			{
				await EnsureBundledAssetAsync(file, folderName);
				recovered++;
			}
			catch
			{
				// ignore broken package entry
			}
		}

		return recovered;
	}
}

