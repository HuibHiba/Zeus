namespace Zeus.Sim.Tests;

/// <summary>
/// The offside-related defaults before calibration round 5 (docs/kalibratie.md): PassConfig.OffsideMargin -1.5 and the
/// attacking-run settings with run depth 1.5 m, through-ball bonus 0.8 and run cooldown 60 s. Tests that pin a historical
/// full-match hash or that assert numbers derived from the old run geometry use these.
/// </summary>
internal static class PreRound5Defaults
{
    public const double OffsideMargin = -1.5;
    public static readonly AttackingRunConfig Runs = new(RunDepthMeters: 1.5, ThroughBallBonus: 0.8, RunCooldownSeconds: 60.0);

    /// <summary>Pins the old margin (unless the test set its own) and the old run settings (unless the test set its own).</summary>
    public static MatchConfig Apply(MatchConfig config)
    {
        var passing = config.Passing ?? new PassConfig();
        if (passing.OffsideMargin == new PassConfig().OffsideMargin) passing = passing with { OffsideMargin = OffsideMargin };
        passing = passing with { ClearStopsAtTarget = false, EarlyShotXg = null }; // historical clearance and hold
        return config with { Instructions = config.Instructions ?? InstructionConfig.Disabled, Passing = passing,
            Duels = (config.Duels ?? new DuelConfig()) with { CautionedAttemptFactor = 1 }, AttackingRuns = config.AttackingRuns ?? Runs };
    }
}
