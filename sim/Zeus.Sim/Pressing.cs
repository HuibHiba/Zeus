namespace Zeus.Sim;

public static class PressingAssignment
{
    /// <summary>
    /// A defender presses when he sees the ball and none of the teammates he can see is closer to it (exact ties: lower id).
    /// Decided from the player's own perception only, so two defenders who do not see each other may both press, and none
    /// presses when nobody sees the ball. The keeper never presses.
    /// </summary>
    public static bool IsPresser(Perception view)
    {
        if (view.Self.IsKeeper || view.VisibleBall is not { } ball) return false;
        double own = (view.Self.Pos - ball.Pos.XY).Length;
        foreach (var mate in view.VisiblePlayers)
        {
            if (mate.Team != view.Self.Team || mate.IsKeeper) continue;
            double d = (mate.Pos - ball.Pos.XY).Length;
            if (d < own - 1e-9 || (Math.Abs(d - own) <= 1e-9 && string.CompareOrdinal(mate.Id, view.Self.Id) < 0)) return false;
        }
        return true;
    }
}
