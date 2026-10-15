// Shared look for every screen: one palette, one set of button/panel styles, applied via Apply().
using Godot;

public static class UiTheme
{
    public static readonly Color Bg = new("0E1621");
    public static readonly Color Panel = new("16212F");
    public static readonly Color PanelLight = new("1F2D40");
    public static readonly Color Accent = new("F2B705");       // gold, "Zeus"
    public static readonly Color Text = new("E8EEF5");
    public static readonly Color Muted = new("8FA1B5");
    public static readonly Color Home = new("E5484D");
    public static readonly Color Away = new("3E8BFF");
    public static readonly Color Good = new("46C281");
    public static readonly Color Bad = new("FF6B6B");

    public const int Title = 64, Heading = 28, Body = 18, Small = 15;

    static StyleBoxFlat Box(Color fill, Color? border = null, int radius = 10, int pad = 14)
    {
        var s = new StyleBoxFlat { BgColor = fill, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = pad, ContentMarginRight = pad, ContentMarginTop = pad * 0.6f, ContentMarginBottom = pad * 0.6f };
        if (border is { } b) { s.BorderColor = b; s.SetBorderWidthAll(2); }
        return s;
    }

    public static StyleBoxFlat PanelBox() => Box(Panel, PanelLight, 14, 20);

    public static Theme Create()
    {
        var t = new Theme { DefaultFontSize = Body, DefaultFont = GameFonts.Body };
        t.SetColor("font_color", "Label", Text);

        foreach (var type in new[] { "Button", "OptionButton", "MenuButton" })
        {
            t.SetStylebox("normal", type, Box(PanelLight, null, 10, 18));
            t.SetStylebox("hover", type, Box(new Color("2A3D57"), Accent, 10, 18));
            t.SetStylebox("pressed", type, Box(new Color("34496A"), Accent, 10, 18));
            t.SetStylebox("focus", type, Box(new Color(0, 0, 0, 0), Accent, 10, 18));
            t.SetStylebox("disabled", type, Box(Panel, null, 10, 18));
            t.SetColor("font_color", type, Text);
            t.SetColor("font_hover_color", type, Colors.White);
            t.SetFontSize("font_size", type, 22);
            t.SetFont("font", type, GameFonts.Semi);
        }
        // Primary button variation: gold with dark text.
        t.SetTypeVariation("PrimaryButton", "Button");
        t.SetStylebox("normal", "PrimaryButton", Box(Accent, null, 10, 18));
        t.SetStylebox("hover", "PrimaryButton", Box(new Color("FFD13D"), Colors.White, 10, 18));
        t.SetStylebox("pressed", "PrimaryButton", Box(new Color("D9A400"), null, 10, 18));
        t.SetStylebox("focus", "PrimaryButton", Box(new Color(0, 0, 0, 0), Colors.White, 10, 18));
        t.SetColor("font_color", "PrimaryButton", new Color("0E1621"));
        t.SetColor("font_hover_color", "PrimaryButton", new Color("0E1621"));
        t.SetColor("font_focus_color", "PrimaryButton", new Color("0E1621"));
        t.SetColor("font_pressed_color", "PrimaryButton", new Color("0E1621"));
        t.SetFontSize("font_size", "PrimaryButton", 30);
        t.SetFont("font", "PrimaryButton", GameFonts.Display);

        // Menu item: no box, big condensed caps, gold bar on the left when hovered/focused.
        t.SetTypeVariation("MenuItem", "Button");
        StyleBoxFlat Item(Color fill, float barAlpha)
        {
            var s = new StyleBoxFlat { BgColor = fill, ContentMarginLeft = 22, ContentMarginRight = 12, ContentMarginTop = 6, ContentMarginBottom = 6 };
            s.BorderWidthLeft = 6; s.BorderColor = new Color(Accent, barAlpha);
            return s;
        }
        t.SetStylebox("normal", "MenuItem", Item(new Color(0, 0, 0, 0), 0));
        t.SetStylebox("hover", "MenuItem", Item(new Color(Accent, 0.14f), 1));
        t.SetStylebox("pressed", "MenuItem", Item(new Color(Accent, 0.26f), 1));
        t.SetStylebox("focus", "MenuItem", Item(new Color(Accent, 0.14f), 1));
        t.SetColor("font_color", "MenuItem", new Color(Text, 0.85f));
        t.SetColor("font_hover_color", "MenuItem", Accent);
        t.SetColor("font_pressed_color", "MenuItem", Accent);
        t.SetColor("font_focus_color", "MenuItem", Accent);
        t.SetFontSize("font_size", "MenuItem", 38);
        t.SetFont("font", "MenuItem", GameFonts.Display);
        t.SetConstant("h_separation", "MenuItem", 0);

        t.SetStylebox("panel", "PanelContainer", PanelBox());
        t.SetStylebox("normal", "LineEdit", Box(Bg, PanelLight, 8, 12));
        t.SetStylebox("focus", "LineEdit", Box(Bg, Accent, 8, 12));
        t.SetColor("font_color", "LineEdit", Text);
        t.SetStylebox("slider", "HSlider", Box(PanelLight, null, 4, 2));
        t.SetStylebox("grabber_area", "HSlider", Box(Accent, null, 4, 2));
        t.SetStylebox("grabber_area_highlight", "HSlider", Box(new Color("FFD13D"), null, 4, 2));
        t.SetConstant("separation", "VBoxContainer", 12);
        t.SetConstant("separation", "HBoxContainer", 14);
        return t;
    }

    static Theme? _cached;
    public static void Apply(Control c) => c.Theme = _cached ??= Create();

    public static Button MenuItem(string text) =>
        new() { Text = text.ToUpperInvariant(), ThemeTypeVariation = "MenuItem", Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 62) };

    public static Label Label(string text, int size = Body, Color? color = null, bool display = false)
    {
        var l = new Label { Text = text };
        if (display) l.AddThemeFontOverride("font", GameFonts.Display);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color ?? Text);
        return l;
    }

    public static Button Primary(string text) => new() { Text = text, ThemeTypeVariation = "PrimaryButton", CustomMinimumSize = new Vector2(0, 58) };
    public static Button Secondary(string text) => new() { Text = text, CustomMinimumSize = new Vector2(0, 50) };
}
