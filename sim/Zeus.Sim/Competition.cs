using System.Text.Json;

namespace Zeus.Sim;

// PRD fase 10, dunne carriere. ALLES HIERIN IS VOORLOPIG (v0, werkhypothese): clubs, niveaus, promotie/degradatie en seeds
// kunnen nog veranderen. De clubnamen zijn verzonnen; er zijn bewust geen echte clubs of namen gebruikt.

/// <summary>A fictional club with a fixed tactic (a file name in tactics/). The player's club has no fixed tactic.</summary>
public sealed record CompetitionClub(string Name, string? TacticFile, bool IsPlayer = false);

/// <summary>One scheduled match: indices into the division's club list (0 = the player's club), in a numbered round (0-based).</summary>
public sealed record CompetitionFixture(int Round, int Home, int Away);

/// <summary>Played result of a fixture (home and away index as in the fixture).</summary>
public sealed record CompetitionResult(int Round, int Home, int Away, int HomeGoals, int AwayGoals);

public sealed record StandingRow(int Club, string Name, bool IsPlayer, int Played, int Won, int Drawn, int Lost,
    int GoalsFor, int GoalsAgainst, int Points)
{
    public int GoalDifference => GoalsFor - GoalsAgainst;
}

public enum SeasonOutcome { Stayed, Promoted, Relegated }

/// <summary>One finished season, kept in the career history.</summary>
public sealed record SeasonRecord(int Season, int Division, int Position, SeasonOutcome Outcome);

/// <summary>The player's own live score (home, away goals) of <see cref="Round"/>, waiting to be processed by <see cref="Competition.PlayRound"/>.</summary>
public sealed record PendingResult(int Round, int HomeGoals, int AwayGoals);

/// <summary>Serializable state of the career: the whole thing is this object (JSON via <see cref="ToJson"/> / <see cref="Parse"/>).</summary>
public sealed class SeasonState
{
    public int Version { get; set; } = 1;
    public int Season { get; set; } = 1;
    /// <summary>0 = amateur, 1 = halfprof, 2 = prof (index into <see cref="Competition.Divisions"/>).</summary>
    public int Division { get; set; }
    /// <summary>Number of completed rounds (0..10). A round is always completed as a whole.</summary>
    public int Round { get; set; }
    public string PlayerTactic { get; set; } = "laag_blok_442.json";
    public List<CompetitionResult> Results { get; set; } = new();
    public List<SeasonRecord> History { get; set; } = new();
    /// <summary>Score of a live match that has been played but not yet processed into <see cref="Results"/>; kept in the file so it survives a crash or Esc.</summary>
    public PendingResult? Pending { get; set; }

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Parses and validates a saved state; throws <see cref="FormatException"/> for anything that does not fit.</summary>
    public static SeasonState Parse(string json)
    {
        try { return ParseCore(json); }
        catch (FormatException) { throw; }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NullReferenceException or ArgumentException or KeyNotFoundException or OverflowException)
        {
            throw new FormatException("Ongeldig seizoensbestand: " + e.Message, e);
        }
    }

    static SeasonState ParseCore(string json)
    {
        SeasonState? s;
        try { s = JsonSerializer.Deserialize<SeasonState>(json, Options); }
        catch (JsonException e) { throw new FormatException("Ongeldig seizoensbestand: " + e.Message, e); }
        if (s is null) throw new FormatException("Leeg seizoensbestand.");
        if (s.Version != 1) throw new FormatException("Onbekende versie van het seizoensbestand.");
        if (s.Division < 0 || s.Division >= Competition.Divisions.Count) throw new FormatException("Onbekende divisie.");
        if (s.Season < 1) throw new FormatException("Ongeldig seizoen.");
        if (s.Round < 0 || s.Round > Competition.Rounds) throw new FormatException("Ongeldige speelronde.");
        if (string.IsNullOrWhiteSpace(s.PlayerTactic) || !TacticFileNames.IsBare(s.PlayerTactic)) throw new FormatException("Geen geldige tactiek gekozen.");
        s.Results ??= new(); s.History ??= new();
        if (s.Results.Count != s.Round * Competition.MatchesPerRound) throw new FormatException("Aantal uitslagen klopt niet met de speelronde.");
        foreach (var r in s.Results)
            if (r is null || r.Round < 0 || r.Round >= s.Round || r.Home is < 0 or >= Competition.TeamsPerDivision || r.Away is < 0 or >= Competition.TeamsPerDivision
                || r.Home == r.Away || r.HomeGoals < 0 || r.AwayGoals < 0)
                throw new FormatException("Ongeldige uitslag in het seizoensbestand.");
        for (int round = 0; round < s.Round; round++)       // every played round holds exactly its scheduled matches, each once
        {
            var expected = Competition.RoundFixtures(round).Select(f => (f.Home, f.Away)).OrderBy(x => x).ToList();
            var actual = s.Results.Where(r => r.Round == round).Select(r => (r.Home, r.Away)).OrderBy(x => x).ToList();
            if (!expected.SequenceEqual(actual)) throw new FormatException($"De uitslagen van ronde {round + 1} kloppen niet met het speelschema.");
        }
        foreach (var h in s.History)
            if (h is null || h.Season < 1 || h.Division < 0 || h.Division >= Competition.Divisions.Count
                || h.Position < 1 || h.Position > Competition.TeamsPerDivision || !Enum.IsDefined(h.Outcome))
                throw new FormatException("Ongeldige seizoensgeschiedenis in het seizoensbestand.");
        if (s.Pending is { } p)
        {
            if (p.HomeGoals < 0 || p.AwayGoals < 0 || p.Round < 0) throw new FormatException("Ongeldige uitslag in behandeling.");
            if (p.Round != s.Round || s.Round >= Competition.Rounds) s.Pending = null;       // stale: that round was processed already
        }
        return s;
    }
}

