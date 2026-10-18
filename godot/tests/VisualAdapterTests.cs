using Zeus.Sim;

public class VisualAdapterTests
{
    static readonly IReadOnlyList<Event> NoEvents = Array.Empty<Event>();

    static PlayerSnapshot P(Side team, string id, double x, double y, double vx = 0, double vy = 0, double heading = 0,
        bool keeper = false) => new(team, id, new Vec2(x, y), new Vec2(vx, vy), heading, IsGoalkeeper: keeper);

    static Snapshot Snap(long tick, PlayerSnapshot[] players, Vec3? ball = null, Vec3? ballVel = null, bool swapped = false) =>
        new(tick, tick * 0.1, players, new BallSnapshot(ball ?? new Vec3(0, 0, 0), ballVel ?? Vec3.Zero), EndsSwapped: swapped);

    static Event Ev(EventType type, long tick, Side team = Side.Home, string id = "H1", string outcome = "", Vec2? at = null) =>
        new(type, team, id, tick, tick * 0.1, at ?? Vec2.Zero, outcome);

    static void AssertVec(Vec2 expected, Vec2 actual, double tol = 1e-9)
    {
        Assert.InRange(actual.X, expected.X - tol, expected.X + tol);
        Assert.InRange(actual.Y, expected.Y - tol, expected.Y + tol);
    }

    // ---- SimWorld ------------------------------------------------------------------------------------------

