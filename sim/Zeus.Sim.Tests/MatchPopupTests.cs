using Zeus.Sim;

public class MatchPopupTests
{
    static Event E(EventType type, Side team, string player, double seconds, string outcome = "", int period = 1) =>
        new(type, team, player, (long)(seconds * 20), seconds, Vec2.Zero, outcome, period);

    static Popup P(PopupKind kind, PopupWeight weight, long tick = 1, string player = "A") =>
        new(kind, weight, Side.Home, player, "", 1, 0, 0, 2.0, tick);

    [Fact]
    public void GoalCarriesScorerMinuteAndNewScore()
    {
        var events = new List<Event>
        {
            E(EventType.Shot, Side.Home, "ST", 60),
            E(EventType.Goal, Side.Home, "", 62),
            E(EventType.Shot, Side.Away, "LW", 300),
            E(EventType.Goal, Side.Away, "", 301),
        };
        var popups = new MatchNarrator().Consume(events);
        Assert.Equal(2, popups.Count);
        Assert.Equal((PopupKind.Goal, "ST", 2, 1, 0), (popups[0].Kind, popups[0].PlayerId, popups[0].Minute, popups[0].HomeScore, popups[0].AwayScore));
        Assert.Equal((1, 1, 6), (popups[1].HomeScore, popups[1].AwayScore, popups[1].Minute));
        Assert.All(popups, p => Assert.Equal(PopupWeight.Major, p.Weight));
    }

    [Fact]
    public void CardsAndSubstitutionAreTranslated()
    {
        var events = new List<Event>
        {
            E(EventType.Card, Side.Away, "CB1", 100, "Yellow"),
            E(EventType.Card, Side.Away, "CB1", 400, "SecondYellowRed"),
            E(EventType.Card, Side.Home, "ST", 500, "Red"),
            E(EventType.Substitution, Side.Home, "LW", 900, "LW->B2"),
        };
        var popups = new MatchNarrator().Consume(events);
        Assert.Equal(new[] { PopupKind.YellowCard, PopupKind.SecondYellow, PopupKind.RedCard, PopupKind.Substitution }, popups.Select(p => p.Kind));
        Assert.Equal(("LW", "B2"), (popups[3].Detail, popups[3].OtherPlayerId));
    }

    [Fact]
    public void FoulIsReplacedByCardOrPenaltyAndFreeKickRestartIsSilent()
    {
        var events = new List<Event>
        {
            E(EventType.Foul, Side.Away, "CB1", 100), E(EventType.Card, Side.Away, "CB1", 100, "Yellow"), E(EventType.RestartAwarded, Side.Home, "LW", 100, "free_kick"),
            E(EventType.Foul, Side.Away, "CB2", 200), E(EventType.RestartAwarded, Side.Home, "ST", 200, "penalty"),
            E(EventType.Foul, Side.Home, "ST", 300), E(EventType.RestartAwarded, Side.Away, "GK", 300, "free_kick"),
            E(EventType.RestartAwarded, Side.Home, "RB", 400, "ThrowIn"), E(EventType.RestartAwarded, Side.Home, "RB", 450, "GoalKick"),
            E(EventType.RestartAwarded, Side.Away, "LW", 500, "Corner"),
        };
        var kinds = new MatchNarrator().Consume(events).Select(p => p.Kind).ToArray();
        Assert.Equal(new[] { PopupKind.YellowCard, PopupKind.Penalty, PopupKind.Foul, PopupKind.Corner }, kinds);
    }

    [Fact]
    public void PeriodsAndEndAreTranslated()
    {
        var events = new List<Event>
        {
            E(EventType.KickOff, Side.Home, "", 0), E(EventType.RestartAwarded, Side.Home, "ST", 0, "kick_off"),
            E(EventType.HalfTime, Side.Home, "", 2700), E(EventType.RestartAwarded, Side.Away, "ST", 2700, "second_half_kick_off", 2),
            E(EventType.FullTime, Side.Home, "", 5400, "", 2),
        };
        Assert.Equal(new[] { PopupKind.HalfTime, PopupKind.SecondHalf, PopupKind.FullTime }, new MatchNarrator().Consume(events).Select(p => p.Kind));
    }

    [Fact]
    public void EachEventIsReadOnceSoReplayAndRepeatedCallsGiveNoDuplicates()
    {
        var events = new List<Event> { E(EventType.Goal, Side.Home, "", 10) };
        var narrator = new MatchNarrator();
        Assert.Single(narrator.Consume(events));
        Assert.Empty(narrator.Consume(events));
        events.Add(E(EventType.Card, Side.Home, "ST", 20, "Yellow"));
        Assert.Single(narrator.Consume(events));
        events.Add(E(EventType.Goal, Side.Home, "", 10));        // same moment and team logged twice: one popup
        Assert.Empty(narrator.Consume(events));
        Assert.Equal(3, narrator.Cursor);
    }