public sealed record SeasonSummary(int Position, SeasonOutcome Outcome, bool Champion);

public static class Competition
{
    public const int TeamsPerDivision = 6;
    public const int Rounds = (TeamsPerDivision - 1) * 2;               // double round robin: 10
    public const int MatchesPerRound = TeamsPerDivision / 2;
    public const int Promoted = 2, Relegated = 1;                          // v0 hypothesis
    public const string PlayerClubName = "FC Zeus";
    /// <summary>At most this many matches run at the same time (memory pressure on the dev PC).</summary>
    public const int MaxParallel = 3;

    public sealed record Division(string Name, string Level, IReadOnlyList<CompetitionClub> Opponents);

    public static readonly IReadOnlyList<Division> Divisions = new[]
    {
        new Division("Amateurklasse", "amateur", new[]
        {
            new CompetitionClub("VV Kleiduinen", "counter_532.json"), new CompetitionClub("SV Rietveld", "hoge_druk_433.json"),
            new CompetitionClub("DVC Zandbergen", "laag_blok_442.json"), new CompetitionClub("FC Mulderhoek", "man_om_man_433.json"),
            new CompetitionClub("SC Hoogeweide", "positiespel_4231.json"),
        }),
        new Division("Halfprofklasse", "halfprof", new[]
        {
            new CompetitionClub("FC Oostdijk", "positiespel_4231.json"), new CompetitionClub("VC Noorderbeek", "counter_532.json"),
            new CompetitionClub("HSV Stroomwijk", "hoge_druk_433.json"), new CompetitionClub("RKVV Bergzicht", "testcase_a_433.json"),
            new CompetitionClub("SV Kerkelanden", "laag_blok_442.json"),
        }),
        new Division("Profklasse", "prof", new[]
        {
            new CompetitionClub("FC Haverkamp", "hoge_druk_433.json"), new CompetitionClub("AFC Veldzicht", "positiespel_4231.json"),
            new CompetitionClub("Unitas Lindenburg", "man_om_man_433.json"), new CompetitionClub("FC Drostburg", "counter_532.json"),
            new CompetitionClub("SC Rijnoord", "laag_blok_442.json"),
        }),
    };

    /// <summary>The six clubs of a division; index 0 is always the player's club.</summary>
    public static IReadOnlyList<CompetitionClub> Clubs(int division) =>
        new[] { new CompetitionClub(PlayerClubName, null, true) }.Concat(Divisions[division].Opponents).ToArray();

    /// <summary>Round-robin schedule by the circle method: first half rounds 0..4, second half the same pairs with home and away swapped.</summary>
    public static IReadOnlyList<CompetitionFixture> Schedule()
    {
        var fixtures = new List<CompetitionFixture>();
        var ring = Enumerable.Range(0, TeamsPerDivision).ToArray();
        for (int round = 0; round < TeamsPerDivision - 1; round++)
        {
            for (int i = 0; i < MatchesPerRound; i++)
            {
                int a = ring[i], b = ring[TeamsPerDivision - 1 - i];
                // Alternate who is home in the first match so the home matches are spread evenly.
                bool flip = (i == 0 && round % 2 == 1) || (i > 0 && (round + i) % 2 == 1);
                fixtures.Add(flip ? new CompetitionFixture(round, b, a) : new CompetitionFixture(round, a, b));
            }
            // Rotate all but the first element one place.
            var last = ring[^1];
            for (int j = ring.Length - 1; j > 1; j--) ring[j] = ring[j - 1];
            ring[1] = last;
        }
        int half = TeamsPerDivision - 1;
        var second = fixtures.Select(f => new CompetitionFixture(f.Round + half, f.Away, f.Home)).ToList();
        fixtures.AddRange(second);
        return fixtures;
    }

