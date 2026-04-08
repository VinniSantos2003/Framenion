using Avalonia.Media.Imaging;
using Sdcb.PaddleOCR;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace framenion.Src;

public class RewardInfo(string name)
{
	public string ItemName { get; set; } = name;
	public string Platinum { get; set; } = "0";
	public string Ducats { get; set; } = "0";
}

public static class GameData
{
	public static readonly IReadOnlyDictionary<string, (string name, string color)> relicType = new Dictionary<string, (string, string)>(StringComparer.Ordinal) {
		["VoidT1"] = ("Lith", "#72523c"),
		["VoidT2"] = ("Meso", "#917147"),
		["VoidT3"] = ("Neo", "#c9c3c4"),
		["VoidT4"] = ("Axi", "#FFD700"),
		["VoidT5"] = ("Requiem", "#e80c1e"),
		["VoidT6"] = ("Omnia", "#FFFFFF")
	};

	public static List<VoidFissure> Fissures { get; set; } = [];
	public static List<Item> ItemsList { get; set; } = [];
	public static List<Relic> RelicList { get; set; } = [];

	public static FrozenDictionary<string, string> Lang { get; set; } = FrozenDictionary<string, string>.Empty;
	public static FrozenDictionary<string, string> ExportTextIcons { get; set; } = FrozenDictionary<string, string>.Empty;
	public static FrozenDictionary<string, string> ExportMissionTypes { get; set; } = FrozenDictionary<string, string>.Empty;
	public static FrozenDictionary<string, string> ExportFactions { get; set; } = FrozenDictionary<string, string>.Empty;

	public static FrozenDictionary<string, (string slug, string ducats)> WarframeMarketItems { get; set; } = FrozenDictionary<string, (string, string)>.Empty;
	public static FrozenDictionary<string, (string name, RecipeDTO recipe)> ExportRecipes { get; set; } = FrozenDictionary<string, (string, RecipeDTO)>.Empty;
	public static FrozenDictionary<string, string> ExportRecipeByName { get; set; } = FrozenDictionary<string, string>.Empty;
	public static FrozenDictionary<string, ResourceDTO> ExportResources { get; set; } = FrozenDictionary<string, ResourceDTO>.Empty;
	public static FrozenDictionary<string, ItemDTO> ExportWarframes { get; set; } = FrozenDictionary<string, ItemDTO>.Empty;
	public static FrozenDictionary<string, ItemDTO> ExportWeapons { get; set; } = FrozenDictionary<string, ItemDTO>.Empty;
	public static FrozenDictionary<string, ItemDTO> ExportSentinels { get; set; } = FrozenDictionary<string, ItemDTO>.Empty;
	public static FrozenDictionary<string, RegionDTO> ExportRegions { get; set; } = FrozenDictionary<string, RegionDTO>.Empty;
	public static FrozenDictionary<string, RelicDTO> ExportRelics { get; set; } = FrozenDictionary<string, RelicDTO>.Empty;
	public static FrozenDictionary<string, List<RewardDTO>> ExportRewards { get; set; } = FrozenDictionary<string, List<RewardDTO>>.Empty;

	public static List<string> UniquelevelCaps { get; set; } = [];
	public static List<string> PrimeItems { get; set; } = [];

	public static async Task<FrozenDictionary<string, T>> Deserialize<T>(string path, JsonTypeInfo<T> typeInfo)
	{
		await using var stream = File.OpenRead(path);
		using var doc = await JsonDocument.ParseAsync(stream);
		var builder = new Dictionary<string, T>(StringComparer.Ordinal);
		foreach (var element in doc.RootElement.EnumerateObject()) {
			var item = JsonSerializer.Deserialize<T>(element.Value, typeInfo);
			if (item != null) {
				builder[element.Name] = item;
			}
		}
		return builder.ToFrozenDictionary(StringComparer.Ordinal);
	}

