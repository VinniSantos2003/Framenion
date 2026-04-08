using Avalonia.Media.Imaging;
using System.Collections.ObjectModel;
using System.IO;

namespace framenion.Src;

public class Relic(string name, string type, string iconPath, string era, string quality, ObservableCollection<Reward> rewards)
{
	public string Name { get; set; } = name;
	public string Type { get; set; } = type;
	public Bitmap? Icon { get; } = File.Exists(iconPath) ? GameData.GetOrCreateBitmap(iconPath) : null;
	public string Era { get; set; } = era;
	public string Quality { get; set; } = quality;
	public ObservableCollection<Reward> Rewards { get; set; } = rewards;
	public int OwnedCount { get; set; } = 0;
	public bool IsCountVisible => OwnedCount > 0;
	public bool Unowned => OwnedCount == 0;
}

public class Reward(string name, string type, string iconPath, int count, string rarity, string borderColor)
{
	public string Name { get; set; } = name;
	public string Type { get; set; } = type;
	public Bitmap? Icon { get; } = File.Exists(iconPath) ? GameData.GetOrCreateBitmap(iconPath) : null;
	public int Count { get; set; } = count;
	public string Rarity { get; set; } = rarity;
	public string BorderColor { get; set; } = borderColor;
}
