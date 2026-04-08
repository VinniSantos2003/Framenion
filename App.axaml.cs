using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using framenion.Src;
using System.Threading.Tasks;

namespace framenion
{
	public partial class App : Application
	{
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.MainWindow = new MainWindow();
				AppData.MainWindow = desktop.MainWindow;
				_ = LoadAndApplySettingsAsync();
			}

			base.OnFrameworkInitializationCompleted();
		}

		private static async Task LoadAndApplySettingsAsync()
		{
			var settings = await AppSettings.LoadAsync();
			settings.ApplyToApplicationResources();
			AppData.AppSettings = settings;
		}

		public void ToggleWindow()
		{
			var mainWindow = AppData.MainWindow;
			if (mainWindow == null) return;
			if (mainWindow.IsVisible) {
				mainWindow.Hide();
			} else {
				mainWindow.Show();
				mainWindow.WindowState = WindowState.Normal;
			}
		}

		public void Exit()
		{
			AppData.MainWindow?.Close();
		}
	}
}