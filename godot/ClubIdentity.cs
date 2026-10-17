// The player's own club (v0): name, two kit colours and a crest, kept in instellingen.json ("club_name", "club_kit1", "club_kit2",
// "club_crest"; with ZEUS_NOSAVE in memory only). Display only: the competition keeps its internal club id (Competition.PlayerClubName).
using System;
using Godot;
using Zeus.Sim;

public static class ClubIdentity
{
    public static readonly Color[] Kits =
    {
        new("C8102E"), new("1F3A93"), new("6CACE4"), new("F5F5F0"), new("1A1A1A"),
        new("1E7B3A"), new("F2C230"), new("E8711C"), new("6A2C91"), new("7A1F2B"),
    };
    static readonly string[] KitKeys = { "club.colour.red", "club.colour.navy", "club.colour.sky", "club.colour.white", "club.colour.black",
        "club.colour.green", "club.colour.yellow", "club.colour.orange", "club.colour.purple", "club.colour.maroon" };
    static readonly string[] CrestKeys = { "club.crest.shield", "club.crest.circle", "club.crest.star", "club.crest.diamond",
        "club.crest.hexagon", "club.crest.pennant", "club.crest.square", "club.crest.octagon" };
    public static int CrestCount => CrestKeys.Length;

    public static string Name => Competition.CleanClubName(Loc.Setting("club_name", "")) ?? Competition.PlayerClubName;
    public static int Kit1 => Index("club_kit1", 0, Kits.Length);
    public static int Kit2 => Index("club_kit2", 3, Kits.Length);
    public static int Crest => Index("club_crest", 0, CrestKeys.Length);
    public static string KitName(int kit) => Loc.T(KitKeys[kit]);
    public static string CrestName(int crest) => Loc.T(CrestKeys[crest]);

    static int Index(string key, int fallback, int count) => int.TryParse(Loc.Setting(key, ""), out var v) && v >= 0 && v < count ? v : fallback;

    /// <summary>Stores the identity; the name must already be cleaned (see <see cref="Competition.CleanClubName"/>). False when the file could not be written.</summary>
    public static bool Save(string name, int kit1, int kit2, int crest)
    {
        Loc.SetSetting("club_name", name, save: false);
        Loc.SetSetting("club_kit1", kit1.ToString(), save: false);
        Loc.SetSetting("club_kit2", kit2.ToString(), save: false);
        return Loc.SetSetting("club_crest", crest.ToString());
    }

    /// <summary>Draws a crest: the outer shape in the second colour, the inner one in the first, with a thin ink edge so light kits stay visible.</summary>
    public static void Draw(CanvasItem canvas, Vector2 center, float radius, int crest, Color fill, Color trim)
    {
        var outer = Shape(crest, center, radius);
        canvas.DrawColoredPolygon(outer, trim);
        canvas.DrawColoredPolygon(Shape(crest, center, radius * .68f), fill);
        canvas.DrawPolyline([.. outer, outer[0]], UiTheme.Ink, Math.Max(1f, radius / 14), true);
    }

    public static void DrawOwn(CanvasItem canvas, Vector2 center, float radius) => Draw(canvas, center, radius, Crest, Kits[Kit1], Kits[Kit2]);

    static Vector2[] Shape(int crest, Vector2 c, float r)
    {
        Vector2[] Ring(int n, float start, Func<int, float>? radius = null)
        {
            var points = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = start + Mathf.Tau * i / n;
                points[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius?.Invoke(i) ?? r);
            }
            return points;
        }
        Vector2[] Points(params (float X, float Y)[] p) => Array.ConvertAll(p, q => c + new Vector2(q.X, q.Y) * r);
        return crest switch
        {
            1 => Ring(28, 0),
            2 => Ring(10, -Mathf.Pi / 2, i => i % 2 == 0 ? r : r * .45f),
            3 => Ring(4, -Mathf.Pi / 2),
            4 => Ring(6, 0),
            5 => Points((-.9f, -.8f), (.9f, -.8f), (0, 1)),
            6 => Points((-.8f, -.8f), (.8f, -.8f), (.8f, .8f), (-.8f, .8f)),
            7 => Ring(8, Mathf.Pi / 8),
            _ => Points((-.85f, -.9f), (.85f, -.9f), (.85f, .15f), (.7f, .55f), (.4f, .82f), (0, 1), (-.4f, .82f), (-.7f, .55f), (-.85f, .15f)),
        };
    }
}