    public static IReadOnlyList<CompetitionFixture> RoundFixtures(int round) => Schedule().Where(f => f.Round == round).ToList();

    /// <summary>The player's own fixture in a round.</summary>
    public static CompetitionFixture PlayerFixture(int round) => RoundFixtures(round).First(f => f.Home == 0 || f.Away == 0);

    /// <summary>Reproducible seed from season, round and pair (splitmix64-style mix). Never 0.</summary>
    public static ulong Seed(int season, int round, int home, int away)
    {
        ulong x = 0x9E3779B97F4A7C15UL;
        foreach (var v in new[] { season, round, home, away })
        {
            x += (ulong)(v + 1) * 0xBF58476D1CE4E5B9UL;
            x ^= x >> 30; x *= 0x94D049BB133111EBUL; x ^= x >> 27;
        }
        x &= 0x7FFFFFFFFFFFUL;                   // keep it well within what a UI SpinBox/double can hold
        return x == 0 ? 1 : x;
    }

    public static string DivisionName(int division) => Divisions[division].Name;
    public static string Level(int division) => Divisions[division].Level;

    /// <summary>The league table from the played results. Order: points, goal difference, goals for, then club name.</summary>
    public static IReadOnlyList<StandingRow> Standings(SeasonState state)
    {
        var clubs = Clubs(state.Division);
        var rows = clubs.Select((c, i) =>
        {
            int w = 0, d = 0, l = 0, gf = 0, ga = 0;
            foreach (var r in state.Results)
            {
                if (r.Home != i && r.Away != i) continue;
                int mine = r.Home == i ? r.HomeGoals : r.AwayGoals, theirs = r.Home == i ? r.AwayGoals : r.HomeGoals;
                gf += mine; ga += theirs;
                if (mine > theirs) w++; else if (mine == theirs) d++; else l++;
            }
            return new StandingRow(i, c.Name, c.IsPlayer, w + d + l, w, d, l, gf, ga, w * 3 + d);
        });
        return rows.OrderByDescending(r => r.Points).ThenByDescending(r => r.GoalDifference).ThenByDescending(r => r.GoalsFor)
            .ThenBy(r => r.Name, StringComparer.Ordinal).ToList();
    }

    public static bool IsFinished(SeasonState state) => state.Round >= Rounds;

    /// <summary>The player's next match, or null when the season is over.</summary>
    public static CompetitionFixture? NextPlayerFixture(SeasonState state) => IsFinished(state) ? null : PlayerFixture(state.Round);

    /// <summary>Settings for the live match of the next round: seed, tactics files and level are derived from the state.</summary>
    public sealed record LiveMatch(CompetitionFixture Fixture, string HomeTactic, string AwayTactic, string Level, ulong Seed, bool PlayerIsHome);

    public static LiveMatch? NextLiveMatch(SeasonState state)
    {
        if (NextPlayerFixture(state) is not { } f) return null;
        var clubs = Clubs(state.Division);
        return new LiveMatch(f, TacticOf(clubs[f.Home], state), TacticOf(clubs[f.Away], state), Level(state.Division),
            Seed(state.Season, f.Round, f.Home, f.Away), f.Home == 0);
    }

    static string TacticOf(CompetitionClub club, SeasonState state) => club.IsPlayer ? state.PlayerTactic : club.TacticFile!;

