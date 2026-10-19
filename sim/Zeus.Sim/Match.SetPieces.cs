namespace Zeus.Sim;

/// <summary>
/// Header finishing after a lofted set-piece routine (B6, docs/spelhervattingen.md "Kopbalafwerking"). Starting values are
/// estimates, not calibrated: measure with Zeus.Runner --set-piece-stats (docs/kalibratie.md B6).
/// </summary>
public sealed record HeaderConfig(
    // false = the 2026-10-19 routines without header model (ball only controlled once it is low).
    bool Enabled = true,
    // A field player can head the ball when it is within this horizontal distance (m) of him (jump and step).
    double Radius = 2.0,
    // Header window: ball height (m) while it comes down; below MinHeight it falls to normal control.
    double MinHeight = 1.3,
    double MaxHeight = 2.6,
    // Height with the best header; at the window edges the chance is multiplied by 1 - HeightPenalty.
    double BestHeight = 1.95,
    double HeightPenalty = 0.5,
    // At the full Radius the chance is multiplied by 1 - ReachPenalty (linear in the distance to the ball).
    double ReachPenalty = 0.5,
    // Aerial skill = (Heading + 0.5 Strength) / 1.5 of the effective attributes; players without attributes have NeutralSkill
    // (the half-professional mean). Skill factor = max(0, 1 + SkillWeight * (skill - NeutralSkill)).
    double NeutralSkill = 0.65,
    double SkillWeight = 2.5,
    // Attacker: chance of a header on goal at full skill/reach/height, at most GoalDistanceFull m from the goal centre.
    double AttemptBase = 0.7,
    // Beyond GoalDistanceFull the attempt chance falls as exp(-(distance - full) / scale).
    double GoalDistanceFull = 6,
    double GoalDistanceScale = 8,
    // Every defender (field player) within PressureRadius of the attacker multiplies the attempt chance by PressureFactor.
    double PressureRadius = 1.5,
    double PressureFactor = 0.7,
    // Defender: chance to head the ball clear (corner); free kicks use FreeKickClearBase (deep crosses are mostly cleared).
    double ClearBase = 0.35,
    double FreeKickClearBase = 1.4,
    // Attempt chance multiplier for free-kick crosses (headers from 11 m and more rarely go on goal).
    double FreeKickAttemptFactor = 0.6,
    double MaxProbability = 0.95,
    // Header shot: ShotExecution with AngleErrorBase * SpreadFactor, mean speed SpeedMean (m/s) and vertical speed DownSpeed;
    // execution skill = finishing + ExecutionSkillWeight * (aerial skill - NeutralSkill).
    double SpreadFactor = 1.2,
    double SpeedMean = 14,
    double DownSpeed = -1.0,
    double ExecutionSkillWeight = 1.0,
    // Header clearance: away from the goal centre at ClearSpeed (m/s) with vertical speed ClearRise.
    double ClearSpeed = 16,
    double ClearRise = 5)
{
    public static readonly HeaderConfig Default = new();

    public void Validate()
    {
        var values = new[] { Radius, MinHeight, MaxHeight, BestHeight, HeightPenalty, ReachPenalty, NeutralSkill, SkillWeight,
            AttemptBase, GoalDistanceFull, GoalDistanceScale, PressureRadius, PressureFactor, ClearBase, FreeKickClearBase,
            FreeKickAttemptFactor, MaxProbability, SpreadFactor, SpeedMean, ExecutionSkillWeight, ClearSpeed, ClearRise };
        if (values.Any(v => !double.IsFinite(v) || v < 0) || !double.IsFinite(DownSpeed) || Radius <= 0 || MaxHeight <= MinHeight
            || HeightPenalty > 1 || ReachPenalty > 1 || PressureFactor > 1 || MaxProbability > 1 || GoalDistanceScale <= 0 || SpeedMean <= 0)
            throw new ArgumentException("Invalid header settings.");
    }
}

