namespace Zeus.Sim;

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
}
