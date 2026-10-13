using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class TriggerTests
{
    private static Trigger T(string condition, GamePhase phase = GamePhase.Verdedigen, string? zone = null, string instruction = "start_pressing") =>
        new("t", phase, condition, zone, new[] { new TriggerAction("ST", instruction) });

    [Fact]
    public void OnlyKnownConditionsInTheDefendingPhaseWithoutZoneAndWithPressingActionsAreSupported()
    {
        Assert.True(TriggerRules.IsSupported(T("bal_naar_keeper")));
        Assert.True(TriggerRules.IsSupported(T("slechte_aanname")));
        Assert.True(TriggerRules.IsSupported(T("tegenstander_ontvangt_op_flank")));
        Assert.False(TriggerRules.IsSupported(T("balwinst_in_hoge_zone")));
        Assert.False(TriggerRules.IsSupported(T("bal_naar_keeper", GamePhase.Opbouw)));
        Assert.False(TriggerRules.IsSupported(T("bal_naar_keeper", zone: "V-BL")));
        Assert.False(TriggerRules.IsSupported(T("bal_naar_keeper", instruction: "zakken")));
    }

    [Fact]
    public void BadTouchAndFlankThresholdsAreInclusiveAndSymmetric()
    {
        var c = new TriggerConfig();
        Assert.True(TriggerRules.IsBadTouch(c.BadTouchRelativeSpeed, c));
        Assert.False(TriggerRules.IsBadTouch(c.BadTouchRelativeSpeed - 0.01, c));
        Assert.False(TriggerRules.IsBadTouch(double.NaN, c));
        Assert.True(TriggerRules.IsOnFlank(c.FlankFromMeters, c));
        Assert.True(TriggerRules.IsOnFlank(-c.FlankFromMeters, c));
        Assert.False(TriggerRules.IsOnFlank(0, c));
        Assert.Throws<ArgumentException>(() => new TriggerConfig(PressSeconds: 0).Validate());
        Assert.Throws<ArgumentException>(() => new TriggerConfig(FlankFromMeters: 99).Validate());
    }

    private static int Fired(MatchConfig config, string home, string away)
    {
        int fired = 0;
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var m = Match.Create(config, TacticLoader.LoadFile(TacticLoaderTests.TacticPath(home)),
                TacticLoader.LoadFile(TacticLoaderTests.TacticPath(away)), seed);
            for (int i = 0; i < 30000; i++) m.Step();
            fired += m.Events.Events.Count(e => e.Type == EventType.TriggerFired);
        }
        return fired;
    }

    private sealed class BackPassBrain : IPlayerBrain
    {
        public Intent Decide(Perception view)
        {
            if (!view.HasBall) return new FormationBrain().Decide(view);
            var keeper = view.VisiblePlayers.FirstOrDefault(p => p.Team == view.Self.Team && p.IsKeeper);
            return keeper is null ? new Intent(IntentKind.Scan, view.Self.Pos) : new Intent(IntentKind.Pass, keeper.Pos, keeper.Id);
        }
    }

    [Fact]
    public void BallPassedToTheOpponentKeeperStartsTheConfiguredPressing()
    {
        var all = new PerceptionConfig(FieldOfViewRadians: 2 * Math.PI);
        Match Play(bool on)
        {
            var m = Match.Create(new MatchConfig(Perception: all, EnableTriggers: on), TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
                TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), 7,
                (side, id) => side == Side.Home ? new BackPassBrain() : new PassingBrain(new PassConfig()));
            for (int i = 0; i < 6000; i++) m.Step();
            return m;
        }
        var fired = Play(true).Events.Events.Where(e => e.Type == EventType.TriggerFired && e.Outcome.EndsWith(TriggerRules.BallToKeeper)).ToArray();
        Assert.NotEmpty(fired);
        Assert.All(fired, e => { Assert.Equal(Side.Away, e.Team); Assert.Equal("ST", e.PlayerId); });
        Assert.Empty(Play(false).Events.Events.Where(e => e.Type == EventType.TriggerFired));
    }

    [Fact]
    public void TriggersFireInAMatchAndCanBeSwitchedOff()
    {
        Assert.True(Fired(new MatchConfig(), "laag_blok_442", "man_om_man_433") > 0);
        Assert.Equal(0, Fired(new MatchConfig(EnableTriggers: false), "laag_blok_442", "man_om_man_433"));
    }

    [Fact]
    public void FiringTriggersChangesTheMatchAndStaysDeterministic()
    {
        Match Play(bool on)
        {
            var m = Match.Create(new MatchConfig(EnableTriggers: on), TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
                TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), 3);
            for (int i = 0; i < 30000; i++) m.Step();
            return m;
        }
        Assert.Equal(Play(true).EventHash(), Play(true).EventHash());
        Assert.NotEqual(Play(true).EventHash(), Play(false).EventHash());
    }
}
