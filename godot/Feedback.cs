// Feedback file (F9 or the Feedback button) and the "something went wrong" message. Creates one zip on the desktop; nothing is sent.
// Contents: screenshot, info.txt (version, language, screen, window, renderer, OS/CPU/RAM), godot.log, gamelog.txt (ring log),
// the progress files, last_match.json and the tester's note (opmerking.txt). Logic without Godot: Zeus.Sim.FeedbackPack.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using Zeus.Sim;

public static class Feedback
{
    static FeedbackOverlay? _open;
    public static bool IsOpen => _open is not null && GodotObject.IsInstanceValid(_open);

    public static Button SmallButton()
    {
        var button = new Button { Text = Loc.T("feedback.button"), TooltipText = Loc.T("feedback.tooltip"), CustomMinimumSize = new Vector2(0, 40), FocusMode = Control.FocusModeEnum.None };
        button.Pressed += () => Open(button);
        return button;
    }

    /// <summary>Opens the note dialog (or the error message when <paramref name="error"/> is set) above the current screen.</summary>
    public static void Open(Node from, bool error = false)
    {
        if (IsOpen) return;
        var tree = from.GetTree();
        if (tree is null) return;
        byte[]? shot = null;
        try { shot = tree.Root.GetViewport().GetTexture().GetImage().SavePngToBuffer(); }
        catch (Exception e) { FeedbackLog.Event("screenshot failed: " + e.Message); }
        _open = new FeedbackOverlay(tree, shot, error);
        tree.Root.AddChild(_open);
        FeedbackLog.Event(error ? "error message shown" : "feedback dialog opened");
    }

    /// <summary>For the exception catchers: safe from any thread.</summary>
    public static void ShowErrorFromAnyThread()
    {
        if (!FeedbackLog.ErrorDialogAllowed()) return;
        Callable.From(() =>
        {
            try { if (Engine.GetMainLoop() is SceneTree tree && tree.Root is not null) Open(tree.Root, error: true); }
            catch (Exception e) { FeedbackLog.Event("error message failed: " + e.Message); }
        }).CallDeferred();
    }

    public static bool LastOnDesktop { get; private set; }

