# Uitdagingen: ijking van de drempels (2026-10-16, branch feat/drempels)

De zes uitdagingen (`challenges/*.json`) zijn gebouwd vóór de niveau-herschaling. Hier zijn niveaus (`niveau_tegenstander`) en drempels geijkt op "haalbaar met een redelijke tactiek, niet triviaal". Alleen JSON is aangepast; er is geen simulatiecode gewijzigd.

## Methode
- Per uitdaging en tactiek 30 wedstrijden (seed 1..30, speler thuis) met `ChallengeEvaluator.Play`-semantiek; "voor" gebruikt 20 wedstrijden. Speler-tactieken: `hoge_druk_433`, `positiespel_4231`, `counter_532` (de drie voorbeeldtactieken), plus `laag_blok_442`, `man_om_man_433`, `testcase_a_433`. Spelerniveaus amateur, halfprof en prof.
- Slagingskans = bootstrap (3000 keer 5 wedstrijden uit de 30 trekken) van het aandeel series dat alle criteria haalt. De echte uitslag in het spel is deterministisch (seed 1..5), dus kans en echte uitslag kunnen verschillen.
- Alleen de eerste poging van uitdaging 6 is gemeten (positiespel); na herhaling kiest de planner een voorbereide tegenstander.
- Meetscripts staan niet in de repo (`.tmp_meet`, lokaal, niet gecommit).

## Wijzigingen en meting (kans per tactiek, spelersniveau halfprof; prof tussen haakjes waar relevant)
Volgorde: hoge druk / positiespel / counter / laag blok.

| Uitdaging | Voor | Na | Kans na |
|---|---|---|---|
| 1 Houd de nul | tegenstander neutraal, tegen <= 1,0 en schoten tegen <= 12 | tegenstander halfprof, tegen <= 1,2, schoten tegen <= 24 | 1 / 0 / 18 / 66 % (prof 3 / 0 / 46 / 69) |
| 2 Beheers de bal | neutraal, bezit >= 55, ongeslagen >= 4 | alleen tegenstander halfprof | 66 / 68 / 0 / 1 % (prof 72 / 82 / 5 / 75) |
| 3 Counter de bezitsploeg | neutraal | alleen tegenstander halfprof | 45 / 0 / 33 / 67 % (prof 16 / 0 / 65 / 98) |
| 4 Scoor tegen het blok | neutraal, voor >= 2,0 en schoten >= 14 | halfprof, voor >= 1,0 en schoten >= 12 | 37 / 68 / 14 / 48 % (prof 79 / 70 / 3 / 83) |
| 5 Schone wedstrijd | neutraal, overtredingen <= 10, rood <= 0, ongeslagen >= 3 | tegenstander amateur, overtredingen <= 12, rood <= 2, ongeslagen >= 2 | prof-speler: 5 / 0 / 3 / 61 % |
| 6 Verras de scout | neutraal | alleen tegenstander halfprof | 67 / 75 / 37 / 75 % (prof 91 / 74 / 58 / 93) |

Voor de aanpassing (tegenstander neutraal, oude drempels) haalde bij halfprof- en profspelers vrijwel niemand uitdaging 1, 4 en 5 (0-10%, uitzondering `man_om_man_433` en `testcase_a_433`, zie onder); uitdaging 2 slaagde alleen met prof (hoge druk 42%, positiespel 63%, laag blok 84%) en uitdaging 3 en 6 waren wisselend (3: counter 57% bij halfprof; 6: 33-85%).

Redenen: een neutrale tegenstander is sterker dan elk niveau (zie spelers.md), en de oude drempels voor goals en schoten lagen boven wat de voorbeeldtactieken halen (laag blok: de voorbeeldtactieken scoren ca. 0,8-1,1 per wedstrijd, niet 2,0).

