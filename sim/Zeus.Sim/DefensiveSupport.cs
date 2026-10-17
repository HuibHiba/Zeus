namespace Zeus.Sim;

/// <summary>A second local defender screens behind the ball chaser without pulling the entire line out of shape.</summary>
internal static class DefensiveSupport
{
    internal static Vec2 Target(Perception view, Vec2 assigned)
    {
        if (view.Self.IsKeeper || view.HasBall || view.IsPresser
            || InstructionRules.OwnAttack(view) || view.Phase == GamePhase.Stilstaand
            || view.VisibleBall is not { } ball) return assigned;
        double direction = view.AttacksPositiveX ? 1 : -1;
        var mates = view.VisiblePlayers.Where(p => p.Team == view.Self.Team && !p.IsKeeper && !p.SentOff)
            .Append(view.Self).ToArray();
        var presser = mates.OrderBy(p => (p.Pos - ball.Pos.XY).Length).ThenBy(p => p.Id, StringComparer.Ordinal).First();
        if (presser.Id == view.Self.Id) return assigned;
        var point = new Vec2(ball.Pos.X - direction * 5, ball.Pos.Y * .85);
        var cover = mates.Where(p => p.Id != presser.Id).OrderBy(p => (p.Pos - point).Length)
            .ThenBy(p => p.Id, StringComparer.Ordinal).First();
        if (cover.Id != view.Self.Id || (point - assigned).Length > 20) return assigned;
        var offset = point - assigned;
        var chosen = offset.Length > 8 ? assigned + offset * (8 / offset.Length) : point;
        return new Vec2(Math.Clamp(chosen.X, -Pitch.HalfLength + 1, Pitch.HalfLength - 1),
            Math.Clamp(chosen.Y, -Pitch.HalfWidth + 1, Pitch.HalfWidth - 1));
    }
}
