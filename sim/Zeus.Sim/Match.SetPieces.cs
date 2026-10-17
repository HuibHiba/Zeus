namespace Zeus.Sim;

public sealed partial class Match
{
    private readonly Dictionary<Player, Vec2> _setPieceRuns = new();

    private void PrepareSetPiece(Side team, Vec2 spot, string reason, int taker)
    {
        _setPieceRuns.Clear();
        if (!_instructions.EnableDynamicShape || reason is not (nameof(PitchExitKind.Corner)
            or nameof(PitchExitKind.ThrowIn) or nameof(PitchExitKind.GoalKick) or "free_kick" or "indirect_free_kick")) return;

        double dir = -OwnGoalSign(team);
        double side = spot.Y < 0 ? -1 : 1;
        var mates = _players.Where(p => p.Team == team && !p.SentOff && !p.IsGoalkeeper && p != _players[taker])
            .OrderBy(p => (p.Body.Pos - spot).Length).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();
        var defenders = _players.Where(p => p.Team != team && !p.SentOff && !p.IsGoalkeeper).ToList();
        var threats = new List<Player>();

        void Place(Player player, Vec2 position, Vec2 run)
        {
            position = ClampToPitch(position);
            run = ClampToPitch(run);
            var facing = spot - position;
            player.Body = player.Body with { Pos = position, Vel = Vec2.Zero,
                Heading = Math.Atan2(facing.Y, facing.X) };
            _setPieceRuns[player] = run;
        }

        void Outlet(Vec2 position, Vec2 run)
        {
            if (mates.Count == 0) return;
            var player = mates[0];
            mates.RemoveAt(0);
            Place(player, position, run);
            threats.Add(player);
        }

        if (reason == nameof(PitchExitKind.Corner))
        {
            Outlet(spot + new Vec2(-dir * 7, -side * 7), spot + new Vec2(-dir * 8, -side * 10));
            // The existing model controls low balls only; use low deliveries until aerial duels exist.
            foreach (double y in new[] { side * 6, -side * 5, side * 12 })
                Outlet(new Vec2(dir * (Pitch.HalfLength - 13), y), new Vec2(dir * (Pitch.HalfLength - 9), y * .7));
            Outlet(new Vec2(dir * (Pitch.HalfLength - 23), 0), new Vec2(dir * (Pitch.HalfLength - 21), side * 2));
            // Keep two players behind the edge player to protect a clearance and the counterattack.
            for (int i = 0; i < mates.Count; i++)
            {
                var position = new Vec2(dir * (i < 2 ? 15 : 26), (i % 2 == 0 ? -1 : 1) * (i < 2 ? 12 : 20));
                Place(mates[i], position, position);
            }
        }
        else if (reason == nameof(PitchExitKind.GoalKick))
        {
            Outlet(new Vec2(-dir * 34, -13), new Vec2(-dir * 31, -16));
            Outlet(new Vec2(-dir * 34, 13), new Vec2(-dir * 31, 16));
            Outlet(new Vec2(-dir * 22, 0), new Vec2(-dir * 18, 0));
        }
        else
        {
            var inside = reason == nameof(PitchExitKind.ThrowIn) ? -side : (spot.Y > 0 ? -1 : 1);
            Outlet(spot + new Vec2(dir * 6, inside * 6), spot + new Vec2(dir * 8, inside * 8));
            Outlet(spot + new Vec2(-dir * 6, inside * 8), spot + new Vec2(-dir * 4, inside * 10));
            Outlet(spot + new Vec2(dir * 14, inside * 14), spot + new Vec2(dir * 16, inside * 12));

            if (reason is "free_kick" or "indirect_free_kick"
                && (new Vec2(dir * Pitch.HalfLength, 0) - spot).Length <= 35)
            {
                var towardsGoal = new Vec2(dir * Pitch.HalfLength, 0) - spot;
                towardsGoal *= 1 / towardsGoal.Length;
                var perpendicular = new Vec2(-towardsGoal.Y, towardsGoal.X);
                for (int i = 0; i < 2 && defenders.Count > 0; i++)
                {
                    var wall = defenders.OrderBy(p => (p.Body.Pos - spot).Length)
                        .ThenBy(p => p.Id, StringComparer.Ordinal).First();
                    defenders.Remove(wall);
                    var point = spot + towardsGoal * 9.3 + perpendicular * (i == 0 ? -.6 : .6);
                    Place(wall, point, point);
                }
            }
        }

        foreach (var attacker in threats)
        {
            if (defenders.Count == 0) break;
            var marker = defenders.OrderBy(p => (p.Body.Pos - attacker.Body.Pos).Length)
                .ThenBy(p => p.Id, StringComparer.Ordinal).First();
            defenders.Remove(marker);
            var goal = new Vec2(dir * Pitch.HalfLength, 0);
            var goalSide = goal - attacker.Body.Pos;
            goalSide *= 1.8 / Math.Max(goalSide.Length, 1.8);
            Place(marker, attacker.Body.Pos + goalSide, _setPieceRuns[attacker] + goalSide);
        }
    }

    private void StartSetPieceRuns()
    {
        // A brief prearranged movement; ordinary perception takes over after the delivery.
        foreach (var (player, target) in _setPieceRuns)
        {
            if (player.SentOff || player.Path is not null) continue;
            player.Target = player == (_passReceiver >= 0 ? _players[_passReceiver] : null) ? _passTarget : target;
            player.Intent = new Intent(IntentKind.MoveToPosition, player.Target, FacingTarget: _ball.Pos.XY);
            player.NextDecisionTick = _tick + Math.Max(1, (long)Math.Ceiling(.8 / _config.StepSeconds));
        }
        _setPieceRuns.Clear();
    }
}
