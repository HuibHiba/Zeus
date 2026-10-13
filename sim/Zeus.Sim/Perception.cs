namespace Zeus.Sim;

/// <summary>Provisional sensing settings; not calibrated to player level yet.</summary>
public sealed record PerceptionConfig(
    double FieldOfViewRadians = 2 * Math.PI / 3,
    double SightRange = 60,
    double NearBallRadius = 1.5,
    double ScanIntervalSeconds = 0.3,
    double BallMemorySeconds = 1.0)
{
    public void Validate()
    {
        if (!double.IsFinite(FieldOfViewRadians) || FieldOfViewRadians <= 0 || FieldOfViewRadians > 2 * Math.PI
            || !double.IsFinite(SightRange) || SightRange <= 0
            || !double.IsFinite(NearBallRadius) || NearBallRadius < 0 || NearBallRadius > SightRange
            || !double.IsFinite(ScanIntervalSeconds) || ScanIntervalSeconds <= 0
            || !double.IsFinite(BallMemorySeconds) || BallMemorySeconds < 0)
            throw new ArgumentException("Invalid perception settings.");
    }
}

public sealed record BallObservation(long Tick, BallSnapshot Ball);

/// <summary>A player's limited view. Remembered ball position is never extrapolated from hidden world state.</summary>
public sealed record Perception(
    long Tick,
    PlayerSnapshot Self,
    GamePhase Phase,
    bool HasBall,
    Vec2 FormationTarget,
    IReadOnlyList<PlayerSnapshot> VisiblePlayers,
    BallSnapshot? VisibleBall,
    BallObservation? KnownBall,
    double HeldBallSeconds = 0,
    bool BallIsLoose = false,
    bool IsPresser = false,
    bool? AttackPositiveX = null)
{
    public bool AttacksPositiveX => AttackPositiveX ?? (Self.Team == Side.Home);
}

public static class PerceptionBuilder
{
    public static bool InSight(PlayerSnapshot observer, Vec2 position, PerceptionConfig config)
    {
        var offset = position - observer.Pos;
        double distance = offset.Length;
        if (distance > config.SightRange) return false;
        if (distance < 1e-9) return true;
        double angle = Mover.NormalizeAngle(Math.Atan2(offset.Y, offset.X) - observer.Heading);
        return Math.Abs(angle) <= config.FieldOfViewRadians / 2 + 1e-12;
    }

    public static Perception Build(Snapshot world, PlayerSnapshot self, GamePhase phase, bool hasBall,
        PerceptionConfig config, double stepSeconds, BallObservation? previousBall = null)
    {
        var visible = world.Players.Where(p => !p.SentOff && !(p.Team == self.Team && p.Id == self.Id)
            && InSight(self, p.Pos, config)).ToArray();
        BallSnapshot? ball = world.Ball;
        // A carrier feels the ball; a ball very close to the feet can be sensed outside the view cone.
        bool nearBall = ball is not null && (ball.Pos.XY - self.Pos).Length <= config.NearBallRadius
            && ball.Pos.Z <= config.NearBallRadius;
        bool seesBall = ball is not null && (hasBall || nearBall ||
            ((ball.Pos - new Vec3(self.Pos.X, self.Pos.Y, 0)).Length <= config.SightRange
            && InSight(self, ball.Pos.XY, config)));
        var known = seesBall ? new BallObservation(world.Tick, ball!) : previousBall;
        if (known is not null && (world.Tick - known.Tick) * stepSeconds > config.BallMemorySeconds + 1e-9)
            known = null;
        return new Perception(world.Tick, self, phase, hasBall, self.Pos, Array.AsReadOnly(visible),
            seesBall ? ball : null, known);
    }
}
