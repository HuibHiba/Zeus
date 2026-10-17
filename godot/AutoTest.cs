// Debug only: ZEUS_AUTOTEST=<script> drives the game with real input events (mouse and keys through Input.ParseInputEvent), so UI
// flows can be tested instead of only read. The script is a JSON array of step strings or a text file with one step per line:
//   wait 30 | wait 1.5s            frames or seconds
//   click 760 400 [shift]          viewport coordinates (1520x780)
//   click Settings                 the topmost visible button or tab whose text or tooltip contains this (case-insensitive)
//   click_node about_back [fx fy]  a visible node by name pattern or #Class, at a fraction of its rect (default the centre)
//   drag 100 100 300 200           press, move in steps, release
//   key Escape [ctrl] [shift]      Godot key name
//   shot name                      screenshot <out>/<name>.png
//   expect_node pattern            a visible node by name pattern or #Class; "!" in front: none may exist
//   expect_text substring          a visible Label/RichTextLabel/Button containing it; "!" in front: none may
//   timeout 5                      how long expect_* keeps retrying (seconds)
//   scene Editor                   change to res://Editor.tscn
//   quit
// Results go to <out>/autotest.log (PASS/FAIL per step) with <out> = ZEUS_AUTOTEST_OUT (default user://autotest); exit code 0/1.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

public partial class AutoTest : Node
{
    string[] _steps = Array.Empty<string>();
    string _out = "";
    int _index, _failures, _phase;
    ulong _until;                       // wait/expect deadline (ms) or frame countdown
    int _frames;
    double _timeout = 5;
    Vector2 _from, _to;
    readonly List<string> _log = new();

