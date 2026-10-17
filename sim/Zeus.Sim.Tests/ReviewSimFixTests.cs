using Zeus.Sim;
namespace Zeus.Sim.Tests;

// Regression tests for the independent review of the simulation (docs/hervatten.md, 'Review-fixes simulatie').
public class ReviewSimFixTests
{
    private static string Json => File.ReadAllText(TacticLoaderTests.TacticPath("laag_blok_442"));
    private static Squad Squad(string level) => SquadLoader.LoadFile(SquadTests.SquadPath(level));

    private static Tactic PathTactic(string phase, (double, double)[] points, string speed = "joggen", double wait = 0)
    {
        var model = TacticEditorModel.Parse(Json);
        model.SetPath(phase, "LB", points, speed, wait);
        return TacticLoader.Parse(model.ToJson());
    }

    // 1. A path-only trigger action (no instruction) used to put a null instruction in PendingTriggers when levels are active.
    private static string PathOnlyTriggerRun(ulong seed, string level)
    {
        var tactic = PathTactic("balwinst", [(.32, .15), (.36, .25)]);
        var phases = tactic.Phases.ToDictionary(kv => kv.Key, kv => new PhaseLayout(kv.Value.Block,
            kv.Value.Players.Select(p => p.Id == "RS" ? p with { X = .85, FollowBall = null } : p).ToArray()));
        tactic = tactic with { Phases = phases, Triggers = new[] { new Trigger("pad", GamePhase.Balwinst, TriggerRules.WinInHighZone, null,
            new[] { new TriggerAction("LB", null, Path: true) }) } };
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0, EnableFatigue: false, Instructions: InstructionConfig.Disabled),
            tactic, TacticLoader.Parse(Json), seed, (_, _) => new FormationBrain(), homeSquad: Squad(level), awaySquad: Squad(level));
        var receiver = match.GetSnapshot().Players.First(p => p.Team == Side.Away && !p.IsKeeper);
        match.SetBall(new Vec3(receiver.Pos.X, receiver.Pos.Y, 0), Vec3.Zero);
        match.Step();
        for (int i = 0; i < 150; i++) { match.SetBall(new Vec3(0, 0, 20), Vec3.Zero); match.Step(); }
        var home = match.GetSnapshot().Players.First(p => p.Team == Side.Home && p.Pos.X > 17.5);
        match.SetBall(new Vec3(home.Pos.X, home.Pos.Y, 0), Vec3.Zero);
        for (int i = 0; i < 400; i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.PathStart && e.PlayerId == "LB" && e.Outcome == "trigger");
        return match.EventHash();
    }

    [Theory]
    [InlineData("amateur")]
    [InlineData("halfprof")]
    [InlineData("prof")]
    public void PathOnlyTriggerWithLevelsDoesNotThrowAndIsDeterministic(string level)
    {
        for (ulong seed = 1; seed <= 12; seed++)
            Assert.Equal(PathOnlyTriggerRun(seed, level), PathOnlyTriggerRun(seed, level));
    }

    // 2. A substitute must get his own effective (compressed) attributes, not those of the player who leaves.
    [Fact]
    public void SubstituteGetsHisOwnEffectiveAttributes()
    {
        var match = SubstitutionTests.Create();
        SubstitutionTests.RunToRest(match);
        var before = match.EffectiveAttributesOf(Side.Home, "LCM");
        Assert.NotNull(before);
        var bench = match.BenchOf(Side.Home).First(b => b.Role != "keeper" && !Equals(Match.EffectiveOf(b.Attributes), before));
        match.Substitute(Side.Home, "LCM", bench.Id);
        var after = match.EffectiveAttributesOf(Side.Home, "LCM");
        Assert.Equal(Match.EffectiveOf(bench.Attributes), after);
        Assert.NotEqual(before, after);
    }

    // 3. A player on a walking path who becomes the ball carrier leaves the path; a path is capped in total time.
    [Fact]
    public void PathPlayerWhoGetsTheBallLeavesThePathAndCanPlay()
    {
        var tactic = PathTactic("opbouw", [(.32, .15), (.36, .25), (.30, .30)], "wandelen", 2);
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0, EnableFatigue: false), tactic, TacticLoader.Parse(Json), 42);
        for (int i = 0; i < 5; i++) { match.SetBall(new Vec3(0, 0, 20), Vec3.Zero); match.Step(); }
        var lb = match.GetSnapshot().Players.First(p => p.Team == Side.Home && p.Id == "LB");
        match.SetBall(new Vec3(lb.Pos.X, lb.Pos.Y, 0), Vec3.Zero);
        for (int i = 0; i < 400; i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.PathEnd && e.PlayerId == "LB" && e.Outcome == "ball");
        Assert.Contains(match.Events.Events, e => e.Type == EventType.Pass && e.Team == Side.Home && e.PlayerId == "LB" && e.Tick > 5);
    }

    [Fact]
    public void PathHasAnUpperBoundOnItsTotalTime()
    {
        var far = new (double, double)[] { (.05, .05), (.95, .95), (.05, .05), (.95, .95), (.05, .05), (.95, .95), (.05, .05), (.95, .95) };
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0, EnableFatigue: false, Instructions: InstructionConfig.Disabled),
            PathTactic("opbouw", far, "wandelen", 5), TacticLoader.Parse(Json), 42, (_, _) => new FormationBrain());
        int steps = (int)((Match.MaxPathSeconds + 5) / 0.1);
        for (int i = 0; i < steps; i++) { match.SetBall(new Vec3(0, 0, 20), Vec3.Zero); match.Step(); }
        var end = Assert.Single(match.Events.Events, e => e.Type == EventType.PathEnd && e.PlayerId == "LB");
        Assert.Equal("timeout", end.Outcome);
        Assert.InRange(end.Seconds, Match.MaxPathSeconds - 1, Match.MaxPathSeconds + 1);
    }

    // 5. Parallel.For exceptions are unwrapped; cancellation stays an OperationCanceledException.
    [Fact]
    public void ChallengePlayThrowsTheRealExceptionAndKeepsCancellation()
    {
        var spec = new ChallengeSpec("p", "P", "", "man_om_man_433.json", 2, new[] { new ChallengeCriterion("winst", ">=", 0) }, "");
        var player = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var opponent = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433"));
        var error = Assert.Throws<InvalidOperationException>(() => ChallengeEvaluator.Play(spec, player, opponent, stepLimit: 300));
        Assert.Contains("niet afgerond", error.Message);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => ChallengeEvaluator.Play(spec, player, opponent, cts.Token));
    }

    [Fact]
    public void PlayRoundThrowsTheRealException()
    {
        string tactics = TacticLoaderTests.TacticsDir(), squads = Path.Combine(Path.GetDirectoryName(tactics)!, "squads");
        Assert.Throws<InvalidOperationException>(() => Competition.PlayRound(new SeasonState(), tactics, squads, stepLimit: 300));
    }

    // 6. The progress callback never goes backwards.
    [Fact]
    public void ProgressNeverDecreases()
    {
        var spec = new ChallengeSpec("p", "P", "", "man_om_man_433.json", 4, new[] { new ChallengeCriterion("winst", ">=", 0) }, "");
        var player = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        var opponent = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433"));
        var seen = new List<double>();
        Assert.Throws<InvalidOperationException>(() => ChallengeEvaluator.Play(spec, player, opponent, progress: v => seen.Add(v), stepLimit: 4000));
        Assert.NotEmpty(seen);
        for (int i = 1; i < seen.Count; i++) Assert.True(seen[i] >= seen[i - 1], $"progress went back at {i}: {seen[i - 1]} -> {seen[i]}");
    }

    // 7. A recorded command must not change when the caller reuses its lists.
    [Fact]
    public void RecordedAdjustmentIsACopy()
    {
        var match = SubstitutionTests.Create();
        var add = new List<string> { "blijf_achter" };
        var instructions = new List<InstructionChange> { new("RB", Add: add) };
        match.Adjust(Side.Home, new TacticAdjustment(Instructions: instructions));
        add.Clear(); instructions.Clear();
        var recorded = Assert.Single(match.Commands).Adjustment!;
        Assert.Equal(["blijf_achter"], Assert.Single(recorded.Instructions!).Add);
    }

    // Second review (docs/hervatten.md, 'Review-fixes simulatie ronde 2').
    private static Perception DefendingView(Vec2 formation, Vec3 ball) => new(0,
        new PlayerSnapshot(Side.Home, "P", formation, Vec2.Zero, 0), GamePhase.Verdedigen, false, formation,
        Array.Empty<PlayerSnapshot>(), new BallSnapshot(ball, Vec3.Zero), new BallObservation(0, new(ball, Vec3.Zero)));

    // R2-1. A ball deep in the own half shifted the whole defending shape without bound onto the own goal line.
    [Fact]
    public void DeepBallDoesNotPinOutfieldPlayersOnTheGoalLine()
    {
        var layout = new PhasePlayer("P", .2, .5, null, Array.Empty<string>(), null);
        var team = new TeamSettings(.5, .5, "zone", .25, null);
        foreach (double formationX in new[] { -30.0, -20, -5, 15 })
        {
            var target = InstructionRules.Target(DefendingView(new(formationX, 0), new(-50, 3, 0)), layout, layout, team, .25,
                Vec2.Zero, new());
            Assert.True(target.X >= -Pitch.HalfLength + 2 - 1e-9, $"{formationX}: {target.X}");
        }
    }

    // R2-2. A hand-built tactic whose phases hold different player ids (the loader always gives all 11 per phase) threw an
    // InvalidOperationException mid-match in LayoutFor/TargetFor; Match.Create now rejects it up front.
    [Fact]
    public void PhasesWithDifferentPlayerIdsAreRejectedAtCreate()
    {
        var tactic = TacticLoader.Parse(Json);
        var phases = tactic.Phases.ToDictionary(kv => kv.Key, kv => kv.Key != GamePhase.Aanval ? kv.Value
            : new PhaseLayout(kv.Value.Block, kv.Value.Players.Select(p => p.Id == "LB" ? p with { Id = "XX" } : p).ToArray()));
        Assert.Throws<ArgumentException>(() => Match.Create(new MatchConfig(), tactic with { Phases = phases }, TacticLoader.Parse(Json), 42));
    }

    // R2-7. A path that is active when a goal is scored must not survive into the kick-off.
    [Fact]
    public void GoalEndsAnActivePathAtTheSameTick()
    {
        var tactic = PathTactic("opbouw", [(.32, .15), (.36, .25), (.30, .30)], "wandelen", 2);
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0, EnableFatigue: false, Instructions: InstructionConfig.Disabled),
            tactic, TacticLoader.Parse(Json), 42, (_, _) => new FormationBrain());
        for (int i = 0; i < 5; i++) { match.SetBall(new Vec3(0, 0, 20), Vec3.Zero); match.Step(); }
        Assert.Contains(match.Events.Events, e => e.Type == EventType.PathStart && e.PlayerId == "LB");
        for (int i = 0; i < 60 && !match.Events.Events.Any(e => e.Type == EventType.Goal); i++)
        {
            match.SetBall(new Vec3(Pitch.HalfLength - .3, 0, .5), new Vec3(20, 0, 0));
            match.Step();
        }
        var goal = match.Events.Events.First(e => e.Type == EventType.Goal);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.PathEnd && e.PlayerId == "LB" && e.Team == Side.Home && e.Tick == goal.Tick);
    }
}