/// <summary>Counters of the header path per match, for measurement only.</summary>
public sealed class HeaderDiagnostics
{
    public int Armed, WindowEntered, KeeperAtEntry, WithCandidate, AttemptsRolled, AttemptsSucceeded, EntrySlotDistanceCount;
    public double EntrySlotDistanceSum;   // distance (m) from the ball at window entry to the nearest attacking slot
}

/// <summary>Pure header chances (no RNG); the match draws against them. See <see cref="HeaderConfig"/>.</summary>
public static class HeaderModel
{
    private static double Common(HeaderConfig h, double skill, double ballDistance, double height) =>
        Math.Max(0, 1 + h.SkillWeight * (skill - h.NeutralSkill))
        * (1 - h.ReachPenalty * Math.Clamp(ballDistance / h.Radius, 0, 1))
        * (1 - h.HeightPenalty * Math.Clamp(Math.Abs(height - h.BestHeight) / ((h.MaxHeight - h.MinHeight) / 2), 0, 1));

    /// <summary>Chance that an attacker heads the ball on goal.</summary>
    public static double AttemptProbability(HeaderConfig h, double skill, double ballDistance, double goalDistance,
        int pressure, double height, bool freeKick) =>
        Math.Clamp(h.AttemptBase * Common(h, skill, ballDistance, height)
            * Math.Exp(-Math.Max(0, goalDistance - h.GoalDistanceFull) / h.GoalDistanceScale)
            * Math.Pow(h.PressureFactor, pressure) * (freeKick ? h.FreeKickAttemptFactor : 1), 0, h.MaxProbability);

    /// <summary>Chance that a defender heads the ball clear.</summary>
    public static double ClearProbability(HeaderConfig h, double skill, double ballDistance, double height, bool freeKick) =>
        Math.Clamp((freeKick ? h.FreeKickClearBase : h.ClearBase) * Common(h, skill, ballDistance, height), 0, h.MaxProbability);
}

// Set-piece routines (B6): placement of attackers and defenders at corners and free kicks, and the taker's aim.
// Only active when the team's tactic has a "standaardsituaties" block; without it nothing here changes the match.
// Shortcut: players are placed at the restart (like the existing 9.15 m push-away), not walked there; upgrade when set pieces get animation.
public sealed partial class Match
{
    private int _shortPartner = -1;
    private Vec2? _setPieceAim;      // first header slot of the routine, after the free-kick offside clamp

    // Header quality: attributes (Heading + half Strength) when present, else role priority. Deterministic, no RNG.
    private double Aerial(Player p)
    {
        if (HasAttributes(p)) return Attributes(p).Heading + .5 * Attributes(p).Strength;
        string role = p.Tactic.Roster.FirstOrDefault(r => r.Id == p.Id)?.Role ?? "";
        return role switch { "targetman" => 3, "spits" => 2, "centrale_verdediger" => 2, "hangende_spits" => 1, _ => 0 };
    }

    // Slots in the box for the header targets, best header first. goalX = attacked goal line, dir = its sign, cs = corner side.
    private static Vec2[] AttackSlots(string kind, double goalX, double dir, double cs)
    {
        Vec2 P(double depth, double y) => new(goalX - dir * depth, y);
        return kind switch
        {
            "korte_paal" => new[] { P(4, cs * 3), P(5, cs * 1.5), P(5, cs * 4.5), P(6, 0) },
            "verre_paal" => new[] { P(4, -cs * 3), P(5, -cs * 1.5), P(5, -cs * 4.5), P(6, 0) },
            "geen" => Array.Empty<Vec2>(),
            _ => new[] { P(11, 0), P(10.5, 2.5), P(10.5, -2.5), P(12, 0.8) } // penaltystip, and the others of a short corner
        };
    }

