// Display only: click a player in the live match for a small card (state, stats, instructions) plus his walking target on the pitch.
using System;
using System.Linq;
using Godot;
using Zeus.Sim;
using Side = Zeus.Sim.Side;

public sealed class PlayerCard
{
    readonly Side _side;
    readonly string _slot;
    PlayerCard(Side side, string slot) { _side = side; _slot = slot; }

    /// <summary>The nearest visible player within reach of the click (screen pixels), or null.</summary>
    public static PlayerCard? At(Snapshot snapshot, Func<Vec2, Vector2> toScreen, Vector2 click)
    {
        PlayerSnapshot? best = null;
        float bestDistance = 18;
        foreach (var p in snapshot.Players)
        {
            float d = p.SentOff ? float.MaxValue : toScreen(p.Pos).DistanceTo(click);
            if (d < bestDistance) { best = p; bestDistance = d; }
        }
        return best is null ? null : new PlayerCard(best.Team, best.Id);
    }

    PlayerSnapshot? Find(Snapshot s) => s.Players.FirstOrDefault(p => p.Team == _side && p.Id == _slot && !p.SentOff);

    /// <summary>Ring around the player and, for your own team, a thin line to where the tactic sends him in the current phase.</summary>
    public void DrawOnField(CanvasItem canvas, Match match, Snapshot snapshot, Side mine, Func<Vec2, Vector2> toScreen)
    {
        if (Find(snapshot) is not { } p) return;
        var at = toScreen(p.Pos);
        canvas.DrawArc(at, 16, 0, Mathf.Tau, 28, UiTheme.Accent, 3);
        if (_side != mine || match.TargetOf(_side, _slot) is not { } target) return;
        var to = toScreen(target);
        canvas.DrawLine(at, to, new Color(UiTheme.Accent, 0.8f), 2);
        canvas.DrawCircle(to, 5, UiTheme.Accent);
    }

    /// <summary>Card in the lower part of the right-hand panel (x 1148..1508); the tactic is only shown for your own team.</summary>
    public void DrawCard(CanvasItem canvas, Match match, Snapshot snapshot, Side mine, PopupNames names)
    {
        if (Find(snapshot) is not { } p) return;
        var box = new Rect2(1156, 468, 344, 258);
        canvas.DrawStyleBox(new StyleBoxFlat { BgColor = UiTheme.PaperShade, BorderColor = _side == Side.Home ? UiTheme.Home : UiTheme.Away, BorderWidthLeft = 4 }, box);
        float x = box.Position.X + 14, y = box.Position.Y;
        var ink = _side == Side.Home ? UiTheme.HomeInk : UiTheme.AwayInk;
        string name = names.Slot(_side, _slot);
        var squadPlayer = match.SquadPlayerOf(_side, _slot);
        canvas.DrawString(GameFonts.Display, new Vector2(x, y + 24), Short(name, 22), HorizontalAlignment.Left, 250, 24, ink);
        canvas.DrawString(GameFonts.Display, new Vector2(box.End.X - 60, y + 24), _slot, HorizontalAlignment.Right, 50, 24, UiTheme.Graphite);
        if (squadPlayer is not null)
            canvas.DrawString(GameFonts.Semi, new Vector2(x, y + 44), Loc.Name(squadPlayer.Role), HorizontalAlignment.Left, 316, 15, UiTheme.Graphite);

        Bar(canvas, new Vector2(x, y + 54), Loc.T("card.condition"), p.LongEnergy);
        Bar(canvas, new Vector2(x + 166, y + 54), Loc.T("card.burst"), p.ShortEnergy);

        var events = match.Events.Events.Where(e => e.Team == _side && e.PlayerId == _slot).ToArray();
        int Count(EventType t) => events.Count(e => e.Type == t);
        canvas.DrawString(GameFonts.Semi, new Vector2(x, y + 94), Loc.T("card.stats", Count(EventType.Goal), Count(EventType.Shot), Count(EventType.Pass)),
            HorizontalAlignment.Left, 316, 15, UiTheme.Ink);
        canvas.DrawString(GameFonts.Semi, new Vector2(x, y + 114), Loc.T("match.card_counts", p.YellowCards, p.SentOff ? 1 : 0), HorizontalAlignment.Left, 316, 15, UiTheme.Ink);

        if (squadPlayer is { } sp)
        {
            var a = sp.Attributes;
            double[] v = [a.Passing, a.FirstTouch, a.Finishing, a.Dribbling, a.Defending, a.Heading, a.Speed, a.Acceleration, a.Strength, a.Endurance,
                a.Vision, a.Anticipation, a.Positioning, a.Decisions, a.Discipline];
            for (int i = 0; i < v.Length; i++)
                canvas.DrawString(GameFonts.Body, new Vector2(x + i % 3 * 108, y + 136 + i / 3 * 16), $"{Loc.T("squad.attribute." + i)} {(int)Math.Round(v[i] * 19 + 1)}",
                    HorizontalAlignment.Left, 104, 14, UiTheme.Ink);
        }

        string text = _side != mine ? Loc.T("card.hidden")
            : InstructionsNow(match, snapshot) is { Length: > 0 } list ? string.Join(", ", list.Select(Loc.Name)) : Loc.T("card.no_instructions");
        canvas.DrawString(GameFonts.Semi, new Vector2(x, y + 232), Loc.T("card.instructions", Short(text, 38)), HorizontalAlignment.Left, 320, 14, UiTheme.Graphite);
    }

    string[] InstructionsNow(Match match, Snapshot snapshot)
    {
        var phase = match.PhaseOf(_side);
        return match.TacticOf(_side).Phases.TryGetValue(phase, out var layout)
            ? layout.Players.FirstOrDefault(q => q.Id == _slot)?.Instructions.ToArray() ?? []
            : [];
    }

    static void Bar(CanvasItem canvas, Vector2 at, string label, double value)
    {
        canvas.DrawString(GameFonts.Semi, at + new Vector2(0, 14), $"{label} {value * 100:0}%", HorizontalAlignment.Left, 150, 14, UiTheme.Graphite);
        canvas.DrawRect(new Rect2(at + new Vector2(0, 20), new Vector2(150, 7)), new Color(0, 0, 0, 0.12f), true);
        canvas.DrawRect(new Rect2(at + new Vector2(0, 20), new Vector2((float)(150 * Math.Clamp(value, 0, 1)), 7)), value < 0.35 ? new Color("8C4A4A") : new Color("3A8F68"), true);
    }

    static string Short(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
