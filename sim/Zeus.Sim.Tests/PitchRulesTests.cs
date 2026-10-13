using Zeus.Sim;
namespace Zeus.Sim.Tests;

public class PitchRulesTests
{
    private static BallState Ball(double x, double y = 0, double z = 0) => new(new Vec3(x, y, z), Vec3.Zero);

    [Theory]
    [InlineData(1, Side.Home, Side.Away)]
    [InlineData(1, Side.Away, Side.Home)]
    [InlineData(-1, Side.Home, Side.Away)]
    [InlineData(-1, Side.Away, Side.Home)]
    public void ThrowInGoesToOpponentOfLastTouch(int sign, Side last, Side restart)
    {
        var exit = PitchRules.Evaluate(Ball(5, sign * 33), Ball(7, sign * 35), last)!.Value;
        Assert.Equal(PitchExitKind.ThrowIn, exit.Kind);
        Assert.Equal(restart, exit.RestartTeam);
        Assert.Null(exit.ScoringTeam);
        Assert.Equal(0.555, exit.Fraction, 12);
        Assert.Equal(6.11, exit.Position.X, 12);
        Assert.Equal(sign * 34.11, exit.Position.Y, 12);
    }

    [Theory]
    [InlineData(1, Side.Home, PitchExitKind.GoalKick, Side.Away)]
    [InlineData(1, Side.Away, PitchExitKind.Corner, Side.Home)]
    [InlineData(-1, Side.Away, PitchExitKind.GoalKick, Side.Home)]
    [InlineData(-1, Side.Home, PitchExitKind.Corner, Side.Away)]
    public void WideEndlineExitChoosesRestartByLastTouch(int sign, Side last, PitchExitKind kind, Side team)
    {
        var exit = PitchRules.Evaluate(Ball(sign * 52, 10), Ball(sign * 54, 10), last)!.Value;
        Assert.Equal(kind, exit.Kind);
        Assert.Equal(team, exit.RestartTeam);
        Assert.Null(exit.ScoringTeam);
        Assert.Equal(sign * 52.61, exit.Position.X, 12);
    }

    [Theory]
    [InlineData(1, Side.Home, Side.Away)]
    [InlineData(-1, Side.Away, Side.Home)]
    public void GoalScorerDependsOnEndNotLastTouch(int sign, Side scorer, Side restart)
    {
        foreach (var last in new[] { Side.Home, Side.Away })
        {
            var exit = PitchRules.Evaluate(Ball(sign * 52), Ball(sign * 54), last)!.Value;
            Assert.Equal(PitchExitKind.Goal, exit.Kind);
            Assert.Equal(scorer, exit.ScoringTeam);
            Assert.Equal(restart, exit.RestartTeam);
        }
    }

    [Fact]
    public void WholeBallMustPassLineNotJustItsCentreOrLeadingEdge()
    {
        Assert.Null(PitchRules.Evaluate(Ball(52.4), Ball(52.5), Side.Home));
        Assert.Null(PitchRules.Evaluate(Ball(52.4), Ball(52.6), Side.Home));
        Assert.Null(PitchRules.Evaluate(Ball(52.4), Ball(Pitch.HalfLength + 0.11), Side.Home));
        Assert.NotNull(PitchRules.Evaluate(Ball(52.4), Ball(52.62), Side.Home));
        Assert.Null(PitchRules.Evaluate(Ball(0, 33.9), Ball(0, 34.1), Side.Home));
        Assert.Null(PitchRules.Evaluate(Ball(0, 33.9), Ball(0, Pitch.HalfWidth + 0.11), Side.Home));
        Assert.NotNull(PitchRules.Evaluate(Ball(0, 33.9), Ball(0, 34.12), Side.Home));
    }

