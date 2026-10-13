namespace Zeus.Sim;

/// <summary>Pure v0 block selection: conditions are conjunctive and the last matching rule wins.</summary>
public static class BlockSelection
{
    public static BlockHeight? Select(Tactic tactic, ScoreState score, int minute, double fatigue)
    {
        ArgumentNullException.ThrowIfNull(tactic);
        if (!Enum.IsDefined(score)) throw new ArgumentOutOfRangeException(nameof(score));
        if (minute < 0) throw new ArgumentOutOfRangeException(nameof(minute));
        if (!double.IsFinite(fatigue) || fatigue < 0 || fatigue > 1)
            throw new ArgumentOutOfRangeException(nameof(fatigue));

        var selected = tactic.Phase(GamePhase.Verdedigen).Block;
        if (selected is { } initial && !Enum.IsDefined(initial))
            throw new ArgumentException("Unknown base block height.", nameof(tactic));
        foreach (var rule in tactic.BlockSwitches)
        {
            var condition = rule.When;
            if (!Enum.IsDefined(rule.To) || !tactic.DefendVariants.ContainsKey(rule.To))
                throw new ArgumentException("Block switch has no available target variant.", nameof(tactic));
            if ((condition.Score is { } requiredScore && !Enum.IsDefined(requiredScore))
                || condition.FromMinute is < 0
                || (condition.FatigueAbove is { } threshold && (!double.IsFinite(threshold) || threshold < 0 || threshold > 1))
                || (condition.Score is null && condition.FromMinute is null && condition.FatigueAbove is null))
                throw new ArgumentException("Invalid block switch condition.", nameof(tactic));

            if ((condition.Score is null || condition.Score == score)
                && (condition.FromMinute is null || minute >= condition.FromMinute.Value)
                && (condition.FatigueAbove is null || fatigue > condition.FatigueAbove.Value))
                selected = rule.To;
        }
        return selected;
    }
}
