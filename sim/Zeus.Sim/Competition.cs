using System.Text.Json;
using System.Text.Json.Serialization;

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

/// <summary>What the club expects of a season, from its squad strength against the rest of the division (v0).</summary>
public enum SeasonGoal { Promotion, TopHalf, Survival }

/// <summary>One finished season, kept in the career history.</summary>
public sealed record SeasonRecord(int Season, int Division, int Position, SeasonOutcome Outcome, int CupWins = 0);     // CupWins: cup rounds won (3 = winner); 0 in older saves

/// <summary>The player's own live score (home, away goals) of <see cref="Round"/>, waiting to be processed by <see cref="Competition.PlayRound"/>.</summary>
public sealed record PendingResult(int Round, int HomeGoals, int AwayGoals,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<string>? RedCards = null);     // names of the player's men sent off live

/// <summary>Serializable state of the career: the whole thing is this object (JSON via <see cref="ToJson"/> / <see cref="Parse"/>).</summary>
public sealed class SeasonState
{
    public int Version { get; set; } = 2;
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

    public FamiliarityState? Familiarity { get; set; }
    public bool TrainedThisRound { get; set; }
    public TacticDemand? PendingDemand { get; set; }
    /// <summary>The club's goal for this season; null in older saves (then nothing is judged). Not written when null, so old files stay byte-identical.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SeasonGoal? Goal { get; set; }
    /// <summary>Your last matches as the opponent saw them (smart opponent v1); null in older saves and not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StyleSample>? StyleHistory { get; set; }
    /// <summary>Career v1: suspended and injured players of the player's club; null in older saves and not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Absence>? Absences { get; set; }
    /// <summary>Career v1: this season's cup results (see <see cref="Zeus.Sim.Cup"/>); null in older saves and not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CupResult>? Cup { get; set; }
    /// <summary>Career v1: players signed at the end of earlier seasons (see <see cref="Zeus.Sim.Signings"/>); null in older saves.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SquadPlayer>? Signings { get; set; }
    /// <summary>The season whose end-of-season offer was answered (picked or declined); 0 = not yet.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LastSigningSeason { get; set; }
    /// <summary>Career v1: attribute delta per player name from growth and ageing (see <see cref="Zeus.Sim.Development"/>); null in older saves.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, double>? Development { get; set; }
    /// <summary>Career v1: names of players who retired (36 and older at a season end); a youth player takes each one's place.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Retired { get; set; }

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
        if (s.Version is not (1 or 2)) throw new FormatException("Onbekende versie van het seizoensbestand.");
        if (s.Division < 0 || s.Division >= Competition.Divisions.Count) throw new FormatException("Onbekende divisie.");
        if (s.Season < 1) throw new FormatException("Ongeldig seizoen.");
        if (s.Round < 0 || s.Round > Competition.Rounds) throw new FormatException("Ongeldige speelronde.");
        if (string.IsNullOrWhiteSpace(s.PlayerTactic) || !TacticFileNames.IsReference(s.PlayerTactic)) throw new FormatException("Geen geldige tactiek gekozen.");
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
        if (s.Goal is { } goal && !Enum.IsDefined(goal)) throw new FormatException("Onbekend seizoensdoel.");
        if (s.StyleHistory is { } styles && (styles.Count > PlayStyleProfile.MaxKept || styles.Any(x => x is null
            || !double.IsFinite(x.Pressing) || !double.IsFinite(x.LineHeight) || !double.IsFinite(x.Tempo)
            || x.Pressing is < 0 or > 1 || x.LineHeight is < 0 or > 1 || x.Tempo is < 0 or > 1
            || x.Possession is { } share && (!double.IsFinite(share) || share is < 0 or > 1))))
            throw new FormatException("Ongeldige stijlgeschiedenis in het seizoensbestand.");
        if (s.Absences is { } absences && absences.Any(a => !Zeus.Sim.Absences.IsValid(a)))
            throw new FormatException("Ongeldige afwezigheid in het seizoensbestand.");
        if (s.Pending?.RedCards is { } reds && reds.Any(string.IsNullOrWhiteSpace)) throw new FormatException("Ongeldige rode kaart bij uitslag.");
        if (!Zeus.Sim.Cup.IsValid(s)) throw new FormatException("Ongeldige bekeruitslagen in het seizoensbestand.");
        if (s.Signings is { } signings && (signings.Count > 3 * s.Season || signings.Any(p => !Zeus.Sim.Signings.IsValid(p))))
            throw new FormatException("Ongeldige versterking in het seizoensbestand.");
        if (s.LastSigningSeason < 0 || s.LastSigningSeason > s.Season) throw new FormatException("Ongeldig versterkingsseizoen.");
        if (s.Development is { } development && !Zeus.Sim.Development.IsValid(development)) throw new FormatException("Ongeldige ontwikkeling in het seizoensbestand.");
        if (s.Retired is { } retired && retired.Any(string.IsNullOrWhiteSpace)) throw new FormatException("Ongeldige pensioenlijst in het seizoensbestand.");
        if (s.History.Any(h => h.CupWins is < 0 or > Zeus.Sim.Cup.Rounds)) throw new FormatException("Ongeldige bekergeschiedenis.");
        s.Version = 2;
        s.Familiarity?.Validate();
        if (s.Familiarity is { } familiarity && (familiarity.TeamId != Competition.PlayerClubName
            || familiarity.Period != (s.Season - 1) * Competition.Rounds + s.Round))
            throw new FormatException("Vertrouwdheid hoort niet bij dit team of deze ronde.");
        if (s.Pending is null) s.PendingDemand = null;
        if (s.PendingDemand is { } demand)
        {
            if (!double.IsFinite(demand.Complexity) || demand.Complexity is < 0 or > 1
                || TacticalConcepts.All.Any(c => !demand.Weights.ContainsKey(c) || !double.IsFinite(demand[c]) || demand[c] is < 0 or > 1))
                throw new FormatException("Ongeldige tactiekeis bij uitslag.");
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
    public const string PlayerClubName = "FC Zeus";      // internal id (familiarity, table order); the player's own club name is display only
    public const int MaxClubName = 24;
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

    public static LiveMatch? NextLiveMatch(SeasonState state, bool adaptive = false)
    {
        if (NextPlayerFixture(state) is not { } f) return null;
        var clubs = Clubs(state.Division);
        var adaptation = adaptive ? Adaptation(state) : null;
        return new LiveMatch(f, TacticOf(clubs[f.Home], state, adaptation), TacticOf(clubs[f.Away], state, adaptation), Level(state.Division),
            Seed(state.Season, f.Round, f.Home, f.Away), f.Home == 0);
    }

    /// <summary>Career-local state. Slot identities remain the provisional player identities across tactics.</summary>
    public static FamiliarityState EnsureFamiliarity(SeasonState state, Squad squad)
    {
        state.Familiarity ??= FamiliarityState.ForTeam(PlayerClubName, [], .0);
        int period = (state.Season - 1) * Rounds + state.Round;
        if (state.Familiarity.Period < period) state.Familiarity.BeginPeriod(period);
        foreach (var player in squad.Starters.Concat(squad.Bench))
            state.Familiarity.Players.TryAdd(player.Id, new FamiliarityPlayer {
                Concepts = TacticalConcepts.All.ToDictionary(c => c, _ => 0.0) });
        return state.Familiarity;
    }

    /// <summary>One collective training session per scheduled round, sharing the match learning budget.</summary>
    public static void Train(SeasonState state, string concept, double intensity)
    {
        if (IsFinished(state) || state.Pending is not null || state.TrainedThisRound || state.Familiarity is null)
            throw new InvalidOperationException("Training is nu niet beschikbaar.");
        FamiliarityState.Train(state.Familiarity, concept, intensity);
        state.TrainedThisRound = true;
    }

    /// <summary>AI clubs have a fixed reference of .7 at every provisional division level.
    /// They are exempt from familiarity penalties; this reference never enters Match.</summary>
    public static double AiFamiliarity(int division) { _ = Divisions[division]; return .7; }

    static string TacticOf(CompetitionClub club, SeasonState state, OpponentAdaptation? adaptation = null) =>
        club.IsPlayer ? state.PlayerTactic : adaptation?.TacticFile ?? club.TacticFile!;

    /// <summary>Amateur clubs never adapt, halfprof clubs in half of the rounds (deterministic: even season + round), prof clubs always.</summary>
    public static bool AdaptsAtLevel(string level, int season, int round) =>
        level == "prof" || (level == "halfprof" && (season + round) % 2 == 0);

    /// <summary>The opponent's counter for the player's next match: only after <see cref="PlayStyleProfile.Window"/> matches in the same style, and only at levels that adapt. Null otherwise.</summary>
    public static OpponentAdaptation? Adaptation(SeasonState state)
    {
        if (IsFinished(state) || !AdaptsAtLevel(Level(state.Division), state.Season, state.Round)) return null;
        var style = PlayStyleProfile.Detect(state.StyleHistory);
        return PlayStyleProfile.Counter(style) is { } c ? new OpponentAdaptation(style, c.TacticFile, c.ReasonKey) : null;
    }

    /// <summary>
    /// Completes the next round: the player's match uses <paramref name="playerResult"/> (home, away goals, from a live match) when given,
    /// otherwise it is simulated too; all other matches are simulated with at most <see cref="MaxParallel"/> at the same time.
    /// The results are appended in schedule order, so the outcome never depends on thread timing.
    /// <paramref name="progress"/> gets the number of finished-match equivalents (0..matches simulated) from worker threads.
    /// </summary>
    public static void PlayRound(SeasonState state, string tacticsDirectory, string squadsDirectory, (int Home, int Away)? playerResult = null,
        CancellationToken token = default, Action<double>? progress = null, MatchConfig? config = null, int stepLimit = 150000,
        bool opponentSubstitutions = false, string? ownTacticsDirectory = null, bool adaptiveOpponent = false, IEnumerable<string>? playerRedCards = null)
    {
        if (IsFinished(state)) throw new InvalidOperationException("Het seizoen is afgelopen.");
        var adaptation = adaptiveOpponent ? Adaptation(state) : null;
        MatchStatistics? playerStats = null;
        var redCarded = new List<string>(playerRedCards ?? Array.Empty<string>());      // names: given (live), pending (live, saved) or from the simulation below
        if (playerResult is null && state.Pending is { } pending && pending.Round == state.Round)
        {
            playerResult = (pending.HomeGoals, pending.AwayGoals);
            redCarded.AddRange(pending.RedCards ?? new());
        }
        var matchConfig = config ?? new MatchConfig(EnableFamiliarity: state.Familiarity is not null);
        var playerTactic = TacticLoader.LoadFile(TacticCatalog.Resolve(state.PlayerTactic, tacticsDirectory, ownTacticsDirectory));
        var demand = state.PendingDemand ?? TacticDemand.From(playerTactic);
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
            lock (cache) { if (!cache.TryGetValue(file, out var t)) cache[file] = t = TacticLoader.LoadFile(TacticCatalog.Resolve(file, tacticsDirectory, ownTacticsDirectory)); return t; }
        }
        var playerSquad = PlayerSquad(state, playerTactic, squadsDirectory);      // absent starters replaced from the bench (null at "neutraal")
        ParallelErrors.For(fixtures.Count, new ParallelOptions { MaxDegreeOfParallelism = MaxParallel, CancellationToken = token }, i =>
        {
            var f = fixtures[i];
            if (playerResult is { } given && (f.Home == 0 || f.Away == 0)) { goals[i] = given; lock (fractions) fractions[i] = 1; return; }
            var adapt = f.Home == 0 || f.Away == 0 ? adaptation : null;      // AI against AI keeps its own tactics
            var home = Load(TacticOf(clubs[f.Home], state, adapt)); var away = Load(TacticOf(clubs[f.Away], state, adapt));
            var fixtureConfig = matchConfig with { EnableFamiliarity = matchConfig.EnableFamiliarity && (f.Home == 0 || f.Away == 0) };
            var match = Match.Create(fixtureConfig, home, away, Seed(state.Season, f.Round, f.Home, f.Away),
                homeSquad: f.Home == 0 ? playerSquad : SquadFactory.ForLevel(level, home, squadsDirectory),
                awaySquad: f.Away == 0 ? playerSquad : SquadFactory.ForLevel(level, away, squadsDirectory),
                homeFamiliarity: f.Home == 0 ? state.Familiarity : null,
                awayFamiliarity: f.Away == 0 ? state.Familiarity : null);
            int steps = 0;
            var substitutions = opponentSubstitutions ? new OpponentSubstitutionPolicy() : null;
            while (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned) && steps < stepLimit)
            {
                if (match.IsRestMoment && substitutions is not null)
                {
                    if (!clubs[f.Home].IsPlayer) substitutions.TrySubstitute(match, Side.Home);
                    if (!clubs[f.Away].IsPlayer) substitutions.TrySubstitute(match, Side.Away);
                }
                match.Step();
                if (++steps % 256 != 0) continue;
                token.ThrowIfCancellationRequested();
                lock (fractions) fractions[i] = Math.Min(0.99, match.TimeSeconds / ((config ?? new MatchConfig()).HalfSeconds * 2));
                Report();
            }
            if (match.Status is not (MatchStatus.Finished or MatchStatus.Abandoned))
                throw new InvalidOperationException($"Wedstrijd {clubs[f.Home].Name} - {clubs[f.Away].Name} werd niet afgerond ({match.Status}).");
            var stats = MatchStatistics.FromMatch(match);
            goals[i] = stats.AwardedGoals();   // an abandoned match counts with its awarded result
            if (f.Home == 0 || f.Away == 0)
            {
                playerStats = stats;
                if (playerSquad is not null) lock (redCarded) redCarded.AddRange(RedCardedNames(match, f.Home == 0 ? Side.Home : Side.Away, playerSquad));
            }
            lock (fractions) fractions[i] = 1;
            Report();
        });
        token.ThrowIfCancellationRequested();
        for (int i = 0; i < fixtures.Count; i++)
            state.Results.Add(new CompetitionResult(fixtures[i].Round, fixtures[i].Home, fixtures[i].Away, goals[i].Home, goals[i].Away));
        if (matchConfig.EnableFamiliarity && state.Familiarity is { } familiarity)
        {
            FamiliarityState.AfterMatch(familiarity, demand);
            familiarity.BeginPeriod((state.Season - 1) * Rounds + state.Round + 1);
        }
        else if (state.Familiarity is { } disabled)
            disabled.BeginPeriod((state.Season - 1) * Rounds + state.Round + 1);
        // The opponent only learns from tactic settings and (when simulated) possession; a live match gives no statistics here.
        if (adaptiveOpponent)      // with the switch off the save stays byte-identical to the old format
        state.StyleHistory = (state.StyleHistory ?? new()).Append(PlayStyleProfile.Sample(playerTactic, playerStats, PlayerFixture(state.Round).Home == 0))
            .TakeLast(PlayStyleProfile.MaxKept).ToList();
        if (playerSquad is not null)          // suspensions and injuries for the next rounds, seeded by this round (a replay gives the same list)
            state.Absences = Absences.AfterRound(state.Absences ?? new(), redCarded, playerSquad.Starters.Select(s => s.Name),
                new Rng(Seed(state.Season, state.Round, TeamsPerDivision, TeamsPerDivision)));
        state.Round++;
        state.TrainedThisRound = false;
        state.PendingDemand = null;
        state.Pending = null;                    // the pending live score (if any) is now part of Results
    }

    /// <summary>The player's eleven for this round: the division's template fitted to his tactic, absent starters replaced from the bench.</summary>
    public static Squad? PlayerSquad(SeasonState state, Tactic playerTactic, string squadsDirectory)
    {
        var squad = SquadFactory.ForLevel(Level(state.Division), playerTactic, squadsDirectory);
        if (squad is null) return null;
        if (state.Signings is { Count: > 0 } signings) squad = Zeus.Sim.Signings.Apply(squad, signings);
        if (state.Retired is { Count: > 0 } retired) squad = Zeus.Sim.Development.WithYouth(squad, retired);
        if (state.Development is { Count: > 0 } development || state.Season > 1) squad = Zeus.Sim.Development.Apply(squad, state.Development ?? new(), state.Season - 1);
        return Absences.WithoutAbsent(squad, (state.Absences ?? new()).Select(a => a.Player));
    }

    /// <summary>Names of the squad players of <paramref name="side"/> sent off in this match (red or second yellow), looked up by slot id.</summary>
    public static IEnumerable<string> RedCardedNames(Match match, Side side, Squad squad) =>
        match.Events.Events.Where(e => e.Type == EventType.Card && e.Team == side && e.Outcome is "Red" or "SecondYellowRed")
            .Select(e => squad.Starters.Concat(squad.Bench).FirstOrDefault(p => p.Id == e.PlayerId)?.Name)
            .OfType<string>();

    /// <summary>Simulates every remaining round of the season. <paramref name="roundDone"/> is called after each completed round (from the calling thread).</summary>
    public static void PlayRest(SeasonState state, string tacticsDirectory, string squadsDirectory, CancellationToken token = default,
        Action<int>? roundDone = null, Action<double>? progress = null, MatchConfig? config = null, bool opponentSubstitutions = false, string? ownTacticsDirectory = null,
        bool adaptiveOpponent = false)
    {
        while (!IsFinished(state))
        {
            token.ThrowIfCancellationRequested();
            int round = state.Round;
            PlayRound(state, tacticsDirectory, squadsDirectory, null, token, progress is null ? null : p => progress(round + p / MatchesPerRound), config,
                opponentSubstitutions: opponentSubstitutions, ownTacticsDirectory: ownTacticsDirectory, adaptiveOpponent: adaptiveOpponent);
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
    public static SeasonSummary StartNextSeason(SeasonState state, string? squadsDirectory = null)
    {
        var summary = Summarize(state);
        state.History.Add(new SeasonRecord(state.Season, state.Division, summary.Position, summary.Outcome, Zeus.Sim.Cup.PlayerWins(state)));
        state.Cup = null;                        // a fresh cup draw next season
        if (squadsDirectory is not null)        // growth and ageing of the squad that played this season (template of the division just played, plus signings)
        {
            var played = SquadLoader.LoadFile(Path.Combine(squadsDirectory, Level(state.Division) + ".json"));
            var everyone = Zeus.Sim.Development.WithYouth(played, state.Retired ?? new()).Starters.Concat(Zeus.Sim.Development.WithYouth(played, state.Retired ?? new()).Bench).Concat(state.Signings ?? new()).ToList();
            state.Development = Zeus.Sim.Development.AfterSeason(state.Development ?? new(), everyone, state.Season - 1);
            var retiring = Zeus.Sim.Development.Retiring(everyone, state.Season).Where(n => !(state.Retired ?? new()).Contains(n)).ToList();
            if (retiring.Count > 0) (state.Retired ??= new()).AddRange(retiring);      // they stop now; next season their youth successors play
        }
        state.Division += summary.Outcome switch { SeasonOutcome.Promoted => 1, SeasonOutcome.Relegated => -1, _ => 0 };
        state.Season++;
        state.Round = 0;
        state.Results = new();
        state.Goal = null;                       // the new season gets its own goal (see ExpectedGoal)
        return summary;
    }

    /// <summary>Squad strength for the season goal: the mean field attribute of the starters fitted to the tactic,
    /// where a starter outside his own template role counts for 90%.</summary>
    public static double SquadStrength(Squad template, Tactic tactic)
    {
        static double Mean(PlayerAttributes a) => new[] { a.Passing, a.FirstTouch, a.Finishing, a.Dribbling, a.Defending, a.Heading, a.Speed,
            a.Acceleration, a.Strength, a.Endurance, a.Vision, a.Anticipation, a.Positioning, a.Decisions, a.Discipline }.Average();
        return SquadFactory.Fit(template, tactic).Starters
            .Average(p => Mean(p.Attributes) * (template.Starters.First(t => t.Name == p.Name).Role == p.Role ? 1 : .9));
    }

    /// <summary>Goal from the strengths of the division's clubs (index 0 = the player's club): top third promotion, bottom third survival,
    /// the rest top half. Equal clubs share their places, so the club sits in the middle of its group.</summary>
    public static SeasonGoal GoalFor(IReadOnlyList<double> strengths)
    {
        double own = strengths[0];
        int stronger = strengths.Skip(1).Count(s => s > own + 1e-9), equal = strengths.Skip(1).Count(s => Math.Abs(s - own) <= 1e-9);
        int rank = 1 + stronger + equal / 2, third = strengths.Count / 3;
        return rank <= third ? SeasonGoal.Promotion : rank > strengths.Count - third ? SeasonGoal.Survival : SeasonGoal.TopHalf;
    }

    /// <summary>The goal the club sets for the current season of <paramref name="state"/> (division and the player's tactic).</summary>
    public static SeasonGoal ExpectedGoal(SeasonState state, string tacticsDirectory, string squadsDirectory, string? ownTacticsDirectory = null)
    {
        var template = SquadLoader.LoadFile(Path.Combine(squadsDirectory, Level(state.Division) + ".json"));
        return GoalFor(Clubs(state.Division).Select(c => SquadStrength(template,
            TacticLoader.LoadFile(TacticCatalog.Resolve(TacticOf(c, state), tacticsDirectory, ownTacticsDirectory)))).ToList());
    }

    /// <summary>Promotion = a promotion place (top 2, also in the top division), top half = top 3, survival = above the relegation place.</summary>
    public static bool GoalMet(SeasonGoal goal, int position) => goal switch
    {
        SeasonGoal.Promotion => position <= Promoted,
        SeasonGoal.TopHalf => position <= TeamsPerDivision / 2,
        _ => position <= TeamsPerDivision - Relegated,
    };

    /// <summary>The player's own club name: control characters removed, trimmed, at most <see cref="MaxClubName"/> characters; null when empty.</summary>
    public static string? CleanClubName(string? raw)
    {
        var name = new string((raw ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length > MaxClubName) name = name[..(char.IsHighSurrogate(name[MaxClubName - 1]) ? MaxClubName - 1 : MaxClubName)].TrimEnd();
        return name.Length == 0 ? null : name;
    }
}
