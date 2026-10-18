// Tactic library overlay (home screen): all starter and own tactics with a build-up preview, favourites, sorting,
// rename/delete for own tactics, import/export (Editor.Sharing code) and the hand-off to the editor or the home team.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Zeus.Sim;

public partial class TacticLibrary : CanvasLayer
{
    const string FavoritesKey = "tactic_favorites";

    sealed record Item(GameSession.TacticEntry Entry, string Name, string Formation, (string Id, double X, double Y)[] Dots, DateTime Modified, bool Broken);

    static TacticLibrary? _current;
    bool _wasPaused, _closed;
    Action<string?> _onClose = null!;
    string? _useAsHome;

    List<Item> _items = new();
    Item? _selected;
    int _sort;
    bool _favoritesOnly;
    VBoxContainer _list = null!;
    Control _detail = null!;
    Label _status = null!;
    ConfirmationDialog _confirm = null!, _renameDialog = null!;
    LineEdit _renameEdit = null!;
    FileDialog? _importDialog, _exportDialog;

    public static bool IsOpen => _current is not null && GodotObject.IsInstanceValid(_current) && !_current._closed;

    /// <summary>onClose gets the tactic chosen with "use as my tactic" (or null); the caller refreshes its own list.</summary>
    public static void Open(Node owner, Action<string?> onClose)
    {
        if (IsOpen || SettingsMenu.IsOpen || AboutMenu.IsOpen || Feedback.IsOpen) return;
        _current = new TacticLibrary { Name = "TacticLibrary", Layer = 70, ProcessMode = ProcessModeEnum.Always, _onClose = onClose };
        owner.AddChild(_current);
    }

    public static void Escape() { if (IsOpen) _current!.Close(); }

