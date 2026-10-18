namespace Zeus.Sim;

public sealed partial class Match
{
    private (Side Winner, Vec2 Location)? _pendingPossessionTrigger;

    public BlockHeight? BlockOf(Side team) => team == Side.Home ? _homeBlock : _awayBlock;

    private PhasePlayer DefensivePlayer(Player player)
    {
        var block = BlockOf(player.Team);
        return block is { } b && player.Tactic.DefendVariants.TryGetValue(b, out var variant)
            ? variant.Players.First(p => p.Id == player.Id) : player.Defend;
    }

    private void UpdateBlocks()
    {
        if (!_config.EnableBlockSwitches) return;
        // Minute excludes accumulated stoppages; thresholds are based on regulation time.
        int minute = (int)(((_period - 1) * _config.HalfSeconds
            + Math.Min(_config.HalfSeconds, (_tick - _periodStartTick - _periodLostTicks) * _config.StepSeconds)) / 60);
        foreach (var team in new[] { Side.Home, Side.Away })
        {
            var players = _players.Where(p => p.Team == team && !p.SentOff).ToArray();
            if (players.Length == 0) continue;
            int goalDifference = team == Side.Home ? _homeScore - _awayScore : _awayScore - _homeScore;
            var score = goalDifference > 0 ? ScoreState.Voor : goalDifference < 0 ? ScoreState.Achter : ScoreState.Gelijk;
            var block = _blockOverride.TryGetValue(team, out var forced) ? forced : BlockSelection.Select(players[0].Tactic, score, minute,
                players.Average(p => 1 - p.Energy.LongEnergy));
            var previous = BlockOf(team);
            if (previous == block) continue;
            if (team == Side.Home) _homeBlock = block; else _awayBlock = block;
            foreach (var player in players) UpdateDefensiveGeometry(player);
            _events.Add(new Event(EventType.BlockChange, team, "", _tick, TimeSeconds, _ball.Pos.XY,
                $"{previous}->{block}"));
        }
    }

    // State triggers have no event to hang on: re-check them every PressSeconds, which keeps the temporary order continuous while they hold.
    private void UpdateStateTriggers()
    {
        if (!_config.EnableTriggers || _status != MatchStatus.Running) return;
        long every = Math.Max(1, (long)Math.Ceiling(_config.TriggerSettings.PressSeconds / _config.StepSeconds));
        if (_tick % every != 0) return;
        int minute = (int)(((_period - 1) * _config.HalfSeconds
            + Math.Min(_config.HalfSeconds, (_tick - _periodStartTick - _periodLostTicks) * _config.StepSeconds)) / 60);
        foreach (var team in new[] { Side.Home, Side.Away })
        {
            if (_possession != team) FireTriggers(team, TriggerRules.OpponentBallInZone, _ball.Pos.XY);
            if (TriggerRules.IsBehindAfterMinute(minute, team == Side.Home ? _homeScore - _awayScore : _awayScore - _homeScore))
                FireTriggers(team, TriggerRules.BehindAfterMinute, _ball.Pos.XY);
        }
    }

    private void UpdatePhases()
    {
        if (!_config.EnableTeamPhases) { SyncPaths(); return; }
        foreach (var change in _phases.Update(_possession, _status != MatchStatus.Running,
            BallRel(Side.Home, _ball.Pos).X, TimeSeconds))
            _events.Add(new Event(EventType.PhaseChange, change.Team, "", _tick, TimeSeconds,
                _ball.Pos.XY, $"{change.From}->{change.To}: {change.Reason}"));
        SyncPaths();
        if (_pendingPossessionTrigger is { } possession)
        {
            _pendingPossessionTrigger = null;
            var loser = possession.Winner == Side.Home ? Side.Away : Side.Home;
            if (TriggerRules.IsLossInOwnHalf(TriggerRules.RelativePosition(possession.Location, loser, _endsSwapped).X, _config.TriggerSettings))
                FireTriggers(loser, TriggerRules.LossInOwnHalf, possession.Location);
            if (TriggerRules.IsWinInHighZone(TriggerRules.RelativePosition(possession.Location, possession.Winner, _endsSwapped).X, _config.TriggerSettings))
                FireTriggers(possession.Winner, TriggerRules.WinInHighZone, possession.Location);
        }
    }
}
