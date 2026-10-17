namespace Zeus.Sim.Tests;

/// <summary>
/// Pre-Wyscout shot settings for frozen historical fixtures only. These values
/// stay in the test assembly; the production defaults use the public source.
/// </summary>
internal static class HistoricalShotSettings
{
    public static ShotConfig Apply(ShotConfig? config = null) => (config ?? new ShotConfig()) with
    {
        GoalLogitIntercept = -0.64518,
        GoalLogitDistance = -0.1465,
        GoalLogitDistanceSquared = 0.09862,
        GoalLogitAngle = 1.30056,
        AimOffsetMeters = 2.4,
    };
}