    public override void _Ready()
    {
        _wasPaused = GetTree().Paused;
        GetTree().Paused = true;
        var root = new Control(); root.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(root);
        UiTheme.ApplyPaper(root);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .65f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.AddChild(shade);
        var center = new CenterContainer(); center.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1180, 0) }; center.AddChild(panel);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 10); panel.AddChild(content);
        content.AddChild(UiTheme.Label(Loc.T("library.title"), 38, UiTheme.Ink, true));

        var bar = new HBoxContainer(); bar.AddThemeConstantOverride("separation", 10); content.AddChild(bar);
        bar.AddChild(UiTheme.Label(Loc.T("library.sort"), UiTheme.Small, UiTheme.Graphite));
        var sort = new OptionButton { Name = "library_sort", CustomMinimumSize = new Vector2(190, 38) };
        foreach (var key in new[] { "name", "formation", "recent" }) sort.AddItem(Loc.T("library.sort." + key));
        sort.ItemSelected += i => { _sort = (int)i; Rebuild(); };
        bar.AddChild(sort);
        var only = new CheckBox { Name = "library_favorites", Text = Loc.T("library.favorites_only") };
        only.Toggled += on => { _favoritesOnly = on; Rebuild(); };
        bar.AddChild(only);
        bar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var import = new Button { Name = "library_import", Text = Loc.T("editor.import_file"), CustomMinimumSize = new Vector2(0, 38) };
        import.Pressed += ShowImport; bar.AddChild(import);

        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 16); content.AddChild(body);
        var scroll = new ScrollContainer { Name = "library_scroll", CustomMinimumSize = new Vector2(640, 440), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _list.AddThemeConstantOverride("separation", 4); scroll.AddChild(_list);
        _detail = new VBoxContainer { CustomMinimumSize = new Vector2(450, 0) }; body.AddChild(_detail);

        _status = UiTheme.Label("", UiTheme.Small, UiTheme.Graphite);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart; _status.CustomMinimumSize = new Vector2(10, 26);
        content.AddChild(_status);
        var back = UiTheme.Primary(Loc.T("about.back")); back.Name = "library_back"; back.Pressed += Close; content.AddChild(back);

        _confirm = new ConfirmationDialog { Theme = UiTheme.PaperTheme() };
        _confirm.GetOkButton().Text = Loc.T("common.ok"); _confirm.GetCancelButton().Text = Loc.T("common.cancel");
        _confirm.Confirmed += () => _onConfirm?.Invoke();
        AddChild(_confirm);
        _renameDialog = new ConfirmationDialog { Title = Loc.T("library.rename"), Theme = UiTheme.PaperTheme() };
        _renameDialog.GetOkButton().Text = Loc.T("common.ok"); _renameDialog.GetCancelButton().Text = Loc.T("common.cancel");
        _renameEdit = new LineEdit { CustomMinimumSize = new Vector2(360, 0) };
        _renameDialog.AddChild(_renameEdit);
        _renameDialog.Confirmed += DoRename;
        AddChild(_renameDialog);

        Rebuild(GameSession.HomePath);
        // Keyboard: start on the selected tactic in the list (arrows walk it), Back otherwise.
        if (_list.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.ButtonPressed) is { } current) current.GrabFocus();
        else back.GrabFocus();
    }

    // ---- data ------------------------------------------------------------------------------------------------------

    static HashSet<string> Favorites() => Loc.Setting(FavoritesKey, "").Split('|', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    static void SaveFavorites(HashSet<string> ids) => Loc.SetSetting(FavoritesKey, string.Join('|', ids.OrderBy(i => i, StringComparer.Ordinal)));

    static Item Load(GameSession.TacticEntry entry)
    {
        var stem = Path.GetFileNameWithoutExtension(entry.Path);
        var name = entry.IsOwn ? stem.Replace('_', ' ') : Loc.Name(stem);
        var modified = File.Exists(entry.Path) ? File.GetLastWriteTime(entry.Path) : DateTime.MinValue;
        try
        {
            var model = TacticEditorModel.LoadFile(entry.Path);
            var dots = model.PlayerIds.Select(id => { var (x, y) = model.GetPosition("opbouw", id); return (id, x, y); }).ToArray();
            return new Item(entry, name, FormationText(stem, model.Name, dots), dots, modified, false);
        }
        catch (Exception e) { GD.PushWarning(e.ToString()); return new Item(entry, name, "?", Array.Empty<(string, double, double)>(), modified, true); }
    }

    // shortcut: formation comes from digits in the file/tactic name (e.g. 4231), else a rough count by position id; upgrade when tactics store their formation.
    static string FormationText(string stem, string name, (string Id, double X, double Y)[] dots)
    {
        foreach (var text in new[] { stem, name })
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            if (digits.Length is 3 or 4 && digits.Sum(c => c - '0') == 10) return string.Join('-', digits.Select(c => c.ToString()));
        }
        var field = dots.Select(d => d.Id).Where(id => id != "GK").ToArray();
        int def = field.Count(id => id.EndsWith('B') || id is "RC" or "LC" or "CC");
        int fwd = field.Count(id => id.Contains('S') && !id.Contains("CM") || id is "RW" or "LW");
        return field.Length == 10 ? $"{def}-{10 - def - fwd}-{fwd}" : "?";
    }

    void Rebuild(string? select = null)
    {
        var favorites = Favorites();
        select ??= _selected?.Entry.Path;
        IEnumerable<Item> items = GameSession.Tactics().Select(Load);
        if (_favoritesOnly) items = items.Where(i => favorites.Contains(i.Entry.Id));
        items = _sort switch
        {
            1 => items.OrderBy(i => i.Formation, StringComparer.Ordinal).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
            2 => items.OrderByDescending(i => i.Modified).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
        };
        _items = items.OrderByDescending(i => favorites.Contains(i.Entry.Id)).ToList();      // stable: favourites first, chosen order within
        _selected = _items.FirstOrDefault(i => i.Entry.Path == select) ?? _items.FirstOrDefault();

        foreach (var child in _list.GetChildren()) { _list.RemoveChild(child); child.QueueFree(); }
        if (_items.Count == 0) _list.AddChild(UiTheme.Label(Loc.T("library.empty"), UiTheme.Body, UiTheme.Graphite));
        var group = new ButtonGroup();
        foreach (var item in _items)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); _list.AddChild(row);
            var star = new Button { Text = favorites.Contains(item.Entry.Id) ? "★" : "☆", Flat = true, CustomMinimumSize = new Vector2(44, 64), TooltipText = Loc.T("library.favorite"), FocusMode = Control.FocusModeEnum.None };
            star.AddThemeFontSizeOverride("font_size", 26);
            star.AddThemeColorOverride("font_color", UiTheme.HomeInk);
            star.Pressed += () => { var f = Favorites(); if (!f.Remove(item.Entry.Id)) f.Add(item.Entry.Id); SaveFavorites(f); Rebuild(); };
            row.AddChild(star);
            var pick = new Button { ToggleMode = true, ButtonGroup = group, ButtonPressed = item == _selected, CustomMinimumSize = new Vector2(0, 64), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipContents = true };
            pick.Pressed += () => { _selected = item; BuildDetail(); };
            row.AddChild(pick);
            var inner = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; inner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            inner.OffsetLeft = 8; inner.OffsetTop = 4; inner.OffsetBottom = -4; inner.OffsetRight = -8;
            inner.AddThemeConstantOverride("separation", 12);
            pick.AddChild(inner);
            inner.AddChild(new TacticPitch(item.Dots, UiTheme.HomeInk) { CustomMinimumSize = new Vector2(88, 56), MouseFilter = Control.MouseFilterEnum.Ignore });
            var text = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
            text.AddThemeConstantOverride("separation", 0);
            text.AddChild(UiTheme.Label(item.Name, UiTheme.Body, UiTheme.Ink));
            text.AddChild(UiTheme.Label(item.Formation + "  •  " + Loc.T(item.Entry.IsOwn ? "library.own" : "library.starter"), UiTheme.Small, UiTheme.Graphite));
            foreach (var label in text.GetChildren().OfType<Label>()) { label.MouseFilter = Control.MouseFilterEnum.Ignore; label.ClipText = true; }
            inner.AddChild(text);
        }
        BuildDetail();
    }

    // ---- detail pane -----------------------------------------------------------------------------------------------

    void BuildDetail()
    {
        foreach (var child in _detail.GetChildren()) { _detail.RemoveChild(child); child.QueueFree(); }
        if (_selected is not { } item) return;
        var box = (VBoxContainer)_detail; box.AddThemeConstantOverride("separation", 8);
        var title = UiTheme.Label(item.Name, 30, UiTheme.Ink, true); title.ClipText = true; box.AddChild(title);
        box.AddChild(UiTheme.Label(item.Formation + "  •  " + Loc.T(item.Entry.IsOwn ? "library.own" : "library.starter"), UiTheme.Body, UiTheme.Graphite));
        box.AddChild(new TacticPitch(item.Dots, UiTheme.HomeInk) { CustomMinimumSize = new Vector2(450, 250) });
        box.AddChild(UiTheme.Label(Loc.T("library.preview_note"), UiTheme.Small, UiTheme.Graphite));
        var use = UiTheme.Primary(Loc.T("library.use")); use.Name = "library_use"; use.CustomMinimumSize = new Vector2(0, 46); use.Disabled = item.Broken;
        use.Pressed += () => { _useAsHome = item.Entry.Path; Close(); };
        box.AddChild(use);
        var grid = new GridContainer { Columns = 2 }; grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); box.AddChild(grid);
        Button Btn(string key, Action pressed, bool disabled = false)
        {
            var b = new Button { Name = "library_" + key, Text = Loc.T("library." + key), CustomMinimumSize = new Vector2(0, 42), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Disabled = disabled };
            b.Pressed += pressed; grid.AddChild(b); return b;
        }
        Btn("edit", () => { _closeToEditor = item.Entry.Path; Close(); }, item.Broken);
        Btn("export", () => ShowExport(item), item.Broken);
        Btn("rename", () => { _renameEdit.Text = item.Name; _renameDialog.PopupCentered(); _renameEdit.GrabFocus(); _renameEdit.SelectAll(); }, !item.Entry.IsOwn);
        Btn("delete", () =>
        {
            _confirm.Title = Loc.T("library.delete");
            _confirm.DialogText = Loc.T("library.delete_confirm", item.Name);
            _onConfirm = () => DoDelete(item);
            _confirm.PopupCentered();
        }, !item.Entry.IsOwn);
    }

    string? _closeToEditor;
    Action? _onConfirm;

    void Say(string text, bool ok = true) { _status.Text = text; _status.AddThemeColorOverride("font_color", ok ? UiTheme.Graphite : UiTheme.Bad); }

    // ---- actions ---------------------------------------------------------------------------------------------------

    void DoDelete(Item item)
    {
        if (!item.Entry.IsOwn) return;      // built-in tactics live in the game folder: never delete them
        try
        {
            File.Delete(item.Entry.Path);
            var f = Favorites(); if (f.Remove(item.Entry.Id)) SaveFavorites(f);
            Say(Loc.T("library.deleted", item.Name));
            _selected = null; Rebuild();
        }
        catch (Exception e) { Say(Loc.T("library.failed") + Loc.Error(e), false); }
    }

    void DoRename()
    {
        if (_selected is not { Entry.IsOwn: true } item) return;
        try
        {
            var stem = TacticFileNames.Sanitize(_renameEdit.Text);
            if (stem.Length == 0) { Say(Loc.T("library.bad_name"), false); return; }
            var target = Path.Combine(Path.GetDirectoryName(item.Entry.Path)!, stem + ".json");
            // Windows paths are case-insensitive: a case-only rename points at the same file and must never delete it.
            bool sameFile = string.Equals(target, item.Entry.Path, StringComparison.OrdinalIgnoreCase);
            if (!sameFile && File.Exists(target)) { Say(Loc.T("library.exists", stem), false); return; }
            var model = TacticEditorModel.LoadFile(item.Entry.Path);
            model.Name = _renameEdit.Text.Trim();
            if (sameFile && target != item.Entry.Path) File.Move(item.Entry.Path, target);   // only the letter case changes
            model.Save(target);
            var newId = TacticCatalog.OwnPrefix + stem + ".json";
            if (!sameFile) File.Delete(item.Entry.Path);
            if (!string.Equals(newId, item.Entry.Id, StringComparison.Ordinal))
            {
                var f = Favorites(); if (f.Remove(item.Entry.Id)) { f.Add(newId); SaveFavorites(f); }
            }
            Say(Loc.T("library.renamed", stem));
            Rebuild(Path.GetFullPath(target));
        }
        catch (Exception e) { Say(Loc.T("library.failed") + Loc.Error(e), false); }
    }

    void ShowImport()
    {
        if (_importDialog is null)
        {
            _importDialog = Editor.TacticDialog(this, FileDialog.FileModeEnum.OpenFile, Loc.T("editor.import_file"));
            _importDialog.GetOkButton().Text = Loc.T("editor.import_file");
            _importDialog.FileSelected += ImportFile;
        }
        _importDialog.PopupCenteredRatio(0.75f);
    }

    void ImportFile(string path)
    {
        try
        {
            var model = TacticExchange.Import(path);      // validated by the real loader before anything is written
            var dir = GameSession.WritableTacticsDir();
            Directory.CreateDirectory(dir);
            var stem = TacticExchange.AvailableName(model.Name, dir);
            var target = Path.Combine(dir, stem + ".json");
            model.Save(target);
            Say(Loc.T("library.imported", Path.GetFileName(path)));
            Rebuild(Path.GetFullPath(target));
        }
        catch (Exception e) { Say(Loc.T("editor.import_failed") + Loc.Error(e), false); }
    }

    void ShowExport(Item item)
    {
        if (_exportDialog is null)
        {
            _exportDialog = Editor.TacticDialog(this, FileDialog.FileModeEnum.SaveFile, Loc.T("editor.export_file"));
            _exportDialog.GetOkButton().Text = Loc.T("editor.export_file");
            _exportDialog.FileSelected += ExportFile;
        }
        var name = TacticFileNames.Sanitize(Path.GetFileNameWithoutExtension(item.Entry.Path));
        _exportDialog.CurrentFile = (name.Length > 0 ? name : "tactic") + ".json";
        _exportDialog.PopupCenteredRatio(0.75f);
    }

    void ExportFile(string path)
    {
        if (_selected is null) return;
        try
        {
            TacticExchange.Export(TacticEditorModel.LoadFile(_selected.Entry.Path), path);
            Say(Loc.T("editor.exported_file", Path.GetFileName(path)));
        }
        catch (Exception e) { Say(Loc.T("editor.export_failed") + Loc.Error(e), false); }
    }

    void Close()
    {
        if (_closed) return;
        _closed = true; _current = null;
        GetTree().Paused = _wasPaused;
        var tree = GetTree();
        QueueFree();
        if (_closeToEditor is { } edit)
        {
            _onClose(null);
            GameSession.EditPath = edit;
            tree.ChangeSceneToFile("res://Editor.tscn");
        }
        else _onClose(_useAsHome);
    }

    public override void _ExitTree()
    {
        if (!_closed) GetTree().Paused = _wasPaused;
        if (_current == this) _current = null;
    }

    int _frames;
    static readonly string Shot = GameSession.DebugEnvironment("ZEUS_SHOT");
    // ZEUS_SHOT=<prefix> saves a screenshot after a few frames and quits (visual check; ZEUS_LIBRARY=2 selects the first own tactic).
    public override void _Process(double delta)
    {
        if (Shot.Length == 0) return;
        if (++_frames == 3 && GameSession.DebugEnvironment("ZEUS_LIBRARY") == "2" && _items.FirstOrDefault(i => i.Entry.IsOwn) is { } own) Rebuild(own.Entry.Path);
        if (_frames != 14) return;
        GetViewport().GetTexture().GetImage().SavePng(Shot + "_library.png");
        GetTree().Quit();
    }
}

