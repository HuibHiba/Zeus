namespace Zeus.Sim;

public static class PressingAssignment
{
    /// <summary>One nearest defender who can actually see/sense the ball; index order breaks exact ties.</summary>
    public static int? Select(Snapshot world, Side defendingTeam, PerceptionConfig sensing, double stepSeconds)
    {
        if (world.Ball is null) return null;
        int? best = null;
        double nearest = double.MaxValue;
        for (int i = 0; i < world.Players.Count; i++)
        {
            var player = world.Players[i];
            if (player.Team != defendingTeam || player.SentOff) continue;
            var view = PerceptionBuilder.Build(world, player, GamePhase.Verdedigen, false, sensing, stepSeconds);
            if (view.VisibleBall is null) continue;
            double distance = (player.Pos - world.Ball.Pos.XY).Length;
            if (distance < nearest) { nearest = distance; best = i; }
        }
        return best;
    }
}