## Bevindingen die niet via de JSON op te lossen zijn
- **Uitdaging 5 is zwak haalbaar.** Tegen `man_om_man_433` (halfprof) maken hoge druk en positiespel 22-27 overtredingen per wedstrijd en krijgen ze in 75-85% van de wedstrijden minstens één rode kaart; de passieve tactieken (counter, laag blok) zijn schoon maar verliezen bijna altijd (ongeslagen 0-1 van 30). Geen enkele voorbeeldtactiek haalt >= 60% tegen een halfprof-tegenstander, ook niet met ruime drempels (beste: 29% bij overtredingen <= 30, rood <= 5). Alleen met een profteam tegen een amateur-man-om-man haalt laag blok 61%. Een mengtactiek (druk met regels) is niet gemeten.
- **Afgebroken wedstrijden.** Meerdere wedstrijden eindigden als `Abandoned` (vermoedelijk te veel rode kaarten), vooral tegen `man_om_man_433` en met `man_om_man_433` of `testcase_a_433` als eigen tactiek. `ChallengeEvaluator.Play` gooit dan een `InvalidOperationException` ("werd niet afgerond"), dus de uitdaging mislukt met een fout in plaats van een uitslag. In de meting is een afgebroken wedstrijd als mislukte serie geteld. Dit is een simulatie- of scheidsrechterkwestie, niet aangepast.
- **Uitbuitende tactieken.** `man_om_man_433` en `testcase_a_433` als eigen tactiek maken tegen laag blok en counter 4-10 goals en 65-100 schoten per wedstrijd en slagen daardoor 94-100% bij uitdaging 4 (en 92-98% bij 3, 95-100% bij 6). Dat is een simulatiegat (man-marking-/testtactiek), geen drempelkwestie; met de bestaande criteriatypen (alleen "minstens"/"hoogstens" op gemiddelden) is het niet te scheiden van een goede tactiek zonder de voorbeeldtactieken ook te laten falen. Een echt nietsdoende of willekeurige tactiek is niet gemeten, omdat er geen zo'n tactiekbestand bestaat.
- Met 5 wedstrijden is de uitslag voor één seedreeks ruis: bij uitdaging 1 en 4 ligt de echte uitslag (seed 1..5) soms boven, soms onder de kans.
- Voorbeeldtactiek-eis (>= 60% voor minstens één voorbeeldtactiek): voldaan voor 1 (laag blok), 2, 3 (laag blok), 4 (positiespel), 6, en 5 alleen in de genoemde profteam-opstelling. Voor 1, 3 en 5 is dat laag blok, niet een van de drie hoofdvoorbeelden; met de drie hoofdvoorbeelden alleen slagen 2, 4 en 6 boven 60%.

## Uitdagingen live (Claude, 2026-10-16, branch feat/uitdagingen-live)

Probleem van de maker: bij uitdagingen speel je geen wedstrijden zelf. Nu kies je per uitdaging tussen twee knoppen: **Speel live** (primair) en **Simuleer alles** (het bestaande, snelle gedrag; ook 'Bekijk wedstrijd 1 live' blijft daar).

Live-stroom:
- `godot/ChallengeRun.cs` is het sessie-object (`GameSession.Challenge`): uitdaging, gekozen tactiek en niveau, de echt gekozen tegenstander (ook de adaptieve planner en `niveau_tegenstander`), de bereikte `ChallengeMatchResult`s en `InMatch`. Wedstrijd n gebruikt seed n (1..N, zoals `ChallengeEvaluator.Play`), speler thuis (`PlayerSide` Home, `InCompetition` uit).
- Main speelt de wedstrijd gewoon (pauzemenu, wissels, popups, snelheid, doorspoelen). Aan het eind bewaart Main `ChallengeEvaluator.ToResult(MatchStatistics.FromMatch(...))` in de run en toont `[Enter] volgende wedstrijd (n/N)` of `[Enter] toon resultaat`. Een gestaakte wedstrijd telt met de toegekende uitslag (zoals in de simulatie) en sluit de wedstrijd ook af.
- Tussenstand (Challenges, na elke wedstrijd): uitslag, per doel de regel uit `ChallengeEvaluator.Evaluate` plus `op koers` / `nog haalbaar` / `niet meer haalbaar`, de uitslagen tot nu toe. De nieuwe helper `ChallengeEvaluator.Standing(spec, resultaten)` (sim, getest in `ChallengeLiveTests`) geeft per doel `OnTrack` (nu gehaald) en `Possible`: of het doel nog gehaald kan worden als alle overige wedstrijden voor dat doel zo gunstig mogelijk verlopen (een bovengrens met extreme maar eindige waarden, geen voorspelling).
- Na de laatste wedstrijd evalueert Challenges met dezelfde `Evaluate`, met dezelfde weergave als het simulatiescherm; bij 'gehaald' gaat `ChallengeProgress.MarkDone` en komt de speelstijl in de geschiedenis, zoals bij de simulatie. Met dezelfde resultaten geeft live en simulatie hetzelfde oordeel (test).
- Esc in de wedstrijd of in het tussenscherm: eerst een bevestiging ('Uitdaging afbreken?'); afbreken bewaart niets. Een wedstrijd die je voortijdig verlaat telt niet; je kunt hem opnieuw spelen of de uitdaging afbreken. Terug naar het menu wist de run.
- Alleen het keuzescherm (tactiek, niveau) is tijdens een run vergrendeld; de uitdagingenlijst ook.

