// Shared look for every screen: one palette, one set of button/panel styles, applied via Apply().
using Godot;

public static class UiTheme
{
    public static readonly Color Bg = new("1A1511");
    public static readonly Color Panel = new("2A221B");
    public static readonly Color PanelLight = new("352C23");
    public static readonly Color Accent = new("F2C94C");       // gold, "Zeus"
    public static readonly Color Text = new("EFE9DA");
    public static readonly Color Muted = new("A39A88");
    public static readonly Color Home = new("F07A5A");
    public static readonly Color Away = new("7FB2E0");
    public static readonly Color Good = new("8CC48A");
    public static readonly Color Bad = new("F28B7A");

    public static readonly Color Bench = Bg, BenchLight = Panel;
    public static readonly Color Board = new("1F2A25"), BoardStripe = new("26332D"), Cover = new("2F4A3C");
    public static readonly Color Paper = new("EADFC6"), PaperShade = new("D8CBAA"), Rule = new("9DB4B0");
    public static readonly Color Ink = new("241E1A"), Graphite = new("5B554C");
    public static readonly Color HomeInk = new("B3341F"), AwayInk = new("2C5A8C");
    public const int Title = 64, Heading = 28, Body = 18, Small = 17;

    static StyleBoxFlat Box(Color fill, Color? border = null, int radius = 3, int pad = 14)
    {
        var s = new StyleBoxFlat { BgColor = fill, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = pad, ContentMarginRight = pad, ContentMarginTop = pad * 0.6f, ContentMarginBottom = pad * 0.6f };
        if (border is { } b) { s.BorderColor = b; s.SetBorderWidthAll(2); }
        return s;
    }

    public static StyleBoxFlat PanelBox() => PaperBox();

