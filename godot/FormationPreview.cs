// The four team settings of a tactic as labelled bars, for the menu team columns (the formation itself lives in MenuBackdrop).
using System;
using Godot;
using Zeus.Sim;

public partial class FormationPreview : Control
{
    TacticEditorModel? _model;
    Color _color = UiTheme.Home;
    string _error = "";

    public FormationPreview() => CustomMinimumSize = new Vector2(270, 262);

    public void Show(string path, Color color)
    {
        _color = color;
        try { _model = TacticEditorModel.LoadFile(path); _error = ""; }
        catch (Exception e) { _model = null; _error = Loc.T("menu.preview.read_error", Loc.Error(e)); }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_model is null)
        {
            DrawString(GameFonts.Body, new Vector2(0, 22), _error, HorizontalAlignment.Left, 270, UiTheme.Small, UiTheme.Bad);
            return;
        }
        (string label, string key)[] bars =
        {
            (Loc.T("menu.preview.tempo"), "tempo"), (Loc.T("menu.preview.pressing"), "pressing_intensiteit"), (Loc.T("menu.preview.line"), "lijnhoogte"), (Loc.T("menu.preview.compactness"), "compactheid"),
        };
        for (int i = 0; i < bars.Length; i++)
        {
            float v = (float)(_model.GetTeamSetting(bars[i].key) ?? 0.5);
            float y = 22 + i * 64;
            DrawString(GameFonts.Semi, new Vector2(0, y), bars[i].label, HorizontalAlignment.Left, -1, 18, UiTheme.Graphite);
            DrawString(GameFonts.Display, new Vector2(270 - 44, y), $"{(int)Math.Round(v * 100)}", HorizontalAlignment.Right, 44, 22, UiTheme.Ink);
            // segmented bar: 20 ticks
            for (int s = 0; s < 10; s++)
                DrawLine(new Vector2(s * 27 + 4, y + 8), new Vector2(s * 27 + 2, y + 19), s < Math.Round(v * 10) ? _color : UiTheme.PaperShade, 3, true);
        }
    }
}
