namespace MailMessenger.Client.Services;

public interface IAssetCacheService
{
	Task<string> EnsureBundledAssetAsync(string fileName, string folderName);
	Task<string?> EnsureBundledPreviewAsync(string fileName, string folderName);
	Task<int> ClearAssetCacheAsync();
	Task<int> RecoverMissingAssetsAsync(IEnumerable<string> expectedFileNames, string folderName);
}