/// <summary>A crest as a control; with Own = true it follows the saved club identity.</summary>
public partial class CrestView : Control
{
    public int Shape;
    public int Fill, Trim;
    public bool Own = true;

    public static CrestView For(float size) => new() { CustomMinimumSize = new Vector2(size, size), Size = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore };

    public override void _Draw()
    {
        float r = Mathf.Min(Size.X, Size.Y) / 2 - 1;
        if (Own) ClubIdentity.DrawOwn(this, Size / 2, r);
        else ClubIdentity.Draw(this, Size / 2, r, Shape, ClubIdentity.Kits[Fill], ClubIdentity.Kits[Trim]);
    }
}

/// <summary>Choice of club name, two kit colours and a crest; saves each valid change at once.</summary>
public partial class ClubPicker : VBoxContainer
{
    public event Action? Changed;
    LineEdit _name = null!;
    Label _status = null!;
    HBoxContainer _kit1 = null!, _kit2 = null!, _crests = null!;
    int _k1, _k2, _crest;
    string _saved = "";

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 8);
        (_k1, _k2, _crest, _saved) = (ClubIdentity.Kit1, ClubIdentity.Kit2, ClubIdentity.Crest, ClubIdentity.Name);
        var nameRow = Row("club.name");
        _name = new LineEdit { Text = _saved, MaxLength = Competition.MaxClubName, PlaceholderText = Loc.T("club.name_placeholder"),
            CustomMinimumSize = new Vector2(360, 36), Name = "club_name" };
        _name.TextChanged += _ => Store();
        nameRow.AddChild(_name);
        _status = UiTheme.Label("", UiTheme.Small, UiTheme.Muted); nameRow.AddChild(_status);
        _kit1 = Row("club.kit1"); _kit2 = Row("club.kit2"); _crests = Row("club.crest");
        for (int i = 0; i < ClubIdentity.Kits.Length; i++)
        {
            int kit = i;
            _kit1.AddChild(Swatch(kit, () => _k1 = kit));
            _kit2.AddChild(Swatch(kit, () => _k2 = kit));
        }
        for (int i = 0; i < ClubIdentity.CrestCount; i++)
        {
            int crest = i;
            var b = new Button { CustomMinimumSize = new Vector2(46, 46), TooltipText = ClubIdentity.CrestName(crest) };
            var view = CrestView.For(34); view.Own = false; view.Shape = crest; view.Position = new Vector2(6, 6); b.AddChild(view);
            b.Pressed += () => { _crest = crest; Store(); };
            _crests.AddChild(b);
        }
        Paint();
    }

    HBoxContainer Row(string title)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); AddChild(row);
        var label = UiTheme.Label(Loc.T(title), 18, UiTheme.Muted, display: true); label.CustomMinimumSize = new Vector2(150, 0); row.AddChild(label);
        return row;
    }

    Button Swatch(int kit, Action pick)
    {
        var b = new Button { CustomMinimumSize = new Vector2(34, 34), TooltipText = ClubIdentity.KitName(kit) };
        b.Pressed += () => { pick(); Store(); };
        return b;
    }

    void Store()
    {
        var name = Competition.CleanClubName(_name.Text);
        _status.Text = name is null ? Loc.T("club.name_empty") : "";
        _status.AddThemeColorOverride("font_color", UiTheme.PaperColor(UiTheme.Bad));
        if (name is not null) _saved = name;
        // An empty name keeps the last valid one; the colours and crest are still saved.
        if (!ClubIdentity.Save(_saved, _k1, _k2, _crest)) _status.Text = Loc.T("settings.save_failed");
        Paint();
        Changed?.Invoke();
    }

    void Paint()
    {
        void Mark(HBoxContainer row, int selected, bool swatch)
        {
            int i = 0;
            foreach (var child in row.GetChildren())
            {
                if (child is not Button b) continue;
                bool on = i == selected;
                var style = new StyleBoxFlat { BgColor = swatch ? ClubIdentity.Kits[i] : (on ? UiTheme.PaperShade : UiTheme.Paper), BorderColor = on ? UiTheme.Ink : UiTheme.Graphite };
                style.SetBorderWidthAll(on ? 4 : 1); style.SetCornerRadiusAll(4);
                foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) b.AddThemeStyleboxOverride(state, style);
                foreach (var view in b.GetChildren()) if (view is CrestView c) { c.Fill = _k1; c.Trim = _k2; c.QueueRedraw(); }
                i++;
            }
        }
        Mark(_kit1, _k1, true); Mark(_kit2, _k2, true); Mark(_crests, _crest, false);
    }
}
