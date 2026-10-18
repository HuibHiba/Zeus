# Managerlaag: ontwerp (roadmap F0)

**Status: voorstel, wacht op beoordeling door de maker.** Opgesteld 2026-10-18 op branch `feat/managerlaag-ontwerp`. Uitwerking van [roadmap-naar-1.0.md](roadmap-naar-1.0.md) §1 besluiten 3, 11, 12, 17, 18, 19, 20, 22 en §4 werkstroom F (F0-F12). Rangorde: vault `Besluiten` boven dit document; dit document boven de losse F-taken; [competitie.md](competitie.md), [spelers.md](spelers.md) en [vertrouwdheid.md](vertrouwdheid.md) beschrijven wat er nu is. Alle getallen zonder bron zijn ontwerpaannames, te ijken in F7.

Leeswijzer voor de maker (20 minuten): §1 grenzen (3 min), §2 datamodel (8 min, alleen de tabelkoppen en defaults), §4 opslag (2 min), §7 bouwvolgorde (3 min), §8 open vragen (4 min). §3, §5 en §6 zijn voor de agents.

---

## 1. Grenzen

### 1.1 Wat de managerlaag is

Een dunne laag om de bestaande wedstrijd heen: een wereld van fictieve clubs, een selectie met zichtbare cijfers, een kleine staf, een bestuur dat een doel stelt en je kan ontslaan, pers die vragen stelt, fans die komen of wegblijven, en één transferbudget met vaste prijzen. Alles grijpt aan op wat er al is: `Competition` (sim/Zeus.Sim/Competition.cs), `Squad`/`SquadFactory` (Squad.cs, SquadFactory.cs), `FamiliarityState` (FamiliarityState.cs), de carrière-opslag in `user://zeus_carriere.json` (godot/Season.cs:14) en `ClubIdentity` (godot/ClubIdentity.cs).

Regel uit de roadmap (§8, risico "Managerlaag groeit uit tot FM"): elk onderdeel heeft een "niet bouwen"-lijst en G9 mag snoeien, niet toevoegen.

### 1.2 Niet bouwen, per onderdeel

| Onderdeel | Wel | Niet bouwen |
|---|---|---|
| Wereld (F9) | 4 fictieve landen, 2-3 divisies, beker per land, 1 Europees toernooi, kalender, promotie/degradatie | Meer dan 4 landen; nationale teams; een tweede Europees toernooi; echte clubs, spelers of competities (roadmap §9); play-offs; wedstrijden op datum-niveau (alleen weken) |
| Club | Identiteit, niveau, reputatie, stadiongroeistadium, fanvertrouwen, opkomst, transferbudget | Financiën (inkomsten, uitgaven, salarissen, schulden, sponsors, kaartverkoop); stadionbouw als keuze (besluit 18); beloopbare club (besluit 21); eigenaars of voorzitters als personen |
| Speler (F1) | Cijfers 1-20, leeftijd, ontwikkeling, veroudering, blessure, schorsing, moraal, vertrouwdheid, stoppen, instroom | Contracten, salarissen, looptijden, clausules, makelaars (besluit 3); jeugdopleiding en jeugdteams (besluit 19); persoonlijkheidstypen; relaties tussen spelers; nationale selecties; huurspelers |
| Transfers (F3) | Vaste prijs per speler, één budget per seizoen, 2 periodes, AI-clubs kopen en verkopen | Onderhandeling, biedingen, tegenbod, speler die weigert, salariseisen, huur, ruil, clausules |
| Staf (F10) | Assistent, scout, fysio als personen met niveau 1-20 en één effect elk; trainingsfocus per week | Meer stafrollen; stafsalarissen; FM-trainingsschema's met sessies per dag (besluit 19); individuele trainingsplannen; stafontwikkeling |
| Coach (F2, F12) | Naam, uiterlijk-indexen, reputatie 0-100, één stijl, 2-3 vaardigheden met kleine uitgelegde effecten, ervaring | Meerdere coaches per carrière (besluit 17); coachcontracten of salaris; coachlicenties; interviews met andere clubs; speler-coach |
| Bestuur (F2) | Vertrouwen 0-100 met reden per wijziging, seizoensdoel, waarschuwing, ontslag, ≥ 3 aanbiedingen | Bestuursverkiezingen, overnames, filosofie-eisen, budgetonderhandeling, "beloften" aan het bestuur |
| Media (F4) | Vragenbank, persconferentie voor en na de wedstrijd (3-4 vragen, 3 antwoorden), effect in punten op moraal, bestuur, fans | Vrije tekstinvoer; journalisten als personen; sociale media; krantenkoppen met eigen generator; reacties van spelers op uitspraken (alleen de moraalpunten) |
| Fans (F5) | Fanvertrouwen 0-100, opkomst %, koppeling aan geluid (E1) en stadion (D11) | Supportersgroepen, protesten, seizoenkaarten, prijzen, uitpubliek |
| Inbox (F6) | Berichtenlijst met type, week, tekst-sleutel, parameters; seizoensoverzicht; Europese resultaten | E-mailachtige conversaties; antwoorden op berichten; nieuws over andere clubs buiten uitslagen, transfers en ontslagen |
| Mods (F11) | Competitie- en clubdata als JSON in een modmap, tenues en emblemen als PNG, validatie met regelnummer | Mod-scripts of code; workshop of download; modding van de simulatieparameters (die blijven in `MatchConfig`); modding van spelersgedrag |

---

## 2. Datamodel

Alle identiteiten zijn stabiele tekst-id's (`land:noord`, `club:noord-0012`, `spl:000417`), gegenereerd uit de seed (§6). Selectie-id's in `Match` blijven tactiekslots (`GK`, `RB`, `B1`..`B7`; Squad.cs:33, 77); de koppeling persoon → slot gebeurt per wedstrijd in `ClubSquad.ToSquad` (§3.2). Kolom "bron": `gen` = generator uit seed, `sim` = uit wedstrijden, `speler` = invoer van de gebruiker, `regel` = afgeleid per week of seizoen, `mod` = overschrijfbaar via modmap (§5).

### 2.1 Wereld

| Veld | Type | Bereik / waarden | Default | Bron |
|---|---|---|---|---|
| `Seed` | ulong | ≠ 0 | uit de klok bij een nieuwe carrière, daarna vast | speler |
| `Countries[4]` | Country | precies 4 | | gen, mod |
| `Season` | int | ≥ 1 | 1 | regel |
| `Week` | int | 1..40 | 1 | regel |
| `Calendar` | lijst van CalendarWeek | 40 per seizoen | | regel |
| `Europe` | Tournament | 32 clubs, knock-out | | regel |
| `TransferWindows` | 2 × (van, tot) in weken | pre-seizoen weken 1-4; winter weken 20-23 | | regel |

Land (`Country`):

| Veld | Type | Bereik | Default | Bron |
|---|---|---|---|---|
| `Id`, `Name` | string | fictief, ≤ 24 tekens | | gen, mod |
| `IsHome` | bool | precies 1 land waar | | gen |
| `NameStyle` | enum | `noord`, `west`, `zuid`, `oost` (lettergreeptabellen, §6.2) | | gen, mod |
| `Divisions` | lijst Division | eigen land 3, andere 2 | | gen, mod |
| `Cup` | Tournament | knock-out, 1 wedstrijd per ronde | | regel |

Divisie (`Division`):