    private (string Kind, Vec2[] Slots)? AttackPlan(Player taker, Vec2 spot, string reason)
    {
        var sp = taker.Tactic.SetPieces;
        if (sp is null) return null;
        double goalX = -OwnGoalSign(taker.Team) * Pitch.HalfLength, dir = Math.Sign(goalX), cs = spot.Y < 0 ? -1 : 1;
        if (reason == nameof(PitchExitKind.Corner))
            return sp.CornerFor is { } corner ? (corner, AttackSlots(corner, goalX, dir, cs)) : null;
        if (reason != "free_kick" || sp.FreeKickFor is null || spot.X * dir <= 0) return null;
        if (sp.FreeKickFor == "schot" && (spot - new Vec2(goalX, 0)).Length < 25) return ("schot", AttackSlots("geen", goalX, dir, cs));
        if (sp.FreeKickFor == "kort") return ("kort", AttackSlots("geen", goalX, dir, cs));
        return ("voorzet", AttackSlots("penaltystip", goalX, dir, cs));
    }

    private void PlaceAt(int index, Vec2 pos) => _players[index].Body = _players[index].Body with { Pos = ClampToPitch(pos), Vel = Vec2.Zero };

    private void PlaceSetPiece(Side team, Vec2 spot, string reason, int taker)
    {
        _shortPartner = -1;
        _setPieceAim = null;
        _headerTeam = null;
        bool corner = reason == nameof(PitchExitKind.Corner);
        if (!corner && reason != "free_kick") return;
        double goalX = -OwnGoalSign(team) * Pitch.HalfLength, dir = Math.Sign(goalX), cs = spot.Y < 0 ? -1 : 1;
        var mates = Enumerable.Range(0, _players.Length)
            .Where(i => i != taker && _players[i].Team == team && !_players[i].SentOff && !_players[i].IsGoalkeeper).ToList();
        if (AttackPlan(_players[taker], spot, reason) is { } plan)
        {
            if (plan.Kind == "kort" && mates.Count > 0)
            {
                _shortPartner = mates.OrderBy(i => (_players[i].Body.Pos - spot).Length).ThenBy(i => i).First();
                mates.Remove(_shortPartner);
                PlaceAt(_shortPartner, new Vec2(spot.X - dir * 6, spot.Y - cs * 4));
            }
            var ordered = mates.OrderByDescending(i => Aerial(_players[i])).ThenBy(i => i).ToList();
            var slots = plan.Slots;
            if (!corner)
            {
                // Open play for offside: keep every header slot level with the second-last opponent (the keeper counts), not behind him.
                var depths = _players.Where(p => p.Team != team && !p.SentOff).Select(p => (goalX - p.Body.Pos.X) * dir).OrderBy(x => x).ToList();
                double line = depths.Count > 1 ? depths[1] : 0;
                slots = slots.Select(s => new Vec2(goalX - dir * Math.Max((goalX - s.X) * dir, line + .3), s.Y)).ToArray();
            }
            _setPieceAim = slots.Length > 0 ? slots[0] : null;
            _setPieceSlots = slots;
            for (int k = 0; k < Math.Min(ordered.Count, slots.Length); k++) PlaceAt(ordered[k], slots[k]);
        }
        var defence = _players.FirstOrDefault(p => p.Team != team)?.Tactic.SetPieces;
        string? mode = corner ? defence?.CornerAgainst : defence?.FreeKickAgainst;
        // A free kick far from the defended goal keeps the open-play shape.
        if (mode is null || (!corner && (spot - new Vec2(goalX, 0)).Length > 35)) return;
        Vec2 D(double depth, double y) => new(goalX - dir * depth, y);
        // Free-kick slots closer than 9.15 m to the ball are skipped: the defender stays where the restart put him.
        void Put(int i, Vec2 pos) { if (corner || (pos - spot).Length >= 9.15) PlaceAt(i, pos); }
        var defenders = Enumerable.Range(0, _players.Length)
            .Where(i => _players[i].Team != team && !_players[i].SentOff && !_players[i].IsGoalkeeper)
            .OrderByDescending(i => Aerial(_players[i])).ThenBy(i => i).ToList();
        for (int k = 0; k < defence!.PostDefenders && defenders.Count > 0; k++)
        {
            Put(defenders[0], D(0.6, k == 0 ? -3.4 : 3.4));
            defenders.RemoveAt(0);
        }
        if (mode == "zone")
        {
            // Four on the six-yard line, then two around the penalty spot.
            var zone = new[] { D(4.5, -6), D(4.5, -2), D(4.5, 2), D(4.5, 6), D(11, -2.5), D(11, 2.5) };
            for (int k = 0; k < Math.Min(zone.Length, defenders.Count); k++) Put(defenders[k], zone[k]);
            return;
        }
        // Man: every attacker in the penalty area gets the nearest free defender, one metre goal-side of him.
        var marked = mates.Concat(_shortPartner >= 0 ? new[] { _shortPartner } : Array.Empty<int>())
            .Where(i => InPenaltyArea(team == Side.Home ? Side.Away : Side.Home, _players[i].Body.Pos))
            .OrderBy(i => Math.Abs(_players[i].Body.Pos.X - goalX)).ThenBy(i => i).ToList();
        foreach (int a in marked)
        {
            if (defenders.Count == 0) break;
            var apos = _players[a].Body.Pos;
            int d = defenders.OrderBy(i => (_players[i].Body.Pos - apos).Length).ThenBy(i => i).First();
            defenders.Remove(d);
            Put(d, new Vec2(apos.X + dir * 1.0, apos.Y));
        }
    }

