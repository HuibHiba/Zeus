using Zeus.Sim;
namespace Zeus.Sim.Tests;

/// <summary>Penalty conversion and the caution of defenders in their own penalty area (docs/kalibratie.md "Ronde 3").</summary>
public class PenaltyTests
{
    private static Match Create(ulong seed, MatchConfig? config = null) => Match.Create(config ?? new MatchConfig(),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
        TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), seed);

    /// <summary>Awards the home team a penalty (away offends at x=40) and plays on until goal, save or ball out.</summary>
    internal static (int Goals, int Saves, int Misses) TakePenalties(int count, MatchConfig? config = null)
    {
        int goals = 0, saves = 0, misses = 0;
        for (ulong seed = 1; seed <= (ulong)count; seed++)
        {
            var match = Create(seed, config);
            match.ReportIncident(Side.Away, "RC", new Vec2(40, 0),
                new RefereeIncident(ContactAction.Tackle, Conduct.Careless, true, true, InsideDefendingPenaltyArea: true,
                    TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true));
            for (int i = 0; i < 400; i++)
            {
                match.Step();
                var events = match.Events.Events;
                if (events.Any(e => e.Type == EventType.Goal)) { goals++; break; }
                if (events.Any(e => e.Type == EventType.Save)) { saves++; break; }
                if (events.Any(e => e.Type == EventType.Out)) { misses++; break; }
            }
        }
        return (goals, saves, misses);
    }

    [Fact]
    public void ConversionOf200PenaltiesIsBetween65And85Percent()
    {
        var (goals, saves, misses) = TakePenalties(200);
        Assert.Equal(200, goals + saves + misses);
        Assert.InRange(goals / 200.0, 0.65, 0.85);
        Assert.True(saves >= 10, $"saves {saves}");    // the keeper does save some ...
        Assert.True(misses >= 5, $"misses {misses}");  // ... and the taker does miss some
    }

    [Fact]
    public void ConversionIsDeterministicForTheSameSeeds()
    {
        Assert.Equal(TakePenalties(20), TakePenalties(20));
    }

    [Fact]
    public void CornerAimBeatsTheOldFixedAimOf2Point4AndTheOpenPlayKeeperReach()
    {
        // The earlier model aimed at +-2.4 m, inside the keeper's 2.5 m reach: ~8% scored. Each parameter matters on its own.
        double rate((int Goals, int Saves, int Misses) r) => r.Goals / 200.0;
        double corner = rate(TakePenalties(200));
        double oldAim = rate(TakePenalties(200, new MatchConfig(Penalties: new PenaltyConfig(AimOffsetMeters: 2.4))));
        double wideReach = rate(TakePenalties(200, new MatchConfig(Penalties: new PenaltyConfig(KeeperReachMeters: 2.5))));
        Assert.True(corner > oldAim + 0.15, $"corner {corner} old aim {oldAim}");
        Assert.True(corner > wideReach + 0.15, $"corner {corner} reach 2.5 {wideReach}");
    }

    [Fact]
    public void PenaltyVelocityAimsAtTheCornerOfTheRequestedSide()
    {
        var penalty = PenaltyConfig.Default;
        var rng = new Rng(3);
        var velocityPositive = PenaltyExecution.Velocity(new Vec2(41.5, 0), 52.5, 1, 1.0, rng, penalty, new ShotConfig());
        var velocityNegative = PenaltyExecution.Velocity(new Vec2(41.5, 0), 52.5, -1, 1.0, rng, penalty, new ShotConfig());
        // Skill 1: no error; the ball reaches the goal line at +-AimOffsetMeters.
        double toGoal = 52.5 - 41.5;
        Assert.Equal(penalty.AimOffsetMeters, velocityPositive.Y / velocityPositive.X * toGoal, 6);
        Assert.Equal(-penalty.AimOffsetMeters, velocityNegative.Y / velocityNegative.X * toGoal, 6);
    }

    [Fact]
    public void PenaltyConfigValidationRejectsNonsense()
    {
        Assert.Throws<ArgumentException>(() => new PenaltyConfig(AimOffsetMeters: double.NaN).Validate());
        Assert.Throws<ArgumentException>(() => new PenaltyConfig(KeeperDiveSeconds: 0).Validate());
        Assert.Throws<ArgumentException>(() => new PenaltyConfig(KeeperDiveMeters: 5).Validate());
        PenaltyConfig.Default.Validate();
    }

    // ---- caution inside the own penalty area ----

    [Fact]
    public void FoulChanceInOwnPenaltyAreaIsScaledAndTheRestBecomesNoChallenge()
    {
        var config = new DuelConfig(PenaltyAreaFoulFactor: 0.25);
        var normal = DuelModel.Chances(0.5, 4, 1, 0.6, 0.75, 0, config);
        var careful = DuelModel.Chances(0.5, 4, 1, 0.6, 0.75, 0, config, inOwnPenaltyArea: true);
        Assert.Equal(normal.Foul * 0.25, careful.Foul, 12);
        Assert.Equal(normal.WonBall, careful.WonBall, 12);
        Assert.Equal(normal.LooseBall, careful.LooseBall, 12);
        Assert.Equal(1.0, careful.WonBall + careful.LooseBall + careful.Foul + careful.None, 12);
        Assert.Equal(normal.None + normal.Foul * 0.75, careful.None, 12);
    }

    [Fact]
    public void FactorOneMeansNoCautionAndInvalidFactorIsRejected()
    {
        var config = new DuelConfig(PenaltyAreaFoulFactor: 1);
        Assert.Equal(DuelModel.Chances(0.5, 4, 1, 0.6, 0.75, 0, config),
            DuelModel.Chances(0.5, 4, 1, 0.6, 0.75, 0, config, inOwnPenaltyArea: true));
        Assert.Throws<ArgumentException>(() => new DuelConfig(PenaltyAreaFoulFactor: 1.5).Validate());
    }

    [Fact]
    public void DefaultCautionGivesFarFewerPenaltiesThanNoCaution()
    {
        // 8 shortened matches (10-minute halves, more challenges so that penalties occur); only the caution factor differs.
        int Penalties(double factor)
        {
            int count = 0;
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var match = Match.Create(new MatchConfig(HalfSeconds: 600, Duels: new DuelConfig(AttemptRate: 0.15, PenaltyAreaFoulFactor: factor)),
                    TacticLoader.LoadFile(TacticLoaderTests.TacticPath("laag_blok_442")),
                    TacticLoader.LoadFile(TacticLoaderTests.TacticPath("man_om_man_433")), seed);
                for (int i = 0; i < 20000 && match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); i++) match.Step();
                count += match.Events.Events.Count(e => e.Type == EventType.RestartAwarded && e.Outcome == "penalty");
            }
            return count;
        }
        int uncautious = Penalties(1), cautious = Penalties(DuelConfig.Default.PenaltyAreaFoulFactor);
        Assert.True(uncautious >= 4, $"without caution {uncautious}");
        Assert.True(cautious * 3 <= uncautious, $"without caution {uncautious}, with {cautious}");
    }
}
