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
        UiTheme.Apply(this);
        Position = new Vector2(110, 34);
        CustomMinimumSize = new Vector2(1000, 0);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(UiTheme.Panel, 0.97f), BorderColor = new Color(UiTheme.Accent, 0.7f), BorderWidthTop = 3,
            ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 14, ContentMarginBottom = 16,
        });
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 8); AddChild(col);
        var head = new HBoxContainer(); col.AddChild(head);
        head.AddChild(UiTheme.Label("PAUZE  •  WISSELS EN TACTIEK", 34, UiTheme.Accent, display: true));
        head.AddChild(UiTheme.Label(_team == Side.Home ? "   jij bent THUIS (rood)" : "   jij bent UIT (blauw)", UiTheme.Small + 1, TeamColor));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var resume = UiTheme.Primary("DOORSPELEN  (Enter)"); resume.CustomMinimumSize = new Vector2(260, 48); resume.Pressed += _close; head.AddChild(resume);
        _status = UiTheme.Label("Kies een speler van het veld en een reserve, of pas de tactiek aan.", UiTheme.Small + 1, UiTheme.Muted);
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
        var root = new VBoxContainer { Name = "WISSELS" }; root.AddThemeConstantOverride("separation", 8); tabs.AddChild(root);
        _subsLeft = UiTheme.Label("", UiTheme.Body, UiTheme.Text); root.AddChild(_subsLeft);
        var cols = new HBoxContainer(); cols.AddThemeConstantOverride("separation", 40); root.AddChild(cols);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; cols.AddChild(left);
        left.AddChild(UiTheme.Label("OP HET VELD", 22, TeamColor, display: true));
        _field = new VBoxContainer(); _field.AddThemeConstantOverride("separation", 3); left.AddChild(_field);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; cols.AddChild(right);
        right.AddChild(UiTheme.Label("BANK", 22, UiTheme.Muted, display: true));
        _bench = new VBoxContainer(); _bench.AddThemeConstantOverride("separation", 3); right.AddChild(_bench);
        var go = UiTheme.Primary("WISSEL UITVOEREN"); go.CustomMinimumSize = new Vector2(0, 48); root.AddChild(go);
        go.Pressed += DoSubstitute;
    }

    string NameOf(string id)
    {
        var p = _squad.Starters.Concat(_squad.Bench).FirstOrDefault(x => x.Id == id);
        return p is null ? id : $"{p.Name}  ({p.Role.Replace('_', ' ')})";
    }

    void RefreshSubs()
    {
        _subsLeft.Text = $"Wissels over: {_match.SubstitutionsLeft(_team)} van {_match.SubstitutionsLeft(_team) + _match.SubstitutionsUsed(_team)}";
        foreach (var c in _field.GetChildren()) c.QueueFree();
        foreach (var c in _bench.GetChildren()) c.QueueFree();
        var snapshot = _match.GetSnapshot();
        foreach (var slot in _match.TacticOf(_team).Roster.Select(r => r.Id))
        {
            var id = _match.SquadIdOf(_team, slot);
            if (id is null) continue;
            var state = snapshot.Players.FirstOrDefault(p => p.Team == _team && p.Id == slot);
            string suffix = state is null ? "" : state.SentOff ? "  •  uitgesloten" : $"  •  {state.LongEnergy * 100:0}%{(state.YellowCards > 0 ? "  •  geel" : "")}";
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
        _status.AddThemeColorOverride("font_color", good ? UiTheme.Good : UiTheme.Bad);
    }

    void DoSubstitute()
    {
        if (_out is null || _in is null) { SetStatus("Kies eerst een speler van het veld en een reserve.", false); return; }
        try
        {
            _match.Substitute(_team, _out, _in);
            SetStatus($"Gewisseld: {NameOf(_out)} eruit, {NameOf(_in)} erin.", true);
            _out = _in = null;
        }
        catch (MatchCommandException e) { SetStatus(e.Message, false); }
        RefreshSubs();
    }

    // ---- tactic ----------------------------------------------------------------------------------------------------

    void BuildTactic(TabContainer tabs)
    {
        var tactic = _match.TacticOf(_team);
        var root = new VBoxContainer { Name = "TACTIEK" }; root.AddThemeConstantOverride("separation", 8); tabs.AddChild(root);
        root.AddChild(UiTheme.Label(tactic.Name, UiTheme.Body, UiTheme.Muted));
        void Slider(string key, string title, double value, string note)
        {
            var label = UiTheme.Label($"{title}: {value:F2}{note}", UiTheme.Body); root.AddChild(label);
            var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, Value = value }; root.AddChild(slider);
            value = slider.Value;                   // the slider rounds to its step: compare against the rounded value, or "unchanged" never matches
            slider.ValueChanged += v => label.Text = $"{title}: {v:F2}{note}";
            _sliders[key] = (slider, label, value);
        }
        Slider("compact", "Compactheid", tactic.Team.Compactness ?? 0.5, "");
        Slider("druk", "Druk zetten", tactic.Team.PressingIntensity, "  (de spelers volgen dit via pressing en triggers)");
        Slider("tempo", "Tempo", tactic.Team.Tempo, "  (experimenteel: werkt pas als het tempo-effect is ingeschakeld)");
        Slider("lijn", "Lijnhoogte", tactic.Team.LineHeight, "  (experimenteel: werkt pas als het lijnhoogte-effect is ingeschakeld)");
        root.AddChild(UiTheme.Label("Pressingvorm (experimenteel)", UiTheme.Small, UiTheme.Muted));
        _form = new OptionButton { CustomMinimumSize = new Vector2(0, 40) };
        foreach (var f in new[] { "zone", "man", "gemengd" }) { _form.AddItem(f); }
        _form.Select(Array.IndexOf(new[] { "zone", "man", "gemengd" }, tactic.Team.PressingForm) is var i and >= 0 ? i : 0);
        root.AddChild(_form);
        root.AddChild(UiTheme.Label("Verdedigend blok", UiTheme.Small, UiTheme.Muted));
        _block = new OptionButton { CustomMinimumSize = new Vector2(0, 40) };
        _block.AddItem("ongewijzigd"); _block.SetItemMetadata(0, "");
        foreach (var variant in tactic.DefendVariants.Keys.OrderBy(k => k))
        {
            _block.AddItem(variant.ToString().ToLowerInvariant()); _block.SetItemMetadata(_block.ItemCount - 1, variant.ToString());
        }
        root.AddChild(_block);
        if (tactic.DefendVariants.Count == 0)
            root.AddChild(UiTheme.Label("Deze tactiek heeft geen blokvarianten; maak ze in de tactiekmaker (Regels > Blokwissels).", UiTheme.Small, UiTheme.Muted));
        var apply = UiTheme.Primary("TACTIEK AANPASSEN"); apply.CustomMinimumSize = new Vector2(0, 48); root.AddChild(apply);
        apply.Pressed += DoAdjust;
    }

    void DoAdjust()
    {
        double? Changed(string key) => Math.Abs(_sliders[key].Slider.Value - _sliders[key].Initial) > 1e-9 ? _sliders[key].Slider.Value : null;
        var form = _form.GetItemText(_form.Selected);
        var tactic = _match.TacticOf(_team);
        BlockHeight? block = _block.Selected > 0 && Enum.TryParse<BlockHeight>((string)_block.GetItemMetadata(_block.Selected), out var parsed) ? parsed : null;
        var adjustment = new TacticAdjustment(Tempo: Changed("tempo"), PressingIntensity: Changed("druk"), LineHeight: Changed("lijn"),
            Compactness: Changed("compact"), PressingForm: form != tactic.Team.PressingForm ? form : null, Block: block);
        if (adjustment.IsEmpty) { SetStatus("Er is niets gewijzigd.", false); return; }
        try
        {
            _match.Adjust(_team, adjustment);
            foreach (var key in _sliders.Keys.ToList()) _sliders[key] = (_sliders[key].Slider, _sliders[key].Label, _sliders[key].Slider.Value);
            SetStatus("Tactiek aangepast; geldt vanaf het hervatten.", true);
        }
        catch (MatchCommandException e) { SetStatus(e.Message, false); }
    }
}
