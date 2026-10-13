using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class MatchFatigueTests
{
    private sealed class PressBrain : IPlayerBrain
    {
        public List<long> DecisionTicks { get; } = new();
        public Intent Decide(Perception view)
        {
            DecisionTicks.Add(view.Tick);
            return new Intent(IntentKind.Press, view.Self.Pos);
        }
    }

    private sealed class PressThenRunBrain : IPlayerBrain
    {
        public Intent Decide(Perception view) => view.Tick < 20
            ? new Intent(IntentKind.Press, view.Self.Pos)
            : new Intent(IntentKind.MoveToPosition, new Vec2(view.Self.Team == Side.Home ? 52 : -52, view.Self.Pos.Y));
    }

    private static Match Make(MatchConfig config, ulong seed = 42, Func<Side, string, IPlayerBrain>? brain = null)
    {
        var match = Match.Create(config with { MaxReactionSeconds = 0, Duels = new DuelConfig(Reach: 0.00001) },
            TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
            TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), seed,
            brain ?? ((_, _) => new PressBrain()));
        // No live carrier/duels while measuring stationary exertion.
        match.SetBall(new Vec3(0, 20, 100), Vec3.Zero);
        return match;
    }

    private static FatigueConfig TestRates => new(ShortPressingRate: 0.05, LongPressingRate: 0.01);

    [Fact]
    public void PlayersStartFreshAndUnknownPlayerHasNoEnergy()
    {
        var match = Make(new MatchConfig());
        foreach (var player in match.GetSnapshot().Players)
            Assert.Equal(FatigueState.Fresh, match.FatigueOf(player.Team, player.Id));
        Assert.Null(match.FatigueOf(Side.Home, "unknown"));
    }

    [Fact]
    public void MatchConsumesEnergyForStationaryPressingAndDisabledModePreservesFreshState()
    {
        var enabled = Make(new MatchConfig(Fatigue: TestRates, Endurance: 0.5));
        var disabled = Make(new MatchConfig(Fatigue: TestRates, EnableFatigue: false));
        for (int i = 0; i < 20; i++) { enabled.Step(); disabled.Step(); }
        foreach (var player in enabled.GetSnapshot().Players)
        {
            var state = enabled.FatigueOf(player.Team, player.Id)!.Value;
            Assert.Equal(0.9, state.ShortEnergy, 10);
            Assert.Equal(0.98, state.LongEnergy, 10);
            Assert.Equal(FatigueState.Fresh, disabled.FatigueOf(player.Team, player.Id));
        }
    }

    [Fact]
    public void RestartPauseRecoversShortEnergyWithoutConsumingLongEnergy()
    {
        var match = Make(new MatchConfig(Fatigue: TestRates, RestartPauseSeconds: 3));
        for (int i = 0; i < 20; i++) match.Step();
        match.SetBall(new Vec3(52.6, 20, 0), new Vec3(10, 0, 0));
        match.Step();
        Assert.Equal(MatchStatus.Restart, match.Status);
        var before = match.FatigueOf(Side.Home, "GK")!.Value;
        match.Step();
        var after = match.FatigueOf(Side.Home, "GK")!.Value;
        Assert.Equal(before.LongEnergy, after.LongEnergy);
        Assert.True(after.ShortEnergy > before.ShortEnergy);
        Assert.InRange(after.ShortEnergy, 0, after.LongEnergy);
    }

    [Fact]
    public void HalfTimeRefillsShortReservoirOnly()
    {
        var match = Make(new MatchConfig(Fatigue: TestRates, HalfSeconds: 2, RestartPauseSeconds: 0));
        for (int i = 0; i < 19; i++) match.Step();
        var before = match.FatigueOf(Side.Home, "GK")!.Value;
        Assert.True(before.ShortEnergy < before.LongEnergy);
        match.Step();
        Assert.Equal(2, match.Period);
        var after = match.FatigueOf(Side.Home, "GK")!.Value;
        Assert.Equal(after.LongEnergy, after.ShortEnergy);
        Assert.Equal(before.LongEnergy - 0.001, after.LongEnergy, 10);
        Assert.True(after.LongEnergy < 1);
    }

    [Fact]
    public void EnergyIsDeterministicAndHigherEndurancePreservesMore()
    {
        var config = new MatchConfig(Fatigue: TestRates);
        var a = Make(config); var b = Make(config);
        var low = Make(config with { Endurance = 0 });
        var high = Make(config with { Endurance = 1 });
        for (int i = 0; i < 40; i++) { a.Step(); b.Step(); low.Step(); high.Step(); }
        foreach (var player in a.GetSnapshot().Players)
        {
            Assert.Equal(a.FatigueOf(player.Team, player.Id), b.FatigueOf(player.Team, player.Id));
            var less = low.FatigueOf(player.Team, player.Id)!.Value;
            var more = high.FatigueOf(player.Team, player.Id)!.Value;
            Assert.True(more.LongEnergy > less.LongEnergy);
            Assert.True(more.ShortEnergy > less.ShortEnergy);
        }
    }

    [Fact]
    public void FatigueAddsRoundedDecisionDelayWhenLongEnergyFalls()
    {
        var tiredBrain = new PressBrain();
        var freshBrain = new PressBrain();
        var config = new MatchConfig(Fatigue: TestRates with { LongPressingRate = 0.2 });
        var tired = Make(config, brain: (side, id) => side == Side.Home && id == "GK" ? tiredBrain : new PressBrain());
        var fresh = Make(config with { EnableFatigue = false },
            brain: (side, id) => side == Side.Home && id == "GK" ? freshBrain : new PressBrain());
        for (int i = 0; i < 60; i++) { tired.Step(); fresh.Step(); }
        Assert.True(tiredBrain.DecisionTicks.Count < freshBrain.DecisionTicks.Count);
        Assert.Contains(tiredBrain.DecisionTicks.Zip(tiredBrain.DecisionTicks.Skip(1), (a, b) => b - a), interval => interval > 3);
        Assert.All(freshBrain.DecisionTicks.Zip(freshBrain.DecisionTicks.Skip(1), (a, b) => b - a), interval => Assert.Equal(3, interval));
    }

    [Fact]
    public void MovementUsesIndividualEnergyProfileAndSnapshotCarriesEnergy()
    {
        var rates = new FatigueConfig(ShortPressingRate: 1, LongPressingRate: 0,
            ShortRecoveryRate: 0, LongDistanceRate: 0, SprintThreshold: 100);
        var config = new MatchConfig(Fatigue: rates);
        var tired = Make(config, brain: (_, _) => new PressThenRunBrain());
        var fresh = Make(config with { EnableFatigue = false }, brain: (_, _) => new PressThenRunBrain());
        for (int i = 0; i < 60; i++) { tired.Step(); fresh.Step(); }
        var slow = tired.GetSnapshot().Players.First(p => p.Team == Side.Home && p.Id == "GK");
        var fast = fresh.GetSnapshot().Players.First(p => p.Team == Side.Home && p.Id == "GK");
        Assert.Equal(PlayerProfile.Default.MaxSpeed * 0.85, slow.Vel.Length, 10);
        Assert.Equal(PlayerProfile.Default.MaxSpeed, fast.Vel.Length, 10);
        Assert.True(fast.Pos.X > slow.Pos.X);
        Assert.Equal(0, slow.ShortEnergy);
        Assert.Equal(1, slow.LongEnergy);
        Assert.Equal(1, fast.ShortEnergy);
        Assert.Equal(1, fast.LongEnergy);
    }
}
