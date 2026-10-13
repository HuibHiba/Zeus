namespace Zeus.Sim;

/// <summary>Uncalibrated v0 passing choices and execution parameters.</summary>
public sealed record PassConfig(double MinDistance = 3, double MaxDistance = 35,
    double HoldSeconds = 3.0, double LeadSeconds = 0.35, double Skill = 0.75,
    double AngleErrorRadians = 0.12, double SpeedErrorFraction = 0.12,
    // After HoldSeconds + ForceSeconds the carrier plays the best visible option even when it scores <= 0
    // (a pressed carrier otherwise waits for a duel to free the ball: 60% of the baseline match was such a stall).
    double ForceSeconds = 2.0,
    // Opponents closer than this to the passer are not 'in the lane': the ball leaves at 8-16 m/s, so only
    // opponents farther along the segment can intercept (pressure on the passer is modelled in PassExecution).
    double LaneStartMeters = 2.0,
    // The passer avoids a receiver who looks offside: ahead of the ball and level with or beyond the most advanced
    // visible opponent (by more than -OffsideMargin m). Estimate from his own field of view only; real offside is judged by Law 11.
    double OffsideMargin = 0.5,
    // Defenders cut the lane of a fast ball in flight: the teammate who can reach the ball's path first moves to the point
    // on it (reaction time + distance / run speed against the ball's arrival). 0 disables lane cutting.
    double LaneCutRunSpeed = 6.5,
    double LaneCutReactionSeconds = 0.4,
    double LaneCutMinBallSpeed = 4.0,
    // Choice score = 1 + ForwardWeight*forward - DistanceWeight*distance - RiskWeight*risk (risk: opponents near the lane/receiver).
    double ForwardWeight = 0.06,
    double DistanceWeight = 0.015,
    double RiskWeight = 0.9,
    // While holding the ball inside this distance of the goal the carrier dribbles towards goal (until he shoots or passes)
    // instead of waiting at his formation spot. 0 disables. Needed so shots come from shooting positions, not from the formation line.
    double DribbleMaxGoalDistance = 35,
    double DribbleStopGoalDistance = 11,
    // Carry the ball forward (towards goal) when no visible opponent stands within CarryClearMeters in a cone of
    // +-CarryConeRadians ahead, up to CarryMaxGoalDistance from goal. 0 disables. Without it the carrier waits at his formation spot.
    double CarryMaxGoalDistance = 45,
    double CarryClearMeters = 7,
    double CarryConeRadians = 0.6,
    // Clearance: a carrier in his own third (closer than ClearZoneMeters to his goal line) with an opponent within
    // ClearPressureMeters and no pass scoring above ClearBelowScore kicks the ball long and wide instead. 0 disables.
    double ClearPressureMeters = 5,
    double ClearZoneMeters = 30,
    double ClearBelowScore = 0.9)
{
    public void Validate()
    {
        if (!double.IsFinite(MinDistance) || MinDistance <= 0 || !double.IsFinite(MaxDistance) || MaxDistance < MinDistance
            || !double.IsFinite(HoldSeconds) || HoldSeconds < 0 || !double.IsFinite(LeadSeconds) || LeadSeconds < 0
            || !double.IsFinite(Skill) || Skill < 0 || Skill > 1
            || !double.IsFinite(AngleErrorRadians) || AngleErrorRadians < 0
            || !double.IsFinite(SpeedErrorFraction) || SpeedErrorFraction < 0 || SpeedErrorFraction >= 1
            || !double.IsFinite(ForceSeconds) || ForceSeconds < 0 || !double.IsFinite(LaneStartMeters) || LaneStartMeters < 0
            || !double.IsFinite(OffsideMargin)
            || !double.IsFinite(LaneCutRunSpeed) || LaneCutRunSpeed < 0 || !double.IsFinite(LaneCutReactionSeconds) || LaneCutReactionSeconds < 0
            || !double.IsFinite(LaneCutMinBallSpeed) || LaneCutMinBallSpeed <= 0
            || !double.IsFinite(ForwardWeight) || !double.IsFinite(DistanceWeight) || !double.IsFinite(RiskWeight) || RiskWeight < 0
            || !double.IsFinite(DribbleMaxGoalDistance) || DribbleMaxGoalDistance < 0 || !double.IsFinite(DribbleStopGoalDistance) || DribbleStopGoalDistance < 0
            || !double.IsFinite(CarryMaxGoalDistance) || CarryMaxGoalDistance < 0 || !double.IsFinite(CarryClearMeters) || CarryClearMeters < 0
            || !double.IsFinite(CarryConeRadians) || CarryConeRadians < 0 || CarryConeRadians > Math.PI
            || !double.IsFinite(ClearPressureMeters) || ClearPressureMeters < 0 || !double.IsFinite(ClearZoneMeters) || ClearZoneMeters < 0
            || !double.IsFinite(ClearBelowScore))
            throw new ArgumentException("Invalid pass settings.");
    }
}

