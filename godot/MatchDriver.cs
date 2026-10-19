// Runs the match: owns the Zeus.Sim instance, the fixed-step accumulator, skip, replay check, highlight re-simulation and event
// reading. No drawing: Main (2D) and later Match3D only read Current/Previous/Alpha and subscribe to the events below.
// The only place in godot/ that may call Match.Step on a live match (scripts/check_match_step.py enforces this).
using System;
using System.IO;
using System.Linq;
using Godot;
using Zeus.Sim;
using Side = Zeus.Sim.Side;

public partial class MatchDriver : Node
{
    Match _match = null!;
    Snapshot _prev = null!, _curr = null!;
    double _stepSeconds;
    double _acc;
    double _speed = 20;
    bool _paused;
    string _homeTactic = "", _awayTactic = "";
    readonly Heatmap _heatHome = new(), _heatAway = new();   // one sample per simulated second, for the end screen
    readonly int[] _live = new int[8];          // live counters (home, away): shots, passes, ball controls, fouls
    bool _pauseRequested;                       // B pressed: open the pause menu at the next rest moment
    Squad? _homeSquad, _awaySquad;              // names and roles for the pause menu (the match itself uses the same neutral squad)
    Side _playerSide = Side.Home;               // the side the player controls: Home, except when he plays away in the competition
    bool _skipping;                             // END pressed: the rest of the match is played in chunks per frame
    const int SkipStepsPerFrame = 3000, SkipStepLimit = 150000;
    int _skipSteps;
    double _totalSeconds = 5400;
    int _countedEvents;
    ReplayFile? _replay;
    OpponentSubstitutionPolicy? _opponentSubstitutions;
    bool _replayVerified;
    ulong _logSeed;
    string _logMode = "menu";
    // Highlight reel (end card): the same match is simulated again (same inputs and recorded commands, so it is identical) and played from tick A to B.
    Func<Match> _newMatch = null!;
    ShotConfig _shotConfig = new();
    System.Collections.Generic.List<Moment>? _moments;
    Match? _hl, _finalMatch;                    // _hl is set while the reel runs; _match/_prev/_curr/_live then show the re-simulation
    Snapshot? _finalPrev, _finalCurr;
    int[] _finalLive = new int[8];
    int _finalCounted, _hlIndex;
    double _hlPlayed;

    /// <summary>Raised for every event read from the match (not during the highlight reel), after the snapshot it belongs to is Current.</summary>
    public event Action<Zeus.Sim.Event>? EventRead;
    /// <summary>Raised after a batch of events was read (one ReadEvents call); the view feeds its popups here.</summary>
    public event Action? EventsRead;
    /// <summary>Raised when a requested pause reached a rest moment: the match is paused, the view opens its menu.</summary>
    public event Action? RestMomentReached;
    public event Action? HighlightStarted;
    public event Action? HighlightEnded;

    public Match Match => _match;
    public Snapshot Current => _curr;
    public Snapshot Previous => _prev;
    /// <summary>Interpolation factor between Previous and Current for the picture (1 while paused).</summary>
    public float Alpha => _paused ? 1f : (float)Math.Clamp(_acc / _stepSeconds, 0, 1);
    public double Speed => _speed;
    public bool Paused => _paused;
    public bool PauseRequested => _pauseRequested;
    public bool Skipping => _skipping;
    public double TotalSeconds => _totalSeconds;
    public double StepSeconds => _stepSeconds;
    public bool Highlighting => _hl is not null;
    public int HighlightIndex => _hlIndex;
    public double HighlightPlayed => _hlPlayed;
    public System.Collections.Generic.List<Moment>? Moments => _moments;
    public ReplayFile? Replay => _replay;
    public bool ReplayVerified => _replayVerified;
    public Side PlayerSide => _playerSide;
    public Squad? HomeSquad => _homeSquad;
    public Squad? AwaySquad => _awaySquad;
    public Squad? PlayerSquad => _playerSide == Side.Home ? _homeSquad : _awaySquad;
    public int[] Live => _live;
    public Heatmap HeatHome => _heatHome;
    public Heatmap HeatAway => _heatAway;
    public string HomeTactic => _homeTactic;
    public string AwayTactic => _awayTactic;
    public ulong LogSeed => _logSeed;
    public string LogMode => _logMode;