| Veld | Type | Bereik | Default | Bron |
|---|---|---|---|---|
| `Id`, `Name` | string | | | gen, mod |
| `Tier` | int | 1 (hoogste) .. 3 | | gen |
| `Level` | string | `prof` (tier 1), `halfprof` (tier 2), `amateur` (tier 3); in landen met 2 divisies tier 2 = `halfprof` | | gen |
| `ClubIds` | lijst | 18 bij tier 1, 16 bij tier 2 en 3 (besluit 12: 34 resp. 30 wedstrijden) | | gen, mod |
| `Promoted`, `Relegated` | int | tier 1: 0 / 3; tier 2: 3 / 3; laagste tier: 3 / 0 | | gen, mod |
| `Fixtures` | lijst CompetitionFixture | dubbele ronde, circle-method, zoals nu (Competition.cs:163-185) | | regel |
| `Results` | lijst CompetitionResult | zoals nu (Competition.cs:16) | | sim |
| `RatingBand` | (min, max) int | tier 1: 14-19, tier 2: 11-15, tier 3: 7-12 (gemiddelde van de 15 attributen per speler) | | gen |

Clubtelling: eigen land 18 + 16 + 16 = 50; drie andere landen 3 × (18 + 16) = 102; totaal **152** clubs, ongeveer 22 spelers per club ≈ **3350 spelers** (roadmap: ~150 en ~3000).

Kalender (40 weken): week 1-4 voorbereiding en transferperiode 1; week 5-38 competitie (34 speelronden, divisies met 30 ronden hebben 4 vrije weken); beker in de midweek van weken 6, 10, 14, 18, 26, 30 (6 ronden, bracket van 64 met vrijstellingen); Europees toernooi in de midweek van weken 8, 12, 16, 22, 24, 28, 32, 34 en 36 (5 ronden, heen en terug behalve de finale); week 20-23 transferperiode 2; week 39-40 seizoenseinde (ontwikkeling, veroudering, stoppen, instroom, promotie/degradatie, bestuursoordeel). Eén leerperiode voor vertrouwdheid = één week (`FamiliarityState.BeginPeriod`, FamiliarityState.cs:39; nu is een periode een speelronde, competitie.md "Vertrouwdheid in de competitie").

Europees toernooi: per land de top 8 van tier 1 van het vorige seizoen (4 × 8 = 32); seizoen 1 op clubreputatie. Beker: alle clubs van het land, bracket van 64 met deterministische vrijstellingen voor de hoogst gerangschikte clubs. Gelijkspel in een knock-outwedstrijd: beslissing door een deterministische trekking uit de seed (geen verlenging of strafschoppenserie in de sim; zie open vraag 5).

### 2.2 Club

| Veld | Type | Bereik | Default | Bron |
|---|---|---|---|---|
| `Id` | string | `club:<land>-<nnnn>` | | gen |
| `Name`, `ShortName` | string | ≤ 24 / ≤ 4 tekens, fictief, niet in de blokkeerlijst | | gen, mod, speler (eigen club) |
| `Kit1`, `Kit2` | int | index in `ClubIdentity.Kits` (10 kleuren, ClubIdentity.cs:9-13) | | gen, mod, speler |
| `Crest` | int | 0..7 (ClubIdentity.cs:16-17) | | gen, mod, speler |
| `KitPng`, `CrestPng` | string? | pad in de modmap (§5.4); null = tekenen zoals nu | null | mod |
| `CountryId`, `DivisionId` | string | | | gen, regel |
| `Level` | string | volgt de divisie | | regel |
| `Reputation` | int | 0..100 | uit divisie en startpositie: tier 1 55-85, tier 2 30-55, tier 3 10-30 | gen, regel |
| `StadiumStage` | int | 0..5 (hekjes, kleine tribune, tribune, overdekt, groot, top) = `clamp(Reputation / 17, 0, 5)`; geen keuze (besluit 18) | | regel |
| `Capacity` | int | 500 × 2^StadiumStage (500 .. 16 000) | | regel |
| `FanTrust` | int | 0..100 | 50 | regel |
| `Attendance` | int | % = `clamp(35 + 0,7 × FanTrust, 20, 100)` (F5: ≤ 20 → ≤ 49%, ≥ 80 → ≥ 91%) | | regel |
| `TransferBudget` | int | in k (duizendtallen), 0 .. 50 000; per seizoen opnieuw: tier 1 2 000-8 000, tier 2 300-1 500, tier 3 50-300, × (0,5 + Reputation / 100) | | regel |
| `TacticFile` | string | AI-tactiek uit `tactics/` (zoals `CompetitionClub.TacticFile`, Competition.cs:10) | | gen, mod |
| `PlayerIds` | lijst | 20..26 personen | | gen, regel |
| `StaffIds` | 3 × string | assistent, scout, fysio | | gen |
| `IsPlayer` | bool | precies 1 club | | speler |
| `Morale` | int | 0..100, teamwaarde | 50 | regel, media |

### 2.3 Speler

Zichtbaar: alle 15 attributen plus keeperblok als gehele getallen 1..20, zoals `squads/*.json` (schema/speler.schema.json, spelers.md "Bestanden"). Intern blijft de afbeelding `(n-1)/19` (Squad.cs:114).

| Veld | Type | Bereik | Default | Bron |
|---|---|---|---|---|
| `Id` | string | `spl:<nnnnnn>`, carrièrebreed uniek en stabiel | | gen |
| `Name` | string | fictief (§6.2) | | gen |
| `CountryId` | string | | | gen |
| `Role` | string | een van `SquadLoader.Roles` (Squad.cs:34) | | gen |
| `PreferredPosition` | string | een van `SquadLoader.Positions` (Squad.cs:33) | | gen |
| `Attributes` | 15 × int | 1..20 | uit de `RatingBand` van de divisie ± 3, rolprofiel zoals `generate_squads.py` regel 27 | gen |
| `Keeper` | 3 × int? | 1..20, alleen rol `keeper` | | gen |
| `Age` | int | 17..38 | | gen, regel (+1 per seizoen) |
| `Potential` | int | 1..20, verborgen; de scout toont een band (§2.4) | `max(gemiddelde, trekking)` | gen |
| `Injury` | int | resterende weken, 0 = fit | 0 | regel |
| `Suspension` | int | resterende competitiewedstrijden | 0 | regel |
| `Yellows` | int | geel in deze competitie; bij 5 → `Suspension = 1`, teller op 0 | 0 | sim |
| `Morale` | int | 0..100 | 50 | regel, media |
| `Familiarity` | dictionary concept → 0..1 | zeven concepten (`TacticalConcepts.All`) | 0 | sim (`FamiliarityState`, nu per slot, straks per persoon) |
| `Learned` | double | 0..0,12 per week (`FamiliarityState.PeriodLimit`, FamiliarityState.cs:11) | 0 | sim |
| `Value` | int | k, afgeleid, niet opgeslagen (§2.3.2) | | regel |

#### 2.3.1 Ontwikkeling, veroudering, blessure, schorsing (regels per seizoen of week)

- **Ontwikkeling** (week 39, per speler): `groei = clamp(Potential - gemiddelde, 0, 3)` punten verdeeld over de 3 laagste attributen van het rolprofiel bij leeftijd ≤ 23; 24-29: één attribuut ±1 (trekking); ≥ 30: `-1` op `speed`, `acceleration` of `endurance`, vanaf 33 `-2`. Trainingsfocus "individueel" (§2.4) verlegt de groei naar het gekozen attribuut. Alle trekkingen uit `Rng(seed, spelerindex + seizoen × 10 000)`; geen `System.Random`.
- **Stoppen**: vanaf 33 kans `0,15 × (leeftijd - 32)` per seizoen; bij 38 altijd. **Instroom**: elke club onder 20 spelers krijgt gegenereerde spelers van 17-19 jaar met `gemiddelde = RatingBand.min - 1` en `Potential` uit de band, tot 22.
- **Blessure** (na elke gespeelde wedstrijd, per speler die speelde): kans `0,02 × (2 - LongEnergy)` (energie uit `FatigueState`, Fatigue.cs:4); duur 1-8 weken, trekking met zwaartepunt bij 2; fysio verkort (§2.4). Geblesseerd = niet selecteerbaar; de UI toont weken. Geen blessure *in* de wedstrijd (niet in de sim).
- **Schorsing**: rood = 1 competitiewedstrijd (2 bij een tweede rood in het seizoen); 5 geel = 1 wedstrijd. Kaarten komen uit `TeamStatistics` (Statistics.cs:3), dus per team; per speler uit de `Card`-events in de eventlog van de eigen wedstrijd en uit het snelle model (§3.4) voor andere wedstrijden.
- **Moraal** (team en speler): +3 bij winst, -3 bij verlies, +1 bij basisplaats, -1 per week zonder minuten; mediapunten (§2.7) komen erbij; geen effect in de sim in v1 (open vraag 7).