    [Fact]
    public void GoalOpeningRequiresWholeBallBelowBarAndBetweenPosts()
    {
        var valid = PitchRules.Evaluate(Ball(52, 3.54, 2.32), Ball(54, 3.54, 2.32), Side.Home)!.Value;
        Assert.Equal(PitchExitKind.Goal, valid.Kind);
        Assert.Equal(PitchExitKind.GoalKick,
            PitchRules.Evaluate(Ball(52, 3.56), Ball(54, 3.56), Side.Home)!.Value.Kind);
        Assert.Equal(PitchExitKind.GoalKick,
            PitchRules.Evaluate(Ball(52, z: 2.34), Ball(54, z: 2.34), Side.Home)!.Value.Kind);
        Assert.Equal(PitchExitKind.GoalKick,
            PitchRules.Evaluate(Ball(52, z: -0.1), Ball(54, z: -0.1), Side.Home)!.Value.Kind);
    }

    [Fact]
    public void BallFitsExactlyAtEitherInsidePostAndUnderBar()
    {
        double y = PitchRules.GoalWidth / 2 - 0.11;
        double z = PitchRules.GoalHeight - 0.11;
        foreach (int sign in new[] { -1, 1 })
            Assert.Equal(PitchExitKind.Goal,
                PitchRules.Evaluate(Ball(52, sign * y, z), Ball(54, sign * y, z), Side.Home)!.Value.Kind);
    }

    [Fact]
    public void GoalHeightIsMeasuredAtCrossingRatherThanSubstepEnd()
    {
        // Crosses at t=.305, z=1.305; final height 2m remains irrelevant to the crossing.
        var goal = PitchRules.Evaluate(Ball(52, z: 1), Ball(54, z: 2), Side.Home)!.Value;
        Assert.Equal(PitchExitKind.Goal, goal.Kind);
        // Descends below the bar only after crossing the line.
        var high = PitchRules.Evaluate(Ball(52, z: 3), Ball(54, z: 1), Side.Home)!.Value;
        Assert.Equal(PitchExitKind.GoalKick, high.Kind);
    }

    [Fact]
    public void EarliestCrossingWinsWhenBothAxesFinishOutside()
    {
        var touchFirst = PitchRules.Evaluate(Ball(52, 34), Ball(54, 36), Side.Home)!.Value;
        Assert.Equal(PitchExitKind.ThrowIn, touchFirst.Kind);
        Assert.Equal(0.055, touchFirst.Fraction, 12);
        var endFirst = PitchRules.Evaluate(Ball(52.5, 33), Ball(54.5, 35), Side.Home)!.Value;
        Assert.Equal(PitchExitKind.GoalKick, endFirst.Kind);
        Assert.Equal(0.055, endFirst.Fraction, 12);
    }

    [Fact]
    public void ExactlySimultaneousCornerCrossingHasStableEndlinePriority()
    {
        // Using radius zero makes the equal t=.5 case exact in binary.
        var exit = PitchRules.Evaluate(Ball(52, 33.5), Ball(53, 34.5), Side.Away, 0)!.Value;
        Assert.Equal(PitchExitKind.Corner, exit.Kind);
        Assert.Equal(0.5, exit.Fraction);
        Assert.Equal(Side.Home, exit.RestartTeam);
    }

    [Fact]
    public void InwardAndAlreadyOutsideSegmentsDoNotRepeatAnExit()
    {
        Assert.Null(PitchRules.Evaluate(Ball(53), Ball(52), Side.Home));
        Assert.Null(PitchRules.Evaluate(Ball(53), Ball(54), Side.Home));
        Assert.Null(PitchRules.Evaluate(Ball(0, 35), Ball(0, 36), Side.Home));
        Assert.Null(PitchRules.Evaluate(Ball(1), Ball(1), Side.Home));
    }

    [Fact]
    public void RadiusIsConfigurableAndInvalidInputsAreRejected()
    {
        Assert.Null(PitchRules.Evaluate(Ball(52), Ball(52.7), Side.Home, 0.3));
        Assert.NotNull(PitchRules.Evaluate(Ball(52), Ball(52.7), Side.Home, 0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitchRules.Evaluate(Ball(0), Ball(0), Side.Home, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitchRules.Evaluate(Ball(0), Ball(0), Side.Home, double.NaN));
        Assert.Throws<ArgumentException>(() => PitchRules.Evaluate(Ball(double.NaN), Ball(0), Side.Home));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitchRules.Evaluate(Ball(0), Ball(0), (Side)10));
    }
}
