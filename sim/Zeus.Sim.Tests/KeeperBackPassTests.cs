using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class KeeperBackPassTests
{
    private static PlayerSnapshot Player(Side team, string id, double x, double y = 0) =>
        new(team, id, new Vec2(x, y), Vec2.Zero, 0);

    private static Perception View(params PlayerSnapshot[] players) => new(0,
        Player(Side.Home, "RC", -30), GamePhase.Opbouw, true, new Vec2(-30, 0),
        players, new BallSnapshot(new Vec3(-30, 0, 0), Vec3.Zero), null, HeldBallSeconds: 5);

    [Fact]
    public void CarrierKnowsOnlyHisOwnActiveKeeperWithinAwarenessDistance()
    {
        var self = Player(Side.Home, "RC", 0);
        var keeper = Player(Side.Home, "replacement", -40) with { IsGoalkeeper = true };
        var world = new Snapshot(0, 0, new[] { self, keeper, Player(Side.Away, "GK", -40),
            Player(Side.Home, "LC", -40) }, new BallSnapshot(Vec3.Zero, Vec3.Zero));
        var config = new PerceptionConfig(AllRoundMeters: 0, KeeperAwarenessMeters: 40);
        Perception Build(bool carrier, PerceptionConfig c) =>
            PerceptionBuilder.Build(world, self, GamePhase.Opbouw, carrier, c, 0.1);
        Assert.Equal(keeper, Assert.Single(Build(true, config).VisiblePlayers));
        Assert.Empty(Build(false, config).VisiblePlayers);
        Assert.Empty(Build(true, config with { KeeperAwarenessMeters = 39.99 }).VisiblePlayers);
        Assert.Empty(Build(true, config with { KeeperAwarenessMeters = 0 }).VisiblePlayers);
        world = world with { Players = new[] { self, keeper with { SentOff = true } } };
        Assert.Empty(Build(true, config).VisiblePlayers);
    }

    [Fact]
    public void PressedCarrierUsesKeeperInsteadOfWeakSidePassOrClearance()
    {
        var view = View(Player(Side.Home, "GK", -50), Player(Side.Home, "side", -30, 15),
            Player(Side.Away, "press", -28));
        Assert.Equal("GK", PassChoice.Choose(view, new PassConfig())!.ReceiverId);
        var intent = new PassingBrain(new PassConfig()).Decide(view);
        Assert.Equal(IntentKind.Pass, intent.Kind);
        Assert.Equal("GK", intent.ReceiverId);
        Assert.Equal("GK", new PassingBrain(new PassConfig()).Decide(view with { HeldBallSeconds = 0 }).ReceiverId);
    }

    [Fact]
    public void KeeperIsNeverAnOutletWithoutPressureEvenWhenForced()
    {
        var view = View(Player(Side.Home, "GK", -50), Player(Side.Away, "distant", -23.99));
        Assert.Null(PassChoice.Choose(view, new PassConfig()));
        Assert.Null(PassChoice.Choose(view, new PassConfig(), force: true));
        Assert.Equal(IntentKind.Scan, new PassingBrain(new PassConfig()).Decide(view).Kind);
    }

    [Fact]
    public void PressureBoundaryIsInclusiveAndTheKeeperMustBeWithinPassRange()
    {
        var view = View(Player(Side.Home, "GK", -50), Player(Side.Away, "press", -28));
        Assert.Equal("GK", PassChoice.Choose(view, new PassConfig())!.ReceiverId);
        Assert.Null(PassChoice.Choose(view with { VisiblePlayers = new[] {
            Player(Side.Home, "GK", -50), Player(Side.Away, "press", -27.99) } }, new PassConfig(), force: true));
        Assert.Null(PassChoice.Choose(view, new PassConfig(MaxDistance: 19), force: true));
    }

    [Fact]
    public void GoodForwardPassAlwaysBeatsEvenAnEnormousKeeperScore()
    {
        var view = View(Player(Side.Home, "GK", -50), Player(Side.Home, "forward", -5, 10),
            Player(Side.Away, "press", -31), Player(Side.Away, "line", 0, 20));
        Assert.Equal("forward", PassChoice.Choose(view, new PassConfig(BackPassToKeeperScore: 100))!.ReceiverId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OwnHalfAndBackwardDirectionFollowTheAttackingEnd(bool positive)
    {
        double dir = positive ? 1 : -1;
        var view = View(Player(Side.Home, "GK", -50 * dir), Player(Side.Away, "press", -28 * dir))
            with { Self = Player(Side.Home, "RC", -30 * dir), AttackPositiveX = positive };
        Assert.Equal("GK", PassChoice.Choose(view, new PassConfig())!.ReceiverId);
        var advanced = view with { Self = Player(Side.Home, "RC", 5 * dir), VisiblePlayers = new[] {
            Player(Side.Home, "GK", -20 * dir), Player(Side.Away, "press", 7 * dir) } };
        Assert.Null(PassChoice.Choose(advanced, new PassConfig(), force: true));
        Assert.Null(PassChoice.Choose(view with { Self = view.Self with { IsGoalkeeper = true } }, new PassConfig()));
    }

    [Fact]
    public void SwitchingOffTheOutletRestoresOrdinaryBackwardScoring()
    {
        var view = View(Player(Side.Home, "GK", -50), Player(Side.Home, "side", -30, 15),
            Player(Side.Away, "press", -28));
        Assert.Equal("side", PassChoice.Choose(view, new PassConfig(BackPassToKeeperScore: 0))!.ReceiverId);
        Assert.Equal("side", PassChoice.Choose(view, new PassConfig(BackPassPressureMeters: 0))!.ReceiverId);
        var forced = PassChoice.Choose(View(Player(Side.Home, "GK", -50)),
            new PassConfig(BackPassToKeeperScore: 0), force: true);
        Assert.Equal(-0.5, forced!.Score, 8);
    }

    [Fact]
    public void KeeperMeetsThePassAndUsesExistingPassingAfterControl()
    {
        var keeper = Player(Side.Home, "GK", -50);
        var view = View() with { Self = keeper, HasBall = false, ReceiveTarget = new Vec2(-48, 1),
            KnownBall = new BallObservation(0, new BallSnapshot(new Vec3(-30, 0, 0), Vec3.Zero)) };
        var brain = new PassingBrain(new PassConfig());
        var receive = brain.Decide(view);
        Assert.Equal(IntentKind.MoveToPosition, receive.Kind);
        Assert.True(receive.Target.X > view.ReceiveTarget!.Value.X);
        Assert.Equal(3, (receive.Target - view.ReceiveTarget.Value).Length, 8);
        Assert.Equal(view.ReceiveTarget, new PassingBrain(new PassConfig(KeeperReceiveAdvanceMeters: 0)).Decide(view).Target);
        Assert.Equal(IntentKind.KeeperGuard, new PassingBrain(new PassConfig(BackPassToKeeperScore: 0)).Decide(view).Kind);
        Assert.Equal(IntentKind.KeeperGuard, new PassingBrain(new PassConfig(BackPassPressureMeters: 0)).Decide(view).Kind);
        var release = brain.Decide(view with { HasBall = true, ReceiveTarget = null,
            VisiblePlayers = new[] { Player(Side.Home, "RC", -30) } });
        Assert.Equal(IntentKind.Pass, release.Kind);
        Assert.Equal("RC", release.ReceiverId);
    }

    [Fact]
    public void KeeperMeetsTheObservedFlightRatherThanTheIntendedLine()
    {
        var ball = new BallSnapshot(new Vec3(-30, 3, 0), new Vec3(-12, 0, 0));
        var view = View() with { Self = Player(Side.Home, "GK", -50), HasBall = false,
            ReceiveTarget = new Vec2(-50, 0), VisibleBall = ball, KnownBall = new BallObservation(0, ball) };
        var intent = new PassingBrain(new PassConfig()).Decide(view);
        Assert.Equal(IntentKind.MoveToPosition, intent.Kind);
        Assert.Equal(3, intent.Target.Y, 8);
        Assert.True(intent.Target.X > -50);
        Assert.Equal(ball.Pos.XY, intent.FacingTarget);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidSettingsAreRejected(double value)
    {
        Assert.Throws<ArgumentException>(() => new PerceptionConfig(KeeperAwarenessMeters: value).Validate());
        Assert.Throws<ArgumentException>(() => new PassConfig(BackPassToKeeperScore: value).Validate());
        Assert.Throws<ArgumentException>(() => new PassConfig(BackPassPressureMeters: value).Validate());
        Assert.Throws<ArgumentException>(() => new PassConfig(BackPassForwardMinScore: value).Validate());
        Assert.Throws<ArgumentException>(() => new PassConfig(KeeperReceiveAdvanceMeters: value).Validate());
    }

    [Fact]
    public void DefaultsAndJsonOverridesKeepTheOutletConfigurable()
    {
        Assert.Equal(60, new PerceptionConfig().KeeperAwarenessMeters);
        Assert.Equal(2.2, new PassConfig().BackPassToKeeperScore);
        Assert.Equal(2, new PassConfig().BackPassPressureMeters);
        Assert.Equal(2.0, new PassConfig().BackPassForwardMinScore);
        Assert.Equal(3, new PassConfig().KeeperReceiveAdvanceMeters);
        var config = MatchConfigJson.Parse("{\"Perception\":{\"KeeperAwarenessMeters\":0},\"Passing\":{\"BackPassToKeeperScore\":0}}");
        config.Sensing.Validate();
        config.PassSettings.Validate();
        Assert.Equal(0, config.Sensing.KeeperAwarenessMeters);
        Assert.Equal(0, config.PassSettings.BackPassToKeeperScore);
    }

    private sealed class FixtureBrain : IPlayerBrain
    {
        public Intent Decide(Perception view)
        {
            if (view.HasBall && view.Self.Team == Side.Home && view.Self.Id == "RC")
            {
                var keeper = view.VisiblePlayers.Single(p => p.Team == Side.Home && p.IsKeeper);
                return new Intent(IntentKind.Pass, keeper.Pos, keeper.Id);
            }
            return new Intent(IntentKind.HoldPosition, view.Self.Pos);
        }
    }

    [Theory]
    [InlineData(Side.Home, false)]
    [InlineData(Side.Away, true)]
    public void OwnTeamKickOffersOnlyFootControlWhileOpposingKeeperCanUseHands(Side keeperTeam, bool canUseHands)
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0,
            Perception: new PerceptionConfig(FieldOfViewRadians: 2 * Math.PI),
            Passing: new PassConfig(Skill: 1)), tactic, tactic, 4, (_, _) => new FixtureBrain());
        var passer = match.GetSnapshot().Players.Single(p => p.Team == Side.Home && p.Id == "RC");
        match.SetBall(new Vec3(passer.Pos.X, passer.Pos.Y, 0), Vec3.Zero);
        for (int i = 0; i < 100 && !match.Events.Events.Any(e => e.Type == EventType.Pass && e.Outcome.StartsWith("to GK ")); i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Pass && e.Team == Side.Home && e.Outcome.StartsWith("to GK "));
        var keeper = match.GetSnapshot().Players.Single(p => p.Team == keeperTeam && p.Id == "GK");
        // Preserve the actual kick's last-touch team, but probe contact at hand height and then at the feet.
        match.SetBall(new Vec3(keeper.Pos.X, keeper.Pos.Y, 1), Vec3.Zero);
        match.Step();
        Assert.Equal(canUseHands ? "GK" : null, match.CarrierId);
        if (canUseHands) Assert.Equal(keeperTeam, match.Possession);
        match.SetBall(new Vec3(keeper.Pos.X, keeper.Pos.Y, 0), Vec3.Zero);
        match.Step();
        Assert.Equal("GK", match.CarrierId);
        Assert.Equal(keeperTeam, match.Possession);
    }

    [Fact]
    public void StandardBrainsFireTheAwayKeeperTriggerAndKeepersPassAfterReceiving()
    {
        int fired = 0, released = 0;
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var match = Match.Create(new MatchConfig(), TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
                TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), seed);
            for (int i = 0; i < 150000 && match.Status != MatchStatus.Finished; i++) match.Step();
            Assert.Equal(MatchStatus.Finished, match.Status);
            var events = match.Events.Events.ToArray();
            fired += events.Count(e => e.Type == EventType.TriggerFired && e.Team == Side.Away
                && e.Outcome.EndsWith(TriggerRules.BallToKeeper));
            for (int i = 0; i < events.Length; i++)
            {
                var pass = events[i];
                if (pass.Type != EventType.Pass || !pass.Outcome.StartsWith("to GK ")) continue;
                var contact = events.Skip(i + 1).FirstOrDefault(e => e.Type is EventType.BallControl or EventType.Interception
                    or EventType.Out or EventType.Goal or EventType.Pass or EventType.Shot);
                if (contact is null || contact.Type != EventType.BallControl || contact.Team != pass.Team || contact.PlayerId != "GK") continue;
                var release = events.FirstOrDefault(e => e.Tick > contact.Tick && e.Type == EventType.Pass
                    && e.Team == contact.Team && e.PlayerId == "GK");
                if (release is not null && release.Tick - contact.Tick < 150) released++;
            }
        }
        Assert.True(fired > 0, $"keeper triggers: {fired}");
        Assert.True(released > 0, $"received backpasses followed by keeper passes: {released}");
    }

    [Fact]
    public void DisabledBackpassesRetainThePreT3FullMatchHashes()
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var match = Match.Create(PreRound5Defaults.Apply(PreRound4Defaults.Apply(new MatchConfig(Perception: new PerceptionConfig(KeeperAwarenessMeters: 0),
            Passing: new PassConfig(BackPassToKeeperScore: 0, KeeperReceiveAdvanceMeters: 0, OffsideMargin: 0.5)))), tactic, tactic, 7);
        for (int i = 0; i < 150000 && match.Status != MatchStatus.Finished; i++) match.Step();
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal("1eb1d8b5a411432d7f21e0dd95bbe23dc6562d1e63cebce594e69ce186608b39", match.EventHash());
        Assert.Equal("77c617a96a40cd5fff4575716511c10fc7f262b4b5e22e7e6e990fc457ae46f6", match.SnapshotHash());
    }
}
