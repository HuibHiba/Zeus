namespace Zeus.Sim;

/// <summary>Uncalibrated v0 tactical behaviours; distances are metres. See docs/instructies.md.</summary>
public sealed record InstructionConfig(
    bool EnableStayBack = true, bool EnableDrop = true, bool EnableWidth = true,
    bool EnableAdvance = true, bool EnableCutInside = true, bool EnableMark = true,
    bool EnableBoxRun = true, bool EnableCoverDepth = true, bool EnableDribbleInside = true,
    bool EnableFindSpace = false, bool EnablePressingForm = false, bool EnableTempo = false,
    bool EnableLineHeight = false, bool EnableCompactness = true,
    double MarkRadius = 12, double MarkGap = 8, double AdvanceMeters = 10,
    double DropMeters = 8, double CoverDepthMeters = 7, double WideMeters = 25,
    double DribbleMeters = 7, double GoodPassScore = 1.5, double MarkBallRadius = 15,
    bool EnableSupport = true, bool EnableDynamicShape = true)
{
    public static readonly InstructionConfig Disabled = new(false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, EnableSupport: false, EnableDynamicShape: false);

    public void Validate()
    {
        if (new[] { MarkRadius, MarkGap, AdvanceMeters, DropMeters, CoverDepthMeters, WideMeters,
            DribbleMeters, GoodPassScore, MarkBallRadius }.Any(x => !double.IsFinite(x) || x < 0)
            || MarkGap > MarkRadius || WideMeters > Pitch.HalfWidth)
            throw new ArgumentException("Invalid instruction settings.");
    }
}

public static class InstructionRules
{
    public static bool Has(Perception view, string instruction) => view.Instructions?.Contains(instruction) == true;
    public static bool OwnAttack(Perception view) => view.Phase is GamePhase.Opbouw or GamePhase.Aanval or GamePhase.Balwinst;