Nog niet / let op: de menu-optie 'Tegenstander wisselt' en `GameSession.OpponentSubstitutions` gelden ook voor live uitdagingen (de simulatie gebruikt ze niet); verwacht dus een licht ander resultaat als die aan staat. Een tussenstand wordt niet bewaard als je het spel sluit.

Debug (alleen debug/editor): `ZEUS_LIVE=1` start live meteen en gaat zelf door (met `ZEUS_FINISH=1 ZEUS_AUTOBACK=1` een volledige run zonder invoer), `ZEUS_LIVEFAKE="2-0,1-1"` toont een tussenstand met verzonnen uitslagen, `ZEUS_NOSAVE=1` laat voortgang en geschiedenis ongemoeid.

## Tips en aanbevolen start (feat/demo-tips)

Elke uitdaging heeft nu `tip_nl/tip_en` (hulp), `uitleg_*` (het doel in gewone taal), `start_tactiek` (bestandsnaam van een voorbeeldtactiek die slaagt) en `start_nl/start_en` (korte toelichting). De drempels zijn niet veranderd. Het uitdagingenscherm toont de tip en de aanbevolen start in een kader bovenaan de rechterkolom, naast Speel live; het tussenoverzicht herhaalt het kader zodra een doel 'niet meer haalbaar' is. De beweringen komen uit de meting hierboven (halfprof, 30 wedstrijden):
- 1 Houd de nul: laag blok 66%, hoge druk 1%, positiespel 0%, counter 18%. Start: Laag blok 4-4-2.
- 2 Beheers de bal: positiespel 68%, hoge druk 66%, counter 0%, laag blok 1%. Start: Positiespel 4-2-3-1.
- 3 Counter de bezitsploeg: laag blok 67%, counter 33%, hoge druk 45%, positiespel 0%. Start: Laag blok 4-4-2.
- 4 Scoor tegen het blok: positiespel 68%, laag blok 48%, hoge druk 37%, counter 14%. Start: Positiespel 4-2-3-1.
- 5 Schone wedstrijd: alleen laag blok met niveau Prof tegen de amateurploeg haalt ca. 61%; druk en positiespel geven 22-27 overtredingen en vaak rood. De tip raadt daarom af continu te drukken en wijst op niveau Prof.
- 6 Verras de scout: alleen de eerste poging is gemeten (positiespel); de tip is gebaseerd op het gedrag van de planner (zelfde stijl drie keer = voorbereid).
Test: `ChallengeTests.EveryChallengeHasATipAndStartInBothLanguages`.

## Uitdagingen 7-10, sterren en vrij te spelen tactieken (demo/f-challenges)

