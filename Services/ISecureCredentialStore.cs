namespace MailMessenger.Client.Services;

public interface ISecureCredentialStore
{
	Task SaveAsync(string email, string appPassword);

	Task<(string Email, string Password)?> LoadAsync();

	void Clear();

	bool HasCredentials { get; }
}
