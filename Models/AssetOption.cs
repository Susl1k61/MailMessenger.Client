using CommunityToolkit.Mvvm.ComponentModel;

namespace MailMessenger.Client.Models;

public partial class AssetOption : ObservableObject
{
	public string FileName { get; init; } = string.Empty;

	public string Title { get; init; } = string.Empty;

	[ObservableProperty]
	private string? previewPath;

	[ObservableProperty]
	private bool isSelected;
}