public sealed record PassOption(string ReceiverId, Vec2 Target, double Score);

public static class PassChoice
{
    public static PassOption? Choose(Perception view, PassConfig config, bool force = false)
    {
        if (!view.HasBall) return null;
        PassOption? best = null;
        double dir = view.AttacksPositiveX ? 1 : -1;
        bool anyOpponent = false;
        double line = double.MinValue; // most advanced visible outfield opponent along the attack direction (the keeper stays behind him)
        foreach (var o in view.VisiblePlayers)
            if (o.Team != view.Self.Team && !o.IsKeeper) { anyOpponent = true; line = Math.Max(line, o.Pos.X * dir); }
        foreach (var mate in view.VisiblePlayers)
        {
            if (mate.Team != view.Self.Team) continue;
            var target = mate.Pos + mate.Vel * config.LeadSeconds;
            target = new Vec2(Math.Clamp(target.X, -Pitch.HalfLength, Pitch.HalfLength),
                Math.Clamp(target.Y, -Pitch.HalfWidth, Pitch.HalfWidth));
            double distance = (target - view.Self.Pos).Length;
            if (distance < config.MinDistance || distance > config.MaxDistance) continue;
            double targetForward = target.X * dir;
            if (anyOpponent && targetForward > view.Self.Pos.X * dir && targetForward > 0
                && targetForward > line - config.OffsideMargin) continue;
            double risk = 0;
            foreach (var opponent in view.VisiblePlayers.Where(p => p.Team != view.Self.Team))
            {
                var laneStart = view.Self.Pos;
                var laneDir = target - laneStart;
                if (laneDir.Length > config.LaneStartMeters + 1e-9) laneStart += laneDir * (config.LaneStartMeters / laneDir.Length);
                double lane = DistanceToSegment(opponent.Pos, laneStart, target);
                risk += Math.Max(0, 1 - lane / 3);
                risk += Math.Max(0, 1 - (opponent.Pos - target).Length / 4);
            }
            double forward = (target.X - view.Self.Pos.X) * (view.AttacksPositiveX ? 1 : -1);
            double score = 1 + config.ForwardWeight * forward - config.DistanceWeight * distance - config.RiskWeight * risk;
            // Stable identity tie-break: list order is not tactical information.
            if ((force || score > 0) && (best is null || score > best.Score + 1e-9
                || (Math.Abs(score - best.Score) <= 1e-9 && string.CompareOrdinal(mate.Id, best.ReceiverId) < 0)))
                best = new PassOption(mate.Id, target, score);
        }
        return best;
    }

    internal static double DistanceToSegment(Vec2 point, Vec2 start, Vec2 end)
    {
        var line = end - start;
        double lengthSquared = line.X * line.X + line.Y * line.Y;
        double t = lengthSquared < 1e-9 ? 0 : Math.Clamp(((point - start).X * line.X + (point - start).Y * line.Y) / lengthSquared, 0, 1);
        return (point - (start + line * t)).Length;
    }
}

public static class PassExecution
{
    /// <summary>Execution error changes velocity only. Physics/contact determines success, never a second success roll.</summary>
    public static Vec3 Velocity(Vec2 from, Vec2 target, double visiblePressure, PassConfig config, Rng rng)
    {
        var difference = target - from;
        double distance = difference.Length;
        double difficulty = (1 - config.Skill) * (1 + distance / 35 + Math.Clamp(visiblePressure, 0, 3));
        double angleNoise = (rng.NextDouble() + rng.NextDouble() - 1) * config.AngleErrorRadians * difficulty;
        double speedNoise = (rng.NextDouble() + rng.NextDouble() - 1) * config.SpeedErrorFraction * difficulty;
        double angle = Math.Atan2(difference.Y, difference.X) + angleNoise;
        double speed = Math.Clamp(7 + 0.25 * distance, 8, 16) * Math.Max(0.2, 1 + speedNoise);
        return new Vec3(Math.Cos(angle) * speed, Math.Sin(angle) * speed, 0);
    }
}