    // Explicit identity wins over the team form. Never substitute an unseen or missing explicit opponent.
    public static PlayerSnapshot? MarkedOpponent(Perception view, InstructionConfig config)
    {
        if (view.Self.IsKeeper || OwnAttack(view) || view.Phase == GamePhase.Stilstaand) return null;
        var opponents = view.VisiblePlayers.Where(p => p.Team != view.Self.Team && !p.SentOff && !p.IsKeeper);
        if (config.EnableMark && Has(view, "dek_man"))
        {
            // Ball-oriented marking (as in real man-marking): only an opponent near the ball is shadowed closely; far from the
            // ball the marker holds his formation position. Without this, two man-marking teams freeze each other completely.
            bool NearBall(PlayerSnapshot p) => view.KnownBall is not { } known || (p.Pos - known.Ball.Pos.XY).Length <= config.MarkBallRadius;
            return opponents.FirstOrDefault(p => p.Id == view.MarkId && (p.Pos - view.Self.Pos).Length <= config.MarkRadius && NearBall(p));
        }
        if (!config.EnablePressingForm || view.PressingForm == "zone") return null;
        double radius = view.PressingForm == "gemengd" ? config.MarkRadius * 0.5 : config.MarkRadius;
        return opponents.Where(p => (p.Pos - view.FormationTarget).Length <= radius)
            .OrderBy(p => (p.Pos - view.FormationTarget).Length).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    public static Vec2? MarkTarget(Perception view, InstructionConfig config)
    {
        if (MarkedOpponent(view, config) is not { } opponent) return null;
        double dir = view.AttacksPositiveX ? 1 : -1;
        var target = new Vec2(opponent.Pos.X - dir * (config.MarkGap + view.MarkGapExtra), opponent.Pos.Y);
        var offset = target - view.Self.Pos;
        return offset.Length > config.MarkRadius ? view.Self.Pos + offset * (config.MarkRadius / offset.Length) : target;
    }

    public static Vec2 Target(Perception view, PhasePlayer layout, PhasePlayer rest,
        TeamSettings team, double baseLine, Vec2 centre, InstructionConfig config)
    {
        if (view.Self.IsKeeper || view.Phase == GamePhase.Stilstaand) return view.FormationTarget;
        double dir = view.AttacksPositiveX ? 1 : -1;
        double x = view.FormationTarget.X * dir, y = view.FormationTarget.Y * dir;
        bool attack = OwnAttack(view);
        var ball = view.KnownBall?.Ball.Pos ?? view.BallBelief;
        if (!attack)
        {
            if (config.EnableLineHeight) x += (team.LineHeight - baseLine) * Pitch.Length;
            if (config.EnableCompactness && team.Compactness is { } compact)
            {
                double factor = 1 - 0.4 * compact;
                x = centre.X * dir + (x - centre.X * dir) * factor;
                y = centre.Y * dir + (y - centre.Y * dir) * factor;
            }
            if (config.EnableDynamicShape && ball is { } seenBall)
            {
                double line = (baseLine - .5) * Pitch.Length;
                if (config.EnableCompactness && team.Compactness is { } compactLine)
                    line = centre.X * dir + (line - centre.X * dir) * (1 - .4 * compactLine);
                x += Math.Min(0, seenBall.X * dir - 8 - line);
            }
            if (config.EnableCoverDepth && Has(view, "dek_ruimte_achter_lijn") && ball is { } known
                && (known.X * dir < 0 || view.VisiblePlayers.Any(p => p.Team != view.Self.Team
                    && !p.SentOff && p.Vel.X * dir < -0.3))) x -= config.CoverDepthMeters;
            if (config.EnableDynamicShape)
            {
                var cover = DefensiveSupport.Target(view, new Vec2(x * dir, y * dir));
                x = cover.X * dir; y = cover.Y * dir;
            }
        }
        else
        {
            if (config.EnableDrop && Has(view, "laat_zakken") && view.Phase == GamePhase.Opbouw && ball is { } known)
                x = Math.Min(x - config.DropMeters, known.X * dir + config.DropMeters);
            if (config.EnableAdvance && Has(view, "kom_op") && view.Phase == GamePhase.Aanval) x += config.AdvanceMeters;
            if (config.EnableCutInside && Has(view, "sluit_naar_binnen") && view.Phase == GamePhase.Aanval) y *= 0.45;
            if (config.EnableBoxRun && Has(view, "loop_zestien_in") && view.Phase == GamePhase.Aanval
                && ball is { } b && b.X * dir >= Pitch.Length / 6)
            { x = Math.Max(x, Pitch.HalfLength - 11); y = Math.Clamp(y, -12, 12); }
            if (config.EnableFindSpace && Has(view, "vrije_man_zoeken") && ball is { } observed)
            {
                // A small local grid ahead of the known ball: maximise observed opponent clearance,
                // retaining proximity to the assigned position and separation from visible teammates.
                var opponents = view.VisiblePlayers.Where(p => p.Team != view.Self.Team && !p.SentOff && !p.IsKeeper).ToArray();
                var lines = opponents.Select(p => p.Pos.X * dir).Distinct().Order().ToArray();
                var gap = lines.Zip(lines.Skip(1), (lower, upper) => (Lower: lower + 2, Upper: upper - 2))
                    .Where(g => g.Upper - g.Lower >= 2)
                    .OrderBy(g => Math.Abs((g.Lower + g.Upper) / 2 - view.FormationTarget.X * dir)).FirstOrDefault();
                if (gap.Upper > gap.Lower)
                {
                    double best = double.NegativeInfinity;
                    Vec2 chosen = new(x, y);
                    foreach (double dx in new[] { 0.0, -6, 6 })
                    foreach (double dy in new[] { 0.0, -8, 8 })
                    {
                        var candidate = new Vec2(Math.Clamp(Math.Max(x + dx, observed.X * dir + 5), gap.Lower, gap.Upper), Math.Clamp(y + dy, -28, 28));
                        var world = candidate * dir;
                        double score = Math.Min(15, opponents.Min(p => (p.Pos - world).Length))
                            - 0.12 * (candidate - new Vec2(x, y)).Length
                            - view.VisiblePlayers.Where(p => p.Team == view.Self.Team).Sum(p => Math.Max(0, 5 - (p.Pos - world).Length));
                        if (score > best) { best = score; chosen = candidate; }
                    }
                    x = chosen.X; y = chosen.Y;
                }
            }
            if (config.EnableSupport)
            {
                var support = SupportPlay.Target(view, new Vec2(x * dir, y * dir));
                x = support.X * dir; y = support.Y * dir;
            }
            // Hard positional constraints win conflicts with other attacking instructions.
            if (config.EnableWidth && Has(view, "blijf_breed") && layout.Y != 0.5)
                y = (layout.Y < 0.5 ? -1 : 1) * Math.Max(config.WideMeters, Math.Abs((layout.Y - 0.5) * Pitch.Width));
            if (config.EnableStayBack && Has(view, "blijf_achter")) x = Math.Min(x, (rest.X - 0.5) * Pitch.Length);
        }
        return new Vec2(Math.Clamp(x * dir, -Pitch.HalfLength, Pitch.HalfLength), Math.Clamp(y * dir, -Pitch.HalfWidth, Pitch.HalfWidth));
    }
}
