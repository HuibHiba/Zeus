using Zeus.Sim;

public class CrowdModelTests
{
    static Snapshot Frame(double seconds, int home = 0, int away = 0, double ballX = 0) =>
        new(1, seconds, Array.Empty<PlayerSnapshot>(), new BallSnapshot(new Vec3(ballX, 0, 0), new Vec3(0, 0, 0)), HomeScore: home, AwayScore: away);

    static Event At(EventType type, double seconds, Side team = Side.Home, string outcome = "") =>
        new(type, team, "", (long)(seconds * 20), seconds, new Vec2(0, 0), outcome);

    static readonly Event[] None = Array.Empty<Event>();

    [Fact]
    public void GoalLiftsTheCrowdAndItSettlesWithinFourSeconds()
    {
        var crowd = new CrowdModel();
        crowd.Update(Frame(600), None);
        double before = crowd.Intensity;
        crowd.Update(Frame(600.05, home: 1), new[] { At(EventType.Goal, 600.05) });
        Assert.True(crowd.Intensity >= before + 0.5, $"{before} -> {crowd.Intensity}");
        for (double t = 600.1; t <= 604.05; t += 0.05) crowd.Update(Frame(t, home: 1), None);
        Assert.InRange(crowd.Intensity, CrowdModel.Base(1, 0, 10), CrowdModel.Base(1, 0, 10) + 0.1);
    }

    [Fact]
    public void TightScoreLateInTheMatchSitsHigherThanAQuietStart()
    {
        var early = new CrowdModel(); early.Update(Frame(10 * 60), None);
        var late = new CrowdModel(); late.Update(Frame(88 * 60, 2, 2), None);
        Assert.True(late.Intensity >= early.Intensity + 0.2, $"{early.Intensity} vs {late.Intensity}");
        Assert.True(CrowdModel.Base(2, 2, 88) > CrowdModel.Base(0, 0, 10));
        Assert.True(CrowdModel.Base(0, 4, 88) < CrowdModel.Base(2, 2, 88));     // a decided match does not get the late tension
    }

    [Fact]
    public void ReactionsArriveInTheTickOfTheirEvent()
    {
        var crowd = new CrowdModel();
        Assert.Equal(new[] { CrowdModel.Reaction.Ooh }, crowd.Update(Frame(100), new[] { At(EventType.Save, 100, Side.Away) }));
        Assert.Equal(new[] { CrowdModel.Reaction.Cheer }, crowd.Update(Frame(100.05, home: 1), new[] { At(EventType.Goal, 100.05) }));
        Assert.Equal(new[] { CrowdModel.Reaction.Groan }, crowd.Update(Frame(100.1, 1, 1), new[] { At(EventType.Goal, 100.1, Side.Away) }));
        Assert.Equal(new[] { CrowdModel.Reaction.Whistles }, crowd.Update(Frame(100.15, 1, 1), new[] { At(EventType.Card, 100.15, Side.Home, "Yellow") }));
        Assert.Equal(new[] { CrowdModel.Reaction.Anticipation }, crowd.Update(Frame(100.2, 1, 1), new[] { At(EventType.RestartAwarded, 100.2, Side.Home, "Corner") }));
        Assert.Equal(new[] { CrowdModel.Reaction.Applause }, crowd.Update(Frame(100.25, 1, 1), new[] { At(EventType.Substitution, 100.25, Side.Away, "A->B") }));
        crowd.Update(Frame(100.3, 1, 1), new[] { At(EventType.Shot, 100.3) });
        Assert.Equal(new[] { CrowdModel.Reaction.Sigh }, crowd.Update(Frame(101, 1, 1), new[] { At(EventType.Out, 101, Side.Home, "GoalKick") }));
        // Same kind again within its gap: rate-limited, no second one-shot.
        Assert.Empty(crowd.Update(Frame(101.05, 1, 1), new[] { At(EventType.Save, 101.05, Side.Away) }));
    }

    [Fact]
    public void BallNearTheGoalRaisesTheIntensityWithoutAnEvent()
    {
        var calm = new CrowdModel(); calm.Update(Frame(300), None);
        var tense = new CrowdModel();
        for (double t = 300; t < 303; t += 0.05) tense.Update(Frame(t, ballX: 45), None);
        Assert.True(tense.Intensity > calm.Intensity + 0.15, $"{calm.Intensity} vs {tense.Intensity}");
    }

    [Fact]
    public void SameInputGivesTheSameOutput()
    {
        static (List<double>, List<CrowdModel.Reaction>) Run()
        {
            var crowd = new CrowdModel();
            var levels = new List<double>(); var shots = new List<CrowdModel.Reaction>();
            for (int i = 0; i < 400; i++)
            {
                double t = i * 0.05;
                var events = i % 60 == 0 ? new[] { At(EventType.Save, t, Side.Away) } : i == 150 ? new[] { At(EventType.Goal, t) } : None;
                shots.AddRange(crowd.Update(Frame(t, i >= 150 ? 1 : 0, 0, ballX: i % 100 < 50 ? 40 : 0), events));
                levels.Add(crowd.Intensity);
            }
            return (levels, shots);
        }
        var (a, ra) = Run(); var (b, rb) = Run();
        Assert.Equal(a, b);
        Assert.Equal(ra, rb);
    }
}
