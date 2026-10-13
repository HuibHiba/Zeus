namespace Zeus.Sim;

/// <summary>Energy reservoirs: one means fresh. ShortEnergy must not exceed LongEnergy.</summary>
public readonly record struct FatigueState(double ShortEnergy, double LongEnergy)
{
    public static readonly FatigueState Fresh = new(1, 1);
}

/// <summary>Uncalibrated gameplay assumptions; rates apply per simulated second, not per decision tick.</summary>
public sealed record FatigueConfig(
    double SprintThreshold = 5.5,         // m/s, from the provisional player-model design
    double RecoveryThreshold = 2.0,       // m/s; no short recovery while actively pressing
    double ShortSprintRate = 0.008,       // energy per second per m/s above the sprint threshold
    double ShortPressingRate = 0.002,     // energy per second while pressing
    double ShortRecoveryRate = 0.012,     // energy per second at low speed when not pressing
    double LongDistanceRate = 0.000017,   // energy per metre travelled
    double LongPressingRate = 0.000025,   // energy per second while pressing
    double MinEnduranceFactor = 0.7,
    double EnduranceFactorRange = 0.6,
    double MinSpeedMultiplier = 0.85,
    double MinAccelerationMultiplier = 1.0, // default: acceleration unchanged
    double MaxReactionExtraSeconds = 0.15)
{
    public static readonly FatigueConfig Default = new();

    public void Validate()
    {
        if (new[] { SprintThreshold, RecoveryThreshold, ShortSprintRate, ShortPressingRate,
                ShortRecoveryRate, LongDistanceRate, LongPressingRate, EnduranceFactorRange,
                MaxReactionExtraSeconds }.Any(v => !double.IsFinite(v) || v < 0)
            || !double.IsFinite(MinEnduranceFactor) || MinEnduranceFactor <= 0
            || !double.IsFinite(MinSpeedMultiplier) || MinSpeedMultiplier < 0 || MinSpeedMultiplier > 1
            || !double.IsFinite(MinAccelerationMultiplier) || MinAccelerationMultiplier < 0 || MinAccelerationMultiplier > 1)
            throw new ArgumentException("Invalid fatigue settings.");
    }
}

/// <summary>Pure two-reservoir fatigue model, without random draws or player/match state.</summary>
public static class Fatigue
{
    /// <summary>
    /// Advances constant exertion over dt seconds. Speed is a nonnegative magnitude (negative input clamps to zero);
    /// endurance clamps to [0,1]. Long energy never recovers during a match; short recovery cannot exceed it.
    /// </summary>
    public static FatigueState Step(FatigueState state, double speed, bool pressing, double endurance,
        double dt, FatigueConfig? config = null)
    {
        config ??= FatigueConfig.Default;
        config.Validate();
        state = Normalize(state);
        if (!double.IsFinite(speed) || !double.IsFinite(endurance) || !double.IsFinite(dt) || dt < 0)
            throw new ArgumentException("Fatigue inputs must be finite and dt nonnegative.");
        speed = Math.Max(0, speed);
        double factor = config.MinEnduranceFactor + config.EnduranceFactorRange * Math.Clamp(endurance, 0, 1);
        double longRate = (config.LongDistanceRate * speed + (pressing ? config.LongPressingRate : 0)) / factor;
        double shortRate = -(config.ShortSprintRate * Math.Max(0, speed - config.SprintThreshold)
            + (pressing ? config.ShortPressingRate : 0)) / factor;
        if (speed < config.RecoveryThreshold && !pressing) shortRate += config.ShortRecoveryRate * factor;
        double longEnergy = Math.Clamp(state.LongEnergy - longRate * dt, 0, 1);
        double shortEnergy = Math.Clamp(state.ShortEnergy + shortRate * dt, 0, longEnergy);
        return new FatigueState(shortEnergy, longEnergy);
    }

    /// <summary>Applies provisional energy effects; deceleration and turn rate retain their base values.</summary>
    public static PlayerProfile Apply(PlayerProfile profile, FatigueState state, FatigueConfig? config = null)
    {
        config ??= FatigueConfig.Default;
        config.Validate();
        state = Normalize(state);
        return profile with
        {
            MaxSpeed = profile.MaxSpeed * (config.MinSpeedMultiplier + (1 - config.MinSpeedMultiplier) * state.ShortEnergy),
            Acceleration = profile.Acceleration * (config.MinAccelerationMultiplier
                + (1 - config.MinAccelerationMultiplier) * state.ShortEnergy)
        };
    }

    public static double ReactionExtra(FatigueState state, FatigueConfig? config = null)
    {
        config ??= FatigueConfig.Default;
        config.Validate();
        return config.MaxReactionExtraSeconds * (1 - Normalize(state).LongEnergy);
    }

    private static FatigueState Normalize(FatigueState state)
    {
        if (!double.IsFinite(state.ShortEnergy) || !double.IsFinite(state.LongEnergy))
            throw new ArgumentException("Energy reservoirs must be finite.");
        double longEnergy = Math.Clamp(state.LongEnergy, 0, 1);
        return new FatigueState(Math.Clamp(state.ShortEnergy, 0, longEnergy), longEnergy);
    }
}
