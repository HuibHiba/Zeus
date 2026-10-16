using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class Round7Tests
{
    [Theory]
    [InlineData("laag_blok_442", "man_om_man_433", 42UL,
        "418675db369e55829f558d90dd7a0cacbfcccea001bf8869f2eeb353d8e3b11b",
        "776adb56ff3851f4293150f94bce0a640baac987cdfdbfbf21d0f926829e8d9b")]
    [InlineData("laag_blok_442", "laag_blok_442", 7UL,
        "77c96fae16aecca19dba51720f692687a9741b7559c7f484f81c7012a0056a66",
        "94923bb6b9812a0ac946b0542a48306987fff42ef0ea94be1124a687911da615")]
    public void DisabledCautionAndDefaultDisabledDragPreserveRound6Bits(string home, string away, ulong seed,
        string eventHash, string snapshotHash)
    {
        var match = Match.Create(new MatchConfig(Duels: new DuelConfig(CautionedAttemptFactor: 1), Passing: new PassConfig(EarlyShotXg: null)),
            TacticLoader.LoadFile(TacticLoaderTests.TacticPath(home)), TacticLoader.LoadFile(TacticLoaderTests.TacticPath(away)), seed);
        for (int tick = 0; tick < 150000 && match.Status != MatchStatus.Finished; tick++) match.Step();
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal(eventHash + "|" + snapshotHash, match.EventHash() + "|" + match.SnapshotHash());
    }

    private sealed class AwayPressBrain : IPlayerBrain
    {
        public Intent Decide(Perception view) => view.Self.Team == Side.Away
            ? new(IntentKind.Press, view.KnownBall?.Ball.Pos.XY ?? view.Self.Pos)
            : new(IntentKind.HoldPosition, view.Self.Pos);
    }

    [Fact]
    public void CautionChangesCommitmentOnlyAfterYellowAndCanBeDisabled()
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        int Duels(bool caution, double factor)
        {
            var config = new MatchConfig(MaxReactionSeconds: 0,
                Duels: new DuelConfig(Reach: 200, AttemptRate: 1, MaxFoul: 0, CautionedAttemptFactor: factor),
                Perception: new PerceptionConfig(FieldOfViewRadians: 2 * Math.PI, SightRange: 200),
                Instructions: InstructionConfig.Disabled);
            var match = Match.Create(config, tactic, tactic, 42, (_, _) => new AwayPressBrain());
            if (caution)
            {
                foreach (var player in match.GetSnapshot().Players.Where(p => p.Team == Side.Away))
                    match.ReportIncident(Side.Away, player.Id, Vec2.Zero,
                        new RefereeIncident(ContactAction.Charge, Conduct.Reckless, match.Status == MatchStatus.Running, true,
                            TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true));
                for (int tick = 0; tick < 31; tick++) match.Step();
            }
            var keeper = match.GetSnapshot().Players.Single(p => p.Team == Side.Home && p.IsKeeper);
            match.SetBall(new Vec3(keeper.Pos.X, keeper.Pos.Y, 0), Vec3.Zero);
            for (int tick = 0; tick < 30; tick++) match.Step();
            return match.Events.Events.Count(e => e.Type == EventType.Duel);
        }
        Assert.Equal(0, Duels(true, 0));
        Assert.True(Duels(true, 1) > 0);
        Assert.True(Duels(false, 0) > 0);
        Assert.Throws<ArgumentException>(() => new DuelConfig(CautionedAttemptFactor: -.1).Validate());
        Assert.Throws<ArgumentException>(() => new DuelConfig(CautionedAttemptFactor: double.NaN).Validate());
    }

    private sealed class ClearBrain : IPlayerBrain
    {
        public Intent Decide(Perception view) => view.HasBall
            ? new(IntentKind.Clear, new Vec2(27.5, 28))
            : new(IntentKind.HoldPosition, view.Self.Pos);
    }

    [Fact]
    public void MatchOnlyCompensatesDragWhenExplicitlyEnabledForStoppingClearances()
    {
        Assert.False(new PassConfig().ClearCompensatesDrag);
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        BallState FirstKick(bool compensate, bool stop)
        {
            var match = Match.Create(new MatchConfig(Passing: new PassConfig(Skill: 1,
                ClearCompensatesDrag: compensate, ClearStopsAtTarget: stop), Duels: new DuelConfig(AttemptRate: 0),
                Instructions: InstructionConfig.Disabled), tactic, tactic, 42, (_, _) => new ClearBrain());
            for (int tick = 0; tick < 100 && !match.Events.Events.Any(e => e.Type == EventType.Pass); tick++) match.Step();
            Assert.Contains(match.Events.Events, e => e.Type == EventType.Pass && e.Outcome == "clear");
            return match.Ball;
        }
        Assert.True(FirstKick(true, true).Vel.Length > FirstKick(false, true).Vel.Length);
        Assert.Equal(FirstKick(false, false), FirstKick(true, false));
        var loaded = MatchConfigJson.Parse("{\"Passing\":{\"ClearCompensatesDrag\":true}}");
        Assert.True(loaded.PassSettings.ClearCompensatesDrag);
    }

    private sealed class StillBrain : IPlayerBrain
    {
        public Intent Decide(Perception view) => new(IntentKind.HoldPosition, view.Self.Pos);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void InFieldClearanceCanRollOutThroughExecutionErrorAndAwardsOpponentThrow(int side)
    {
        var from = new Vec2(-30, side * 25);
        var target = new Vec2(27.5, side * 28);
        var passing = new PassConfig();
        var physics = new BallConfig();
        Vec3? stray = null;
        int normalExits = 0, clearExits = 0;
        for (ulong seed = 1; seed <= 64; seed++)
        {
            foreach (bool clearance in new[] { false, true })
            {
                var velocity = PassExecution.Velocity(from, target, 3, passing, new Rng(seed), physics.RollDeceleration,
                    clearance ? physics : null);
                var ball = new BallState(new Vec3(from.X, from.Y, 0), velocity);
                for (int tick = 0; tick < 300; tick++)
                {
                    var next = BallPhysics.Step(ball, physics, .1);
                    if (PitchRules.Evaluate(ball, next, Side.Home) is { Kind: PitchExitKind.ThrowIn })
                    {
                        if (clearance) { clearExits++; stray ??= velocity; } else normalExits++;
                        break;
                    }
                    ball = next;
                    if (ball.Vel == Vec3.Zero) break;
                }
            }
        }
        Assert.Equal(0, normalExits);
        Assert.True(clearExits > 0);
        Assert.NotNull(stray);
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var match = Match.Create(new MatchConfig(Duels: new DuelConfig(AttemptRate: 0),
            Control: new BallControlConfig(.001, .3, 18), InterceptControl: new BallControlConfig(.001, .3, 9),
            Instructions: InstructionConfig.Disabled), tactic, tactic, 42, (_, _) => new StillBrain());
        match.SetBall(new Vec3(from.X, from.Y, 0), stray.Value);
        for (int i = 0; i < 300 && match.Status == MatchStatus.Running; i++) match.Step();
        Assert.Equal(MatchStatus.Restart, match.Status);
        var exit = Assert.Single(match.Events.Events.Where(e => e.Type == EventType.Out));
        Assert.Equal("ThrowIn", exit.Outcome);
        Assert.Equal(Side.Home, exit.Team);
        var awarded = Assert.Single(match.Events.Events.Where(e => e.Type == EventType.RestartAwarded));
        Assert.Equal("ThrowIn", awarded.Outcome);
        Assert.Equal(Side.Away, awarded.Team);
        Assert.Equal(side * Pitch.HalfWidth, match.Ball.Pos.Y, 8);
        for (int i = 0; i < 32; i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartTaken && e.Team == Side.Away);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Pass && e.Team == Side.Away && e.Outcome == "restart ThrowIn");
    }

    [Fact]
    public void DragCompensationChangesSpeedWithoutChangingDirectionOrConsumingExtraDraws()
    {
        var passing = new PassConfig();
        var ordinaryRng = new Rng(42);
        var clearRng = new Rng(42);
        var from = new Vec2(-30, 25);
        var target = new Vec2(27.5, 28);
        var ordinary = PassExecution.Velocity(from, target, 3, passing, ordinaryRng, .98);
        var clear = PassExecution.Velocity(from, target, 3, passing, clearRng, .98, new BallConfig());
        Assert.True(clear.Length > ordinary.Length);
        Assert.Equal(Math.Atan2(ordinary.Y, ordinary.X), Math.Atan2(clear.Y, clear.X), 10);
        Assert.Equal(ordinaryRng.NextDouble(), clearRng.NextDouble());
        Assert.Throws<ArgumentException>(() => BallPhysics.SpeedForRollingDistance(double.NaN, new BallConfig()));
        Assert.Throws<ArgumentException>(() => BallPhysics.SpeedForRollingDistance(-1, new BallConfig()));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    public void InverseRollingSpeedActuallyStopsNearTheRequestedDistance(double distance)
    {
        foreach (double drag in new[] { 0.0, .22 })
        {
            var physics = new BallConfig(DragCoefficient: drag);
            var ball = new BallState(Vec3.Zero, new Vec3(BallPhysics.SpeedForRollingDistance(distance, physics), 0, 0));
            for (int tick = 0; tick < 1000 && ball.Vel != Vec3.Zero; tick++) ball = BallPhysics.Step(ball, physics, .1);
            Assert.Equal(Vec3.Zero, ball.Vel);
            Assert.InRange(ball.Pos.X, distance - .3, distance + .3);
        }
    }
}
