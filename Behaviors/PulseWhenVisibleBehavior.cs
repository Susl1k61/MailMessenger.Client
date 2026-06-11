namespace MailMessenger.Client.Behaviors;

/// <summary>
/// Запускает бесконечную пульсирующую анимацию на View,
/// когда его свойство IsVisible становится true, и останавливает при false.
/// </summary>
public sealed class PulseWhenVisibleBehavior : Behavior<View>
{
	private View? _view;
	private CancellationTokenSource? _cts;

	protected override void OnAttachedTo(View view)
	{
		base.OnAttachedTo(view);
		_view = view;
		view.PropertyChanged += OnPropertyChanged;
		if (view.IsVisible)
		{
			StartPulse();
		}
	}

	protected override void OnDetachingFrom(View view)
	{
		view.PropertyChanged -= OnPropertyChanged;
		StopPulse();
		_view = null;
		base.OnDetachingFrom(view);
	}

	private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(View.IsVisible))
		{
			return;
		}

		if (_view?.IsVisible == true)
		{
			StartPulse();
		}
		else
		{
			StopPulse();
		}
	}

	private void StartPulse()
	{
		StopPulse();
		if (_view is null)
		{
			return;
		}

		_cts = new CancellationTokenSource();
		var token = _cts.Token;
		var view = _view;

		_ = Task.Run(async () =>
		{
			while (!token.IsCancellationRequested)
			{
				await MainThread.InvokeOnMainThreadAsync(async () =>
				{
					if (!token.IsCancellationRequested && view.IsVisible)
					{
						await view.ScaleToAsync(1.25, 350, Easing.SinInOut);
					}
				});

				if (token.IsCancellationRequested)
				{
					break;
				}

				await MainThread.InvokeOnMainThreadAsync(async () =>
				{
					if (!token.IsCancellationRequested && view.IsVisible)
					{
						await view.ScaleToAsync(1.0, 350, Easing.SinInOut);
					}
				});

				// Небольшая пауза между пульсами.
				try
				{
					await Task.Delay(600, token);
				}
				catch (TaskCanceledException)
				{
					break;
				}
			}

			// Сбрасываем масштаб при остановке.
			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				if (view.IsVisible == false)
				{
					view.Scale = 1.0;
				}
			});
		}, token);
	}

	private void StopPulse()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;
	}
}
