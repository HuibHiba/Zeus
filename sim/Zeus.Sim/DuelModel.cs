namespace Zeus.Sim;

public enum DuelOutcome { WonBall, LooseBall, Foul, None }

public readonly record struct DuelChances(double WonBall, double LooseBall, double Foul, double None);

/// <summary>Uncalibrated v0 duel assumptions. No physical player collision is simulated.</summary>
public sealed record DuelConfig(
    double Reach = 1.2,
    double SpeedScale = 8,
    double MaxFoul = 0.6,
    double BaseFoul = 0.34,
    double SpeedFoul = 0.15,
    double ApproachFoul = 0.15,
    double IndisciplineFoul = 0.20,
    double FatigueFoul = 0.10,
    double BaseWin = 0.50,
    double SkillWin = 0.60,
    double FatigueWinPenalty = 0.35,
    double ApproachWinPenalty = 0.25,
    double LooseChance = 0.45,
    // Chance per second that a pressing defender within Reach actually commits to a challenge (event Duel).
    // Source: Referentie wedstrijdevents: ca. 33 duelpogingen (tackles + overtredingen) per team per wedstrijd; the sim has
    // ca. 720 'press within reach' seconds per team, so 33/720 = 0.046. Calibrated on counts, not on physical tackle geometry.
    double AttemptRate = 0.029,
    // Conduct classification of a foul from duel facts (deterministic, no draw): score = w*speed + (1-w)*approach,
    // speed = closing speed / SpeedScale, approach = |facing angle| / pi (0 in front, 1 from behind).
    // Reckless (yellow) from a zone-dependent score threshold, ExcessiveForce (red) above a high threshold.
    // Thresholds are set so ca. 11% of fouls get a yellow (own third 17%, middle 11%, attacking 7%) and ca. 0.5% a red,
    // Source: calibration/event_models.json overtreding_locatie_kaart; Referentie wedstrijdevents. Counts, not law interpretation.
    double ConductSpeedWeight = 0.5,
    double RecklessOwnThird = 0.46,
    double RecklessMiddle = 0.737,
    double RecklessAttacking = 0.75,
    double ExcessiveForceScore = 0.787)
{
    public static readonly DuelConfig Default = new();

    public void Validate()
    {
        if (!double.IsFinite(Reach) || Reach <= 0 || !double.IsFinite(SpeedScale) || SpeedScale <= 0
            || new[] { MaxFoul, BaseFoul, SpeedFoul, ApproachFoul, IndisciplineFoul, FatigueFoul,
                BaseWin, SkillWin, FatigueWinPenalty, ApproachWinPenalty, LooseChance, AttemptRate, ConductSpeedWeight, RecklessOwnThird, RecklessMiddle, RecklessAttacking, ExcessiveForceScore }
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

    /// <summary>
    /// Deterministic conduct of a foul from its measured facts. relativeX is the foul position in the offender's attacking
    /// direction (0 = own goal line, 1 = opponent goal line). Fair is never returned: the foul is already established.
    /// </summary>
    public static Conduct ClassifyConduct(double closingSpeed, double facingAngle, double relativeX, DuelConfig? config = null)
    {
        config ??= DuelConfig.Default;
        config.Validate();
        if (!double.IsFinite(closingSpeed) || !double.IsFinite(facingAngle) || !double.IsFinite(relativeX))
            throw new ArgumentException("Conduct inputs must be finite.");
        double speed = Math.Clamp(closingSpeed / config.SpeedScale, 0, 1);
        double approach = Math.Abs(Mover.NormalizeAngle(facingAngle)) / Math.PI;
        double score = config.ConductSpeedWeight * speed + (1 - config.ConductSpeedWeight) * approach;
        if (score >= config.ExcessiveForceScore) return Conduct.ExcessiveForce;
        double threshold = relativeX < 1.0 / 3 ? config.RecklessOwnThird
            : relativeX < 2.0 / 3 ? config.RecklessMiddle : config.RecklessAttacking;
        return score >= threshold ? Conduct.Reckless : Conduct.Careless;
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
