using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class RefereeTests
{
    private static RefereeIncident Incident(Conduct? conduct) => new(ContactAction.Charge, conduct, true, true,
        TacticalContext: TacticalContext.NoTacticalOffence);

    [Theory]
    [InlineData(Conduct.Careless, CardDecision.None)]
    [InlineData(Conduct.Reckless, CardDecision.Yellow)]
    [InlineData(Conduct.ExcessiveForce, CardDecision.Red)]
    public void EstablishedConductDeterminesCardWithoutRandomDraw(Conduct conduct, CardDecision card)
    {
        var decision = Referee.Assess(Incident(conduct));
        Assert.Equal(RefereeRestart.DirectFreeKick, decision.Restart);
        Assert.Equal(card, decision.Card);
        Assert.True(decision.Complete);
    }

    [Fact]
    public void MissingFactsAreNotSilentlyClassifiedAsCareless()
    {
        Assert.False(Referee.Assess(Incident(null)).Complete);
        Assert.False(Referee.Assess(Incident(Conduct.Careless) with { ContactOrPunishableAttempt = false }).Complete);
        Assert.False(Referee.Assess(Incident(Conduct.Careless) with { TacticalContext = TacticalContext.NotAssessed }).Complete);
        Assert.Equal(RefereeRestart.None, Referee.Assess(Incident(Conduct.Fair)).Restart);
    }

    [Fact]
    public void PenaltyAndBallOutOfPlayChangeRestartNotConductCard()
    {
        Assert.Equal(RefereeRestart.Penalty,
            Referee.Assess(Incident(Conduct.Reckless) with { InsideDefendingPenaltyArea = true }).Restart);
        var stopped = Referee.Assess(Incident(Conduct.ExcessiveForce) with { BallInPlay = false });
        Assert.Equal(RefereeRestart.RetainExisting, stopped.Restart);
        Assert.Equal(CardDecision.Red, stopped.Card);
    }

    [Fact]
    public void HoldingIsAnOffenceWithoutCarelessClassification()
    {
        Assert.Equal(RefereeRestart.DirectFreeKick,
            Referee.Assess(Incident(null) with { Action = ContactAction.Hold }).Restart);
    }
}
