using System.Text.Json;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class AttackingRunTests
{
    private static PlayerSnapshot Player(Side side, string id, double x, double y = 0, double speed = 0) =>
        new(side, id, new Vec2(x, y), new Vec2(speed, 0), 0);

    private static Perception RunView() => new(0, Player(Side.Home, "runner", 18), GamePhase.Aanval,
        false, new Vec2(18, 6), new[] { Player(Side.Home, "carrier", 0),
            Player(Side.Away, "GK", 45), Player(Side.Away, "defender", 25) },
        new BallSnapshot(Vec3.Zero, Vec3.Zero), null, CanRunInBehind: true, TeamBallCarrierId: "carrier");

    private static Perception PassView(double x = 24, double speed = 4, double error = 0) => new(0,
        Player(Side.Home, "carrier", 10), GamePhase.Aanval, true, new Vec2(10, 0),
        new[] { Player(Side.Home, "runner", x, 5, speed), Player(Side.Away, "GK", 45),
            Player(Side.Away, "defender", 25, -8) }, new BallSnapshot(new Vec3(10, 0, 0), Vec3.Zero),
        null, HeldBallSeconds: 3, LineMisjudgement: error);

    private static Match Create(AttackingRunConfig? runs = null, ulong seed = 1, MatchConfig? config = null,
        Tactic? tactic = null) => Match.Create((config ?? new MatchConfig()) with { AttackingRuns = runs },
        tactic ?? HistoricalTactics.Load("laag_blok_442"),
        tactic ?? HistoricalTactics.Load("laag_blok_442"), seed);

    // Pre-RC7 pass choice: only the target is judged, so a runner level with the line can still be found.
    private static readonly AttackingRunConfig NoOnsideMargin = new(OnsideMarginMeters: null);

    private static readonly AttackingRunConfig Onside = new(OnsideMarginMeters: 0);

    [Fact]
    public void OnsideMarginSkipsAReceiverWhoWillBeBeyondTheLineAtTheKick()
    {
        // Runner 1 m behind the line (25) at 4 m/s: 25.4 after LeadSeconds, so offside at a kick that late.
        Assert.Null(PassChoice.Choose(PassView(24, 4), new PassConfig(OffsideMargin: 0.5), force: true, attackingRuns: Onside));
        Assert.Equal("runner", PassChoice.Choose(PassView(24, 4), new PassConfig(OffsideMargin: 0.5), attackingRuns: NoOnsideMargin)!.ReceiverId);
        Assert.Equal("runner", PassChoice.Choose(PassView(23, 4), new PassConfig(OffsideMargin: 0.5), attackingRuns: Onside)!.ReceiverId); // 24.4 <= 25
        // Standing 0.5 m offside: the default target margin (-3 m) accepted him; the receiver check does not.
        Assert.Null(PassChoice.Choose(PassView(25.5, 0), new PassConfig(), force: true, attackingRuns: Onside));
        Assert.Equal("runner", PassChoice.Choose(PassView(25.5, 0), new PassConfig(), attackingRuns: NoOnsideMargin)!.ReceiverId);
        Assert.Equal("runner", PassChoice.Choose(PassView(25.5, 0, 1), new PassConfig(), attackingRuns: Onside)!.ReceiverId); // passer misjudges the line 1 m deeper
        Assert.Null(PassChoice.Choose(PassView(24.5, 0), new PassConfig(), force: true, attackingRuns: new AttackingRunConfig(OnsideMarginMeters: 1)));
        Assert.Throws<ArgumentException>(() => new AttackingRunConfig(OnsideMarginMeters: -1).Validate());
    }

    [Fact]
    public void AttackerWithoutTheBallHoldsTheLineInsteadOfParkingOffside()
    {
        var view = RunView() with { CanRunInBehind = false, FormationTarget = new Vec2(30, 6) };
        var brain = new PassingBrain(new PassConfig(), attackingRuns: Onside);
        Assert.Equal(new Vec2(25, 6), brain.Decide(view).Target); // level with the second last opponent
        Assert.Equal(new Vec2(24, 6), new PassingBrain(new PassConfig(), attackingRuns: new AttackingRunConfig(OnsideMarginMeters: 1)).Decide(view).Target);
        Assert.Equal(new Vec2(30, 6), new PassingBrain(new PassConfig(), attackingRuns: NoOnsideMargin).Decide(view).Target);
        Assert.Equal(new Vec2(22, 6), brain.Decide(view with { FormationTarget = new Vec2(22, 6) }).Target); // already onside
        Assert.Equal(new Vec2(30, 6), brain.Decide(view with { TeamBallCarrierId = null }).Target); // only while his team has the ball
        var mirrored = view with { AttackPositiveX = false, Self = Player(Side.Home, "runner", -18), FormationTarget = new Vec2(-30, 6),
            VisiblePlayers = view.VisiblePlayers.Select(p => p with { Pos = new Vec2(-p.Pos.X, p.Pos.Y) }).ToArray() };
        Assert.Equal(new Vec2(-25, 6), brain.Decide(mirrored).Target);
    }

    [Fact]
    public void RunUsesOwnVisibleLineAndFormationWidth()
    {
        var brain = new PassingBrain(new PassConfig(OffsideMargin: 0.5), attackingRuns: PreRound5Defaults.Runs);
        var intent = brain.Decide(RunView());
        Assert.Equal(IntentKind.RunInBehind, intent.Kind);
        Assert.Equal(new Vec2(26.5, 6), intent.Target);
        Assert.Equal(27.5, brain.Decide(RunView() with { LineMisjudgement = 1 }).Target.X);
        var reversed = RunView() with { AttackPositiveX = false,
            Self = Player(Side.Home, "runner", -18), FormationTarget = new Vec2(-18, 6),
            VisiblePlayers = RunView().VisiblePlayers.Select(p => p with { Pos = new Vec2(-p.Pos.X, p.Pos.Y) }).ToArray() };
        Assert.Equal(new Vec2(-26.5, 6), brain.Decide(reversed).Target);
    }

    [Fact]
    public void RunRequiresAssignmentPossessionVisibleCarrierAndVisibleLine()
    {
        var view = RunView();
        var brain = new PassingBrain(new PassConfig(OffsideMargin: 0.5));
        foreach (var invalid in new[] {
            view with { CanRunInBehind = false }, view with { HasBall = true },
            view with { BallIsLoose = true }, view with { TeamPassInFlight = true },
            view with { TeamBallCarrierId = null }, view with { TeamBallCarrierId = "unseen" },
            view with { Self = view.Self with { IsGoalkeeper = true } },
            view with { Self = view.Self with { Pos = new Vec2(0, 0) } },
            view with { VisiblePlayers = view.VisiblePlayers.Where(p => p.Id != "GK").ToArray() },
            view with { VisiblePlayers = view.VisiblePlayers.Select(p => p.Id == "carrier" ? p with { Pos = new Vec2(-0.01, 0) } : p).ToArray() } })
            Assert.NotEqual(IntentKind.RunInBehind, brain.Decide(invalid).Kind);
        Assert.Equal(view.FormationTarget, brain.Decide(view with { TeamBallCarrierId = null }).Target);
        Assert.NotEqual(IntentKind.RunInBehind,
            new PassingBrain(new PassConfig(OffsideMargin: 0.5), attackingRuns: new AttackingRunConfig(EnableRuns: false)).Decide(view).Kind);
        Assert.Equal(new Vec2(30, 6), brain.Decide(view with { TeamPassInFlight = true, ReceiveTarget = new Vec2(30, 6) }).Target);
    }

    [Fact]
    public void ThroughBallLeadsAnOnsideRunnerBeyondTheLine()
    {
        var option = PassChoice.Choose(PassView(), new PassConfig(OffsideMargin: 0.5), attackingRuns: PreRound5Defaults.Runs)!;
        Assert.Equal("runner", option.ReceiverId);
        Assert.True(option.IsThroughBall);
        Assert.Equal(new Vec2(25.4, 5), option.Target);
        Assert.Null(PassChoice.Choose(PassView(), new PassConfig(OffsideMargin: 0.5), attackingRuns: PreRound5Defaults.Runs with { EnableThroughBalls = false }));
        var noBonus = PassChoice.Choose(PassView(), new PassConfig(OffsideMargin: 0.5), attackingRuns: PreRound5Defaults.Runs with { ThroughBallBonus = 0 })!;
        Assert.Equal(0.8, option.Score - noBonus.Score, 8);
        var mirrored = PassView() with { AttackPositiveX = false, Self = Player(Side.Home, "carrier", -10),
            VisiblePlayers = PassView().VisiblePlayers.Select(p => p with { Pos = new Vec2(-p.Pos.X, p.Pos.Y), Vel = new Vec2(-p.Vel.X, p.Vel.Y) }).ToArray() };
        Assert.Equal(-25.4, PassChoice.Choose(mirrored, new PassConfig(OffsideMargin: 0.5), attackingRuns: PreRound5Defaults.Runs)!.Target.X, 8);
    }

    [Fact]
    public void ThroughBallRequiresForwardVelocityLeadAndAnApparentlyOnsideReceiver()
    {
        foreach (var view in new[] { PassView(26, 0), PassView(26, -4), PassView(26, 0.2),
            PassView(26, 4), PassView() with { VisiblePlayers = PassView().VisiblePlayers.Where(p => p.Id != "GK").ToArray() } })
            Assert.Null(PassChoice.Choose(view, new PassConfig(OffsideMargin: 0.5), force: true));
        Assert.Null(PassChoice.Choose(PassView(26, 4, 2), new PassConfig(LeadSeconds: 0, OffsideMargin: 0.5), attackingRuns: NoOnsideMargin));
        Assert.True(PassChoice.Choose(PassView(26, 4, 2), new PassConfig(OffsideMargin: 0.5), attackingRuns: NoOnsideMargin)!.IsThroughBall);
        Assert.Null(PassChoice.Choose(PassView(26, 4, -2), new PassConfig(OffsideMargin: 0.5), attackingRuns: NoOnsideMargin));
        Assert.True(PassChoice.Choose(PassView(25, 4), new PassConfig(OffsideMargin: 0.5), attackingRuns: NoOnsideMargin)!.IsThroughBall); // level is onside
    }

    [Fact]
    public void ThroughBallBonusCanMakeTheRunnerPreferableToASafePass()
    {
        var view = PassView();
        view = view with { VisiblePlayers = view.VisiblePlayers.Select(p => p.Id == "runner"
            ? p with { Pos = new Vec2(p.Pos.X, 15) } : p).Append(Player(Side.Home, "safe", 24)).ToArray() };
        // Pre-round-3 pass weights: with the newer forward weight the runner wins even without the bonus.
        Assert.Equal("safe", PassChoice.Choose(view, new PassConfig(OffsideMargin: 0.5, ForwardWeight: 0.06, DistanceWeight: 0.015),
            attackingRuns: NoOnsideMargin with { ThroughBallBonus = 0 })!.ReceiverId);
        Assert.Equal("runner", PassChoice.Choose(view, new PassConfig(OffsideMargin: 0.5, ForwardWeight: 0.06, DistanceWeight: 0.015),
            attackingRuns: NoOnsideMargin)!.ReceiverId);
    }

    [Fact]
    public void RunDurationAndCooldownApplyPerPlayer()
    {
        var settings = new AttackingRunConfig(RunSeconds: 0.6, RunCooldownSeconds: 2, RunStartDistanceMeters: 100);
        var match = Create(settings, config: new MatchConfig(Passing: new PassConfig(HoldSeconds: 20)));
        var previous = new Dictionary<(Side, string), bool>();
        var starts = new Dictionary<(Side, string), long>();
        int runs = 0;
        for (int tick = 0; tick < 300; tick++)
        {
            match.Step();
            foreach (var player in match.GetSnapshot().Players)
            {
                var key = (player.Team, player.Id);
                bool running = match.IntentOf(player.Team, player.Id)?.Kind == IntentKind.RunInBehind;
                if (running && !previous.GetValueOrDefault(key))
                {
                    if (starts.TryGetValue(key, out long last)) Assert.True(tick - last >= 20);
                    starts[key] = tick;
                    runs++;
                }
                if (running) Assert.True(tick - starts[key] < 6);
                previous[key] = running;
            }
        }
        Assert.True(runs > 2);
    }

    [Fact]
    public void MissingOpponentsNeverMakeTheEstimatedLineDeeperAndKeeperCanBeUpfield()
    {
        var view = PassView() with { VisiblePlayers = new[] { Player(Side.Away, "GK", 5),
            Player(Side.Away, "last", 40), Player(Side.Away, "second", 30), Player(Side.Away, "third", 20) } };
        Assert.Equal(30, AttackingRuns.EstimatedLine(view));
        Assert.Equal(20, AttackingRuns.EstimatedLine(view with {
            VisiblePlayers = view.VisiblePlayers.Where(p => p.Id != "second").ToArray() }));
    }

    [Fact]
    public void MatchAssignsAtMostTwoRunnersByTacticRoleAndStopsThemAfterPossession()
    {
        var match = Create(new AttackingRunConfig(RunStartDistanceMeters: 100));
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        int runs = 0;
        for (int tick = 0; tick < 3000; tick++)
        {
            match.Step();
            foreach (var side in new[] { Side.Home, Side.Away })
            {
                var active = match.GetSnapshot().Players.Where(p => p.Team == side
                    && match.IntentOf(side, p.Id)?.Kind == IntentKind.RunInBehind).ToArray();
                Assert.True(active.Length <= 2);
                foreach (var player in active)
                {
                    runs++;
                    var view = match.PerceptionOf(side, player.Id)!;
                    Assert.False(view.HasBall);
                    Assert.False(view.BallIsLoose);
                    Assert.True(view.CanRunInBehind);
                    Assert.True(AttackingRuns.IsAttackingRole(tactic.Roster.First(p => p.Id == player.Id).Role));
                    Assert.True(view.VisiblePlayers.First(p => p.Team == side && p.Id == view.TeamBallCarrierId).Pos.X
                        * (view.AttacksPositiveX ? 1 : -1) >= 0);
                }
            }
        }
        Assert.True(runs > 0);
    }

    [Fact]
    public void WingerCanRunButDefenderWithAStrikerIdentityCannot()
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        tactic = tactic with { Roster = tactic.Roster.Select(p => p with {
            Role = p.Id == "RB" ? "buitenspeler" : p.Id == "GK" ? "keeper" : "back" }).ToArray() };
        var match = Create(new AttackingRunConfig(RunStartDistanceMeters: 100), tactic: tactic);
        bool sawWinger = false;
        for (int i = 0; i < 3000; i++)
        {
            match.Step();
            foreach (var p in match.GetSnapshot().Players)
                if (match.IntentOf(p.Team, p.Id)?.Kind == IntentKind.RunInBehind)
                {
                    Assert.Equal("RB", p.Id);
                    sawWinger = true;
                }
        }
        Assert.True(sawWinger);
    }

    [Fact]
    public void NoiseAndMatchOutcomesAreSeededAndDisabledFeaturesHaveStableKickOffHashes()
    {
        var a = Create(seed: 7); var b = Create(seed: 7); var c = Create(seed: 8);
        var errors = new List<double>();
        for (int i = 0; i < 1000; i++)
        {
            a.Step(); b.Step(); c.Step();
            foreach (var player in a.GetSnapshot().Players)
            {
                var view = a.PerceptionOf(player.Team, player.Id);
                Assert.Equal(view?.LineMisjudgement, b.PerceptionOf(player.Team, player.Id)?.LineMisjudgement);
                if (view?.Tick == i) errors.Add(view.LineMisjudgement);
            }
        }
        Assert.Contains(errors, e => e < -1);
        Assert.Contains(errors, e => e > 1);
        Assert.InRange(errors.Average(), -0.1, 0.1);
        Assert.InRange(errors.Select(e => e * e).Average(), 0.8, 1.2);
        Assert.Equal(a.EventHash(), b.EventHash());
        Assert.Equal(a.SnapshotHash(), b.SnapshotHash());
        Assert.NotEqual(a.EventHash(), c.EventHash());
        // This historical baseline predates both attacking runs and keeper outlets: disable both extensions.
        var disabled = Create(new AttackingRunConfig(EnableRuns: false, EnableThroughBalls: false, OnsideMarginMeters: null), seed: 7,
            config: PreRound5Defaults.Apply(PreRound4Defaults.Apply(new MatchConfig(Perception: new PerceptionConfig(KeeperAwarenessMeters: 0),
                Passing: new PassConfig(BackPassToKeeperScore: 0, KeeperReceiveAdvanceMeters: 0)))));
        Finish(disabled);
        // Renewed for the separated kick-off; the historical instruction/run settings above stay fixed.
        Assert.Equal("5ef4d75e88dce364cfd4624bd1aae48b51f6766bced312b498c62f685615636d", disabled.EventHash());
        Assert.Equal("b730b7f529cc322c7fc48243c6fad43a7f4691d04be71f796c3136e4504a6ce2", disabled.SnapshotHash());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullMatchesProduceOffsideWithNoiseAndNoneWithoutIt(bool instructions)
    {
        int offsides = 0;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            // Full-speed pressers (before Q1 2027 pressing): with jockeying seeds 1-8 without instructions give no offside at all.
            var match = Create(seed: seed, config: new MatchConfig(Instructions: instructions ? new() : InstructionConfig.Disabled, PressJockeyMeters: 0));
            Finish(match);
            offsides += match.Events.Events.Count(e => e.Type == EventType.Offside);
            // Pin the historical clearance and carrier movement for the zero-noise geometry control.
            // New close-goal stops and clearances can legitimately leave another striker beyond the line.
            // The deliberate offside gamble (OffsideMargin -3) is also switched off: since restart kicks read the real attack
            // direction in the second half, a free kick may pass to a striker just beyond the line by that gamble (seed 5).
            // Only for the no-offside control; the instructed run keeps the default gamble and its historical timeline.
            var exact = Create(new AttackingRunConfig(LineMisjudgementMeters: 0), seed,
                config: new MatchConfig(Instructions: instructions ? new() : InstructionConfig.Disabled,
                    Passing: new PassConfig(ClearStopsAtTarget: false, EarlyShotXg: null, OffsideMargin: instructions ? -3.0 : 0)));
            Finish(exact);
            if (!instructions) Assert.DoesNotContain(exact.Events.Events, e => e.Type == EventType.Offside);
            else
            {
                // Exact perception does not prevent an instructed formation move from crossing the line.
                foreach (var player in exact.GetSnapshot().Players.Where(p => !p.SentOff))
                    if (exact.PerceptionOf(player.Team, player.Id) is { } view)
                        Assert.Equal(0, view.LineMisjudgement);
            }
        }
        Assert.True(offsides > 0);
    }

    private static void Finish(Match match)
    {
        for (int i = 0; i < 150000 && match.Status != MatchStatus.Finished; i++) match.Step();
        Assert.Equal(MatchStatus.Finished, match.Status);
    }

    [Fact]
    public void SettingsHaveDefaultsValidateAndRoundTrip()
    {
        var config = new MatchConfig();
        var runs = config.AttackingRunSettings;
        Assert.True(runs.EnableRuns && runs.EnableThroughBalls);
        Assert.Equal(3.0, runs.RunDepthMeters);
        Assert.Equal(1, runs.LineMisjudgementMeters);
        Assert.Equal(1.6, runs.ThroughBallBonus);
        Assert.Equal(0.3, runs.MinRunSpeed);
        Assert.Equal(4, runs.RunSeconds);
        Assert.Equal(30, runs.RunCooldownSeconds);
        Assert.Equal(14, runs.RunStartDistanceMeters);
        runs.Validate();
        var custom = config with { AttackingRuns = runs with { EnableRuns = false, RunDepthMeters = 2 } };
        Assert.Equal(custom, MatchConfigJson.Parse(JsonSerializer.Serialize(custom)));
        Assert.False(MatchConfigJson.Parse("{\"AttackingRuns\":{\"EnableRuns\":false}}").AttackingRunSettings.EnableRuns);
        Assert.Throws<ArgumentException>(() => MatchConfigJson.Parse("{\"AttackingRuns\":{\"RunDepthMeter\":2}}"));
        foreach (var invalid in new[] { runs with { RunDepthMeters = -1 }, runs with { RunDepthMeters = double.NaN },
            runs with { LineMisjudgementMeters = -1 }, runs with { LineMisjudgementMeters = double.PositiveInfinity },
            runs with { ThroughBallBonus = -1 }, runs with { ThroughBallBonus = double.NaN },
            runs with { MinRunSpeed = 0 }, runs with { MinRunSpeed = double.PositiveInfinity },
            runs with { RunSeconds = 0 }, runs with { RunSeconds = double.NaN },
            runs with { RunCooldownSeconds = 3 }, runs with { RunCooldownSeconds = double.PositiveInfinity },
            runs with { RunStartDistanceMeters = 0 }, runs with { RunStartDistanceMeters = double.NaN } })
        {
            Assert.Throws<ArgumentException>(() => invalid.Validate());
            Assert.Throws<ArgumentException>(() => Create(invalid));
        }
    }
}