#### 2.3.2 Vaste prijs (F3)

`Value = round(basis(tier) × 1,22^(gemiddelde - 10) × leeftijd(age))` in k, met `basis` = 40 (tier 1), 15 (tier 2), 5 (tier 3) van de verkopende club, `leeftijd` = 1,3 (≤ 22), 1,0 (23-29), 0,6 (30-32), 0,3 (≥ 33). Monotoon stijgend in het gemiddelde (F3-criterium). Kopen: `Value` van het budget af, speler wisselt direct van club, alleen in een transferperiode en als de koper ≤ 26 spelers heeft; verkopen: `0,8 × Value` erbij. AI-clubs: per transferperiode koopt elke club met minder dan 20 spelers of zonder keeper de goedkoopste passende speler die ze kunnen betalen; clubs met > 24 spelers verkopen de laagste. Concentratiegrens (F3): een club mag niet kopen als ze al 5 van de top-10 (op gemiddelde) van de divisie bezit.

### 2.4 Staf en training

| Veld | Type | Bereik | Default | Bron |
|---|---|---|---|---|
| `Id`, `Name`, `Age` | | zoals speler | | gen |
| `Role` | enum | `assistant`, `scout`, `physio` | | gen |
| `Level` | int | 1..20; bij de start tier 1 10-16, tier 2 7-12, tier 3 4-9 | | gen |
| `Price` | int | k, `Level × 10`; wisselen = vaste prijs uit het transferbudget, ≤ 3 klikken, geen salaris (F10) | | regel |

Effecten (één per rol, in één zin in het spel):

- **Assistent**: neemt de trainingsfocus over als de speler niets kiest (kiest het concept met het grootste tekort, zoals Season.cs:300-301) en vermenigvuldigt de trainingsgroei met `1 + 0,02 × (Level - 10,5)` (niveau 1 → 0,81; niveau 20 → 1,19; verschuiving ≥ 20%, F10).
- **Scout**: bepaalt hoeveel de TEST-tab (godot/Editor.Scout.cs) van de volgende tegenstander toont: niveau ≥ 1 de stijlregel; ≥ 8 ook de formatie; ≥ 13 ook de triggers; ≥ 17 ook de `Potential`-band (±2) van eigen en te kopen spelers.
- **Fysio**: blessureduur × `1,2 - 0,02 × Level` (niveau 1 → 1,18; niveau 20 → 0,80).

Trainingsfocus per week (vervangt de knop "Train" per ronde, Season.cs:305-313): `tactiek` (concept naar keuze, `FamiliarityState.Train` met intensiteit 1,0), `conditie` (`LongEnergy` van de hele selectie +0,05 extra herstel die week, geen leren), `standaardsituaties` (concept `laag_blok` 0,5 plus een vaste +2% op de kans dat een hoekschopvariant uit B6 wordt uitgevoerd; pas na B6), `individueel` (één speler, één attribuut: telt mee in §2.3.1). De leerlimiet van 0,12 per week blijft (FamiliarityState.cs:11).

### 2.5 Coach

Opslag als één object `Coach` in de carrière (3d-architectuur.md §4, besluit 17).

| Veld | Type | Bereik | Default | Bron |
|---|---|---|---|---|
| `Name` | string | zelfde opschoning als clubnaam (`Competition.CleanClubName`, Competition.cs:422) | | speler |
| `Skin`, `Height`, `Build`, `Hair`, `HairColour`, `Beard`, `Glasses`, `Outfit` | int | 0..7; 160..200 (cm); 0..2; 0..7; 0..5; 0..3; 0..1; 0..9 (3d-architectuur.md §4, D5) | 0 | speler |
| `Reputation` | int | 0..100 | 20 | regel |
| `Style` | enum | `motivator`, `tacticus`, `opleider`, `diplomaat` | gekozen bij de start, vast | speler |
| `Experience` | int | 0..100; +1 per gespeelde wedstrijd, +5 per gehaald seizoensdoel | 0 | regel |
| `Skills` | 2 × int | `Effect = 5% + 10% × Experience / 100` (F12: 5-15%) | | regel |

Stijleffecten (elk één zin in het spel, meetbaar n = 100 carrières): motivator: mediapunten op moraal × (1 + Effect); tacticus: wedstrijdleren `AfterMatch` × (1 + Effect); opleider: ontwikkeling van spelers ≤ 23 × (1 + Effect); diplomaat: bestuurs- en fanverlies bij verlies × (1 - Effect). Geen stijl mag > 55% van onderlinge vergelijkingen winnen (F12); de vergelijking is het gemiddelde eindseizoenspunt over 100 carrières per stijl.

Reputatie: per wedstrijd `+1` winst, `-1` verlies, gewogen × (0,5 tier 3, 1 tier 2, 1,5 tier 1), Europees × 2; seizoenseinde `+10` doel gehaald, `-10` gemist, `+5` kampioen of beker. Clip 0..100.

### 2.6 Bestuur

| Veld | Type | Bereik | Default | Bron |
|---|---|---|---|---|
| `Trust` | int | 0..100 | 50 | regel |
| `Goal` | enum | `Promotion`, `TopHalf`, `Survival` (bestaat: `SeasonGoal`, Competition.cs:27), plus `Cup` (≥ kwartfinale) en `Europe` (deelname) als tweede doel bij tier 1 | `Competition.GoalFor` (Competition.cs:397) over 16-18 clubs | regel |
| `MissedInARow` | int | 0..2 | 0 | regel |
| `Warning` | bool | waar zodra `Trust < 30` | false | regel |
| `Log` | lijst (week, delta, reden-sleutel) | elke wijziging heeft een reden (F2) | | regel |
| `Offers` | lijst (clubId, week) | alleen na ontslag of aan het seizoenseinde bij `Reputation ≥ clubReputation - 10` | | regel |

Regels: winst `+2`, gelijk `0`, verlies `-2` (× 2 tegen een club met lagere reputatie); mediapunten erbij; seizoenseinde doel gehaald `+15`, gemist `-20`. Ontslag als `Trust < 15` of `MissedInARow = 2`; dan ≥ 3 aanbiedingen uit clubs met `Reputation ≤ coachReputation + 10` en geen tier-1-club bij reputatie < 40 (F2). De speler kiest; weigeren = de carrière stopt (open vraag 8). Doelcriteria F2 (headless): 20-40% ontslagen na twee keer missen, ≤ 5% bij halen; de getallen hierboven zijn startwaarden voor F7.

### 2.7 Media: vragenbank

Bestand `data/media/vragen.json` (meegeleverd) en `user://mods/<mod>/media/vragen.json` (toevoegen, §5). Teksten zijn sleutels in de taaltabellen (`data/lang/en.json`, `nl.json`), geen tekst in de vragenbank zelf.