- Nieuw: 7 Win de tweede bal (tegen lange bal), 8 Neem het initiatief (tegen flankdruk), 9 Koel hoofd (tegen vleugelspel, met kaarten), 10 De topploeg (tegen hoge druk op prof). Drempels en meting: `docs/kalibratie.md`, sectie "Uitdagingsdrempels 7-10 en sterren".
- JSON: optioneel `ster_2` en `ster_3` (lijsten met criteria, zelfde vorm als `criteria`) bovenop de gewone doelen. `ster_3` zonder `ster_2` is ongeldig. `ChallengeEvaluator.Stars` telt de sterren (0 = niet gehaald).
- Voortgang (`user://zeus_progress.json`): elk item in `gehaald` krijgt `sterren` (beste poging blijft staan). Een item zonder `sterren` (oude save) telt als 1 ster; oudere versies negeren de extra sleutel. `ZEUS_NOSAVE=1` schrijft niets.
- Vrijspelen (`TacticUnlocks` in de sim): lange bal 4-4-2 bij 3, flankdruk 3-5-2 bij 7, vleugelspel 4-3-3 bij 9 sterren. In de tactiekkeuze van Uitdagingen en van de eigen ploeg in het Menu staan ze tot dan uitgeschakeld als "vergrendeld (X sterren)"; eigen tactieken zijn nooit vergrendeld. Competitie, editor, meter en selectie zijn niet vergrendeld. Besluit RC4 ronde 2: dat blijft zo (kleinste consistente oplossing). Vrijspelen is een beloning voor spelen met de tactiek, niet een verbod om hem te bekijken of na te bouwen; wie hem in de editor opent, kan hem toch al opslaan als eigen tactiek, dus verbergen daar zou schijnveiligheid zijn. Een tooltip op beide keuzelijsten (`challenge.locked_tip`, alleen zolang er iets vergrendeld is) legt dit uit.
- Het uitdagingenscherm toont sterren per uitdaging in de lijst, het totaal en de drempels per tactiek onder de lijst, de extra sterdoelen bij de doelen en na afloop de behaalde sterren en wat nieuw is vrijgespeeld.

## Uitdagingen 11-30 in thema's (pc 2, 2026-10-19, branch pc2/uitdagingen)

Van 10 naar 30, zelfde JSON-formaat en dezelfde `ChallengeSpec`/`ChallengeCriterion`; elke nieuwe uitdaging heeft `ster_2` en `ster_3`, NL en EN tekst, tip en aanbevolen start. Eén kleinste uitbreiding: optioneel `niveau_speler` (een waarde uit `SquadFactory.Levels`) legt het niveau van de eigen selectie vast; het keuzemenu staat dan vergrendeld op dat niveau met een regel eronder (`challenge.player_level`). Ontbreekt het veld, dan verandert er niets (test `PlayerLevelIsOptionalAndMustBeAKnownLevel`). Meetinstrument: `Zeus.Runner --challenge-calibrate` kreeg `--max-parallel N` (N seeds tegelijk, uitvoer blijft op seedvolgorde).

### Thema's
- **Met amateurs pressen (11-15)**, `niveau_speler` amateur: 11 Druk met amateurs (positiespel, amateur), 12 Jaag de lange bal (lange bal, amateur), 13 Amateurs tegen halfprofs (counter, halfprof: de bekerstunt), 14 Druk op de drukker (hoge druk, amateur), 15 Zondagmiddag (laag blok, amateur).
- **Standhouden (16-20)**, tegen profploegen, niet winnen maar niet verliezen: 16 Houd stand (positiespel), 17 Verdedig het resultaat (vleugelspel), 18 Niet verliezen (lange bal), 19 De muur (flankdruk), 20 De storm doorstaan (hoge druk, past zich aan).
- **Beroemde tactische vraagstukken met fictieve teams (21-30)**: 21 Het grendelslot van Lago Azzurro (catenaccio: laag blok, prof), 22 Het totaalvoetbal van Oranje-Zwart (positiespel, prof, past zich aan), 23 De lange bal van Wimble Town (lange bal, halfprof), 24 De tegendruk van Rode Muur (gegenpressing: hoge druk, prof), 25 De bus van Stad United (laag blok, halfprof), 26 Het tiki-taka van Blauwgranaat (positiespel, halfprof), 27 De mandekking van Eisenstadt (man-om-man, halfprof), 28 De vleugels van Costa Verde (vleugelspel, prof), 29 De counter van Noordstad (counter, prof), 30 Het drievoudige schild van Vestenburg (flankdruk, prof).
- **Tegen tien man (31-33)**, JSON `tegenstander_rood: true`: 31 Tien man achter de bal (laag blok, halfprof), 32 Overtal tegen de counter (counter, prof), 33 Tien man die blijven drukken (hoge druk, prof).
- **Een voorsprong verdedigen (34-36)**, JSON `start_voor`/`start_tegen` (stand bij de aftrap, jij eerst): 34 Verdedig de 1-0 (positiespel, prof), 35 De gevaarlijkste stand (2-0, hoge druk, prof), 36 Kom terug van 0-1 (counter, halfprof).
- Deze twee thema's vroegen een Match-wijziging; de maker zei ja. Kleinste vorm: `Match.StartScenario(homeGoals, awayGoals, sentOff)` in het nieuwe partial-bestand `sim/Zeus.Sim/Match.Scenario.cs` (Match.cs zelf ongewijzigd), alleen vóór de eerste stap: zet de stand (telt mee in statistiek, triggers en uitslag) en stuurt bij "tien man" de laatste veldspeler van die ploeg weg met een rode-kaart-event op tick 0, zodat scheidsrechterregels, wissels en statistiek hem als elke uitsluiting behandelen. `ChallengeSpec.ApplyScenario` past dit toe in `ChallengeEvaluator.Play` en in de live wedstrijd (`MatchDriver`, speler thuis). Geen start-minuut: een wedstrijd begint altijd bij 0' (de stand telt dus 90 minuten). Test: `ScenarioFieldsParseAndStartTheMatchWithAScoreAndTenMen`. Het uitdagingenscherm toont de beginstand of de tien man als regel onder het niveau (`challenge.start_score`, `challenge.opponent_ten`). Runner: `--start-for`, `--start-against`, `--opponent-sent-off`.

