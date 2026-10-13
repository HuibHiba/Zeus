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
        tactic ?? TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
        tactic ?? TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")), seed);

    [Fact]
    public void RunUsesOwnVisibleLineAndFormationWidth()
    {
        var brain = new PassingBrain(new PassConfig());
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
        var brain = new PassingBrain(new PassConfig());
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
            new PassingBrain(new PassConfig(), attackingRuns: new AttackingRunConfig(EnableRuns: false)).Decide(view).Kind);
        Assert.Equal(new Vec2(30, 6), brain.Decide(view with { TeamPassInFlight = true, ReceiveTarget = new Vec2(30, 6) }).Target);
    }

    [Fact]
    public void ThroughBallLeadsAnOnsideRunnerBeyondTheLine()
    {
        var option = PassChoice.Choose(PassView(), new PassConfig())!;
        Assert.Equal("runner", option.ReceiverId);
        Assert.True(option.IsThroughBall);
        Assert.Equal(new Vec2(25.4, 5), option.Target);
        Assert.Null(PassChoice.Choose(PassView(), new PassConfig(), attackingRuns: new AttackingRunConfig(EnableThroughBalls: false)));
        var noBonus = PassChoice.Choose(PassView(), new PassConfig(), attackingRuns: new AttackingRunConfig(ThroughBallBonus: 0))!;
        Assert.Equal(0.8, option.Score - noBonus.Score, 8);
        var mirrored = PassView() with { AttackPositiveX = false, Self = Player(Side.Home, "carrier", -10),
            VisiblePlayers = PassView().VisiblePlayers.Select(p => p with { Pos = new Vec2(-p.Pos.X, p.Pos.Y), Vel = new Vec2(-p.Vel.X, p.Vel.Y) }).ToArray() };
        Assert.Equal(-25.4, PassChoice.Choose(mirrored, new PassConfig())!.Target.X, 8);
    }

    [Fact]
    public void ThroughBallRequiresForwardVelocityLeadAndAnApparentlyOnsideReceiver()
    {
        foreach (var view in new[] { PassView(26, 0), PassView(26, -4), PassView(26, 0.2),
            PassView(26, 4), PassView() with { VisiblePlayers = PassView().VisiblePlayers.Where(p => p.Id != "GK").ToArray() } })
            Assert.Null(PassChoice.Choose(view, new PassConfig(), force: true));
        Assert.Null(PassChoice.Choose(PassView(26, 4, 2), new PassConfig(LeadSeconds: 0)));
        Assert.True(PassChoice.Choose(PassView(26, 4, 2), new PassConfig())!.IsThroughBall);
        Assert.Null(PassChoice.Choose(PassView(26, 4, -2), new PassConfig()));
        Assert.True(PassChoice.Choose(PassView(25, 4), new PassConfig())!.IsThroughBall); // level is onside
    }

    [Fact]
    public void ThroughBallBonusCanMakeTheRunnerPreferableToASafePass()
    {
        var view = PassView();
        view = view with { VisiblePlayers = view.VisiblePlayers.Select(p => p.Id == "runner"
            ? p with { Pos = new Vec2(p.Pos.X, 15) } : p).Append(Player(Side.Home, "safe", 24)).ToArray() };
        Assert.Equal("safe", PassChoice.Choose(view, new PassConfig(),
            attackingRuns: new AttackingRunConfig(ThroughBallBonus: 0))!.ReceiverId);
        Assert.Equal("runner", PassChoice.Choose(view, new PassConfig())!.ReceiverId);
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
    public void NoiseAndMatchOutcomesAreSeededAndDisabledFeaturesKeepOldHashes()
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
        var disabled = Create(new AttackingRunConfig(EnableRuns: false, EnableThroughBalls: false), seed: 7,
            config: new MatchConfig(Perception: new PerceptionConfig(KeeperAwarenessMeters: 0),
                Passing: new PassConfig(BackPassToKeeperScore: 0, KeeperReceiveAdvanceMeters: 0)));
        Finish(disabled);
        Assert.Equal("4b7198964f156d92d7975ef27518362b2a2b08498fa831e0285cf085bb7a06d1", disabled.EventHash());
        Assert.Equal("f0073e74ce2cd148778090e346517975fe42d0b63fabfddeab9e50fb5be27ad7", disabled.SnapshotHash());
    }

    [Fact]
    public void FullMatchesProduceOffsideWithNoiseAndNoneWithoutIt()
    {
        int offsides = 0;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var match = Create(seed: seed);
            Finish(match);
            offsides += match.Events.Events.Count(e => e.Type == EventType.Offside);
            var exact = Create(new AttackingRunConfig(LineMisjudgementMeters: 0), seed);
            Finish(exact);
            Assert.DoesNotContain(exact.Events.Events, e => e.Type == EventType.Offside);
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
        Assert.Equal(1.5, runs.RunDepthMeters);
        Assert.Equal(1, runs.LineMisjudgementMeters);
        Assert.Equal(0.8, runs.ThroughBallBonus);
        Assert.Equal(0.3, runs.MinRunSpeed);
        Assert.Equal(4, runs.RunSeconds);
        Assert.Equal(60, runs.RunCooldownSeconds);
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
