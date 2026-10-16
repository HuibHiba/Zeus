using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using Zeus.Sim;

public static class Loc
{
    const string SettingsPath = "user://instellingen.json";
    static bool _initialized, _saveBlocked;
    static LanguageTable _table = new("{}", "{}");
    public static string Language { get; private set; } = "en";
    static readonly Dictionary<string, string> _settings = new();      // every key of instellingen.json, so one setting never erases another

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        var status = SafeFile.Read(SettingsPath, out var text);
        if (status == SafeFile.ReadStatus.Unreadable) _saveBlocked = true;
        if (status == SafeFile.ReadStatus.Ok)
        {
            try
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(text)
                    ?? throw new FormatException("Invalid settings.");
                foreach (var pair in settings) _settings[pair.Key] = pair.Value;
                Language = settings.GetValueOrDefault("language") == "nl" ? "nl" : "en";
            }
            catch (Exception e) when (e is JsonException or FormatException)
            {
                _saveBlocked = !SafeFile.SetAside(SettingsPath);
            }
        }
        LoadTables();
    }

    static string ReadTable(string language)
    {
        var root = OS.HasFeature("editor") ? Path.Combine(GameSession.DataRoot(), "data") : GameSession.DataRoot();
        var path = Path.Combine(root, "lang", language + ".json");
        try
        {
            var json = File.ReadAllText(path);
            LanguageTable.Parse(json);
            return json;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            GD.PushWarning("Language table unavailable: " + path + " (" + e.Message + ")");
            return "{}";
        }
    }

    static void LoadTables() => _table = new LanguageTable(ReadTable("en"), Language == "en" ? "{}" : ReadTable("nl"));

    public static bool SetLanguage(string language)
    {
        Initialize();
        if (language is not ("en" or "nl")) throw new ArgumentException("Unsupported language.", nameof(language));
        Language = language;
        LoadTables();
        if (_saveBlocked) return false;
        _settings["language"] = language;
        return SafeFile.Write(SettingsPath, JsonSerializer.Serialize(_settings), out _);
    }

    /// <summary>Other small user settings (for example "popups") live in the same file next to the language.</summary>
    public static string Setting(string key, string fallback) { Initialize(); return _settings.GetValueOrDefault(key) ?? fallback; }

    public static bool SetSetting(string key, string value)
    {
        Initialize();
        _settings[key] = value;
        if (_saveBlocked) return false;
        return SafeFile.Write(SettingsPath, JsonSerializer.Serialize(_settings), out _);
    }

    public static string T(string key) { Initialize(); return _table.T(key); }
    public static string T(string key, params object[] args) { Initialize(); return _table.T(key, args); }
}