### Methode
Als bij 7-10: `--challenge-calibrate`, verse seeds 801-840 (40 wedstrijden per tactiek), speler thuis, tegenstander en niveaus zoals in de uitdaging (spelersniveau halfprof, of het vaste `niveau_speler`). Kans = bootstrap (3000 keer 5 uit 40) van het aandeel series met minstens 1 / 2 / 3 sterren; "series" = de 8 losse reeksen 801-805, ... Het spel speelt seed 1..5, dus de echte uitslag kan afwijken. Bij 20 en 22 (past zich aan) is alleen de eerste poging gemeten. Meetscript (bootstrap) staat niet in de repo. Doel: minstens één voorbeeldtactiek >= 60% voor 1 ster, geen tactiek boven ca. 90%, en 3 sterren zelden.

### Meting
Kans op minstens 1 / 2 / 3 sterren per voorbeeldtactiek (halfprof-speler, of het vaste `niveau_speler`), 40 wedstrijden per tactiek, seeds 801-840, bootstrap 3000 x 5. Volgorde: hoge druk / positiespel / counter / laag blok. Vet = referentie (`start_tactiek`). Drempels zijn na de eerste meting bijgesteld (eerste versie: 13, 15, 22, 28 waren 94-100%, 12, 19, 30 waren 0-12%); de tabel toont de eindstand.

