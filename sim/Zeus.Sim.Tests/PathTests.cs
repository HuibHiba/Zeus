using System.Text.Json.Nodes;

namespace Zeus.Sim.Tests;

public class PathTests
{
    private static string Json => File.ReadAllText(TacticLoaderTests.TacticPath("laag_blok_442"));
    private static JsonObject Root() => JsonNode.Parse(Json)!.AsObject();
    private static JsonNode Entry(JsonObject root) => root["fases"]!["opbouw"]!["spelers"]!.AsArray().First(p => p!["id"]!.GetValue<string>() == "LB")!;
    private static readonly (double X, double Y)[] Points = { (.32, .15), (.36, .25), (.30, .30) };

    private static Tactic WithPath(string phase = "opbouw", string speed = "joggen", double wait = 0)
    {
        var model = TacticEditorModel.Parse(Json);
        model.SetPath(phase, "LB", Points, speed, wait);
        return TacticLoader.Parse(model.ToJson());
    }

    private static Match Make(Tactic tactic, MatchConfig? config = null) => Match.Create(config ?? new MatchConfig(
        MaxReactionSeconds: 0, EnableFatigue: false, Instructions: InstructionConfig.Disabled), tactic,
        TacticLoader.Parse(Json), 42, (_, _) => new FormationBrain());

    private static PlayerSnapshot Player(Match match) => match.GetSnapshot().Players.First(p => p.Team == Side.Home && p.Id == "LB");

    // Keep the ball high and central so this test exercises movement without possession changes or restarts.
    private static void Step(Match match)
    {
        match.SetBall(new Vec3(0, 0, 20), Vec3.Zero);
        match.Step();
    }

    [Fact]
    public void VisitsPointsInOrderWaitsAndReturnsExactlyOnce()
    {
        var tactic = WithPath(wait: .5);
        var match = Make(tactic);
        int next = 0;
        var arrival = new List<long>();
        var departure = new List<long>();
        bool atPoint = false;
        for (int i = 0; i < 1000; i++)
        {
            Step(match);
            if (next < Points.Length)
            {
                var target = Pitch.ToMeters(Points[next].X, Points[next].Y, Side.Home);
                bool near = (Player(match).Pos - target).Length <= .02;
                if (near && !atPoint) { arrival.Add(match.Tick); atPoint = true; }
                if (!near && atPoint) { departure.Add(match.Tick); next++; atPoint = false; }
            }
            if (match.Events.Events.Any(e => e.Type == EventType.PathEnd && e.PlayerId == "LB" && e.Outcome == "completed")) break;
        }
        Assert.Equal(Points.Length, next);
        Assert.All(departure.Zip(arrival), pair => Assert.True(pair.First - pair.Second >= 5));
        var layout = tactic.Phase(GamePhase.Opbouw).Players.First(p => p.Id == "LB");
        Assert.True((Player(match).Pos - Pitch.ToMeters(layout.X, layout.Y, Side.Home)).Length <= .02);
        for (int i = 0; i < 100; i++) Step(match);
        Assert.Single(match.Events.Events.Where(e => e.Type == EventType.PathStart && e.Team == Side.Home && e.PlayerId == "LB"));
        Assert.Single(match.Events.Events.Where(e => e.Type == EventType.PathEnd && e.Team == Side.Home && e.PlayerId == "LB"));
    }

    [Fact]
    public void NoPathIsBitIdenticalWithSwitchOnOrOff()
    {
        var tactic = TacticLoader.Parse(Json);
        var on = Make(tactic, new MatchConfig());
        var off = Make(tactic, new MatchConfig(EnablePaths: false));
        for (int i = 0; i < 1000; i++)
        {
            on.Step(); off.Step();
            Assert.Equal(on.GetSnapshot(), off.GetSnapshot());
        }
        Assert.Equal(on.Events.Hash(), off.Events.Hash());
        Assert.DoesNotContain(on.Events.Events, e => e.Type is EventType.PathStart or EventType.PathEnd);
    }

