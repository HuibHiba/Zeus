namespace Zeus.Sim;

public sealed partial class Match
{
    public const int MaxScenarioGoals = 20;

    /// <summary>
    /// Scenario start for challenges, set before the first <see cref="Step"/>: the score at kick-off (counts as goals in the
    /// statistics, triggers and result) and, optionally, one side a player short from the start (their last outfield player is
    /// sent off at kick-off with a red card event, so the referee rules, substitutions and statistics treat it like any sending-off).
    /// </summary>
    public void StartScenario(int homeGoals = 0, int awayGoals = 0, Side? sentOff = null, int minute = 0)
    {
        if (_tick != 0 || _status != MatchStatus.Running) throw new InvalidOperationException("A scenario is set before the first step.");
        if (homeGoals < 0 || awayGoals < 0 || homeGoals > MaxScenarioGoals || awayGoals > MaxScenarioGoals)
            throw new ArgumentOutOfRangeException(nameof(homeGoals), $"Scenario goals must be in 0..{MaxScenarioGoals}.");
        if (minute != 0 && (minute < MinScenarioMinute || minute > MaxScenarioMinute))
            throw new ArgumentOutOfRangeException(nameof(minute), $"Start minute must be 0 or in {MinScenarioMinute}..{MaxScenarioMinute}.");
        _homeScore = homeGoals;
        _awayScore = awayGoals;
        if (sentOff is { } side)
        {
            // The last outfield player of that side (a forward in every shipped formation), never the keeper or the kick-off taker.
            int index = Array.FindLastIndex(_players, p => p.Team == side && !p.IsGoalkeeper && !p.SentOff);
            if (index < 0 || index == _carrier) throw new InvalidOperationException("No outfield player to send off.");
            var player = _players[index];
            player.SentOff = true;
            _events.Add(new Event(EventType.Card, side, player.Id, _tick, TimeSeconds, player.Body.Pos, CardDecision.Red.ToString()));
        }
        if (minute == 0) return;
        // Start in the second half at this minute: the clock, period and ends are set as after half-time, then a fresh home
        // kick-off as in Init. Energy starts full (no first half was played); the match ends at 90' as usual.
        long ticksPerMinute = (long)Math.Round(60 / _config.StepSeconds);
        _period = 2;
        _events.CurrentPeriod = 2;
        _endsSwapped = true;
        _tick = minute * ticksPerMinute;
        _periodStartTick = _tick - (long)((minute * 60 - _config.HalfSeconds) / _config.StepSeconds);
        _periodLostTicks = 0;
        PrepareKickOff(Side.Home);
        _ball = BallState.AtRest;
        _possession = Side.Home;
        double best = double.MaxValue;
        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i].Team != Side.Home || _players[i].SentOff) continue;
            double d = _players[i].Body.Pos.Length;
            if (d < best) { best = d; _carrier = i; }
        }
        _players[_carrier].Body = _players[_carrier].Body with { Pos = Vec2.Zero };
        StartReady();
    }

    public const int MinScenarioMinute = 46, MaxScenarioMinute = 85;
}
