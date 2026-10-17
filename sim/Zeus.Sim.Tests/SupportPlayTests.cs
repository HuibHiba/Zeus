using Zeus.Sim;

namespace Zeus.Sim.Tests;

public class SupportPlayTests
{
    static Perception Blocked(int direction = 1)
    {
        PlayerSnapshot Player(Side side, string id, double x, double y) =>
            new(side, id, new Vec2(x, y) * direction, Vec2.Zero, 0);
        return new Perception(0, Player(Side.Home, "RDM", 0, 0), GamePhase.Opbouw, false,
            new Vec2(0, 0), new[] { Player(Side.Home, "RC", -15, 0),
                Player(Side.Away, "M", -7, 0), Player(Side.Away, "D", 25, 10) },
            new BallSnapshot(new Vec3(-15 * direction, 0, 0), Vec3.Zero), null,
            AttackPositiveX: direction > 0, TeamBallCarrierId: "RC");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void MarkedPassingLaneGetsALocalSupportingAngle(int direction)
    {
        var view = Blocked(direction);
        var target = SupportPlay.Target(view, view.FormationTarget);
        var carrier = view.VisiblePlayers[0].Pos;
        var marker = view.VisiblePlayers[1].Pos;
        Assert.NotEqual(view.FormationTarget, target);
        Assert.InRange((target - view.FormationTarget).Length, 0, 6.5);
        Assert.True(PassChoice.DistanceToSegment(marker, carrier, target)
            > PassChoice.DistanceToSegment(marker, carrier, view.FormationTarget) + 1);
        Assert.True((target - carrier).Length >= 5);
    }

    [Fact]
    public void SupportingAnglesMirrorWithEnds()
    {
        Assert.Equal(SupportPlay.Target(Blocked(), Vec2.Zero) * -1,
            SupportPlay.Target(Blocked(-1), Vec2.Zero));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void DefaultInstructionsOfferAnAngleInsteadOfHoldingABlockedPosition(int direction)
    {
        var view = Blocked(direction);
        var layout = new PhasePlayer("RDM", .5, .5, null, [], null);
        var team = new TeamSettings(.5, .5, "zone", .25, 0);
        var target = InstructionRules.Target(view, layout, layout, team, .25, Vec2.Zero, new());
        Assert.NotEqual(view.FormationTarget, target);
        Assert.Equal(SupportPlay.Target(view, view.FormationTarget), target);
        Assert.Equal(view.FormationTarget, InstructionRules.Target(view, layout, layout, team, .25,
            Vec2.Zero, new(EnableSupport: false)));
    }

    [Theory]
    [InlineData("defending")]
    [InlineData("stopped")]
    [InlineData("carrier")]
    [InlineData("keeper")]
    [InlineData("loose")]
    [InlineData("inflight")]
    [InlineData("receiver")]
    [InlineData("hidden")]
    public void AssignedShapeIsRetainedOutsideVisiblePossessionSupport(string scenario)
    {
        var view = Blocked();
        view = scenario switch {
            "defending" => view with { Phase = GamePhase.Verdedigen },
            "stopped" => view with { Phase = GamePhase.Stilstaand },
            "carrier" => view with { HasBall = true },
            "keeper" => view with { Self = view.Self with { IsGoalkeeper = true } },
            "loose" => view with { BallIsLoose = true },
            "inflight" => view with { TeamPassInFlight = true },
            "receiver" => view with { ReceiveTarget = new(1, 2) },
            _ => view with { VisiblePlayers = view.VisiblePlayers.Skip(1).ToArray() }
        };
        Assert.Equal(view.FormationTarget, SupportPlay.Target(view, view.FormationTarget));
    }

    [Fact]
    public void DistantPlayersKeepTheirTacticalAreaAndOpenOptionsDoNotWander()
    {
        var view = Blocked();
        Assert.Equal(new Vec2(30, 0), SupportPlay.Target(view, new Vec2(30, 0)));
        view = view with { VisiblePlayers = view.VisiblePlayers.Select(p => p.Team == Side.Away
            ? p with { Pos = p.Pos + new Vec2(0, 20) } : p).ToArray() };
        Assert.Equal(view.FormationTarget, SupportPlay.Target(view, view.FormationTarget));
    }

    [Fact]
    public void HardWidthAndRestDefenceStillWinAndSupportCanBeDisabled()
    {
        var view = Blocked() with { Instructions = new[] { "blijf_achter", "blijf_breed" } };
        var layout = new PhasePlayer("RDM", .5, .8, null, [], null);
        var rest = layout with { X = .25 };
        var team = new TeamSettings(.5, .5, "zone", .25, 0);
        var target = InstructionRules.Target(view, layout, rest, team, .25, Vec2.Zero, new(EnableSupport: true));
        Assert.True(target.X <= -26.25);
        Assert.True(target.Y >= 25);
        Assert.Equal(Vec2.Zero, InstructionRules.Target(Blocked(), layout, rest, team, .25, Vec2.Zero,
            InstructionConfig.Disabled));
    }
}
