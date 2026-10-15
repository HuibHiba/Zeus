// Main menu: pick home/away tactic and seed, start a match, open the tactic editor, or quit.
using System;
using System.IO;
using System.Linq;
using Godot;

public partial class Menu : Control
{
    OptionButton _home = null!, _away = null!;
    SpinBox _seed = null!;
    Label _message = null!;

    public override void _Ready()
    {
        var box = new VBoxContainer { Position = new Vector2(80, 60), CustomMinimumSize = new Vector2(520, 0) };
        AddChild(box);
        box.AddChild(new Label { Text = "ZEUS ONLINE" });
        box.AddChild(new Label { Text = "Tactisch voetbalspel: plaats je spelers vrij en kijk hoe de wedstrijd verloopt." });
        box.AddChild(new Label { Text = "Thuisteam (rood)" });
        _home = new OptionButton(); box.AddChild(_home);
        box.AddChild(new Label { Text = "Uitteam (blauw)" });
        _away = new OptionButton(); box.AddChild(_away);
        box.AddChild(new Label { Text = "Seed (zelfde seed, zelfde wedstrijd)" });
        _seed = new SpinBox { MinValue = 1, MaxValue = 999999, Value = 1 }; box.AddChild(_seed);

        var start = new Button { Text = "Start wedstrijd" }; start.Pressed += Start; box.AddChild(start);
        var edit = new Button { Text = "Bewerk thuistactiek in de editor" }; edit.Pressed += Edit; box.AddChild(edit);
        var quit = new Button { Text = "Afsluiten" }; quit.Pressed += () => GetTree().Quit(); box.AddChild(quit);
        _message = new Label(); box.AddChild(_message);
        Reload();
    }

    void Reload()
    {
        var files = Directory.GetFiles(GameSession.TacticsDir(), "*.json").Select(p => Path.GetFileName(p)!).OrderBy(n => n).ToList();
        _home.Clear(); _away.Clear();
        foreach (var f in files) { _home.AddItem(f); _away.AddItem(f); }
        if (files.Count > 1) _away.Select(Math.Min(1, files.Count - 1));
    }

    static string FullPath(OptionButton b) => Path.Combine(GameSession.TacticsDir(), b.GetItemText(b.Selected));

    void Start()
    {
        if (_home.ItemCount == 0) { _message.Text = "Geen tactieken gevonden in tactics/."; return; }
        GameSession.HomePath = FullPath(_home);
        GameSession.AwayPath = FullPath(_away);
        GameSession.Seed = (ulong)_seed.Value;
        GetTree().ChangeSceneToFile("res://Main.tscn");
    }

    void Edit()
    {
        if (_home.ItemCount == 0) return;
        GameSession.EditPath = FullPath(_home);
        GetTree().ChangeSceneToFile("res://Editor.tscn");
    }
}
