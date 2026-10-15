using System.Text.Json;
using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class InstructionTests
{
    private static readonly PhasePlayer Layout = new("P", .7, .9, null, Array.Empty<string>(), null);
    private static readonly PhasePlayer Rest = Layout with { X = .25 };
    private static readonly TeamSettings Team = new(.5, .5, "zone", .25, 0);
    private static PlayerSnapshot Opp(double x, double y, string id = "O") => new(Side.Away, id, new(x, y), Vec2.Zero, 0);
    private static Perception View(string instruction, GamePhase phase = GamePhase.Aanval) => new(0,
        new PlayerSnapshot(Side.Home, "P", new(20, 25), Vec2.Zero, 0), phase, false, new(20, 25),
        new[] { Opp(20, 25), Opp(38, -10, "D") }, new BallSnapshot(new(25, 0, 0), Vec3.Zero),
        new BallObservation(0, new(new(25, 0, 0), Vec3.Zero)), Instructions: new[] { instruction });

    private static Vec2 Target(Perception v, InstructionConfig config) =>
        InstructionRules.Target(v, Layout, Rest, Team, .25, Vec2.Zero, config);

    [Theory]
    [InlineData("blijf_achter", GamePhase.Aanval, "x", -1)]
    [InlineData("laat_zakken", GamePhase.Opbouw, "x", -1)]
    [InlineData("blijf_breed", GamePhase.Aanval, "y", 1)]
    [InlineData("kom_op", GamePhase.Aanval, "x", 1)]
    [InlineData("sluit_naar_binnen", GamePhase.Aanval, "y", -1)]
    [InlineData("loop_zestien_in", GamePhase.Aanval, "x", 1)]
    [InlineData("dek_ruimte_achter_lijn", GamePhase.Verdedigen, "x", -1)]
    public void InstructionsChangeActualMovementAndMirrorWithEnds(string instruction, GamePhase phase, string axis, int sign)
    {
        var v = View(instruction, phase);
        if (phase == GamePhase.Verdedigen) v = v with { KnownBall = new(0, new(new(-10, 0, 0), Vec3.Zero)) };
        var target = Target(v, new());
        var baseline = Target(v, InstructionConfig.Disabled);
        Assert.True(((axis == "x" ? target.X - baseline.X : target.Y - baseline.Y) * sign) > 0);
        var body = new BodyState(v.Self.Pos, Vec2.Zero, 0);
        var control = body;
        double displacement = 0;
        for (int i = 0; i < 100; i++)
        {
            body = Mover.Step(body, target, PlayerProfile.Default, .1);
            control = Mover.Step(control, baseline, PlayerProfile.Default, .1);
            displacement += (body.Pos - control.Pos).Length;
        }
        Assert.True(displacement / 100 > 1);
        var mirrored = v with { AttackPositiveX = false, Self = v.Self with { Pos = v.Self.Pos * -1 },
            FormationTarget = v.FormationTarget * -1,
            KnownBall = new(0, v.KnownBall!.Ball with { Pos = v.KnownBall.Ball.Pos * -1 }),
            VisiblePlayers = v.VisiblePlayers.Select(p => p with { Pos = p.Pos * -1, Vel = p.Vel * -1 }).ToArray() };
        Assert.Equal(target * -1, Target(mirrored, new()));
    }

    [Fact]
    public void WidthAndRestDefenceWinConflictsAndKeeperAndStoppagesAreUnaffected()
    {
        var view = View("blijf_achter") with { Instructions = new[] { "blijf_achter", "kom_op", "loop_zestien_in", "blijf_breed", "sluit_naar_binnen" } };
        var target = Target(view, new());
        Assert.Equal(-26.25, target.X, 8);
        Assert.True(target.Y >= 25);
        Assert.Equal(view.FormationTarget, Target(view with { Phase = GamePhase.Stilstaand }, new()));
        Assert.Equal(view.FormationTarget, Target(view with { Self = view.Self with { IsGoalkeeper = true } }, new()));
        Assert.Equal(View("laat_zakken").FormationTarget, Target(View("laat_zakken"), new()));
        Assert.Equal(View("loop_zestien_in").FormationTarget, Target(View("loop_zestien_in") with { KnownBall = null }, new()));
    }

    [Fact]
    public void MarkingFollowsOnlyAssignedVisibleOpponentWithinRadiusAndFallsBack()
    {
        var view = View("dek_man", GamePhase.Verdedigen) with { MarkId = "O", VisiblePlayers = new[] { Opp(23, 24), Opp(21, 25, "other") } };
        var brain = new PassingBrain(new());
        Assert.Equal(new Vec2(21.5, 24), brain.Decide(view).Target);
        Assert.Equal(view.FormationTarget, brain.Decide(view with { MarkId = "missing" }).Target);
        Assert.Equal(view.FormationTarget, brain.Decide(view with { VisiblePlayers = new[] { Opp(50, 25) } }).Target);
        Assert.Equal(view.FormationTarget, new PassingBrain(new(), instructions: InstructionConfig.Disabled).Decide(view).Target);
        Assert.Equal(view.FormationTarget, brain.Decide(view with { VisiblePlayers = new[] { Opp(23, 24) with { SentOff = true } } }).Target);
        var reversed = view with { AttackPositiveX = false, FormationTarget = view.FormationTarget * -1, Self = view.Self with { Pos = view.Self.Pos * -1 },
            VisiblePlayers = new[] { Opp(-23, -24) } };
        Assert.Equal(new Vec2(-21.5, -24), brain.Decide(reversed).Target);
        Assert.Equal(IntentKind.Scan, brain.Decide(view with { VisibleBall = null, KnownBall = null,
            Self = view.Self with { Pos = new(21.5, 24) } }).Kind);
    }

    [Theory]
    [InlineData("man", 14, true)]
    [InlineData("gemengd", 14, false)]
    [InlineData("gemengd", 6, true)]
    [InlineData("zone", 6, false)]
    public void PressingFormsHaveDifferentLocalMarkingRanges(string form, double distance, bool follows)
    {
        var view = View("", GamePhase.Verdedigen) with { PressingForm = form,
            VisiblePlayers = new[] { Opp(20 - distance, 25) } };
        Assert.Equal(follows, InstructionRules.MarkTarget(view, new(EnablePressingForm: true)) is not null);
        Assert.Null(InstructionRules.MarkTarget(view, InstructionConfig.Disabled));
    }

    [Fact]
    public void FreeManSeeksObservedClearanceAndDoesNotInventHiddenOpponents()
    {
        var v = View("vrije_man_zoeken");
        var chosen = Target(v, new(EnableFindSpace: true));
        Assert.True((chosen - v.VisiblePlayers[0].Pos).Length > (v.FormationTarget - v.VisiblePlayers[0].Pos).Length + 5);
        Assert.Equal(v.FormationTarget, Target(v with { VisiblePlayers = Array.Empty<PlayerSnapshot>() }, new(EnableFindSpace: true)));
        Assert.Equal(v.FormationTarget, Target(v with { KnownBall = null }, new(EnableFindSpace: true)));
    }

    [Fact]
    public void DribbleCutsInsideWithoutGoodPassAndYieldsToGoodPassAndTimeout()
    {
        var v = View("dribbel_in") with { HasBall = true };
        var brain = new PassingBrain(new(ClearPressureMeters: 0, CarryMaxGoalDistance: 0, DribbleMaxGoalDistance: 0));
        var intent = brain.Decide(v);
        Assert.Equal(IntentKind.DribbleInside, intent.Kind);
        Assert.True(intent.Target.X > v.Self.Pos.X && intent.Target.Y < v.Self.Pos.Y);
        var good = v with { VisiblePlayers = new[] { new PlayerSnapshot(Side.Home, "mate", new(35, 25), Vec2.Zero, 0) }, HeldBallSeconds = 3 };
        Assert.Equal(IntentKind.Pass, brain.Decide(good).Kind);
        Assert.NotEqual(IntentKind.DribbleInside, brain.Decide(v with { HeldBallSeconds = 10 }).Kind);
        Assert.NotEqual(IntentKind.DribbleInside, new PassingBrain(new(), instructions: InstructionConfig.Disabled).Decide(v).Kind);
    }

    private sealed class DuelFixtureBrain(bool dribble) : IPlayerBrain
    {
        public Intent Decide(Perception v) => new(dribble && v.HasBall ? IntentKind.DribbleInside : IntentKind.HoldPosition, v.Self.Pos);
    }

    [Fact]
    public void DribbleUsesExistingDuelChanceAgainstVisibleNonPressingOpponentAndIsSeeded()
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        tactic = tactic with { Phases = tactic.Phases.ToDictionary(p => p.Key, p => p.Value with {
            Players = p.Value.Players.Select(p => p.Id == "RC" ? p with { X = .5, Y = .5 } : p).ToArray() }) };
        Match Play(ulong seed, bool enabled)
        {
            var match = Match.Create(new(MaxReactionSeconds: 0,
                Instructions: InstructionConfig.Disabled with { EnableDribbleInside = enabled },
                Duels: new DuelConfig(AttemptRate: 1)), tactic, tactic, seed,
                (side, _) => new DuelFixtureBrain(side == Side.Home));
            match.Step();
            return match;
        }
        var first = Play(4, true);
        Assert.Single(first.Events.Events.Where(e => e.Type == EventType.Duel));
        Assert.Equal(first.EventHash(), Play(4, true).EventHash());
        Assert.DoesNotContain(Play(4, false).Events.Events, e => e.Type == EventType.Duel);
        var outcomes = Enumerable.Range(1, 12).Select(seed => Play((ulong)seed, true).Events.Events.Single(e => e.Type == EventType.Duel).Outcome).Distinct();
        Assert.True(outcomes.Count() > 1);
    }

    [Fact]
    public void TempoChangesReleaseTimeAndLineHeightAndCompactnessChangeShape()
    {
        var view = View("") with { HasBall = true, HeldBallSeconds = 2,
            VisiblePlayers = new[] { new PlayerSnapshot(Side.Home, "mate", new(30, 25), Vec2.Zero, 0) } };
        var brain = new PassingBrain(new(DribbleMaxGoalDistance: 0, CarryMaxGoalDistance: 0), instructions: new(EnableTempo: true));
        Assert.Equal(IntentKind.Pass, brain.Decide(view with { Tempo = 1 }).Kind);
        Assert.NotEqual(IntentKind.Pass, brain.Decide(view with { Tempo = 0 }).Kind);
        var defend = View("", GamePhase.Verdedigen);
        var high = InstructionRules.Target(defend, Layout, Rest, Team with { LineHeight = .4 }, .25, Vec2.Zero, new(EnableLineHeight: true));
        Assert.Equal(15.75, high.X - defend.FormationTarget.X, 8);
        var compact = InstructionRules.Target(defend, Layout, Rest, Team with { Compactness = 1 }, .25, Vec2.Zero, new());
        Assert.True(Math.Abs(compact.Y) < Math.Abs(defend.FormationTarget.Y));
        Assert.True(Math.Abs(compact.X) < Math.Abs(defend.FormationTarget.X));
    }

    [Fact]
    public void ConfigDefaultsGateUncalibratedEffectsRoundTripsAndRejectsInvalidParameters()
    {
        var defaults = new MatchConfig();
        Assert.Equal(new InstructionConfig(), defaults.InstructionSettings);
        Assert.All(typeof(InstructionConfig).GetProperties().Where(p => p.PropertyType == typeof(bool)),
            p => Assert.Equal(p.Name is not ("EnablePressingForm" or "EnableTempo" or "EnableLineHeight" or "EnableFindSpace"), (bool)p.GetValue(defaults.InstructionSettings)!));
        var config = defaults with { Instructions = InstructionConfig.Disabled with { EnableWidth = true } };
        Assert.Equal(config, MatchConfigJson.Parse(JsonSerializer.Serialize(config)));
        foreach (var invalid in new[] { new InstructionConfig(MarkRadius: -1), new InstructionConfig(WideMeters: 35),
            new InstructionConfig(AdvanceMeters: double.NaN), new InstructionConfig(MarkGap: 19) })
            Assert.Throws<ArgumentException>(invalid.Validate);
    }

    [Fact]
    public void MatchPassesPhaseInstructionsAndSettingsIntoOwnPerception()
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var match = Match.Create(new(MaxReactionSeconds: 0), tactic, tactic, 4);
        match.Step();
        var view = match.PerceptionOf(Side.Home, "RB")!;
        Assert.Equal(tactic.Phase(view.Phase).Players.Single(p => p.Id == "RB").Instructions, view.Instructions);
        Assert.Equal(tactic.Team.Tempo, view.Tempo);
        Assert.Equal(tactic.Team.PressingForm, view.PressingForm);
    }

    [Fact]
    public void TriggerInstructionsEnterOwnOrdersThenExpireWithoutRemovingPhaseInstructions()
    {
        var source = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var trigger = new Trigger("temporary width", GamePhase.Balverlies, TriggerRules.LossInOwnHalf,
            null, new[] { new TriggerAction("RB", "blijf_breed") });
        Assert.True(TriggerRules.IsSupported(trigger));
        foreach (var instruction in new[] { "blijf_achter", "laat_zakken", "kom_op", "sluit_naar_binnen", "dek_man",
            "loop_zestien_in", "dek_ruimte_achter_lijn", "dribbel_in", "vrije_man_zoeken" })
            Assert.True(TriggerRules.IsSupported(trigger with { Then = new[] { new TriggerAction("RB", instruction) } }));
        foreach (bool enabled in new[] { true, false })
        {
            var tactic = source with { Triggers = new[] { trigger } };
            var match = Match.Create(new(MaxReactionSeconds: 0, Profile: new PlayerProfile(MaxSpeed: 0),
                Perception: new PerceptionConfig(ScanIntervalSeconds: .1), EnableTriggers: enabled,
                Triggers: new TriggerConfig(PressSeconds: .5, OwnHalfEnd: .9)), tactic, source, 4,
                (_, _) => new FormationBrain());
            var opponent = match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.Id == "RC");
            match.SetBall(new(opponent.Pos.X, opponent.Pos.Y, 0), Vec3.Zero);
            match.Step();
            Assert.Equal(Side.Away, match.Possession);
            match.Step();
            Assert.Equal(enabled, match.PerceptionOf(Side.Home, "RB")!.Instructions!.Contains("blijf_breed"));
            for (int i = 0; i < 10; i++) match.Step();
            Assert.DoesNotContain("blijf_breed", match.PerceptionOf(Side.Home, "RB")!.Instructions!);
        }
    }
    [Fact]
    public void MarkingRadiusStartsAtPlayerRatherThanFormationAnchor()
    {
        var v = View("dek_man", GamePhase.Verdedigen) with { MarkId = "O", FormationTarget = new(-30, -25),
            VisiblePlayers = new[] { Opp(23, 24) } };
        Assert.Equal(new Vec2(21.5, 24), InstructionRules.MarkTarget(v, new()));
        Assert.Null(InstructionRules.MarkTarget(v with { Self = v.Self with { Pos = new(-30, -25) },
            FormationTarget = new(23, 24) }, new()));
    }

    [Fact]
    public void CentralWidthDoesNotChooseAnArbitraryFlank()
    {
        var v = View("blijf_breed");
        var central = Layout with { Y = .5 };
        Assert.Equal(v.FormationTarget, InstructionRules.Target(v, central, Rest, Team, .25, Vec2.Zero, new()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InsideDribbleShootsFirstAndStopsBeforeEitherGoalLine(bool positive)
    {
        double dir = positive ? 1 : -1;
        var v = View("dribbel_in") with { HasBall = true, AttackPositiveX = positive,
            Self = View("").Self with { Pos = new(40 * dir, 5 * dir) }, VisiblePlayers = Array.Empty<PlayerSnapshot>() };
        var brain = new PassingBrain(new(ClearPressureMeters: 0, CarryMaxGoalDistance: 0));
        Assert.Equal(IntentKind.Shoot, brain.Decide(v).Kind);
        var noShot = new PassingBrain(new(ClearPressureMeters: 0), new ShotConfig(MinGoalProbability: 1));
        var intent = noShot.Decide(v with { Self = v.Self with { Pos = new(49 * dir, 20 * dir) } });
        Assert.Equal(IntentKind.DribbleInside, intent.Kind);
        Assert.InRange(intent.Target.X * dir, 0, Pitch.HalfLength - 1);
    }

    [Fact]
    public void PhasePressingAlsoChasesAVisibleLooseBall()
    {
        var source = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var tactic = source with { Phases = source.Phases.ToDictionary(k => k.Key, k => k.Value with {
            Players = k.Value.Players.Select(p => p.Id == "RB" ? p with { Instructions = new[] { "start_pressing" } } : p).ToArray() }) };
        var match = Match.Create(new(MaxReactionSeconds: 0, Profile: new PlayerProfile(MaxSpeed: 0),
            Perception: new PerceptionConfig(FieldOfViewRadians: 2 * Math.PI, SightRange: 200)), tactic, tactic, 4);
        match.SetBall(new(0, 0, 2), Vec3.Zero);
        match.Step();
        var view = match.PerceptionOf(Side.Away, "RB")!;
        Assert.True(view.BallIsLoose);
        Assert.True(view.IsPresser);
        Assert.Equal(IntentKind.Press, new PassingBrain(new()).Decide(view).Kind);
    }

    [Fact]
    public void DefaultDefensiveLineStaysWithinEightMetersOfEveryDrawnTactic()
    {
        foreach (string path in Directory.GetFiles(TacticLoaderTests.TacticsDir(), "*.json"))
        {
            var tactic = TacticLoader.LoadFile(path);
            foreach (bool home in new[] { false, true })
            {
                var side = home ? Side.Home : Side.Away;
                var match = Match.Create(new(MaxReactionSeconds: 0, EnableBlockSwitches: false,
                    EnableTeamPhases: false, EnableFatigue: false), tactic, tactic, 4, (_, _) => new FormationBrain());
                if (home)
                {
                    var keeper = match.GetSnapshot().Players.Single(p => p.Team == Side.Away && p.IsKeeper);
                    match.SetBall(new(keeper.Pos.X, keeper.Pos.Y, 0), Vec3.Zero);
                    match.Step();
                    Assert.Equal(Side.Away, match.Possession);
                }
                double sum = 0;
                for (int tick = 0; tick < 700; tick++)
                {
                    match.SetBall(new(0, 0, 10), Vec3.Zero);
                    match.Step();
                    if (tick >= 600) sum += match.GetSnapshot().Players.Where(p => p.Team == side && !p.IsKeeper)
                        .Min(p => p.Pos.X * (home ? 1 : -1));
                }
                double drawn = (tactic.Phase(GamePhase.Verdedigen).Players.Where(p => p.Id != "GK").Min(p => p.X) - .5) * Pitch.Length;
                Assert.True(Math.Abs(sum / 100 - drawn) <= 8, $"{Path.GetFileName(path)} {side}: actual {sum / 100:F2}, drawn {drawn:F2}");
            }
        }
    }

}
