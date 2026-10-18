using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using Zeus.Sim;

public static class Loc
{
    const string SettingsPath = "user://instellingen.json";
    static bool _initialized, _saveBlocked, _dataAlerted;
    static readonly string NL = System.Environment.NewLine;
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
        var debugLanguage = GameSession.DebugEnvironment("ZEUS_LANG");
        if (debugLanguage is "en" or "nl") Language = debugLanguage;
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
            if (language == "en" && !_dataAlerted)
            {
                _dataAlerted = true;      // fixed text on purpose: without the data folder no translation exists
                OS.Alert("De map 'data' naast GaffersNotebook.exe ontbreekt of is onvolledig. Pak de hele zip uit en verplaats de exe niet." + NL + "Verwacht bestand: " + path
                    + NL + NL + "The folder 'data' next to GaffersNotebook.exe is missing or incomplete. Extract the whole zip and do not move the exe." + NL + "Expected file: " + path,
                    "Gaffer's Notebook");
            }
            return "{}";
        }
    }

    static void LoadTables()
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo(Language == "nl" ? "nl-NL" : "en-GB");
        // Numbers formatted with ToString/interpolation outside Loc.T must follow the game language, not the OS (decimal comma in the English UI).
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.CurrentCulture = culture;
        _table = new LanguageTable(ReadTable("en"), Language == "en" ? "{}" : ReadTable("nl"), culture);
        FilePickerLanguage.Apply();
    }

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

    /// <summary>True when instellingen.json could not be read or set aside, so nothing is written to it.</summary>
    public static bool SaveBlocked { get { Initialize(); return _saveBlocked; } }

    public static bool SetSetting(string key, string value, bool save = true)
    {
        Initialize();
        _settings[key] = value;
        if (!save) return true;
        if (_saveBlocked) return false;
        if (GameSession.DebugEnvironment("ZEUS_NOSAVE").Length > 0) return true;      // visual and smoke checks must not touch the real settings
        return SafeFile.Write(SettingsPath, JsonSerializer.Serialize(_settings), out _);
    }

    public static string T(string key) { Initialize(); return _table.T(key); }
    public static string T(string key, params object[] args) { Initialize(); return _table.T(key, args); }

    // Built-in data labels are translated; a name entered by the player stays their own name.
    public static string Name(string value)
    {
        foreach (var prefix in new[] { "Fictieve speler ", "Fictieve reserve " })
            if (value.StartsWith(prefix, StringComparison.Ordinal))
                return T(prefix == "Fictieve speler " ? "common.player_number" : "common.reserve_number", value[prefix.Length..]);
        var key = "common.name." + value.ToLowerInvariant().Replace(' ', '_');
        var translated = T(key);
        return translated == key ? value : translated;
    }

    public static string Criterion(ChallengeCriterion c, double? actual = null)
    {
        var (_, unit, _) = ChallengeEvaluator.Metrics[c.Metric];
        var text = T("challenge.criterion", T("challenge.metric." + c.Metric),
            T(c.Operator == ">=" ? "challenge.at_least" : "challenge.at_most"), c.Value,
            unit.Length == 0 ? "" : " " + T("challenge.unit." + c.Metric));
        return actual is { } value ? T("challenge.criterion_actual", text, value) : text;
    }

    // Keep technical exceptions in the log, with a useful localized message in the player UI.
    public static string Error(Exception error)
    {
        GD.PushWarning(error.ToString());
        if (error is MatchCommandException)
        {
            var message = error.Message;
            if (message.Contains("Maximaal aantal wissels")) return T("pause.error.sub_limit");
            if (message.Contains("keeper") || message.Contains("Keeper")) return T("pause.error.keeper");
            if (message.Contains("uitgesloten")) return T("pause.error.sent_off");
            if (message.Contains("rustmoment")) return T("pause.error.stoppage");
            if (message.Contains("afgelopen of gestaakt")) return T("pause.error.finished");
            if (message.Contains("al gewisseld")) return T("pause.error.already_subbed");
            return T("pause.error.invalid");
        }
        return T(error is IOException or UnauthorizedAccessException ? "common.error.file" : "common.error.invalid");
    }
}