/// <summary>A small pitch (goal on the left) with one dot per player; the goalkeeper is hollow.</summary>
public partial class TacticPitch : Control
{
    readonly (string Id, double X, double Y)[] _dots;
    readonly Color _color;

    public TacticPitch((string Id, double X, double Y)[] dots, Color color) { _dots = dots; _color = color; }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, UiTheme.Cover);
        var line = new Color(UiTheme.Text, 0.55f);
        DrawRect(r, line, false, 1);
        DrawLine(new Vector2(r.Size.X / 2, 0), new Vector2(r.Size.X / 2, r.Size.Y), line, 1);
        DrawArc(r.Size / 2, r.Size.Y * 0.14f, 0, Mathf.Tau, 24, line, 1);
        foreach (var x in new[] { 0f, r.Size.X * 0.84f })
            DrawRect(new Rect2(x, r.Size.Y * 0.22f, r.Size.X * 0.16f, r.Size.Y * 0.56f), line, false, 1);
        float radius = Mathf.Clamp(r.Size.Y * 0.035f, 2.5f, 7f);
        foreach (var (id, x, y) in _dots)
        {
            var p = new Vector2((float)x * r.Size.X, (float)y * r.Size.Y);
            if (id == "GK") DrawArc(p, radius, 0, Mathf.Tau, 12, UiTheme.Text, 1.5f);
            else { DrawCircle(p, radius + 1, UiTheme.Text); DrawCircle(p, radius, _color); }
        }
    }
}