```json
{
  "schemaVersion": "0.1",
  "questions": [
    {
      "id": "pre_derby_favourite",
      "trigger": { "moment": "pre", "when": ["opponent_lower_reputation"] },
      "text": "media.q.pre_derby_favourite",
      "answers": [
        { "text": "media.a.pre_derby_favourite.confident", "morale": 4, "board": 0, "fans": 3, "followUp": "post_lost_as_favourite" },
        { "text": "media.a.pre_derby_favourite.humble",    "morale": 0, "board": 2, "fans": -2 },
        { "text": "media.a.pre_derby_favourite.deflect",   "morale": -2, "board": 0, "fans": 0 }
      ]
    }
  ]
}
```

| Veld | Type | Bereik | Verplicht |
|---|---|---|---|
| `id` | string | `^[a-z0-9_]+$`, uniek over alle bestanden | ja |
| `trigger.moment` | enum | `pre`, `post`, `season_start`, `season_end`, `transfer`, `sacked`, `appointed` | ja |
| `trigger.when` | lijst enum | `won`, `lost`, `drawn`, `big_win` (≥ 3 verschil), `big_loss`, `red_card`, `opponent_higher_reputation`, `opponent_lower_reputation`, `streak_win3`, `streak_loss3`, `board_warning`, `goal_on_track`, `goal_off_track`, `cup`, `europe`, `after_transfer_in`, `after_transfer_out`, `followUp` | nee (leeg = altijd bij dat moment) |
| `text` | string | taalsleutel | ja |
| `answers` | 3 × antwoord | precies 3 | ja |
| `answers[].text` | string | taalsleutel | ja |
| `answers[].morale`, `board`, `fans` | int | -8..8; per antwoord ≥ 1 veld ≠ 0 en `|som| ≥ 2` (F4: 2-8 punten, meetbaar) | ja |
| `answers[].followUp` | string? | id van een vraag met `when: ["followUp"]`, komt binnen 3 wedstrijden terug | nee |

Selectie per persconferentie: 3-4 vragen met passende trigger, niet gesteld in de laatste 5 wedstrijden (`AskedLog` in de carrière: id en week), deterministisch uit `Rng(seed, week)`. Overslaan kan altijd: dan het middelste antwoord zonder punten (F4). Aantallen: v1 ≥ 30, EA ≥ 80, 1.0 ≥ 150 (roadmap F4). `LocCoverageTests` moet elke sleutel in beide talen vinden.

### 2.8 Inbox en nieuws

| Veld | Type | Bereik |
|---|---|---|
| `Week`, `Season` | int | |
| `Kind` | enum | `result`, `board`, `media`, `transfer`, `injury`, `suspension`, `development`, `offer`, `europe`, `season_summary` |
| `Text` | string | taalsleutel |
| `Args` | lijst string | parameters voor `Loc.T(key, args)` (godot/Loc.cs:103) |
| `Read` | bool | |

Maximaal 200 berichten bewaard (oudste weg). Het seizoensoverzicht (F6) is één bericht van het type `season_summary` met de eindstand, bekerresultaat, Europees resultaat, topscorer van de eigen club en bestuursoordeel.

---

## 3. Koppeling met de bestaande code

### 3.1 Wat wordt uitgebreid of vervangen

