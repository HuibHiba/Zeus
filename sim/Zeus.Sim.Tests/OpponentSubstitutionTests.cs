using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class OpponentSubstitutionTests
{
    private static readonly OpponentSubstitutionConfig Early = new(FatigueMinute: 0, EnergyThreshold: 1);

    [Fact]
    public void FullMatchIsDeterministicFinishedAndReplaysRecordedCommands()
    {
        var config = new MatchConfig();
        string json = File.ReadAllText(TacticLoaderTests.TacticPath("laag_blok_442"));
        var tactic = TacticLoader.Parse(json);
        Match Play(bool enabled)
        {
            var match = Match.Create(config, tactic, tactic, 5);
            // Keep the production start minute, but make the fatigue trigger certain for this fixture.
            var policy = new OpponentSubstitutionPolicy(new(EnergyThreshold: .99));
            long previousRest = -1;
            for (int i = 0; i < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); i++)
            {
                if (enabled && match.IsRestMoment)
                {
                    var before = match.GetSnapshot();
                    long rest = match.Events.Events.LastOrDefault(e => e.Type == EventType.RestartAwarded)?.Tick ?? 0;
                    if (policy.TrySubstitute(match, Side.Away))
                    {
                        Assert.NotEqual(previousRest, rest);
                        previousRest = rest;
                        Assert.True(before.TimeSeconds >= 3600);
                        var outgoing = before.Players.Single(p => p.Team == Side.Away && p.Id == match.Events.Events.Last().PlayerId);
                        Assert.False(outgoing.IsKeeper);
                        Assert.False(outgoing.SentOff);
                    }
                }
                match.Step();
            }
            Assert.Equal(MatchStatus.Finished, match.Status);
            return match;
        }
        var first = Play(true);
        var second = Play(true);
        Assert.Equal(3, first.SubstitutionsUsed(Side.Away));
        Assert.Equal(first.Commands, second.Commands);
        Assert.Equal(first.EventHash(), second.EventHash());
        Assert.Equal(first.SnapshotHash(), second.SnapshotHash());
        Assert.Equal(3, first.Events.Events.Count(e => e.Type == EventType.Substitution));
        var replay = ReplayFile.Capture(config, json, json, 5, (int)first.Tick, first.EventHash(), commands: first.Commands);
        var replayed = replay.Run();
        Assert.Equal(first.SnapshotHash(), replayed.SnapshotHash());
        Assert.Equal(first.EventHash(), replayed.EventHash());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void RespectsMatchAndPolicyLimitsAndOnePerRest(int limit)
    {
        var match = SubstitutionTests.Create(config: new MatchConfig(HalfSeconds: 100, MaxSubstitutions: limit));
        var policy = new OpponentSubstitutionPolicy(Early);
        for (int i = 0; i < 10000 && match.Status != MatchStatus.Finished; i++)
        {
            if (match.IsRestMoment)
            {
                policy.TrySubstitute(match, Side.Away);
                Assert.False(new OpponentSubstitutionPolicy(Early).TrySubstitute(match, Side.Away));
            }
            match.Step();
        }
        Assert.Equal(Math.Min(limit, 3), match.SubstitutionsUsed(Side.Away));
        Assert.Equal("GK", match.SquadIdOf(Side.Away, "GK"));
    }

    [Fact]
    public void PicksMostTiredEligiblePlayerAndSameRoleGroup()
    {
        var match = SubstitutionTests.Create();
        for (int i = 0; i < 1000; i++) match.Step();
        SubstitutionTests.RunToRest(match);
        var before = match.GetSnapshot();
        var tired = before.Players.Where(p => p.Team == Side.Away && !p.IsKeeper && !p.SentOff)
            .OrderBy(p => p.LongEnergy).ThenBy(p => p.Id, StringComparer.Ordinal).First();
        // Provide reserves of every broad group so the most tired player always has a replacement.
        var bench = (List<SquadPlayer>)match.BenchOf(Side.Away);
        var role = match.TacticOf(Side.Away).Roster.Single(p => p.Id == tired.Id).Role;
        bench[1] = bench[1] with { Role = role, Keeper = null };
        Assert.True(new OpponentSubstitutionPolicy(Early).TrySubstitute(match, Side.Away));
        Assert.Equal(tired.Id, match.Events.Events.Last().PlayerId);
        Assert.Equal("B2", match.SquadIdOf(Side.Away, tired.Id));
    }

    [Fact]
    public void EmptyBenchAndKeeperOnlyBenchDoNothing()
    {
        var match = SubstitutionTests.Create();
        for (int i = 0; i < 1000; i++) match.Step();
        SubstitutionTests.RunToRest(match);
        var bench = (List<SquadPlayer>)match.BenchOf(Side.Away);
        bench.RemoveAll(p => p.Keeper is null);
        var policy = new OpponentSubstitutionPolicy(Early);
        Assert.False(policy.TrySubstitute(match, Side.Away));
        bench.Clear();
        Assert.False(policy.TrySubstitute(match, Side.Away));
        Assert.Empty(match.Commands);
    }

    [Fact]
    public void LateDeficitPrioritisesAttackerAndExcludesSentOffAndEmergencyKeeper()
    {
        var match = SubstitutionTests.Create();
        // Controlled boundary fixture: no incident or score manipulation is used in the replay test.
        typeof(Match).GetField("_homeScore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(match, 1);
        typeof(Match).GetField("_tick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(match, 42000L);
        match.ReportIncident(Side.Away, "GK", Vec2.Zero, new RefereeIncident(ContactAction.Charge, Conduct.ExcessiveForce, true, true,
            TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true));
        var before = match.GetSnapshot();
        var policy = new OpponentSubstitutionPolicy();
        Assert.True(policy.TrySubstitute(match, Side.Away));
        var command = Assert.Single(match.Commands);
        var outgoing = before.Players.Single(p => p.Team == Side.Away && p.Id == command.OutPlayerId);
        Assert.False(outgoing.IsKeeper);
        Assert.False(outgoing.SentOff);
        Assert.Contains(match.TacticOf(Side.Away).Roster.Single(p => p.Id == outgoing.Id).Role,
            new[] { "back", "centrale_verdediger", "wingback" });
        Assert.Contains(match.SquadPlayerOf(Side.Away, outgoing.Id)!.Role, new[] { "spits", "targetman", "buitenspeler" });
    }

    [Fact]
    public void DefaultsWaitAndPolicyNeverActsDuringPlayOrAfterFinish()
    {
        var match = SubstitutionTests.Create(config: new MatchConfig(HalfSeconds: 20));
        var early = new OpponentSubstitutionPolicy(Early);
        Assert.False(new OpponentSubstitutionPolicy().TrySubstitute(match, Side.Away));
        SubstitutionTests.RunToRest(match);
        SubstitutionTests.RunToPlay(match);
        Assert.False(early.TrySubstitute(match, Side.Away));
        for (int i = 0; i < 3000 && match.Status != MatchStatus.Finished; i++) match.Step();
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.False(early.TrySubstitute(match, Side.Away));
        Assert.Empty(match.Commands);
    }

    [Fact]
    public void ChallengeDefaultsEqualExplicitDisabledAndDirectMatch()
    {
        var tactic = SubstitutionTests.Tactic();
        var spec = new ChallengeSpec("x", "X", "", "laag_blok_442.json", 1, [new("winst", ">=", 0)], "");
        var baseline = Match.Create(new MatchConfig(), tactic, tactic, 1);
        for (int i = 0; i < 150000 && baseline.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); i++) baseline.Step();
        var expected = ChallengeEvaluator.ToResult(MatchStatistics.FromMatch(baseline));
        Assert.Equal(expected, Assert.Single(ChallengeEvaluator.Play(spec, tactic, tactic, maxParallel: 1)));
        Assert.Equal(expected, Assert.Single(ChallengeEvaluator.Play(spec, tactic, tactic, maxParallel: 1, opponentSubstitutions: false)));
    }

    [Fact]
    public void ChallengeEnabledUsesTheSamePolicyAsTheDirectLoop()
    {
        var tactic = SubstitutionTests.Tactic();
        var spec = new ChallengeSpec("x", "X", "", "laag_blok_442.json", 1, [new("winst", ">=", 0)], "");
        var match = Match.Create(new MatchConfig(), tactic, tactic, 1);
        var policy = new OpponentSubstitutionPolicy();
        for (int i = 0; i < 150000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); i++)
        {
            if (match.IsRestMoment) policy.TrySubstitute(match, Side.Away);
            match.Step();
        }
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal(ChallengeEvaluator.ToResult(MatchStatistics.FromMatch(match)),
            Assert.Single(ChallengeEvaluator.Play(spec, tactic, tactic, maxParallel: 1, opponentSubstitutions: true)));
    }

    [Fact]
    public void CompetitionDefaultsEqualDisabledAndEnabledShortRoundCompletes()
    {
        string tactics = Path.GetDirectoryName(TacticLoaderTests.TacticPath("laag_blok_442"))!;
        string squads = Path.GetDirectoryName(SquadTests.SquadPath("amateur"))!;
        var config = new MatchConfig(HalfSeconds: 5);
        var first = new SeasonState();
        var second = new SeasonState();
        var enabled = new SeasonState();
        Competition.PlayRound(first, tactics, squads, config: config);
        Competition.PlayRound(second, tactics, squads, config: config, opponentSubstitutions: false);
        Competition.PlayRound(enabled, tactics, squads, config: config, opponentSubstitutions: true);
        Assert.Equal(first.ToJson(), second.ToJson());
        // No policy trigger before minute 60, even with the flag enabled.
        Assert.Equal(first.ToJson(), enabled.ToJson());
        Assert.Equal(1, enabled.Round);
    }
}
