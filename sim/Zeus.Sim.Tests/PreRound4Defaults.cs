namespace Zeus.Sim.Tests;

/// <summary>
/// The calibrated defaults before calibration round 4 (docs/kalibratie.md). Tests that pin a historical full-match hash
/// or that need opponents to touch the ball close to the kicker use these so they keep testing their own subject.
/// </summary>
internal static class PreRound4Defaults
{
    public static readonly BallControlConfig InterceptControl = new(1.2, 0.3, 9.0);
    public static readonly ShotConfig Shots = new(MinGoalProbability: 0.125);
    public static readonly DuelConfig Duels = new(BaseFoul: 0.34, AttemptRate: 0.05, RecklessOwnThird: 0.46,
        RecklessMiddle: 0.737, RecklessAttacking: 0.75);

    public static PassConfig Passing(PassConfig config) => config with { HoldSeconds = 3.0, ClearZoneMeters = 37, ClearStopsAtTarget = false };

    public static MatchConfig Apply(MatchConfig config) => config with
    {
        InterceptControl = InterceptControl, Shots = Shots, Duels = Duels, Passing = Passing(config.Passing ?? new PassConfig()),
    };
}