    /// <summary>
    /// Completes the next round: the player's match uses <paramref name="playerResult"/> (home, away goals, from a live match) when given,
    /// otherwise it is simulated too; all other matches are simulated with at most <see cref="MaxParallel"/> at the same time.
    /// The results are appended in schedule order, so the outcome never depends on thread timing.
    /// <paramref name="progress"/> gets the number of finished-match equivalents (0..matches simulated) from worker threads.
    /// </summary>
    public static void PlayRound(SeasonState state, string tacticsDirectory, string squadsDirectory, (int Home, int Away)? playerResult = null,
        CancellationToken token = default, Action<double>? progress = null, MatchConfig? config = null, int stepLimit = 150000)
    {
        if (IsFinished(state)) throw new InvalidOperationException("Het seizoen is afgelopen.");
        if (playerResult is null && state.Pending is { } pending && pending.Round == state.Round) playerResult = (pending.HomeGoals, pending.AwayGoals);
        var fixtures = RoundFixtures(state.Round);
        var clubs = Clubs(state.Division);
        var level = Level(state.Division);
        var goals = new (int Home, int Away)[fixtures.Count];
        var fractions = new double[fixtures.Count];
        double reported = 0;
        void Report() { if (progress is null) return; lock (fractions) { reported = Math.Max(reported, fractions.Sum()); progress(reported); } } // non-decreasing, called under the lock: the callback must still be thread-safe (any worker thread)
        var cache = new Dictionary<string, Tactic>();
        Tactic Load(string file)
        {
            lock (cache) { if (!cache.TryGetValue(file, out var t)) cache[file] = t = TacticLoader.LoadFile(Path.Combine(tacticsDirectory, file)); return t; }
        }
        ParallelErrors.For(fixtures.Count, new ParallelOptions { MaxDegreeOfParallelism = MaxParallel, CancellationToken = token }, i =>
        {
            var f = fixtures[i];
            if (playerResult is { } given && (f.Home == 0 || f.Away == 0)) { goals[i] = given; lock (fractions) fractions[i] = 1; return; }
            var home = Load(TacticOf(clubs[f.Home], state)); var away = Load(TacticOf(clubs[f.Away], state));
            var match = Match.Create(config ?? new MatchConfig(), home, away, Seed(state.Season, f.Round, f.Home, f.Away),
                homeSquad: SquadFactory.ForLevel(level, home, squadsDirectory), awaySquad: SquadFactory.ForLevel(level, away, squadsDirectory));
            int steps = 0;
            while (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && steps < stepLimit)
            {
                match.Step();
                if (++steps % 256 != 0) continue;
                token.ThrowIfCancellationRequested();
                lock (fractions) fractions[i] = Math.Min(0.99, match.TimeSeconds / ((config ?? new MatchConfig()).HalfSeconds * 2));
                Report();
            }
            if (match.Status != MatchStatus.Finished)
                throw new InvalidOperationException($"Wedstrijd {clubs[f.Home].Name} - {clubs[f.Away].Name} werd niet afgerond ({match.Status}).");
            var stats = MatchStatistics.FromMatch(match);
            goals[i] = (stats.Home.Goals, stats.Away.Goals);
            lock (fractions) fractions[i] = 1;
            Report();
        });
        token.ThrowIfCancellationRequested();
        for (int i = 0; i < fixtures.Count; i++)
            state.Results.Add(new CompetitionResult(fixtures[i].Round, fixtures[i].Home, fixtures[i].Away, goals[i].Home, goals[i].Away));
        state.Round++;
        state.Pending = null;                    // the pending live score (if any) is now part of Results
    }

    /// <summary>Simulates every remaining round of the season. <paramref name="roundDone"/> is called after each completed round (from the calling thread).</summary>
    public static void PlayRest(SeasonState state, string tacticsDirectory, string squadsDirectory, CancellationToken token = default,
        Action<int>? roundDone = null, Action<double>? progress = null, MatchConfig? config = null)
    {
        while (!IsFinished(state))
        {
            token.ThrowIfCancellationRequested();
            int round = state.Round;
            PlayRound(state, tacticsDirectory, squadsDirectory, null, token, progress is null ? null : p => progress(round + p / MatchesPerRound), config);
            roundDone?.Invoke(state.Round);
        }
    }

    /// <summary>Position, promotion/relegation outcome of a finished season (v0: top 2 up, bottom 1 down, except at the edges).</summary>
    public static SeasonSummary Summarize(SeasonState state)
    {
        if (!IsFinished(state)) throw new InvalidOperationException("Het seizoen is nog niet afgelopen.");
        var table = Standings(state);
        int position = table.ToList().FindIndex(r => r.IsPlayer) + 1;
        var outcome = SeasonOutcome.Stayed;
        if (position <= Promoted && state.Division < Divisions.Count - 1) outcome = SeasonOutcome.Promoted;
        else if (position > TeamsPerDivision - Relegated && state.Division > 0) outcome = SeasonOutcome.Relegated;
        return new SeasonSummary(position, outcome, position == 1);
    }

    /// <summary>Closes the finished season (history) and starts the next one in the right division. The chosen tactic is kept.</summary>
    public static SeasonSummary StartNextSeason(SeasonState state)
    {
        var summary = Summarize(state);
        state.History.Add(new SeasonRecord(state.Season, state.Division, summary.Position, summary.Outcome));
        state.Division += summary.Outcome switch { SeasonOutcome.Promoted => 1, SeasonOutcome.Relegated => -1, _ => 0 };
        state.Season++;
        state.Round = 0;
        state.Results = new();
        return summary;
    }
}
