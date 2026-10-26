namespace Zeus.Sim;

/// <summary>One cup match (club names, since guests come from another division); the winner goes through.</summary>
public sealed record CupResult(int Round, string Home, string Away, int HomeGoals, int AwayGoals, string Winner);

/// <summary>
/// Career v1 cup: a knock-out of eight clubs, the player's division plus two guests from the next division up (or down, at the
/// top), drawn with a seed per season. Three rounds, due after league rounds 3, 6 and 8, simulated only (no live play in v1).
/// A draw is decided like a penalty shoot-out would be, by a seeded coin (v1). Everyone plays at the player's division level.
/// </summary>
public static class Cup
{
    public const int Teams = 8, Rounds = 3;
    /// <summary>Cup round k is due once this many league rounds are completed.</summary>
    public static readonly int[] DueAfterLeagueRound = { 3, 6, 8 };
    const int SeedRound = 20;      // league seeds use rounds 0..9

    public static IReadOnlyList<CompetitionClub> Clubs(int division)
    {
        int guests = division + 1 < Competition.Divisions.Count ? division + 1 : division - 1;
        return Competition.Clubs(division).Concat(Competition.Divisions[guests].Opponents.Take(Teams - Competition.TeamsPerDivision)).ToList();
    }

    /// <summary>The bracket order of the season: a seeded shuffle; pairs are (0,1), (2,3), (4,5), (6,7), the first of each pair at home.</summary>
    public static IReadOnlyList<CompetitionClub> Draw(int season, int division)
    {
        var clubs = Clubs(division).ToArray();
        var rng = new Rng(Competition.Seed(season, SeedRound, 0, 0));
        for (int i = clubs.Length - 1; i > 0; i--)
        {
            int j = (int)rng.NextUInt((uint)(i + 1));
            (clubs[i], clubs[j]) = (clubs[j], clubs[i]);
        }
        return clubs;
    }

    public static int RoundsPlayed(SeasonState state) => state.Cup is { Count: > 0 } cup ? cup.Max(r => r.Round) + 1 : 0;

    public static bool Due(SeasonState state) => !Competition.IsFinished(state) && RoundsPlayed(state) < Rounds && state.Round >= DueAfterLeagueRound[RoundsPlayed(state)];

    /// <summary>The pairs of a cup round: the draw for the first, the winners of the previous round (in bracket order) after that.</summary>
    public static IReadOnlyList<(CompetitionClub Home, CompetitionClub Away)> Pairs(SeasonState state, int round)
    {
        var draw = Draw(state.Season, state.Division).ToList();
        IReadOnlyList<CompetitionClub> entrants = round == 0 ? draw
            : (state.Cup ?? new()).Where(r => r.Round == round - 1).OrderBy(r => draw.FindIndex(c => c.Name == r.Home))
                .Select(r => draw.First(c => c.Name == r.Winner)).ToList();
        var pairs = new List<(CompetitionClub, CompetitionClub)>();
        for (int i = 0; i + 1 < entrants.Count; i += 2) pairs.Add((entrants[i], entrants[i + 1]));
        return pairs;
    }

    /// <summary>The player's match of the due cup round, or null when nothing is due or the player is out.</summary>
    public static (CompetitionClub Home, CompetitionClub Away)? PlayerMatch(SeasonState state)
    {
        if (!Due(state)) return null;
        foreach (var pair in Pairs(state, RoundsPlayed(state)))
            if (pair.Home.IsPlayer || pair.Away.IsPlayer) return pair;
        return null;
    }

    public static int PlayerWins(SeasonState state) => (state.Cup ?? new()).Count(r => r.Winner == Competition.PlayerClubName);

    public static bool PlayerOut(SeasonState state) =>
        (state.Cup ?? new()).Any(r => (r.Home == Competition.PlayerClubName || r.Away == Competition.PlayerClubName) && r.Winner != Competition.PlayerClubName);

    public static string? Winner(SeasonState state) => RoundsPlayed(state) == Rounds ? state.Cup!.First(r => r.Round == Rounds - 1).Winner : null;

