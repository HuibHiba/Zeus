# Slimme tegenstander v1 (roadmap B4, VOORLOPIG)

Branch `feat/slimme-tegenstander`. Alleen de **tactiekkeuze van de tegenstander in de competitie** verandert; wedstrijdregels, `Snapshot` en `EventLog` blijven gelijk.

## Stijlherkenning (`sim/Zeus.Sim/PlayStyleProfile.cs`)
Na elke gespeelde competitieronde bewaart `Competition.PlayRound` een `StyleSample` van jouw tactiek: pressing, lijnhoogte, tempo (uit de tactiek-JSON) en, alleen bij gesimuleerde wedstrijden, jouw aandeel balcontroles (`MatchStatistics.Home/Away.BallControls`). Een live wedstrijd levert hier geen statistieken (alleen de eindstand komt terug), dus daar blijft het aandeel leeg en telt de tactiek. Er is geen nieuwe sim-meting.

| Stijl | Regel per wedstrijd |
|---|---|
| HighPress | pressing >= 0,65 en lijnhoogte >= 0,5 |
| LowBlock | pressing <= 0,4 en lijnhoogte <= 0,4 |
| Possession | balaandeel >= 55%, of (zonder aandeel) tempo <= 0,5 en lijnhoogte >= 0,5 |
| None | rest (o.a. vleugelspel, man-om-man, lange bal) |

Een stijl telt pas als je **laatste 3 wedstrijden dezelfde stijl** hadden (`PlayStyleProfile.Detect`). Bewaard worden de laatste 5.

## Tegenmaatregel (vaste tabel, `PlayStyleProfile.Counter`)
| Jouw stijl | Tegenstander speelt | Reden (tekstsleutel) |
|---|---|---|
| HighPress | `lange_bal_442.json` | `opponent.adapt.high_press`: verwacht hoge druk, lange ballen achter jouw lijn |
| Possession | `laag_blok_442.json` | `opponent.adapt.possession`: compact laag blok |
| LowBlock | `hoge_druk_433.json` | `opponent.adapt.low_block`: zet hoog druk |

Vleugelspel (smaller blok, dubbelen) is niet gebouwd: er is geen betrouwbare breedtemeting in de tactiek of statistieken (twijfelpunt). Er worden hele bestaande tactieken gekozen, geen instellingen aangepast.

## Niveau
`Competition.AdaptsAtLevel`: amateur nooit; halfprof in de helft van de rondes (`Seed(seizoen, ronde, 0, 0)` is even); prof altijd. Alleen de wedstrijd van de speler wordt aangepast.

## Koppeling
- `Competition.Adaptation(state)` geeft de aanpassing voor de volgende ronde (of null). `NextLiveMatch(state, adaptive)` en `PlayRound/PlayRest(..., adaptiveOpponent)` gebruiken die; standaard `false`, dus bestaande tests en de uitdaging-code blijven gelijk.
- Godot: schakelaar `GameSession.AdaptiveOpponent` (instelling `adaptive_opponent`, standaard aan, Instellingen > spel: "Tegenstanders passen zich aan"). `Season.cs` BuildActions toont de gekozen tegenstandertactiek (nu de echte, ook bij aanpassing) en de reden in de voorbespreking; `Editor.Scout.cs` toont dezelfde reden in de TEST-tab (leest de carrière; leeg bij problemen).
- Opslag: `zeus_carriere.json` krijgt het optionele veld `StyleHistory` (lijst van max. 5 samples; niet geschreven als null). Formaatversie blijft 2 (de bestaande test wijst `Version: 3` af); oude carrières laden met `StyleHistory = null` en worden pas bij de volgende ronde aangevuld. `Parse` weigert ongeldige samples (buiten 0..1, null, > 5). De `UserSaveFile`-laag (opslagmarkering 1) is niet veranderd.

## Uitdaging 6 "Verras de scout"
Ongemoeid: die gebruikt `OpponentPlanner` (oefenwedstrijden, 3 van de laatste 5 wedstrijden gelijkend) met de eigen geschiedenis in het uitdagingenbestand. De competitie gebruikt een aparte, eenvoudige tabel; de twee delen geen code of opslag.

## Meting en beslisregel
Zie `kalibratie.md`, "B4 slimme tegenstander v1" (`--adaptive-check`, seeds 441-480). Nog niet uitgevoerd.

## Open
- Tegenmaatregelen zijn niet getoetst op sterkte; tabel kan na de meting wijzigen.
- Zichtbare reden verschijnt ook als de tegenmaatregel gelijk is aan de normale clubtactiek.
- Live wedstrijden voeden het balaandeel niet.