    // The taker's aim and ball type for a routine; null keeps the historical restart pass.
    private (Vec2 Aim, bool Loft, bool Shot)? SetPieceKick(Player taker)
    {
        if (AttackPlan(taker, _ball.Pos.XY, _restartReason) is not { } plan) return null;
        if (plan.Kind == "kort")
            return _shortPartner >= 0 && !_players[_shortPartner].SentOff ? (_players[_shortPartner].Body.Pos, false, false) : null;
        if (plan.Kind == "schot") return (new Vec2(-OwnGoalSign(taker.Team) * Pitch.HalfLength, 0), false, true);
        return _setPieceAim is { } header ? (header, true, false) : null;
    }

    private Side? _headerTeam;   // team of a lofted routine kick whose header window is still open (null = no header model)
    private bool _headerFreeKick;
    private bool _headerEntered;
    private Vec2[] _setPieceSlots = Array.Empty<Vec2>();

    /// <summary>Header-model counters for measurement (Zeus.Runner --set-piece-stats); not part of events or snapshots.</summary>
    public HeaderDiagnostics HeaderStats { get; } = new();

    // Called at every restart kick; arms the header window only for a lofted routine (corner or free-kick cross).
    private void ArmHeader((Vec2 Aim, bool Loft, bool Shot)? routine, Side team)
    {
        _headerTeam = routine is { Loft: true } && _config.HeaderSettings.Enabled ? team : null;
        _headerFreeKick = _restartReason == "free_kick";
        _headerEntered = false;
        if (_headerTeam is not { } attacking) return;
        HeaderStats.Armed++;
        // Without this the placed players left their slots at the kick (formation run, up to ~10 m in a 2 s flight), so the
        // ball came down where nobody stood. Players in the box hold their spot until the cross arrives.
        var defending = attacking == Side.Home ? Side.Away : Side.Home;
        long hold = _tick + (long)Math.Ceiling((_headerFlightSeconds + .3) / _config.StepSeconds);
        foreach (var p in _players)
        {
            if (p.SentOff || p.IsGoalkeeper || p.Path is not null || !InPenaltyArea(defending, p.Body.Pos)) continue;
            p.Target = p.Body.Pos;
            p.Intent = new Intent(IntentKind.HoldPosition, p.Body.Pos);
            p.NextDecisionTick = Math.Max(p.NextDecisionTick, hold);
        }
    }

    private double _headerFlightSeconds;

