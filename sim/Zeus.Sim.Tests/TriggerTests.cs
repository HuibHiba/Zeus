using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class TriggerTests
{
    private static Trigger T(string condition, GamePhase phase = GamePhase.Verdedigen, string? zone = null, string instruction = "start_pressing") =>
        new("t", phase, condition, zone, new[] { new TriggerAction("ST", instruction) });

    [Fact]
    public void OnlyKnownConditionsInTheirOwnPhaseWithValidZonesAndPressingActionsAreSupported()
    {
        Assert.True(TriggerRules.IsSupported(T("bal_naar_keeper")));
        Assert.True(TriggerRules.IsSupported(T("slechte_aanname")));
        Assert.True(TriggerRules.IsSupported(T("tegenstander_ontvangt_op_flank")));
        Assert.False(TriggerRules.IsSupported(T("balwinst_in_hoge_zone")));
        Assert.False(TriggerRules.IsSupported(T("bal_naar_keeper", GamePhase.Opbouw)));
        Assert.True(TriggerRules.IsSupported(T("bal_naar_keeper", zone: "V-BL")));
        Assert.False(TriggerRules.IsSupported(T("bal_naar_keeper", zone: "V-XX")));
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

    [Fact]
    public void NewConditionsRequireTheirOwnPhase()
    {
        foreach (var (condition, phase) in new[] {
            (TriggerRules.BackPassToMidfielder, GamePhase.Verdedigen),
            (TriggerRules.LossInOwnHalf, GamePhase.Balverlies),
            (TriggerRules.WinInHighZone, GamePhase.Balwinst) })
            foreach (var candidate in Enum.GetValues<GamePhase>())
                Assert.Equal(candidate == phase, TriggerRules.IsSupported(T(condition, candidate)));
    }

    [Theory]
    [InlineData(0, 0, "V-BL")]
    [InlineData(1.0 / 3, 0.2, "M-HL")]
    [InlineData(2.0 / 3, 0.4, "A-C")]
    [InlineData(0.5, 0.6, "M-HR")]
    [InlineData(0.5, 0.8, "M-HR")]
    [InlineData(1, 1, "A-BR")]
    public void ZoneBoundariesUseTeamRelativeCoordinates(double x, double y, string zone)
    {
        var config = new TriggerConfig();
        Assert.True(TriggerRules.IsInZone(zone, (x, y), config));
        foreach (var side in Enum.GetValues<Side>())
            foreach (bool swapped in new[] { false, true })
            {
                var location = Pitch.ToMeters(x, y, side, swapped);
                var relative = TriggerRules.RelativePosition(location, side, swapped);
                Assert.Equal(x, relative.X, 12);
                Assert.Equal(y, relative.Y, 12);
                // Use interior points for the world roundtrip: floating point may round a boundary down.
                location = Pitch.ToMeters(0.8, 0.1, side, swapped);
                Assert.True(TriggerRules.IsInZone("A-BL", TriggerRules.RelativePosition(location, side, swapped), config));
                Assert.False(TriggerRules.IsInZone("V-BR", TriggerRules.RelativePosition(location, side, swapped), config));
            }
    }

    [Fact]
    public void ZoneAndConditionThresholdsAreStrictWhereSpecified()
    {
        var c = new TriggerConfig();
        Assert.True(TriggerRules.IsInZone("V-BL", (c.DefensiveThirdEnd - 1e-9, c.LeftOutsideEnd - 1e-9), c));
        Assert.True(TriggerRules.IsInZone("M-HL", (c.AttackingThirdStart - 1e-9, c.LeftHalfEnd - 1e-9), c));
        Assert.True(TriggerRules.IsInZone("A-C", (1, c.CentreEnd - 1e-9), c));
        Assert.True(TriggerRules.IsInZone("A-BR", (1, c.RightHalfEnd + 1e-9), c));
        Assert.False(TriggerRules.IsInZone("A-C", (double.NaN, 0.5), c));
        Assert.True(TriggerRules.IsInZone(null, (0, 0), c));
        Assert.False(TriggerRules.IsLossInOwnHalf(0.5, c));
        Assert.True(TriggerRules.IsLossInOwnHalf(0.5 - 1e-9, c));
        Assert.True(TriggerRules.IsWinInHighZone(2.0 / 3, c));
        Assert.False(TriggerRules.IsWinInHighZone(2.0 / 3 - 1e-9, c));
        Assert.False(TriggerRules.IsLossInOwnHalf(double.NaN, c));
        Assert.False(TriggerRules.IsWinInHighZone(double.PositiveInfinity, c));
        foreach (string role in new[] { "controlerende_middenvelder", "box_to_box", "aanvallende_middenvelder" })
        {
            Assert.True(TriggerRules.IsBackPassToMidfielder(role, new Vec2(-4, 0), true, c));
            Assert.True(TriggerRules.IsBackPassToMidfielder(role, new Vec2(4, 0), false, c));
            Assert.False(TriggerRules.IsBackPassToMidfielder(role, new Vec2(4, 0), true, c));
            Assert.False(TriggerRules.IsBackPassToMidfielder(role, new Vec2(-3, 4), true, c with { BackwardPassFraction = 0.6 }));
            Assert.True(TriggerRules.IsBackPassToMidfielder(role, new Vec2(-3.01, 4), true, c with { BackwardPassFraction = 0.6 }));
        }
        Assert.False(TriggerRules.IsBackPassToMidfielder("spits", new Vec2(-4, 0), true, c));
        Assert.False(TriggerRules.IsBackPassToMidfielder("box_to_box", Vec2.Zero, true, c));
        Assert.False(TriggerRules.IsBackPassToMidfielder("box_to_box", new Vec2(double.NaN, 0), true, c));
    }

    [Theory]
    [InlineData(Side.Home, false)]
    [InlineData(Side.Away, false)]
    [InlineData(Side.Home, true)]
    [InlineData(Side.Away, true)]
    public void BackwardPassDirectionMirrorsWithTeamAndEnds(Side team, bool swapped)
    {
        var origin = Pitch.ToMeters(0.6, 0.5, team, swapped);
        var target = Pitch.ToMeters(0.4, 0.5, team, swapped);
        bool attacksPositiveX = (team == Side.Home) != swapped;
        Assert.True(TriggerRules.IsBackPassToMidfielder("box_to_box", target - origin, attacksPositiveX, new TriggerConfig()));
        Assert.False(TriggerRules.IsBackPassToMidfielder("box_to_box", origin - target, attacksPositiveX, new TriggerConfig()));
    }

    [Fact]
    public void NewSettingsAreValidatedAndUsed()
    {
        var c = new TriggerConfig();
        c.Validate();
        Assert.Equal(1.0 / 3, c.DefensiveThirdEnd);
        Assert.Equal(2.0 / 3, c.AttackingThirdStart);
        Assert.Equal(0.2, c.LeftOutsideEnd);
        Assert.Equal(0.4, c.LeftHalfEnd);
        Assert.Equal(0.6, c.CentreEnd);
        Assert.Equal(0.8, c.RightHalfEnd);
        Assert.Equal(0.5, c.OwnHalfEnd);
        Assert.Equal(0.3, c.BackwardPassFraction);
        foreach (var invalid in new[] {
            c with { DefensiveThirdEnd = 0 }, c with { DefensiveThirdEnd = double.NaN },
            c with { AttackingThirdStart = c.DefensiveThirdEnd }, c with { AttackingThirdStart = 1 },
            c with { LeftOutsideEnd = 0 }, c with { LeftHalfEnd = c.LeftOutsideEnd },
            c with { CentreEnd = c.RightHalfEnd }, c with { RightHalfEnd = double.PositiveInfinity },
            c with { RightHalfEnd = 1 }, c with { OwnHalfEnd = 0 },
            c with { BackwardPassFraction = -0.1 }, c with { BackwardPassFraction = double.NaN } })
            Assert.Throws<ArgumentException>(() => invalid.Validate());
        var custom = c with { DefensiveThirdEnd = 0.2, AttackingThirdStart = 0.8,
            LeftOutsideEnd = 0.1, LeftHalfEnd = 0.3, CentreEnd = 0.5, RightHalfEnd = 0.7, OwnHalfEnd = 0.4 };
        custom.Validate();
        Assert.True(TriggerRules.IsInZone("M-HR", (0.7, 0.6), custom));
        Assert.False(TriggerRules.IsLossInOwnHalf(0.45, custom));
        Assert.False(TriggerRules.IsWinInHighZone(0.7, custom));
    }

    [Theory]
    [InlineData(Side.Home, true)]
    [InlineData(Side.Away, true)]
    [InlineData(Side.Home, false)]
    [InlineData(Side.Away, false)]
    public void PossessionTriggersFireAtTheChangeLocationInTheirTransitionPhase(Side winner, bool matchingZone)
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        tactic = tactic with {
            Phases = tactic.Phases.ToDictionary(p => p.Key, p => p.Value with {
                Players = p.Value.Players.Select(player => player.Id == "GK" ? player with { X = 0.8, Y = 0.5 } : player).ToArray() }),
            Triggers = new[] { T(TriggerRules.LossInOwnHalf, GamePhase.Balverlies, matchingZone ? "V-C" : "V-BL") with { Then = new[] { new TriggerAction("RS", "start_pressing") } },
                T(TriggerRules.WinInHighZone, GamePhase.Balwinst, matchingZone ? "A-C" : "A-BL") with { Then = new[] { new TriggerAction("RS", "start_pressing") } } }
        };
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0, EnableTeamPhases: true),
            tactic, tactic, 7, (_, _) => new FormationBrain());
        for (int i = 0; i < 200; i++) match.Step();
        var loserSetup = winner == Side.Home ? Side.Away : Side.Home;
        if (match.Possession != loserSetup)
        {
            var opponent = match.GetSnapshot().Players.First(p => p.Team == loserSetup && p.Id == "LC");
            match.SetBall(new Vec3(opponent.Pos.X, opponent.Pos.Y, 0), Vec3.Zero);
            match.Step();
            Assert.Equal(loserSetup, match.Possession);
        }
        var keeper = match.GetSnapshot().Players.Single(p => p.Team == winner && p.Id == "GK");
        int before = match.Events.Events.Count;
        match.SetBall(new Vec3(keeper.Pos.X, keeper.Pos.Y, 0), Vec3.Zero);
        match.Step();
        var fired = match.Events.Events.Skip(before).Where(e => e.Type == EventType.TriggerFired).ToArray();
        if (!matchingZone)
        {
            Assert.Empty(fired);
            return;
        }
        Assert.Equal(2, fired.Length);
        Assert.Contains(fired, e => e.Team == winner && e.Outcome.EndsWith(TriggerRules.WinInHighZone));
        var loser = winner == Side.Home ? Side.Away : Side.Home;
        Assert.Contains(fired, e => e.Team == loser && e.Outcome.EndsWith(TriggerRules.LossInOwnHalf));
        Assert.All(fired, e => Assert.Equal(keeper.Pos, e.Location));
        Assert.Equal(GamePhase.Balwinst, match.PhaseOf(winner));
        Assert.Equal(GamePhase.Balverlies, match.PhaseOf(loser));
    }

    [Fact]
    public void StateConditionsAreSupportedAndBehindNeedsMinuteSeventy()
    {
        Assert.True(TriggerRules.IsSupported(T(TriggerRules.OpponentBallInZone, zone: "V-C")));
        Assert.False(TriggerRules.IsSupported(T(TriggerRules.OpponentBallInZone, GamePhase.Aanval)));
        foreach (var phase in Enum.GetValues<GamePhase>()) Assert.True(TriggerRules.IsSupported(T(TriggerRules.BehindAfterMinute, phase)));
        Assert.True(TriggerRules.IsBehindAfterMinute(70, -1));
        Assert.False(TriggerRules.IsBehindAfterMinute(69, -1));
        Assert.False(TriggerRules.IsBehindAfterMinute(80, 0));
        Assert.False(TriggerRules.IsBehindAfterMinute(80, 2));
    }

    [Fact]
    public void OpponentBallInZoneTriggerChangesAFixedMatch()
    {
        var baseTactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        Match Play(Trigger[] triggers)
        {
            var home = baseTactic with { Triggers = triggers };
            var m = Match.Create(new MatchConfig(), home, baseTactic with { Triggers = Array.Empty<Trigger>() }, 3);
            for (int i = 0; i < 12000; i++) m.Step();
            return m;
        }
        var with = Play(new[] { T(TriggerRules.OpponentBallInZone) with { Then = new[] { new TriggerAction("RS", "start_pressing") } } });
        var without = Play(Array.Empty<Trigger>());
        var fired = with.Events.Events.Where(e => e.Type == EventType.TriggerFired && e.Outcome.EndsWith(TriggerRules.OpponentBallInZone)).ToArray();
        Assert.NotEmpty(fired);
        Assert.All(fired, e => { Assert.Equal(Side.Home, e.Team); Assert.Equal("RS", e.PlayerId); });
        Assert.NotEqual(without.EventHash(), with.EventHash());
    }

    private sealed class MidfielderBackPassBrain : IPlayerBrain
    {
        public Intent Decide(Perception view)
        {
            if (!view.HasBall) return new FormationBrain().Decide(view);
            double direction = view.AttacksPositiveX ? 1 : -1;
            var receiver = view.VisiblePlayers.FirstOrDefault(p => p.Team == view.Self.Team
                && p.Id is "LCM" or "RCM" && (p.Pos.X - view.Self.Pos.X) * direction < -2);
            return receiver is null ? new Intent(IntentKind.Scan, view.Self.Pos)
                : new Intent(IntentKind.Pass, receiver.Pos, receiver.Id);
        }
    }

    [Fact]
    public void BackPassToRosterMidfielderFiresAtKickOrigin()
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));
        tactic = tactic with { Triggers = new[] { T(TriggerRules.BackPassToMidfielder) with { Then = new[] { new TriggerAction("RS", "start_pressing") } } } };
        var match = Match.Create(new MatchConfig(Perception: new PerceptionConfig(FieldOfViewRadians: 2 * Math.PI, SightRange: 200)),
            tactic, tactic, 7, (_, _) => new MidfielderBackPassBrain());
        for (int i = 0; i < 6000; i++) match.Step();
        var fired = match.Events.Events.Where(e => e.Type == EventType.TriggerFired).ToArray();
        Assert.NotEmpty(fired);
        Assert.All(fired, e => Assert.Contains(match.Events.Events, pass => pass.Type == EventType.Pass
            && pass.Tick == e.Tick && pass.Team != e.Team && pass.Location == e.Location));
    }
}
