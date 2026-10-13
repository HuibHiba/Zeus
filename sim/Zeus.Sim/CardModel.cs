namespace Zeus.Sim;

public enum CardDecision { None, Yellow, Red, SecondYellowRed }

/// <summary>Probabilities after a foul has already been established, before accounting for an existing yellow.</summary>
public readonly record struct CardChances(double None, double Yellow, double Red);

/// <summary>Uncalibrated v0 card assumptions; these probabilities do not determine whether a foul occurs.</summary>
public sealed record CardConfig(
    double BaseRed = 0.0005,
    double BaseYellow = 0.04,
    double SpeedScale = 8,
    double SpeedRed = 0.005,
    double BehindRed = 0.004,
    double SpeedYellow = 0.04,
    double BehindYellow = 0.02,
    double MaxRed = 0.025,
    double MaxYellow = 0.20)
{
    public static readonly CardConfig Default = new();

    public void Validate()
    {
        if (!double.IsFinite(SpeedScale) || SpeedScale <= 0
            || new[] { BaseRed, BaseYellow, SpeedRed, BehindRed, SpeedYellow, BehindYellow, MaxRed, MaxYellow }
                .Any(v => !double.IsFinite(v) || v < 0 || v > 1))
            throw new ArgumentException("Invalid card settings.");
    }
}

/// <summary>Pure card decision for an existing foul; no second foul probability or player collision.</summary>
public static class CardModel
{
    /// <summary>Relative speed in m/s is clamped to a severity in [0,1]; negative speed means zero severity.</summary>
    public static CardChances Chances(double relativeSpeed, bool fromBehind, CardConfig? config = null)
    {
        config ??= CardConfig.Default;
        config.Validate();
        if (!double.IsFinite(relativeSpeed))
            throw new ArgumentException("Relative speed must be finite.", nameof(relativeSpeed));
        double severity = Math.Clamp(relativeSpeed / config.SpeedScale, 0, 1);
        double red = Math.Clamp(config.BaseRed + config.SpeedRed * severity
            + (fromBehind ? config.BehindRed : 0), 0, config.MaxRed);
        // Direct red has priority; yellow is capped by the remaining probability mass.
        double yellow = Math.Clamp(config.BaseYellow + config.SpeedYellow * severity
            + (fromBehind ? config.BehindYellow : 0), 0, Math.Min(config.MaxYellow, 1 - red));
        return new CardChances(1 - red - yellow, yellow, red);
    }

    /// <summary>Exactly one draw per foul. A second yellow becomes a dismissal; direct red remains direct red.</summary>
    public static CardDecision ChooseCard(double relativeSpeed, bool fromBehind, bool alreadyYellow,
        Rng rng, CardConfig? config = null)
    {
        var chances = Chances(relativeSpeed, fromBehind, config);
        double draw = rng.NextDouble();
        if (draw < chances.Red) return CardDecision.Red;
        if (draw < chances.Red + chances.Yellow)
            return alreadyYellow ? CardDecision.SecondYellowRed : CardDecision.Yellow;
        return CardDecision.None;
    }
}
