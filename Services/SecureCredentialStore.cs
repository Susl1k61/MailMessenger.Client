namespace MailMessenger.Client.Services;

public sealed class SecureCredentialStore : ISecureCredentialStore
{
	private bool usePreferencesFallback;

	public bool HasCredentials => Preferences.Get(Constants.HasCredentialsPreferenceKey, false);

	public async Task SaveAsync(string email, string appPassword)
	{
		var normalizedEmail = email.Trim().ToLowerInvariant();
		await SetSecretAsync(Constants.CredentialEmailKey, normalizedEmail);
		await SetSecretAsync(Constants.CredentialPasswordKey, appPassword);
		Preferences.Set(Constants.HasCredentialsPreferenceKey, true);
	}

	public async Task<(string Email, string Password)?> LoadAsync()
	{
		if (!HasCredentials)
		{
			return null;
		}

		var email = await GetSecretAsync(Constants.CredentialEmailKey);
		var password = await GetSecretAsync(Constants.CredentialPasswordKey);
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
		{
			Clear();
			return null;
		}

		return (email, password);
	}

	public void Clear()
	{
		RemoveSecret(Constants.CredentialEmailKey);
		RemoveSecret(Constants.CredentialPasswordKey);
		Preferences.Remove(Constants.HasCredentialsPreferenceKey);
		Preferences.Remove(Constants.CredentialStorageModeKey);
		usePreferencesFallback = false;
	}

	private async Task SetSecretAsync(string key, string value)
	{
		if (usePreferencesFallback)
		{
			Preferences.Set(key, value);
			return;
		}

		try
		{
			await SecureStorage.SetAsync(key, value);
			Preferences.Set(Constants.CredentialStorageModeKey, Constants.StorageModeSecure);
		}
		catch (Exception)
		{
			usePreferencesFallback = true;
			Preferences.Set(Constants.CredentialStorageModeKey, Constants.StorageModePreferences);
			Preferences.Set(key, value);
		}
	}

	private async Task<string?> GetSecretAsync(string key)
	{
		if (usePreferencesFallback
			|| Preferences.Get(Constants.CredentialStorageModeKey, Constants.StorageModeSecure)
				== Constants.StorageModePreferences)
		{
			usePreferencesFallback = true;
			var value = Preferences.Get(key, string.Empty);
			return string.IsNullOrWhiteSpace(value) ? null : value;
		}

		try
		{
			return await SecureStorage.GetAsync(key);
		}
		catch (Exception)
		{
			usePreferencesFallback = true;
			Preferences.Set(Constants.CredentialStorageModeKey, Constants.StorageModePreferences);
			var value = Preferences.Get(key, string.Empty);
			return string.IsNullOrWhiteSpace(value) ? null : value;
		}
	}

	private static void RemoveSecret(string key)
	{
		try
		{
			SecureStorage.Remove(key);
		}
		catch (Exception)
		{
			// ignored
		}

		Preferences.Remove(key);
	}
}
