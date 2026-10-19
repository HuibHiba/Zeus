namespace Zeus.Sim;

/// <summary>Configurable v0 trigger assumptions; see docs/blokwissels.md.</summary>
public sealed record TriggerConfig(
    // Pressing lasts this long, or until the team's next decision with its own possession.
    double PressSeconds = 4,
    // Speed relative to the intended receiver (m/s).
    double BadTouchRelativeSpeed = 9,
    // Distance from the long axis (|y| in metres).
    double FlankFromMeters = 22,
    // Team-relative zone boundaries, including mirrored ends.
    double DefensiveThirdEnd = 1.0 / 3,
    double AttackingThirdStart = 2.0 / 3,
    double LeftOutsideEnd = 0.2,
    double LeftHalfEnd = 0.4,
    double CentreEnd = 0.6,
    double RightHalfEnd = 0.8,
    double OwnHalfEnd = 0.5,
    double BackwardPassFraction = 0.3)
{
    public void Validate()
    {
        if (!double.IsFinite(PressSeconds) || PressSeconds <= 0 || !double.IsFinite(BadTouchRelativeSpeed) || BadTouchRelativeSpeed <= 0
            || !double.IsFinite(FlankFromMeters) || FlankFromMeters <= 0 || FlankFromMeters > Pitch.HalfWidth
            || !Unit(DefensiveThirdEnd) || !Unit(AttackingThirdStart) || DefensiveThirdEnd <= 0
            || DefensiveThirdEnd >= AttackingThirdStart || AttackingThirdStart >= 1
            || !Unit(LeftOutsideEnd) || !Unit(LeftHalfEnd) || !Unit(CentreEnd) || !Unit(RightHalfEnd)
            || LeftOutsideEnd <= 0 || LeftOutsideEnd >= LeftHalfEnd || LeftHalfEnd >= CentreEnd
            || CentreEnd >= RightHalfEnd || RightHalfEnd >= 1
            || !Unit(OwnHalfEnd) || OwnHalfEnd <= 0 || OwnHalfEnd >= 1 || !Unit(BackwardPassFraction))
            throw new ArgumentException("Invalid trigger settings.");
    }

    private static bool Unit(double value) => double.IsFinite(value) && value >= 0 && value <= 1;
}

public static class TriggerRules
{
    public const string BallToKeeper = "bal_naar_keeper";
    public const string BadTouch = "slechte_aanname";
    public const string OpponentOnFlank = "tegenstander_ontvangt_op_flank";
    public const string BackPassToMidfielder = "bal_terug_naar_middenvelder";
    public const string LossInOwnHalf = "balverlies_in_eigen_helft";
    public const string WinInHighZone = "balwinst_in_hoge_zone";
    public const string StartPressing = "start_pressing";
    // State conditions: checked once per PressSeconds while they hold (see Match.UpdateStateTriggers).
    public const string OpponentBallInZone = "tegenstander_heeft_bal_in_zone";
    public const string BehindAfterMinute = "achterstand_na_minuut_70";
    public const int BehindFromMinute = 70;

    public static bool IsValidZone(string zone) => zone.Split('-') is var parts && parts.Length == 2
        && parts[0] is "V" or "M" or "A" && parts[1] is "BL" or "HL" or "C" or "HR" or "BR";

    /// <summary>Each condition belongs to one phase; unsupported instructions remain inactive.</summary>
    public static bool IsSupported(Trigger trigger) =>
        (trigger.Condition switch {
            BallToKeeper or BadTouch or OpponentOnFlank or BackPassToMidfielder => trigger.Phase == GamePhase.Verdedigen,
            LossInOwnHalf => trigger.Phase == GamePhase.Balverlies,
            WinInHighZone => trigger.Phase == GamePhase.Balwinst,
            OpponentBallInZone => trigger.Phase == GamePhase.Verdedigen,
            BehindAfterMinute => true,
            _ => false
        }) && (trigger.Zone is null || IsValidZone(trigger.Zone))
        && trigger.Then.All(a => (a.Instruction is null && a.Path) || a.Instruction is { } i && SupportedInstructions.Contains(i));

    /// <summary>Instructions a rule can give; a rule with any other instruction stays inactive as a whole (the editor offers only these).</summary>
    public static IReadOnlyList<string> SupportedInstructions { get; } = Array.AsReadOnly(new[] { StartPressing, "blijf_achter", "laat_zakken", "blijf_breed", "kom_op",
        "sluit_naar_binnen", "loop_zestien_in", "dek_ruimte_achter_lijn", "dribbel_in", "vrije_man_zoeken", "dek_man" });

    public static (double X, double Y) RelativePosition(Vec2 location, Side team, bool endsSwapped)
    {
        double sign = (team == Side.Away) != endsSwapped ? -1 : 1;
        return (Math.Clamp(location.X * sign / Pitch.Length + 0.5, 0, 1),
            Math.Clamp(location.Y * sign / Pitch.Width + 0.5, 0, 1));
    }

    public static bool IsInZone(string? zone, (double X, double Y) relative, TriggerConfig config)
    {
        if (zone is null) return true;
        if (!IsValidZone(zone) || !double.IsFinite(relative.X) || !double.IsFinite(relative.Y)) return false;
        string third = relative.X < config.DefensiveThirdEnd ? "V" : relative.X < config.AttackingThirdStart ? "M" : "A";
        string lane = relative.Y < config.LeftOutsideEnd ? "BL" : relative.Y < config.LeftHalfEnd ? "HL"
            : relative.Y < config.CentreEnd ? "C" : relative.Y <= config.RightHalfEnd ? "HR" : "BR";
        return zone == third + "-" + lane;
    }

    public static bool IsBackPassToMidfielder(string role, Vec2 pass, bool attacksPositiveX, TriggerConfig config) =>
        role is "controlerende_middenvelder" or "box_to_box" or "aanvallende_middenvelder"
        && double.IsFinite(pass.X) && double.IsFinite(pass.Y) && pass.Length > 0
        && pass.X * (attacksPositiveX ? 1 : -1) < -config.BackwardPassFraction * pass.Length;

    public static bool IsLossInOwnHalf(double relativeX, TriggerConfig config) =>
        double.IsFinite(relativeX) && relativeX < config.OwnHalfEnd;

    public static bool IsWinInHighZone(double relativeX, TriggerConfig config) =>
        double.IsFinite(relativeX) && relativeX >= config.AttackingThirdStart;

    public static bool IsBehindAfterMinute(int minute, int goalDifference) =>
        goalDifference < 0 && minute >= BehindFromMinute;

    public static bool IsBadTouch(double relativeSpeed, TriggerConfig config) =>
        double.IsFinite(relativeSpeed) && relativeSpeed >= config.BadTouchRelativeSpeed;

    public static bool IsOnFlank(double y, TriggerConfig config) =>
        double.IsFinite(y) && Math.Abs(y) >= config.FlankFromMeters;
}
