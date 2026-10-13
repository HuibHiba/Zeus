namespace Zeus.Sim;

public sealed partial class Match
{
    /// <summary>Current immutable energy value, or null for an unknown player.</summary>
    public FatigueState? FatigueOf(Side team, string id) =>
        _players.FirstOrDefault(p => p.Team == team && p.Id == id)?.Energy;

    private void UpdateFatigue(bool resting = false)
    {
        if (!_config.EnableFatigue) return;
        foreach (var player in _players)
        {
            if (player.SentOff) continue;
            player.Energy = Fatigue.Step(player.Energy, resting ? 0 : player.Body.Vel.Length,
                !resting && player.Intent?.Kind == IntentKind.Press, _config.Endurance,
                _config.StepSeconds, _config.FatigueSettings);
        }
    }

    private PlayerProfile EffectiveProfile(Player player) => _config.EnableFatigue
        ? Fatigue.Apply(_config.PlayerProfile, player.Energy, _config.FatigueSettings)
        : _config.PlayerProfile;

    private long DecisionFatigueTicks(Player player) => !_config.EnableFatigue ? 0
        : (long)Math.Round(Fatigue.ReactionExtra(player.Energy, _config.FatigueSettings)
            / _config.StepSeconds, MidpointRounding.AwayFromZero);

    private void RecoverHalfTimeFatigue()
    {
        if (!_config.EnableFatigue) return;
        foreach (var player in _players)
            if (!player.SentOff)
                player.Energy = new FatigueState(player.Energy.LongEnergy, player.Energy.LongEnergy);
    }
}
