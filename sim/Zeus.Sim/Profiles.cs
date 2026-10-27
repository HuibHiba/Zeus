using System.Text.Json;

namespace Zeus.Sim;

/// <summary>Where a profile keeps its files. Profile 1 is the main folder (<c>user://</c>, the RC7 files stay where they are); later profiles live in <c>user://profielen/&lt;name&gt;/</c>.</summary>
public static class ProfileLayout
{
    public const string Folder = "profielen";

    /// <summary>A profile name is a short folder name; anything else counts as "main" so a bad setting can never escape <c>user://</c>.</summary>
    public static bool IsValidName(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= 32 && name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');

    /// <summary><c>user://file</c> for the main profile, <c>user://profielen/name/file</c> for another one.</summary>
    public static string Path(string fileName, string? profile) =>
        IsValidName(profile) ? $"user://{Folder}/{profile}/{fileName}" : $"user://{fileName}";
}

/// <summary>Contents of <c>profiel.json</c>: who the player is and the club. Indices are checked against the pickers on read.</summary>
public sealed class ProfileData
{
    public string Name { get; set; } = "";
    public int Skin { get; set; }
    public int Hair { get; set; }
    public int Outfit { get; set; }
    public int Colour { get; set; }
    public string ClubName { get; set; } = "";
    public int Kit1 { get; set; }
    public int Kit2 { get; set; } = 3;
    public int Crest { get; set; }
    public string Created { get; set; } = "";

    public string ToJson() => JsonSerializer.Serialize(new
    {
        version = 1, name = Name, coach = new { skin = Skin, hair = Hair, outfit = Outfit, colour = Colour },
        club = new { name = ClubName, kit1 = Kit1, kit2 = Kit2, crest = Crest }, created = Created,
    });

    public static ProfileData Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.GetProperty("version").GetInt32() != 1) throw new FormatException("Unsupported profile version.");
            var coach = root.GetProperty("coach");
            var club = root.GetProperty("club");
            int Pick(JsonElement e, string key, int count)
            {
                int v = e.GetProperty(key).GetInt32();
                return v >= 0 && v < count ? v : throw new FormatException($"Profile value '{key}' out of range.");
            }
            return new ProfileData
            {
                Name = Competition.CleanClubName(root.GetProperty("name").GetString()) ?? "",
                Skin = Pick(coach, "skin", 5), Hair = Pick(coach, "hair", 6), Outfit = Pick(coach, "outfit", 4), Colour = Pick(coach, "colour", 10),
                ClubName = Competition.CleanClubName(club.GetProperty("name").GetString()) ?? "",
                Kit1 = Pick(club, "kit1", 10), Kit2 = Pick(club, "kit2", 10), Crest = Pick(club, "crest", 8),
                Created = root.GetProperty("created").GetString() ?? "",
            };
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            throw new FormatException("Invalid profile data.", e);
        }
    }
}