| Uitdaging | Tegenstander | 1 ster | 2 sterren | 3 sterren | Hoge druk | Positiespel | Counter | Laag blok |
|---|---|---|---|---|---|---|---|---|
| 11 Druk met amateurs (amateur) | positiespel, amateur | ongeslagen >= 3, bezit >= 50 | winst >= 2 | bezit >= 55 | **73 / 43 / 40** | 18 / 12 / 0 | 0 | 0 |
| 12 Jaag de lange bal (amateur) | lange bal, amateur | bezit >= 55, ongeslagen >= 3 | schoten tegen <= 18 | winst >= 3 | 40 / 0 / 0 | **71 / 7 / 2** | 0 | 0 |
| 13 Amateurs tegen halfprofs (amateur) | counter, halfprof | ongeslagen >= 3, tegen <= 1,2 | winst >= 3 | winst >= 4, tegen <= 0,8 | 30 / 17 / 2 | **72 / 48 / 14** | 51 / 18 / 2 | 99 / 82 / 48 |
| 14 Druk op de drukker (amateur) | hoge druk, amateur | winst >= 2, voor >= 1,0 | winst >= 3 | tegen <= 1,0 | **60 / 27 / 12** | 50 / 19 / 7 | 34 / 11 / 8 | 62 / 28 / 17 |
| 15 Zondagmiddag (amateur) | laag blok, amateur | schoten >= 20, winst >= 3 | winst >= 4 | voor >= 2,5 | 6 / 1 / 0 | **76 / 47 / 15** | 0 | 40 / 13 / 0 |
| 16 Houd stand | positiespel, prof | ongeslagen >= 3, tegen <= 1,6 | ongeslagen >= 4 | tegen <= 1,2 | **61 / 39 / 30** | 8 / 2 / 1 | 20 / 4 / 4 | 11 / 2 / 1 |
| 17 Verdedig het resultaat | vleugelspel, prof | ongeslagen >= 2, schoten tegen <= 16 | ongeslagen >= 3 | tegen <= 1,0 | 21 / 14 / 5 | 28 / 15 / 2 | 2 / 1 / 0 | **82 / 64 / 29** |
| 18 Niet verliezen | lange bal, prof | ongeslagen >= 3 | ongeslagen >= 4 | tegen <= 1,0 | 12 / 1 / 0 | 8 / 1 / 0 | 0 | **69 / 32 / 13** |
| 19 De muur | flankdruk, prof | tegen <= 2,4, ongeslagen >= 2 | ongeslagen >= 3 | tegen <= 1,8 | 31 / 9 / 5 | 21 / 6 / 4 | 0 | **66 / 43 / 27** |
| 22 Totaalvoetbal (past zich aan) | positiespel, prof | ongeslagen >= 3, bezit >= 50 | ongeslagen >= 4 | winst >= 3 | **79 / 43 / 18** | 0 | 0 | 0 |
| 28 De vleugels | vleugelspel, prof | schoten tegen <= 16, ongeslagen >= 3 | ongeslagen >= 4 | doelsaldo >= 0,2 | 13 / 4 / 2 | 15 / 4 / 3 | 1 / 0 / 0 | **64 / 31 / 26** |
| 30 Het drievoudige schild | flankdruk, prof | ongeslagen >= 2, voor >= 1,2 | winst >= 2 | doelsaldo >= 0 | 27 / 4 / 2 | 10 / 5 / 0 | 0 | **67 / 41 / 17** |
| 20 De storm doorstaan (past zich aan) | hoge druk, prof | ongeslagen >= 2, tegen <= 1,8 | ongeslagen >= 3 | doelsaldo >= 0 | 51 / 37 / 25 | 44 / 28 / 10 | 51 / 34 / 14 | **70 / 59 / 45** |
| 21 Het grendelslot | laag blok, prof | ongeslagen >= 3, voor >= 1,2 | winst >= 2 | winst >= 3 | 31 / 22 / 7 | **65 / 44 / 17** | 0 | 22 / 19 / 8 |
| 23 De lange bal van Wimble Town | lange bal, halfprof | winst >= 2, schoten tegen <= 18 | winst >= 3 | tegen <= 1,0 | 1 / 0 / 0 | 0 | 0 | **63 / 36 / 23** |
| 24 De tegendruk | hoge druk, prof | tegen <= 2,0, voor >= 1,2 | ongeslagen >= 3 | winst >= 2 | 41 / 32 / 28 | 26 / 21 / 10 | 26 / 20 / 10 | **74 / 63 / 49** |
| 25 De bus van Stad United | laag blok, halfprof | winst >= 2, schoten >= 20 | winst >= 3 | voor >= 2,0 | 17 / 3 / 2 (eerste meting) | **70 / 34 / 26** | 0 | 43 / 24 / 11 |
| 26 Het tiki-taka | positiespel, halfprof | bezit >= 50, winst >= 2 | winst >= 3 | tegen <= 1,0 | **70 / 31 / 15** | 46 / 26 / 10 | 0 | 0 |
| 27 De mandekking | man-om-man, halfprof | ongeslagen >= 2, rood <= 2 | ongeslagen >= 3 | rood <= 0 | **69 / 44 / 6** | 66 / 32 / 14 | 0 | 45 / 14 / 9 |
| 29 De counter | counter, prof | bezit >= 55, tegen <= 1,2 | ongeslagen >= 3 | winst >= 3 | 31 / 31 / 13 | **78 / 72 / 41** | 1 / 1 / 0 | 0 |

Opvallend: tegen een profploeg met de bal (16, 22) houdt hoge druk het best stand en geeft een laag blok juist meer weg; flankdruk 3-5-2 op prof is de zwaarste tegenstander in het spel (alle tactieken 2,2-3,8 tegen); laag blok tegen counter (13) is het bekende sim-gat (99%). Counter 5-3-2 tegen een amateur laag blok (15) gaf 54 rode kaarten in 40 wedstrijden (niet in de drempels verwerkt, wel een sim-kwestie).