    [Fact]
    public void PathsAreDeterministicAndStayOnPitch()
    {
        var a = Make(WithPath());
        var b = Make(WithPath());
        for (int i = 0; i < 800; i++)
        {
            Step(a); Step(b);
            Assert.Equal(a.GetSnapshot(), b.GetSnapshot());
            Assert.InRange(Player(a).Pos.X, -Pitch.HalfLength, Pitch.HalfLength);
            Assert.InRange(Player(a).Pos.Y, -Pitch.HalfWidth, Pitch.HalfWidth);
        }
        Assert.Equal(a.Events.Hash(), b.Events.Hash());
    }

    [Fact]
    public void PhaseChangeAbortsTheOldPath()
    {
        var match = Make(WithPath());
        Step(match);
        match.SetBall(new Vec3(30, 0, 20), Vec3.Zero);
        match.Step();
        Assert.Equal(GamePhase.Aanval, match.PhaseOf(Side.Home));
        Assert.Contains(match.Events.Events, e => e.Type == EventType.PathEnd && e.PlayerId == "LB" && e.Outcome == "phase changed");
    }

    [Theory]
    [InlineData("wandelen", 2)]
    [InlineData("joggen", 4)]
    [InlineData("sprint", 7.8)]
    public void SpeedCapIsRespected(string speed, double cap)
    {
        var match = Make(WithPath(speed: speed));
        for (int i = 0; i < 20; i++) { Step(match); Assert.InRange(Player(match).Vel.Length, 0, cap + 1e-9); }
    }

    [Theory]
    [InlineData("[]", "pad")]
    [InlineData("[[1.1,0.5]]", "outside")]
    [InlineData("[[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0]]", "1..8")]
    [InlineData("[[0.5]]", "two numbers")]
    public void LoaderRejectsInvalidPoints(string points, string message)
    {
        var root = Root(); Entry(root)["pad"] = JsonNode.Parse(points);
        Assert.Contains(message, Assert.Throws<TacticLoadException>(() => TacticLoader.Parse(root.ToJsonString())).Message);
    }

    [Theory]
    [InlineData("pad_snelheid", "\"vliegen\"")]
    [InlineData("pad_wacht", "-1")]
    [InlineData("pad_wacht", "5.1")]
    public void LoaderRejectsInvalidOptions(string key, string value)
    {
        var root = Root(); Entry(root)[key] = JsonNode.Parse(value);
        Assert.Contains(key, Assert.Throws<TacticLoadException>(() => TacticLoader.Parse(root.ToJsonString())).Message);
    }

