using System.Text.Json;
using Zeus.Sim;

namespace Zeus.Sim.Tests;

public class SourceShotModelTests
{
    private static string SourcePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "calibration", "wyscout_models.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Wyscout shot calibration source not found above the test output.");
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(10, 0)]
    [InlineData(12, 8)]
    [InlineData(12, -8)]
    [InlineData(20, 3)]
    [InlineData(40, 0)]
    [InlineData(54, 0)]
    public void DefaultModelMatchesIndependentFormulaFromWyscoutSource(double distance, double lateral)
    {
        using var source = JsonDocument.Parse(File.ReadAllText(SourcePath()));
        var coefficients = source.RootElement.GetProperty("schot_doelpunt").GetProperty("coefficienten");
        double Coefficient(string name) => coefficients.GetProperty(name).GetDouble();
        // Angle subtended by a 7.32 m goal, derived with the tangent subtraction
        // identity rather than copying the implementation's two atan2 calls.
        double alongSquared = Math.Max(distance * distance - lateral * lateral, .01);
        double angle = Math.Atan2(7.32 * Math.Sqrt(alongSquared), alongSquared + lateral * lateral - 3.66 * 3.66);
        double logit = Coefficient("const") + Coefficient("dist_m") * distance
            + Coefficient("dist_m_sq") * distance * distance / 100 + Coefficient("goal_angle_rad") * angle;
        Assert.Equal(1 / (1 + Math.Exp(-logit)), ShotModel.GoalProbability(distance, lateral), 12);
        var defaults = new ShotConfig();
        Assert.Equal(Coefficient("const"), defaults.GoalLogitIntercept);
        Assert.Equal(Coefficient("dist_m"), defaults.GoalLogitDistance);
        Assert.Equal(Coefficient("dist_m_sq"), defaults.GoalLogitDistanceSquared);
        Assert.Equal(Coefficient("goal_angle_rad"), defaults.GoalLogitAngle);
    }

    [Fact]
    public void CustomGoalModelRoundTripsInMatchConfiguration()
    {
        var shots = new ShotConfig(GoalLogitIntercept: -1.25, GoalLogitDistance: -.2,
            GoalLogitDistanceSquared: .3, GoalLogitAngle: 1.1);
        shots.Validate();
        var json = JsonSerializer.Serialize(new { Shots = shots });
        Assert.Equal(shots, MatchConfigJson.Parse(json).ShotSettings);
        Assert.Equal(new ShotConfig(), MatchConfigJson.Parse("{\"Shots\":{}}").ShotSettings);
        Assert.NotEqual(ShotModel.GoalProbability(10, 0), ShotModel.GoalProbability(10, 0, shots));
    }

    [Fact]
    public void DefaultChanceNeverIncreasesAcrossTheFullPitch()
    {
        double previous = 1;
        for (int quarterMetres = 0; quarterMetres <= 420; quarterMetres++)
        {
            double distance = quarterMetres / 4.0;
            double chance = ShotModel.GoalProbability(distance, 0);
            Assert.InRange(chance, 0, 1);
            Assert.True(chance <= previous, $"Goal chance increased at {distance} m: {previous} -> {chance}");
            previous = chance;
        }
    }

    [Theory]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(105)]
    public void LongRangeExtrapolationCannotCreateAGoodShot(double distance)
    {
        Assert.InRange(ShotModel.GoalProbability(distance, 0), 0, .01);
    }

    [Fact]
    public void ExtrapolationUsesTheConfiguredTurningPointAndKeepsTheActualAngle()
    {
        var shots = new ShotConfig(GoalLogitIntercept: 0, GoalLogitDistance: -1,
            GoalLogitDistanceSquared: 1, GoalLogitAngle: 1);
        // This custom polynomial has its minimum at 50 m. Its distance term
        // remains -25 at 100 m; the visible goal angle still narrows at 100 m.
        double trueAngle = 2 * Math.Atan(3.66 / 100);
        Assert.Equal(1 / (1 + Math.Exp(-(-25 + trueAngle))), ShotModel.GoalProbability(100, 0, shots), 12);
        Assert.True(ShotModel.GoalProbability(100, 0, shots) < ShotModel.GoalProbability(50, 0, shots));
        // A custom non-rising quadratic keeps its original feature values.
        var decreasing = shots with { GoalLogitIntercept = 100, GoalLogitDistanceSquared = -.01, GoalLogitAngle = 0 };
        Assert.Equal(1 / (1 + Math.Exp(1)), ShotModel.GoalProbability(100, 0, decreasing), 12);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void EveryGoalModelCoefficientRejectsNonFiniteValues(double value)
    {
        var defaults = new ShotConfig();
        foreach (var invalid in new[] {
            defaults with { GoalLogitIntercept = value }, defaults with { GoalLogitDistance = value },
            defaults with { GoalLogitDistanceSquared = value }, defaults with { GoalLogitAngle = value } })
            Assert.Throws<ArgumentException>(invalid.Validate);
    }

    private static Perception FreeChance(double held)
    {
        var pos = new Vec2(Pitch.HalfLength - 10, 0);
        return new Perception(0, new PlayerSnapshot(Side.Home, "ST", pos, Vec2.Zero, 0),
            GamePhase.Aanval, true, Vec2.Zero, Array.Empty<PlayerSnapshot>(),
            new BallSnapshot(new Vec3(pos.X, pos.Y, 0), Vec3.Zero), null,
            AttackPositiveX: true, HeldBallSeconds: held);
    }

    private static ShotConfig ConstantChance(double logit) => new(GoalLogitIntercept: logit,
        GoalLogitDistance: 0, GoalLogitDistanceSquared: 0, GoalLogitAngle: 0);

    [Fact]
    public void PassingBrainUsesConfiguredModelForOrdinaryShootingDecision()
    {
        var view = FreeChance(5);
        Assert.Equal(IntentKind.Shoot, new PassingBrain(new PassConfig(), ConstantChance(0)).Decide(view).Kind);
        Assert.NotEqual(IntentKind.Shoot, new PassingBrain(new PassConfig(), ConstantChance(-20)).Decide(view).Kind);
    }

    [Fact]
    public void PassingBrainUsesConfiguredModelForEarlyShootingDecision()
    {
        var config = new PassConfig(EarlyShotXg: .4);
        var view = FreeChance(.5);
        Assert.Equal(IntentKind.Shoot, new PassingBrain(config, ConstantChance(0)).Decide(view).Kind);
        // A 0.2 chance clears the ordinary 0.08 threshold, but not early 0.4.
        Assert.NotEqual(IntentKind.Shoot, new PassingBrain(config, ConstantChance(Math.Log(.2 / .8))).Decide(view).Kind);
    }
}
