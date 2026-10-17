using Zeus.Sim;
using Xunit.Abstractions;

namespace Zeus.Sim.Tests;

public class KickOffShapeTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases => new[] { "hoge_druk_433", "positiespel_4231", "counter_532" }
        .SelectMany(name => new[] { "initial", "home_goal", "away_goal", "second_half", "home_goal_second_half", "away_goal_second_half" }
            .Select(stage => new object[] { name, stage }));

    [Theory]
    [MemberData(nameof(Cases))]
    public void KickOffKeepsASeparatedOwnHalfShapeAndLegalCentreCircle(string name, string stage)
    {
        var tactic = TacticLoader.LoadFile(TacticLoaderTests.TacticPath(name));
        var match = Match.Create(new MatchConfig(HalfSeconds: 5), tactic, tactic, 42,
            (_, _) => new FormationBrain());
        if (stage.EndsWith("second_half", StringComparison.Ordinal))
        {
            for (int i = 0; i < 100 && match.Period == 1; i++) match.Step();
            Assert.Equal(2, match.Period);
        }
        if (stage is not ("initial" or "second_half"))
        {
            if (match.Period == 2)
            {
                for (int i = 0; i < 100 && match.Status == MatchStatus.Restart; i++) match.Step();
                // A second player must touch the ball before injecting a new open-play goal.
                var receiver = match.GetSnapshot().Players.Single(p => p.Team == Side.Home && p.Id == "RC");
                match.SetBall(new Vec3(receiver.Pos.X, receiver.Pos.Y, 0), Vec3.Zero);
                match.Step();
            }
            int goalDirection = (stage.StartsWith("home_goal", StringComparison.Ordinal) ? 1 : -1)
                * (match.EndsSwapped ? -1 : 1);
            match.SetBall(new Vec3(goalDirection * 52.4, 0, 0), new Vec3(goalDirection * 10, 0, 0));
            match.Step();
            Assert.Equal(MatchStatus.Restart, match.Status);
            Assert.Equal(1, match.HomeScore + match.AwayScore);
        }
        var snapshot = match.GetSnapshot();
        var kicking = stage == "initial" || stage.StartsWith("away_goal", StringComparison.Ordinal) ? Side.Home : Side.Away;
        Assert.Equal(kicking, match.Possession);
        Assert.Equal(Vec3.Zero, snapshot.Ball!.Pos);
        foreach (Side side in Enum.GetValues<Side>())
        {
            double ownSign = (side == Side.Home ? -1 : 1) * (match.EndsSwapped ? -1 : 1);
            var players = snapshot.Players.Where(p => p.Team == side && !p.SentOff).ToArray();
            var taker = side == kicking ? players.MinBy(p => p.Pos.Length) : null;
            var field = players.Where(p => !p.IsKeeper && p != taker).ToArray();
            for (int i = 0; i < players.Length; i++)
                for (int j = i + 1; j < players.Length; j++)
                    Assert.True((players[i].Pos - players[j].Pos).Length >= .5,
                        $"{side}: {players[i].Id}/{players[j].Id} overlap at kick-off");
            output.WriteLine($"{name}/{stage}/{side}: " + string.Join("; ", players.Select(p => $"{p.Id}=({p.Pos.X:F2},{p.Pos.Y:F2})")));
            Assert.All(players, p => Assert.True(p.Pos.X * ownSign >= -1e-8, $"{side}/{p.Id} outside own half: {p.Pos}"));
            Assert.All(field, p => Assert.True(p.Pos.X * ownSign >= 1 - 1e-8, $"{side}/{p.Id} collapsed onto centre line: {p.Pos}"));
            Assert.True(field.Max(p => p.Pos.X * ownSign) - field.Min(p => p.Pos.X * ownSign) >= 10,
                $"{side}: formation lost its longitudinal depth");
            if (side != kicking)
                Assert.All(players, p => Assert.True(p.Pos.Length >= 9.15 - 1e-8, $"{side}/{p.Id} inside centre circle: {p.Pos}"));
            else Assert.InRange(taker!.Pos.Length, 0, .71);
        }
        // The first normal decision may move a player, but cannot teleport the formation back onto a line.
        for (int i = 0; i < 100 && match.Status == MatchStatus.Restart; i++) match.Step();
        Assert.Equal(MatchStatus.Running, match.Status);
        var before = match.GetSnapshot();
        match.Step();
        var after = match.GetSnapshot();
        Assert.All(before.Players, p => Assert.InRange((after.Players.Single(q => q.Team == p.Team && q.Id == p.Id).Pos - p.Pos).Length, 0, 1));
        Assert.All(Enum.GetValues<Side>(), side =>
        {
            var xs = after.Players.Where(p => p.Team == side && !p.IsKeeper).Select(p => p.Pos.X).ToArray();
            Assert.True(xs.Max() - xs.Min() >= 10);
        });
    }
}