    [Fact]
    public void SuppressedSkipCountsScoreButOnlyShowsFinalResult()
    {
        var events = new List<Event>
        {
            E(EventType.Goal, Side.Home, "", 10), E(EventType.Card, Side.Home, "ST", 20, "Red"), E(EventType.HalfTime, Side.Home, "", 2700),
            E(EventType.Goal, Side.Away, "", 3000), E(EventType.FullTime, Side.Home, "", 5400),
        };
        var popups = new MatchNarrator().Consume(events, suppress: true);
        var end = Assert.Single(popups);
        Assert.Equal((PopupKind.FullTime, 1, 1), (end.Kind, end.HomeScore, end.AwayScore));
    }

    [Fact]
    public void ModeAndSpeedFilter()
    {
        var goal = P(PopupKind.Goal, PopupWeight.Major);
        var yellow = P(PopupKind.YellowCard, PopupWeight.Normal);
        var offside = P(PopupKind.Offside, PopupWeight.Minor);
        var penalty = P(PopupKind.Penalty, PopupWeight.Normal);
        var all = new[] { goal, yellow, offside, penalty };
        Assert.Equal(4, PopupRules.Filter(all, PopupMode.All, 1).Count);
        Assert.Equal(new[] { goal, penalty }, PopupRules.Filter(all, PopupMode.Important, 4));
        Assert.Empty(PopupRules.Filter(all, PopupMode.Off, 1));
        Assert.Equal(new[] { goal }, PopupRules.Filter(all, PopupMode.All, 16));           // 16x: only goal, red, half time, end
        Assert.True(PopupRules.Duration(goal, 4) < PopupRules.Duration(goal, 1));
        Assert.Equal(PopupMode.All, PopupRules.ParseMode(null));
        Assert.Equal(PopupMode.Important, PopupRules.ParseMode(PopupRules.ModeName(PopupMode.Important)));
    }

    [Fact]
    public void QueueShowsPopupsInOrderAndNeverBlocksForever()
    {
        var queue = new PopupQueue();
        queue.Enqueue(P(PopupKind.YellowCard, PopupWeight.Normal, 1), 2.0);
        queue.Enqueue(P(PopupKind.Substitution, PopupWeight.Normal, 2), 2.0);
        queue.Update(0.1);
        Assert.Equal(PopupKind.YellowCard, queue.Main!.Popup.Kind);
        queue.Update(2.0);
        Assert.Equal(PopupKind.Substitution, queue.Main!.Popup.Kind);
        queue.Update(2.1);
        Assert.Null(queue.Main);
        queue.Update(0);
        Assert.Equal(0, queue.Waiting);
    }

    [Fact]
    public void MajorPopupJumpsAheadAndCutsAShowingCardShort()
    {
        var queue = new PopupQueue();
        queue.Enqueue(P(PopupKind.YellowCard, PopupWeight.Normal, 1), 2.0);
        queue.Update(0.1);
        queue.Enqueue(P(PopupKind.Substitution, PopupWeight.Normal, 2), 2.0);
        queue.Enqueue(P(PopupKind.Goal, PopupWeight.Major, 3), 2.5);
        queue.Update(PopupQueue.CutShort + 0.01);
        Assert.Equal(PopupKind.Goal, queue.Main!.Popup.Kind);
        queue.Update(2.6);
        Assert.Equal(PopupKind.Substitution, queue.Main!.Popup.Kind);
    }

    [Fact]
    public void PauseFreezesAndStripRunsBesideTheMainLane()
    {
        var queue = new PopupQueue();
        queue.Enqueue(P(PopupKind.Goal, PopupWeight.Major, 1), 2.5);
        queue.Enqueue(P(PopupKind.Offside, PopupWeight.Minor, 2), 1.2);
        queue.Update(0);
        Assert.NotNull(queue.Main);
        Assert.NotNull(queue.Strip);
        for (int i = 0; i < 100; i++) queue.Update(0);         // paused: nothing expires
        Assert.NotNull(queue.Main);
        queue.Update(1.3);
        Assert.Null(queue.Strip);
        Assert.NotNull(queue.Main);
    }

    [Fact]
    public void LongBacklogDropsMinorPopupsAndDuplicates()
    {
        var queue = new PopupQueue();
        for (int i = 0; i < 10; i++) queue.Enqueue(P(PopupKind.Foul, PopupWeight.Minor, i), 1.2);
        queue.Enqueue(P(PopupKind.Foul, PopupWeight.Minor, 9), 1.2);      // duplicate of an already waiting key
        Assert.True(queue.Waiting <= PopupQueue.MaxBacklog);
        queue.Enqueue(P(PopupKind.Goal, PopupWeight.Major, 50), 2.5);
        queue.DropWaiting(p => p.Weight != PopupWeight.Major);
        Assert.Equal(1, queue.Waiting);
    }
}
