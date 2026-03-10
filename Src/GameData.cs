using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace framenion.Src;

public static class GameData
{
	public static readonly Dictionary<string, (string, string)> relicType = new() {
		{ "VoidT1", ("Lith", "#72523c") },
		{ "VoidT2", ("Meso", "#917147") },
		{ "VoidT3", ("Neo", "#c9c3c4") },
		{ "VoidT4", ("Axi", "#FFD700") },
		{ "VoidT5", ("Requiem", "#e80c1e") },
		{ "VoidT6", ("Omnia", "#FFFFFF") }
	};
	public static readonly string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Framenion");
	public static readonly string cacheDir = Path.Combine(appDataDir, "cache");
	public static readonly string iconsCacheDir = Path.Combine(cacheDir, "icons");
	private static readonly SemaphoreSlim _iconDownloadSemaphore = new(10, 10);
	public static readonly HttpClient httpClient = new() {
		BaseAddress = new Uri("https://browse.wf/"),
	};

	public static JsonElement mainRoot = new();

	public static List<VoidFissure> fissures = [];
	public static List<Item> itemsList = [];

	public static FrozenDictionary<string, string> lang = FrozenDictionary<string, string>.Empty;
	public static FrozenDictionary<string, (string, JsonElement)> exportRecipes = FrozenDictionary<string, (string, JsonElement)>.Empty;
	public static FrozenDictionary<string, JsonElement> exportRegions = FrozenDictionary<string, JsonElement>.Empty;
	public static FrozenDictionary<string, JsonElement> exportResources = FrozenDictionary<string, JsonElement>.Empty;
	public static FrozenDictionary<string, JsonElement> exportWarframes = FrozenDictionary<string, JsonElement>.Empty;
	public static FrozenDictionary<string, JsonElement> exportWeapons = FrozenDictionary<string, JsonElement>.Empty;
	public static FrozenDictionary<string, JsonElement> exportSentinels = FrozenDictionary<string, JsonElement>.Empty;
	public static FrozenDictionary<string, JsonElement> exportMissionTypes = FrozenDictionary<string, JsonElement>.Empty;
	public static FrozenDictionary<string, JsonElement> exportFactions = FrozenDictionary<string, JsonElement>.Empty;
	public static FrozenDictionary<string, JsonElement> exportTextIcons = FrozenDictionary<string, JsonElement>.Empty;
	public static JsonDocument? exportMisc = null;

	public static DispatcherTimer? fissureRefreshTimer;
	public static DispatcherTimer? fissureUpdateTimer;

	public static async Task<FrozenDictionary<string, JsonElement>> ParseDictionary(string path)
	{
		await using var stream = File.OpenRead(path);
		using var doc = await JsonDocument.ParseAsync(stream);
		var builder = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
		foreach (var element in doc.RootElement.EnumerateObject()) {
			builder[element.Name] = element.Value.Clone();
		}
		return builder.ToFrozenDictionary(StringComparer.Ordinal);
	}

	public static Bitmap? DecodeThumbnail(string path)
	{
		try {
			using var fs = File.OpenRead(path);
			return Bitmap.DecodeToWidth(fs, 80, BitmapInterpolationMode.LowQuality);
		} catch {
			return null;
		}
	}

	public static string GetLocalIconPath(string icon)
	{
		if (string.IsNullOrEmpty(icon)) return "";
		if (icon.Contains("/CraftingComponents/")) {
			return Path.Combine(iconsCacheDir, icon.Split("/CraftingComponents/")[1]);
		}
		return Path.Combine(iconsCacheDir, icon.Split('/').Last());
	}

	private static async Task DownloadIconAsync(string icon)
	{
		var iconPath = GetLocalIconPath(icon);
		if (File.Exists(iconPath)) return;
		await _iconDownloadSemaphore.WaitAsync();
		try {
			if (File.Exists(iconPath)) return;
			var iconResp = await httpClient.GetAsync(icon);
			iconResp.EnsureSuccessStatusCode();
			await using var iconStream = await iconResp.Content.ReadAsStreamAsync();
			await using var fileStream = File.Create(iconPath);
			await iconStream.CopyToAsync(fileStream);
		} finally {
			_iconDownloadSemaphore.Release();
		}
	}