	public static Bitmap? GetOrCreateBitmap(string localPath, int decodeWidth = 80)
	{
		if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath)) return null;
		var lazy = AppData.BitmapCache.GetOrAdd(localPath, path =>
			new Lazy<Bitmap?>(() => {
				try {
					using var fs = File.OpenRead(path);
					return Bitmap.DecodeToWidth(fs, decodeWidth, BitmapInterpolationMode.LowQuality);
				} catch {
					return null;
				}
			}, LazyThreadSafetyMode.ExecutionAndPublication));
		return lazy.Value;
	}

	public static void ClearBitmapCache()
	{
		foreach (var kv in AppData.BitmapCache) {
			if (kv.Value.IsValueCreated && kv.Value.Value is IDisposable d) {
				try { d.Dispose(); } catch { }
			}
		}
		AppData.BitmapCache.Clear();
	}

	public static string GetLocalIconPath(string icon)
	{
		if (string.IsNullOrEmpty(icon)) return "";
		if (icon.Contains("/CraftingComponents/")) {
			return Path.Combine(AppData.IconsCacheDir, icon.Split("/CraftingComponents/")[1]);
		} else if (icon.Contains("/AvatarImages/")) {
			return Path.Combine(AppData.IconsCacheDir, icon.Split("/AvatarImages/")[1]);
		} else if (icon.Contains("/Relics/")) {
			return Path.Combine(AppData.IconsCacheDir, icon.Split("/Relics/")[1]);
		}
		return Path.Combine(AppData.IconsCacheDir, icon.Split('/').Last());
	}

	public static async Task DownloadIconAsync(string icon)
	{
		var iconPath = GetLocalIconPath(icon);
		if (File.Exists(iconPath)) return;

		await AppData.IconDownloadSemaphore.WaitAsync();
		try {
			if (File.Exists(iconPath)) return;
			using var iconStream = await AppData.GetStreamAsync(icon);
			using var fileStream = File.Create(iconPath);
			await iconStream.CopyToAsync(fileStream);
		} finally {
			AppData.IconDownloadSemaphore.Release();
		}
	}

	public static async Task DownloadIconsAsync(IEnumerable<string> icons)
	{
		var failures = new ConcurrentBag<string>();
		await Task.WhenAll(icons.Where(i => !string.IsNullOrEmpty(i)).Select(async icon => {
			try {
				await DownloadIconAsync(icon);
			} catch {
			}
		}));
	}

	public static async Task LoadWFMarketData(bool updateFile)
	{
		var cacheFile = Path.Combine(AppData.CacheDir, "wfmarketitems.json");
		try {
			if (updateFile) {
				var url = "https://api.warframe.market/v2/items/";
				using var stream = await AppData.GetStreamAsync(url);
				using var fileStream = File.Create(cacheFile);
				await stream.CopyToAsync(fileStream);
			}

			using var cacheStream = File.OpenRead(cacheFile);
			using var doc = await JsonDocument.ParseAsync(cacheStream);
			var builder = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
			if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array) {
				foreach (var item in data.EnumerateArray()) {
					item.TryGetProperty("slug", out var slugProp);
					item.TryGetProperty("i18n", out var i18nProp);
					i18nProp.TryGetProperty("en", out var enProp);
					enProp.TryGetProperty("name", out var nameProp);
					var slug = slugProp.GetString() ?? "";
					var ducats = "0";
					if (item.TryGetProperty("ducats", out var ducatsProp) && ducatsProp.ValueKind == JsonValueKind.Number) {
						ducats = ducatsProp.GetInt32().ToString();
					}
					var name = nameProp.GetString() ?? "";
					builder[name] = (slug, ducats);
				}
			}

			WarframeMarketItems = builder.ToFrozenDictionary(StringComparer.Ordinal);
		} catch (Exception ex) {
			MessageBox.Show("Error", "Failed to load Warframe Market items: " + ex.Message);
		}
	}

	public static async Task LoadFile(string file, string cacheDir, bool updateFile)
	{
		var exportCacheFile = Path.Combine(cacheDir, file + ".json");
		if (!File.Exists(exportCacheFile) || updateFile) {
			try {
				var url = "https://raw.githubusercontent.com/calamity-inc/warframe-public-export-plus/refs/heads/senpai/" + file + ".json";
				using var inStream = await AppData.GetStreamAsync(url);
				await using var outStream = File.Create(exportCacheFile);
				await inStream.CopyToAsync(outStream);
			} catch {
				throw new FileNotFoundException("Failed to retrieve file: " + file);
			}
		}

		try {
			switch (file) {
				case "dict.en": {
						using var stream = File.OpenRead(exportCacheFile);
						using var doc = await JsonDocument.ParseAsync(stream);
						var builder = new Dictionary<string, string>(StringComparer.Ordinal);
						foreach (var prop in doc.RootElement.EnumerateObject()) {
							var name = prop.Name;
							if (name.Contains("/CraftingComponent_") && name.Contains("Prime") && !name.Contains("Desc")) {
								PrimeItems.Add(prop.Value.GetString() ?? "");
							}
							builder[name] = prop.Value.ToString();
						}
						Lang = builder.ToFrozenDictionary();

						var existing = new HashSet<string>(PrimeItems, StringComparer.OrdinalIgnoreCase);
						var groups = PrimeItems.Where(s => !string.IsNullOrWhiteSpace(s)).GroupBy(s => {
							var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
							if (tokens.Length <= 1) return s;
							return string.Join(' ', tokens.Take(tokens.Length - 1));
						}, StringComparer.OrdinalIgnoreCase);

						foreach (var g in groups) {
							if (g.Count() < 2) continue;
							var blueprintName = g.Key + " Blueprint";
							if (!existing.Contains(blueprintName)) {
								PrimeItems.Add(blueprintName);
								existing.Add(blueprintName);
							}
						}
						break;
					}
				case "ExportWarframes":
					ExportWarframes = await Deserialize(exportCacheFile, ExportJsonContext.Default.ItemDTO);
					break;
				case "ExportWeapons":
					ExportWeapons = await Deserialize(exportCacheFile, ExportJsonContext.Default.ItemDTO);
					break;
				case "ExportSentinels":
					ExportSentinels = await Deserialize(exportCacheFile, ExportJsonContext.Default.ItemDTO);
					break;
				case "ExportRecipes": {
						await using var stream = File.OpenRead(exportCacheFile);
						using var doc = await JsonDocument.ParseAsync(stream);
						var builder = new Dictionary<string, (string, RecipeDTO)>(StringComparer.Ordinal);
						var byName = new Dictionary<string, string>(StringComparer.Ordinal);
						foreach (var r in doc.RootElement.EnumerateObject()) {
							if (r.Value.TryGetProperty("resultType", out var rt) && rt.GetString() is string rtStr) {
								var recipe = JsonSerializer.Deserialize(r.Value, ExportJsonContext.Default.RecipeDTO);
								if (recipe == null) continue;
								builder[rtStr] = (r.Name, recipe);
								byName[r.Name] = rtStr;
							}
						}
						ExportRecipes = builder.ToFrozenDictionary();
						ExportRecipeByName = byName.ToFrozenDictionary();
						break;
					}
				case "ExportRegions":
					ExportRegions = await Deserialize(exportCacheFile, ExportJsonContext.Default.RegionDTO);
					break;
				case "ExportResources":
					ExportResources = await Deserialize(exportCacheFile, ExportJsonContext.Default.ResourceDTO);
					break;
				case "ExportMissionTypes": {
						using var stream = File.OpenRead(exportCacheFile);
						using var doc = await JsonDocument.ParseAsync(stream);
						var builder = new Dictionary<string, string>(StringComparer.Ordinal);
						foreach (var prop in doc.RootElement.EnumerateObject()) {
							if (prop.Value.TryGetProperty("name", out var nameProp)) {
								builder[prop.Name] = nameProp.GetString() ?? "";
							}
						}
						ExportMissionTypes = builder.ToFrozenDictionary();
						break;
					}
				case "ExportFactions": {
						using var stream = File.OpenRead(exportCacheFile);
						using var doc = await JsonDocument.ParseAsync(stream);
						var builder = new Dictionary<string, string>(StringComparer.Ordinal);
						foreach (var prop in doc.RootElement.EnumerateObject()) {
							if (prop.Value.TryGetProperty("name", out var nameProp)) {
								builder[prop.Name] = nameProp.GetString() ?? "";
							}
						}
						ExportFactions = builder.ToFrozenDictionary();
						break;
					}
				case "ExportTextIcons": {
						using var stream = File.OpenRead(exportCacheFile);
						using var doc = await JsonDocument.ParseAsync(stream);
						var builder = new Dictionary<string, string>(StringComparer.Ordinal);
						foreach (var prop in doc.RootElement.EnumerateObject()) {
							if (prop.Value.TryGetProperty("DIT_AUTO", out var nameProp)) {
								builder[prop.Name] = nameProp.GetString() ?? "";
							}
						}
						ExportTextIcons = builder.ToFrozenDictionary();
						break;
					}
				case "ExportMisc": {
						await using var stream = File.OpenRead(exportCacheFile);
						using var doc = await JsonDocument.ParseAsync(stream);
						foreach (var prop in doc.RootElement.GetProperty("uniqueLevelCaps").EnumerateObject()) {
							UniquelevelCaps.Add(prop.Name);
						}
						break;
					}
				case "ExportRelics":
					ExportRelics = await Deserialize(exportCacheFile, ExportJsonContext.Default.RelicDTO);
					break;
				case "ExportRewards": {
						await using var stream = File.OpenRead(exportCacheFile);
						using var doc = await JsonDocument.ParseAsync(stream);
						var builder = new Dictionary<string, List<RewardDTO>>(StringComparer.Ordinal);
						foreach (var prop in doc.RootElement.EnumerateObject()) {
							if (!prop.Name.Contains("/VoidKeyMissionRewards/") && !prop.Name.Contains("/ImmortalRelicRewards/")) {
								continue;
							}

							var flatList = new List<RewardDTO>();
							if (prop.Value.ValueKind == JsonValueKind.Array) {
								foreach (var outer in prop.Value[0].EnumerateArray()) {
									var reward = JsonSerializer.Deserialize(outer, ExportJsonContext.Default.RewardDTO);
									if (reward != null) {
										flatList.Add(reward);
									}
								}
							}

							if (flatList.Count > 0) {
								builder[prop.Name] = flatList;
							}
						}

						ExportRewards = builder.ToFrozenDictionary(StringComparer.Ordinal);
						break;
					}
			}
		} catch (Exception ex) {
			MessageBox.Show("Error", $"Error loading {file}: {ex.Message}");
		}
	}

	private static string ResolveName(string langKey)
	{
		var name = Lang.TryGetValue(langKey, out var value) ? value : langKey;
		return name.Replace("<ARCHWING> ", "");
	}

	private static IEnumerable<string> GetIngredientIconUrls(string type)
	{
		if (!ExportRecipes.TryGetValue(type, out var recipe)) yield break;
		var ingredients = recipe.recipe.Ingredients;
		if (ingredients == null || ingredients.Count < 1 ) yield break;
		foreach (var ingredient in ingredients) {
			var ingredientType = ingredient.Type;
			if (string.IsNullOrWhiteSpace(ingredientType)) continue;
			if (!ExportResources.TryGetValue(ingredientType, out var resource) || resource == null) continue;
			var icon = resource.Icon;
			if (!string.IsNullOrEmpty(icon)) yield return icon;
		}
	}

	private static ObservableCollection<RecipeIngredient> BuildIngredients(string parentName, string parentType, string blueprintPath)
	{
		var result = new ObservableCollection<RecipeIngredient>();
		if (!ExportRecipes.TryGetValue(parentType, out var recipe)) return result;
		var ingredients = recipe.recipe.Ingredients;
		if (ingredients == null) return result;

		result.Add(new RecipeIngredient(parentName + " Blueprint", parentType, 1, blueprintPath) { RecipeKey = recipe.name});
		foreach (var ingredient in ingredients) {
			var ingredientType = ingredient.Type;
			if (string.IsNullOrWhiteSpace(ingredientType)) continue;
			if (!ExportResources.TryGetValue(ingredientType, out var resource) || resource == null) continue;
			var ingredientLangKey = resource.Name;
			result.Add(new RecipeIngredient(
				ResolveName(ingredientLangKey),
				ingredientType,
				ingredient.Count,
				GetLocalIconPath(resource.Icon)
			));
		}
		return result;
	}

	private static ObservableCollection<Reward> BuildRewards(string type)
	{
		var result = new ObservableCollection<Reward>();
		if (!ExportRewards.TryGetValue(type, out var rewards)) return result;
		foreach (var reward in rewards) {
			var rewardType = reward.Type.Replace("/Lotus/StoreItems/", "/Lotus/");
			var borderColor = "";
			switch (reward.Rarity) {
				case "COMMON":
					borderColor = "#95543B";
					break;
				case "UNCOMMON":
					borderColor = "#D1CFD1";
					break;
				case "RARE":
					borderColor = "#EED78A";
					break;
			}

			if (ExportResources.TryGetValue(rewardType, out var resource)) {
				result.Add(new Reward(ResolveName(resource.Name), rewardType, GetLocalIconPath(resource.Icon), reward.Count, reward.Rarity, borderColor));
				continue;
			}

			if (ExportRecipeByName.TryGetValue(rewardType, out var recipeEntry)) {
				var recipe = ExportRecipes[recipeEntry].recipe;
				string displayName = "";
				string iconPath = "";

				if (ExportResources.TryGetValue(recipe.ResultType, out var associatedResource)) {
					displayName = ResolveName(associatedResource.Name);
					iconPath = GetLocalIconPath(associatedResource.Icon);
				} else {
					var match = ItemsList.FirstOrDefault(i => string.Equals(i.Type, recipe.ResultType, StringComparison.Ordinal));
					if (match != null) {
						displayName = $"{match.Name} Blueprint";
						iconPath = match.IconPath;
					}
				}

				result.Add(new Reward(displayName, rewardType, iconPath, reward.Count, reward.Rarity, borderColor));
			}
		}
		return new ObservableCollection<Reward>(result.OrderBy(r => GetRaritySortKey(r.Rarity)));
	}

	private static bool ShouldSkipWeapon(string type, ItemDTO weapon)
	{
		if (type.Contains("PvPVariant") || type.Contains("Doppelganger")) return true;
		var partType = weapon.PartType;
		if (partType == null) return false;
		// moas, hounds, k-drives, zaw blades and amp prisms
		return partType != "LWPT_MOA_HEAD" && partType != "LWPT_ZANUKA_HEAD" && partType != "LWPT_HB_DECK" && partType != "LWPT_BLADE" && partType != "LWPT_AMP_OCULUS";
	}

	public static async Task LoadExports()
	{
		string blueprintPath = Path.Combine(AppContext.BaseDirectory, "assets", "blueprint.png");
		try {
			ItemsList.Clear();
			var iconUrls = new HashSet<string>(StringComparer.Ordinal);
			foreach (var (type, element) in ExportWarframes
				.Concat(ExportWeapons.Where(kvp => !ShouldSkipWeapon(kvp.Key, kvp.Value)))
				.Concat(ExportSentinels.Where(kvp => !kvp.Key.Contains("/Pets/")))) {
				iconUrls.Add(element.Icon);
				foreach (var url in GetIngredientIconUrls(type)) iconUrls.Add(url);
			}

			foreach (var (type, relic) in ExportRelics) {
				iconUrls.Add(relic.Icon);
			}
			await DownloadIconsAsync(iconUrls);

			foreach (var (type, warframe) in ExportWarframes) {
				var name = ResolveName(warframe.Name);
				var category = warframe.Category;
				if (category is "MechSuits" or "SpaceSuits") category = "Vehicles";
				ItemsList.Add(new Item(name, type, BuildIngredients(name, type, blueprintPath), category, GetLocalIconPath(warframe.Icon), false));
			}

			foreach (var (type, weapon) in ExportWeapons) {
				if (ShouldSkipWeapon(type, weapon)) continue;
				var name = ResolveName(weapon.Name);
				var category = weapon.Category;
				if (type.Contains("/Hoverboard/")) category = "Vehicles";
				else if (type.Contains("/Pets/")) category = "Companions";
				else if (type.Contains("Amp") && type.Contains("Barrel")) category = "OperatorAmps";
				ItemsList.Add(new Item(name, type, BuildIngredients(name, type, blueprintPath), category, GetLocalIconPath(weapon.Icon), false));
			}

			foreach (var (type, sentinel) in ExportSentinels) {
				if (type.Contains("/Pets/")) continue;
				var name = ResolveName(sentinel.Name);
				ItemsList.Add(new Item(name, type, BuildIngredients(name, type, blueprintPath), "Companions", GetLocalIconPath(sentinel.Icon), false));
			}

			foreach (var (type, relic) in ExportRelics) {
				var quality = relic.Quality;
				switch (relic.Quality) {
					case "VPQ_BRONZE":
						quality = "Intact";
						break;
					case "VPQ_SILVER":
						quality = "Exceptional";
						break;
					case "VPQ_GOLD":
						quality = "Flawless";
						break;
					case "VPQ_PLATINUM":
						quality = "Radiant";
						break;
				}
				var name = $"{relic.Era} {relic.Category} Relic [{quality}]";
				RelicList.Add(new Relic(name, type, GetLocalIconPath(relic.Icon), relic.Era, quality, BuildRewards(relic.RewardManifest)));
			}
		} catch (Exception ex) {
			MessageBox.Show("Error", "Failed to load exports: " + ex.Message);
		}
	}

	public static async Task ExtractGameInfo()
	{
		Directory.CreateDirectory(AppData.AppDataDir);
		bool isWindows = OperatingSystem.IsWindows();
		var exeFileName = isWindows ? "warframe-api-helper.exe" : "warframe-api-helper";
		var exePath = Path.Combine(AppData.AppDataDir, exeFileName);
		if (!File.Exists(exePath)) {
			if (!await MessageBox.AskYesNo("Download required component", "Do you want to download warframe-api-helper from its official GitHub repository?")) {
				return;
			}

			string url = isWindows
				? "https://github.com/Sainan/warframe-api-helper/releases/download/1.1.1/warframe-api-helper.exe"
				: "https://github.com/Sainan/warframe-api-helper/releases/download/1.1.1/Linux.zip";
			var tempPath = Path.Combine(AppData.AppDataDir, Path.GetRandomFileName());
			try {
				using var inStream = await AppData.GetStreamAsync(url);
				using var outStream = File.Create(tempPath);
				await inStream.CopyToAsync(outStream);

				if (!isWindows && url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
					try {
						System.IO.Compression.ZipFile.ExtractToDirectory(tempPath, AppData.AppDataDir);
						var extracted = Directory.EnumerateFiles(AppData.AppDataDir, "warframe-api-helper*", SearchOption.AllDirectories)
							.FirstOrDefault(f => Path.GetFileName(f).StartsWith("warframe-api-helper", StringComparison.Ordinal)) ?? throw new FileNotFoundException("Extracted helper not found.");
						if (File.Exists(exePath)) File.Delete(exePath);
						File.Move(extracted, exePath);
					} finally {
						try { File.Delete(tempPath); } catch { }
					}

					if (!isWindows) {
						try {
							using var chmod = new Process {
								StartInfo = new ProcessStartInfo {
									FileName = "chmod",
									Arguments = $"+x \"{exePath}\"",
									UseShellExecute = false,
									CreateNoWindow = true
								}
							};
							chmod.Start();
							await chmod.WaitForExitAsync();
						} catch {
							// todo: test proceed but the helper might fail without exec bit
						}
					}
				} else {
					if (File.Exists(exePath)) File.Delete(exePath);
					File.Move(tempPath, exePath);
				}
			} catch (Exception ex) {
				try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
				MessageBox.Show("Error", "Failed to download component: " + ex.Message);
				return;
			}
		}

		if (!await MessageBox.AskYesNo("Disclaimer", "By confirming, you acknowledge and agree to use warframe-api-helper to retrieve your inventory data at your own risk.", "Confirm", "Close")) {
			MessageBox.Show("Info", "Operation cancelled by user.");
			return;
		}

		using var process = new Process {
			StartInfo = new ProcessStartInfo {
				FileName = exePath,
				WorkingDirectory = AppData.AppDataDir,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = true,
				CreateNoWindow = true,
			}
		};

		process.Start();
		try {
			await process.StandardInput.WriteLineAsync();
			await process.StandardInput.FlushAsync();
			process.StandardInput.Close();
		} catch { }

		var stdoutTask = process.StandardOutput.ReadToEndAsync();
		var stderrTask = process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();
		var stdout = await stdoutTask;
		var stderr = await stderrTask;
		if (process.ExitCode != 0) {
			var msg = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr + Environment.NewLine + stdout;
			MessageBox.Show("warframe-api-helper error", msg);
			return;
		}
	}

	public static async Task<RewardInfo> GetItemData(string itemName)
	{
		WarframeMarketItems.TryGetValue(itemName, out var marketInfo);
		var rewardInfo = new RewardInfo(itemName);
		try {
			if (!string.IsNullOrEmpty(marketInfo.slug)) {
				rewardInfo.Ducats = marketInfo.ducats;
				using var stream = await AppData.GetStreamAsync($"https://api.warframe.market/v2/orders/item/{marketInfo.slug}/top");
				using var doc = await JsonDocument.ParseAsync(stream);

				if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("sell", out var sell) &&
					sell.ValueKind == JsonValueKind.Array && sell.GetArrayLength() > 0) {
					var first = sell[0];
					if (first.TryGetProperty("platinum", out var platinumProp)) {
						rewardInfo.Platinum = platinumProp.GetInt32().ToString();
					}
				}
			}
		} catch {
			MessageBox.Show("Error", "Failed to fetch market data for " + itemName);
		}

		return rewardInfo;
	}

	public static long XpToMaster(string type)
	{
		int levelCap = 30;
		if (UniquelevelCaps.Contains(type)) {
			levelCap = 40;
		}

		bool isWarframe = type.Contains("/Lotus/Powersuits/", StringComparison.Ordinal);
		long baseXp = isWarframe ? 1000L : 500L;
		return baseXp * (long)levelCap * (long)levelCap;
	}

	public static int GetRelicSortKey(string quality)
	{
		return quality switch {
			"Intact" => 1,
			"Flawless" => 2,
			"Exceptional" => 3,
			"Radiant" => 4,
			_ => int.MaxValue
		};
	}

	public static int GetRaritySortKey(string rarity)
	{
		return rarity switch {
			"COMMON" => 1,
			"UNCOMMON" => 2,
			"RARE" => 3,
			_ => int.MaxValue
		};
	}
}