    // The plain lob (vz = g d / (2 v), drag ignored) lands 6-8 m short on a 31 m corner and passes head height well before the
    // slot. With the header model the horizontal speed is solved (bisection over the real ball physics) so that a kick without
    // error comes down through BestHeight at the aim; the lift and the pass's direction and speed error are kept. No RNG.
    private (Vec3 Velocity, double Lift) HeaderLob(Vec2 from, Vec2 aim, Vec3 velocity)
    {
        double distance = (aim - from).Length;
        double nominal = Math.Clamp(7 + 0.25 * distance, 8, 16); // PassExecution's speed without error
        double lift = Math.Clamp(.5 * _config.BallPhysics.Gravity * distance / nominal, 7, 12);
        double lo = 4, hi = 40;
        for (int k = 0; k < 30; k++)
        {
            double mid = (lo + hi) / 2;
            if (LobReach(mid, lift) < distance) lo = mid; else hi = mid;
        }
        double scale = (lo + hi) / 2 / nominal;
        _headerFlightSeconds = 2 * lift / _config.BallPhysics.Gravity;
        return (new Vec3(velocity.X * scale, velocity.Y * scale, 0), lift);
    }

    // Horizontal distance at which a kick from the ground first comes down through BestHeight.
    private double LobReach(double speed, double lift)
    {
        var physics = _config.BallPhysics with { SubSteps = 1 };
        double dt = _config.StepSeconds / Math.Max(1, _config.BallPhysics.SubSteps), head = _config.HeaderSettings.BestHeight;
        var ball = new BallState(Vec3.Zero, new Vec3(speed, 0, lift));
        for (int k = 0; k < 2000; k++)
        {
            ball = BallPhysics.Step(ball, physics, dt);
            if (ball.Vel.Z < 0 && ball.Pos.Z <= head) return ball.Pos.X;
        }
        return ball.Pos.X;
    }

    private double HeaderSkill(Player p) => HasAttributes(p)
        ? (Attributes(p).Heading + .5 * Attributes(p).Strength) / 1.5 : _config.HeaderSettings.NeutralSkill;

    // One header contest per armed kick, while the ball comes down through the window in the attacked penalty area.
    // The player nearest to the ball gets the first chance, the nearest opponent (if any within reach) the second.
    // True when the ball was headed (shot or clearance): the caller stops this tick's ball update.
    private bool TryHeader(IReadOnlyList<PlayerSnapshot> at)
    {
        var h = _config.HeaderSettings;
        var ball = _ball.Pos;
        if (_ball.Vel.Z >= 0 || ball.Z > h.MaxHeight) return false;
        if (ball.Z < h.MinHeight) { _headerTeam = null; return false; } // nobody reached it: normal control as before
        Side team = _headerTeam!.Value, defending = team == Side.Home ? Side.Away : Side.Home;
        if (Math.Abs(ball.X) > Pitch.HalfLength || !InPenaltyArea(defending, ball.XY)) return false;
        int keeper = Array.FindIndex(_players, p => p.Team == defending && p.IsGoalkeeper && !p.SentOff);
        bool keeperBall = keeper >= 0 && (at[keeper].Pos - ball.XY).Length <= _keeperControl.ControlRadius;
        if (!_headerEntered)
        {
            _headerEntered = true;
            HeaderStats.WindowEntered++;
            if (keeperBall) HeaderStats.KeeperAtEntry++;
            if (_setPieceSlots.Length > 0) { HeaderStats.EntrySlotDistanceSum += _setPieceSlots.Min(s => (s - ball.XY).Length); HeaderStats.EntrySlotDistanceCount++; }
        }
        if (keeperBall) return false; // the keeper's ball
        int Nearest(Side side)
        {
            int best = -1;
            double bestDistance = h.Radius + 1e-9;
            for (int i = 0; i < _players.Length; i++)
            {
                var p = _players[i];
                if (p.Team != side || p.SentOff || p.IsGoalkeeper || i == _restartKickPlayer) continue;
                if (side == team && side == _offsideTeam && _offsideCandidates.Contains(p.Id)) continue; // left to the offside call
                double d = (at[i].Pos - ball.XY).Length;
                if (d < bestDistance) { best = i; bestDistance = d; }
            }
            return best;
        }
        int attacker = Nearest(team), defender = Nearest(defending);
        if (attacker < 0 && defender < 0) return false;
        _headerTeam = null;
        HeaderStats.WithCandidate++;
        var goal = new Vec2(OwnGoalSign(defending) * Pitch.HalfLength, 0);
        double Distance(int i) => i < 0 ? double.MaxValue : (at[i].Pos - ball.XY).Length;
        foreach (int i in Distance(defender) < Distance(attacker) ? new[] { defender, attacker } : new[] { attacker, defender })
        {
            if (i < 0) continue;
            var p = _players[i];
            double chance = i == attacker
                ? HeaderModel.AttemptProbability(h, HeaderSkill(p), Distance(i), (goal - ball.XY).Length,
                    at.Count(s => s.Team == defending && !s.SentOff && !s.IsKeeper && (s.Pos - at[i].Pos).Length < h.PressureRadius),
                    ball.Z, _headerFreeKick)
                : HeaderModel.ClearProbability(h, HeaderSkill(p), Distance(i), ball.Z, _headerFreeKick);
            bool success = _rng.NextDouble() < chance;
            if (i == attacker) { HeaderStats.AttemptsRolled++; if (success) HeaderStats.AttemptsSucceeded++; }
            if (!success) continue;
            if (i == attacker) HeadAtGoal(i, goal, keeper >= 0 ? at[keeper].Pos.Y : -ball.Y);
            else HeadClear(i, goal);
            return true;
        }
        return false;
    }