    public static void Install(Node owner)
    {
        var path = GameSession.DebugEnvironment("ZEUS_AUTOTEST");
        if (path.Length == 0) return;
        var test = new AutoTest { Name = "AutoTest", ProcessMode = ProcessModeEnum.Always };
        var outDir = GameSession.DebugEnvironment("ZEUS_AUTOTEST_OUT");
        test._out = outDir.Length > 0 ? outDir : ProjectSettings.GlobalizePath("user://autotest");
        Directory.CreateDirectory(test._out);
        var text = File.ReadAllText(path);
        test._steps = (text.TrimStart().StartsWith('[') ? JsonSerializer.Deserialize<string[]>(text)! : text.Split('\n'))
            .Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith('#')).ToArray();
        owner.AddChild(test);
    }

    public override void _Process(double delta)
    {
        if (_index >= _steps.Length) { Finish(); return; }
        var step = _steps[_index];
        var parts = step.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var arg = parts.Length > 1 ? parts[1].Trim() : "";
        bool first = _phase == 0;
        try
        {
            var done = parts[0] switch
            {
                "wait" => Wait(arg, first),
                "timeout" => Set(() => _timeout = double.Parse(arg, System.Globalization.CultureInfo.InvariantCulture)),
                "click" => Click(arg, first),
                "click_node" => ClickNode(arg, first),
                "drag" => Drag(arg),
                "key" => Key(arg),
                "shot" => Set(() => GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_out, arg + ".png"))),
                "expect_node" => Expect(arg, first, FindNodes),
                "expect_text" => Expect(arg, first, FindTexts),
                "scene" => Set(() => GetTree().ChangeSceneToFile($"res://{arg}.tscn")),
                "quit" => Quit(),
                _ => throw new FormatException("unknown step"),
            };
            if (done is null) { _phase++; return; }
            Record(done.Value, step, done.Value ? "" : _reason);
        }
        catch (Exception e) { Record(false, step, e.Message); }
    }

    string _reason = "";
    bool? Fail(string reason) { _reason = reason; return false; }
    static bool? Set(Action action) { action(); return true; }

    void Record(bool ok, string step, string reason)
    {
        if (!ok) _failures++;
        var line = $"{(ok ? "PASS" : "FAIL")}  {_index + 1,3}  {step}{(reason.Length > 0 ? "  -- " + reason : "")}";
        GD.Print("AUTOTEST " + line);
        _log.Add(line);
        _index++; _phase = 0;
    }

    void Finish()
    {
        var summary = $"{(_failures == 0 ? "PASS" : "FAIL")}  {_steps.Length - _failures}/{_steps.Length} steps";
        _log.Add(summary);
        GD.Print("AUTOTEST " + summary);
        File.WriteAllLines(Path.Combine(_out, "autotest.log"), _log);
        GetTree().Quit(_failures == 0 ? 0 : 1);
        SetProcess(false);
    }

    bool? Quit() { _index = _steps.Length; Finish(); return null; }

    bool? Wait(string arg, bool first)
    {
        if (first)
        {
            if (arg.EndsWith('s')) _until = Time.GetTicksMsec() + (ulong)(double.Parse(arg[..^1], System.Globalization.CultureInfo.InvariantCulture) * 1000);
            else _frames = int.Parse(arg);
        }
        return arg.EndsWith('s') ? (Time.GetTicksMsec() >= _until ? true : null) : (--_frames <= 0 ? true : null);
    }

    // Clicks take two frames: move + press, then release (Godot buttons fire on release).
    bool? Click(string arg, bool first)
    {
        if (first)
        {
            _shift = arg.EndsWith(" shift");
            if (_shift) arg = arg[..^6];
            var xy = arg.Split(' ');
            if (xy.Length == 2 && float.TryParse(xy[0], out var x) && float.TryParse(xy[1], out var y)) _from = new Vector2(x, y);
            else if (FindButton(arg) is { } at) _from = at;
            else return Fail("no visible button or tab with this text");
            Mouse(_from, true);
            return null;
        }
        Mouse(_from, false);
        _shift = false;
        return true;
    }
    bool _shift;

    bool? ClickNode(string arg, bool first)
    {
        if (first)
        {
            var a = arg.Split(' ');
            var fraction = a.Length == 3 ? new Vector2(float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture)) : new Vector2(0.5f, 0.5f);
            if (FindNodes(a[0]).OfType<Control>().LastOrDefault() is not { } c) return Fail("no visible node");
            _from = c.GetGlobalTransformWithCanvas() * (c.Size * fraction);
            Mouse(_from, true);
            return null;
        }
        Mouse(_from, false);
        return true;
    }

    const int DragSteps = 12;
    bool? Drag(string arg)
    {
        if (_phase == 0)
        {
            var v = arg.Split(' ').Select(t => float.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            (_from, _to) = (new Vector2(v[0], v[1]), new Vector2(v[2], v[3]));
            Mouse(_from, true);
            return null;
        }
        if (_phase <= DragSteps) { Move(_from.Lerp(_to, _phase / (float)DragSteps), MouseButtonMask.Left); return null; }
        Mouse(_to, false);
        return true;
    }

    bool? Key(string arg)
    {
        var a = arg.Split(' ');
        var code = OS.FindKeycodeFromString(a[0]);
        if (code == Godot.Key.None) return Fail("unknown key name");
        var e = new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = _phase == 0, CtrlPressed = a.Contains("ctrl"), ShiftPressed = a.Contains("shift") };
        if (code is >= Godot.Key.A and <= Godot.Key.Z) e.Unicode = a.Contains("shift") ? (int)code : (int)code + 32;
        Input.ParseInputEvent(e);
        return _phase == 0 ? null : true;
    }

    bool? Expect(string arg, bool first, Func<string, IEnumerable<Node>> find)
    {
        if (first) _until = Time.GetTicksMsec() + (ulong)(_timeout * 1000);
        bool negate = arg.StartsWith('!');
        bool found = find(negate ? arg[1..] : arg).Any();
        if (found != negate) return true;
        return Time.GetTicksMsec() >= _until ? Fail(negate ? "still present" : "not found") : null;
    }

    // Viewport coordinates through the window's stretch transform, so the events take the same path as a real mouse.
    Vector2 ToWindow(Vector2 p) => GetTree().Root.GetFinalTransform() * p;

    void Mouse(Vector2 at, bool pressed)
    {
        Move(at, pressed ? 0 : MouseButtonMask.Left);
        var w = ToWindow(at);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = pressed, Position = w, GlobalPosition = w, ShiftPressed = _shift,
            ButtonMask = pressed ? MouseButtonMask.Left : 0 });
    }

    void Move(Vector2 at, MouseButtonMask mask)
    {
        var w = ToWindow(at);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = w, GlobalPosition = w, ButtonMask = mask });
    }

    // All nodes in the tree, internal children included (TabContainer's tab bar), skipping hidden branches.
    IEnumerable<Node> Visible(Node node)
    {
        foreach (var child in node.GetChildren(includeInternal: true))
        {
            if (child is CanvasItem { Visible: false } || child is CanvasLayer { Visible: false } || child is Window { Visible: false }) continue;
            yield return child;
            foreach (var n in Visible(child)) yield return n;
        }
    }

    IEnumerable<Node> FindNodes(string pattern) => Visible(GetTree().Root)
        .Where(n => pattern.StartsWith('#') ? n.GetClass() == pattern[1..] || n.GetType().Name == pattern[1..] : n.Name.ToString().Match(pattern));

    IEnumerable<Node> FindTexts(string text) => Visible(GetTree().Root).Where(n => n switch
    {
        Label l => l.Text.Contains(text, StringComparison.OrdinalIgnoreCase),
        RichTextLabel r => r.GetParsedText().Contains(text, StringComparison.OrdinalIgnoreCase),
        Button b => b.Text.Contains(text, StringComparison.OrdinalIgnoreCase),
        _ => false,
    });

    // The topmost match: highest canvas layer first, then the last in tree order (drawn on top).
    Vector2? FindButton(string text)
    {
        var hits = new List<(int Layer, Vector2 At)>();
        foreach (var n in Visible(GetTree().Root))
        {
            if (n is BaseButton { Disabled: false } b
                && ((b as Button)?.Text.Contains(text, StringComparison.OrdinalIgnoreCase) == true || (b.TooltipText.Length > 0 && b.TooltipText.Contains(text, StringComparison.OrdinalIgnoreCase))))
                hits.Add((Layer(b), b.GetGlobalTransformWithCanvas() * (b.Size / 2)));
            else if (n is TabBar bar)
                for (int i = 0; i < bar.TabCount; i++)
                    if (bar.GetTabTitle(i).Contains(text, StringComparison.OrdinalIgnoreCase))
                        hits.Add((Layer(bar), bar.GetGlobalTransformWithCanvas() * bar.GetTabRect(i).GetCenter()));
        }
        return hits.Count == 0 ? null : hits.Select((h, i) => (h, i)).OrderBy(x => x.h.Layer).ThenBy(x => x.i).Last().h.At;
    }

    static int Layer(Node n)
    {
        for (var p = n.GetParent(); p is not null; p = p.GetParent())
            if (p is CanvasLayer layer) return layer.Layer;
        return 0;
    }
}
