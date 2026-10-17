namespace Zeus.Sim;

/// <summary>Local passing support using only visible players, within the assigned tactical area.</summary>
public static class SupportPlay
{
    public static Vec2 Target(Perception view, Vec2 assigned)
    {
        if (view.HasBall || view.Self.IsKeeper || !InstructionRules.OwnAttack(view)
            || view.BallIsLoose || view.TeamPassInFlight || view.ReceiveTarget is not null
            || view.TeamBallCarrierId is null) return assigned;
        var carrier = view.VisiblePlayers.FirstOrDefault(p => p.Team == view.Self.Team
            && p.Id == view.TeamBallCarrierId && !p.SentOff);
        if (carrier is null || (assigned - carrier.Pos).Length is < 6 or > 32) return assigned;
        var opponents = view.VisiblePlayers.Where(p => p.Team != view.Self.Team && !p.SentOff).ToArray();
        if (opponents.Length == 0) return assigned;
        var mates = view.VisiblePlayers.Where(p => p.Team == view.Self.Team && !p.SentOff).ToArray();
        double Score(Vec2 candidate)
        {
            double gap = 6, lane = 4;
            foreach (var opponent in opponents)
            {
                gap = Math.Min(gap, (opponent.Pos - candidate).Length);
                if ((opponent.Pos - carrier.Pos).Length > 2)
                    lane = Math.Min(lane, PassChoice.DistanceToSegment(opponent.Pos, carrier.Pos, candidate));
            }
            double crowded = 0;
            foreach (var mate in mates) crowded += Math.Max(0, 5 - (mate.Pos - candidate).Length);
            return gap + 1.5 * lane - .4 * (candidate - assigned).Length - 1.5 * crowded;
        }
        var chosen = assigned;
        double best = Score(assigned) + .75; // Do not wander when the assigned passing option is already open.
        double direction = view.AttacksPositiveX ? 1 : -1;
        var defenders = opponents.Select(p => p.Pos.X * direction).OrderDescending().ToArray();
        double offsideLine = defenders.Length >= 2 ? Math.Max(carrier.Pos.X * direction, defenders[1]) : double.PositiveInfinity;
        foreach (double dx in new[] { 0.0, -4, 4 })
        foreach (double dy in new[] { -5.0, 0, 5 })
        {
            var candidate = new Vec2(Math.Clamp(assigned.X + dx * direction, -Pitch.HalfLength + 1, Pitch.HalfLength - 1),
                Math.Clamp(assigned.Y + dy * direction, -Pitch.HalfWidth + 1, Pitch.HalfWidth - 1));
            if ((candidate - carrier.Pos).Length < 5
                || (candidate.X * direction > assigned.X * direction && candidate.X * direction > offsideLine - 1)) continue;
            double score = Score(candidate);
            if (score > best) { best = score; chosen = candidate; }
        }
        return chosen;
    }
}
