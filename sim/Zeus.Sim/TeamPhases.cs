namespace Zeus.Sim;

public sealed record PhaseChange(Side Team, GamePhase From, GamePhase To, string Reason);

/// <summary>Deterministic, configurable v0 phase timing; not a calibrated football model.</summary>
public sealed class TeamPhases
{
    private readonly double _transitionSeconds;
    private readonly double _attackThirdThreshold;
    private Side _possession;
    private GamePhase _home;
    private GamePhase _away;
    private bool _stopped;
    private double? _transitionUntil;
    private double _lastTime;

    public TeamPhases(Side initialPossession = Side.Home, double transitionSeconds = 3,
        double attackThirdThreshold = .66)
    {
        if (!Enum.IsDefined(initialPossession)) throw new ArgumentException("Unknown initial possession.");
        if (!double.IsFinite(transitionSeconds) || transitionSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(transitionSeconds));
        if (!double.IsFinite(attackThirdThreshold) || attackThirdThreshold < 0 || attackThirdThreshold > 1)
            throw new ArgumentOutOfRangeException(nameof(attackThirdThreshold));
        _transitionSeconds = transitionSeconds;
        _attackThirdThreshold = attackThirdThreshold;
        _possession = initialPossession;
        _home = initialPossession == Side.Home ? GamePhase.Opbouw : GamePhase.Verdedigen;
        _away = initialPossession == Side.Away ? GamePhase.Opbouw : GamePhase.Verdedigen;
    }

    public GamePhase PhaseOf(Side team) => team switch {
        Side.Home => _home,
        Side.Away => _away,
        _ => throw new ArgumentException("Unknown phase team.", nameof(team))
    };

    /// <summary>
    /// ballHomeRelativeX is 0 at Home's own goal and 1 at Home's attacked goal, irrespective of world direction.
    /// timeSeconds is monotonic elapsed simulation time; equal timestamps are permitted.
    /// A stoppage cancels the transition window; resumption selects the current possession's baseline.
    /// </summary>
    public IReadOnlyList<PhaseChange> Update(Side possession, bool stopped, double ballHomeRelativeX,
        double timeSeconds)
    {
        if (!Enum.IsDefined(possession)) throw new ArgumentException("Unknown possession team.", nameof(possession));
        if (!double.IsFinite(ballHomeRelativeX) || ballHomeRelativeX < 0 || ballHomeRelativeX > 1)
            throw new ArgumentOutOfRangeException(nameof(ballHomeRelativeX));
        if (!double.IsFinite(timeSeconds) || timeSeconds < _lastTime)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), "Simulation time must be finite and monotonic.");
        bool changedPossession = possession != _possession;
        bool resumed = _stopped && !stopped;
        if (changedPossession && !stopped && !resumed && !double.IsFinite(timeSeconds + _transitionSeconds))
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), "Transition expiry must remain finite.");

        string reason;
        if (stopped)
        {
            _transitionUntil = null;
            reason = "stopped";
        }
        else if (resumed)
        {
            _transitionUntil = null;
            reason = "resumed";
        }
        else if (changedPossession)
        {
            _transitionUntil = _transitionSeconds > 0 ? timeSeconds + _transitionSeconds : null;
            reason = "possession_changed";
        }
        else if (_transitionUntil is { } expiry && timeSeconds >= expiry)
        {
            _transitionUntil = null;
            reason = "transition_expired";
        }
        else reason = "ball_zone";

        var home = Desired(Side.Home);
        var away = Desired(Side.Away);
        var changes = new List<PhaseChange>(2);
        if (_home != home) changes.Add(new(Side.Home, _home, home, reason));
        if (_away != away) changes.Add(new(Side.Away, _away, away, reason));
        _home = home;
        _away = away;
        _possession = possession;
        _stopped = stopped;
        _lastTime = timeSeconds;
        return changes.AsReadOnly();

        GamePhase Desired(Side team)
        {
            if (stopped) return GamePhase.Stilstaand;
            if (_transitionUntil is not null)
                return team == possession ? GamePhase.Balwinst : GamePhase.Balverlies;
            if (team != possession) return GamePhase.Verdedigen;
            double attackingX = team == Side.Home ? ballHomeRelativeX : 1 - ballHomeRelativeX;
            return attackingX >= _attackThirdThreshold ? GamePhase.Aanval : GamePhase.Opbouw;
        }
    }
}
