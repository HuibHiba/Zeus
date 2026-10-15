using Zeus.Sim;
namespace Zeus.Sim.Tests;

/// <summary>Tests for the parameters added during count calibration (docs/kalibratie.md).</summary>
public class CalibrationTests
{
    private static Match Create(MatchConfig config, ulong seed = 42) => Match.Create(config,
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), seed);

    private static Match Run(MatchConfig config, int steps, ulong seed = 42)
    {
        var match = Create(config, seed);
        for (int i = 0; i < steps; i++) match.Step();
        return match;
    }

    private static PlayerSnapshot Player(Side team, string id, double x, double y, bool keeper = false) =>
        new(team, id, new Vec2(x, y), Vec2.Zero, 0, IsGoalkeeper: keeper);

    private static Perception View(PlayerSnapshot self, bool hasBall, params PlayerSnapshot[] others) => new(0, self,
        GamePhase.Opbouw, hasBall, Vec2.Zero, others, new BallSnapshot(Vec3.Zero, Vec3.Zero), null, HeldBallSeconds: 4);

    // ---- Step 1: duel attempt rate ----

    [Fact]
    public void AttemptRateZeroGivesNoDuelsAndOneGivesMoreThanDefault()
    {
        int none = Run(new MatchConfig(Duels: new DuelConfig(AttemptRate: 0)), 6000).Events.Events.Count(e => e.Type == EventType.Duel);
        int normal = Run(new MatchConfig(), 6000).Events.Events.Count(e => e.Type == EventType.Duel);
        int always = Run(new MatchConfig(Duels: new DuelConfig(AttemptRate: 1)), 6000).Events.Events.Count(e => e.Type == EventType.Duel);
        Assert.Equal(0, none);
        Assert.True(always > normal, $"always {always} normal {normal}");
    }

    [Fact]
    public void Round4DefaultsAreValidAndMatchTheDocumentedCalibration()
    {
        var config = new MatchConfig();
        Assert.Equal(0.6, config.InterceptSettings.ControlRadius);
        Assert.Equal(2.4, config.PassSettings.HoldSeconds);
        Assert.Equal(45, config.PassSettings.ClearZoneMeters);
        Assert.Equal(0.08, config.ShotSettings.MinGoalProbability);
        Assert.Equal(0.065, config.DuelSettings.AttemptRate);
        Assert.Equal(0.4, config.DuelSettings.BaseFoul);
        Assert.True(config.InterceptSettings.ControlRadius < config.ControlSettings.ControlRadius);
        config.PassSettings.Validate();
        config.ShotSettings.Validate();
        config.DuelSettings.Validate();
    }

    [Fact]
    public void AttemptRateMustBeAProbability()
    {
        Assert.Throws<ArgumentException>(() => new DuelConfig(AttemptRate: -0.1).Validate());
        Assert.Throws<ArgumentException>(() => new DuelConfig(AttemptRate: 1.1).Validate());
        new DuelConfig(AttemptRate: 1).Validate();
    }

    [Fact]
    public void DefaultDuelMixHasFoulsWinsAndLooseBallsInTheCalibratedOrder()
    {
        // Typical close challenge (0.6 m, 3 m/s, from the side): hand-computed from the default parameters
        // foul 0.415, ball won 0.347, loose ball 0.069, nothing 0.142. A drift of the calibration moves these.
        var c = DuelModel.Chances(0.6, 3, 1.0, 0.6, 0.75, 0);
        Assert.InRange(c.Foul, 0.395, 0.435);
        Assert.InRange(c.WonBall, 0.33, 0.365);
        Assert.InRange(c.LooseBall, 0.055, 0.085);
        Assert.InRange(c.None, 0.12, 0.16);
        Assert.True(c.LooseBall < c.WonBall && c.LooseBall < c.Foul);
        Assert.Equal(1.0, c.Foul + c.WonBall + c.LooseBall + c.None, 12);
    }

    [Fact]
    public void LooseBallVelocityIsAUnitDirectionTimesTheConfiguredSpeed()
    {
        var config = new DuelConfig(LooseBallSpeed: 4);
        // The old code used 3 x the offset: a defender 0.5 m away gave 1.5 m/s and one 1.2 m away 3.6 m/s. Now both give 4 m/s.
        var near = DuelModel.LooseBallVelocity(new Vec2(0.3, 0.4), 0, config);
        var far = DuelModel.LooseBallVelocity(new Vec2(0.72, 0.96), 0, config);
        Assert.Equal(4.0, near.XY.Length, 9);
        Assert.Equal(4.0, far.XY.Length, 9);
        Assert.Equal(0.6 * 4, near.X, 9);
        Assert.Equal(0.8 * 4, near.Y, 9);
        Assert.Equal(near.X, far.X, 9);
        Assert.Equal(near.Y, far.Y, 9);
        Assert.Equal(0, near.Z);
        // No offset: along the carrier's heading.
        var onTop = DuelModel.LooseBallVelocity(Vec2.Zero, Math.PI / 2, config);
        Assert.Equal(0, onTop.X, 9);
        Assert.Equal(4.0, onTop.Y, 9);
        Assert.Throws<ArgumentException>(() => DuelModel.LooseBallVelocity(new Vec2(double.NaN, 0), 0, config));
    }

    [Fact]
    public void LooseBallDuelsHappenInAMatchAndKeepTheBallFinite()
    {
        var match = Create(new MatchConfig(Duels: new DuelConfig(BaseFoul: 0, MaxFoul: 0, SpeedFoul: 0, ApproachFoul: 0,
            IndisciplineFoul: 0, FatigueFoul: 0, BaseWin: 0, SkillWin: 0, LooseChance: 1, AttemptRate: 1, LooseBallSpeed: 5)));
        for (int i = 0; i < 3000; i++)
        {
            match.Step();
            Assert.True(double.IsFinite(match.Ball.Vel.X) && double.IsFinite(match.Ball.Vel.Y) && match.Ball.Vel.XY.Length < 40);
        }
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Duel && e.Outcome == "LooseBall");
    }

    [Fact]
    public void InvalidLooseBallSpeedIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new DuelConfig(LooseBallSpeed: -1).Validate());
        Assert.Throws<ArgumentException>(() => new DuelConfig(LooseBallSpeed: 40).Validate());
        new DuelConfig(LooseBallSpeed: 0).Validate();
    }

    // ---- Step 2: conduct from duel facts ----

    [Fact]
    public void ConductIsDeterministicAndGrowsWithSpeedAndRearApproach()
    {
        var cfg = new DuelConfig();
        Assert.Equal(Conduct.Careless, DuelModel.ClassifyConduct(0, 0, 0.5, cfg));
        Assert.Equal(DuelModel.ClassifyConduct(5, 2.5, 0.5, cfg), DuelModel.ClassifyConduct(5, 2.5, 0.5, cfg));
        Assert.Equal(Conduct.Reckless, DuelModel.ClassifyConduct(4.4, Math.PI * 0.95, 0.5, cfg));
        Assert.Equal(Conduct.ExcessiveForce, DuelModel.ClassifyConduct(8, Math.PI, 0.5, cfg));
        // Sign of the angle does not matter; negative closing speed counts as no speed.
        Assert.Equal(DuelModel.ClassifyConduct(4.4, Math.PI * 0.9, 0.5, cfg), DuelModel.ClassifyConduct(4.4, -Math.PI * 0.9, 0.5, cfg));
        Assert.Equal(Conduct.Careless, DuelModel.ClassifyConduct(-5, 0, 0.5, cfg));
    }

    [Fact]
    public void ConductThresholdIsLowestInOwnThirdAndHighestInMiddle()
    {
        var cfg = new DuelConfig();
        // A score between the own-third and the middle threshold is Reckless only in the own third.
        double mid = (cfg.RecklessOwnThird + cfg.RecklessMiddle) / 2;
        double speed = cfg.SpeedScale, angle = Math.PI * (mid - cfg.ConductSpeedWeight) / (1 - cfg.ConductSpeedWeight);
        Assert.Equal(Conduct.Reckless, DuelModel.ClassifyConduct(speed, angle, 0.1, cfg));
        Assert.Equal(Conduct.Careless, DuelModel.ClassifyConduct(speed, angle, 0.5, cfg));
        Assert.True(cfg.RecklessOwnThird < cfg.RecklessMiddle && cfg.RecklessMiddle <= cfg.ExcessiveForceScore);
    }

    [Fact]
    public void ConductRejectsNonFiniteInputsAndInvalidConfig()
    {
        Assert.Throws<ArgumentException>(() => DuelModel.ClassifyConduct(double.NaN, 0, 0.5));
        Assert.Throws<ArgumentException>(() => new DuelConfig(RecklessMiddle: 1.5).Validate());
    }

    [Fact]
    public void MatchGivesCardsWhenEveryFoulIsReckless()
    {
        var cfg = new DuelConfig(AttemptRate: 1, RecklessOwnThird: 0, RecklessMiddle: 0, RecklessAttacking: 0,
            ExcessiveForceScore: 1, BaseFoul: 1, MaxFoul: 1);
        var match = Run(new MatchConfig(Duels: cfg), 6000);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Card && e.Outcome == "Yellow");
    }

    // ---- Step 3: passing ----

    [Fact]
    public void PasserAvoidsReceiverBeyondTheLastVisibleOpponent()
    {
        var self = Player(Side.Home, "p", 0, 0);
        var view = View(self, true, Player(Side.Home, "offside", 25, 5), Player(Side.Home, "onside", 15, -8),
            Player(Side.Away, "defender", 20, 0), Player(Side.Away, "keeper", 50, 0, keeper: true));
        Assert.Equal("onside", PassChoice.Choose(view, new PassConfig())!.ReceiverId);
        // Without the check a forward receiver would win; a huge negative margin disables it.
        var off = PassChoice.Choose(view, new PassConfig(OffsideMargin: -100))!;
        Assert.Equal("offside", off.ReceiverId);
    }

    [Fact]
    public void DefaultOffsideMarginAcceptsATargetSlightlyBeyondTheLineButNotFarBeyondIt()
    {
        Assert.Equal(-3.0, new PassConfig().OffsideMargin);
        var self = Player(Side.Home, "p", 0, 0);
        Perception Beyond(double x) => View(self, true, Player(Side.Home, "r", x, 5),
            Player(Side.Away, "defender", 20, 0), Player(Side.Away, "keeper", 50, 0, keeper: true));
        Assert.Equal("r", PassChoice.Choose(Beyond(22.5), new PassConfig())!.ReceiverId);
        Assert.Null(PassChoice.Choose(Beyond(26), new PassConfig()));
    }

    [Fact]
    public void Round5OffsideDefaultsAreFixedAndValid()
    {
        var pass = new PassConfig();
        Assert.Equal(-3.0, pass.OffsideMargin);
        pass.Validate();
        var runs = new AttackingRunConfig();
        Assert.Equal(3.0, runs.RunDepthMeters);
        Assert.Equal(1.6, runs.ThroughBallBonus);
        Assert.Equal(30.0, runs.RunCooldownSeconds);
        runs.Validate();
        Assert.True(runs.RunCooldownSeconds >= runs.RunSeconds);
        Assert.Throws<ArgumentException>(() => (pass with { OffsideMargin = double.NaN }).Validate());
        Assert.Throws<ArgumentException>(() => (runs with { RunCooldownSeconds = runs.RunSeconds - 1 }).Validate());
        // The pinned pre-round-5 values are the previous calibrated ones and differ from the new defaults.
        Assert.Equal(-1.5, PreRound5Defaults.OffsideMargin);
        Assert.Equal(1.5, PreRound5Defaults.Runs.RunDepthMeters);
        Assert.NotEqual(PreRound5Defaults.Runs, runs);
    }

    [Fact]
    public void OpponentNextToThePasserIsNotInTheLane()
    {
        var self = Player(Side.Home, "p", 0, 0);
        var view = View(self, true, Player(Side.Home, "mate", 0, 15), Player(Side.Away, "presser", 0.5, 0.5));
        Assert.True(PassChoice.Choose(view, new PassConfig())!.Score > PassChoice.Choose(view, new PassConfig(LaneStartMeters: 0))!.Score);
    }

    [Fact]
    public void ForcedChoiceAcceptsNegativeScoresAndCarrierPassesAfterForceSeconds()
    {
        var self = Player(Side.Home, "p", 0, 0);
        var view = View(self, true, Player(Side.Home, "far", 0, 34), Player(Side.Away, "a", 0, 10), Player(Side.Away, "b", 1, 20),
            Player(Side.Away, "c", -1, 30), Player(Side.Away, "d", 0, 33));
        var cfg = new PassConfig(RiskWeight: 3);
        Assert.Null(PassChoice.Choose(view, cfg));
        Assert.NotNull(PassChoice.Choose(view, cfg, force: true));
        var brain = new PassingBrain(cfg);
        Assert.Equal(IntentKind.Scan, brain.Decide(view with { HeldBallSeconds = cfg.HoldSeconds + cfg.ForceSeconds - 0.5 }).Kind);
        Assert.Equal(IntentKind.Pass, brain.Decide(view with { HeldBallSeconds = cfg.HoldSeconds + cfg.ForceSeconds + 0.1 }).Kind);
    }

    [Fact]
    public void IntendedReceiverMovesToMeetThePassAndOthersDoNotChaseIt()
    {
        var brain = new PassingBrain(new PassConfig());
        var self = Player(Side.Home, "r", 10, 0);
        var ball = new BallSnapshot(new Vec3(0, 0, 0), new Vec3(10, 0, 0));
        var view = View(self, false, Player(Side.Home, "other", 3, 3)) with { VisibleBall = ball, BallIsLoose = true, ReceiveTarget = new Vec2(14, 2) };
        var intent = brain.Decide(view);
        Assert.Equal(IntentKind.MoveToPosition, intent.Kind);
        Assert.Equal(new Vec2(14, 2), intent.Target);
        var other = Player(Side.Home, "other", 3, 3);
        var passive = brain.Decide(View(other, false) with { VisibleBall = ball, BallIsLoose = true, TeamPassInFlight = true,
            FormationTarget = new Vec2(3, 3) });
        Assert.NotEqual(1.0, passive.Target.X - (ball.Pos.X + ball.Vel.X * 0.25)); // not the chase target
        Assert.Equal(new Vec2(3, 3), passive.Target);
    }

    [Fact]
    public void DefenderCutsTheLaneOfAFastBallWhenHeCanReachIt()
    {
        var brain = new PassingBrain(new PassConfig());
        var ball = new BallSnapshot(new Vec3(0, 0, 0), new Vec3(12, 0, 0));
        var defender = Player(Side.Away, "d", 15, 4);
        var view = View(defender, false) with { VisibleBall = ball, BallIsLoose = true, FormationTarget = new Vec2(40, 40) };
        var intent = brain.Decide(view);
        Assert.Equal(IntentKind.MoveToPosition, intent.Kind);
        Assert.Equal(15, intent.Target.X, 6);
        Assert.Equal(0, intent.Target.Y, 6);
        // Far from the path: cannot arrive in time, so he does not cut the lane.
        var far = brain.Decide(View(Player(Side.Away, "d", 15, 40), false) with { VisibleBall = ball, BallIsLoose = true });
        Assert.NotEqual(new Vec2(15, 0), far.Target);
        // Lane cutting can be switched off.
        var off = new PassingBrain(new PassConfig(LaneCutRunSpeed: 0)).Decide(view);
        Assert.NotEqual(new Vec2(15, 0), off.Target);
    }

    // Distance from the kick (event Pass/Shot) to the touch of the first opponent afterwards (event Interception), per interception.
    private static IReadOnlyList<double> KickToInterceptionDistances(Match m)
    {
        var events = m.Events.Events;
        var list = new List<double>();
        for (int i = 1; i < events.Count; i++)
        {
            if (events[i].Type != EventType.Interception) continue;
            for (int j = i - 1; j >= 0 && events[i].Tick - events[j].Tick <= 30; j--)
            {
                if (events[j].Type is not (EventType.Pass or EventType.Shot) || events[j].Team == events[i].Team) continue;
                list.Add((events[i].Location - events[j].Location).Length);
                break;
            }
        }
        return list;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KickShieldKeepsOpponentsFromTouchingTheBallWithinTheShieldDistance(bool instructions)
    {
        // Isolate ball-contact geometry from the new tactical shape/pressing behaviours.
        // A touch can come at most one physics substep (ball speed 16 m/s x 0.02 s = 0.32 m) before the shield distance.
        const double tolerance = 0.4;
        var shielded = KickToInterceptionDistances(Run(PreRound4Defaults.Apply(new MatchConfig(Instructions: instructions ? new() : InstructionConfig.Disabled)), 54000));
        Assert.True(shielded.Count > 10, $"too few interceptions to test: {shielded.Count}");
        Assert.True(shielded.Min() >= new MatchConfig().KickShieldMeters - tolerance, $"closest touch {shielded.Min():F2} m");
        // The same assertion fails for a shield of 0.5 m or none: opponents then touch the ball close to the kicker.
        foreach (double small in new[] { 0.5, 0.0 })
        {
            var open = KickToInterceptionDistances(Run(PreRound4Defaults.Apply(new MatchConfig(Instructions: instructions ? new() : InstructionConfig.Disabled, KickShieldMeters: small)), 54000));
            Assert.True(open.Min() < new MatchConfig().KickShieldMeters - tolerance, $"shield {small}: closest touch {open.Min():F2} m");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterceptControlDefaultsAreStricterThanFieldControlSpeedAndConfigurable(bool instructions)
    {
        // Pins the original tight marking: this test is about the intercept settings, and its counts are scenario-sensitive.
        var cfg = new MatchConfig(Instructions: instructions ? new(MarkRadius: 18, MarkGap: 1.5, MarkBallRadius: 1000) : InstructionConfig.Disabled);
        Assert.True(cfg.InterceptSettings.MaxRelativeSpeed <= cfg.ControlSettings.MaxRelativeSpeed);
        Assert.True(cfg.InterceptSettings.ControlRadius > 0);
        var tight = cfg with { InterceptControl = new BallControlConfig(0.3, 0.3, 1) };
        int tightControls = Run(tight, 20000).Events.Events.Count(e => e.Type == EventType.Interception);
        int normalControls = Run(cfg, 20000).Events.Events.Count(e => e.Type == EventType.Interception);
        Assert.True(tightControls < normalControls, $"tight {tightControls} normal {normalControls}");
        Assert.Throws<ArgumentException>(() => Create(new MatchConfig(InterceptControl: new BallControlConfig(0, 0.3, 4))));
    }

    [Fact]
    public void InvalidNewPassSettingsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new PassConfig(ForceSeconds: -1).Validate());
        Assert.Throws<ArgumentException>(() => new PassConfig(LaneStartMeters: -1).Validate());
        Assert.Throws<ArgumentException>(() => new PassConfig(LaneCutMinBallSpeed: 0).Validate());
        Assert.Throws<ArgumentException>(() => new PassConfig(RiskWeight: -1).Validate());
        Assert.Throws<ArgumentException>(() => new PassConfig(DribbleMaxGoalDistance: -1).Validate());
    }

    [Fact]
    public void CarrierNearGoalDribblesTowardsGoalWhileHoldingBeforeTheDecision()
    {
        var brain = new PassingBrain(new PassConfig());
        var carrier = Player(Side.Home, "c", 30, 0);
        var intent = brain.Decide(View(carrier, true) with { HeldBallSeconds = 0.5 });
        Assert.Equal(IntentKind.MoveToPosition, intent.Kind);
        Assert.True(intent.Target.X > 30 && intent.Target.X < Pitch.HalfLength);
        var far = brain.Decide(View(Player(Side.Home, "c", 0, 0), true) with { HeldBallSeconds = 0.5 });
        Assert.Equal(IntentKind.HoldPosition, far.Kind);
    }

    // ---- Step 4: shots and keeper ----

    [Fact]
    public void GoalProbabilityFallsWithDistanceAndStaysInUnitInterval()
    {
        double previous = 1;
        foreach (double d in new[] { 3.0, 6, 10, 16, 22, 30, 40 })
        {
            double p = ShotModel.GoalProbability(d, 0);
            Assert.InRange(p, 0, 1);
            Assert.True(p < previous);
            previous = p;
        }
        // Foot shots without pressure; the all-shot averages in event_models (6-10 m 16.6%, 16-22 m 5.4%) include headers.
        Assert.InRange(ShotModel.GoalProbability(8, 0), 0.20, 0.40);
        Assert.InRange(ShotModel.GoalProbability(20, 0), 0.02, 0.10);
        Assert.True(ShotModel.GoalProbability(12, 8) < ShotModel.GoalProbability(12, 0)); // narrow angle
    }

    [Fact]
    public void ShotErrorGrowsWithErrorSettingAndSkillRemovesIt()
    {
        var from = new Vec2(-40, 0);
        var aim = new Vec2(-52.5, 0);
        double Spread(ShotConfig c, double skill)
        {
            double max = 0;
            for (ulong s = 0; s < 200; s++)
            {
                var v = ShotExecution.Velocity(from, aim, skill, new Rng(s), c);
                max = Math.Max(max, Math.Abs(v.Y / Math.Abs(v.X)));
            }
            return max;
        }
        Assert.True(Spread(new ShotConfig(AngleErrorBase: 2), 0.75) > Spread(new ShotConfig(AngleErrorBase: 0.5), 0.75));
        Assert.True(Spread(new ShotConfig(AngleErrorBase: 0, AngleErrorPerMeter: 0.1), 0.75) > Spread(new ShotConfig(AngleErrorBase: 0, AngleErrorPerMeter: 0), 0.75));
        Assert.Equal(0, Spread(new ShotConfig(), 1), 9);
        Assert.Equal(ShotExecution.Velocity(from, aim, 0.75, new Rng(1), new ShotConfig()),
            ShotExecution.Velocity(from, aim, 0.75, new Rng(1), new ShotConfig()));
    }

    [Fact]
    public void ShotThresholdControlsWhetherACarrierShoots()
    {
        var carrier = Player(Side.Home, "c", 45, 0);
        var view = View(carrier, true) with { HeldBallSeconds = 5 };
        var brain = new PassingBrain(new PassConfig(), new ShotConfig(MinGoalProbability: 0.05));
        Assert.Equal(IntentKind.Shoot, brain.Decide(view).Kind);
        var timid = new PassingBrain(new PassConfig(), new ShotConfig(MinGoalProbability: 0.9));
        Assert.NotEqual(IntentKind.Shoot, timid.Decide(view).Kind);
    }

    [Fact]
    public void KeeperReachDefaultIsWiderThanFieldControlAndInvalidShotSettingsAreRejected()
    {
        var cfg = new MatchConfig();
        Assert.True(cfg.KeeperSettings.ControlRadius > cfg.ControlSettings.ControlRadius);
        Assert.Throws<ArgumentException>(() => new ShotConfig(MinGoalProbability: 2).Validate());
        Assert.Throws<ArgumentException>(() => new ShotConfig(SpeedMean: 0).Validate());
        Assert.Throws<ArgumentException>(() => Create(new MatchConfig(Shots: new ShotConfig(AngleErrorBase: -1))));
    }

    [Fact]
    public void CalibratedMatchesAreDeterministicPerSeed()
    {
        Assert.Equal(Run(new MatchConfig(), 4000, 7).EventHash(), Run(new MatchConfig(), 4000, 7).EventHash());
        Assert.NotEqual(Run(new MatchConfig(), 4000, 7).EventHash(), Run(new MatchConfig(), 4000, 8).EventHash());
    }
}
