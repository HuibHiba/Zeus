using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Zeus.Sim;

/// <summary>
/// Friends mode without a server (roadmap 3b): paste a friend's tactic (share code or JSON) as an opponent, play a ladder match
/// (home v away from the menu, seed from the menu) and swap result codes. A pasted result is replayed on seed and tactics
/// before it counts, so a doctored score is rejected. Same overlay pattern as AboutMenu; the logic lives in Zeus.Sim.Ladder.
/// </summary>
public partial class Friends : CanvasLayer
{
    const string FilePath = "user://zeus_ladder.json";
    static Friends? _current;
    bool _wasPaused, _closed;
    Action? _onTacticsChanged;
    Label _status = null!;
    LineEdit _code = null!;
    VBoxContainer _table = null!;
    Button _play = null!;
    readonly List<LadderResult> _results = new();

    public static bool IsOpen => _current is not null && GodotObject.IsInstanceValid(_current) && !_current._closed;

    public static void Open(Node owner, Action? onTacticsChanged = null)
    {
        if (IsOpen || AboutMenu.IsOpen || SettingsMenu.IsOpen || Feedback.IsOpen) return;
        _current = new Friends { Name = "Friends", Layer = 70, ProcessMode = ProcessModeEnum.Always, _onTacticsChanged = onTacticsChanged };
        owner.AddChild(_current);
    }