public sealed class PassingBrain : IPlayerBrain
{
    // Point on the ball's path this player can reach before the ball, if he is the best placed visible teammate; else null.
    private Vec2? LaneCut(Perception view, BallSnapshot ball)
    {
        var velocity = ball.Vel.XY;
        double speed = velocity.Length;
        var direction = velocity * (1 / speed);
        double reachable = Math.Min(speed * 2.5, 45); // the path ahead that matters (friction is ignored)
        (double Margin, Vec2 Point)? Evaluate(Vec2 position)
        {
            double along = Math.Clamp((position - ball.Pos.XY).X * direction.X + (position - ball.Pos.XY).Y * direction.Y, 0, reachable);
            var point = ball.Pos.XY + direction * along;
            double mine = _config.LaneCutReactionSeconds + (point - position).Length / _config.LaneCutRunSpeed;
            double margin = along / speed - mine; // > 0: he is there before the ball
            return margin >= 0 ? (margin, point) : null;
        }
        var self = Evaluate(view.Self.Pos);
        if (self is null) return null;
        foreach (var mate in view.VisiblePlayers)
        {
            if (mate.Team != view.Self.Team || mate.IsKeeper) continue;
            var other = Evaluate(mate.Pos);
            if (other is not null && (other.Value.Margin > self.Value.Margin + 1e-9
                || (Math.Abs(other.Value.Margin - self.Value.Margin) <= 1e-9 && string.CompareOrdinal(mate.Id, view.Self.Id) < 0)))
                return null;
        }
        return self.Value.Point;
    }

    // True when no visible opponent is within CarryClearMeters in the cone towards goal.
    private bool SpaceAhead(Perception view, Vec2 toGoal)
    {
        double heading = Math.Atan2(toGoal.Y, toGoal.X);
        foreach (var p in view.VisiblePlayers)
        {
            if (p.Team == view.Self.Team) continue;
            var d = p.Pos - view.Self.Pos;
            if (d.Length > _config.CarryClearMeters) continue;
            if (Math.Abs(Mover.NormalizeAngle(Math.Atan2(d.Y, d.X) - heading)) <= _config.CarryConeRadians) return false;
        }
        return true;
    }

    private readonly PassConfig _config;
    private readonly ShotConfig _shots;
    private readonly FormationBrain _formation = new();
    public PassingBrain(PassConfig config, ShotConfig? shots = null) { _config = config; _shots = shots ?? new ShotConfig(); }

