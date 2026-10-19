using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class SetPieceTests
{
    private static Tactic Load(string name, SetPieces? block) =>
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath(name)) with { SetPieces = block };

    private static Match Create(SetPieces? block, ulong seed = 42, MatchConfig? config = null, double? aerial = null)
    {
        Tactic home = Load("laag_blok_442", block), away = Load("man_om_man_433", block);
        // aerial: a prof squad with Heading and Strength of every starter set to this value.
        Squad? SquadFor(Tactic t)
        {
            if (aerial is not { } v) return null;
            var s = SquadFactory.Fit(SquadLoader.LoadFile(SquadTests.SquadPath("prof")), t);
            return s with { Starters = s.Starters.Select(p => p with { Attributes = p.Attributes with { Heading = v, Strength = v } }).ToArray() };
        }
        return Match.Create(config ?? new MatchConfig(), home, away, seed, homeSquad: SquadFor(home), awaySquad: SquadFor(away));
    }

    // A corner for whoever the match awards it to: the ball leaves the end line with the home team as last toucher.
    private static (Match Match, Event Award) Corner(SetPieces? block, ulong seed = 42, MatchConfig? config = null, double? aerial = null)
    {
        var match = Create(block, seed, config, aerial);
        match.SetBall(new Vec3(-Pitch.HalfLength + .2, 20, 0), new Vec3(-6, 0, 0));
        for (int i = 0; i < 50 && match.Status != MatchStatus.Restart; i++) match.Step();
        var award = match.Events.Events.Last(e => e.Type == EventType.RestartAwarded);
        Assert.Equal("Corner", award.Outcome);
        return (match, award);
    }

    [Fact]
    public void TacticWithoutBlockHasNoSetPiecesAndBlockParses()
    {
        Assert.Null(TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")).SetPieces);
        var json = File.ReadAllText(TacticLoaderTests.TacticPath("laag_blok_442")).TrimEnd().TrimEnd('}')
            + ",\"standaardsituaties\":{\"corner_voor\":\"verre_paal\",\"palen\":1,\"vrije_trap_tegen\":\"man\"}}";
        var sp = TacticLoader.Parse(json).SetPieces!;
        Assert.Equal("verre_paal", sp.CornerFor);
        Assert.Null(sp.CornerAgainst);
        Assert.Equal(1, sp.PostDefenders);
        Assert.Equal("man", sp.FreeKickAgainst);
        Assert.Throws<TacticLoadException>(() => TacticLoader.Parse(json.Replace("verre_paal", "onzin")));
    }

    [Fact]
    public void NearPostPutsAtLeastTwoAttackersWithinSixMetresOfTheFirstPost()
    {
        var (match, award) = Corner(new SetPieces(CornerFor: "korte_paal"));
        double goalX = Math.Sign(award.Location.X) * Pitch.HalfLength, post = Math.Sign(award.Location.Y) * 3.66;
        var near = match.GetSnapshot().Players.Where(p => p.Team == award.Team && !p.IsGoalkeeper
            && (p.Pos - new Vec2(goalX, post)).Length < 6).ToList();
        Assert.True(near.Count >= 2, $"{near.Count} attackers near the first post");
    }

    [Fact]
    public void FarPostPutsAtLeastTwoAttackersWithinSixMetresOfTheFarPost()
    {
        var (match, award) = Corner(new SetPieces(CornerFor: "verre_paal"));
        double goalX = Math.Sign(award.Location.X) * Pitch.HalfLength, post = -Math.Sign(award.Location.Y) * 3.66;
        Assert.True(match.GetSnapshot().Players.Count(p => p.Team == award.Team && !p.IsGoalkeeper
            && (p.Pos - new Vec2(goalX, post)).Length < 6) >= 2);
    }

    [Fact]
    public void PenaltySpotRoutinePutsAtLeastTwoAttackersWithinFiveMetresOfTheSpot()
    {
        var (match, award) = Corner(new SetPieces(CornerFor: "penaltystip"));
        var spot = new Vec2(Math.Sign(award.Location.X) * (Pitch.HalfLength - 11), 0);
        Assert.True(match.GetSnapshot().Players.Count(p => p.Team == award.Team && !p.IsGoalkeeper && (p.Pos - spot).Length < 5) >= 2);
    }

    [Fact]
    public void ShortCornerPlacesAPartnerAndAimsTheKickAtHim()
    {
        var (match, award) = Corner(new SetPieces(CornerFor: "kort"));
        var snapshot = match.GetSnapshot();
        var taker = snapshot.Players.Single(p => p.Team == award.Team && p.Id == award.PlayerId);
        Assert.Contains(snapshot.Players, p => p.Team == award.Team && p.Id != taker.Id && !p.IsGoalkeeper
            && (p.Pos - taker.Pos).Length is > 4 and < 9);
        var partner = snapshot.Players.First(p => p.Team == award.Team && p.Id != taker.Id && !p.IsGoalkeeper
            && (p.Pos - taker.Pos).Length is > 4 and < 9);
        for (int i = 0; i < 40 && !match.Events.Events.Any(e => e.Type == EventType.RestartTaken); i++) match.Step();
        Assert.Contains(match.Events.Events, e => e.Type == EventType.RestartTaken);
        // The first pass heads for the partner: its direction is within about 35 degrees of the line to him.
        var ball = match.GetSnapshot().Ball!;
        var toPartner = partner.Pos - taker.Pos;
        double cos = (ball.Vel.X * toPartner.X + ball.Vel.Y * toPartner.Y) / (ball.Vel.XY.Length * toPartner.Length);
        Assert.True(cos > .8, $"cos {cos}");
    }

    [Fact]
    public void ZonalPutsAtLeastFourDefendersInTheSixYardBox()
    {
        var (match, award) = Corner(new SetPieces(CornerAgainst: "zone"));
        double sign = Math.Sign(award.Location.X);
        int inBox = match.GetSnapshot().Players.Count(p => p.Team != award.Team && !p.IsGoalkeeper
            && Math.Abs(p.Pos.Y) <= 9.16 && p.Pos.X * sign >= Pitch.HalfLength - 5.5);
        Assert.True(inBox >= 4, $"{inBox} defenders in the goal area");
    }

    [Fact]
    public void ManMarkingGivesEveryAttackerInThePenaltyAreaADefenderWithinTwoMetres()
    {
        var (match, award) = Corner(new SetPieces(CornerFor: "penaltystip", CornerAgainst: "man"));
        var players = match.GetSnapshot().Players;
        double sign = Math.Sign(award.Location.X);
        var attackers = players.Where(p => p.Team == award.Team && !p.IsGoalkeeper
            && Math.Abs(p.Pos.Y) <= 20.15 && p.Pos.X * sign >= Pitch.HalfLength - 16.5).ToList();
        Assert.NotEmpty(attackers);
        foreach (var a in attackers)
            Assert.Contains(players, d => d.Team != award.Team && !d.IsGoalkeeper && (d.Pos - a.Pos).Length <= 2);
    }

    [Fact]
    public void RoutinesAreDeterministicAndChangeTheCorner()
    {
        var block = new SetPieces("korte_paal", "man", 2, "voorzet", "zone");
        string Hash(SetPieces? b) { var (m, _) = Corner(b, 7); for (int i = 0; i < 400; i++) m.Step(); return m.EventHash(); }
        Assert.Equal(Hash(block), Hash(block));
        Assert.NotEqual(Hash(null), Hash(block)); // the block has an effect; without it the corner is the historical one
    }

    [Fact]
    public void HeaderAttemptChanceRisesWithHeading()
    {
        var h = HeaderConfig.Default;
        var chances = Enumerable.Range(0, 11).Select(k => HeaderModel.AttemptProbability(h, (k / 10.0 + .5 * 1.0) / 1.5,
            ballDistance: 1, goalDistance: 5, pressure: 1, height: 1.95, freeKick: false)).ToList();
        for (int k = 1; k < chances.Count; k++) Assert.True(chances[k] > chances[k - 1], $"Heading {k / 10.0}: {chances[k]} <= {chances[k - 1]}");
        Assert.All(chances, c => Assert.InRange(c, 0, h.MaxProbability));
    }

    [Fact]
    public void EqualHeadersSplitTheDuelAndTheKeeperClaimsMoreWhenCloser()
    {
        var h = HeaderConfig.Default;
        double s = HeaderModel.Strength(h, .65, 1, 1.95);
        Assert.Equal(.5, HeaderModel.AttackerDuelChance(h, s, s), 6);
        Assert.True(HeaderModel.AttackerDuelChance(h, s, HeaderModel.Strength(h, .9, 1, 1.95)) < .5);
        Assert.Equal(0, HeaderModel.KeeperClaimProbability(h, h.KeeperClaimRadius, 2.5, 2));
        Assert.True(HeaderModel.KeeperClaimProbability(h, 3, 2.5, 2) > HeaderModel.KeeperClaimProbability(h, 4, 2.5, 2));
        Assert.True(HeaderModel.KeeperClaimProbability(h, 3, 2.5, 2.5) > HeaderModel.KeeperClaimProbability(h, 3, 2.5, 1.4));
    }

    [Fact]
    public void WithoutBlockTheHeaderSettingsChangeNothing()
    {
        string Hash(MatchConfig config) { var (m, _) = Corner(null, 7, config); for (int i = 0; i < 400; i++) m.Step(); return m.EventHash(); }
        var eager = new MatchConfig(HeaderFinishing: new HeaderConfig(Radius: 6, AttemptBase: .95, ClearBase: .95));
        Assert.Equal(Hash(new MatchConfig()), Hash(eager));
        Assert.Equal(Hash(new MatchConfig()), Hash(new MatchConfig()));
    }

    [Fact]
    public void HeaderRoutineIsDeterministic()
    {
        var block = new SetPieces(CornerFor: "korte_paal", CornerAgainst: "zone");
        string Hash() { var (m, _) = Corner(block, 11, aerial: .9); for (int i = 0; i < 400; i++) m.Step(); return m.EventHash(); }
        Assert.Equal(Hash(), Hash());
    }

    [Fact]
    public void NearPostCornerWithStrongHeadersGivesMoreHeaderAttemptsThanWithWeakHeaders()
    {
        var block = new SetPieces(CornerFor: "korte_paal");
        (int Succeeded, string Info) Attempts(double aerial)
        {
            int armed = 0, entered = 0, candidates = 0, rolled = 0, succeeded = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var (m, _) = Corner(block, seed, aerial: aerial);
                for (int i = 0; i < 80; i++) m.Step();
                var s = m.HeaderStats;
                armed += s.Armed; entered += s.WindowEntered; candidates += s.WithCandidate; rolled += s.AttemptsRolled; succeeded += s.AttemptsSucceeded;
            }
            return (succeeded, $"armed {armed}, entered {entered}, candidate {candidates}, rolled {rolled}, succeeded {succeeded}");
        }
        var strong = Attempts(1.0);
        var weak = Attempts(0.0);
        Assert.True(strong.Succeeded > weak.Succeeded, $"strong: {strong.Info}; weak: {weak.Info}");
    }

    [Fact]
    public void UnknownFieldsInTheBlockAreRejected()
    {
        var json = File.ReadAllText(TacticLoaderTests.TacticPath("laag_blok_442")).TrimEnd().TrimEnd('}')
            + ",\"standaardsituaties\":{\"corner_vor\":\"kort\"}}";
        Assert.Throws<TacticLoadException>(() => TacticLoader.Parse(json));
    }
}
