namespace Zeus.Sim;

/// <summary>
/// Penalty-kick execution, separate from open-play shots. Calibrated on the conversion rate (real: ca. 75-78% scored,
/// ca. 8-10% off target, the rest saved), not on a measured run-up/dive model; see docs/kalibratie.md "Ronde 3".
/// </summary>
public sealed record PenaltyConfig(
    // The taker aims at a corner: this far from the goal centre (m). The posts stand at 3.66 m; takers aim ca. 0.3-0.6 m inside them.
    double AimOffsetMeters = 3.1,
    // Direction error (rad, triangular) = AngleErrorRadians x (1 - skill); 0.24 at the default skill 0.75 is 0.06 rad (about +-0.7 m at 11 m).
    // Smaller than an open-play shot (ShotConfig.AngleErrorBase): the ball is still, the taker has all the time.
    double AngleErrorRadians = 0.24,
    // The keeper commits to a side at the moment of the kick (50/50, a draw) and pushes off at this speed (m/s) towards ...
    double KeeperPushOffSpeed = 1.7,
    // ... this lateral distance from the goal centre (m), for this long (s), after which his normal behaviour resumes.
    // AimOffsetMeters, AngleErrorRadians, KeeperPushOffSpeed and KeeperReachMeters were tuned together on 1000 penalties
    // (75% scored, 15% saved, 9.5% off target; real ca. 75-78%, 17%, 8%), not measured; the push-off is the most sensitive one.
    double KeeperDiveMeters = 2.2,
    double KeeperDiveSeconds = 0.8,
    // Reach of the keeper (m) while the penalty is in flight. Smaller than KeeperSettings (2.5 m, fitted on open-play shots that
    // he reads in flight): a ball placed in the corner at 26 m/s is reached only at full stretch, even on the right side.
    double KeeperReachMeters = 1.6)
{
    public static readonly PenaltyConfig Default = new();

    public void Validate()
    {
        if (new[] { AimOffsetMeters, AngleErrorRadians, KeeperPushOffSpeed, KeeperDiveMeters, KeeperDiveSeconds, KeeperReachMeters }
                .Any(v => !double.IsFinite(v) || v < 0)
            || AimOffsetMeters > 3.66 + 2 || KeeperDiveMeters > 3.66 || KeeperDiveSeconds <= 0 || KeeperReachMeters <= 0 || KeeperPushOffSpeed > 10)
            throw new ArgumentException("Invalid penalty settings.");
    }
}

public static class PenaltyExecution
{
    /// <summary>
    /// Ball velocity of a penalty from the spot towards the corner on <paramref name="side"/> (+1 = positive y, -1 = negative y)
    /// of the goal at <paramref name="goalX"/>. One shot execution (4 draws); the outcome is decided by flight and keeper contact.
    /// </summary>
    public static Vec3 Velocity(Vec2 spot, double goalX, int side, double skill, Rng rng, PenaltyConfig penalty, ShotConfig shot)
    {
        var aim = new Vec2(goalX, Math.Sign(side) * penalty.AimOffsetMeters);
        return ShotExecution.Velocity(spot, aim, skill, rng,
            shot with { AngleErrorBase = penalty.AngleErrorRadians, AngleErrorPerMeter = 0 });
    }
}