    public override void _Ready()
    {
        _wasPaused = GetTree().Paused;
        GetTree().Paused = true;
        var root = new Control(); root.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(root);
        UiTheme.ApplyPaper(root);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .65f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.AddChild(shade);
        var center = new CenterContainer(); center.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1040, 0) }; center.AddChild(panel);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 10); panel.AddChild(content);
        content.AddChild(UiTheme.Label(Loc.T("friends.title"), 38, UiTheme.Ink, true));
        content.AddChild(Wrapped(Loc.T("friends.intro")));

        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); content.AddChild(row);
        var paste = UiTheme.Secondary(Loc.T("friends.paste_tactic")); paste.Name = "friends_paste_tactic"; paste.Pressed += PasteTactic; row.AddChild(paste);
        _play = UiTheme.Primary(Loc.T("friends.play")); _play.Name = "friends_play"; _play.Pressed += Play; row.AddChild(_play);
        var pasteResult = UiTheme.Secondary(Loc.T("friends.paste_result")); pasteResult.Name = "friends_paste_result"; pasteResult.Pressed += PasteResult; row.AddChild(pasteResult);
        var pasteClaim = UiTheme.Secondary(Loc.T("friends.paste_challenge")); pasteClaim.Name = "friends_paste_challenge"; pasteClaim.Pressed += PasteChallenge; row.AddChild(pasteClaim);

        var codeRow = new HBoxContainer(); codeRow.AddThemeConstantOverride("separation", 12); content.AddChild(codeRow);
        _code = new LineEdit { Editable = false, PlaceholderText = Loc.T("friends.code_placeholder"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; codeRow.AddChild(_code);
        var copy = UiTheme.Secondary(Loc.T("friends.copy_code")); copy.Name = "friends_copy"; copy.Pressed += CopyCode; codeRow.AddChild(copy);

        _status = Wrapped(""); _status.CustomMinimumSize = new Vector2(0, 44); content.AddChild(_status);
        content.AddChild(UiTheme.Label(Loc.T("friends.standings"), UiTheme.Body, UiTheme.Ink));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 260), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.AddChild(scroll);
        _table = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(_table);
        var back = UiTheme.Primary(Loc.T("about.back")); back.Name = "friends_back"; back.Pressed += Close; content.AddChild(back);

        Load();
        RefreshTable();
        back.GrabFocus();
    }

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        GetViewport().SetInputAsHandled();
        Close();
    }

    // ---- friend's tactic: share code or plain JSON from the clipboard, saved as an own tactic, selectable as away side in the menu ----
    void PasteTactic()
    {
        var text = DisplayServer.ClipboardGet();
        if (string.IsNullOrWhiteSpace(text)) { Say(Loc.T("editor.clipboard_empty"), false); return; }
        try
        {
            var model = TacticExchange.FromText(text);
            var dir = GameSession.OwnTacticsDir();
            Directory.CreateDirectory(dir);
            var stem = TacticExchange.AvailableName(model.Name, dir);
            model.Save(Path.Combine(dir, stem + ".json"));
            _onTacticsChanged?.Invoke();
            Say(Loc.T("friends.tactic_saved", stem.Replace('_', ' ')), true);
        }
        catch (Exception e) { Say(Loc.T("editor.invalid_tactic") + Loc.Error(e), false); }
    }

    // ---- ladder match: the menu's home v away on the menu's seed, simulated without live input so a friend can replay it ----
    void Play()
    {
        if (GameSession.HomePath is not { } home || GameSession.AwayPath is not { } away) { Say(Loc.T("menu.no_tactics"), false); return; }
        _play.Disabled = true;
        Say(Loc.T("friends.playing"), true);
        string homeJson = File.ReadAllText(home), awayJson = File.ReadAllText(away);
        string homeLevel = GameSession.HomeLevel, awayLevel = GameSession.AwayLevel, squads = GameSession.SquadsDir();
        ulong seed = GameSession.Seed;
        Task.Run(() =>
        {
            try
            {
                var result = Ladder.Play(homeJson, awayJson, homeLevel, awayLevel, seed, squads);
                Callable.From(() => Played(result)).CallDeferred();
            }
            catch (Exception e) { Callable.From(() => { _play.Disabled = false; Say(Loc.T("challenge.failed") + Loc.Error(e), false); }).CallDeferred(); }
        });
    }

    void Played(LadderResult result)
    {
        if (_closed) return;
        _play.Disabled = false;
        _results.Add(result);
        Save();
        _code.Text = Ladder.ToCode(result);
        Say(Loc.T("friends.played", result.HomeName, result.HomeGoals, result.AwayGoals, result.AwayName), true);
        RefreshTable();
    }

    void CopyCode()
    {
        if (_code.Text.Length == 0) { Say(Loc.T("friends.no_code"), false); return; }
        DisplayServer.ClipboardSet(_code.Text);
        Say(Loc.T("friends.code_copied"), true);
    }

    // ---- friend's result: verified by replaying it on the same seed and tactics before it enters the ladder ----
    void PasteResult()
    {
        var text = DisplayServer.ClipboardGet();
        if (string.IsNullOrWhiteSpace(text)) { Say(Loc.T("editor.clipboard_empty"), false); return; }
        LadderResult claimed;
        try { claimed = Ladder.FromCode(text); }
        catch (Exception e) { Say(Loc.T("friends.bad_code") + Loc.Error(e), false); return; }
        if (_results.Any(r => r.EventHash == claimed.EventHash && r.Seed == claimed.Seed)) { Say(Loc.T("friends.already"), false); return; }
        var home = FindTactic(claimed.HomeTactic); var away = FindTactic(claimed.AwayTactic);
        if (home is null || away is null) { Say(Loc.T("friends.tactic_unknown"), false); return; }
        _play.Disabled = true;
        Say(Loc.T("friends.verifying"), true);
        var squads = GameSession.SquadsDir();
        Task.Run(() =>
        {
            bool ok; string reason;
            try { ok = Ladder.Verify(claimed, home, away, squads, out reason); }
            catch (Exception e) { ok = false; reason = Loc.Error(e); }
            Callable.From(() => Verified(claimed, ok, reason)).CallDeferred();
        });
    }

    void Verified(LadderResult claimed, bool ok, string reason)
    {
        if (_closed) return;
        _play.Disabled = false;
        if (!ok)
        {
            Say(reason switch
            {
                "version" => Loc.T("friends.reject_version"),
                "tactic" => Loc.T("friends.reject_tactic"),
                "result" => Loc.T("friends.reject_result"),
                _ => Loc.T("friends.bad_code") + reason,
            }, false);
            return;
        }
        _results.Add(claimed);
        Save();
        Say(Loc.T("friends.accepted", claimed.HomeName, claimed.HomeGoals, claimed.AwayGoals, claimed.AwayName), true);
        RefreshTable();
    }

    // ---- friend's challenge claim: replayed on seeds 1..N with the same tactic and level before it is believed ----
    void PasteChallenge()
    {
        var text = DisplayServer.ClipboardGet();
        if (string.IsNullOrWhiteSpace(text)) { Say(Loc.T("editor.clipboard_empty"), false); return; }
        ChallengeClaim claim;
        try { claim = ChallengeShare.FromCode(text); }
        catch (Exception e) { Say(Loc.T("friends.bad_code") + Loc.Error(e), false); return; }
        var spec = ChallengeEvaluator.LoadAll(GameSession.ChallengesDir(), out _, Loc.Language).FirstOrDefault(s => s.Id == claim.ChallengeId);
        var tactic = FindTactic(claim.Tactic);
        if (tactic is null) { Say(Loc.T("friends.tactic_unknown"), false); return; }
        _play.Disabled = true;
        Say(Loc.T("friends.verifying"), true);
        string tactics = GameSession.TacticsDir(), squads = GameSession.SquadsDir();
        Task.Run(() =>
        {
            bool ok; string reason;
            try { ok = ChallengeShare.Verify(claim, spec, tactic, tactics, squads, out reason); }
            catch (Exception e) { ok = false; reason = Loc.Error(e); }
            Callable.From(() =>
            {
                if (_closed) return;
                _play.Disabled = false;
                Say(ok ? Loc.T("friends.challenge_ok", spec?.Name ?? claim.ChallengeId, claim.Stars, claim.TacticName, string.Join("  ", claim.Results.Select(r => $"{r.GoalsFor}-{r.GoalsAgainst}")))
                    : reason switch
                    {
                        "version" => Loc.T("friends.reject_version"),
                        "challenge" => Loc.T("friends.challenge_unknown"),
                        "tactic" => Loc.T("friends.reject_tactic"),
                        "result" => Loc.T("friends.challenge_rejected"),
                        _ => Loc.T("friends.bad_code") + reason,
                    }, ok);
            }).CallDeferred();
        });
    }

    /// <summary>The JSON of the local tactic (built-in or own) with this ladder hash, or null when the friend's tactic is not imported yet.</summary>
    static string? FindTactic(string hash)
    {
        foreach (var entry in GameSession.Tactics())
        {
            try
            {
                var json = File.ReadAllText(entry.Path);
                if (Ladder.TacticHash(json) == hash) return json;
            }
            catch (Exception) { }
        }
        return null;
    }

    void RefreshTable()
    {
        foreach (var child in _table.GetChildren()) child.QueueFree();
        var rows = Ladder.Standings(_results);
        if (rows.Count == 0) { _table.AddChild(Wrapped(Loc.T("friends.empty"))); return; }
        _table.AddChild(UiTheme.Label(Loc.T("friends.head"), UiTheme.Small, UiTheme.Ink));
        foreach (var r in rows)
            _table.AddChild(UiTheme.Label($"{r.Name}   {r.Played} / {r.Won}-{r.Drawn}-{r.Lost}   {r.GoalsFor}-{r.GoalsAgainst}   {r.Points} {Loc.T("friends.points")}", UiTheme.Small, UiTheme.Graphite));
    }

    void Load()
    {
        _results.Clear();
        if (SafeFile.Read(FilePath, out var text) != SafeFile.ReadStatus.Ok) return;
        try
        {
            foreach (var code in JsonSerializer.Deserialize<string[]>(text) ?? Array.Empty<string>())
            {
                try { _results.Add(Ladder.FromCode(code)); }
                catch (FormatException) { }      // a bad line is skipped, the rest stays
            }
        }
        catch (JsonException) { }
    }

    void Save()
    {
        if (GameSession.DebugEnvironment("ZEUS_NOSAVE").Length > 0) return;
        if (!SafeFile.Write(FilePath, JsonSerializer.Serialize(_results.Select(Ladder.ToCode).ToArray()), out var error)) Say(error, false);
    }

    void Say(string text, bool good) { _status.Text = text; _status.AddThemeColorOverride("font_color", good ? UiTheme.Good : UiTheme.Bad); }

    static Label Wrapped(string text)
    {
        var label = UiTheme.Label(text, UiTheme.Small, UiTheme.Graphite);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.CustomMinimumSize = new Vector2(10, 0);
        return label;
    }

    void Close()
    {
        if (_closed) return;
        _closed = true; _current = null;
        GetTree().Paused = _wasPaused;
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (!_closed) GetTree().Paused = _wasPaused;
        if (_current == this) _current = null;
    }
}
