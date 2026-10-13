using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class TeamPhasesTests
{
    [Fact]
    public void InitialPossessorBuildsAndOpponentDefends()
    {
        var phases = new TeamPhases();
        Assert.Equal(GamePhase.Opbouw, phases.PhaseOf(Side.Home));
        Assert.Equal(GamePhase.Verdedigen, phases.PhaseOf(Side.Away));
        Assert.Empty(phases.Update(Side.Home, false, .3, 0));
        var awayInitial = new TeamPhases(Side.Away);
        Assert.Equal(GamePhase.Opbouw, awayInitial.PhaseOf(Side.Away));
        Assert.Equal(GamePhase.Verdedigen, awayInitial.PhaseOf(Side.Home));
    }

    [Fact]
    public void EachTeamUsesItsOwnAttackDirectionAndThresholdEqualityIsAttack()
    {
        var home = new TeamPhases();
        var change = Assert.Single(home.Update(Side.Home, false, .66, 0));
        Assert.Equal(GamePhase.Aanval, change.To);
        Assert.Equal("ball_zone", change.Reason);
        Assert.Single(home.Update(Side.Home, false, .65, 1));
        Assert.Equal(GamePhase.Opbouw, home.PhaseOf(Side.Home));
        var away = new TeamPhases(Side.Away);
        away.Update(Side.Away, false, .3, 0);
        Assert.Equal(GamePhase.Aanval, away.PhaseOf(Side.Away));
        away.Update(Side.Away, false, .7, 1);
        Assert.Equal(GamePhase.Opbouw, away.PhaseOf(Side.Away));
    }

    [Fact]
    public void PossessionSwitchChangesBothTeamsInHomeFirstOrderAndExpiresExactly()
    {
        var phases = new TeamPhases();
        var changes = phases.Update(Side.Away, false, .2, 1);
        Assert.Equal(new[] { Side.Home, Side.Away }, changes.Select(c => c.Team));
        Assert.Equal(GamePhase.Balverlies, changes[0].To);
        Assert.Equal(GamePhase.Balwinst, changes[1].To);
        Assert.All(changes, c => Assert.Equal("possession_changed", c.Reason));
        Assert.Empty(phases.Update(Side.Away, false, .2, 3.999));
        var expiry = phases.Update(Side.Away, false, .2, 4);
        Assert.Equal(GamePhase.Verdedigen, phases.PhaseOf(Side.Home));
        Assert.Equal(GamePhase.Aanval, phases.PhaseOf(Side.Away));
        Assert.All(expiry, c => Assert.Equal("transition_expired", c.Reason));
    }

    [Fact]
    public void RepeatedStealsResetTheTransitionWindow()
    {
        var phases = new TeamPhases();
        phases.Update(Side.Away, false, .5, 1);
        phases.Update(Side.Home, false, .5, 2);
        phases.Update(Side.Away, false, .5, 3);
        Assert.Empty(phases.Update(Side.Away, false, .5, 5.999));
        Assert.Equal(GamePhase.Balwinst, phases.PhaseOf(Side.Away));
        phases.Update(Side.Away, false, .5, 6);
        Assert.Equal(GamePhase.Opbouw, phases.PhaseOf(Side.Away));
    }

    [Fact]
    public void StopCancelsTransitionAndResumeUsesCurrentPossessionWithoutSyntheticSteal()
    {
        var phases = new TeamPhases();
        phases.Update(Side.Away, false, .2, 1);
        var stop = phases.Update(Side.Away, true, .2, 1.1);
        Assert.All(stop, c => Assert.Equal(GamePhase.Stilstaand, c.To));
        Assert.All(stop, c => Assert.Equal("stopped", c.Reason));
        Assert.Empty(phases.Update(Side.Home, true, .9, 1.2));
        var resumed = phases.Update(Side.Home, false, .9, 1.3);
        Assert.Equal(GamePhase.Aanval, phases.PhaseOf(Side.Home));
        Assert.Equal(GamePhase.Verdedigen, phases.PhaseOf(Side.Away));
        Assert.All(resumed, c => Assert.Equal("resumed", c.Reason));
    }

    [Fact]
    public void LongStopAndPossessionChangeOnlyAtResumeAlsoSelectBaseline()
    {
        var phases = new TeamPhases();
        phases.Update(Side.Home, true, .2, 1);
        phases.Update(Side.Away, false, .2, 100);
        Assert.Equal(GamePhase.Aanval, phases.PhaseOf(Side.Away));
        Assert.Equal(GamePhase.Verdedigen, phases.PhaseOf(Side.Home));
        Assert.Empty(phases.Update(Side.Away, false, .2, 101));
    }

    [Fact]
    public void ZeroTransitionAndCustomThresholdAreSupported()
    {
        var phases = new TeamPhases(transitionSeconds: 0, attackThirdThreshold: .8);
        phases.Update(Side.Away, false, .3, 1);
        Assert.Equal(GamePhase.Opbouw, phases.PhaseOf(Side.Away));
        phases.Update(Side.Away, false, .1, 1);
        Assert.Equal(GamePhase.Aanval, phases.PhaseOf(Side.Away));
    }

    [Fact]
    public void InvalidUpdateDoesNotMutatePhasesOrTime()
    {
        var phases = new TeamPhases();
        phases.Update(Side.Home, false, .2, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => phases.Update(Side.Away, false, .2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => phases.Update(Side.Away, false, double.NaN, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => phases.Update(Side.Away, false, -0.01, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => phases.Update(Side.Away, false, 1.01, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => phases.Update(Side.Away, false, .2, double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => phases.Update((Side)99, false, .2, 3));
        Assert.Equal(GamePhase.Opbouw, phases.PhaseOf(Side.Home));
        Assert.Empty(phases.Update(Side.Home, false, .2, 2));
    }

    [Fact]
    public void InvalidConfigurationAndUnknownPhaseTeamAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new TeamPhases((Side)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TeamPhases(transitionSeconds: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TeamPhases(transitionSeconds: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TeamPhases(attackThirdThreshold: 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TeamPhases(attackThirdThreshold: double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => new TeamPhases().PhaseOf((Side)99));
    }
}
