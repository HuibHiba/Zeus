using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class PossessionTests
{
    [Fact]
    public void KickOffCarrierStartsAtCentreAndKeepsBallWhileRunning()
    {
        var match = MatchTests.MakeMatch(42);
        string carrier = match.CarrierId!;
        Assert.Equal(Vec2.Zero, match.GetSnapshot().Players.Single(p => p.Team == Side.Home && p.Id == carrier).Pos);
        double maxSpeed = 0;
        for (int i = 0; i < 100; i++)
        {
            match.Step();
            Assert.Equal(carrier, match.CarrierId);
            var player = match.GetSnapshot().Players.Single(p => p.Team == Side.Home && p.Id == carrier);
            maxSpeed = Math.Max(maxSpeed, player.Vel.Length);
            Assert.Equal(0.4, (match.Ball.Pos.XY - player.Pos).Length, 8);
        }
        Assert.True(maxSpeed > 4); // Carrying uses relative speed, not the loose-ball control threshold.
    }

    [Fact]
    public void AwayControlSwitchesBothPhasesAndLogsOneChange()
    {
        var match = MatchTests.MakeMatch(42);
        var keeper = match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.Id == "GK");
        match.SetBall(new Vec3(keeper.Pos.X, keeper.Pos.Y, 0), Vec3.Zero);
        Assert.Null(match.CarrierId);
        match.Step();
        Assert.Equal(Side.Away, match.Possession);
        Assert.Equal("GK", match.CarrierId);
        Assert.Equal(GamePhase.Opbouw, match.PhaseOf(Side.Away));
        Assert.Equal(GamePhase.Verdedigen, match.PhaseOf(Side.Home));
        for (int i = 0; i < 10; i++) match.Step();
        Assert.Single(match.Events.Events.Where(e => e.Type == EventType.PossessionChange));
    }

    [Fact]
    public void FastBallAtCarriersFeetIsReleased()
    {
        var match = MatchTests.MakeMatch(42);
        match.SetBall(Vec3.Zero, new Vec3(20, 0, 0));
        match.Step();
        Assert.Null(match.CarrierId);
        Assert.True(match.Ball.Pos.X > 1);
    }

    [Fact]
    public void HighBallCannotBeControlled()
    {
        var match = MatchTests.MakeMatch(42);
        var keeper = match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.Id == "GK");
        match.SetBall(new Vec3(keeper.Pos.X, keeper.Pos.Y, 3), Vec3.Zero);
        match.Step();
        Assert.Null(match.CarrierId);
        Assert.Equal(Side.Home, match.Possession);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FollowBallMovesDefendingTargetsInWorldDirectionForEitherTeam(bool instructions)
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        foreach (var defendingSide in new[] { Side.Home, Side.Away })
        {
            // Isolate coordinate mirroring from reaction timing and restricted perception.
            var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0, Instructions: instructions ? new() : InstructionConfig.Disabled,
                Profile: new PlayerProfile(MaxSpeed: 0),
                Perception: new PerceptionConfig(FieldOfViewRadians: 2 * Math.PI, SightRange: 200,
                    ScanIntervalSeconds: 0.1)), tactic, tactic, 42);
            if (defendingSide == Side.Home)
            {
                var keeper = match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.Id == "GK");
                match.SetBall(new Vec3(keeper.Pos.X, keeper.Pos.Y, 0), Vec3.Zero);
                match.Step();
            }
            match.SetBall(new Vec3(0, 0, 10), Vec3.Zero);
            match.Step();
            var centre = match.TargetOf(defendingSide, "RC")!.Value;
            match.SetBall(new Vec3(20, 10, 10), Vec3.Zero);
            match.Step();
            var shifted = match.TargetOf(defendingSide, "RC")!.Value;
            double factor = instructions ? 1 - .4 * (tactic.Team.Compactness ?? 0) : 1;
            Assert.Equal(3 * factor, shifted.X - centre.X, 8); // 20 m * follow 0.3 * gain 0.5
            Assert.Equal(2 * factor, shifted.Y - centre.Y, 8); // 10 m * follow 0.4 * gain 0.5
        }
    }
}
