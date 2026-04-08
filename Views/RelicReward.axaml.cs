using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using framenion.Src;
using System;
using System.Threading.Tasks;

namespace framenion;

public partial class RelicRewardWindow : Window
{
	public string ItemName { get; set; } = string.Empty;
	public string Ducats { get; set; } = "0";
	public string Price { get; set; } = "0";
	
	public RelicRewardWindow()
	{
		InitializeComponent();
	}

	public RelicRewardWindow(string name, string price, string ducats, int x, int y, int width)
	{
		ItemName = name;
		Ducats = ducats;
		Price = price;
		InitializeComponent();
		DataContext = this;
		Width = width;
		WindowStartupLocation = WindowStartupLocation.Manual;
		Position = new PixelPoint(x, y);
		IsHitTestVisible = false;
	}

	public static async Task Display(RewardInfo reward, int x, int y, int width)
	{
		var owner = AppData.MainWindow;
		if (owner == null) return;

		await Dispatcher.UIThread.InvokeAsync(() => {
			var win = new RelicRewardWindow(reward.ItemName, reward.Platinum, reward.Ducats, x, y, width);
			try {
				win.ShowActivated = false;
			} catch { }

			if (owner.IsVisible) {
				win.Show(owner);
			} else {
				win.Show();
			}

			AppData.RewardWindows.Add(win);
			win.Closed += (_, _) => {
				AppData.RewardWindows.Remove(win);
			};

			_ = Task.Run(async () => {
				await Task.Delay(15000);
				await Dispatcher.UIThread.InvokeAsync(() => {
					if (win.IsVisible) win.Close();
				});
			});
		});
	}
}