    // A live challenge match also ends when the referee abandons it (the result then counts as awarded, like in the simulation).
    // Menu and league matches need the same terminal screen so a referee's stoppage cannot strand the player.
    public bool MatchOver => _curr.Status is MatchStatus.Finished or MatchStatus.Abandoned;

    /// <summary>False at the end screen, while skipping and in a replay: no pause menu (coaching) then.</summary>
    public bool CanSteer => _replay is null && PlayerSquad is not null && !_skipping && !MatchOver;

    public static string Env(string name, string fallback)
    {
        var v = GameSession.DebugEnvironment(name);
        return v.Length > 0 ? v : fallback;
    }

    public void Initialize()
    {
        var replayPath = GameSession.DebugEnvironment("ZEUS_REPLAY");
        Tactic home, away;
        Squad? homeSquad = null, awaySquad = null;
        MatchConfig config;
        ulong seed = 1;
        FamiliarityState? homeFamiliarity = null, awayFamiliarity = null;
        if (replayPath.Length > 0)
        {
            _replay = ReplayFile.Load(replayPath);
            home = TacticLoader.Parse(_replay.HomeTacticJson);
            away = TacticLoader.Parse(_replay.AwayTacticJson);
            config = _replay.Config;
            seed = _replay.Seed;
        }
        else
        {
            var dir = Env("ZEUS_TACTICS_DIR", GameSession.TacticsDir());
            home = TacticLoader.LoadFile(GameSession.HomePath ?? Path.Combine(dir, Env("ZEUS_HOME", "laag_blok_442.json")));
            away = TacticLoader.LoadFile(GameSession.AwayPath ?? Path.Combine(dir, Env("ZEUS_AWAY", "testcase_a_433.json")));
            config = new MatchConfig();
            seed = GameSession.Seed;
            homeSquad = SquadFactory.ForLevel(Env("ZEUS_HOMELEVEL", GameSession.HomeLevel), home, GameSession.SquadsDir());
            awaySquad = SquadFactory.ForLevel(Env("ZEUS_AWAYLEVEL", GameSession.AwayLevel), away, GameSession.SquadsDir());
            if (GameSession.InCompetition && GameSession.CompetitionSquad is { } careerSquad)      // career v1: signings in, absent starters replaced, as in the simulation
            {
                if (GameSession.PlayerSide == Side.Home) homeSquad = careerSquad; else awaySquad = careerSquad;
            }
            if (GameSession.InCompetition && GameSession.CompetitionFamiliarity is { } careerFamiliarity)
            {
                config = config with { EnableFamiliarity = true };
                if (GameSession.PlayerSide == Side.Home) homeFamiliarity = careerFamiliarity;
                else awayFamiliarity = careerFamiliarity;
            }
            if (GameSession.UseFamiliarity && !GameSession.InCompetition && GameSession.Challenge is null && homeSquad is not null)     // selection screen checkbox (v0, voorlopig)
            {
                var key = FamiliarityStore.Key(Env("ZEUS_HOMELEVEL", GameSession.HomeLevel), homeSquad.Name);
                homeFamiliarity = FamiliarityStore.Load().Get(key, key, homeSquad.Starters.Concat(homeSquad.Bench).Select(p => p.Id));
                config = config with { EnableFamiliarity = true };
            }
        }
        _stepSeconds = config.StepSeconds;
        _homeTactic = Loc.Name(home.Name); _awayTactic = Loc.Name(away.Name);
        _shotConfig = config.ShotSettings;
        _newMatch = () => Match.Create(config, home, away, seed, homeSquad: homeSquad, awaySquad: awaySquad, homeFamiliarity: homeFamiliarity, awayFamiliarity: awayFamiliarity);
        _match = _newMatch();
        _logSeed = seed;
        _logMode = _replay is not null ? "replay" : GameSession.Challenge is not null ? "challenge" : GameSession.InCompetition ? "competition" : "menu";
        FeedbackLog.MatchStarted(seed, home.Name, away.Name, Env("ZEUS_HOMELEVEL", GameSession.HomeLevel), Env("ZEUS_AWAYLEVEL", GameSession.AwayLevel), _logMode);
        _homeSquad = homeSquad ?? Squad.NeutralFor(home);
        _awaySquad = awaySquad ?? Squad.NeutralFor(away);
        _playerSide = GameSession.InCompetition ? GameSession.PlayerSide : Side.Home;
        if (GameSession.DebugEnvironment("ZEUS_PLAYERSIDE") == "away") _playerSide = Side.Away;       // visual check of the pause menu for the away side
        _opponentSubstitutions = _replay is null && GameSession.OpponentSubstitutions ? new OpponentSubstitutionPolicy() : null;
        _totalSeconds = config.HalfSeconds * 2;
        _prev = _curr = _match.GetSnapshot();
        if (GameSession.DebugEnvironment("ZEUS_FINISH").Length > 0)         // visual check of the end screen: play the whole match instantly
        {
            for (int n = 0; n < 150000 && _match.GetSnapshot().Status != MatchStatus.Finished; n++)
            {
                StepMatch();
                if (n % 10 == 0) SampleHeat(_match.GetSnapshot());
            }
            _prev = _curr = _match.GetSnapshot();
        }
        if (GameSession.DebugEnvironment("ZEUS_SHOT").Length > 0) _speed = 4.0;
        ReadEvents();
        GD.Print($"Loaded '{home.Name}' (home) vs '{away.Name}' (away), step {_stepSeconds}s");
    }