    /// <summary>Plays the due cup round (all its matches, at most <see cref="Competition.MaxParallel"/> at once) and appends the results in bracket order.</summary>
    public static void PlayRound(SeasonState state, string tacticsDirectory, string squadsDirectory, CancellationToken token = default,
        Action<double>? progress = null, MatchConfig? config = null, string? ownTacticsDirectory = null, int stepLimit = 150000)
    {
        if (!Due(state)) throw new InvalidOperationException("Er is geen bekerronde aan de beurt.");
        int round = RoundsPlayed(state);
        var pairs = Pairs(state, round);
        var draw = Draw(state.Season, state.Division).ToList();
        var level = Competition.Level(state.Division);
        var matchConfig = config ?? new MatchConfig();
        var results = new CupResult[pairs.Count];
        var fractions = new double[pairs.Count];
        var redCarded = new List<string>();      // the player's men sent off in the cup: suspended for the next league round
        void Report() { if (progress is null) return; lock (fractions) progress(fractions.Sum()); }
        Tactic Load(CompetitionClub club) => TacticLoader.LoadFile(TacticCatalog.Resolve(club.IsPlayer ? state.PlayerTactic : club.TacticFile!, tacticsDirectory, ownTacticsDirectory));
        ParallelErrors.For(pairs.Count, new ParallelOptions { MaxDegreeOfParallelism = Competition.MaxParallel, CancellationToken = token }, i =>
        {
            var (homeClub, awayClub) = pairs[i];
            var home = Load(homeClub); var away = Load(awayClub);
            Squad? SquadOf(CompetitionClub club, Tactic tactic) => club.IsPlayer ? Competition.PlayerSquad(state, tactic, squadsDirectory) : SquadFactory.ForLevel(level, tactic, squadsDirectory);
            ulong seed = Competition.Seed(state.Season, SeedRound + 1 + round, draw.IndexOf(homeClub), draw.IndexOf(awayClub));
            var match = Match.Create(matchConfig, home, away, seed, homeSquad: SquadOf(homeClub, home), awaySquad: SquadOf(awayClub, away));
            int steps = 0;
            while (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && steps < stepLimit)
            {
                match.Step();
                if (++steps % 256 != 0) continue;
                token.ThrowIfCancellationRequested();
                lock (fractions) fractions[i] = Math.Min(0.99, match.TimeSeconds / (matchConfig.HalfSeconds * 2));
                Report();
            }
            if (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned))
                throw new InvalidOperationException($"Bekerwedstrijd {homeClub.Name} - {awayClub.Name} werd niet afgerond ({match.Status}).");
            var (hg, ag) = MatchStatistics.FromMatch(match).AwardedGoals();
            if (homeClub.IsPlayer || awayClub.IsPlayer)
            {
                var own = SquadOf(homeClub.IsPlayer ? homeClub : awayClub, homeClub.IsPlayer ? home : away);
                if (own is not null) lock (redCarded) redCarded.AddRange(Competition.RedCardedNames(match, homeClub.IsPlayer ? Side.Home : Side.Away, own));
            }
            // A draw goes to a seeded penalty shoot-out on attributes (Shootout); the squads are the ones that played.
            string winner = hg > ag ? homeClub.Name : ag > hg ? awayClub.Name
                : Shootout.Play(SquadOf(homeClub, home) ?? Squad.NeutralFor(home), SquadOf(awayClub, away) ?? Squad.NeutralFor(away), seed).Winner == Side.Home ? homeClub.Name : awayClub.Name;
            results[i] = new CupResult(round, homeClub.Name, awayClub.Name, hg, ag, winner);
            lock (fractions) fractions[i] = 1;
            Report();
        });
        token.ThrowIfCancellationRequested();
        (state.Cup ??= new()).AddRange(results);
        if (redCarded.Count > 0) state.Absences = Absences.Suspend(state.Absences ?? new(), redCarded);
    }

    public static bool IsValid(SeasonState state)
    {
        var cup = state.Cup;
        if (cup is null) return true;
        if (state.Division < 0 || state.Division >= Competition.Divisions.Count) return false;
        var names = Clubs(state.Division).Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        for (int round = 0; round < Rounds; round++)
        {
            int count = cup.Count(r => r is not null && r.Round == round);
            if (count != 0 && count != Teams >> (round + 1)) return false;
            if (count == 0 && cup.Any(r => r is not null && r.Round > round)) return false;
        }
        return cup.All(r => r is not null && r.Round is >= 0 and < Rounds && names.Contains(r.Home) && names.Contains(r.Away) && r.Home != r.Away
            && r.HomeGoals >= 0 && r.AwayGoals >= 0 && (r.Winner == r.Home || r.Winner == r.Away));
    }
}