Ook opvallend: tegen hoge druk op prof (20, 24) en tegen een prof-laag-blok (21) is counter 5-3-2 kansloos (0%; tegen laag blok prof 0,12-3,23 met 64 rode kaarten in 40 wedstrijden) en tegen mandekking (27) verliest counter alles (0-39, 72 schoten tegen per wedstrijd): sim-gaten, al bekend uit `kalibratie.md`, hier niet opgelost. Van de drie hoofdvoorbeelden is in 11-30 steeds minstens één tactiek boven 60%, behalve 17, 18, 19, 23, 28 en 30 waar alleen laag blok 4-4-2 slaagt (zoals eerder bij 1, 3 en 7).

Scenario-uitdagingen (`--opponent-sent-off`, `--start-for/--start-against`, `--start-minute`; zelfde seeds en methode):

| Uitdaging | Scenario | 1 ster | 2 sterren | 3 sterren | Hoge druk | Positiespel | Counter | Laag blok |
|---|---|---|---|---|---|---|---|---|
| 31 Tien man achter de bal | laag blok halfprof, rood bij aftrap | winst >= 3, voor >= 2,0 | winst >= 4 | voor >= 3,0 | 39 / 27 / 3 | **63 / 56 / 12** | 1 / 1 / 0 | 80 / 76 / 34 |
| 32 Overtal tegen de counter | counter prof, rood | winst >= 3, tegen <= 1,0 | winst >= 4 | voor >= 2,0 | 12 / 3 / 2 | **77 / 42 / 4** | 48 / 16 / 4 | 77 / 44 / 23 |
| 33 Tien man die blijven drukken | hoge druk prof, rood | ongeslagen >= 2, winst >= 1 | ongeslagen >= 3 | winst >= 2 | 6 / 4 / 0 (eerste meting) | 12 / 5 / 1 (eerste meting) | 47 / 23 / 10 | **78 / 50 / 40** |
| 34 Verdedig de 1-0 | positiespel prof, 1-0 vanaf 70' | winst >= 4, tegen <= 0,4 | winst >= 5 | tegen <= 0,2 | 60 / 22 / 22 | **76 / 36 / 34** | 38 / 10 / 10 | 50 / 18 / 15 |
| 35 De gevaarlijkste stand | hoge druk prof, 2-0 vanaf 60' | winst >= 5, tegen <= 0,8 | tegen <= 0,4 | tegen <= 0,2 | **78 / 24 / 6** | 55 / 34 / 15 | 44 / 26 / 14 | 64 / 43 / 20 |
| 36 Kom terug van 0-1 | counter halfprof, 0-1 vanaf 60' | ongeslagen >= 2 | ongeslagen >= 3 | winst >= 2 | 72 / 36 / 5 | 68 / 32 / 11 | 47 / 15 / 4 | **83 / 49 / 17** |

Wat de scenario's laten zien: met tien man tegenover zich maakt positiespel 2,15-0,28 tegen een halfprof laag blok (3,6 schoten tegen); tien profs met hoge druk verslaan een halfprof-elftal nog steeds (W13 van 40 voor laag blok). Een wedstrijd vanaf de 70e of 60e minuut heeft 3-8 schoten per ploeg; met een voorsprong wint vrijwel elke tactiek, vandaar de strenge drempels (4-5 overwinningen, tegen <= 0,4-0,8). Terugkomen van 0-1 in een half uur lukt zelden (winst 4-7 van 40), dus de eis is "niet verliezen". Energie start vol in de 60e/70e minuut (geen eerste helft gespeeld); zie `Match.StartScenario`.

Totaal: 10 + 26 uitdagingen, 96 sterren (vrijspelen bij 3/7/9 blijft).

