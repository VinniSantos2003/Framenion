using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using framenion.Src;
using System;
using System.Threading.Tasks;

namespace framenion;

public partial class SettingsWindow : Window
{
	private AppSettings settings = new();

	public SettingsWindow()
	{
		InitializeComponent();
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Opened += async (_, _) => await LoadAsync();
	}

	private async Task LoadAsync()
	{
		settings = await AppSettings.LoadAsync();
		AccentColorBox.Text = settings.AccentColor;
		EnableNotificationsCheckBox.IsChecked = settings.EnableNotifications;
		EnableEELogReadBox.IsChecked = settings.EnableEELogRead;
		EnableRelicOverlayBox.IsChecked = settings.EnableRelicOverlay;
		UiScaleNumeric.Value = settings.UIScale;
		DebugOCRBox.IsChecked = settings.DebugOCR;
		OverlayOffset.Value = settings.OverlayOffset;
	}

	private async void SaveApply_Click(object? sender, RoutedEventArgs e)
	{
		settings.AccentColor = AccentColorBox.Text?.Trim() ?? settings.AccentColor;
		settings.EnableNotifications = EnableNotificationsCheckBox.IsChecked ?? true;
		settings.EnableEELogRead = EnableEELogReadBox.IsChecked ?? true;
		settings.EnableRelicOverlay = EnableRelicOverlayBox.IsChecked ?? true;
		settings.UIScale = (int)(UiScaleNumeric.Value ?? settings.UIScale);
		settings.DebugOCR = DebugOCRBox.IsChecked ?? false;
		settings.OverlayOffset = (int)(OverlayOffset.Value ?? settings.OverlayOffset);
		settings.ApplyToApplicationResources();

		var originalBackground = SaveButton.Background;
		try {
			await settings.SaveAsync();
			if (settings.EnableEELogRead) {
				AppData.Monitor?.Start();
			} else {
				AppData.Monitor?.Stop();
			}
			SaveButton.Content = "Saved";
			SaveButton.Background = Brush.Parse("#33cc33");
			await Task.Delay(2000);
			SaveButton.Content = "Save";
			SaveButton.Background = originalBackground;
		} catch (Exception ex) {
			MessageBox.Show("Error", $"Failed to save settings: {ex.Message}");
		}
	}

	private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