    [Fact]
    public void EditorRoundTripsDetachesAliasesAndRollsBack()
    {
        var model = TacticEditorModel.Parse(Json);
        model.SetPath("aanval", "LB", Points);
        Assert.False(model.IsAlias("aanval"));
        Assert.Empty(model.GetPath("opbouw", "LB"));
        Assert.Equal(Points, model.GetPath("aanval", "LB"));
        Assert.Equal(Points, TacticEditorModel.Parse(model.ToJson()).GetPath("aanval", "LB"));
        var before = model.ToJson();
        Assert.Throws<TacticLoadException>(() => model.SetPath("opbouw", "LB", Points, "vliegen"));
        Assert.Equal(before, model.ToJson());
        Assert.Throws<ArgumentException>(() => model.SetPath("aanval", "LB", new[] { (double.NaN, .5) }));
        Assert.Equal(before, model.ToJson());
        model.ClearPath("aanval", "LB");
        Assert.Empty(model.GetPath("aanval", "LB"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("start_pressing")]
    public void TriggerCanRestartTheCurrentPhasePathAlongsideAnInstruction(string? instruction)
    {
        var tactic = WithPath("balwinst");
        var phases = tactic.Phases.ToDictionary(kv => kv.Key, kv => new PhaseLayout(kv.Value.Block,
            kv.Value.Players.Select(p => p.Id == "RS" ? p with { X = .85, FollowBall = null } : p).ToArray()));
        tactic = tactic with { Phases = phases };
        tactic = tactic with { Triggers = new[] { new Trigger("pad", GamePhase.Balwinst, TriggerRules.WinInHighZone, null,
            new[] { new TriggerAction("LB", instruction, Path: true) }) } };
        var match = Make(tactic);
        var receiver = match.GetSnapshot().Players.First(p => p.Team == Side.Away && !p.IsKeeper);
        match.SetBall(new Vec3(receiver.Pos.X, receiver.Pos.Y, 0), Vec3.Zero);
        match.Step();
        Assert.Equal(Side.Away, match.Possession);
        for (int i = 0; i < 150; i++) Step(match);
        // A home player on the attacking third receives the ball, causing Balwinst and its trigger.
        var home = match.GetSnapshot().Players.First(p => p.Team == Side.Home && p.Pos.X > 17.5);
        match.SetBall(new Vec3(home.Pos.X, home.Pos.Y, 0), Vec3.Zero);
        match.Step();
        Assert.Equal(Side.Home, match.Possession);
        Assert.Contains(match.Events.Events, e => e.Type == EventType.PathStart && e.PlayerId == "LB" && e.Outcome == "trigger");
    }

    [Fact]
    public void DisabledPathsIgnoreAllPathSettings()
    {
        var config = new MatchConfig(EnablePaths: false);
        var with = Make(WithPath(), config);
        var without = Make(TacticLoader.Parse(Json), config);
        for (int i = 0; i < 300; i++)
        {
            with.Step(); without.Step();
            Assert.Equal(with.GetSnapshot(), without.GetSnapshot());
        }
        Assert.Equal(with.Events.Hash(), without.Events.Hash());
    }

    [Fact]
    public void ActivePathIgnoresBallFollowAndMovementInstructions()
    {
        var model = TacticEditorModel.Parse(Json);
        model.SetPath("opbouw", "LB", Points);
        model.SetFollow("opbouw", "LB", 1, 1);
        var match = Make(TacticLoader.Parse(model.ToJson()), new MatchConfig(MaxReactionSeconds: 0));
        for (int i = 0; i < 10; i++)
        {
            match.SetBall(new Vec3(-15, 20, 20), Vec3.Zero);
            match.Step();
            Assert.Equal(Pitch.ToMeters(Points[0].X, Points[0].Y, Side.Home), match.TargetOf(Side.Home, "LB"));
        }
    }

    [Fact]
    public void AwayPathUsesMirroredCoordinatesAndAllowsBoundaryPoints()
    {
        var model = TacticEditorModel.Parse(Json);
        model.SetPath("verdedigen", "LB", new[] { (0.0, 1.0), (1.0, 0.0) });
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0), TacticLoader.Parse(Json),
            TacticLoader.Parse(model.ToJson()), 42, (_, _) => new FormationBrain());
        Step(match);
        Assert.Equal(Pitch.ToMeters(0, 1, Side.Away), match.TargetOf(Side.Away, "LB"));
    }

    [Fact]
    public void LoaderAcceptsPathOnlyAndCombinedTriggerActions()
    {
        var root = Root();
        root["triggers"] = JsonNode.Parse("""
            [{"wanneer":{"fase":"verdedigen","voorwaarde":"bal_naar_keeper"},
              "dan":[{"speler":"LB","pad":true},{"speler":"LB","pad":true,"instructie":"start_pressing"}]}]
            """);
        var trigger = Assert.Single(TacticLoader.Parse(root.ToJsonString()).Triggers);
        Assert.True(TriggerRules.IsSupported(trigger));
        Assert.All(trigger.Then, a => Assert.True(a.Path));
        root["triggers"]![0]!["dan"]![0]!["pad"] = false;
        Assert.Throws<TacticLoadException>(() => TacticLoader.Parse(root.ToJsonString()));
    }
}