    public Intent Decide(Perception view)
    {
        if (!view.HasBall)
        {
            if (view.Self.IsKeeper && view.KnownBall is { } known)
            {
                double ownGoalX = view.AttacksPositiveX ? -Pitch.HalfLength : Pitch.HalfLength;
                double targetY = known.Ball.Pos.Y * 0.2;
                if (view.BallIsLoose && Math.Abs(known.Ball.Vel.X) > 1e-9)
                {
                    double time = (ownGoalX - known.Ball.Pos.X) / known.Ball.Vel.X;
                    if (time > 0 && time < 3) targetY = known.Ball.Pos.Y + known.Ball.Vel.Y * time;
                }
                return new Intent(IntentKind.KeeperGuard,
                    new Vec2(ownGoalX + (view.AttacksPositiveX ? 0.8 : -0.8), Math.Clamp(targetY, -3.3, 3.3)),
                    FacingTarget: known.Ball.Pos.XY);
            }
            if (view.IsPresser && view.VisibleBall is { } carriedBall)
                return new Intent(IntentKind.Press, carriedBall.Pos.XY);
            // The intended receiver goes to meet the pass; the others know it is coming and keep their positions.
            if (view.ReceiveTarget is { } meet)
                return new Intent(IntentKind.MoveToPosition, view.VisibleBall is { } incoming && (incoming.Pos.XY - view.Self.Pos).Length < 3
                    ? incoming.Pos.XY : meet);
            if (_config.LaneCutRunSpeed > 0 && view.BallIsLoose && !view.TeamPassInFlight && view.VisibleBall is { } flying
                && flying.Pos.Z < 1.5 && flying.Vel.XY.Length >= _config.LaneCutMinBallSpeed
                && LaneCut(view, flying) is { } cut)
                return new Intent(IntentKind.MoveToPosition, cut);
            if (view.BallIsLoose && !view.TeamPassInFlight && view.VisibleBall is { } ball && ball.Pos.Z < 0.5)
            {
                double ownDistance = (ball.Pos.XY - view.Self.Pos).Length;
                bool nearerMate = view.VisiblePlayers.Any(p => p.Team == view.Self.Team
                    && ((p.Pos - ball.Pos.XY).Length < ownDistance - 1e-9
                        || (Math.Abs((p.Pos - ball.Pos.XY).Length - ownDistance) <= 1e-9
                            && string.CompareOrdinal(p.Id, view.Self.Id) < 0)));
                if (!nearerMate)
                {
                    var target = ball.Pos.XY + ball.Vel.XY * 0.25;
                    return new Intent(IntentKind.MoveToPosition, target);
                }
            }
            // Once in position, keep scanning instead of freezing forever with a loose ball behind the body.
            if (view.VisibleBall is null && (view.FormationTarget - view.Self.Pos).Length <= 0.02)
                return new Intent(IntentKind.Scan, view.Self.Pos);
            return _formation.Decide(view);
        }
        double goalX = view.AttacksPositiveX ? Pitch.HalfLength : -Pitch.HalfLength;
        if (_config.ClearPressureMeters > 0
            && view.Self.Pos.X * (view.AttacksPositiveX ? 1 : -1) < -Pitch.HalfLength + _config.ClearZoneMeters
            && view.VisiblePlayers.Any(p => p.Team != view.Self.Team && (p.Pos - view.Self.Pos).Length <= _config.ClearPressureMeters)
            && !(PassChoice.Choose(view, _config) is { } safe && safe.Score >= _config.ClearBelowScore))
        {
            double side = view.Self.Pos.Y >= 0 ? 1 : -1;
            return new Intent(IntentKind.Clear, new Vec2(goalX - (view.AttacksPositiveX ? 25 : -25), side * 28));
        }
        if (view.HeldBallSeconds < _config.HoldSeconds)
        {
            var toGoal = new Vec2(goalX, 0) - view.Self.Pos;
            if (_config.DribbleMaxGoalDistance > 0 && toGoal.Length <= _config.DribbleMaxGoalDistance && toGoal.Length > _config.DribbleStopGoalDistance)
                return new Intent(IntentKind.MoveToPosition, new Vec2(goalX, 0) - toGoal * (_config.DribbleStopGoalDistance / toGoal.Length));
            if (_config.CarryMaxGoalDistance > 0 && toGoal.Length <= _config.CarryMaxGoalDistance && toGoal.Length > _config.DribbleStopGoalDistance
                && SpaceAhead(view, toGoal))
                return new Intent(IntentKind.MoveToPosition, view.Self.Pos + toGoal * (Math.Min(10, toGoal.Length - _config.DribbleStopGoalDistance) / toGoal.Length));
            return _formation.Decide(view);
        }
        double goalDistance = (new Vec2(goalX, 0) - view.Self.Pos).Length;
        if (ShotModel.GoalProbability(goalDistance, view.Self.Pos.Y) >= _shots.MinGoalProbability)
        {
            var keeper = view.VisiblePlayers.FirstOrDefault(p => p.Team != view.Self.Team && p.IsKeeper);
            double aimY = keeper is null ? (view.Self.Pos.Y > 0 ? 2.4 : -2.4) : (keeper.Pos.Y >= 0 ? -2.4 : 2.4);
            return new Intent(IntentKind.Shoot, new Vec2(goalX + (view.AttacksPositiveX ? 1 : -1), aimY));
        }
        var option = PassChoice.Choose(view, _config);
        option ??= view.HeldBallSeconds >= _config.HoldSeconds + _config.ForceSeconds
            ? PassChoice.Choose(view, _config, force: true) : null;
        return option is not null ? new Intent(IntentKind.Pass, option.Target, option.ReceiverId)
            : new Intent(IntentKind.Scan, view.Self.Pos);
    }
}
