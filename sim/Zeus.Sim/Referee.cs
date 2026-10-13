namespace Zeus.Sim;

public enum ContactAction { Charge, JumpAt, Kick, Trip, Push, Strike, Tackle, Hold }
public enum Conduct { Fair, Careless, Reckless, ExcessiveForce }
public enum TacticalContext { NotAssessed, NoTacticalOffence }
public enum RefereeRestart { None, DirectFreeKick, Penalty, RetainExisting }

/// <summary>Established incident facts, not a probability of a referee choosing a card.</summary>
public sealed record RefereeIncident(ContactAction Action, Conduct? Conduct, bool BallInPlay,
    bool ContactOrPunishableAttempt, bool InsideDefendingPenaltyArea = false,
    TacticalContext TacticalContext = TacticalContext.NotAssessed);

public sealed record RefereeDecision(RefereeRestart Restart, CardDecision Card, bool Complete, string Reason);

/// <summary>IFAB 2026/27 Law 12 subset. SPA/DOGSO, handball, advantage and indirect offences are not adjudicated here.</summary>
public static class Referee
{
    public static RefereeDecision Assess(RefereeIncident incident)
    {
        if (!Enum.IsDefined(incident.Action) || !Enum.IsDefined(incident.TacticalContext)
            || (incident.Conduct is { } severity && !Enum.IsDefined(severity)))
            throw new ArgumentException("Unknown incident classification.");
        if (incident.Action != ContactAction.Hold && incident.Conduct is null)
            return new(RefereeRestart.None, CardDecision.None, false, "conduct not established");
        if (!incident.ContactOrPunishableAttempt)
            return new(RefereeRestart.None, CardDecision.None, false, "contact/attempt not established");
        bool foul = incident.Action == ContactAction.Hold || incident.Conduct != Conduct.Fair;
        if (!foul) return new(RefereeRestart.None, CardDecision.None, true, "fair contact");
        var card = incident.Conduct switch {
            Conduct.Reckless => CardDecision.Yellow,
            Conduct.ExcessiveForce => CardDecision.Red,
            _ => CardDecision.None
        };
        var restart = !incident.BallInPlay ? RefereeRestart.RetainExisting
            : incident.InsideDefendingPenaltyArea ? RefereeRestart.Penalty : RefereeRestart.DirectFreeKick;
        bool complete = incident.TacticalContext == TacticalContext.NoTacticalOffence;
        return new(restart, card, complete, complete ? "Law 12 contact/conduct subset"
            : "conduct assessed; SPA/DOGSO not assessed");
    }
}
