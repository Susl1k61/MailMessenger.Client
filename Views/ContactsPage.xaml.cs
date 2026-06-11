using MailMessenger.Client.ViewModels;

namespace MailMessenger.Client.Views;

public partial class ContactsPage : ContentPage
{
	private bool isAnimatingSheet;
	private double panStartTranslation;

	public ContactsPage(ContactsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		SettingsSheet.TranslationY = 400;
		SettingsBackdrop.Opacity = 0;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (BindingContext is ContactsViewModel viewModel)
		{
			viewModel.PropertyChanged -= OnViewModelPropertyChanged;
			viewModel.PropertyChanged += OnViewModelPropertyChanged;
			viewModel.LoadContactsCommand.Execute(null);
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (BindingContext is ContactsViewModel viewModel)
		{
			viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		}
	}

	private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ContactsViewModel.IsSettingsOpen)
			|| BindingContext is not ContactsViewModel vm
			|| isAnimatingSheet)
		{
			return;
		}

		isAnimatingSheet = true;
		try
		{
			if (vm.IsSettingsOpen)
			{
				SettingsBackdrop.IsVisible = true;
				SettingsSheet.IsVisible = true;
				SettingsSheet.TranslationY = 400;
				SettingsBackdrop.Opacity = 0;
				await Task.WhenAll(
					SettingsSheet.TranslateTo(0, 0, 220, Easing.CubicOut),
					SettingsBackdrop.FadeTo(1, 180, Easing.CubicOut));
			}
			else
			{
				await Task.WhenAll(
					SettingsSheet.TranslateTo(0, 420, 180, Easing.CubicIn),
					SettingsBackdrop.FadeTo(0, 140, Easing.CubicIn));
				SettingsBackdrop.IsVisible = false;
				SettingsSheet.IsVisible = false;
			}
		}
		finally
		{
			isAnimatingSheet = false;
		}
	}

	private void OnSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
	{
		if (BindingContext is not ContactsViewModel vm || !vm.IsSettingsOpen)
		{
			return;
		}

		switch (e.StatusType)
		{
			case GestureStatus.Started:
				panStartTranslation = SettingsSheet.TranslationY;
				break;

			case GestureStatus.Running:
				var next = Math.Max(0, panStartTranslation + e.TotalY);
				SettingsSheet.TranslationY = next;
				SettingsBackdrop.Opacity = Math.Max(0.25, 1 - (next / 420));
				break;

			case GestureStatus.Completed:
			case GestureStatus.Canceled:
				if (SettingsSheet.TranslationY > 120)
				{
					vm.CloseSettingsCommand.Execute(null);
				}
				else
				{
					_ = SettingsSheet.TranslateTo(0, 0, 140, Easing.CubicOut);
					_ = SettingsBackdrop.FadeTo(1, 120, Easing.CubicOut);
				}
				break;
		}
	}
}
