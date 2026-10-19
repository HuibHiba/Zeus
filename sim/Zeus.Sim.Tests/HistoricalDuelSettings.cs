namespace Zeus.Sim.Tests;

/// <summary>Pre-Wyscout duel settings for frozen historical fixtures, never shipped in production.</summary>
internal static class HistoricalDuelSettings
{
    public static DuelConfig Create() => new(AttemptRate: .065, RecklessOwnThird: .44,
        RecklessMiddle: .7, RecklessAttacking: .7, ExcessiveForceScore: .787, PenaltyAreaFoulFactor: .2,
        CautionedAvoidsBehind: false);
}