    // Pause and steer (PRD): substitutions and tactic changes are only accepted at a rest moment, so B waits for the next one.
    public void RequestPause()
    {
        if (!CanSteer) return;
        _pauseRequested = true;
        if (_match.IsRestMoment) ReachRestMoment();
    }

    void ReachRestMoment()
    {
        _pauseRequested = false;
        _paused = true;
        RestMomentReached?.Invoke();
    }

    /// <summary>The pause menu closed: run on from the current tick.</summary>
    public void Resume()
    {
        _paused = false;
        _acc = 0;
    }

    /// <summary>Space: a verified replay stays paused.</summary>
    public void TogglePause()
    {
        if (!_replayVerified) _paused = !_paused;
    }

    // Fast result with analysis (PRD): play the rest of the match at once, then the end screen shows the stats and heatmaps.
    // Played in chunks of SkipStepsPerFrame per frame (see SkipChunk) so the window keeps responding; the heat samples are kept.
    public void SkipToEnd()
    {
        if (_replay is not null || MatchOver || _skipping) return;
        _skipping = true;
        _skipSteps = 0;
        _pauseRequested = false;
        _acc = 0;
    }

    void StepMatch()
    {
        _replay?.ApplyDue(_match);
        if (_match.IsRestMoment)
            _opponentSubstitutions?.TrySubstitute(_match, _playerSide == Side.Home ? Side.Away : Side.Home);
        _match.Step();
    }