| Bestand:regel | Nu | Wordt |
|---|---|---|
| sim/Zeus.Sim/Competition.cs:36-55 `SeasonState` | 1 divisie van 6, `Version` 1-2 | blijft bestaan als lezer van oude saves; nieuwe klasse `CareerState` (versie 3, §4) bevat de wereld |
| Competition.cs:125-156 `TeamsPerDivision = 6`, `Divisions` vast | 3 vaste divisies, 15 vaste clubs | `Division` uit de wereld; `Competition.Schedule()` wordt `Schedule(int teams)` (circle-method blijft, Competition.cs:163-185) |
| Competition.cs:193 `Seed(season, round, home, away)` | indices | `Seed(season, competitionId-hash, round, homeClubHash, awayClubHash)`; dezelfde mix, nooit 0 |
| Competition.cs:277 `PlayRound` | simuleert de eigen divisie volledig, 3 tegelijk | `PlayWeek`: eigen divisie volledig (zoals nu, `MaxParallel` 3), alle andere wedstrijden via het snelle model (§3.4) |
| Competition.cs:245 `EnsureFamiliarity` (slot-id's) | vertrouwdheid per slot | per persoon-id (`Player.Familiarity`), vóór de wedstrijd omgezet naar een `FamiliarityState` op slot-id's door `ClubSquad.ToSquad`, erna teruggeschreven per persoon |
| Competition.cs:387-411 `SquadStrength`, `ExpectedGoal` | sjabloon per niveau | zelfde formule over de echte selectie van elke club in de divisie |
| sim/Zeus.Sim/SquadFactory.cs:13-18 `ForLevel` | laadt `squads/<level>.json` | blijft voor menu, uitdagingen en meter; de carrière gebruikt `ClubSquad.ToSquad(tactic)` die `SquadFactory.Fit` (regel 21) hergebruikt met de 18 gekozen spelers |
| sim/Zeus.Sim/Squad.cs:36-56 `SquadLoader` | strikte parser 1..20 | ongewijzigd; `ClubSquad` bouwt `SquadPlayer` direct met `(n-1)/19` (regel 114) |
| sim/Zeus.Sim/Match.cs:234-292 `Match.Create` | `homeSquad`, `homeFamiliarity` | **ongewijzigd** (de sim verandert niet; roadmap §7) |
| godot/Season.cs | 1 scherm: stand, schema, acties, vertrouwdheid | wordt het carrièrescherm met tabbladen: Week (volgende wedstrijd, training, persconferentie), Stand (divisie, beker, Europa), Selectie, Transfers, Staf, Bestuur, Inbox; de bestaande `Load`/`Save` (Season.cs:217-247) blijven |
| godot/Season.cs:14 `user://zeus_carriere.json` | | zelfde bestand, versie 3 |
| godot/ClubIdentity.cs:20-23 | leest `instellingen.json` | leest de eigen club uit de carrière als die bestaat, anders de instellingen (menu, uitdagingen); `ClubPicker` (regel 94) schrijft naar beide |
| godot/SquadScreen.cs | selectie per niveau uit `squads/` | hergebruikt `SquadTable`; krijgt een carrièrestand (18 uit 20-26 kiezen, cijfers 1-20, blessure, schorsing) |
| godot/Editor.Scout.cs:35 `RefreshScout` | toont stijl van de gekozen tegenstander | toont wat het scoutniveau toelaat (§2.4) |
| godot/UserSaveFile.cs:11-12 | marker `"1"` | ongewijzigd (§4) |
| data/lang/*.json | clubnamen via `Loc.Name` (Loc.cs:106-114) | gegenereerde namen worden niet vertaald; alleen divisie- en toernooinamen krijgen sleutels |
| scripts/generate_squads.py | 3 sjabloonselecties | blijft (menu, uitdagingen); de wereld genereert in C# (§6) |

Nieuw in `sim/Zeus.Sim/World/`: `World.cs`, `WorldGenerator.cs`, `Names.cs`, `Calendar.cs`, `FastResult.cs`, `ClubSquad.cs`, `Transfers.cs`, `Board.cs`, `Media.cs`, `Lifecycle.cs` (ontwikkeling, blessure, schorsing), `CareerState.cs`, `CareerMigration.cs`. Nieuw in de Runner: `--career-batch` (§6.4). Nieuw in Godot: tabbladen in Season.cs (gesplitst in partials zoals `Editor.*.cs`), `PressConference.cs`, `ModLoader.cs`.

### 3.2 Spelercijfers 1-20 in de sim ("+4 geeft meetbaar effect", besluit 22, B7)

De keten is al gebouwd en verandert niet door de managerlaag:

1. Opslag en weergave: geheel getal 1..20 (schema/speler.schema.json; §2.3).
2. `SquadPlayer.Attributes` 0..1 via `(n-1)/19` (Squad.cs:114). Een stap van +4 is +0,2105.
3. `Match.Create` zet `p.Effective = EffectiveOf(attributes)` (Match.cs:284): `Pull` trekt naar het halfprofgemiddelde met `LevelSpreadUp = 0,35` boven en `LevelSpreadDown = 0,20` eronder (Match.Attributes.cs:30-33). Boven het anker wordt +4 dus **+0,074 effectief**, eronder +0,042.
4. Effecten per attribuut (Match.Attributes.cs): passing → `Skill + 0,35 × (passing - ref)` (regel 153); finishing → `Skill - 0,24 + 2,0 × (finishing - ref)` (regel 162); vision → scaninterval, gezichtsveld, passopties (regels 87-90, 146); anticipation → reactievertraging `0,7 × max(0, ref - a)` (regel 100); speed/acceleration → `+2,4`, `+2,5` (regels 77-79); first touch → controle ×`(1 + 0,3 × (a - ref))` (regels 174-175); positioning → formatiebias (regels 137-143); decisions → softmaxtemperatuur (regel 145); keeper → `1 + 0,3 × delta` alleen boven het anker (regels 63-73). `dribbling`, `heading`, `strength` en `rushingOut` hebben geen effect (spelers.md).

Rekenvoorbeeld passing: +4 boven het anker → +0,074 → passvaardigheid +0,026 → ongeveer +2 tot +3 procentpunt passnauwkeurigheid (de probe mat 73-79% prof tegen 70-75% halfprof bij een profiel dat ~4 punten hoger ligt, spelers.md "Meting voor en na"). Het B7-criterium (+3 punten, n = 200) ligt op de rand; **B7 ijkt de constanten (`LevelSpreadUp/Down`, de hellingen), niet dit document.** De managerlaag eist alleen: (a) de getoonde cijfers zijn de ruwe waarden, (b) `Player.Effective` blijft intern, (c) F1 meet na elke seizoensstap dat het divisiegemiddelde binnen ±10% van seizoen 1 blijft, zodat B7's ijking geldig blijft.

Meting voor de managerlaag zelf: `--career-batch` schrijft per seizoen per divisie het gemiddelde en de spreiding van elk attribuut (F1) en per transferperiode de prijs tegen het gemiddelde (F3, monotoon).

### 3.3 Vertrouwdheid per persoon

Nu: `FamiliarityState.Players` is gekeyed op slot-id (vertrouwdheid.md "Staat, leren en opslag"), dus een invaller erft niets en een basisspeler die van slot wisselt verliest zijn groei. In de carrière: `Player.Familiarity` per persoon. `ClubSquad.ToSquad` maakt per wedstrijd `FamiliarityState.ForTeam(clubId, slotIds)` en vult elk slot met de waarden van de persoon in dat slot; na `PlayWeek` schrijft `AfterMatch` (FamiliarityState.cs:48) terug per persoon. Wie niet speelde leert niet (nu: de hele opgeslagen selectie, inclusief bank; competitie.md). AI-clubs houden de vaste referentie 0,7 (`Competition.AiFamiliarity`, Competition.cs:267). `Match` verandert niet.

### 3.4 Snel resultaatmodel voor niet-gesimuleerde wedstrijden

150 clubs × 34 ronden ≈ 2600 competitiewedstrijden per seizoen plus beker en Europa; een volle simulatie kost 5-10 s per wedstrijd (PRD "Prestaties") en haalt F9 (≤ 10 min per seizoen) en F7 (100 × 10 seizoenen) niet. Daarom:

- **Eigen divisie:** volledige simulatie, zoals nu (Competition.cs:298-329), 3 tegelijk; 8-9 wedstrijden per ronde ≈ 30-45 s op de ontwikkel-pc. De eigen wedstrijd live of gesimuleerd, identieke seed (competitie.md "Seeds").
- **Alle andere wedstrijden:** `FastResult.Play(home, away, seed)`: deterministisch, doelpunten uit twee Poisson-trekkingen met verwachting `λ = 1,4 × 1,15^(Δsterkte)` per team (Δsterkte = verschil in `SquadStrength` in punten op de schaal 1..20, thuis +0,3), geijkt zodat gelijke paren 2,2-3,2 doelpunten per wedstrijd geven (dezelfde band als `LevelCalibrationTests`, spelers.md "Regressie en grenzen"). Kaarten: geel ~2,0 per team, rood 0,08, uit dezelfde trekking.
- **Test** (`FastResultTests`): voor amateur-amateur, halfprof-halfprof, prof-prof en prof-amateur ligt het snelle model binnen ±0,3 doelpunt en ±8 procentpunt winstkans van de meting uit `--calibrate-levels` (`.tmp_levels/niveau_ronde2.jsonl`, spelers.md "Resultaten ronde 2"). Herijken na elke B-kalibratie.
- **Headless balans** (`--career-batch`): optioneel `--fast-own` waarmee ook de eigen divisie snel rekent; dan ≈ 3000 wedstrijden × < 1 ms. 100 × 10 seizoenen ≈ minuten, niet uren. Volle simulatie van de eigen divisie wordt apart gemeten (F9: één seizoen ≤ 10 min).

Dit is ontwerpkeuze nummer één voor de maker (open vraag 1): zonder snel model is de wereld van 150 clubs niet te balanceren (roadmap §8).

---

## 4. Opslag: `zeus_carriere.json` versie 3

Twee lagen, zoals nu (opslag-bij-updates.md): de bestandslaag `UserSaveFile` (marker `_gaffersNotebookSaveVersion`, nu `"1"`, UserSaveFile.cs:11-12, `.bak` en `.bad`) en de domeinversie in het JSON-object (`Version`, Competition.cs:38, nu 1 of 2). **De marker blijft `"1"`**: het bestand blijft een JSON-object dat een oudere build herkent en weigert op `Version` (demo.md regel 132: een andere versie wordt `.bad`). De domeinversie gaat naar 3.

| Versie | Wat verandert | Lezen |
|---|---|---|
| 1 | `SeasonState` zonder vertrouwdheid (competitie.md) | `SeasonState.Parse` (Competition.cs:62) accepteert 1 en 2 en zet `Version = 2` |
| 2 | + `Familiarity`, `TrainedThisRound`, `PendingDemand`, optioneel `Goal` | idem |
| **3** | nieuwe root `CareerState { Version: 3, Seed, World, OwnClubId, Coach, Board, Inbox, AskedLog, Pending, PendingDemand, TrainingFocus, Week, Season }`; geen `Division`, `Round`, `Results`, `History` op rootniveau meer (zitten in `World`); `History` wordt `Coach.History` met clubId erbij | `CareerState.Parse`: leest `Version`; 1 of 2 → `CareerMigration.FromSeasonState` (hieronder); 3 → direct; > 3 → `FormatException` "Nieuwere carrièreversie", het scherm blokkeert opslaan zoals bij `Unreadable` (Season.cs:223-228) en zet het bestand **niet** opzij |

Migratie 1/2 → 3 (`CareerMigration.FromSeasonState(SeasonState old, string clubName, int kit1, int kit2, int crest)`):

1. Genereer de wereld met `Seed = Competition.Seed(old.Season, 0, 0, 0)` (deterministisch uit de oude staat, nooit 0).
2. Plaats de eigen club in het eigen land op tier `3 - old.Division` (amateur 0 → tier 3). De naam, kleuren en het embleem komen uit `instellingen.json` (ClubIdentity.cs:20-23, Save regel 30); daarna is de carrière leidend.
3. Het lopende seizoen gaat **niet** mee: de oude stand (6 clubs, 10 ronden) past niet op 16-18 clubs. `old.History` wordt `Coach.History` (seizoen, divisie → tier, positie, uitkomst). `old.Season` blijft; `Week = 1`. De speler krijgt één inboxbericht `migrated` dat het seizoen opnieuw begint in dezelfde divisie.
4. Vertrouwdheid: `old.Familiarity.Players[slot]` wordt overgenomen door de persoon die `ClubSquad.ToSquad(old.PlayerTactic)` in dat slot zet; bankslots `B1..B7` naar de bankspelers in volgorde. `Learned` en `Period` op 0.
5. `old.Pending` en `old.PendingDemand` vervallen (de ronde bestaat niet meer).
6. Een nieuwe `Coach` met de naam van de club als placeholder ("Coach van <club>"), stijl `tacticus`, reputatie uit de tier (tier 3 → 20, 2 → 40, 1 → 60).

Verplichte tests (`CareerMigrationTests`): een v1- en een v2-fixture uit `CompetitionTests.StateRoundTripsThroughJson` laden zonder fout; dezelfde oude staat geeft twee keer dezelfde wereld (hash van `World.ToJson()`); een v3-bestand met `Version: 4` geeft `FormatException` en wordt niet opzijgezet (autotest `scripts/autotest/10a_save_damaged.json` als voorbeeld van de bestandslaag); v3-rondreis `Parse(ToJson())` byte-identiek.

Grootte: ~3350 spelers × ~350 bytes + clubs ≈ 1,5 MB met `WriteIndented = false` voor `World` (de rest mag ingesprongen blijven). Opslaan na elke week en na elke beslissing, zoals nu (Season.cs:240). `FeedbackPack` (sim/Zeus.Sim/FeedbackPack.cs:20) neemt het bestand al mee.

Opslag per versie vanaf 3: elk veld dat erbij komt is optioneel met default (`JsonIgnoreCondition.WhenWritingNull`, zoals `Goal`, Competition.cs:54), zodat versie 3 niet hoeft te verspringen voor F2-F12. Pas als een veld van betekenis verandert komt versie 4 met een migratiestap in `CareerMigration`.

---

## 5. Bewerkbare data (besluit 20, F11)

### 5.1 Modmap

`user://mods/<modnaam>/` (op Windows `%APPDATA%\GaffersNotebook\mods\<modnaam>\`, zie opslag-bij-updates.md). Eén mod tegelijk actief, gekozen bij **Nieuwe carrière** en vastgelegd in `CareerState.ModName`; een lopende carrière laadt alleen PNG's opnieuw (namen en cijfers zitten in de save). Zonder mod: de generator (§6).

```
mods/mijn-wereld/
  mod.json              verplicht: { "schemaVersion": "0.1", "name": "...", "author": "...", "seed": 12345 }
  wereld.json           optioneel: landen, divisies, clubs (namen, kleuren, tactiek, startreputatie)
  spelers.json          optioneel: spelers per club (anders gegenereerd)
  media/vragen.json     optioneel: extra persvragen (§2.7), id's mogen de meegeleverde niet overschrijven
  tenues/<clubId>.png   optioneel
  emblemen/<clubId>.png optioneel
```

### 5.2 Schema `wereld.json` (uittreksel)

```json
{
  "schemaVersion": "0.1",
  "countries": [
    {
      "id": "noord", "name": "Noordland", "nameStyle": "noord", "isHome": true,
      "divisions": [
        {
          "id": "noord-1", "name": "Noordelijke Eredivisie", "tier": 1, "promoted": 0, "relegated": 3,
          "clubs": [
            { "id": "club:noord-0001", "name": "FC Havenstad", "shortName": "HAV", "kit1": 0, "kit2": 3, "crest": 2,
              "tactic": "hoge_druk_433.json", "reputation": 72 }
          ]
        }
      ]
    }
  ]
}
```

`spelers.json`: `{ "schemaVersion": "0.1", "players": [ { "club": "club:noord-0001", "name": "...", "role": "spits", "preferredPosition": "ST", "age": 24, "attributes": { ...15 × 1..20 }, "keeper": { ... } } ] }`; velden en namen identiek aan `schema/speler.schema.json`. Ontbrekende spelers van een club worden bijgegenereerd tot 20.

### 5.3 Validatie en foutmeldingen

Dezelfde stijl als `SquadLoader` (Squad.cs:103-123: pad `$.countries[0].divisions[1].clubs[3].kit1: verwacht 0..9`) plus het regelnummer uit `JsonException.LineNumber` bij kapotte JSON. Regels:

| Regel | Melding (sleutel `mod.error.*`, beide talen) |
|---|---|
| Ongeldige JSON | `mod.error.json`: "<bestand> regel <n>: ongeldige JSON (<reden>)" |
| Onbekend of dubbel veld | `mod.error.field`: "<pad>: onbekend of dubbel veld" |
| Precies 4 landen, precies 1 `isHome`, eigen land 3 divisies, andere 2 | `mod.error.structure` |
| 16 of 18 clubs per divisie; tiers 1..n aaneengesloten | `mod.error.division` |
| Id's uniek over de hele wereld; patroon `^[a-z0-9:-]+$` | `mod.error.id` |
| Naam ≤ 24 tekens, geen controletekens, niet leeg (`Competition.CleanClubName`), niet in de blokkeerlijst (§6.3) | `mod.error.name`, `mod.error.blocked` |
| `tactic` bestaat in `tactics/` of `user://tactieken` (`TacticCatalog.Resolve`) | `mod.error.tactic` |
| `kit1`, `kit2` 0..9; `crest` 0..7; `reputation` 0..100; cijfers 1..20; leeftijd 17..38 | `mod.error.range` |
| PNG: afmetingen en formaat (§5.4) | `mod.error.png` |
| Vraag: 3 antwoorden, punten -8..8, `|som| ≥ 2`, id uniek, taalsleutels bestaan | `mod.error.question` |

Gedrag bij fouten (F11): de melding verschijnt op het scherm **Nieuwe carrière** met bestand en regelnummer; de mod wordt niet geactiveerd; het spel start met de generator. Een kapotte PNG in een lopende carrière: melding in de inbox, terugval op het getekende embleem (ClubIdentity.cs:39). Er wordt nooit iets in de modmap geschreven.

### 5.4 PNG-tenues en -emblemen

| Bestand | Afmeting | Inhoud | Gebruik |
|---|---|---|---|
| `emblemen/<clubId>.png` | 256 × 256, RGBA, transparant buiten de vorm | embleem | 2D: `CrestView` (ClubIdentity.cs:77) tekent de texture in plaats van `Draw`; 3D: vlaggen en spandoeken (D11) |
| `tenues/<clubId>.png` | 512 × 512, RGBA | tenue-texture; v1 2D: alleen de gemiddelde kleur van het bovenste kwart als shirtkleur; 3D: de texture op het spelersmodel (D3: shader-parameter uit `ClubIdentity.Kits`) | |
| `tenues/<clubId>_uit.png` | 512 × 512 | uittenue (optioneel) | bij gelijke shirtkleur (ΔE < 20 op de gemiddelde kleur) |

Afwijkende afmetingen worden geweigerd (`mod.error.png`: "verwacht 256×256"), geen schaling. Maximaal 1 MiB per bestand (zelfde grens als tactiekimport, tactieken-delen.md). Lees-only; de PNG blijft in de modmap, de save bevat alleen `ModName`.

Deelcode voor tactieken (F11, "bestaat, uitbreiden"): ongewijzigd `TacticExchange` (sim/Zeus.Sim/TacticExchange.cs); de managerlaag voegt niets toe behalve dat een tactiek uit de carrière exporteerbaar is via dezelfde knop.

---

## 6. Generatie van de wereld

### 6.1 Determinisme

`WorldGenerator.Generate(ulong seed, ModData? mod)`: alle trekkingen uit `Rng` (sim/Zeus.Sim/Rng.cs) met afgeleide stromen `new Rng(seed, stroom)` per onderdeel (landen 1, clubs 2, spelers 3, staf 4, kalender 5), zodat een wijziging in de spelergeneratie de clubnamen niet verandert. Geen `System.Random`, geen `Guid`, geen klok. Test: dezelfde seed geeft twee keer dezelfde `World.ToJson()`; seeds 1..20 geven 20 verschillende werelden; de generator voor 152 clubs en ~3350 spelers draait in < 2 s.

Volgorde: landen en namen → divisies en clubindeling → clubreputatie (tier-band, gespreid over de divisie zodat de stand niet triviaal is) → spelers per club (22 ± 2, rolverdeling 3 keepers, 7 verdedigers, 7 middenvelders, 5 aanvallers, leeftijden 17-35 met zwaartepunt 24-28) → staf (3 per club) → kalender → Europese deelnemers seizoen 1 op reputatie.

### 6.2 Naamgeneratie per land

Vier lettergreepstijlen, elk met tabellen `voor`, `midden`, `achter` voor plaatsnamen en `voornaam`, `achternaam` voor personen, in `data/world/namen.json` (meegeleverd, mod kan vervangen):

| Stijl | Klank (voorbeeld, fictief) | Clubvoorvoegsel |
|---|---|---|
| `noord` | Nederlands/Scandinavisch: Kleiduinen, Rietveld (zoals de bestaande namen, Competition.cs:140-154) | FC, SV, VV, SC |
| `west` | Engels/Frans: Ashcombe, Verlaine | United, Town, Athletic, AS |
| `zuid` | Spaans/Italiaans: Valdoria, Montecalvo | CF, UD, AC, Real |
| `oost` | Duits/Slavisch: Steinburg, Novak | FK, SK, Dynamo, Spartak |

Plaatsnaam = `voor + midden? + achter` (2-3 lettergrepen, ≤ 16 tekens); clubnaam = voorvoegsel + plaatsnaam, uniek per wereld. Persoonsnaam = voornaam + achternaam uit de stijl van het land; 10% van de spelers krijgt de stijl van een ander land. De 18 bestaande namen uit `generate_squads.py` regel 25 zitten in de `noord`-tabellen zodat de sjabloonselecties blijven passen.

### 6.3 Blokkeerlijst

`data/world/blokkeerlijst.txt`: één naam per regel, kleine letters zonder diakritische tekens; echte clubnamen (Europese topdivisies en bekerwinnaars), plaatsnamen die als clubnaam dienen (Ajax is geen plaats, maar wel geblokkeerd), bekende spelers en trainers (≥ 1000 regels, samengesteld door een agent uit openfootball-clublijsten, CC0; geen andere bron nodig). Regels: een gegenereerde of mod-naam wordt geweigerd als de genormaliseerde vorm (kleine letters, geen diakritische tekens, zonder voorvoegsel FC/SV/...) gelijk is aan een regel, of als een regel van ≥ 5 tekens er als heel woord in voorkomt. Test `WorldNamesTests`: 20 seeds × alle club- en spelersnamen → 0 treffers; de testset bevat bovendien 10 echte namen die geweigerd moeten worden (F9-criterium).

### 6.4 Verdeling van cijfers en balans headless

Per divisie `RatingBand` (§2.1). Per speler: gemiddelde uit een driehoeksverdeling binnen de band; rolprofiel zoals `generate_squads.py` regel 27 (fysiek relatief hoog, overzicht laag bij amateur) geschaald naar dat gemiddelde; daarna ±2 ruis per attribuut, clip 1..20. Keepers: `keeper`-blok op gemiddelde ±2.

Meten: `dotnet Zeus.Runner.dll --career-batch --careers 100 --seasons 10 --first-seed 1 --results carriere.jsonl [--fast-own] [--style tacticus] [--verify-career]`, naar het patroon van `--calibrate-levels` (sim/Zeus.Runner/LevelCalibration.cs). Per carrière per seizoen één JSON-regel: divisiegemiddelden per attribuut, aantal transfers, hoogste concentratie top-10 per divisie, prijs/gemiddelde-paren, bestuursvertrouwen, ontslagen (met reden: twee keer gemist / vertrouwen), aanbiedingen (tiers tegen reputatie), fanvertrouwen en opkomst, trainingsfocus-effect (vertrouwdheid, conditie, blessureduur) bij stafniveau 5 en 15, stijleffect, crashes. `--verify-career` toetst, exitcode 3 bij een overschrijding:

| Criterium | Grens | Bron |
|---|---|---|
| Geen crash, geen `Abandoned`-fout die de carrière stopt | 100 × 10 seizoenen | F7 |
| Divisiegemiddelde per attribuut in seizoen 10 | binnen ±10% van seizoen 1 | F1 |
| Clubs met > 50% van de top-10 van hun divisie | 0 na 10 seizoenen | F3 |
| Prijs tegen gemiddelde | monotoon stijgend bij gelijke leeftijd en tier | F3 |
| Ontslag na twee keer het doel missen / bij halen | 20-40% / ≤ 5% | F2 |
| Aanbiedingen aan een coach met reputatie < 40 | 0 tier-1-clubs | F2 |
| Opkomst bij fanvertrouwen ≤ 20 / ≥ 80 | ≤ 50% / ≥ 90% | F5 |
| Trainingsfocus-effect; stafniveau 15 tegenover 5 | meetbaar (n = 100) en verschil ≥ 20% | F10 |
| Stijleffect; winstaandeel per stijl in onderlinge vergelijking | 5-15% op de genoemde maatstaf; ≤ 55% | F12 |
| Mediapunten per antwoord | 2-8, zichtbaar in de log | F4 |
| Eén seizoen alle competities (eigen divisie vol gesimuleerd) | ≤ 10 min op de ontwikkel-pc | F9 |

De toets draait als regressietest alleen in een kleine vorm (`CareerBatchTests`: 3 carrières × 3 seizoenen, `--fast-own`, vaste seeds) in `Zeus.Sim.Tests`; de volle 100 × 10 is een meetopdracht zoals `--calibrate-levels`, met de uitkomst in `docs/hervatten.md`.

---

## 7. Bouwvolgorde

Elke stap ≤ één agent-week, apart te beoordelen, met "klaar als" uit de roadmap waar die er is. Bestaande tests die groen moeten blijven bij elke stap: `GoldenMatchTests` (de sim verandert niet), `SquadTests`, `SquadFactoryTests`, `FamiliarityTests`, `CompetitionFamiliarityTests`, `LevelCalibrationTests`, `LocCoverageTests`, `scripts/smoke_godot.py`, autotest `11a_league` (roadmap §7 T1). Alle nieuwe tests in `sim/Zeus.Sim.Tests`, headless, zonder Godot.

| Stap | Roadmap | Wat | Klaar als | Tests |
|---|---|---|---|---|
| S1 | F9 | `World`, `WorldGenerator`, `Names`, blokkeerlijst, `data/world/*.json` | Deterministisch uit een seed; 152 clubs, ~3350 spelers in < 2 s; 0 blokkeerlijst-treffers op 20 seeds; cijfers per divisie in de band | nieuw `WorldGeneratorTests`, `WorldNamesTests` |
| S2 | F9 | `Calendar`, `Schedule(teams)`, beker, Europa, promotie/degradatie, `FastResult`, `PlayWeek` zonder UI | Een seizoen van alle competities headless ≤ 10 min (eigen divisie vol), `--fast-own` < 30 s; stand en bracket kloppen; `FastResult` binnen de band van §3.4 | `CompetitionTests` aangepast op 16/18 clubs (schema, tiebreaks, randen); nieuw `CalendarTests`, `FastResultTests`, `TournamentTests` |
| S3 | F9, F1 | `CareerState` v3, `CareerMigration`, `ClubSquad.ToSquad`, vertrouwdheid per persoon; Season.cs leest en schrijft v3; tabblad Week en Stand | Oude v1/v2-saves laden (F1); de maker speelt 3 seizoenen in 4 landen zonder blokkerende fout (M4); live wedstrijd en uitslag terug werken zoals nu | nieuw `CareerMigrationTests`, `ClubSquadTests`; `CompetitionFamiliarityTests`; autotest `11a_league` bijgewerkt |
| S4 | F1 | `Lifecycle`: ontwikkeling, veroudering, stoppen, instroom, blessures, schorsingen, moraal | 100 × 10 headless: divisiegemiddelden binnen ±10% (F1); geen club onder 18 spelers | nieuw `LifecycleTests`, `CareerBatchTests` (klein); Runner `--career-batch` |
| S5 | F1 | Tabblad Selectie: 18 uit de club kiezen, cijfers 1-20, blessure, schorsing, `SquadTable` hergebruikt | Opstelling kiezen ≤ 1 min; een geblesseerde of geschorste speler is niet te kiezen; cijfers zijn de ruwe waarden | smoke; autotest `12_career_squad` (nieuw) |
| S6 | F2 | `Board`, coachreputatie, waarschuwing, ontslag, aanbiedingen; tabblad Bestuur; coachnaam en stijlkeuze bij Nieuwe carrière (uiterlijk pas in D5) | Elke wijziging toont een reden; headless 20-40% / ≤ 5%; geen tier-1-aanbieding bij reputatie < 40 | nieuw `BoardTests`; `CareerBatchTests` uitgebreid |
| S7 | F3 | `Transfers`: prijs, budget, periodes, AI-handel; tabblad Transfers | Kopen of verkopen ≤ 3 klikken; geen contractscherm; concentratiegrens; prijzen monotoon | nieuw `TransferTests` |
| S8 | F4 | `Media`: vragenbank, selectie, `PressConference.cs`, ≥ 30 vragen in beide talen | ≤ 90 s per persconferentie; 2-8 punten per antwoord, zichtbaar; geen herhaling binnen 5 wedstrijden; overslaan kan altijd | nieuw `MediaTests`; `LocCoverageTests` |
| S9 | F10 | Trainingsfocus per week, staf met niveau en effecten, tabblad Staf; scoutniveau in de TEST-tab | Elke focus meetbaar (n = 100 carrières); stafniveau verschuift ≥ 20%; staf wisselen ≤ 3 klikken | nieuw `StaffTests`; `FamiliarityTests` |
| S10 | F12 | Coachstijl en vaardigheden, ervaring | Elk effect één zin, 5-15% meetbaar; geen stijl > 55% | `CareerBatchTests` met `--style` |
| S11 | F5 | Fanvertrouwen, opkomst, koppeling naar `CrowdModel` (E1) en stadionstadium (D11) | Opkomst ≤ 50% bij ≤ 20, ≥ 90% bij ≥ 80; `CrowdModel.Intensity` schaalt met opkomst; reden per wijziging | nieuw `FansTests`; `CrowdModel`-unittests (godot/tests) |
| S12 | F6 | Inbox, nieuwsfeed, seizoensoverzicht, Europese resultaten; tabblad Inbox | Mediaan tijd tussen twee wedstrijden 2-6 min bij testers (A6) | smoke; `LocCoverageTests` |
| S13 | F7 | Balansronde: 100 × 10 met `--verify-career`, constanten bijstellen, uitkomst in `docs/hervatten.md` | Alle grenzen uit §6.4 groen; geen crash | `CareerBatchTests` |
| S14 | F11 | `ModLoader`, validatie, PNG-tenues en -emblemen, `docs/mods.md` | Gewijzigde club zichtbaar zonder herstart in 2D (3D in D11); kapotte JSON geeft bestand en regelnummer en het spel start | nieuw `ModLoaderTests` (fixtures met fouten per regel uit §5.3) |

Volgorde en afhankelijkheden volgen roadmap §4 F: S1-S5 in 2027-Q3 (F9, F1), S6-S9 in 2027-Q4 (F2, F3, F4, F10), S10-S13 in 2028-Q2 (F12, F5, F6, F7), S14 in 2028-Q3 (F11). G9 (eind 2028-Q1) kan S8 inkorten (minder vragen), S9 versimpelen (staf zonder niveau-effect) en S12 uitstellen.

Wat de maker per stap moet zien: S1 een lijst van 20 clubnamen en 20 spelersnamen per land (oordeel: fictief en geloofwaardig); S2 een stand en bracket in de Runner-uitvoer; S3 zijn eigen oude carrière geladen; S5-S9 het scherm; S13 de tabel uit §6.4.

---

## 8. Open vragen voor de maker

| # | Vraag | Aanbevolen antwoord |
|---|---|---|
| 1 | Mogen wedstrijden buiten de eigen divisie met een snel resultaatmodel (Poisson uit sterkte, deterministisch) worden berekend in plaats van de volle simulatie? | Ja. Zonder dit zijn F7 en F9 niet haalbaar; de eigen divisie blijft vol gesimuleerd en het snelle model wordt geijkt op de volle (§3.4). |
| 2 | Vier fictieve landen met klankstijlen noord, west, zuid, oost; eigen land = noord (Nederlands/Scandinavisch), 3 divisies. Akkoord met de stijlen en dat het eigen land noord is? | Ja; namen van de landen zelf (bijv. Noordland) kiest de maker na S1, wanneer hij de gegenereerde lijsten ziet. |
| 3 | Divisiegrootte: tier 1 18 clubs (34 wedstrijden), tier 2 en 3 16 clubs (30), promotie 3 / degradatie 3? | Ja: binnen besluit 12, en 3/3 geeft meer beweging dan de huidige 2/1. |
| 4 | Een lopend oud seizoen (v1/v2) gaat bij de migratie verloren; geschiedenis, divisie, club en vertrouwdheid blijven. Acceptabel? | Ja; de oude stand van 6 clubs past niet op 16-18 en de demo-carrières zijn kort. Melding in de inbox. |
| 5 | Gelijkspel in beker en Europa: deterministische trekking uit de seed (geen verlenging of strafschoppen in de sim). | Ja voor v1; verlenging en strafschoppenserie in de sim zijn een B-taak, niet F. |
| 6 | Transferbudget en prijzen in een eenheid "k" zonder valuta, of een fictieve munt? | "k" zonder valuta; één getal, geen financiële laag (besluit 3). |
| 7 | Moraal (team en speler) heeft in v1 geen effect in de simulatie, alleen op training, transfers en de getoonde waarde. Of moet het de sim raken? | Geen sim-effect in v1 (de sim verandert niet; B7 ijkt de attributen). Heroverwegen bij G9 als testers moraal als "nep" ervaren. |
| 8 | Na ontslag kiest de speler uit ≥ 3 aanbiedingen; weigeren = einde carrière (nieuwe starten). Of een seizoen "werkloos" met later nieuwe aanbiedingen? | Weigeren = einde; werkloos zijn is extra staat zonder spel. |
| 9 | Coachstijl is vast per carrière (gekozen bij de start) of veranderbaar? | Vast; anders zijn de F12-metingen per stijl niet te doen en wordt wisselen de optimale zet. |
| 10 | Blokkeerlijst uit openfootball (CC0) met ≥ 1000 echte club-, plaats- en persoonsnamen: voldoende als "woordenlijst" uit F9, of ook een handmatige controle van de 152 clubnamen door de maker? | Lijst plus een blik van de maker op de gegenereerde clubnamen bij S1 (≈ 15 min); de lijst vangt exacte en bijna-exacte treffers, niet elke gelijkenis. |
