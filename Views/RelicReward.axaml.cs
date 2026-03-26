using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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

	public RelicRewardWindow(string name, string ducats, string price, int x, int y)
	{
		ItemName = name;
		Ducats = ducats;
		Price = price;
		InitializeComponent();
		DataContext = this;
		this.WindowStartupLocation = WindowStartupLocation.Manual;
		this.Position = new PixelPoint(x, y);
		this.IsHitTestVisible = false;
	}

	public static async Task Display(Window owner, string name, int x, int y, TimeSpan duration)
	{
		bool found = GameData.warframeMarketItems.TryGetValue(name, out var marketInfo);
		var ducats = "0";
		var platinum = "0";
		try {
			if (!string.IsNullOrEmpty(marketInfo.Item1)) {
				ducats = marketInfo.Item2;
				var resp = await GameData.httpClient.GetAsync($"https://api.warframe.market/v2/orders/item/{marketInfo.Item1}/top");
				resp.EnsureSuccessStatusCode();
				using var stream = await resp.Content.ReadAsStreamAsync();
				using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);

				if (doc.RootElement.TryGetProperty("data", out var data) &&
					data.TryGetProperty("sell", out var sell) &&
					sell.ValueKind == System.Text.Json.JsonValueKind.Array &&
					sell.GetArrayLength() > 0) {
					var first = sell[0];
					if (first.TryGetProperty("platinum", out var platinumProp)) {
						platinum = platinumProp.GetInt32().ToString();
					}
				}
			}

		} catch {
			if (!found) {
				MessageBox.Show(owner, "Error", "Item not found: " + name);
			}
			MessageBox.Show(owner, "Error", "Failed to fetch market data for " + name);
		}

		var win = new RelicRewardWindow(name, ducats, platinum, x, y);
		try {
			win.ShowActivated = false;
		} catch { }

		win.Show(owner);
		await Task.Delay(duration);
		if (!win.IsVisible) return;
		win.Close();
	}
}