    void SkipChunk()
    {
        for (int n = 0; n < SkipStepsPerFrame && _skipSteps < SkipStepLimit && _match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned); n++, _skipSteps++)
        {
            StepMatch();
            if (_skipSteps % 10 == 0) SampleHeat(_match.GetSnapshot());
        }
        _prev = _curr = _match.GetSnapshot();
        ReadEvents();       // skipping suppresses all popups except the final result (the view reads Skipping)
        if (_match.Status is MatchStatus.Finished or MatchStatus.Abandoned || _skipSteps >= SkipStepLimit)
        {
            _skipping = false;
        }
    }

    void SampleHeat(Snapshot snapshot)
    {
        _heatHome.AddSnapshot(snapshot, Side.Home);
        _heatAway.AddSnapshot(snapshot, Side.Away);
    }

    /// <summary>End card: the clips of the finished match, or null when there are none (or in a replay).</summary>
    public System.Collections.Generic.List<Moment>? PickHighlights()
    {
        if (_replay is null && Highlights.Pick(_match.Events.Events, _stepSeconds, _shotConfig) is { Count: > 0 } moments)
            _moments = moments;
        return _moments;
    }

    public void StartHighlights()
    {
        if (_hl is not null || _moments is not { Count: > 0 }) return;
        (_finalMatch, _finalPrev, _finalCurr, _finalCounted) = (_match, _prev, _curr, _countedEvents);
        Array.Copy(_live, _finalLive, _live.Length);
        Array.Clear(_live);
        _hl = _match = _newMatch();
        _prev = _curr = _hl.GetSnapshot();
        (_countedEvents, _hlIndex, _hlPlayed, _acc) = (0, 0, 0, 0);
        HighlightStarted?.Invoke();
    }

    public void EndHighlights()
    {
        if (_hl is null) return;
        _hl = null;
        (_match, _prev, _curr, _countedEvents) = (_finalMatch!, _finalPrev!, _finalCurr!, _finalCounted);
        Array.Copy(_finalLive, _live, _live.Length);
        _acc = 0;
        HighlightEnded?.Invoke();
    }

    void HighlightStep()
    {
        var commands = _finalMatch!.Commands;       // the player's and the opponent's substitutions/adjustments, applied at the same ticks
        while (_hl!.Commands.Count < commands.Count && commands[_hl.Commands.Count].Tick == _hl.Tick) _hl.Apply(commands[_hl.Commands.Count]);
        _hl.Step();
    }

    // Per frame: first run the clip's lead-in unseen (chunks like SkipChunk), then play start..end at 1x, then the next clip.
    void HighlightProcess(double delta)
    {
        var clip = _moments![_hlIndex];
        bool over() => _hl!.Status is MatchStatus.Finished or MatchStatus.Abandoned;
        try
        {
            if (_hl!.Tick < clip.StartTick)
            {
                for (int n = 0; n < SkipStepsPerFrame && _hl.Tick < clip.StartTick && !over(); n++) HighlightStep();
                _prev = _curr = _hl.GetSnapshot(); _acc = 0;
            }
            else
            {
                _acc += Math.Min(delta, 0.25);
                _hlPlayed += delta;
                while (_acc >= _stepSeconds && _hl.Tick < clip.EndTick && !over())
                {
                    _acc -= _stepSeconds;
                    HighlightStep();
                    _prev = _curr;
                    _curr = _hl.GetSnapshot();
                }
            }
            for (var events = _hl.Events.Events; _countedEvents < events.Count;) Tally(events[_countedEvents++]);
            if (_hl.Tick >= clip.EndTick || over())
            {
                var original = _finalMatch!.Events.Events;      // determinism guard: the re-simulation must equal the original up to here
                if (!original.Take(_hl.Events.Events.Count).SequenceEqual(_hl.Events.Events)) throw new InvalidOperationException("Highlight re-simulation differs from the match.");
                _hlIndex++;
                _hlPlayed = 0;
                if (_hlIndex >= _moments.Count) EndHighlights();
            }
        }
        catch (Exception error)
        {
            GD.PushError($"Highlights mislukt: {error.Message}");
            EndHighlights();
        }
    }

    /// <summary>One frame of match time; Main calls this from _Process before it draws.</summary>
    public void Advance(double delta)
    {
        if (_hl is not null)
        {
            _speed = 1;
            HighlightProcess(delta);
            return;
        }
        _speed = SettingsMenu.MatchSpeed;
        CheckReplayEnd();
        if (_skipping) SkipChunk();
        else if (!_paused)
        {
            _acc += Math.Min(delta, 0.25) * _speed;
            while (_acc >= _stepSeconds)
            {
                _acc -= _stepSeconds;
                StepMatch();
                _prev = _curr;
                _curr = _match.GetSnapshot();
                if (_curr.Tick % 10 == 0) SampleHeat(_curr);
                if (_pauseRequested && _match.IsRestMoment) { ReachRestMoment(); break; }
                ReadEvents();
                CheckReplayEnd();
                if (_replayVerified) break;
            }
        }
    }

    void ReadEvents()
    {
        var events = _match.Events.Events;
        while (_countedEvents < events.Count)
        {
            var e = events[_countedEvents++];
            EventRead?.Invoke(e);
            Tally(e);
            if (!_skipping) AudioManager.Event(e, _speed);
        }
        EventsRead?.Invoke();
    }

    void Tally(Zeus.Sim.Event e)
    {
        int slot = e.Type switch { EventType.Shot => 0, EventType.Pass => 2, EventType.BallControl => 4, EventType.Foul => 6, _ => -1 };
        if (slot >= 0) _live[slot + (e.Team == Side.Home ? 0 : 1)]++;
    }

    void CheckReplayEnd()
    {
        if (_replay is null || _replayVerified || _curr.Tick < _replay.Steps) return;
        _paused = true;
        _acc = 0;
        try { _replay.ApplyDue(_match); _replay.Verify(_match); }
        catch (InvalidOperationException error)
        {
            GD.PushError($"Replaycontrole mislukt: {error.Message}");
            GetParent()?.SetProcess(false);     // Main stops drawing and advancing, as its own SetProcess(false) did before the move
            GetTree().Quit(1);
            return;
        }
        _replayVerified = true;
        GD.Print($"Replay verified after {_curr.Tick} steps: {_match.EventHash()}");
    }
}
