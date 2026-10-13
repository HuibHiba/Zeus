namespace Zeus.Sim;

/// <summary>
/// v0 assumptions for executing tactic triggers (docs/blokwissels.md, "Triggers"). Not calibrated on data.
/// </summary>
public sealed record TriggerConfig(
    // A fired trigger keeps its players pressing for this long, or until their team has the ball.
    double PressSeconds = 4,
    // 'slechte_aanname': the receiver takes a pass whose speed relative to him is at least this (m/s): a heavy touch.
    double BadTouchRelativeSpeed = 9,
    // 'tegenstander_ontvangt_op_flank': the receiver stands at least this far from the long axis of the pitch (|y| in m).
    double FlankFromMeters = 22)
{
    public void Validate()
    {
        if (!double.IsFinite(PressSeconds) || PressSeconds <= 0 || !double.IsFinite(BadTouchRelativeSpeed) || BadTouchRelativeSpeed <= 0
            || !double.IsFinite(FlankFromMeters) || FlankFromMeters <= 0 || FlankFromMeters > Pitch.HalfWidth)
            throw new ArgumentException("Invalid trigger settings.");
    }
}

public static class TriggerRules
{
    public const string BallToKeeper = "bal_naar_keeper";
    public const string BadTouch = "slechte_aanname";
    public const string OpponentOnFlank = "tegenstander_ontvangt_op_flank";
    public const string StartPressing = "start_pressing";

    /// <summary>
    /// Executable triggers: a known condition, the 'verdedigen' phase, no zone (its meaning is not defined, so a trigger with a
    /// zone is not guessed) and only 'start_pressing' actions. Everything else stays inactive and is never reinterpreted.
    /// </summary>
    public static bool IsSupported(Trigger trigger) =>
        trigger.Phase == GamePhase.Verdedigen && trigger.Zone is null
        && trigger.Condition is BallToKeeper or BadTouch or OpponentOnFlank
        && trigger.Then.All(a => a.Instruction == StartPressing);

    /// <summary>Heavy touch: the pass reaches the receiver faster than he can settle it.</summary>
    public static bool IsBadTouch(double relativeSpeed, TriggerConfig config) =>
        double.IsFinite(relativeSpeed) && relativeSpeed >= config.BadTouchRelativeSpeed;

    /// <summary>Wide area: |y| is the same for both teams and after the change of ends, so it needs no mirroring.</summary>
    public static bool IsOnFlank(double y, TriggerConfig config) =>
        double.IsFinite(y) && Math.Abs(y) >= config.FlankFromMeters;
}
