namespace Zeus.Sim;

// Match rule orchestration; pure decisions live in Referee, PitchRules and OffsideRules.
public sealed partial class Match
{
    private void ResolveDuel()
    {
        if (_carrier < 0 || _tick < _nextDuelTick) return;
        var holder = _players[_carrier];
        var duelSettings = _config.DuelSettings;
        int defenderIndex = -1;
        double nearest = duelSettings.Reach;
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (!player.Started || player.SentOff || player.Team == holder.Team || player.Intent?.Kind != IntentKind.Press
                && !(_instructions.EnableDribbleInside && holder.Intent?.Kind == IntentKind.DribbleInside
                    && holder.View?.VisiblePlayers.Any(p => p.Team == player.Team && p.Id == player.Id) == true)) continue;
            double distance = (player.Body.Pos - holder.Body.Pos).Length;
            if (distance <= nearest) { nearest = distance; defenderIndex = i; }
        }
        if (defenderIndex < 0) return;
        // Opportunity second: the defender attempts a challenge with probability AttemptRate (one draw; none when always).
        _nextDuelTick = _tick + Math.Max(1, (long)Math.Ceiling(1.0 / _config.StepSeconds));
        if (duelSettings.AttemptRate < 1 && _rng.NextDouble() >= duelSettings.AttemptRate) return;
        var defender = _players[defenderIndex];
        var offset = defender.Body.Pos - holder.Body.Pos;
        double angle = Mover.NormalizeAngle(Math.Atan2(offset.Y, offset.X) - holder.Body.Heading);
        var relative = defender.Body.Vel - holder.Body.Vel;
        double closing = nearest < 1e-9 ? 0 : -(relative.X * offset.X + relative.Y * offset.Y) / nearest;
        if (HasAttributes(defender) && _config.AttributeSettings.Defending)
            duelSettings = duelSettings with { BaseFoul = Math.Clamp(duelSettings.BaseFoul + .15 * Math.Max(0, RefDefending - Attributes(defender).Defending), 0, 1) };
        var outcome = DuelModel.Resolve(nearest, closing, angle,
            HasAttributes(defender) && _config.AttributeSettings.Defending ? Math.Clamp(0.6 + Attributes(defender).Defending - RefDefending, 0, 1) : 0.6,
            HasAttributes(defender) && _config.AttributeSettings.Discipline ? Attributes(defender).Discipline : 0.75,
            _config.EnableFatigue ? 1 - defender.Energy.ShortEnergy : 0, _rng, duelSettings,
            inOwnPenaltyArea: InPenaltyArea(defender.Team, holder.Body.Pos));
        _events.Add(new Event(EventType.Duel, defender.Team, defender.Id, _tick, TimeSeconds, holder.Body.Pos, outcome.ToString()));
        if (outcome == DuelOutcome.WonBall)
        {
            _carrier = defenderIndex;
            _controlSinceTick = _tick;
            _possession = defender.Team;
            _pendingPossessionTrigger = (defender.Team, holder.Body.Pos);
            _lastTouch = defender.Team;
            _offsideCandidates = Array.Empty<string>();
            _events.Add(new Event(EventType.PossessionChange, defender.Team, defender.Id, _tick, TimeSeconds, holder.Body.Pos, "duel"));
        }
        else if (outcome == DuelOutcome.LooseBall)
        {
            _lastTouch = defender.Team;
            // Unit direction towards the defender times LooseBallSpeed (m/s), not a multiple of the distance in metres.
            _ball = new BallState(new Vec3(holder.Body.Pos.X, holder.Body.Pos.Y, 0),
                DuelModel.LooseBallVelocity(offset, holder.Body.Heading, duelSettings));
            _lastPasser = _carrier;
            _passerExcludedUntilTick = _tick + Math.Max(1, (long)Math.Ceiling(0.3 / _config.StepSeconds));
            _carrier = -1;
        }
        else if (outcome == DuelOutcome.Foul)
        {
            // The v0 action model samples an unsafe challenge, not the referee's judgement.
            // Its explicit Careless classification is synthetic until real tackle/contact facts are modelled.
            // Conduct follows from the duel facts (closing speed, approach angle, pitch third), not from a draw.
            double relX = (holder.Body.Pos.X * -OwnGoalSign(defender.Team) + Pitch.HalfLength) / Pitch.Length;
            var conductSettings = _config.DuelSettings;
            if (HasAttributes(defender) && _config.AttributeSettings.Discipline)
            {
                // Discipline changes the unsafe action; the referee still judges the supplied conduct deterministically.
                double bias = .15 * (Attributes(defender).Discipline - RefDiscipline);
                conductSettings = conductSettings with { RecklessOwnThird = Math.Clamp(conductSettings.RecklessOwnThird + bias, 0, 1),
                    RecklessMiddle = Math.Clamp(conductSettings.RecklessMiddle + bias, 0, 1),
                    RecklessAttacking = Math.Clamp(conductSettings.RecklessAttacking + bias, 0, 1) };
            }
            var conduct = DuelModel.ClassifyConduct(closing, angle, relX, conductSettings);
            ReportIncident(defender.Team, defender.Id, holder.Body.Pos,
                new RefereeIncident(ContactAction.Charge, conduct, true, true,
                    InsideDefendingPenaltyArea: InPenaltyArea(defender.Team, holder.Body.Pos),
                    TacticalContext: TacticalContext.NoTacticalOffence, AgainstOpponent: true));
        }
    }

    public RefereeDecision ReportIncident(Side offendingTeam, string playerId, Vec2 location, RefereeIncident incident)
    {
        if (!double.IsFinite(location.X) || !double.IsFinite(location.Y))
            throw new ArgumentException("Incident location must be finite.");
        if (incident.BallInPlay != (_status == MatchStatus.Running))
            throw new ArgumentException("Incident ball-in-play fact differs from match state.");
        var player = _players.FirstOrDefault(p => p.Team == offendingTeam && p.Id == playerId)
            ?? throw new ArgumentException("Unknown incident player.");
        var decision = Referee.Assess(incident);
        if (!decision.Complete) throw new ArgumentException("Incomplete or unsupported incident assessment: " + decision.Reason);
        if (decision.Restart == RefereeRestart.None) return decision;
        var card = decision.Card;
        if (card == CardDecision.Yellow)
        {
            player.YellowCards++;
            if (player.YellowCards >= 2) card = CardDecision.SecondYellowRed;
        }
        if (card is CardDecision.Red or CardDecision.SecondYellowRed) player.SentOff = true;
        if (card != CardDecision.None)
            _events.Add(new Event(EventType.Card, offendingTeam, playerId, _tick, TimeSeconds, location, card.ToString()));
        if (_players.Count(p => p.Team == offendingTeam && !p.SentOff) < 7)
        {
            _carrier = -1;
            _status = MatchStatus.Abandoned;
            _events.Add(new Event(EventType.Abandoned, offendingTeam, "", _tick, TimeSeconds, location, "fewer than seven players"));
            return decision with { Card = card };
        }
        if (player.SentOff && player.IsGoalkeeper)
        {
            var goal = new Vec2(OwnGoalSign(offendingTeam) * Pitch.HalfLength, 0);
            var replacement = _players.Where(p => p.Team == offendingTeam && !p.SentOff)
                .OrderBy(p => (p.Body.Pos - goal).Length).First();
            replacement.IsGoalkeeper = true;
            replacement.Build = player.Build;
            replacement.Defend = player.Defend;
            _events.Add(new Event(EventType.KeeperChanged, offendingTeam, replacement.Id, _tick, TimeSeconds,
                replacement.Body.Pos, "emergency keeper"));
        }
        if (decision.Restart is RefereeRestart.DirectFreeKick or RefereeRestart.Penalty)
        {
            _events.Add(new Event(EventType.Foul, offendingTeam, playerId, _tick, TimeSeconds, location, incident.Conduct is { } conduct ? conduct.ToString() : ""));
            var team = offendingTeam == Side.Home ? Side.Away : Side.Home;
            var spot = decision.Restart == RefereeRestart.Penalty
                ? new Vec2(OwnGoalSign(offendingTeam) * (Pitch.HalfLength - 11), 0) : ClampToPitch(location);
            BeginRestart(team, spot, decision.Restart == RefereeRestart.Penalty ? "penalty" : "free_kick");
        }
        else if (_status == MatchStatus.Restart && _players[_restartTaker].SentOff)
        {
            long originalDeadline = _restartUntilTick;
            BeginRestart(_possession, _ball.Pos.XY, _restartReason);
            _restartUntilTick = originalDeadline;
        }
        return decision with { Card = card };
    }

    private void CheckOutOfBounds()
    {
        if (_status != MatchStatus.Running || (Math.Abs(_ball.Pos.X) <= Pitch.HalfLength + _config.BallPhysics.Radius
            && Math.Abs(_ball.Pos.Y) <= Pitch.HalfWidth + _config.BallPhysics.Radius)) return;
        var spot = new Vec2(Math.Clamp(_ball.Pos.X, -Pitch.HalfLength, Pitch.HalfLength),
            Math.Clamp(_ball.Pos.Y, -Pitch.HalfWidth, Pitch.HalfWidth));
        _events.Add(new Event(EventType.Out, _possession, "", _tick, TimeSeconds, spot));
        // Covers externally injected balls already beyond the boundary; normal exits use swept PitchRules.
        BeginRestart(_lastTouch == Side.Home ? Side.Away : Side.Home, spot, "boundary");
    }

    private void HandlePitchExit(PitchExit exit)
    {
        if (exit.Kind == PitchExitKind.Goal && _uninterruptedRestart is not null)
        {
            bool ownGoal = exit.ScoringTeam != _uninterruptedRestartTeam;
            bool disallowedDirect = _uninterruptedRestart is "indirect_free_kick" or nameof(PitchExitKind.ThrowIn);
            if (ownGoal || disallowedDirect)
                exit = exit with { Kind = ownGoal ? PitchExitKind.Corner : PitchExitKind.GoalKick,
                    RestartTeam = ownGoal ? exit.ScoringTeam!.Value : exit.RestartTeam, ScoringTeam = null };
        }
        if (exit.Kind == PitchExitKind.Goal)
        {
            if (exit.ScoringTeam == Side.Home) _homeScore++; else _awayScore++;
            _events.Add(new Event(EventType.Goal, exit.ScoringTeam!.Value, "", _tick, TimeSeconds, exit.Position));
            // The kick-off line-up is reconstructed on each team's own half.
            PrepareKickOff(exit.RestartTeam);
            BeginRestart(exit.RestartTeam, Vec2.Zero, "kick_off");
            return;
        }
        var spot = exit.Kind switch {
            PitchExitKind.Corner => new Vec2(Math.CopySign(Pitch.HalfLength - 0.4, exit.Position.X), Math.CopySign(Pitch.HalfWidth - 0.4, exit.Position.Y)),
            PitchExitKind.GoalKick => new Vec2(Math.CopySign(Pitch.HalfLength - 5.5, exit.Position.X), 0),
            _ => ClampToPitch(exit.Position)
        };
        _events.Add(new Event(EventType.Out, _lastTouch, "", _tick, TimeSeconds, spot, exit.Kind.ToString()));
        BeginRestart(exit.RestartTeam, spot, exit.Kind.ToString());
    }

    private void TakeRestartKick()
    {
        var player = _players[_carrier];
        var snapshot = GetSnapshot();
        var view = PerceptionBuilder.Build(snapshot, snapshot.Players[_carrier], PhaseOf(player.Team), true, _sensing, _config.StepSeconds);
        var option = PassChoice.Choose(view, _config.PassSettings);
        var aim = option?.Target ?? ClampToPitch(player.Body.Pos + new Vec2(Math.Cos(player.Body.Heading), Math.Sin(player.Body.Heading)) * 10);
        if (_restartReason == "penalty")
        {
            double goalX = -OwnGoalSign(_possession) * Pitch.HalfLength;
            int side = _rng.NextDouble() < 0.5 ? -1 : 1;
            _ball = new BallState(_ball.Pos, PenaltyExecution.Velocity(_ball.Pos.XY, goalX, side,
                _config.PassSettings.Skill, _rng, _config.PenaltySettings, _config.ShotSettings));
            CommitPenaltyKeeper(goalX);
        }
        else
        {
            var velocity = PassExecution.Velocity(_ball.Pos.XY, aim, 0, _config.PassSettings, _rng);
            _ball = _restartReason == nameof(PitchExitKind.ThrowIn)
                ? new BallState(new Vec3(_ball.Pos.X, _ball.Pos.Y, 1.7), new Vec3(velocity.X, velocity.Y, 3))
                : new BallState(_ball.Pos, velocity);
        }
        _events.Add(new Event(_restartReason == "penalty" ? EventType.Shot : EventType.Pass,
            player.Team, player.Id, _tick, TimeSeconds, _ball.Pos.XY, "restart " + _restartReason));
        _lastTouch = player.Team;
        _lastShotTeam = _restartReason == "penalty" ? player.Team : null;
        RegisterKick(_carrier, _restartReason switch {
            nameof(PitchExitKind.GoalKick) => OffsideSource.GoalKick,
            nameof(PitchExitKind.Corner) => OffsideSource.Corner,
            nameof(PitchExitKind.ThrowIn) => OffsideSource.ThrowIn,
            _ => OffsideSource.OpenPlay
        });
        _restartKickPlayer = _carrier;
        _uninterruptedRestart = _restartReason;
        _uninterruptedRestartTeam = player.Team;
        _lastPasser = _carrier;
        _kickOrigin = _ball.Pos.XY;
        _passReceiver = -1;
        _passerExcludedUntilTick = _tick + Math.Max(1, (long)Math.Ceiling(0.3 / _config.StepSeconds));
        _carrier = -1;
    }

    // The defending keeper commits to a side at the kick (a 50/50 draw, independent of the taker's choice) and pushes off
    // towards it; his normal behaviour (which would read the ball's flight perfectly) resumes after KeeperDiveSeconds.
    private void CommitPenaltyKeeper(double goalX)
    {
        int keeperIndex = Array.FindIndex(_players, p => p.Team != _possession && p.IsGoalkeeper && !p.SentOff);
        if (keeperIndex < 0) return;
        var penalty = _config.PenaltySettings;
        int dive = _rng.NextDouble() < 0.5 ? -1 : 1;
        var keeper = _players[keeperIndex];
        var target = new Vec2(goalX + Math.Sign(-goalX) * 0.8, dive * penalty.KeeperDiveMeters);
        keeper.Intent = new Intent(IntentKind.KeeperGuard, target);
        keeper.Target = target;
        keeper.Body = keeper.Body with { Vel = new Vec2(0, dive * penalty.KeeperPushOffSpeed) };
        keeper.NextDecisionTick = _tick + Math.Max(1, (long)Math.Ceiling(penalty.KeeperDiveSeconds / _config.StepSeconds));
    }

    private void BeginRestart(Side team, Vec2 spot, string reason, int? preferredTaker = null)
    {
        int taker = preferredTaker ?? -1;
        if (taker < 0 || _players[taker].SentOff || _players[taker].Team != team)
        {
            double nearest = double.MaxValue;
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i].Team != team || _players[i].SentOff) continue;
                double distance = (_players[i].Body.Pos - spot).Length;
                if (distance < nearest) { nearest = distance; taker = i; }
            }
        }
        if (taker < 0)
        {
            _carrier = -1;
            _status = MatchStatus.Abandoned;
            _ball = new BallState(new Vec3(spot.X, spot.Y, 0), Vec3.Zero);
            _events.Add(new Event(EventType.Abandoned, team, "", _tick, TimeSeconds, spot, "no active restart taker"));
            return;
        }
        foreach (var player in _players)
        {
            var pos = player.Body.Pos;
            if (!player.SentOff && reason == nameof(PitchExitKind.GoalKick) && player.Team != team
                && InPenaltyArea(team, pos))
                pos = new Vec2(OwnGoalSign(team) * (Pitch.HalfLength - 16.5 - 0.1), pos.Y);
            double distanceRequired = reason == nameof(PitchExitKind.ThrowIn) ? 2 : 9.15;
            if (!player.SentOff && player.Team != team && (pos - spot).Length < distanceRequired)
            {
                var away = pos - spot;
                if (away.Length < 1e-9) away = new Vec2(1, 0);
                pos = ClampToPitch(spot + away * (distanceRequired / away.Length));
                if ((pos - spot).Length < distanceRequired - 1e-8)
                {
                    var inside = -spot;
                    if (inside.Length < 1e-9) inside = new Vec2(1, 0);
                    pos = spot + inside * (distanceRequired / inside.Length);
                }
            }
            if (!player.SentOff && reason == "penalty")
            {
                bool defendingKeeper = player.Team != team && player.IsGoalkeeper;
                if (defendingKeeper) pos = new Vec2(-OwnGoalSign(team) * Pitch.HalfLength, 0);
                else if (player != _players[taker])
                {
                    double goalSign = -OwnGoalSign(team);
                    pos = new Vec2(goalSign * Math.Min(pos.X * goalSign, Pitch.HalfLength - 16.5 - 0.1), pos.Y);
                    if ((pos - spot).Length < 9.15)
                        pos = new Vec2(goalSign * (Math.Abs(spot.X) - 9.15), pos.Y);
                }
            }
            player.Body = player.Body with { Pos = pos, Vel = Vec2.Zero };
            player.Intent = null;
            player.NextDecisionTick = _tick;
        }
        var inward = -spot;
        var facing = inward;
        if (inward.Length < 1e-9)
        {
            // Kick-off at the centre spot: stand on the own half, facing the opponent's goal (follows ends swap).
            inward = new Vec2(OwnGoalSign(team), 0);
            facing = new Vec2(-OwnGoalSign(team), 0);
        }
        // Leave space for the carried ball even when the player turns at a boundary/corner.
        var takerPos = ClampToPitch(spot + inward * (0.7 / inward.Length));
        _players[taker].Body = _players[taker].Body with { Pos = reason == nameof(PitchExitKind.ThrowIn) ? spot : takerPos,
            Heading = reason == "penalty" ? (OwnGoalSign(team) < 0 ? 0 : Math.PI) : Math.Atan2(facing.Y, facing.X) };
        _ball = new BallState(new Vec3(spot.X, spot.Y, 0), Vec3.Zero);
        _possession = team;
        _carrier = -1;
        _status = MatchStatus.Restart;
        _restartTaker = taker;
        _restartReason = reason;
        _restartKickPlayer = -1;
        _uninterruptedRestart = null;
        _restartUntilTick = _tick + Math.Max(1, (long)Math.Ceiling(_config.RestartPauseSeconds / _config.StepSeconds));
        _events.Add(new Event(EventType.RestartAwarded, team, _players[taker].Id, _tick, TimeSeconds, spot, reason));
        UpdatePhases();
    }

    private static Vec2 ClampToPitch(Vec2 pos) => new(Math.Clamp(pos.X, -Pitch.HalfLength, Pitch.HalfLength),
        Math.Clamp(pos.Y, -Pitch.HalfWidth, Pitch.HalfWidth));

    private double OwnGoalSign(Side team) => (team == Side.Home ? -1 : 1) * (_endsSwapped ? -1 : 1);

    private bool InPenaltyArea(Side defendingTeam, Vec2 pos) =>
        Math.Abs(pos.Y) <= 20.15 && pos.X * OwnGoalSign(defendingTeam) >= Pitch.HalfLength - 16.5;

    private void PrepareKickOff(Side kickingTeam)
    {
        foreach (var player in _players.Where(p => !p.SentOff))
        {
            var layout = player.Team == kickingTeam ? player.Build : player.Defend;
            player.Body = new BodyState(Pitch.ToMeters(Math.Min(layout.X, 0.5), layout.Y, player.Team, _endsSwapped),
                Vec2.Zero, OwnGoalSign(player.Team) < 0 ? 0 : Math.PI);
        }
        _offsideCandidates = Array.Empty<string>();
    }

    private void CheckPeriodEnd()
    {
        long regulationTicks = (long)Math.Ceiling(_config.HalfSeconds / _config.StepSeconds - 1e-9);
        if (_tick - _periodStartTick < regulationTicks + _periodLostTicks) return;
        if ((_status == MatchStatus.Restart && _restartReason == "penalty") || _uninterruptedRestart == "penalty") return;
        if (_period == 1)
        {
            _events.Add(new Event(EventType.HalfTime, Side.Home, "", _tick, TimeSeconds, Vec2.Zero));
            RecoverHalfTimeFatigue();
            _period = 2;
            _events.CurrentPeriod = 2;
            _endsSwapped = true;
            _periodStartTick = _tick;
            _periodLostTicks = 0;
            PrepareKickOff(Side.Away);
            BeginRestart(Side.Away, Vec2.Zero, "second_half_kick_off");
        }
        else
        {
            _status = MatchStatus.Finished;
            _carrier = -1;
            _ball = _ball with { Vel = Vec3.Zero };
            foreach (var player in _players) player.Body = player.Body with { Vel = Vec2.Zero };
            _events.Add(new Event(EventType.FullTime, Side.Home, "", _tick, TimeSeconds, Vec2.Zero));
        }
    }

    private void RegisterKick(int kickerIndex, OffsideSource source)
    {
        var kicker = _players[kickerIndex];
        var team = kicker.Team;
        _offsideTeam = team;
        _offsideCandidates = OffsideRules.PositionsAtTouch(GetSnapshot(), team, OwnGoalSign(team) < 0, source)
            .Where(id => id != kicker.Id).ToArray();
    }

}