    [Fact]
    public void SimToWorldSwapsHeightAndAcrossAxis()
    {
        Assert.Equal(new Vec3(3, 1.5, 4), SimWorld.SimToWorld(new Vec2(3, 4), 1.5));
        Assert.Equal(new Vec3(3, 2, 4), SimWorld.SimToWorld(new Vec3(3, 4, 2)));
        var back = SimWorld.WorldToSim(new Vec3(3, 1.5, 4), out var h);
        Assert.Equal(new Vec2(3, 4), back);
        Assert.Equal(1.5, h);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.7)]
    [InlineData(-0.7)]
    [InlineData(Math.PI / 2)]
    [InlineData(-Math.PI / 2)]
    [InlineData(3.1)]
    [InlineData(-3.1)]
    public void YawMakesTheModelFaceTheSimHeading(double heading)
    {
        var forward = SimWorld.ForwardFromYaw(SimWorld.YawFromHeading(heading));
        Assert.InRange(forward.X, Math.Cos(heading) - 1e-9, Math.Cos(heading) + 1e-9);
        Assert.InRange(forward.Z, Math.Sin(heading) - 1e-9, Math.Sin(heading) + 1e-9);
        Assert.Equal(0, forward.Y);
    }

    [Fact]
    public void MirroredHeadingsGiveMirroredWorldDirections()
    {
        // Sim +y maps to world +z; the handedness flip means opposite headings give opposite z.
        var left = SimWorld.ForwardFromYaw(SimWorld.YawFromHeading(0.5));
        var right = SimWorld.ForwardFromYaw(SimWorld.YawFromHeading(-0.5));
        Assert.InRange(left.X - right.X, -1e-9, 1e-9);
        Assert.InRange(left.Z + right.Z, -1e-9, 1e-9);
        Assert.True(left.Z > 0);
        Assert.InRange(SimWorld.YawFromHeading(0), -Math.PI - 1e-9, Math.PI + 1e-9);
    }

    // ---- Interpolation -------------------------------------------------------------------------------------

    [Fact]
    public void HermiteHitsTheSnapshotPositionsExactlyAndStaysSmoothBetween()
    {
        var buffer = new SnapshotBuffer();
        buffer.Push(Snap(10, new[] { P(Side.Home, "H1", 0, 0, 5, 0) }), NoEvents);
        buffer.Push(Snap(11, new[] { P(Side.Home, "H1", 0.5, 0, 5, 0) }), NoEvents);
        var f = buffer.Sample(10);
        AssertVec(new Vec2(0, 0), f.Players[0].Pos, 0);
        f = buffer.Sample(11);
        AssertVec(new Vec2(0.5, 0), f.Players[0].Pos, 0);
        f = buffer.Sample(10.5);
        AssertVec(new Vec2(0.25, 0), f.Players[0].Pos);
        Assert.Equal(1, f.PlayerCount);
    }

    [Fact]
    public void StandingPlayersNeverOvershoot()
    {
        var buffer = new SnapshotBuffer();
        for (long t = 0; t < 5; t++) buffer.Push(Snap(t, new[] { P(Side.Home, "H1", 7, -3) }), NoEvents);
        for (double v = 0; v <= 4; v += 0.07)
            AssertVec(new Vec2(7, -3), buffer.Sample(v).Players[0].Pos, 0);
    }

    [Fact]
    public void AcceleratingPlayerStaysWithinTheSegmentBounds()
    {
        var buffer = new SnapshotBuffer();
        buffer.Push(Snap(0, new[] { P(Side.Home, "H1", 0, 0, 0, 0) }), NoEvents);
        buffer.Push(Snap(1, new[] { P(Side.Home, "H1", 0.1, 0, 2, 0) }), NoEvents);
        for (double v = 0; v <= 1; v += 0.05)
            Assert.InRange(buffer.Sample(v).Players[0].Pos.X, -0.01, 0.11);
    }

    [Fact]
    public void HeadingTurnsThroughPiNotThroughZero()
    {
        var buffer = new SnapshotBuffer();
        buffer.Push(Snap(0, new[] { P(Side.Home, "H1", 0, 0, heading: 3.1) }), NoEvents);
        buffer.Push(Snap(1, new[] { P(Side.Home, "H1", 0, 0, heading: -3.1) }), NoEvents);
        for (double v = 0.1; v < 1; v += 0.1)
            Assert.True(Math.Abs(buffer.Sample(v).Players[0].Heading) > 3.0, $"heading at {v}");
        Assert.Equal(3.1, buffer.Sample(0).Players[0].Heading, 12);
        Assert.Equal(-3.1, buffer.Sample(1).Players[0].Heading, 12);
        Assert.InRange(SnapshotBuffer.LerpAngle(-3.1, 3.1, 0.5), -3.2, 3.2);
        Assert.True(Math.Abs(SnapshotBuffer.LerpAngle(-3.1, 3.1, 0.5)) > 3.0);
    }

    [Fact]
    public void TeleportsAboveThreeMetresPerTickCutInsteadOfInterpolating()
    {
        var buffer = new SnapshotBuffer();
        buffer.Push(Snap(0, new[] { P(Side.Home, "H1", 0, 0) }), NoEvents);
        buffer.Push(Snap(1, new[] { P(Side.Home, "H1", 10, 0) }), NoEvents);
        var f = buffer.Sample(0.5);
        AssertVec(new Vec2(0, 0), f.Players[0].Pos, 0);
        Assert.True(f.Teleported);
        f = buffer.Sample(1);
        AssertVec(new Vec2(10, 0), f.Players[0].Pos, 0);
        Assert.False(f.Teleported);
    }

    [Fact]
    public void BallTeleportAlsoCuts()
    {
        var buffer = new SnapshotBuffer();
        buffer.Push(Snap(0, new[] { P(Side.Home, "H1", 0, 0) }, new Vec3(0, 0, 0)), NoEvents);
        buffer.Push(Snap(1, new[] { P(Side.Home, "H1", 0, 0) }, new Vec3(30, 5, 0)), NoEvents);
        var f = buffer.Sample(0.5);
        Assert.Equal(new Vec3(0, 0, 0), f.BallPos);
        Assert.True(f.Teleported);
    }

    [Fact]
    public void BallKinksAtAContactEventAndCurvesWithoutOne()
    {
        Vec3 Mid(IReadOnlyList<Event> events)
        {
            var buffer = new SnapshotBuffer();
            var players = new[] { P(Side.Home, "H1", 0, 0) };
            buffer.Push(Snap(0, players, new Vec3(0, 0, 0), new Vec3(10, 0, 0)), NoEvents);
            buffer.Push(Snap(1, players, new Vec3(1, 0, 0), new Vec3(10, 0, 0)), NoEvents);
            buffer.Push(Snap(2, players, new Vec3(1, 1, 0), new Vec3(0, 10, 0)), events);
            return buffer.Sample(1.5).BallPos;
        }
        var kinked = Mid(new[] { Ev(EventType.Pass, 2) });
        Assert.Equal(1, kinked.X, 12);
        Assert.Equal(0.5, kinked.Y, 12);
        var smooth = Mid(NoEvents);
        Assert.NotEqual(0.5, smooth.Y, 6);
        foreach (var type in new[] { EventType.Pass, EventType.Shot, EventType.BallControl, EventType.FirstTouch,
            EventType.Save, EventType.Interception })
            Assert.True(SnapshotBuffer.IsContactEvent(type));
        Assert.False(SnapshotBuffer.IsContactEvent(EventType.Foul));
    }

    [Fact]
    public void PlayersMissingFromTheNextTickAreHeldAndMarkedLeaving()
    {
        var buffer = new SnapshotBuffer();
        buffer.Push(Snap(0, new[] { P(Side.Home, "H1", 0, 0, 2, 0), P(Side.Home, "H2", 5, 5) }), NoEvents);
        buffer.Push(Snap(1, new[] { P(Side.Home, "H1", 0.2, 0, 2, 0) }), NoEvents);
        var f = buffer.Sample(0.5);
        Assert.Equal(2, f.PlayerCount);
        Assert.True(f.Players[1].Leaving);
        AssertVec(new Vec2(5, 5), f.Players[1].Pos, 0);
        Assert.False(f.Players[0].Leaving);
        Assert.Equal(1, buffer.Sample(1).PlayerCount);
    }

    [Fact]
    public void RingDropsOldestTicksAndAGapClearsTheBuffer()
    {
        var buffer = new SnapshotBuffer(4);
        var players = new[] { P(Side.Home, "H1", 0, 0) };
        for (long t = 0; t < 6; t++) buffer.Push(Snap(t, players), t == 4 ? new[] { Ev(EventType.Pass, 4) } : NoEvents);
        Assert.Equal(2, buffer.OldestTick);
        Assert.Equal(5, buffer.NewestTick);
        Assert.Null(buffer.At(1));
        Assert.Equal(2, buffer.Sample(-7).Tick);
        Assert.Equal(5, buffer.Sample(99).Tick);
        Assert.Single(buffer.EventsBetween(3, 5));
        Assert.Empty(buffer.EventsBetween(4, 5));
        buffer.Push(Snap(20, players), NoEvents);
        Assert.Equal(1, buffer.Count);
        Assert.Equal(20, buffer.OldestTick);
    }

    [Fact]
    public void UpcomingEventsCoverTheLookaheadWindow()
    {
        var buffer = new SnapshotBuffer();
        var players = new[] { P(Side.Home, "H1", 0, 0) };
        for (long t = 40; t <= 60; t++)
            buffer.Push(Snap(t, players), t is 50 or 55 ? new[] { Ev(EventType.Pass, t) } : NoEvents);
        Assert.Single(buffer.UpcomingEvents(46, 4));
        Assert.Single(buffer.UpcomingEvents(50, 4));
        Assert.Equal(2, buffer.UpcomingEvents(50, 5).Count);
        Assert.Empty(buffer.UpcomingEvents(51, 3));
    }

    // ---- Locomotion ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0.0, 0.0, 0.0, Locomotion.Idle)]
    [InlineData(0.29, 0.0, 0.0, Locomotion.Idle)]
    [InlineData(0.3, 0.0, 0.0, Locomotion.Walk)]
    [InlineData(1.99, 0.0, 0.0, Locomotion.Walk)]
    [InlineData(2.0, 0.0, 0.0, Locomotion.Jog)]
    [InlineData(4.99, 0.0, 0.0, Locomotion.Jog)]
    [InlineData(5.0, 0.0, 0.0, Locomotion.Sprint)]
    [InlineData(8.0, 0.0, 0.0, Locomotion.Sprint)]
    [InlineData(3.0, 0.0, Math.PI, Locomotion.Backpedal)]     // 180 degrees
    [InlineData(3.0, 0.0, 2.0, Locomotion.Backpedal)]         // about 115 degrees
    [InlineData(3.0, 0.0, 1.5, Locomotion.Strafe)]            // about 86 degrees
    [InlineData(3.0, 0.0, 1.1, Locomotion.Strafe)]            // about 63 degrees
    [InlineData(3.0, 0.0, 0.9, Locomotion.Jog)]               // about 52 degrees
    public void LocomotionFollowsTheThresholdTable(double speed, double velAngle, double heading, Locomotion expected)
    {
        var vel = new Vec2(speed * Math.Cos(velAngle), speed * Math.Sin(velAngle));
        Assert.Equal(expected, VisualAdapter.ClassifyLocomotion(vel, heading));
    }

    [Fact]
    public void LateralSpeedIsMeasuredPerpendicularToTheHeading()
    {
        Assert.Equal(4, VisualAdapter.LateralSpeed(new Vec2(0, 4), 0), 12);
        Assert.Equal(0, VisualAdapter.LateralSpeed(new Vec2(4, 0), 0), 12);
        Assert.Equal(4, VisualAdapter.LateralSpeed(new Vec2(4, 0), Math.PI / 2), 12);
    }

    // ---- Actions -------------------------------------------------------------------------------------------

    static SnapshotBuffer BufferWith(long from, long to, Func<long, PlayerSnapshot[]> players, Func<long, Vec3>? ball,
        params Event[] events)
    {
        var buffer = new SnapshotBuffer();
        for (long t = from; t <= to; t++)
            buffer.Push(Snap(t, players(t), ball?.Invoke(t)), events.Where(e => e.Tick == t).ToArray());
        return buffer;
    }

    static PlayerSnapshot[] Two(long _) => new[] { P(Side.Home, "H1", 0, 0), P(Side.Away, "A1", 20, 0) };

    [Fact]
    public void PassOnTickFiftyStartsTheActionAtTickFortySeven()
    {
        var buffer = BufferWith(40, 60, Two, null, Ev(EventType.Pass, 50, outcome: "to H2 12.000"));
        var adapter = new VisualAdapter();
        adapter.Update(buffer, 46.9);
        Assert.Equal(PlayerAction.None, adapter.Players[0].Action);
        adapter.Update(buffer, 47);
        Assert.Equal(PlayerAction.Pass, adapter.Players[0].Action);
        Assert.Equal(47, adapter.Players[0].ActionStartTick);
        Assert.Equal(PlayerAction.None, adapter.Players[1].Action);
        adapter.Update(buffer, 50);
        Assert.Equal(PlayerAction.Pass, adapter.Players[0].Action);
        adapter.Update(buffer, 54);
        Assert.Equal(PlayerAction.None, adapter.Players[0].Action);
    }

    [Fact]
    public void WindUpIsConfigurableUpToTheLookahead()
    {
        var buffer = BufferWith(40, 60, Two, null, Ev(EventType.Shot, 50));
        var adapter = new VisualAdapter(windUpTicks: 4);
        adapter.Update(buffer, 46);
        Assert.Equal(PlayerAction.Shot, adapter.Players[0].Action);
        Assert.Equal(46, adapter.Players[0].ActionStartTick);
        Assert.Throws<ArgumentOutOfRangeException>(() => new VisualAdapter(5));
    }

    [Theory]
    [InlineData(2.0, PlayerAction.Header)]
    [InlineData(1.31, PlayerAction.Header)]
    [InlineData(1.3, PlayerAction.Control)]
    [InlineData(0.2, PlayerAction.Control)]
    public void ContactAboveOnePointThreeMetresIsAHeader(double ballZ, PlayerAction expected)
    {
        var buffer = BufferWith(40, 60, Two, t => new Vec3(0, 0, t == 50 ? ballZ : 0), Ev(EventType.BallControl, 50));
        var adapter = new VisualAdapter();
        adapter.Update(buffer, 48);
        Assert.Equal(expected, adapter.Players[0].Action);
    }

    [Fact]
    public void ThrowInClearAndSaveMapToTheirOwnActions()
    {
        var buffer = BufferWith(40, 70, t => new[] { P(Side.Home, "H1", 0, 0), P(Side.Away, "GK", 50, 0, keeper: true) }, null,
            Ev(EventType.Pass, 45, outcome: "restart ThrowIn"), Ev(EventType.Pass, 55, outcome: "clear"),
            Ev(EventType.Save, 65, Side.Away, "GK"));
        var adapter = new VisualAdapter();
        adapter.Update(buffer, 45);
        Assert.Equal(PlayerAction.ThrowIn, adapter.Players[0].Action);
        adapter.Update(buffer, 55);
        Assert.Equal(PlayerAction.Shot, adapter.Players[0].Action);
        adapter.Update(buffer, 64);
        Assert.Equal(PlayerAction.KeeperDive, adapter.Players[1].Action);
    }

    [Fact]
    public void KeeperMovingSidewaysFasterThanThreeMetresPerSecondDives()
    {
        var buffer = BufferWith(0, 5, t => new[] { P(Side.Home, "GK", -50, 0, 0, 4, heading: 0, keeper: true), P(Side.Home, "H1", 0, 0, 0, 4) }, null);
        var adapter = new VisualAdapter();
        adapter.Update(buffer, 2);
        Assert.Equal(PlayerAction.KeeperDive, adapter.Players[0].Action);
        Assert.Equal(PlayerAction.None, adapter.Players[1].Action);
    }

    [Fact]
    public void CarrierJoggingDribbles()
    {
        var buffer = new SnapshotBuffer();
        for (long t = 0; t < 5; t++)
            buffer.Push(Snap(t, new[] { P(Side.Home, "H1", t * 0.3, 0, 3, 0), P(Side.Away, "A1", 20, 0) }, new Vec3(t * 0.3 + 0.5, 0, 0)), NoEvents, "H1");
        var adapter = new VisualAdapter();
        adapter.Update(buffer, 2);
        Assert.Equal(PlayerAction.Dribble, adapter.Players[0].Action);
        Assert.Equal(PlayerAction.None, adapter.Players[1].Action);
    }

    [Fact]
    public void GoalMakesTheScorerAndTwoNearestTeammatesCheerAndTheOpponentsSulk()
    {
        PlayerSnapshot[] Team(long _) => new[] {
            P(Side.Home, "H1", 40, 0), P(Side.Home, "H2", 38, 2), P(Side.Home, "H3", 30, 0), P(Side.Home, "H4", -10, 0),
            P(Side.Away, "A1", 45, 0), P(Side.Away, "A2", 20, 0) };
        var buffer = BufferWith(30, 70, Team, null,
            Ev(EventType.Shot, 48, Side.Home, "H1"), Ev(EventType.Goal, 50, Side.Home, "", at: new Vec2(52.5, 0)));
        var adapter = new VisualAdapter();
        adapter.Update(buffer, 52);
        var a = adapter.Players;
        Assert.Equal(PlayerAction.Celebrate, a[0].Action);
        Assert.Equal(PlayerAction.Celebrate, a[1].Action);
        Assert.Equal(PlayerAction.Celebrate, a[2].Action);
        Assert.Equal(PlayerAction.None, a[3].Action);
        Assert.Equal(PlayerAction.Disappointed, a[4].Action);
        Assert.Equal(PlayerAction.Disappointed, a[5].Action);
        Assert.Equal(50, a[0].ActionStartTick);
    }

    [Fact]
    public void FoulMakesTheOffenderTackleAndTheNearestOpponentFall()
    {
        PlayerSnapshot[] Team(long _) => new[] { P(Side.Home, "H1", 10, 0), P(Side.Away, "A1", 10.5, 0), P(Side.Away, "A2", 30, 0) };
        var buffer = BufferWith(30, 70, Team, null, Ev(EventType.Foul, 50, Side.Home, "H1", at: new Vec2(10, 0)));
        var adapter = new VisualAdapter();
        adapter.Update(buffer, 48);
        Assert.Equal(PlayerAction.Tackle, adapter.Players[0].Action);
        Assert.Equal(PlayerAction.None, adapter.Players[1].Action);
        adapter.Update(buffer, 52);
        Assert.Equal(PlayerAction.Fall, adapter.Players[1].Action);
        Assert.Equal(PlayerAction.None, adapter.Players[2].Action);
    }

    // ---- Output mapping ------------------------------------------------------------------------------------

    [Fact]
    public void PlayerAndBallAreMappedToWorldAxes()
    {
        var buffer = new SnapshotBuffer();
        buffer.Push(Snap(0, new[] { P(Side.Home, "H1", 3, 4, 3, 0, heading: Math.PI / 2) }, new Vec3(1, 2, 0.5)), NoEvents);
        var adapter = new VisualAdapter();
        adapter.Update(buffer, 0);
        var p = adapter.Players[0];
        Assert.Equal(new Vec3(3, 0, 4), p.Pos3);
        Assert.Equal(SimWorld.YawFromHeading(Math.PI / 2), p.Yaw);
        Assert.Equal(3, p.Speed, 12);
        Assert.Equal(new Vec3(1, 0.5, 2), p.LookAt);
        Assert.True(adapter.Ball.Visible);
        Assert.Equal(new Vec3(1, 0.5, 2), adapter.Ball.Pos3);
    }

    // ---- Referee -------------------------------------------------------------------------------------------

    [Fact]
    public void RefereeStaysAtLeastEightMetresFromTheBallAcrossASweep()
    {
        var buffer = new SnapshotBuffer();
        var adapter = new VisualAdapter();
        for (long t = 0; t < 400; t++)
        {
            double bx = -50 + t * 0.25, by = 30 * Math.Sin(t * 0.05);
            buffer.Push(Snap(t, new[] { P(Side.Home, "H1", 0, 0) }, new Vec3(bx, by, 0), new Vec3(2.5, 0, 0)), NoEvents);
            adapter.Update(buffer, t);
            var r = adapter.Referee.Pos3;
            Assert.True(Math.Sqrt((r.X - bx) * (r.X - bx) + (r.Z - by) * (r.Z - by)) >= AdapterThresholds.RefereeMinBallDistance - 1e-6,
                $"tick {t}");
            Assert.InRange(r.X, -Pitch.HalfLength - 2.01, Pitch.HalfLength + 2.01);
            Assert.InRange(r.Z, -Pitch.HalfWidth - 2.01, Pitch.HalfWidth + 2.01);
        }
    }

    [Fact]
    public void RefereeStaysOutOfTheLineOfAPass()
    {
        var buffer = new SnapshotBuffer();
        var adapter = new VisualAdapter();
        // Ball at the centre, flying along +x: the diagonal target (0,0) lies on the lane.
        buffer.Push(Snap(0, new[] { P(Side.Home, "H1", 0, 0) }, new Vec3(0, 0, 0), new Vec3(20, 0, 0)), NoEvents);
        adapter.Update(buffer, 0);
        var r = adapter.Referee.Pos3;
        Assert.True(Math.Abs(r.Z) >= AdapterThresholds.RefereeLaneClearance || r.X < -0.001, $"referee at {r}");
        Assert.True(Math.Sqrt(r.X * r.X + r.Z * r.Z) >= AdapterThresholds.RefereeMinBallDistance - 1e-6);
        Assert.Equal(0, adapter.Referee.Pos3.Y);
    }

    [Fact]
    public void RefereeDiagonalFlipsWithTheEnds()
    {
        Assert.Equal(-RefereeSign(false), RefereeSign(true));
        Assert.True(VisualAdapter.RefereeTarget(new Vec2(100, 0), false).X <= Pitch.HalfLength - 8);

        static double RefereeSign(bool swapped) => Math.Sign(VisualAdapter.RefereeTarget(new Vec2(20, 0), swapped).Y);
    }

    // ---- Determinism ---------------------------------------------------------------------------------------

    [Fact]
    public void SameInputGivesTheSameOutput()
    {
        var events = new[] { Ev(EventType.Pass, 50), Ev(EventType.Goal, 56, Side.Home, "", at: new Vec2(52, 0)) };
        List<string> Run()
        {
            var buffer = BufferWith(30, 80, t => new[] { P(Side.Home, "H1", t * 0.3, 1, 3, 0, 0.2), P(Side.Away, "A1", 20, -t * 0.1, 0, -1, 3.1) },
                t => new Vec3(t * 0.4, 0.1 * t, t % 7 * 0.3), events);
            var adapter = new VisualAdapter();
            var lines = new List<string>();
            for (double v = 30; v <= 80; v += 0.37)
            {
                adapter.Update(buffer, v);
                foreach (var p in adapter.Players.Take(adapter.PlayerCount))
                    lines.Add($"{p.Id}|{p.Pos3}|{p.Yaw:R}|{p.Speed:R}|{p.Locomotion}|{p.Action}|{p.ActionStartTick}|{p.LookAt}");
                lines.Add($"{adapter.Ball.Pos3}|{adapter.Referee.Pos3}|{adapter.Referee.Yaw:R}");
            }
            return lines;
        }
        Assert.Equal(Run(), Run());
    }
}