	public static async Task DownloadIconsAsync(Window window, IEnumerable<string> icons)
	{
		var failures = new ConcurrentBag<string>();
		await Task.WhenAll(icons.Where(i => !string.IsNullOrEmpty(i)).Select(async icon => {
			try { await DownloadIconAsync(icon); } catch { failures.Add(icon); }
		}));

		if (!failures.IsEmpty) {
			MessageBox.Show(window, "Error", $"Failed to download {failures.Count} icon(s).");
		}
	}

	public static async Task LoadFile(Window window, string file, string cacheDir)
	{
		var exportCacheFile = Path.Combine(cacheDir, file + ".json");
		if (!File.Exists(exportCacheFile)) {
			try {
				var resp = await httpClient.GetAsync("https://raw.githubusercontent.com/calamity-inc/warframe-public-export-plus/refs/heads/senpai/" + file + ".json");
				resp.EnsureSuccessStatusCode();
				var json = await resp.Content.ReadAsStringAsync();
				await File.WriteAllTextAsync(exportCacheFile, json);
			} catch {
				MessageBox.Show(window, "Error", "Failed to download file: " + file);
			}
		}

		try {
			switch (file) {
				case "dict.en": {
						using var stream = File.OpenRead(exportCacheFile);
						using var doc = await JsonDocument.ParseAsync(stream);
						var builder = new Dictionary<string, string>(StringComparer.Ordinal);
						foreach (var prop in doc.RootElement.EnumerateObject()) {
							builder[prop.Name] = prop.Value.ToString();
						}
						lang = builder.ToFrozenDictionary();
						break;
					}
				case "ExportWarframes":
					exportWarframes = await ParseDictionary(exportCacheFile);
					break;
				case "ExportWeapons":
					exportWeapons = await ParseDictionary(exportCacheFile);
					break;
				case "ExportSentinels":
					exportSentinels = await ParseDictionary(exportCacheFile);
					break;
				case "ExportRecipes": {
						await using var stream = File.OpenRead(exportCacheFile);
						using var doc = await JsonDocument.ParseAsync(stream);
						var builder = new Dictionary<string, (string, JsonElement)>(StringComparer.Ordinal);
						foreach (var r in doc.RootElement.EnumerateObject()) {
							if (r.Value.TryGetProperty("resultType", out var rt) && rt.GetString() is string rtStr) {
								builder[rtStr] = (r.Name, r.Value.Clone());
							}
						}
						exportRecipes = builder.ToFrozenDictionary();
						break;
					}
				case "ExportRegions":
					exportRegions = await ParseDictionary(exportCacheFile);
					break;
				case "ExportResources":
					exportResources = await ParseDictionary(exportCacheFile);
					break;
				case "ExportMissionTypes":
					exportMissionTypes = await ParseDictionary(exportCacheFile);
					break;
				case "ExportFactions":
					exportFactions = await ParseDictionary(exportCacheFile);
					break;
				case "ExportTextIcons":
					exportTextIcons = await ParseDictionary(exportCacheFile);
					break;
				case "ExportMisc": {
						await using var stream = File.OpenRead(exportCacheFile);
						exportMisc = await JsonDocument.ParseAsync(stream);
						break;
					}
			}
		} catch (Exception ex) {
			MessageBox.Show(window, "Error", "Failed to load file: " + file + " — " + ex.Message);
		}
	}

	private static string ResolveName(string langKey)
	{
		var name = lang.TryGetValue(langKey, out var value) ? value : langKey;
		return name.Replace("<ARCHWING> ", "");
	}

	private static IEnumerable<string> GetIngredientIconUrls(string type)
	{
		if (!exportRecipes.TryGetValue(type, out var recipe)) yield break;
		var ingredients = recipe.Item2.GetProperty("ingredients");
		if (ingredients.ValueKind != JsonValueKind.Array) yield break;
		foreach (var ingredient in ingredients.EnumerateArray()) {
			var ingredientType = ingredient.GetProperty("ItemType").GetString() ?? "";
			if (string.IsNullOrWhiteSpace(ingredientType)) continue;
			if (!exportResources.TryGetValue(ingredientType, out var resource) || resource.ValueKind != JsonValueKind.Object) continue;
			var icon = resource.GetProperty("icon").GetString();
			if (!string.IsNullOrEmpty(icon)) yield return icon;
		}
	}

