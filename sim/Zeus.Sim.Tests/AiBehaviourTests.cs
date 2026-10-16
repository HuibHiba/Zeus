using System.Text.Json.Nodes;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class AiBehaviourTests
{
    private static Perception Chance(int direction = 1, double distance = 10, params PlayerSnapshot[] opponents)
    {
        var pos = new Vec2(direction * (Pitch.HalfLength - distance), 0);
        return new Perception(0, new PlayerSnapshot(Side.Home, "ST", pos, Vec2.Zero, 0),
            GamePhase.Aanval, true, Vec2.Zero, opponents,
            new BallSnapshot(new Vec3(pos.X, pos.Y, 0), Vec3.Zero), null,
            AttackPositiveX: direction > 0, HeldBallSeconds: .5);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ClearHighXgChanceIsShotDuringHold(int direction)
    {
        var view = Chance(direction);
        Assert.True(ShotModel.GoalProbability(10, 0) >= .15);
        Assert.Equal(IntentKind.Shoot, new PassingBrain(new PassConfig()).Decide(view).Kind);
        Assert.NotEqual(IntentKind.Shoot, new PassingBrain(new PassConfig(EarlyShotXg: null)).Decide(view).Kind);
    }

    [Fact]
    public void KeeperOnTheGoalLineDoesNotCountAsAnOutfieldBlocker()
    {
        var keeper = new PlayerSnapshot(Side.Away, "GK", new Vec2(Pitch.HalfLength, 0), Vec2.Zero, 0);
        Assert.Equal(IntentKind.Shoot, new PassingBrain(new PassConfig()).Decide(Chance(1, 10, keeper)).Kind);
    }

    [Theory]
    [InlineData(45, 0)] // lane
    [InlineData(41.5, 2.1)] // pressure outside the lane
    public void VisibleBlockerPreventsEarlyShotAndCarrierDoesNotRetreat(double x, double y)
    {
        var view = Chance(1, 10, new PlayerSnapshot(Side.Away, "CB", new Vec2(x, y), Vec2.Zero, 0));
        var intent = new PassingBrain(new PassConfig()).Decide(view);
        Assert.Equal(IntentKind.HoldPosition, intent.Kind);
        Assert.Equal(view.Self.Pos, intent.Target);
        Assert.Equal(IntentKind.MoveToPosition, new PassingBrain(new PassConfig(EarlyShotXg: null)).Decide(view).Kind);
    }

    [Fact]
    public void LowerQualityChanceStillAdvancesDuringHoldAndConfigRoundTrips()
    {
        Assert.InRange(ShotModel.GoalProbability(17, 0), .08, .149999);
        Assert.Equal(IntentKind.MoveToPosition, new PassingBrain(new PassConfig()).Decide(Chance(distance: 17)).Kind);
        Assert.Null(MatchConfigJson.Parse("{\"Passing\":{\"EarlyShotXg\":null}}").PassSettings.EarlyShotXg);
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, -.01, 1.01 })
            Assert.Throws<ArgumentException>(() => new PassConfig(EarlyShotXg: value).Validate());
    }

    [Fact]
    public void FreeCarrierContinuesAfterHoldInsteadOfPlayingBackwards()
    {
        var view = Chance(distance: 22) with { HeldBallSeconds = 3 };
        view = view with { VisiblePlayers = new[] {
            new PlayerSnapshot(Side.Home, "CM", view.Self.Pos - new Vec2(10, 0), Vec2.Zero, 0) } };
        var intent = new PassingBrain(new PassConfig(CarryAfterHold: true)).Decide(view);
        Assert.Equal(IntentKind.MoveToPosition, intent.Kind);
        Assert.True(intent.Target.X > view.Self.Pos.X);
        Assert.Equal(IntentKind.Pass, new PassingBrain(new PassConfig(EarlyShotXg: null)).Decide(view).Kind);
        Assert.Equal(IntentKind.Pass, new PassingBrain(new PassConfig(CarryAfterHold: false)).Decide(view).Kind);
    }

    [Fact]
    public void CarryDoesNotOverrideGoodForwardPassOrRunIntoVisiblePressure()
    {
        var view = Chance(distance: 22) with { HeldBallSeconds = 3 };
        var mate = new PlayerSnapshot(Side.Home, "ST2", view.Self.Pos + new Vec2(15, 0), Vec2.Zero, 0);
        Assert.Equal(IntentKind.Pass, new PassingBrain(new PassConfig(CarryAfterHold: true)).Decide(view with { VisiblePlayers = new[] { mate } }).Kind);
        var opponent = new PlayerSnapshot(Side.Away, "CB", view.Self.Pos + new Vec2(4, 0), Vec2.Zero, 0);
        Assert.NotEqual(IntentKind.MoveToPosition, new PassingBrain(new PassConfig(CarryAfterHold: true)).Decide(view with { VisiblePlayers = new[] { opponent } }).Kind);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ShootableClearChanceBeatsBackwardOutletDuringHold(int direction)
    {
        var view = Chance(direction, 17);
        var mate = new PlayerSnapshot(Side.Home, "CM", view.Self.Pos - new Vec2(direction * 8, 0), Vec2.Zero, 0);
        view = view with { VisiblePlayers = new[] { mate } };
        var enabled = new PassConfig(PreferShotToBackPass: true);
        Assert.Equal(IntentKind.Shoot, new PassingBrain(enabled).Decide(view).Kind);
        Assert.Equal(IntentKind.MoveToPosition, new PassingBrain(enabled with { PreferShotToBackPass = false }).Decide(view).Kind);
        Assert.Equal(IntentKind.MoveToPosition, new PassingBrain(enabled with { EarlyShotXg = null }).Decide(view).Kind);
    }

    [Fact]
    public void BackwardOutletGuardRespectsBlockersAndShootingRange()
    {
        foreach (double distance in new[] { 17.0, 22.0 })
        {
            var view = Chance(distance: distance);
            var mate = new PlayerSnapshot(Side.Home, "CM", view.Self.Pos - new Vec2(8, 0), Vec2.Zero, 0);
            var blocker = new PlayerSnapshot(Side.Away, "CB", view.Self.Pos + new Vec2(3, 0), Vec2.Zero, 0);
            view = view with { VisiblePlayers = distance == 17 ? new[] { mate, blocker } : new[] { mate } };
            Assert.NotEqual(IntentKind.Shoot, new PassingBrain(new PassConfig(PreferShotToBackPass: true)).Decide(view).Kind);
        }
    }

    [Theory]
    [InlineData(1.2, false)]
    [InlineData(1.35, true)]
    public void OptionalCloseShootingHoldOnlyReleasesClearChance(double held, bool shoot)
    {
        var view = Chance(distance: 17) with { HeldBallSeconds = held };
        var config = new PassConfig(CloseShotHoldSeconds: 1.35);
        Assert.Equal(shoot, new PassingBrain(config).Decide(view).Kind == IntentKind.Shoot);
        Assert.NotEqual(IntentKind.Shoot, new PassingBrain(config with { CloseShotHoldSeconds = null }).Decide(view).Kind);
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -.1 })
            Assert.Throws<ArgumentException>(() => new PassConfig(CloseShotHoldSeconds: invalid).Validate());
        Assert.Equal(1.35, MatchConfigJson.Parse("{\"Passing\":{\"CloseShotHoldSeconds\":1.35}}").PassSettings.CloseShotHoldSeconds);
    }

    private static double BackPassShotDistance()
    {
        // First distance on the centre line where the raw chance has fallen below ShotConfig.MinGoalProbability (0.08) but is still >= 0.05.
        for (double distance = 17; distance < 30; distance += .25)
            if (ShotModel.GoalProbability(distance, 0) < .075) return distance;
        throw new InvalidOperationException("no distance found");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void FreeCarrierShootsInsteadOfBackPassBelowTheOrdinaryThresholdAfterTheHold(int direction)
    {
        double distance = BackPassShotDistance();
        Assert.InRange(ShotModel.GoalProbability(distance, 0), .05, .0799);
        var view = Chance(direction, distance) with { HeldBallSeconds = 3 };
        var mate = new PlayerSnapshot(Side.Home, "CM", view.Self.Pos - new Vec2(direction * 8, 0), Vec2.Zero, 0);
        view = view with { VisiblePlayers = new[] { mate } };
        var config = new PassConfig();
        Assert.True(config.PreferShotToBackPass);
        Assert.Equal(.05, config.BackPassShotXg);
        Assert.Equal(IntentKind.Shoot, new PassingBrain(config).Decide(view).Kind);
        // Each switch restores the previous behaviour: the backward outlet is played.
        Assert.Equal(IntentKind.Pass, new PassingBrain(config with { BackPassShotXg = null }).Decide(view).Kind);
        Assert.Equal(IntentKind.Pass, new PassingBrain(config with { PreferShotToBackPass = false }).Decide(view).Kind);
        Assert.Equal(IntentKind.Pass, new PassingBrain(config with { EarlyShotXg = null }).Decide(view).Kind);
        // Not during the hold, and never through a blocker.
        Assert.NotEqual(IntentKind.Shoot, new PassingBrain(config).Decide(view with { HeldBallSeconds = 1 }).Kind);
        var blocker = new PlayerSnapshot(Side.Away, "CB", view.Self.Pos + new Vec2(direction * 4, 0), Vec2.Zero, 0);
        Assert.NotEqual(IntentKind.Shoot, new PassingBrain(config).Decide(view with { VisiblePlayers = new[] { mate, blocker } }).Kind);
        // Below the new floor the ball is still played back.
        Assert.Equal(IntentKind.Pass, new PassingBrain(config with { BackPassShotXg = .2 }).Decide(view).Kind);
    }

    [Fact]
    public void BackPassShotXgRoundTripsAndIsValidated()
    {
        Assert.Null(MatchConfigJson.Parse("{\"Passing\":{\"BackPassShotXg\":null}}").PassSettings.BackPassShotXg);
        Assert.Equal(.06, MatchConfigJson.Parse("{\"Passing\":{\"BackPassShotXg\":0.06}}").PassSettings.BackPassShotXg);
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -.01, 1.01 })
            Assert.Throws<ArgumentException>(() => new PassConfig(BackPassShotXg: invalid).Validate());
    }

    [Theory]
    [InlineData("hoge_druk_433")]
    [InlineData("positiespel_4231")]
    [InlineData("counter_532")]
    [InlineData("laag_blok_442")]
    [InlineData("man_om_man_433")]
    [InlineData("testcase_a_433")]
    public void ExampleTacticsFollowTheBallInBuildUpAndAttackExceptTheKeeper(string name)
    {
        var root = JsonNode.Parse(File.ReadAllText(TacticLoaderTests.TacticPath(name)))!;
        JsonNode Resolve(string phase)
        {
            var block = root["fases"]![phase]!;
            return block["gelijk_aan"] is { } alias ? Resolve(alias.GetValue<string>()) : block;
        }
        foreach (string phase in new[] { "opbouw", "aanval" })
            foreach (var player in Resolve(phase)["spelers"]!.AsArray())
            {
                var follow = player!["volg_bal"]?.AsArray();
                if (player["id"]!.GetValue<string>() == "GK") { Assert.Null(follow); continue; }
                Assert.NotNull(follow);
                foreach (var component in follow!) Assert.InRange(component!.GetValue<double>(), .1, .5);
            }
    }

    [Theory]
    // Share of outfield players standing still (< 0.3 m/s) in opbouw and aanval, first 12000 ticks; before the volg_bal data
    // these were roughly 85% (laag blok), 55% and 55%. The limits sit a few points above the measured values.
    [InlineData("laag_blok_442", 1UL, .62)]
    [InlineData("laag_blok_442", 2UL, .62)]
    [InlineData("positiespel_4231", 1UL, .45)]
    [InlineData("counter_532", 1UL, .55)]
    public void ShippedTacticLimitsAttackingStillness(string name, ulong seed, double limit)
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath(name));
        var match = Match.Create(new MatchConfig(), tactic, tactic, seed);
        long still = 0, total = 0;
        int sinceRestart = 0;
        for (int tick = 0; tick < 12000; tick++)
        {
            match.Step();
            if (match.Status != MatchStatus.Running) { sinceRestart = 0; continue; }
            if (++sinceRestart < 50 || match.Tick < 100) continue;
            foreach (var p in match.GetSnapshot().Players)
            {
                if (p.SentOff || p.IsKeeper || match.PhaseOf(p.Team) is not (GamePhase.Opbouw or GamePhase.Aanval)) continue;
                total++;
                if (p.Vel.Length < .3) still++;
            }
        }
        Assert.True(total > 1000);
        Assert.True((double)still / total < limit, $"{name} seed {seed}: {still}/{total} = {(double)still / total:P1}");
    }

    [Theory]
    [InlineData("laag_blok_442", "man_om_man_433", 42UL)]
    [InlineData("laag_blok_442", "laag_blok_442", 7UL)]
    public void DisabledFixPreservesPreviousGoldenBits(string home, string away, ulong seed)
    {
        var match = Match.Create(new MatchConfig(Passing: new PassConfig(EarlyShotXg: null, PreferShotToBackPass: false, BackPassShotXg: null)),
            HistoricalTactics.Load(home), HistoricalTactics.Load(away), seed);
        for (int tick = 0; tick < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); tick++) match.Step();
        Assert.Equal(MatchStatus.Finished, match.Status);
        string expected = home == away
            ? "5301302ea9e689fd0b15e6b18f3006045dfed0e06e7d87f5998133a5894779a0|e8820a48ede19e1d7829959ee5c3234f43a185ee378b912e02e5ce0ef38b1824"
            : "9feaaf354e4bd80e4ad96e96771578c389eaf3b4ffa9dd12ac9f6d335a830ab5|558d71fd316efb8af9d8acf2885d01086dfbcd9c3b152053cd5c1bd18d199d61";
        Assert.Equal(expected, match.EventHash() + "|" + match.SnapshotHash());
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void ExplicitBallFollowingLimitsAttackingStillness(ulong seed)
    {
        // The movement effect is useful, but the example-data change was rejected on shot calibration.
        // Keep the proposed setup explicit; this is not a claim that the shipped tactics meet the stillness goal.
        var root = JsonNode.Parse(File.ReadAllText(TacticLoaderTests.TacticPath("laag_blok_442")))!;
        foreach (var player in root["fases"]!["opbouw"]!["spelers"]!.AsArray())
            if (player!["id"]!.GetValue<string>() != "GK")
                player["volg_bal"] = new JsonArray(.2, .4);
        var tactic = TacticLoader.Parse(root.ToJsonString());
        var match = Match.Create(new MatchConfig(), tactic, tactic, seed);
        long still = 0, total = 0;
        int sinceRestart = 0;
        for (int tick = 0; tick < 12000; tick++)
        {
            match.Step();
            if (match.Status != MatchStatus.Running) { sinceRestart = 0; continue; }
            if (++sinceRestart < 50 || match.Tick < 100) continue;
            foreach (var p in match.GetSnapshot().Players)
            {
                if (p.SentOff || p.IsKeeper || match.PhaseOf(p.Team) is not (GamePhase.Opbouw or GamePhase.Aanval)) continue;
                total++;
                if (p.Vel.Length < .3) still++;
            }
        }
        Assert.True(total > 1000);
        Assert.True((double)still / total < .55, $"seed {seed}: {still}/{total} = {(double)still / total:P1}");
    }
}
