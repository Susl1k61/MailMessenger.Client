using MailMessenger.Client.ViewModels;

namespace MailMessenger.Client.Views;

public partial class ChatPage : ContentPage, IQueryAttributable
{
	// Tracks whether the last WebView navigation was an emoji/sticker scheme —
	// we cancel it and handle it manually, so we must NOT also process it in Navigated.
	private bool _emojiHandled;

	public ChatPage(ChatViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		viewModel.Messages.CollectionChanged += OnMessagesChanged;
		viewModel.ScrollToEndRequested += OnScrollToEndRequested;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (BindingContext is ChatViewModel viewModel)
			viewModel.ApplyQueryAttributes(query);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (BindingContext is ChatViewModel viewModel)
		{
			viewModel.Messages.CollectionChanged -= OnMessagesChanged;
			viewModel.ScrollToEndRequested -= OnScrollToEndRequested;
			viewModel.Messages.CollectionChanged += OnMessagesChanged;
			viewModel.ScrollToEndRequested += OnScrollToEndRequested;
			viewModel.LoadHistoryCommand.Execute(null);
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (BindingContext is ChatViewModel viewModel)
		{
			viewModel.Messages.CollectionChanged -= OnMessagesChanged;
			viewModel.ScrollToEndRequested -= OnScrollToEndRequested;
			viewModel.Dispose();
		}
	}

	private void OnMessagesChanged(object? sender,
		System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		=> _ = ScrollToEndAsync();

	private void OnScrollToEndRequested(object? sender, EventArgs e)
		=> _ = ScrollToEndAsync();

	private async Task ScrollToEndAsync()
	{
		if (BindingContext is not ChatViewModel viewModel
			|| viewModel.FilteredMessages.Count == 0)
			return;

		var last = viewModel.FilteredMessages[^1];
		await Task.Delay(80);
		await Dispatcher.DispatchAsync(() =>
		{
			try { MessagesView.ScrollTo(last, position: ScrollToPosition.End, animate: false); }
			catch { /* layout not ready */ }
		});

		viewModel.MarkAsReadNowCommand.Execute(null);
	}

	// ─── Emoji WebView ─────────────────────────────────────────────────────

	/// <summary>
	/// Called BEFORE the WebView navigates.
	/// We intercept emoji:// and sticker:// here and cancel the navigation.
	/// On iOS the cancel alone is enough. On Android we also need Navigated as
	/// fallback because some WebView builds fire Navigated even after Cancel=true.
	/// </summary>
	private void OnEmojiWebViewNavigating(object? sender, WebNavigatingEventArgs e)
	{
		_emojiHandled = false;

		if (TryHandleEmojiUrl(e.Url))
		{
			e.Cancel = true;
			_emojiHandled = true;
		}
	}

	/// <summary>
	/// Android fallback: if the navigation was NOT already handled in Navigating,
	/// parse the final URL here (some Android WebView versions ignore Cancel=true).
	/// </summary>
	private void OnEmojiWebViewNavigated(object? sender, WebNavigatedEventArgs e)
	{
		if (_emojiHandled) return;
		TryHandleEmojiUrl(e.Url);
	}

	/// <summary>
	/// Parses emoji:// and sticker:// URLs, executes the corresponding command,
	/// then refocuses the message Entry. Returns true if the URL was handled.
	/// </summary>
	private bool TryHandleEmojiUrl(string url)
	{
		const string emojiScheme   = "emoji://";
		const string stickerScheme = "sticker://";

		if (url.StartsWith(emojiScheme, StringComparison.OrdinalIgnoreCase))
		{
			var emoji = Uri.UnescapeDataString(url[emojiScheme.Length..]);
			if (BindingContext is ChatViewModel vm)
			{
				vm.AddEmojiCommand.Execute(emoji);
				// Re-focus entry so keyboard stays open
				Dispatcher.Dispatch(() =>
				{
					MessageEntry?.Focus();
				});
			}
			return true;
		}

		if (url.StartsWith(stickerScheme, StringComparison.OrdinalIgnoreCase))
		{
			var sticker = Uri.UnescapeDataString(url[stickerScheme.Length..]);
			if (BindingContext is ChatViewModel vm)
				vm.SendStickerCommand.Execute(sticker);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Fires when the search Grid's IsVisible changes — auto-focuses the Entry.
	/// </summary>
	private void OnSearchBarPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(VisualElement.IsVisible)
			&& sender is Grid { IsVisible: true })
		{
			Dispatcher.Dispatch(() => SearchEntry?.Focus());
		}
	}
}
