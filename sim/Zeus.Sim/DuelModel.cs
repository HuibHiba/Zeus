namespace Zeus.Sim;

public enum DuelOutcome { WonBall, LooseBall, Foul, None }

public readonly record struct DuelChances(double WonBall, double LooseBall, double Foul, double None);

/// <summary>Uncalibrated v0 duel assumptions. No physical player collision is simulated.</summary>
public sealed record DuelConfig(
    double Reach = 1.2,
    double SpeedScale = 8,
    double MaxFoul = 0.35,
    double BaseFoul = 0.02,
    double SpeedFoul = 0.12,
    double ApproachFoul = 0.10,
    double IndisciplineFoul = 0.10,
    double FatigueFoul = 0.06,
    double BaseWin = 0.15,
    double SkillWin = 0.65,
    double FatigueWinPenalty = 0.35,
    double ApproachWinPenalty = 0.25,
    double LooseChance = 0.20)
{
    public static readonly DuelConfig Default = new();

    public void Validate()
    {
        if (!double.IsFinite(Reach) || Reach <= 0 || !double.IsFinite(SpeedScale) || SpeedScale <= 0
            || new[] { MaxFoul, BaseFoul, SpeedFoul, ApproachFoul, IndisciplineFoul, FatigueFoul,
                BaseWin, SkillWin, FatigueWinPenalty, ApproachWinPenalty, LooseChance }
                .Any(v => !double.IsFinite(v) || v < 0 || v > 1))
            throw new ArgumentException("Invalid duel settings.");
    }
}

/// <summary>Pure probability model for a defender attempting a close challenge against a ball carrier.</summary>
public static class DuelModel
{
    /// <summary>
    /// Distance is in metres; closing speed is signed m/s (positive means approaching).
    /// Facing angle describes the defender's approach relative to the carrier: zero from in front,
    /// ±pi from behind. Skill, discipline and fatigue are clamped to [0,1]. All finite inputs are required.
    /// </summary>
    public static DuelChances Chances(double distance, double closingSpeed, double facingAngle,
        double defendingSkill, double discipline, double fatigue, DuelConfig? config = null)
    {
        config ??= DuelConfig.Default;
        config.Validate();
        if (new[] { distance, closingSpeed, facingAngle, defendingSkill, discipline, fatigue }
            .Any(v => !double.IsFinite(v)) || distance < 0)
            throw new ArgumentException("Duel inputs must be finite and distance nonnegative.");
        if (distance > config.Reach) return new DuelChances(0, 0, 0, 1);

        double speed = Math.Clamp(closingSpeed / config.SpeedScale, 0, 1);
        double approach = Math.Abs(Mover.NormalizeAngle(facingAngle)) / Math.PI;
        double skill = Math.Clamp(defendingSkill, 0, 1);
        double tired = Math.Clamp(fatigue, 0, 1);
        double careful = Math.Clamp(discipline, 0, 1);
        // Reach decreases the opportunity continuously within the challenge radius, with a hard outer gate.
        double reachFactor = 1 - 0.5 * distance / config.Reach;
        double foul = reachFactor * Math.Clamp(config.BaseFoul + config.SpeedFoul * speed
            + config.ApproachFoul * approach + config.IndisciplineFoul * (1 - careful)
            + config.FatigueFoul * tired, 0, config.MaxFoul);
        double winConditional = reachFactor * Math.Clamp(config.BaseWin + config.SkillWin * skill, 0, 1)
            * (1 - config.FatigueWinPenalty * tired) * (1 - config.ApproachWinPenalty * approach);
        double win = (1 - foul) * winConditional;
        double loose = (1 - foul - win) * reachFactor * config.LooseChance;
        return new DuelChances(win, loose, foul, 1 - foul - win - loose);
    }

    /// <summary>One categorical draw for an in-range attempt; no draw when outside reach.</summary>
    public static DuelOutcome Resolve(double distance, double closingSpeed, double facingAngle,
        double defendingSkill, double discipline, double fatigue, Rng rng, DuelConfig? config = null)
    {
        var chances = Chances(distance, closingSpeed, facingAngle, defendingSkill, discipline, fatigue, config);
        if (distance > (config ?? DuelConfig.Default).Reach) return DuelOutcome.None;
        double draw = rng.NextDouble();
        if (draw < chances.Foul) return DuelOutcome.Foul;
        if (draw < chances.Foul + chances.WonBall) return DuelOutcome.WonBall;
        if (draw < chances.Foul + chances.WonBall + chances.LooseBall) return DuelOutcome.LooseBall;
        return DuelOutcome.None;
    }
}
