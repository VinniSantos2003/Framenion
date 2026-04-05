using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace framenion.Src;

public class ItemDTO
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("icon")]
	public string Icon { get; set; } = "";

	[JsonPropertyName("productCategory")]
	public string Category { get; set; } = "";

	[JsonPropertyName("partType")]
	public string? PartType { get; set; }
}

public class RecipeDTO
{
	[JsonPropertyName("resultType")]
	public string ResultType { get; set; } = "";

	[JsonPropertyName("ingredients")]
	public List<RecipeIngredientDTO> Ingredients { get; set; } = [];
}

public class RecipeIngredientDTO
{
	[JsonPropertyName("ItemType")]
	public string Type { get; set; } = "";

	[JsonPropertyName("ItemCount")]
	public int Count { get; set; } = 1;
}

public class ResourceDTO
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("icon")]
	public string Icon { get; set; } = "";
}

public class RegionDTO
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("systemName")]
	public string SystemName { get; set; } = "";

	[JsonPropertyName("missionType")]
	public string MissionType { get; set; } = "";

	[JsonPropertyName("faction")]
	public string Faction { get; set; } = "";

	[JsonPropertyName("minEnemyLevel")]
	public int MinEnemyLevel { get; set; } = 0;

	[JsonPropertyName("maxEnemyLevel")]
	public int MaxEnemyLevel { get; set; } = 0;
}

[JsonSerializable(typeof(ItemDTO))]
[JsonSerializable(typeof(ResourceDTO))]
[JsonSerializable(typeof(RecipeDTO))]
[JsonSerializable(typeof(RecipeIngredientDTO))]
[JsonSerializable(typeof(RegionDTO))]

[JsonSerializable(typeof(Dictionary<string, ItemDTO>))]
[JsonSerializable(typeof(Dictionary<string, ResourceDTO>))]
[JsonSerializable(typeof(Dictionary<string, RecipeDTO>))]
[JsonSerializable(typeof(Dictionary<string, RegionDTO>))]

public partial class ExportJsonContext : JsonSerializerContext { }
