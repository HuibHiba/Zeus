using Zeus.Sim;

public class DebugViewStateTests
{
    static PlayerSnapshot Player(Side team, string id, double x, bool sentOff = false) =>
        new(team, id, new Vec2(x, 0), new Vec2(0, 0), 0, SentOff: sentOff);

    static Snapshot Frame(bool swapped = false) => new(1, .1, new[] {
        Player(Side.Home, "passer", 0), Player(Side.Home, "receiver", 35),
        Player(Side.Home, "level", 30), Player(Side.Away, "GK", 50),
        Player(Side.Away, "defender", 30), Player(Side.Away, "red", 45, true)
    }, new BallSnapshot(new Vec3(0, 0, 0), new Vec3(0, 0, 0)), EndsSwapped: swapped);

    [Fact]
    public void PhaseEventsApplyToTheirTeamOnlyAndUnknownStaysUnknown()
    {
        var view = new DebugViewState();
        Assert.Null(view.PhaseOf(Side.Home));
        foreach (var phase in Enum.GetValues<GamePhase>())
        {
            view.Read(new Event(EventType.PhaseChange, Side.Home, "", 1, .1, new Vec2(0, 0),
                $"Opbouw->{phase}: reason"), Frame());
            Assert.Equal(phase, view.PhaseOf(Side.Home));
            Assert.Null(view.PhaseOf(Side.Away));
        }
        view.Read(new Event(EventType.PhaseChange, Side.Home, "", 1, .1, new Vec2(0, 0), "bad"), Frame());
        Assert.Equal(GamePhase.Stilstaand, view.PhaseOf(Side.Home));
    }

    [Fact]
    public void TriggersExpireInSimulationTimeAndHistoryRemainsBounded()
    {
        var view = new DebugViewState();
        var trigger = new Event(EventType.TriggerFired, Side.Away, "defender", 1, .1,
            new Vec2(0, 0), "druk: slechte_aanname");
        view.Read(trigger, Frame());
        view.Advance(.1);
        view.Advance(4.09);
        Assert.Equal(trigger, Assert.Single(view.Triggers));
        view.Advance(4.1);
        Assert.Empty(view.Triggers);
        for (int i = 0; i < 10; i++) view.Read(trigger with { Tick = i }, Frame());
        Assert.Equal(6, view.RecentEvents.Count);
        Assert.Equal(4, view.RecentEvents[0].Tick);
    }

    [Fact]
    public void PassFreezesReceiverPositionAndRestartClearsPreviousReceiver()
    {
        var view = new DebugViewState();
        var pass = new Event(EventType.Pass, Side.Home, "passer", 1, .1, new Vec2(0, 0), "to receiver 35.000");
        view.Read(pass, Frame());
        Assert.Equal("receiver", view.LastPass!.ReceiverId);
        Assert.Equal(new Vec2(35, 0), view.LastPass.ReceiverPosition);
        view.Advance(10);
        Assert.Equal(new Vec2(35, 0), view.LastPass.ReceiverPosition);
        view.Read(pass with { Outcome = "restart ThrowIn" }, Frame());
        Assert.Null(view.LastPass!.ReceiverId);
        Assert.Null(view.LastPass.ReceiverPosition);
        Assert.Equal(2, view.PassCount);
    }

    [Fact]
    public void OffsideUsesSecondActiveOpponentBallHalfAndEnds()
    {
        var frame = Frame();
        var line = DebugViewState.OffsideLine(frame, Side.Home)!;
        Assert.Equal(30, line.X);
        Assert.Equal("receiver", Assert.Single(line.Candidates));
        frame = frame with { Ball = frame.Ball! with { Pos = new Vec3(40, 0, 0) } };
        Assert.Equal(40, DebugViewState.OffsideLine(frame, Side.Home)!.X);
        Assert.Empty(DebugViewState.OffsideLine(frame, Side.Home)!.Candidates);
        var mirrored = Frame(true) with { Players = Frame().Players.Select(p => p with { Pos = new Vec2(-p.Pos.X, 0) }).ToArray() };
        Assert.Equal(-30, DebugViewState.OffsideLine(mirrored, Side.Home)!.X);
        Assert.Equal("receiver", Assert.Single(DebugViewState.OffsideLine(mirrored, Side.Home)!.Candidates));
        var reversed = Frame() with { Players = Frame().Players.Select(p => p with {
            Team = p.Team == Side.Home ? Side.Away : Side.Home, Pos = new Vec2(-p.Pos.X, 0) }).ToArray() };
        Assert.Equal(-30, DebugViewState.OffsideLine(reversed, Side.Away)!.X);
        Assert.Null(DebugViewState.OffsideLine(frame with { Ball = null }, Side.Home));
    }

    [Fact]
    public void DisplayNeverMutatesOrChangesMatchHashes()
    {
        var tactic = TacticLoader.LoadFile(Path.Combine(AppContext.BaseDirectory,
            "../../../../../tactics/laag_blok_442.json"));
        var baseline = Match.Create(new MatchConfig(), tactic, tactic, 7);
        var displayed = Match.Create(new MatchConfig(), tactic, tactic, 7);
        var view = new DebugViewState();
        int read = 0;
        for (int i = 0; i < 100; i++)
        {
            baseline.Step();
            displayed.Step();
            var snapshot = displayed.GetSnapshot();
            while (read < displayed.Events.Events.Count) view.Read(displayed.Events.Events[read++], snapshot);
            view.Advance(snapshot.TimeSeconds);
            DebugViewState.OffsideLine(snapshot, Side.Home);
            DebugViewState.OffsideLine(snapshot, Side.Away);
        }
        Assert.Equal(baseline.EventHash(), displayed.EventHash());
        Assert.Equal(Canonical.Hash(baseline.GetSnapshot()), Canonical.Hash(displayed.GetSnapshot()));
    }

    [Fact]
    public void DefaultsAreValidAndInvalidDisplaySettingsAreRejected()
    {
        var config = new DebugViewConfig();
        config.Validate();
        Assert.Equal(4, config.TriggerSeconds);
        Assert.Equal(6, config.RecentEventCount);
        foreach (double seconds in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => (config with { TriggerSeconds = seconds }).Validate());
        foreach (int count in new[] { 0, -1, 7 })
            Assert.Throws<ArgumentOutOfRangeException>(() => (config with { RecentEventCount = count }).Validate());
    }
}