    private void HeadAtGoal(int index, Vec2 goal, double keeperY)
    {
        var h = _config.HeaderSettings;
        var p = _players[index];
        var shot = _config.ShotSettings with { AngleErrorBase = _config.ShotSettings.AngleErrorBase * h.SpreadFactor, SpeedMean = h.SpeedMean };
        // Aimed at the side away from the keeper, without a draw; the spread of ShotExecution decides the rest.
        var aim = new Vec2(goal.X, (keeperY >= 0 ? -1 : 1) * shot.AimOffsetMeters);
        double skill = Math.Clamp(FinishingFor(p) + h.ExecutionSkillWeight * (HeaderSkill(p) - h.NeutralSkill), 0, 1);
        var v = ShotExecution.Velocity(_ball.Pos.XY, aim, skill, _rng, shot);
        _ball = new BallState(_ball.Pos, v with { Z = h.DownSpeed });
        _events.Add(new Event(EventType.Shot, p.Team, p.Id, _tick, TimeSeconds, _ball.Pos.XY, "header"));
        HeaderTouch(index, shot: true);
    }

    private void HeadClear(int index, Vec2 ownGoal)
    {
        var h = _config.HeaderSettings;
        var p = _players[index];
        var away = _ball.Pos.XY - ownGoal;
        var dir = away.Length < 1e-9 ? new Vec2(-Math.Sign(ownGoal.X), 0) : away * (1 / away.Length);
        _ball = new BallState(_ball.Pos, new Vec3(dir.X * h.ClearSpeed, dir.Y * h.ClearSpeed, h.ClearRise));
        _events.Add(new Event(EventType.Pass, p.Team, p.Id, _tick, TimeSeconds, _ball.Pos.XY, "header clear"));
        HeaderTouch(index, shot: false);
    }

    // Same bookkeeping as a kick in open play (ExecutePass): the header is a touch, not control.
    private void HeaderTouch(int index, bool shot)
    {
        var p = _players[index];
        _lastTouch = p.Team;
        _lastShotTeam = shot ? p.Team : null;
        _restartKickPlayer = -1;
        _uninterruptedRestart = null;
        RegisterKick(index, OffsideSource.OpenPlay);
        _lastPasser = index;
        _kickOrigin = _ball.Pos.XY;
        _passReceiver = -1;
        _passerExcludedUntilTick = _tick + Math.Max(1, (long)Math.Ceiling(0.3 / _config.StepSeconds));
        _carrier = -1;
        if (p.Team == _possession) return;
        _events.Add(new Event(EventType.PossessionChange, p.Team, p.Id, _tick, TimeSeconds, _ball.Pos.XY, "from " + _possession));
        _possession = p.Team;
        _pendingPossessionTrigger = (p.Team, _ball.Pos.XY);
    }
}