    public static Theme Create()
    {
        var t = new Theme { DefaultFontSize = Body, DefaultFont = GameFonts.Body };
        t.SetColor("font_color", "Label", Ink);
        t.SetColor("font_color", "CheckBox", Ink);

        foreach (var type in new[] { "Button", "OptionButton", "MenuButton" })
        {
            t.SetStylebox("normal", type, Box(PanelLight, null, 3, 18));
            t.SetStylebox("hover", type, Box(new Color("40362B"), Accent, 3, 18));
            t.SetStylebox("pressed", type, Box(new Color("4A3D30"), Accent, 3, 18));
            t.SetStylebox("focus", type, Box(new Color(0, 0, 0, 0), Accent, 3, 18));
            t.SetStylebox("disabled", type, Box(Panel, null, 3, 18));
            t.SetColor("font_color", type, Text);
            t.SetColor("font_hover_color", type, Colors.White);
            t.SetFontSize("font_size", type, 20);
            t.SetFont("font", type, GameFonts.Semi);
        }
        // Primary button variation: gold with dark text.
        t.SetTypeVariation("PrimaryButton", "Button");
        t.SetStylebox("normal", "PrimaryButton", Box(Accent, null, 3, 18));
        t.SetStylebox("hover", "PrimaryButton", Box(new Color("F7D66E"), Colors.White, 3, 18));
        t.SetStylebox("pressed", "PrimaryButton", Box(new Color("B8932A"), null, 3, 18));
        t.SetStylebox("focus", "PrimaryButton", Box(new Color(0, 0, 0, 0), Colors.White, 3, 18));
        t.SetColor("font_color", "PrimaryButton", new Color("1A1511"));
        t.SetColor("font_hover_color", "PrimaryButton", new Color("1A1511"));
        t.SetColor("font_focus_color", "PrimaryButton", new Color("1A1511"));
        t.SetColor("font_pressed_color", "PrimaryButton", new Color("1A1511"));
        t.SetColor("font_hover_pressed_color", "PrimaryButton", new Color("1A1511"));
        t.SetFontSize("font_size", "PrimaryButton", 30);
        t.SetFont("font", "PrimaryButton", GameFonts.Display);

        // Compact button: for dense lists and tables where the normal button padding would make rows too tall.
        t.SetTypeVariation("CompactButton", "Button");
        StyleBoxFlat Compact(Color fill, Color? border = null) { var s = Box(fill, border, 3, 12); s.ContentMarginTop = s.ContentMarginBottom = 3; return s; }
        t.SetStylebox("normal", "CompactButton", Compact(PanelLight));
        t.SetStylebox("hover", "CompactButton", Compact(new Color("40362B"), Accent));
        t.SetStylebox("pressed", "CompactButton", Compact(new Color("4A3D30"), Accent));
        t.SetStylebox("focus", "CompactButton", Compact(new Color(0, 0, 0, 0), Accent));
        t.SetStylebox("disabled", "CompactButton", Compact(Panel));
        t.SetFontSize("font_size", "CompactButton", 16);

        // Menu item: no box, big condensed caps, gold bar on the left when hovered/focused.
        t.SetTypeVariation("MenuItem", "Button");
        StyleBoxFlat Item(Color fill, float barAlpha)
        {
            var s = new StyleBoxFlat { BgColor = fill, ContentMarginLeft = 22, ContentMarginRight = 12, ContentMarginTop = 6, ContentMarginBottom = 6 };
            s.BorderWidthBottom = 3; s.BorderColor = new Color(Accent, barAlpha);
            return s;
        }
        t.SetStylebox("normal", "MenuItem", Item(new Color(0, 0, 0, 0), 0));
        t.SetStylebox("hover", "MenuItem", Item(Accent, 1));
        t.SetStylebox("pressed", "MenuItem", Item(new Color("B8932A"), 1));
        t.SetStylebox("focus", "MenuItem", Item(Accent, 1));
        t.SetColor("font_color", "MenuItem", new Color(Text, 0.85f));
        t.SetColor("font_hover_color", "MenuItem", Ink);
        t.SetColor("font_pressed_color", "MenuItem", Ink);
        t.SetColor("font_focus_color", "MenuItem", Ink);
        t.SetFontSize("font_size", "MenuItem", 38);
        t.SetFont("font", "MenuItem", GameFonts.Display);
        t.SetConstant("h_separation", "MenuItem", 0);

        // Tabs: condensed caps, gold underline on the selected tab, no box.
        StyleBoxFlat Tab(float underline, Color border)
        {
            var s = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8 };
            s.BorderWidthBottom = (int)underline; s.BorderColor = border;
            return s;
        }
        t.SetStylebox("tab_selected", "TabContainer", Tab(3, Accent));
        t.SetStylebox("tab_hovered", "TabContainer", Tab(3, new Color(Accent, 0.4f)));
        t.SetStylebox("tab_unselected", "TabContainer", Tab(3, new Color(1, 1, 1, 0.08f)));
        t.SetStylebox("panel", "TabContainer", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) });
        t.SetColor("font_selected_color", "TabContainer", Accent);
        t.SetColor("font_hovered_color", "TabContainer", Text);
        t.SetColor("font_unselected_color", "TabContainer", Muted);
        t.SetFont("font", "TabContainer", GameFonts.Display);
        t.SetFontSize("font_size", "TabContainer", 22);

        // Chip: small toggle button; pressed = gold.
        t.SetTypeVariation("Chip", "Button");
        t.SetStylebox("normal", "Chip", Box(PanelLight, null, 14, 12));
        t.SetStylebox("hover", "Chip", Box(new Color("40362B"), Accent, 14, 12));
        t.SetStylebox("pressed", "Chip", Box(Accent, null, 14, 12));
        t.SetStylebox("hover_pressed", "Chip", Box(new Color("F7D66E"), null, 14, 12));
        t.SetStylebox("focus", "Chip", Box(new Color(0, 0, 0, 0), Colors.White, 14, 12));
        t.SetColor("font_pressed_color", "Chip", new Color("1A1511"));
        t.SetColor("font_hover_pressed_color", "Chip", new Color("1A1511"));
        t.SetFontSize("font_size", "Chip", 17);
        t.SetFont("font", "Chip", GameFonts.Semi);

        t.SetStylebox("panel", "PanelContainer", PanelBox());
        t.SetStylebox("panel", "TooltipPanel", PaperBox());
        t.SetColor("font_color", "TooltipLabel", Ink);
        t.SetFontSize("font_size", "TooltipLabel", Small);
        t.SetStylebox("normal", "LineEdit", Box(Bg, PanelLight, 8, 12));
        t.SetStylebox("focus", "LineEdit", Box(Bg, Accent, 8, 12));
        t.SetColor("font_color", "LineEdit", Text);
        t.SetStylebox("slider", "HSlider", Box(PanelLight, null, 4, 2));
        t.SetStylebox("grabber_area", "HSlider", Box(Accent, null, 4, 2));
        t.SetStylebox("grabber_area_highlight", "HSlider", Box(new Color("F7D66E"), null, 4, 2));
        t.SetConstant("separation", "VBoxContainer", 12);
        t.SetConstant("separation", "HBoxContainer", 14);
        return t;
    }


    public static Color PaperColor(Color color) => color == Text ? Ink : color == Muted ? Graphite
        : color == Home ? HomeInk : color == Away ? AwayInk : color == Accent ? Ink
        : color == Good ? new Color("3E6B3A") : color == Bad ? new Color("8E1F14") : color;

    public static StyleBoxFlat PaperBox()
    {
        var box = Box(Paper, PaperShade, 0, 20);
        box.ShadowColor = new Color(0, 0, 0, 0.35f);
        box.ShadowSize = 0;
        box.ShadowOffset = new Vector2(3, 3);
        return box;
    }

    static Theme? _paper;
    public static Theme PaperTheme()
    {
        if (_paper is null)
        {
            _paper = Create();
            _paper.SetStylebox("panel", "PanelContainer", PaperBox());
            foreach (var type in new[] { "Label", "Button", "OptionButton", "MenuButton", "CheckBox", "LineEdit", "SpinBox", "PopupMenu", "CompactButton", "Chip" })
            {
                _paper.SetColor("font_color", type, Ink);
                _paper.SetColor("font_disabled_color", type, Graphite);
                _paper.SetColor("font_hover_color", type, Ink);
                _paper.SetColor("font_focus_color", type, Ink);
                _paper.SetColor("font_pressed_color", type, Ink);
                _paper.SetColor("font_hover_pressed_color", type, Ink);
            }
            foreach (var type in new[] { "Button", "OptionButton", "MenuButton", "CompactButton", "Chip" })
            {
                int pad = type == "CompactButton" ? 8 : 14;
                var normal = Box(PaperShade, null, 3, pad);
                if (type is "CompactButton" or "Chip") normal.ContentMarginTop = normal.ContentMarginBottom = 3;
                _paper.SetStylebox("normal", type, normal);
                _paper.SetStylebox("hover", type, Box(PaperShade, Ink, 3, pad));
                _paper.SetStylebox("pressed", type, Box(Accent, Ink, 3, pad));
                _paper.SetStylebox("focus", type, Box(new Color(0, 0, 0, 0), Ink, 3, pad));
                _paper.SetStylebox("disabled", type, Box(Paper, null, 3, pad));
            }
            _paper.SetStylebox("panel", "PopupMenu", PaperBox());
            _paper.SetStylebox("hover", "PopupMenu", Box(Accent, null, 0, 4));
            _paper.SetStylebox("normal", "LineEdit", Box(Paper, Graphite, 0, 8));
            _paper.SetStylebox("focus", "LineEdit", Box(Paper, Ink, 0, 8));
            _paper.SetColor("caret_color", "LineEdit", Ink);
            _paper.SetColor("font_placeholder_color", "LineEdit", Graphite);
            _paper.SetStylebox("tab_selected", "TabContainer", Box(Paper, Graphite, 0, 8));
            _paper.SetStylebox("tab_unselected", "TabContainer", Box(PaperShade, null, 0, 8));
            _paper.SetStylebox("tab_hovered", "TabContainer", Box(PaperShade, Ink, 0, 8));
            _paper.SetColor("font_selected_color", "TabContainer", Ink);
            _paper.SetColor("font_unselected_color", "TabContainer", Graphite);
            _paper.SetColor("font_hovered_color", "TabContainer", Ink);
            _paper.SetColor("font_selected_color", "LineEdit", Ink);
            _paper.SetColor("font_color", "ProgressBar", Ink);
            _paper.SetStylebox("background", "ProgressBar", Box(PaperShade, null, 0, 0));
            _paper.SetStylebox("fill", "ProgressBar", Box(Accent, null, 0, 0));
            _paper.SetColor("selection_color", "LineEdit", Accent);
            _paper.SetStylebox("normal", "TextEdit", Box(Paper, Graphite, 0, 8));
            _paper.SetStylebox("focus", "TextEdit", Box(Paper, Ink, 0, 8));
            _paper.SetColor("font_color", "TextEdit", Ink);
            _paper.SetColor("font_selected_color", "TextEdit", Ink);
            _paper.SetColor("caret_color", "TextEdit", Ink);
            _paper.SetColor("font_placeholder_color", "TextEdit", Graphite);
            _paper.SetColor("selection_color", "TextEdit", Accent);
        }
        return _paper;
    }

    public static void ApplyPaper(Control root) => root.Theme = PaperTheme();

    static Theme? _cached;
    public static void Apply(Control c) => c.Theme = _cached ??= Create();

    public static Button MenuItem(string text) =>
        new() { Text = text.ToUpperInvariant(), ThemeTypeVariation = "MenuItem", Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 62) };

    public static Label Label(string text, int size = Body, Color? color = null, bool display = false)
    {
        var l = new Label { Text = text };
        if (display) l.AddThemeFontOverride("font", GameFonts.Display);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", PaperColor(color ?? Text));
        return l;
    }

    public static Button Primary(string text) => new() { Text = text, ThemeTypeVariation = "PrimaryButton", CustomMinimumSize = new Vector2(0, 58) };
    public static Button Secondary(string text) => new() { Text = text, CustomMinimumSize = new Vector2(0, 50) };
}