    public static string DesktopDir()
    {
        LastOnDesktop = false;
        var debug = GameSession.DebugEnvironment("ZEUS_FEEDBACK_DIR");
        if (debug.Length > 0) { Directory.CreateDirectory(debug); return debug; }
        try
        {
            var desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
            if (desktop.Length > 0 && Directory.Exists(desktop)) { LastOnDesktop = true; return desktop; }
        }
        catch (Exception) { }
        var fallback = ProjectSettings.GlobalizePath("user://feedback");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    /// <summary>Builds the zip. Individual failures only add a line to info.txt; returns the path or throws when the zip itself cannot be written.</summary>
    public static string Create(string note, byte[]? screenshot, string screenName)
    {
        var user = SafeUserName();
        var notes = new List<string>();
        var entries = new List<(string Name, byte[]? Data)>();
        string Clean(string text) => FeedbackPack.Anonymize(text, user);

        if (screenshot is { Length: > 0 }) entries.Add(("screenshot.png", screenshot)); else notes.Add("screenshot: not available");

        var userDir = ProjectSettings.GlobalizePath("user://");
        var present = FeedbackPack.ExistingProgressFiles(userDir, File.Exists, out var missing);
        foreach (var name in present)
        {
            try { entries.Add(("voortgang/" + name, FeedbackPack.Utf8(Clean(File.ReadAllText(Path.Combine(userDir, name)))))); }
            catch (Exception e) { notes.Add($"{name}: unreadable ({e.GetType().Name})"); }
        }
        if (missing.Count > 0) notes.Add("not present (skipped): " + string.Join(", ", missing));

        try
        {
            var path = ProjectSettings.GlobalizePath("user://logs/godot.log");
            string log = "";
            if (File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                log = reader.ReadToEnd();
            }
            if (log.Trim().Length == 0) notes.Add("godot.log: empty or missing, see gamelog.txt");
            else entries.Add(("godot.log", FeedbackPack.Utf8(Clean(FeedbackPack.TailLines(log, 2000)))));
        }
        catch (Exception e) { notes.Add($"godot.log: locked or unreadable ({e.GetType().Name}), see gamelog.txt"); }

        entries.Add(("gamelog.txt", FeedbackPack.Utf8(string.Join("\n", FeedbackLog.Lines) + "\n")));
        if (FeedbackLog.LastMatchJson is { } match) entries.Add(("last_match.json", FeedbackPack.Utf8(Clean(match))));
        try
        {
            var crash = ProjectSettings.GlobalizePath("user://feedback_vorige_crash.txt");
            if (File.Exists(crash)) entries.Add(("vorige_crash.txt", FeedbackPack.Utf8(Clean(File.ReadAllText(crash)))));
        }
        catch (Exception e) { notes.Add("previous crash log unreadable: " + e.GetType().Name); }
        entries.Add(("opmerking.txt", FeedbackPack.Utf8(Clean(note.Trim()) + "\n")));
        entries.Insert(0, ("info.txt", FeedbackPack.Utf8(Clean(InfoText(screenName, notes)))));

        var dir = DesktopDir();
        var file = Path.Combine(dir, FeedbackPack.ZipName(DateTime.Now));
        for (int n = 2; File.Exists(file); n++) file = Path.Combine(dir, Path.GetFileNameWithoutExtension(FeedbackPack.ZipName(DateTime.Now)) + $"-{n}.zip");
        using var ms = new MemoryStream();
        FeedbackPack.WriteZip(ms, entries);
        File.WriteAllBytes(file, ms.ToArray());
        FeedbackLog.Event("feedback file created: " + Path.GetFileName(file));
        return file;
    }

    static string SafeUserName() { try { return System.Environment.UserName; } catch (Exception) { return ""; } }

    static string InfoText(string screenName, List<string> notes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Gaffer's Notebook feedback");
        sb.AppendLine($"created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"version: {GameInfo.Version}");
        sb.AppendLine($"language: {Loc.Language}");
        sb.AppendLine($"screen: {screenName}");
        void Try(string label, Func<string> get) { try { sb.AppendLine($"{label}: {get()}"); } catch (Exception e) { sb.AppendLine($"{label}: unavailable ({e.GetType().Name})"); } }
        Try("window", () => $"{DisplayServer.WindowGetSize().X}x{DisplayServer.WindowGetSize().Y} (mode {DisplayServer.WindowGetMode()})");
        Try("viewport", () => $"{(Engine.GetMainLoop() as SceneTree)?.Root.GetVisibleRect().Size}");
        Try("screen size", () => $"{DisplayServer.ScreenGetSize()}");
        Try("renderer", () => $"{RenderingServer.GetCurrentRenderingMethod()} / {RenderingServer.GetCurrentRenderingDriverName()} / {RenderingServer.GetVideoAdapterName()}");
        Try("os", () => $"{OS.GetName()} {OS.GetVersion()}");
        Try("cpu", () => $"{OS.GetProcessorName()} ({OS.GetProcessorCount()} threads)");
        Try("ram", () =>
        {
            var physical = (long)OS.GetMemoryInfo()["physical"];
            return physical > 0 ? (physical / 1024.0 / 1024 / 1024).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " GB" : "unknown";
        });
        Try("godot", () => Engine.GetVersionInfo()["string"].ToString());
        sb.AppendLine($"debug build: {OS.IsDebugBuild()}");
        foreach (var note in notes) sb.AppendLine("note: " + note);
        return sb.ToString();
    }
}

/// <summary>The dialog itself: a note field with "Create" and "Cancel" (Enter = create, Esc = cancel), then the saved message with "Open folder".</summary>
public partial class FeedbackOverlay : CanvasLayer
{
    readonly SceneTree _tree;
    readonly byte[]? _shot;
    readonly bool _error;
    readonly bool _wasPaused;
    readonly string _screen;
    Control _root = null!;
    LineEdit? _note;
    string _savedPath = "";
    bool _done;

    public FeedbackOverlay(SceneTree tree, byte[]? shot, bool error)
    {
        _tree = tree; _shot = shot; _error = error; _wasPaused = tree.Paused;
        _screen = tree.CurrentScene?.Name ?? "?";
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        _tree.Paused = true;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        UiTheme.ApplyPaper(_root);
        AddChild(_root);
        ShowAsk();
        if (GameSession.DebugEnvironment("ZEUS_SHOT").Length > 0 && DebugMode().Length > 0) _debugFrames = 0;
    }

    int _debugFrames = -1;
    static string DebugMode() => GameSession.DebugEnvironment("ZEUS_FEEDBACK") is { Length: > 0 } m ? m : GameSession.DebugEnvironment("ZEUS_THROW").Length > 0 ? "throw" : "";

    VBoxContainer NewPanel(string title, Color? titleColor = null)
    {
        foreach (var child in _root.GetChildren()) child.QueueFree();
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);
        var center = new CenterContainer(); center.SetAnchorsPreset(Control.LayoutPreset.FullRect); _root.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(820, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiTheme.Paper, BorderColor = new Color(UiTheme.Accent, 0.8f), BorderWidthTop = 3,
            ContentMarginLeft = 32, ContentMarginRight = 32, ContentMarginTop = 22, ContentMarginBottom = 24,
        });
        center.AddChild(panel);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 12); panel.AddChild(box);
        box.AddChild(UiTheme.Label(title, 36, titleColor ?? UiTheme.Accent, display: true));
        return box;
    }

    static Label Text(string text, Color? color = null)
    {
        var l = UiTheme.Label(text, UiTheme.Body, color ?? UiTheme.Text);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(740, 0);
        return l;
    }

    void ShowAsk()
    {
        var box = NewPanel(Loc.T(_error ? "feedback.error_title" : "feedback.title"), _error ? UiTheme.Bad : null);
        box.AddChild(Text(Loc.T(_error ? "feedback.error_body" : "feedback.intro")));
        box.AddChild(Text(Loc.T("feedback.contents"), UiTheme.Muted));
        box.AddChild(UiTheme.Label(Loc.T("feedback.note_label"), UiTheme.Small + 2, UiTheme.Muted));
        _note = new LineEdit { PlaceholderText = Loc.T("feedback.note_placeholder"), MaxLength = 2000, CustomMinimumSize = new Vector2(0, 46) };
        _note.TextSubmitted += _ => Make();
        box.AddChild(_note);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; box.AddChild(row);
        row.AddThemeConstantOverride("separation", 16);
        var cancel = UiTheme.Secondary(Loc.T(_error ? "feedback.close" : "feedback.cancel")); cancel.CustomMinimumSize = new Vector2(220, 52); cancel.Pressed += Close; row.AddChild(cancel);
        var make = UiTheme.Primary(Loc.T(_error ? "feedback.error_make" : "feedback.make")); make.CustomMinimumSize = new Vector2(300, 52); make.Pressed += Make; row.AddChild(make);
        _note.GrabFocus();
        if (GameSession.DebugEnvironment("ZEUS_FEEDBACK_TEXT") is { Length: > 0 } text) _note.Text = text;
    }

    void Make()
    {
        if (_done) return;
        var note = _note?.Text ?? "";
        try
        {
            _savedPath = Feedback.Create(note, _shot, _screen);
            ShowSaved(null);
        }
        catch (Exception e)
        {
            FeedbackLog.Fault("feedback", e);
            ShowSaved(Loc.Error(e));
        }
    }

    void ShowSaved(string? failure)
    {
        _done = true;
        var box = NewPanel(Loc.T("feedback.title"), failure is null ? null : UiTheme.Bad);
        if (failure is not null) box.AddChild(Text(Loc.T("feedback.failed", failure), UiTheme.Bad));
        else
        {
            box.AddChild(Text(Loc.T(Feedback.LastOnDesktop ? "feedback.saved_desktop" : "feedback.saved_other", _savedPath), UiTheme.Good));
            box.AddChild(Text(Loc.T("feedback.saved_hint"), UiTheme.Muted));
        }
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; box.AddChild(row);
        row.AddThemeConstantOverride("separation", 16);
        if (failure is null)
        {
            var open = UiTheme.Secondary(Loc.T("feedback.open_folder")); open.CustomMinimumSize = new Vector2(240, 52);
            open.Pressed += () => OS.ShellShowInFileManager(_savedPath); row.AddChild(open);
        }
        var close = UiTheme.Primary(Loc.T("feedback.close")); close.CustomMinimumSize = new Vector2(220, 52); close.Pressed += Close; row.AddChild(close);
        close.GrabFocus();
    }

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true } k) return;
        if (k.Keycode is Key.Escape) { Close(); GetViewport().SetInputAsHandled(); }
        else if (_done && k.Keycode is Key.Enter or Key.KpEnter) { Close(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode is Key.F9) GetViewport().SetInputAsHandled();
    }

    void Close()
    {
        _tree.Paused = _wasPaused;
        FeedbackLog.Event("feedback dialog closed");
        QueueFree();
    }

    // Debug only (ZEUS_FEEDBACK + ZEUS_SHOT): screenshot of this dialog, then quit.
    public override void _Process(double delta)
    {
        if (_debugFrames < 0) return;
        var mode = DebugMode();
        if (_debugFrames == 2 && mode == "done" && !_done) Make();
        if (++_debugFrames != 8) return;
        GetViewport().GetTexture().GetImage().SavePng(GameSession.DebugEnvironment("ZEUS_SHOT") + "_feedback_" + mode + ".png");
        GetTree().Quit();
    }
}