	private static ObservableCollection<RecipeIngredient> BuildIngredients(string parentName, string parentType, string blueprintPath)
	{
		var result = new ObservableCollection<RecipeIngredient>();
		if (!exportRecipes.TryGetValue(parentType, out var recipe)) return result;
		var ingredientArray = recipe.Item2.GetProperty("ingredients");
		if (ingredientArray.ValueKind != JsonValueKind.Array) return result;

		result.Add(new RecipeIngredient(parentName + " Blueprint", parentType, 1, blueprintPath) { RecipeKey = recipe.Item1});
		foreach (var ingredient in ingredientArray.EnumerateArray()) {
			var ingredientType = ingredient.GetProperty("ItemType").GetString() ?? "";
			var ingredientCount = ingredient.GetProperty("ItemCount").GetInt32();
			if (string.IsNullOrWhiteSpace(ingredientType)) continue;
			if (!exportResources.TryGetValue(ingredientType, out var resource) || resource.ValueKind != JsonValueKind.Object) continue;
			var ingredientLangKey = resource.GetProperty("name").GetString() ?? "";
			result.Add(new RecipeIngredient(
				ResolveName(ingredientLangKey),
				ingredientType,
				ingredientCount,
				GetLocalIconPath(resource.GetProperty("icon").GetString() ?? "")
			));
		}
		return result;
	}

	private static bool ShouldSkipWeapon(string type, JsonElement weapon)
	{
		if (type.Contains("PvPVariant") || type.Contains("Doppelganger")) return true;
		if (!weapon.TryGetProperty("partType", out var partProp)) return false;
		var partType = partProp.GetString();
		// moas, hounds, k-drives, zaw blades and amp prisms
		return partType != "LWPT_MOA_HEAD" && partType != "LWPT_ZANUKA_HEAD" && partType != "LWPT_HB_DECK" && partType != "LWPT_BLADE" && partType != "LWPT_AMP_OCULUS";
	}

	public static async Task LoadExports(Window window)
	{
		string blueprintPath = Path.Combine(AppContext.BaseDirectory, "assets", "blueprint.png");
		try {
			itemsList.Clear();
			var iconUrls = new HashSet<string>(StringComparer.Ordinal);
			foreach (var (type, element) in exportWarframes
				.Concat(exportWeapons.Where(kvp => !ShouldSkipWeapon(kvp.Key, kvp.Value)))
				.Concat(exportSentinels.Where(kvp => !kvp.Key.Contains("/Pets/")))) {
				iconUrls.Add(element.GetProperty("icon").ToString());
				foreach (var url in GetIngredientIconUrls(type)) iconUrls.Add(url);
			}
			await DownloadIconsAsync(window, iconUrls);

			foreach (var (type, warframe) in exportWarframes) {
				var name = ResolveName(warframe.GetProperty("name").GetString() ?? "");
				var category = warframe.GetProperty("productCategory").GetString() ?? "";
				if (category is "MechSuits" or "SpaceSuits") category = "Vehicles";
				itemsList.Add(new Item(name, type, BuildIngredients(name, type, blueprintPath), category, GetLocalIconPath(warframe.GetProperty("icon").ToString()), false));
			}

			foreach (var (type, weapon) in exportWeapons) {
				if (ShouldSkipWeapon(type, weapon)) continue;
				var name = ResolveName(weapon.GetProperty("name").ToString());
				var category = weapon.GetProperty("productCategory").GetString() ?? "";
				if (type.Contains("/Hoverboard/")) category = "Vehicles";
				else if (type.Contains("/Pets/")) category = "Companions";
				else if (type.Contains("Amp") && type.Contains("Barrel")) category = "OperatorAmps";
				itemsList.Add(new Item(name, type, BuildIngredients(name, type, blueprintPath), category, GetLocalIconPath(weapon.GetProperty("icon").ToString()), false));
			}

			foreach (var (type, sentinel) in exportSentinels) {
				if (type.Contains("/Pets/")) continue;
				var name = ResolveName(sentinel.GetProperty("name").GetString() ?? "");
				itemsList.Add(new Item(name, type, BuildIngredients(name, type, blueprintPath), "Companions", GetLocalIconPath(sentinel.GetProperty("icon").ToString()), false));
			}
		} catch (Exception ex) {
			MessageBox.Show(window, "Error", "Failed to load exports: " + ex.Message);
		}
	}
}