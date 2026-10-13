using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class BrainTests
{
    private sealed class RecordingBrain : IPlayerBrain
    {
        private readonly bool _move;
        public RecordingBrain(bool move = false) => _move = move;
        public List<Perception> Views { get; } = new();
        public Intent Decide(Perception view)
        {
            Views.Add(view);
            return _move ? new FormationBrain().Decide(view) : new Intent(IntentKind.HoldPosition, view.Self.Pos);
        }
    }

    private static Tactic Tactic() => TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442"));

    private sealed class InvalidBrain : IPlayerBrain
    {
        public Intent Decide(Perception view) => new(IntentKind.MoveToPosition, new Vec2(double.NaN, 0));
    }

    [Fact]
    public void InvalidIntentCannotPoisonMovementWithNaN()
    {
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0), Tactic(), Tactic(), 42,
            (_, _) => new InvalidBrain());
        Assert.Throws<InvalidOperationException>(() => match.Step());
        Assert.Equal(0, match.Tick);
        Assert.All(match.GetSnapshot().Players, p => Assert.True(double.IsFinite(p.Pos.X)));
    }

    [Fact]
    public void InjectedBrainControlsMovementAndDecisionsUseRoundedFixedTicks()
    {
        var brains = new Dictionary<(Side, string), RecordingBrain>();
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0,
            Perception: new PerceptionConfig(ScanIntervalSeconds: 0.25)), Tactic(), Tactic(), 42,
            (side, id) => brains[(side, id)] = new RecordingBrain());
        var before = match.GetSnapshot();
        for (int i = 0; i < 7; i++) match.Step();
        Assert.Equal(new long[] { 0, 3, 6 }, brains[(Side.Home, "RC")].Views.Select(p => p.Tick));
        Assert.All(before.Players, p => Assert.Equal(p.Pos,
            match.GetSnapshot().Players.Single(q => q.Team == p.Team && q.Id == p.Id).Pos));
        Assert.Equal(IntentKind.HoldPosition, match.IntentOf(Side.Home, "RC")!.Kind);
    }

    [Fact]
    public void AllBrainsSeeOneStateBeforeAnyPlayerMoves()
    {
        var views = new Dictionary<(Side, string), RecordingBrain>();
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0,
            Perception: new PerceptionConfig(FieldOfViewRadians: 2 * Math.PI, SightRange: 200)),
            Tactic(), Tactic(), 42, (side, id) => views[(side, id)] = new RecordingBrain(move: true));
        var before = match.GetSnapshot();
        match.Step();
        foreach (var brain in views.Values)
        {
            var view = Assert.Single(brain.Views);
            Assert.Equal(before.Players.Single(p => p.Team == view.Self.Team && p.Id == view.Self.Id), view.Self);
            Assert.Equal(21, view.VisiblePlayers.Count);
            Assert.All(view.VisiblePlayers, p => Assert.Contains(p, before.Players));
        }
        Assert.Contains(match.GetSnapshot().Players, p => p.Pos != before.Players.Single(
            q => q.Team == p.Team && q.Id == p.Id).Pos);
    }

    [Fact]
    public void FollowBallDoesNotReadHiddenWorldPosition()
    {
        var t = Tactic();
        var match = Match.Create(new MatchConfig(MaxReactionSeconds: 0, Profile: new PlayerProfile(MaxSpeed: 0),
            Perception: new PerceptionConfig(ScanIntervalSeconds: 0.1)), t, t, 42);
        match.SetBall(new Vec3(0, 0, 10), Vec3.Zero);
        match.Step();
        var view = match.PerceptionOf(Side.Away, "RC")!;
        Assert.NotNull(view.VisibleBall);
        var target = view.FormationTarget;
        // Behind the away defender (who faces -x), outside foot-sensing radius.
        match.SetBall(new Vec3(45, 10, 10), Vec3.Zero);
        match.Step();
        view = match.PerceptionOf(Side.Away, "RC")!;
        Assert.Null(view.VisibleBall);
        Assert.Equal(new Vec3(0, 0, 10), view.KnownBall!.Ball.Pos);
        Assert.Equal(target, view.FormationTarget);
    }

    [Fact]
    public void FormationBrainKeepsTacticalTargetAndHoldsWhenArrived()
    {
        var self = new PlayerSnapshot(Side.Home, "RC", Vec2.Zero, Vec2.Zero, 0);
        var view = new Perception(0, self, GamePhase.Opbouw, false, new Vec2(4, 0),
            Array.Empty<PlayerSnapshot>(), null, null);
        var brain = new FormationBrain();
        Assert.Equal(new Intent(IntentKind.MoveToPosition, new Vec2(4, 0)), brain.Decide(view));
        Assert.Equal(IntentKind.HoldPosition, brain.Decide(view with { FormationTarget = Vec2.Zero }).Kind);
    }
}
