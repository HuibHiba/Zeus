// Pause-and-adjust panel (PRD: pause and steer, no real-time intervention): substitutions and tactic changes at a rest moment.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Zeus.Sim;
using Side = Zeus.Sim.Side;

public partial class PauseMenu : PanelContainer
{
    readonly Match _match;
    readonly Squad _squad;
    readonly Side _team;                        // the side the player controls (Home outside the competition, Away when he plays away)
    readonly Action _close;
    string? _out, _in;
    Label _status = null!, _subsLeft = null!;
    VBoxContainer _field = null!, _bench = null!;
    readonly Dictionary<string, (HSlider Slider, Label Label, double Initial)> _sliders = new();
    OptionButton _form = null!, _block = null!;

    Color TeamColor => _team == Side.Home ? UiTheme.Home : UiTheme.Away;

    public PauseMenu(Match match, Squad squad, Side team, Action close)
    {
        _match = match; _squad = squad; _team = team; _close = close;
    }

    public override void _Ready()
    {
        UiTheme.ApplyPaper(this);
        Position = new Vector2(110, 34);
        CustomMinimumSize = new Vector2(1000, 0);
        Size = new Vector2(1200, 0);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiTheme.Paper, BorderColor = new Color(UiTheme.Accent, 0.7f), BorderWidthTop = 3,
            ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 14, ContentMarginBottom = 16,
        });
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 8); AddChild(col);
        var head = new HBoxContainer(); col.AddChild(head);
        head.AddChild(UiTheme.Label(Loc.T("pause.title"), 34, UiTheme.Accent, display: true));
        head.AddChild(UiTheme.Label(_team == Side.Home ? Loc.T("pause.you_home") : Loc.T("pause.you_away"), UiTheme.Small + 1, TeamColor));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var resume = UiTheme.Primary(Loc.T("pause.resume")); resume.CustomMinimumSize = new Vector2(260, 48); resume.Pressed += _close; head.AddChild(resume);
        _status = UiTheme.Label(Loc.T("pause.help"), UiTheme.Small + 1, UiTheme.Muted);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart; col.AddChild(_status);

        var tabs = new TabContainer { CustomMinimumSize = new Vector2(0, 460) }; col.AddChild(tabs);
        BuildSubs(tabs);
        BuildTactic(tabs);
        RefreshSubs();
        if (int.TryParse(GameSession.DebugEnvironment("ZEUS_PAUSETAB"), out var tab)) tabs.CurrentTab = tab;      // visual check of a tab
        if (GameSession.DebugEnvironment("ZEUS_PAUSEDEMO").Length > 0)      // visual/functional check: substitute the left winger for reserve 2
        {
            _out = _match.SquadIdOf(_team, _match.TacticOf(_team).Roster[5].Id); _in = "B2";
            DoSubstitute();
        }
    }

    // ---- substitutions -------------------------------------------------------------------------------------------

    void BuildSubs(TabContainer tabs)
    {
        var root = new VBoxContainer { Name = "subs" }; root.AddThemeConstantOverride("separation", 8); tabs.AddChild(root); tabs.SetTabTitle(tabs.GetTabCount() - 1, Loc.T(root.Name == "subs" ? "pause.subs_tab" : "pause.tactic_tab"));
        _subsLeft = UiTheme.Label("", UiTheme.Body, UiTheme.Text); root.AddChild(_subsLeft);
        var cols = new HBoxContainer(); cols.AddThemeConstantOverride("separation", 40); root.AddChild(cols);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; cols.AddChild(left);
        left.AddChild(UiTheme.Label(Loc.T("pause.on_pitch"), 22, TeamColor, display: true));
        _field = new VBoxContainer(); _field.AddThemeConstantOverride("separation", 3); left.AddChild(_field);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; cols.AddChild(right);
        right.AddChild(UiTheme.Label(Loc.T("pause.bench"), 22, UiTheme.Muted, display: true));
        _bench = new VBoxContainer(); _bench.AddThemeConstantOverride("separation", 3); right.AddChild(_bench);
        var go = UiTheme.Primary(Loc.T("pause.substitute")); go.CustomMinimumSize = new Vector2(0, 48); root.AddChild(go);
        go.Pressed += DoSubstitute;
    }

    string NameOf(string id)
    {
        var p = _squad.Starters.Concat(_squad.Bench).FirstOrDefault(x => x.Id == id);
        return p is null ? id : Loc.T("common.player_role", Loc.Name(p.Name), Loc.Name(p.Role));
    }

    void RefreshSubs()
    {
        _subsLeft.Text = Loc.T("pause.subs_left", _match.SubstitutionsLeft(_team), _match.SubstitutionsLeft(_team) + _match.SubstitutionsUsed(_team));
        foreach (var c in _field.GetChildren()) c.QueueFree();
        foreach (var c in _bench.GetChildren()) c.QueueFree();
        var snapshot = _match.GetSnapshot();
        foreach (var slot in _match.TacticOf(_team).Roster.Select(r => r.Id))
        {
            var id = _match.SquadIdOf(_team, slot);
            if (id is null) continue;
            var state = snapshot.Players.FirstOrDefault(p => p.Team == _team && p.Id == slot);
            string suffix = state is null ? "" : state.SentOff ? Loc.T("pause.sent_off") : $"  •  {state.LongEnergy * 100:0}%{(state.YellowCards > 0 ? Loc.T("pause.yellow") : "")}";
            var b = Row($"{slot}  {NameOf(id)}{suffix}", id == _out);
            var captured = id;
            b.Pressed += () => { _out = captured; RefreshSubs(); };
            _field.AddChild(b);
        }
        foreach (var reserve in _match.BenchOf(_team))
        {
            var b = Row($"{reserve.Id}  {NameOf(reserve.Id)}", reserve.Id == _in);
            var captured = reserve.Id;
            b.Pressed += () => { _in = captured; RefreshSubs(); };
            _bench.AddChild(b);
        }
    }

    static Button Row(string text, bool selected)
    {
        var b = new Button { Text = text, ToggleMode = true, ThemeTypeVariation = "Chip", Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 30), ClipText = true };
        b.SetPressedNoSignal(selected);
        return b;
    }

    void SetStatus(string text, bool good)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", good ? new Color("3E6B3A") : new Color("8E1F14"));
    }

    void DoSubstitute()
    {
        if (_out is null || _in is null) { SetStatus(Loc.T("pause.choose_players"), false); return; }
        try
        {
            _match.Substitute(_team, _out, _in);
            OnboardingProgress.Mark("substitution");
            SetStatus(Loc.T("pause.substituted", NameOf(_out), NameOf(_in)), true);
            _out = _in = null;
        }
        catch (MatchCommandException e) { SetStatus(Loc.Error(e), false); }
        RefreshSubs();
    }

    // ---- tactic ----------------------------------------------------------------------------------------------------

    void BuildTactic(TabContainer tabs)
    {
        var tactic = _match.TacticOf(_team);
        var root = new VBoxContainer { Name = "tactic" }; root.AddThemeConstantOverride("separation", 8); tabs.AddChild(root); tabs.SetTabTitle(tabs.GetTabCount() - 1, Loc.T(root.Name == "subs" ? "pause.subs_tab" : "pause.tactic_tab"));
        root.AddChild(UiTheme.Label(Loc.Name(tactic.Name), UiTheme.Body, UiTheme.Muted));
        void Slider(string key, string title, double value, string note)
        {
            var label = UiTheme.Label(Loc.T("common.label_value_note", title, value, note), UiTheme.Body); root.AddChild(label);
            var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, Value = value }; root.AddChild(slider);
            value = slider.Value;                   // the slider rounds to its step: compare against the rounded value, or "unchanged" never matches
            slider.ValueChanged += v => label.Text = Loc.T("common.label_value_note", title, v, note);
            _sliders[key] = (slider, label, value);
        }
        Slider("compact", Loc.T("pause.compactness"), tactic.Team.Compactness ?? 0.5, "");
        Slider("druk", Loc.T("pause.pressing"), tactic.Team.PressingIntensity, Loc.T("pause.pressing_note"));
        Slider("tempo", Loc.T("pause.tempo"), tactic.Team.Tempo, Loc.T("pause.tempo_note"));
        Slider("lijn", Loc.T("pause.line_height"), tactic.Team.LineHeight, Loc.T("pause.line_note"));
        root.AddChild(UiTheme.Label(Loc.T("pause.pressing_style"), UiTheme.Small, UiTheme.Muted));
        _form = new OptionButton { CustomMinimumSize = new Vector2(0, 40) };
        foreach (var f in new[] { "zone", "man", "gemengd" }) { _form.AddItem(Loc.Name(f)); _form.SetItemMetadata(_form.ItemCount - 1, f); }
        _form.Select(Array.IndexOf(new[] { "zone", "man", "gemengd" }, tactic.Team.PressingForm) is var i and >= 0 ? i : 0);
        root.AddChild(_form);
        root.AddChild(UiTheme.Label(Loc.T("pause.block"), UiTheme.Small, UiTheme.Muted));
        _block = new OptionButton { CustomMinimumSize = new Vector2(0, 40) };
        _block.AddItem(Loc.T("pause.unchanged")); _block.SetItemMetadata(0, "");
        foreach (var variant in tactic.DefendVariants.Keys.OrderBy(k => k))
        {
            _block.AddItem(Loc.Name(variant.ToString().ToLowerInvariant())); _block.SetItemMetadata(_block.ItemCount - 1, variant.ToString());
        }
        root.AddChild(_block);
        if (tactic.DefendVariants.Count == 0)
            root.AddChild(UiTheme.Label(Loc.T("pause.no_blocks"), UiTheme.Small, UiTheme.Muted));
        var apply = UiTheme.Primary(Loc.T("pause.adjust")); apply.CustomMinimumSize = new Vector2(0, 48); root.AddChild(apply);
        apply.Pressed += DoAdjust;
    }

    void DoAdjust()
    {
        double? Changed(string key) => Math.Abs(_sliders[key].Slider.Value - _sliders[key].Initial) > 1e-9 ? _sliders[key].Slider.Value : null;
        var form = (string)_form.GetItemMetadata(_form.Selected);
        var tactic = _match.TacticOf(_team);
        BlockHeight? block = _block.Selected > 0 && Enum.TryParse<BlockHeight>((string)_block.GetItemMetadata(_block.Selected), out var parsed) ? parsed : null;
        var adjustment = new TacticAdjustment(Tempo: Changed("tempo"), PressingIntensity: Changed("druk"), LineHeight: Changed("lijn"),
            Compactness: Changed("compact"), PressingForm: form != tactic.Team.PressingForm ? form : null, Block: block);
        if (adjustment.IsEmpty) { SetStatus(Loc.T("pause.no_changes"), false); return; }
        try
        {
            _match.Adjust(_team, adjustment);
            foreach (var key in _sliders.Keys.ToList()) _sliders[key] = (_sliders[key].Slider, _sliders[key].Label, _sliders[key].Slider.Value);
            SetStatus(Loc.T("pause.adjusted"), true);
        }
        catch (MatchCommandException e) { SetStatus(Loc.Error(e), false); }
    }
}
