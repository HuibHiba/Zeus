// A challenge that is played live, one match at a time (Challenges starts it, Main plays and records each match, Challenges judges it).
using System.Collections.Generic;
using Zeus.Sim;

public sealed class ChallengeRun
{
    public ChallengeSpec Spec { get; }
    public string PlayerPath { get; }
    public string PlayerLevel { get; }
    public string OpponentPath { get; }
    public string PlayerName { get; }
    public OpponentPlan? Plan { get; }
    public HistoryEntry Played { get; }
    public List<ChallengeMatchResult> Results { get; } = new();
    /// <summary>True from the moment a match is started until Main has recorded its result (so leaving a match early records nothing).</summary>
    public bool InMatch { get; private set; }

    public ChallengeRun(ChallengeSpec spec, string playerPath, string playerLevel, string opponentPath, string playerName, OpponentPlan? plan, HistoryEntry played)
    {
        Spec = spec; PlayerPath = playerPath; PlayerLevel = playerLevel; OpponentPath = opponentPath; PlayerName = playerName; Plan = plan; Played = played;
    }

    public bool Done => Results.Count >= Spec.Matches;
    /// <summary>Seed of the match to play next: 1..N, the same seeds as the simulation.</summary>
    public ulong NextSeed => (ulong)Results.Count + 1;

    /// <summary>Sets the shared match fields for the next match and marks it as started.</summary>
    public void Prepare()
    {
        GameSession.StashMenuChoices();
        GameSession.HomePath = PlayerPath; GameSession.HomeLevel = PlayerLevel;
        GameSession.AwayPath = OpponentPath; GameSession.AwayLevel = Spec.OpponentLevel;
        GameSession.Seed = NextSeed;
        GameSession.InCompetition = false;
        GameSession.PlayerSide = Side.Home;
        GameSession.Challenge = this;
        InMatch = true;
    }

    /// <summary>Called by Main when the match ends. Only the first call per started match counts.</summary>
    public void Record(ChallengeMatchResult result)
    {
        if (!InMatch || Done) return;
        InMatch = false;
        Results.Add(result);
    }

    public void Abort() => GameSession.Challenge = null;
}
