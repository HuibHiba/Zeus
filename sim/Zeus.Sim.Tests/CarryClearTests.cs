using System.Text.Json;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

/// <summary>Carry-forward, clearance, all-round awareness and keeper-parry settings (docs/kalibratie.md).</summary>
public class CarryClearTests
{
    private static PlayerSnapshot Player(Side team, string id, double x, double y, double heading = 0, bool keeper = false) =>
        new(team, id, new Vec2(x, y), Vec2.Zero, heading, IsGoalkeeper: keeper);

    private static Perception View(PlayerSnapshot self, double held, params PlayerSnapshot[] others) => new(0, self,
        GamePhase.Opbouw, true, new Vec2(self.Pos.X, self.Pos.Y), others, new BallSnapshot(Vec3.Zero, Vec3.Zero), null,
        HeldBallSeconds: held, AttackPositiveX: true);

    [Fact]
    public void CarrierWithSpaceAheadRunsTowardsGoalButNotWithAnOpponentInTheCone()
    {
        var brain = new PassingBrain(new PassConfig());
        var self = Player(Side.Home, "m", 10, 0);
        var free = brain.Decide(View(self, 0.5, Player(Side.Away, "behind", 5, 0)));
        Assert.Equal(IntentKind.MoveToPosition, free.Kind);
        Assert.True(free.Target.X > 10);
        var blocked = brain.Decide(View(self, 0.5, Player(Side.Away, "ahead", 15, 1)));
        Assert.Equal(new Vec2(10, 0), blocked.Target); // formation target = own position, no carry
        var off = new PassingBrain(new PassConfig(CarryMaxGoalDistance: 0)).Decide(View(self, 0.5));
        Assert.Equal(new Vec2(10, 0), off.Target);
    }

    [Fact]
    public void PressedCarrierInHisOwnThirdClearsLongAndWide()
    {
        var self = Player(Side.Home, "cb", -40, 5);
        var view = View(self, 0.5, Player(Side.Away, "press", -38, 5));
        var intent = new PassingBrain(new PassConfig()).Decide(view);
        Assert.Equal(IntentKind.Clear, intent.Kind);
        Assert.True(intent.Target.X > 0 && intent.Target.Y > 0);
        // A clean pass option keeps the carrier from clearing; the setting can also be switched off.
        var safe = new PassingBrain(new PassConfig()).Decide(view with { VisiblePlayers = new[] { Player(Side.Away, "press", -38, 5), Player(Side.Home, "mate", -33, -12) } });
        Assert.NotEqual(IntentKind.Clear, safe.Kind);
        Assert.NotEqual(IntentKind.Clear, new PassingBrain(new PassConfig(ClearPressureMeters: 0)).Decide(view).Kind);
        // Not in his own third: no clearance.
        Assert.NotEqual(IntentKind.Clear, new PassingBrain(new PassConfig()).Decide(View(Player(Side.Home, "m", 10, 5), 0.5, Player(Side.Away, "press", 12, 5))).Kind);
    }

    [Fact]
    public void AllRoundAwarenessSeesPlayersBehindOnlyWithinTheRadius()
    {
        var observer = Player(Side.Home, "a", 0, 0, heading: 0);
        var cfg = new PerceptionConfig(AllRoundMeters: 10);
        Assert.False(PerceptionBuilder.InSight(observer, new Vec2(-5, 0), cfg));            // behind, no all-round flag
        Assert.True(PerceptionBuilder.InSight(observer, new Vec2(-5, 0), cfg, allRound: true));
        Assert.False(PerceptionBuilder.InSight(observer, new Vec2(-15, 0), cfg, allRound: true));
        Assert.False(PerceptionBuilder.InSight(observer, new Vec2(-5, 0), new PerceptionConfig(AllRoundMeters: 0), allRound: true)); // switched off
        Assert.Throws<ArgumentException>(() => new PerceptionConfig(AllRoundMeters: 100).Validate());
    }

    [Fact]
    public void ClearanceMatchesAreDeterministicAndEmitClearEvents()
    {
        string home = TacticLoaderTests.TacticPath("laag_blok_442"), away = TacticLoaderTests.TacticPath("man_om_man_433");
        Match Play(ulong seed)
        {
            var m = Match.Create(new MatchConfig(), TacticLoader.LoadFile(home), TacticLoader.LoadFile(away), seed);
            for (int i = 0; i < 12000; i++) m.Step();
            return m;
        }
        Assert.Equal(Play(5).EventHash(), Play(5).EventHash());
        Assert.NotEqual(Play(5).EventHash(), Play(6).EventHash());
    }

    [Fact]
    public void ParriedSavesProduceCornersAndZeroRateNeverDoes()
    {
        string home = TacticLoaderTests.TacticPath("laag_blok_442"), away = TacticLoaderTests.TacticPath("man_om_man_433");
        int Corners(double rate)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var m = Match.Create(new MatchConfig(Shots: new ShotConfig(ParryCornerRate: rate)), TacticLoader.LoadFile(home), TacticLoader.LoadFile(away), seed);
                for (int i = 0; i < 30000; i++) m.Step();
                total += m.Events.Events.Count(e => e.Type == EventType.Out && e.Outcome == nameof(PitchExitKind.Corner));
            }
            return total;
        }
        Assert.True(Corners(1.0) > Corners(0.0));
    }

    [Fact]
    public void MatchConfigRoundTripsThroughJsonWithPartialOverrides()
    {
        var config = JsonSerializer.Deserialize<MatchConfig>("{\"Passing\":{\"CarryMaxGoalDistance\":60},\"Shots\":{\"ParryCornerRate\":0.2}}")!;
        Assert.Equal(60, config.PassSettings.CarryMaxGoalDistance);
        Assert.Equal(0.2, config.ShotSettings.ParryCornerRate);
        Assert.Equal(new PassConfig().RiskWeight, config.PassSettings.RiskWeight); // omitted: keeps the default
        config.PassSettings.Validate();
    }
}
