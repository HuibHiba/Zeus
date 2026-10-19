using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class ReplayCommandTests
{
    private const string TacticName = "laag_blok_442";
    // The scripted substitutions need the pre-round-3 scenario (in round 3 RS is sent off first; docs/kalibratie.md).
    private static readonly MatchConfig Config = new(HalfSeconds: 600, PositionJogSeconds: 0, DefendingBallFollowGain: 0.5,
        Passing: new PassConfig(ForwardWeight: 0.06, DistanceWeight: 0.015));

    private static string Json(string name) => File.ReadAllText(TacticLoaderTests.TacticPath(name));
    private static string SquadJson(string name) => File.ReadAllText(SquadTests.SquadPath(name));

    private static Match Create(ulong seed) => Match.Create(Config, TacticLoader.Parse(Json(TacticName)), TacticLoader.Parse(Json(TacticName)), seed,
        homeSquad: SquadLoader.Parse(SquadJson("prof")), awaySquad: SquadLoader.Parse(SquadJson("amateur")));

    // Plays a match with commands at tick 0, at rest moments after ~1500 and ~4000 ticks and one at the very end.
    private static (Match Match, int Steps) PlayWithCommands(ulong seed)
    {
        var match = Create(seed);
        match.Adjust(Side.Home, new TacticAdjustment(Compactness: .8, Tempo: .3));
        for (int i = 0; i < 1500; i++) match.Step();
        SubstitutionTests.RunToRest(match);
        match.Substitute(Side.Home, "LCM", "B3");
        match.Adjust(Side.Away, new TacticAdjustment(Instructions: [new InstructionChange("RB", Add: ["blijf_achter"])]));
        for (int i = 0; i < 2500; i++) match.Step();
        SubstitutionTests.RunToRest(match);
        match.Substitute(Side.Away, "RS", "B4");
        for (int i = 0; i < 200; i++) match.Step();
        SubstitutionTests.RunToRest(match);
        match.Substitute(Side.Home, "RB", "B2"); // after the last step
        return (match, (int)match.Tick);
    }

    private static ReplayFile Capture(Match match, int steps, ulong seed) => ReplayFile.Capture(Config, Json(TacticName), Json(TacticName), seed, steps,
        match.EventHash(), SquadJson("prof"), SquadJson("amateur"), match.Commands);

    [Fact]
    public void SameSeedAndSameCommandsGiveTheSameEventHash()
    {
        var (a, _) = PlayWithCommands(21);
        var (b, _) = PlayWithCommands(21);
        Assert.Equal(a.EventHash(), b.EventHash());
        Assert.Equal(a.SnapshotHash(), b.SnapshotHash());
        Assert.Equal(5, a.Commands.Count);
    }

    [Fact]
    public void ReplayWithCommandsReproducesTheEventHashAfterSaveAndLoad()
    {
        var (match, steps) = PlayWithCommands(33);
        Assert.Equal(5, match.Commands.Count);
        var replay = Capture(match, steps, 33);
        Assert.Equal(2, replay.FormatVersion);
        string path = Path.Combine(Path.GetTempPath(), "zeus_cmd_replay_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            replay.Save(path);
            var loaded = ReplayFile.Load(path);
            var rerun = loaded.Run(); // Run verifies the recorded event hash
            Assert.Equal(match.EventHash(), rerun.EventHash());
            Assert.Equal(match.SnapshotHash(), rerun.SnapshotHash());
            Assert.Equal(match.Commands.Count, rerun.Commands.Count);
            Assert.Equal(match.Commands.Select(c => (c.Tick, c.Team, c.Kind, c.OutPlayerId, c.InPlayerId)),
                rerun.Commands.Select(c => (c.Tick, c.Team, c.Kind, c.OutPlayerId, c.InPlayerId)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ReplayWithoutTheCommandsDoesNotReproduceTheHash()
    {
        var (match, steps) = PlayWithCommands(33);
        var stripped = Capture(match, steps, 33) with { FormatVersion = 1, Commands = null };
        Assert.Throws<InvalidOperationException>(() => stripped.Run());
    }

    [Fact]
    public void CommandsChangeTheOutcomeComparedWithTheSameSeedWithoutThem()
    {
        var (withCommands, steps) = PlayWithCommands(8);
        var plain = Create(8);
        for (int i = 0; i < steps; i++) plain.Step();
        Assert.NotEqual(plain.EventHash(), withCommands.EventHash());
    }

    [Fact]
    public void ReplayWithoutCommandsStaysVersionOneAndOldReplaysStayReadable()
    {
        var match = Create(5);
        for (int i = 0; i < 200; i++) match.Step();
        var replay = Capture(match, 200, 5);
        Assert.Equal(1, replay.FormatVersion);
        Assert.Null(replay.Commands);
        // A replay file written before Commands existed has no such field.
        var json = System.Text.Json.JsonSerializer.Serialize(replay);
        Assert.Equal(match.EventHash(), System.Text.Json.JsonSerializer.Deserialize<ReplayFile>(json)!.Run().EventHash());
    }

    [Fact]
    public void InvalidCommandTimelinesAreRejected()
    {
        var (match, steps) = PlayWithCommands(4);
        var replay = Capture(match, steps, 4);
        Assert.Throws<ArgumentException>(() => (replay with { Commands = replay.Commands!.Reverse().ToArray() }).Run());
        Assert.Throws<ArgumentException>(() => (replay with { Steps = 10 }).Run());
        Assert.Throws<ArgumentException>(() => (replay with { FormatVersion = 1 }).Run());
        Assert.Throws<ArgumentException>(() => (replay with { Commands = [new MatchCommand(0, Side.Home, "hax")] }).Run());
    }

    [Fact]
    public void CommandAtTheWrongTickIsRejectedByApply()
    {
        var match = Create(4);
        Assert.Throws<MatchCommandException>(() => match.Apply(new MatchCommand(5, Side.Home, MatchCommand.SubstituteKind, "RB", "B2")));
    }

    [Fact]
    public void UndefinedTeamAndMismatchedPayloadAreMatchCommandExceptions()
    {
        var match = Create(4);
        Assert.Throws<MatchCommandException>(() => match.Apply(new MatchCommand(0, (Side)7, MatchCommand.SubstituteKind, "RB", "B2")));
        Assert.Throws<MatchCommandException>(() => match.Apply(new MatchCommand(0, Side.Home, MatchCommand.SubstituteKind)));
        Assert.Throws<MatchCommandException>(() => match.Apply(new MatchCommand(0, Side.Home, MatchCommand.SubstituteKind, "RB", "B2",
            new TacticAdjustment(Tempo: .5))));
        Assert.Throws<MatchCommandException>(() => match.Apply(new MatchCommand(0, Side.Home, MatchCommand.AdjustKind, "RB", "B2",
            new TacticAdjustment(Tempo: .5))));
        Assert.Throws<MatchCommandException>(() => match.Apply(new MatchCommand(0, Side.Home, MatchCommand.AdjustKind)));
        Assert.Empty(match.Commands);
    }

    [Fact]
    public void StepsHaveAnUpperBoundAndABadCommandInAReplayIsACommandException()
    {
        var (match, steps) = PlayWithCommands(4);
        var replay = Capture(match, steps, 4);
        Assert.Throws<ArgumentException>(() => (replay with { Steps = ReplayFile.MaxSteps + 1 }).Run());
        Assert.Throws<MatchCommandException>(() => (replay with { Commands = [new MatchCommand(0, (Side)9, MatchCommand.SubstituteKind, "RB", "B2")] }).Run());
    }
}