### Uitdaging van de dag (pc 2, 2026-10-19)
`sim/Zeus.Sim/DailyChallenge.cs`: onderaan de lijst staat onder de kop "Vandaag" één uitdaging die alleen uit de datum volgt (`dag_JJJJMMDD`), dus voor iedereen dezelfde: tegenstander uit de zeven voorbeeldtactieken, niveau, twee doelen uit een vaste lijst (plus een strenger tweede-sterdoel) en in twee van de vijf dagen een scenario (tien man, of 1-0 voor vanaf de 65e minuut). Bewust niet geijkt (de tip zegt dat). Voortgang en sterren worden onder het dag-id bewaard; het sterrentotaal in het scherm telt die van vandaag mee (+1 of +2), de vrijspeeldrempels niet anders. Deelbaar via de uitdagingscode (branch `pc2/vriendenmodus`); na samenvoegen moet `Friends.PasteChallenge` een `dag_`-id via `DailyChallenge.FromId` opzoeken (staat op het bord). Test: `DailyChallengeTests`.


### Herijking op HEAD 24f2f48 (2026-10-27, punt 25 uit de review)
Uitdagingen 11-36 opnieuw gemeten met de nieuwe defaults (`PressJockeyMeters` 4, `CautionedAvoidsBehind`): `scripts/challenge_stars.py --run`, seeds 801-840, halfprof tenzij `niveau_speler`, bootstrap 3000x. Cellen = kans in procenten op 1 / 2 / 3 sterren. Drempels zijn nog **niet** aangepast.

| uitdaging | hoge_druk_433 | positiespel_4231 | counter_532 | laag_blok_442 |
| 11_druk_met_amateurs | 92/71/62 | 48/33/0 | 0/0/0 | 0/0/0 |
| 12_jaag_de_lange_bal | 40/0/0 | 55/0/0 | 0/0/0 | 0/0/0 |
| 13_amateurs_tegen_halfprof | 32/8/1 | 77/44/14 | 50/32/8 | 98/79/44 |
| 14_druk_op_de_drukker | 52/24/15 | 31/12/11 | 53/23/11 | 56/31/22 |
| 15_zondagmiddag | 13/4/1 | 36/11/4 | 0/0/0 | 38/14/0 |
| 16_houd_stand | 53/28/25 | 27/9/6 | 8/2/2 | 21/7/5 |
| 17_verdedig_het_resultaat | 21/15/6 | 52/40/25 | 2/1/0 | 74/56/15 |
| 18_niet_verliezen | 24/6/1 | 40/13/4 | 0/0/0 | 41/13/6 |
| 19_de_muur | 35/12/6 | 23/8/5 | 0/0/0 | 50/30/18 |
| 20_de_storm_doorstaan | 34/24/19 | 50/28/13 | 55/45/30 | 64/57/41 |
| 21_het_grendelslot | 27/11/3 | 44/40/23 | 0/0/0 | 6/2/0 |
| 22_totaalvoetbal | 63/29/8 | 0/0/0 | 0/0/0 | 0/0/0 |
| 23_de_lange_bal_van_wimble | 0/0/0 | 0/0/0 | 0/0/0 | 62/38/19 |
| 24_de_tegendruk | 37/26/25 | 17/13/11 | 47/42/36 | 57/53/41 |
| 25_de_bus_van_stad_united | 24/8/3 | 82/61/43 | 0/0/0 | 8/5/3 |
| 26_tiki_taka | 62/28/22 | 58/32/17 | 0/0/0 | 0/0/0 |
| 27_man_tegen_man | 56/22/17 | 77/44/42 | 3/0/0 | 31/7/7 |
| 28_de_vleugels | 15/7/5 | 40/23/18 | 1/0/0 | 56/29/27 |
| 29_de_counter | 27/24/10 | 64/62/27 | 5/5/1 | 0/0/0 |
| 30_het_drievoudige_schild | 34/14/5 | 7/2/0 | 1/0/0 | 56/37/17 |
| 31_tien_man_achter_de_bal | 49/27/7 | 24/21/1 | 12/10/0 | 70/66/24 |
| 32_overtal_tegen_de_counter | 9/3/2 | 71/38/16 | 43/16/6 | 87/58/40 |
| 33_tien_man_die_drukken | 82/57/49 | 75/48/39 | 57/39/17 | 73/59/33 |
| 34_verdedig_de_1_0 | 81/46/44 | 37/11/10 | 47/17/16 | 47/16/14 |
| 35_de_gevaarlijkste_stand | 63/38/18 | 35/22/13 | 36/17/7 | 43/27/12 |
| 36_kom_terug | 62/28/11 | 73/39/6 | 43/14/5 | 94/76/24 |
