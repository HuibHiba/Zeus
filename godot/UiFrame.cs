// Screen frame for the expand-aspect viewport: the viewport is at least 1600x900 and grows on the longer axis (21:9 -> ~2133x900,
// 4:3 -> 1600x1200). Screens with an absolute 1600x900 layout (Menu, Main, Editor, cover) sit centred in it; page screens grow up to MaxWidth.
// The space around them is the plain bench (clear colour UiTheme.Bg), drawn by the screens' own backgrounds where they fill the viewport.
using System;
using Godot;

public static class UiFrame
{
    public static readonly Vector2 Design = new(1600, 900);
    public const float MaxWidth = 2160;        // page screens: 3440x1440 still fills, 32:9 gets a centred page on the bench

    /// <summary>Top-left of the centred 1600x900 design frame in viewport coordinates (zero on 16:9).</summary>
    public static Vector2 Offset(Viewport viewport) => ((viewport.GetVisibleRect().Size - Design) / 2).Max(Vector2.Zero).Floor();

    /// <summary>Runs <paramref name="apply"/> now and on every viewport resize while <paramref name="node"/> is in the tree.</summary>
    public static void Follow(Node node, Action apply)
    {
        var viewport = node.GetViewport();
        void Changed() => apply();
        viewport.SizeChanged += Changed;
        node.TreeExiting += () => viewport.SizeChanged -= Changed;
        apply();
    }

    /// <summary>An absolute-layout screen root: exactly the 1600x900 design frame, kept centred by its anchors.</summary>
    public static void Center(Control c)
    {
        c.SetAnchorsPreset(Control.LayoutPreset.Center);
        c.OffsetLeft = -Design.X / 2; c.OffsetRight = Design.X / 2; c.OffsetTop = -Design.Y / 2; c.OffsetBottom = Design.Y / 2;
    }

    /// <summary>A page screen root: full height, full width up to MaxWidth, centred.</summary>
    public static void Page(Control c)
    {
        c.AnchorLeft = c.AnchorRight = 0.5f; c.AnchorTop = 0; c.AnchorBottom = 1; c.OffsetTop = c.OffsetBottom = 0;
        Follow(c, () => { float half = Math.Min(c.GetViewport().GetVisibleRect().Size.X, MaxWidth) / 2; c.OffsetLeft = -half; c.OffsetRight = half; });
    }
}
