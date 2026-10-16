# AI-diagnose: terugspelen bij een open kans en stilstaande spelers

Gemeten op 2026-10-16, branch `fix/ai-diagnose`, basis `b4f1f23`. Alleen diagnose: er is niets in `sim/Zeus.Sim` gewijzigd. Het meetgereedschap is `sim/Zeus.Runner/AiDiagnose.cs` (`--ai-diagnose`, hieronder "de meter"). De ruwe uitvoer van de gebruikte runs staat niet in git; ze zijn met het commando onderaan reproduceerbaar.

Aanleiding (waarneming van de maker na handmatig spelen): (1) "voor een open goal spelen ze hem terug", (2) "spelers blijven lang stilstaan". Beide zijn met cijfers bevestigd en hebben een duidelijke oorzaak in de besliskaders van `PassingBrain` en in de statische aanvalsformaties. De oorzaak is niet de beslissingsfrequentie.

## 1. Methode

- **Opzet:** zes voorbeeldtactieken (`hoge_druk_433`, `positiespel_4231`, `counter_532`, `laag_blok_442`, `man_om_man_433`, `testcase_a_433`), tactiek i thuis tegen tactiek i+1 uit (rotatie, dus zes ongelijke paren), seeds 1-4, hele wedstrijden (24 wedstrijden, 1,30 miljoen lopende ticks van 0,1 s), standaard `MatchConfig`, neutrale spelers (zoals de demo met niveau "neutraal"). Beide teams worden gemeten. Sequentieel, één proces; per run ca. 2,5 minuten.
- **Controle op spelersniveau:** dezelfde meting met `--level amateur` en `--level halfprof` (squads uit `squads/`), 4 seeds. De conclusies zijn gelijk (zie 2.5 en 3.4).
- **Open kans (a):** per tick met een baldrager (veldspeler, spel loopt) die binnen 25 m van het doelmidden staat en bij wie in het echte spelbeeld geen veldtegenstander binnen 2,0 m van de schietbaan (baldrager naar doelmidden) staat en geen binnen 2,5 m van hemzelf. De keeper telt niet mee als blokkade (hij staat bijna altijd op de baan; zie 2.4). Aaneengesloten open ticks van één baldrager vormen een **episode**; de uitkomst van de episode is het eerstvolgende Pass- of Shot-event van die speler (of "niet meer open", "bal verloren"). Een pass telt als **terug** als de ontvanger meer dan 1 m dichter bij het eigen doel staat dan de passer.
- **Vers beslismoment:** de intentie in `Match.IntentOf` is tot 0,3 s oud (beslisinterval) en de baldrager beweegt intussen. De tabel "vers" telt daarom alleen ticks waarop de baldrager net besliste (`Perception.Tick == Tick - 1`) en berekent xG en houdtijd uit dat beslismoment (`HeldBallSeconds`, positie in de waarneming). Alle percentages voor de beslissing komen uit die tabel.
- **Stilstaan (b):** per speler en tick (lopend spel, niet de eerste 10 s, niet de eerste 5 s na een herstart, geen uitgesloten spelers) is de speler "stil" bij snelheid < 0,3 m/s. Uitgesplitst naar rol, relatie tot de baldrager (baldrager / ploeggenoot / tegenstander / losse bal), fase (`PhaseOf`), intentie, afstand tot de doelpositie (`TargetOf`), instructies, zichtbaar zijn van de bal en tactiek. Runs van aaneengesloten stilstand (>= 3 s, >= 60 s) en voorbeeldtoestanden na 10 s stilstand zijn apart vastgelegd. Keepers zitten in de totaalcijfers (62% stil) maar niet in de runs.
- **Referentie en grenzen:** er staat geen gemeten verdeling van speelsnelheden in de repo, dus "58% stil" is gemeten, niet getoetst aan echt voetbal. De drempel 0,3 m/s en de keuze voor 2 m baan zijn mijn definities. Vier seeds zijn genoeg voor de richting en de rangorde, niet voor kleine effecten. De what-if-proeven in 4.2 gebruiken 2 of 4 seeds.

Commando (vanuit `sim`, na `dotnet build Zeus.Runner -c Release`):

```
dotnet Zeus.Runner/bin/Release/net8.0/Zeus.Runner.dll --ai-diagnose --seeds 4 --tactics-dir ../tactics [--level halfprof --squads-dir ../squads] [--only <tactiek>] [--dump-ticks 3000,4000] --out ai.txt
```

## 2. Probleem 1: terugspelen bij een open kans

### 2.1 Wat er gebeurt

Over 24 wedstrijden: 2594 open-kansepisodes (108 per wedstrijd, beide teams). Uitkomst:

| Uitkomst | Episodes | Aandeel |
|---|---:|---:|
| Schot | 1034 | 39,9% |
| Niet meer open (gedrukt of weggelopen, geen trap) | 1058 | 40,8% |
| Pass terug (veldspeler) | 469 | 18,1% |
| Pass vooruit/zijwaarts | 32 | 1,2% |
| Bal verloren | 1 | 0,0% |

De 469 terugpasses komen vrijwel allemaal uit de afstandsband waar de schotdrempel niet gehaald wordt: 397 met minimale afstand 18-25 m, 72 met 12-18 m, 0 binnen 12 m. Gemiddeld begon zo'n episode op 24,5 m, de pass viel op 20,9 m en na 3,3 s balbezit. Met squads: amateur 507 terugpasses van 2275 episodes (22,3%), halfprof 466 van 2454 (19,0%).

### 2.2 Beslissing per afstandsband (vers beslismoment, 11147 beslissingen)

| Afstand | Houdtijd | xG | n | Wat de baldrager kiest |
|---|---|---|---:|---|
| < 12 m | < 2,4 s | >= 0,08 | 635 | 61% loopt naar het doel, **35% loopt van het doel af**, 3% blijft staan |
| < 12 m | >= 2,4 s | >= 0,08 | 114 | 100% schiet |
| 12-18 m | < 2,4 s | >= 0,08 | 1878 | 100% loopt naar het doel (**mag niet schieten**) |
| 12-18 m | >= 2,4 s | >= 0,08 | 622 | 100% schiet |
| 12-18 m | >= 2,4 s | < 0,08 | 125 | 58% blijft staan (Scan), 38% pass terug |
| 18-25 m | < 2,4 s | < 0,08 | 3678 | 98% loopt naar het doel |
| 18-25 m | >= 2,4 s | < 0,08 | 3396 | **80% blijft staan met de bal (Scan)**, 16% pass terug, 4% pass vooruit |
| >= 25 m | >= 2,4 s | < 0,08 | 240 | 83% Scan, 13% pass terug |

De xG-drempel 0,08 valt voor een schot recht voor het doel op 18,1 m (op 6 m zijlijn 17,9 m; `ShotModel.GoalProbability`). In de houdtijdvenster zijn dus 85% van de schietbare beslissingen binnen 12 m en 75% van die op 12-18 m verboden door de timer.

### 2.3 Top 3 oorzaken (met bewijs)

**1. De houdtijd blokkeert het schot, en binnen 11 m loopt de baldrager terug naar zijn formatieplek (`Passing.cs` r. 366-374).** Het schot (`if (canShoot) return Shoot();`, r. 378) staat ná het blok `if (view.HeldBallSeconds < holdSeconds)` (2,4 s). In dat blok dribbelt hij naar het doel tot `DribbleStopGoalDistance` (11 m); binnen 11 m valt hij door naar `_formation.Decide`, dat hem naar zijn **formatiepositie** stuurt.
- Bewijs: binnen 11 m en houdtijd < 2,4 s: 564 ticks "MoveToPosition van het doel af" tegen 245 naar het doel toe (en 27 Press). Bij de schoten die wel vielen verstrijkt mediaan 0,8 s (gemiddeld 0,9 s; p90 1,5 s) tussen het eerste schietbare tick en het schot, en de gemiddelde houdtijd bij de trap is 2,7 s.
- Van de episodes die ooit xG >= 0,08 haalden (1272), eindigden er 193 (15%) zonder schot ("niet meer open") en 41 (3%) in een terugpass.
- Voorbeeld (uit de meter): tick 292, Home/RB `hoge_druk_433`, 14,8 m van het doel, xG 0,075, houdtijd 2,5 s, terug op LW op 2,2 m.

**2. Na de houdtijd is er geen "doorlopen"; in de band 18-25 m (xG < 0,08) blijft hij staan of speelt terug.** Het dribbel- en meedraagblok (`DribbleMaxGoalDistance` 35 m, `CarryMaxGoalDistance` 45 m) werkt alleen binnen `HeldBallSeconds < holdSeconds`. Daarna volgen alleen steekbal, schot (xG >= 0,08) of pass. Is er geen pass met score > 0, dan wordt het `Scan` (staan) tot `HoldSeconds + ForceSeconds` (4,4 s) en dan de beste pass, ook achterwaarts.
- Bewijs: tabel 2.2, rij 18-25 m: 80% Scan en 16% pass terug; 85% (397/469) van de terugpass-episodes zit in die band.
- Onderliggend: de schotdrempel is een pure xG-drempel (kalibratiedoel: ca. 13 schoten per team, `docs/kalibratie.md` stap 4 en ronde 6); het model kent geen "loop nog 5 m door tot de schotband".

**3. Passkeuze en schotkeuze zijn een vaste volgorde zonder waardevergelijking; elke vrije achterwaartse teamgenoot krijgt score > 0 (`Passing.cs` r. 146-151, 376-380).** Score = 1 + 0,06 x vooruit - 0,015 x afstand - 0,9 x risico. Een vrije ontvanger 10 m achter de baldrager heeft 1 - 0,6 - 0,15 = 0,25 > 0. Pas en schot worden niet tegen elkaar gewogen: de volgorde is steekbal > schot (xG >= 0,08) > pass. Zodra de baldrager net onder de drempel zit, wint dus een achterwaartse veilige pass van "twee passen verder lopen".
- Bewijs: 469 terugpasses tegen 32 vooruit-/zijwaartse passes in open episodes; dit patroon is gelijk met amateur- en halfprofsquads.
- Opmerking over het begrip "open doel": de keeper komt in de schotbeslissing alleen voor als richtpunt (`Shoot()`). In de meting staat de keeper in 14 van 2594 episodes (0,5%) buiten de baan (> 4 m opzij of > 8 m van het doel). Een echt leeg doel komt in deze sim praktisch niet voor; wat de maker ziet is een vrije baan op 18-25 m (oorzaak 2) of een schot dat 1-2 s te laat valt (oorzaak 1). Het xG-model kent geen keeperpositie.

Niet de oorzaak: steekbal-voorrang (4 ticks in de beslissingstabel), `dribbel_in` (verwaarloosbaar), pass- of schotuitvoering (alleen draaisnelheid vóór de trap, hooguit enkele ticks).

## 3. Probleem 2: spelers blijven stilstaan

### 3.1 Omvang

Van alle speler-ticks in lopend spel is 58,2% stil (14,98 van 25,76 miljoen; 57,2% met amateur, 59,9% met halfprof). Afkomstig van de uitsplitsing:

| Splitsing | Stil | Noot |
|---|---:|---|
| Intentie `HoldPosition` | 8,74 M ticks (34% van alle ticks), 99,8% stil | op doelpositie aangekomen |
| Intentie `Scan` | 3,52 M ticks (13,7%), 97,6% stil | bal niet gezien en op doelpositie |
| Intentie `MoveToPosition` | 15,8% stil | |
| Fase Opbouw / Aanval | **81,9% / 61,3%** | (Verdedigen 47,2%, Balverlies/Balwinst 15%) |
| Relatie: ploeggenoot van baldrager | **71,5%** | tegenstander van baldrager 46,0%; baldrager 36,9% |
| Rol (42-64%) | spits 44,6%, targetman 41,7% | back 61,2%, centrale verdediger 62,0% |
| Tactiek | man_om_man 71,7%, laag_blok 62,5% | hoge_druk 45,5% |
| Doelpositie | op doel (< 0,5 m): 87,2% stil; ver (> 3 m): 1,7% | de spelers staan dus stil omdat ze zijn aangekomen, niet omdat ze vastlopen |
| Afstand tot de bal | < 15 m: 39,5%; > 50 m: 69,3% | |

Stilstandsruns >= 3 s (runs lopen door over herstarts heen, want die ticks tellen niet mee): gemiddeld 9-17 s per rol (back 16,0 s, centrale verdediger 16,5 s); 1154 runs >= 60 s in 24 wedstrijden (neutraal; 942 halfprof; met amateur slechts 10).

### 3.2 Top 3 oorzaken (met bewijs)

**1. De aanvalsformaties zijn statisch.** In alle zes voorbeeldtactieken hebben `opbouw` en `aanval` geen `volg_bal`; alleen `verdedigen` volgt de bal (`[0.3, 0.4]` of vergelijkbaar, met `BallFollowGain` 0,5; `Match.TargetFor`; `man_om_man_433` heeft ook daar geen `volg_bal`). Een ploeggenoot zonder bal heeft dus een vaste doelpositie, komt aan, en `FormationBrain` geeft `HoldPosition`. De enige beweging zonder bal is `RunInBehind` voor maximaal twee aanvallers per 30 s (`AttackingRunConfig`): 77711 ticks, 0,3% van alles.
- Bewijs: fase Opbouw 81,9% stil (7,69 M van 9,39 M ticks), ploeggenoot van baldrager 71,5%.
- What-if (alleen tactiekdata, geen code): `volg_bal [0.3, 0.4]` voor alle veldspelers in `opbouw` en `aanval` van de zes tactieken, 2 seeds: stil **58,1% naar 36,5%**, Opbouw 81,8% naar 37,4%, ploeggenoot 71,2% naar 34,7%, runs >= 60 s **610 naar 12**. Neveneffect: schoten 679 naar 796 (+17%), onderscheppingen +14%, fouten -22%, goals 76 naar 79 (2 seeds, ruis).

**2. Spelers die de bal niet kennen, staan en draaien alleen langzaam.** 34,9% van de speler-ticks heeft geen zichtbare bal (9,00 M van 25,76 M) en 24,0% kent hem zelfs niet uit het geheugen (6,18 M; `BallMemorySeconds` 1,0 s; kijkhoek 120 graden). Dan geeft `Match.BallRel` "geen positie bekend" terug: `(0.5, 0.5)`, dus het basisformatiepunt. Staat de speler daar al, dan is zijn intentie `Scan` (draait 0,4 x draaisnelheid per tick) en blijft hij staan. 97,6% van de Scan-ticks is stil.
- Dode-balproblemen: een losse bal in rust zonder baldrager komt 34077 ticks voor (ca. 142 s per wedstrijd; 92k ticks met amateursquads), met 3 aaneengesloten stilstanden >= 30 s (8 met amateur). Voorbeeld uit de meter: `positiespel_4231` tegen `counter_532`, seed 2, tick 13784: bal in rust op (-50,4; 26,7), thuis 3 van 11 spelers blind, uit alle 11 `Scan(blind)`: geen enkele veldspeler beweegt. Dit is het in `docs/kalibratie.md` (ronde 6) genoemde "dode bal"-geval; het komt nog steeds voor, hier met een bal tegen de achterlijn.
- Teamvriezers (<= 3 van 20 veldspelers bewegen) >= 30 s: 8 keer neutraal, 18 keer halfprof; totaal 217k ticks (16,8% van het lopende spel, ca. 15 minuten per wedstrijd met hooguit 3 bewegende veldspelers; dit omvat ook ballen in de lucht en dode bal).

**3. De baldrager staat zelf stil.** Van alle baldragerticks (629k) is 17,4% `HoldPosition` en 12,5% `Scan` (samen 30%): hij loopt in de houdtijd van 2,4 s alleen naar het doel als hij binnen 45 m van het doel staat; anders gaat hij naar zijn formatieplek en wacht. Onder druk zonder passoptie met score > 0 blijft hij `Scan` tot 4,4 s.
- Voorbeeld (meter): `testcase_a_433` tegen `hoge_druk_433`, seed 4: vanaf tick 2084 staan thuis minstens vier veldspelers (hangende spits, RB, LC, RC) 313-329 s onafgebroken stil in `Opbouw`; bal heen en weer bij LB onder een drietal drukzetters op hetzelfde punt (0,0; -25,5), carrier LB `Scan`, houdtijd 3,9 s, 10 teamgenoten zichtbaar maar geen optie met score > 0.

Bijkomend (kleiner): `blijf_breed` 85,3% stil, `dribbel_in` 87,8%, `loop_zestien_in` 71,0%, `dek_man` 64,5% (man_om_man heeft de hoogste stilstand door balgeoriënteerd dekken, `MarkBallRadius` 15 m); `vrije_man_zoeken` (68,0%) is nog uitgeschakeld (`EnableFindSpace`), dus geen effect. Die instructies leggen een statische plek vast; ze veroorzaken het stilstaan niet, ze versterken het.

### 3.3 Is het beslisinterval te lang?

Nee. 98,9% van de opeenvolgende beslissingen ligt precies 3 ticks (0,3 s) uit elkaar (9,31 M van 9,41 M; 99,4% als de eerste beslissing na een herstart wordt weggelaten); 0,4% ligt 4 ticks uit elkaar (vermoeidheid, +0,1 s). Bij de aanwezige beslisgrens 0,3 s is een speler die iets ziet nooit langer dan 0,3 s blind voor zijn eigen volgende beslissing. Squadattributen verlengen dit niet zichtbaar (amateur: dezelfde verdeling; `ScanIntervalSeconds >= 0,15 s`). De langere intervallen (31-34 ticks) zijn de eerste beslissing na een herstart. Het stilstaan is dus geen reactie-vertraging maar een gebrek aan doel.

### 3.4 Dode zones in formatie en instructies

Geen speler loopt vast op een onbereikbaar punt: slechts 1,7% van de ticks is stil terwijl de speler meer dan 3 m van zijn doelpositie staat. De dode zone is de formatie zelf: een speler zonder bal en zonder instructie die een bewegend doel geeft, staat op zijn tekenpunt. De rij `ballDist > 50 m` (69,3% stil) en `Opbouw` (81,9%) laten zien dat dat vooral aan de bal-vrije kant van het veld gebeurt.

## 4. Voorgestelde kleine ingrepen

Alle ingrepen vereisen een nieuwe kalibratiecontrole (`scripts/calibration_run.py`, symmetrisch paar `laag_blok_442` en het ongelijke paar) op: schoten per team (ca. 13), goals per schot (ca. 10-12%), passsucces en uit-passes (ca. 2%), buitenspel, onderscheppingen, inworpen. Ze zijn hier alleen gemeten met de meter; er is niets gecommit in de simulatie.

### 4.1 Voorstellen

| Nr | Ingreep | Verwacht effect | Risico voor kalibratiedoelen |
|---|---|---|---|
| A | **Vroeg schot toelaten**: in `PassingBrain.Decide` een tak vóór het houdtijdblok: `canShoot` en xG >= 0,15 (ca. 13 m) en vrije baan (geen veldtegenstander binnen 2 m van de baan of 2,5 m van de schutter) schiet direct. | Episodes die xG >= 0,08 haalden maar niet schoten: 193 naar 73; wachttijd op een schot daalt (mediaan 0,8 s blijft, gemiddelde 0,90 naar 0,75 s). | Gemeten: schoten 1319 naar 1332, goals 148 naar 148, buitenspel +11% (256 naar 285), fouten +6%; 4 seeds. Een variant zonder xG-grens van 0,15 (elk schot met xG >= 0,08 direct) gaf op 2 seeds 76 naar 47 goals bij 679 naar 647 schoten (kwaliteit daalt: schutters stoppen met naar 11 m lopen), dus niet gebruiken. |
| B | **Niet terugwandelen binnen 11 m**: in het houdtijdblok bij `toGoal <= DribbleStopGoalDistance` blijven staan op de plek (of `Scan`/schietklaar), niet naar de formatieplek. | Verwijdert de 564 ticks "van het doel af" (35% van de beslissingen < 12 m). Klein, deterministisch. | Zeer laag; raakt alleen de houdtijd van baldragers binnen 11 m. |
| C | **Doorlopen na de houdtijd** in de band 12-25 m: geen xG-schot en geen vooruit-pass >= `GoodPassScore` (1,5): `Carry`-tak (naar het doel, 5 m) ook voor `HeldBallSeconds >= holdSeconds`, tot `canShoot`. Ook geen achterwaartse pass boven de 30 m van het doel binnen 25 m als er geen druk is. | Verwacht: terugpasses 469 naar < 100, Scan in de band 18-25 m (80%) naar < 20%. | Niet getest. Hoger risico: meer schoten van 14-18 m (nu 622 schoten bij xG >= 0,08), meer duels. Vraagt een kalibratieronde op `CarryMaxGoalDistance`/`ClearBelowScore`. |
| D | **Aanvalsfases laten de bal volgen**: `volg_bal` in `opbouw` en `aanval` van de voorbeeldtactieken (tactiekdata, geen code), of `BallFollowGain` voor aanvalsfases apart. | Stilstand 58,1% naar 36,5%, runs >= 60 s 610 naar 12 (2 seeds). | Schoten +17%, onderscheppingen +14%, fouten -22% op 2 seeds: de aantallen schuiven mee, dus nieuwe kalibratie nodig. Zwaar bij `lijnhoogte`/compactheid in aanvalsfase. |
| E | **Bal kennen**: `KnownBall` niet laten terugvallen op `(0.5, 0.5)` maar op de laatste bekende positie (of `BallMemorySeconds` 1 s naar 3 s) en een snellere scanbeweging (0,4 naar 1,0 x draaisnelheid). | Minder `Scan`-stilstand (nu 13,7% van de ticks) en kortere dode ballen (34k ticks in rust). | Laag tot matig; beïnvloedt pressing (wie ziet de bal) en dus onderscheppingen. |

### 4.2 Aanbevolen volgorde

1. **Eerste ingreep: A en B samen** (klein, lokaal in `PassingBrain`, geen tactiekdata): lost het "te laat of niet schieten voor een vrije kans" op (hoofdoorzaak 1) en past in de gemeten kalibratieruimte. Proef A (4 seeds) liet de hoofdgetallen gelijk. B is niet apart gemeten.
2. **Tweede:** C voor de band 18-25 m (hoofdoorzaak 2 en 3). Dit is waar de "terugspeel"-indruk vandaan komt; het vergt een kalibratieronde.
3. **Voor stilstaan:** D als tactiekdata (snel te testen met de meter), E daarna. D is het grootste effect (stilstand -22 procentpunt), maar raakt de uitkomstaantallen; doe het met de meter en `calibration_run.py` naast elkaar.

## 5. Voorstel voor een regressietest

Nieuw bestand `sim/Zeus.Sim.Tests/AiBehaviourTests.cs` (niet aangemaakt):

- **Eenheidstest `PassingBrain`:** baldrager op 10 m recht voor het doel, vrije baan, houdtijd 0,5 s: intentie is `Shoot` (of minstens geen `MoveToPosition` van het doel af). Baldrager op 22 m, houdtijd 3 s, geen druk, vrije baan: geen `Pass` met ontvanger achter de baldrager als er een voorwaartse optie of meedraagmogelijkheid is.
- **Statistische test (3 seeds x `laag_blok_442` tegen zichzelf, ca. 55k ticks):** open-kansepisodes (definitie uit 1) eindigen voor meer dan 90% in een schot of "niet meer open"; terugpasses bij een vrije baan < 5%; aandeel stil (< 0,3 m/s) in fase Opbouw < 60% (na ingreep D); geen veldspeler langer dan 60 s onafgebroken stil in lopend spel; ballen in rust zonder baldrager < 20 s aaneengesloten. Drempels vastleggen bij de gekozen ingreep, niet nu (huidig: 18,1%, 81,9%, 1154 runs >= 60 s, 3 dode-balruns >= 30 s).
- De meter is bruikbaar als vangnet: `--ai-diagnose --seeds 2 --only laag_blok_442 --tactics-dir ../tactics` (ca. 6 s per wedstrijd) en de vier genoemde tabellen vergelijken met dit document.

## 6. Beperkingen

- De `Intent` die de meter leest is de laatste beslissing; de verse tabel (2.2) corrigeert daarvoor, de 2.1-tabel niet (episodes zijn daardoor ruw maar zelf consistent).
- De "open"-definitie is mijn keuze (25 m, 2 m baan, keeper uitgesloten); met strengere eisen worden aantallen kleiner, de rangorde van de oorzaken blijft (zelfde patroon met amateur en halfprof).
- Beide teams zijn gemeten, dus tactiekeffecten en tegenstanderwerking zitten erdoor; de rangorde per tactiek staat in 3.1.
- Eén seed levert gemiddeld 4 dode-ballen en 4 teamvriezers op; dit zijn aantallen, geen tijdverdelingen.
- Geen test van een verbeterde `FormationBrain`, geen toets aan echte trackingdata.


## Ronde 1 fix (2026-10-16, fix/ai-gedrag) - gedeeltelijk resultaat

**Wel behouden:** vroeg schieten bij een vrije goede kans en niet naar de formatie teruglopen binnen 11 m tijdens de houdtijd. **Niet opgelost:** het algemene terugspelen op 18-25 m en de hoge stilstand van de standaardtactieken. De opdracht is op die punten niet volledig gehaald; de gemeten bewegingsproeven zijn wegens kalibratieoverschrijdingen niet als standaard behouden.

### Beslisregels en schakelaars

- `PassConfig.EarlyShotXg = 0.15`: standaard aan, dezelfde `PassingBrain` voor neutraal, niveaus en selecties. Tijdens de houdtijd mag een veldspeler schieten als `canShoot` waar is, de gewone afstand/hoek-xG minstens 0,15 is, en geen zichtbare actieve veldtegenstander binnen 2 m van het lijnsegment naar het doel of 2,5 m van de schutter staat. De keeper telt niet als veldblokkade. Alleen eigen waarneming; geen extra RNG-trekkingen. De verworpen variant die elke kans vanaf 0,08 vroeg afvuurt is niet gebruikt.
- `KeepCloseGoalPosition = true`: binnen `DribbleStopGoalDistance` blijft de baldrager tijdens de houdtijd op zijn plek en richt zich naar het doel, zodra hij niet al schiet. Hiermee kan hij niet naar zijn formatieplek worden teruggestuurd. Een nog lopende oude intentie kan in de ruwe meter nog enkele ticks teruglopen geven (564 -> 3 ticks); de geconstrueerde beslisregressie toetst het nieuwe beslismoment zelf.
- `EarlyShotXg = null` schakelt beide wijzigingen en ook de onderstaande doorloopproef uit. Met dezelfde tactiekdata zijn de oude beslisvolgorde en RNG-consumptie behouden. De hele herhaalde 24-wedstrijdrapportage (`off.txt`) is byte-identiek aan de oorspronkelijke nulmeting; twee volledige oorspronkelijke gouden event- en snapshothashes worden bovendien in `AiBehaviourTests` getoetst.
- `CarryAfterHold = false`: alleen als expliciete proef beschikbaar. Na de houdtijd op 12-25 m, zonder schot, steekpas of goede voorwaartse pass en met vrije bestaande loopkegel, draagt de speler de bal 5 m richting doel. De eerste ingreep bleek onvoldoende (18,1% -> 18,7% terugpasses), dus deze tweede ingreep is daadwerkelijk gemeten. Hij verlaagt terugpasses tot 1,6%, maar geeft veel te veel schoten; daarom standaard UIT. Met balvolgen erbij geeft LL 55,38 schoten per team; zonder de doorloopproef met dezelfde volgwaarde nog 46,75. Dit is geen geslaagde kalibratie.

### Voor en na, afzonderlijke ingrepen

Elke rij: zes tactieken in dezelfde rotatie als sectie 1 (H-P, P-C, C-L, L-M, M-T, T-H), seeds 1-4, 24 volledige of gestaakte wedstrijden, beide teams samen. Eventkolommen zijn **gemiddelden per wedstrijd**, inclusief herstarts en strafschoppen. Pass-events bevatten ook wegtrappen; gewone passes zonder wegtrappen staan apart in de kalibratieanalyse. De open-uitkomsten zijn absolute episodeaantallen; terugpass is een deelverzameling van pass/trap. Stilstand gebruikt exact de oorspronkelijke meetdefinitie.

- `before`: oorspronkelijke build en tactieken.
- `early`: alleen vroeg schieten; positie houden uit.
- `close`: alleen positie houden (`EarlyShotXg=1` voorkomt in deze proef vroege schoten).
- `both`: **de behouden standaard**, vroeg schieten plus positie houden, geen balvolgen of doorlopen.
- `carry`: beide plus doorlopen, oorspronkelijke tactieken; afgewezen als standaard.
- `follow34`: beide plus doorlopen plus balvolgen [0,3, 0,4]; afgewezen.
- `candidate24`: beide plus balvolgen [0,2, 0,4], doorlopen UIT; beste gemeten volgkandidaat, ook afgewezen.

| Variant | Stil % | Open episodes | Schot / pass incl. wegtrap / terugpass | Schoten | Goals | Buitenspel | Fouten | Pass-events |
|---|---:|---:|---|---:|---:|---:|---:|---:|
| before | 58,2 | 2594 | 1034 / 501 / 469 | 54.96 | 6.17 | 10.67 | 22.58 | 961.92 |
| early | 59,1 | 2657 | 1153 / 573 / 546 | 55.50 | 6.17 | 11.88 | 23.88 | 954.21 |
| close | 60,0 | 2389 | 963 / 480 / 442 | 51.54 | 4.96 | 10.33 | 22.79 | 941.17 |
| both | 59,5 | 2620 | 1173 / 534 / 491 | 56.25 | 5.79 | 11.00 | 23.42 | 958.50 |
| carry | 60,0 | 2057 | 1627 / 41 / 33 | 80.33 | 8.42 | 8.96 | 22.12 | 934.79 |
| follow34 | 38,4 | 2102 | 1629 / 65 / 37 | 84.38 | 8.50 | 10.83 | 19.04 | 952.42 |
| candidate24 | 41,6 | 2482 | 1074 / 560 / 493 | 56.12 | 6.54 | 9.79 | 20.00 | 939.17 |

Bij `close` is T-H seed 3 gestaakt (minder dan zeven spelers); ook die events/ticks zijn volledig meegeteld. Alle overige rotatiewedstrijden zijn Finished. Geen uitslagen verwijderd. De nulmeting met `EarlyShotXg=null` is een extra controle en niet als nieuwe onafhankelijke steekproef meegeteld.

Bij de behouden standaard stijgen de open schotepisodes van 39,9% naar 44,8%; terugpasses over alle open episodes dalen niet (18,1% -> 18,7%). Onder episodes die ooit xG >= 0,08 halen, daalt 'niet meer open zonder trap' van 193 naar 75, terugpasses van 41 naar 33. Gemiddelde schotwachttijd 0,90 -> 0,74 s, p90 1,5 -> 1,2 s, mediaan blijft 0,8 s. Goals 148 -> 139: geen bewezen verbetering in doelpunten. De standaardstilstand verbetert niet: 58,2% -> 59,5%.

### Balvolgen en regressies

Alle zes voorbeeldtactieken zijn lokaal met volgwaarden in opbouw en aanval gemeten; alleen veldspelers, ook bij `gelijk_aan` (L en M), verdedigen/keepers/instructies ongewijzigd. Proeven: [0,2,0,2], [0,2,0,4], [0,3,0,3], [0,3,0,4], [0,4,0,2], [0,4,0,4]. Elke waarde kreeg vier LL-seeds zonder doorlopen. De beste, [0,2,0,4], kreeg daarnaast alle zes rotatieparen en alle zes symmetrische paren plus LM. Stilstand daalt daarbij naar 41,6% (doel < ongeveer 45%), maar LL-schoten stijgen van 13,88 naar 32,00. De gemiddelden over ongelijke paren verbergen dat probleem. Alle cijfers, doelen en beide vernieuwde goldens: `docs/kalibratie.md`, Ronde 1 AI-fix.

Daarom zijn `tactics/*.json` en `scripts/make_presets.py` uiteindelijk ongewijzigd. Schema en semantiek van alle proefbestanden zijn gevalideerd; de drie generatorpresets komen semantisch overeen met de behouden voorbeeldbestanden. Dit is een afgewezen datawijziging, geen claim dat de stilstandopdracht klaar is.

`AiBehaviourTests.cs`: directe schotkeuze tijdens de houdtijd in beide richtingen; vrije baan/druk/keeper; positie houden binnen 11 m; xG-grens en configvalidatie; aan/uit-regressies voor de optionele carry; twee oorspronkelijke volledige hashes bij `EarlyShotXg=null`; drie korte seeds van 12000 ticks met **expliciet aangebrachte** balvolging [0,2,0,4] en stilstand in Opbouw/Aanval < 55%. Die laatste test bewaakt het bewegingsmechanisme van de proef, niet een grens die de huidige standaardtactieken zouden halen. De bredere voorgestelde grenzen voor terugpasses < 5%, maximale stilstandruns en dode ballen zijn niet als voldane standaardregressie opgenomen.

### Herhalen

De Runner-meter accepteert nu ook `--config` (dezelfde strikte `MatchConfigJson`) en `--events-dir` voor heranalyse met `scripts/calibration_run.py:analyse`. Er is niets aan de bestaande kalibratiescripts veranderd. Voorbeeld vanaf de repo-root, na een sequentiele Release-build:

```powershell
dotnet .tmp_ai/bin/Zeus.Runner.dll --ai-diagnose --seeds 4 --tactics-dir tactics --events-dir .tmp_ai/events_herhaling --out .tmp_ai/herhaling.txt
```

Configuraties onder `Passing`: oud gedrag `{"EarlyShotXg":null}`; alleen vroeg `{"KeepCloseGoalPosition":false}`; alleen positie `{"EarlyShotXg":1}`; doorloopproef `{"CarryAfterHold":true}`. Wegens de kalibratie-uitkomst blijft de laatste expliciet optioneel. De reproduceerbare lokale invoer, wrappers en rapporten staan in `.tmp_ai/` (niet gecommit): `old_tactics/`, de zes `follow*`-mappen, configuraties, ruwe events, `calibrate.py`, `calibrate_ll.py`, `report.py`, `tables.py` en `*_cal.json`. Die wrappers starten steeds maar een Runner tegelijk. Vier seeds bepalen hier de richting; geen onafhankelijke validatie of realismeclaim. Niveaus zijn in deze ronde niet opnieuw als volledige meetserie gedraaid.

Eindcontrole: 13 nieuwe AI-testgevallen groen; 123 gerichte gevallen afgedekt via 122 geslaagde eerste gevallen en 2 geslaagde nulruisgevallen na fixtureherstel (een daarvan overlapte). Geen volledige suite. Beide nieuwe standaardgoldens en de oude hashes met de fix uit groen. Protocol, TRX-locaties en sandboxbeperking staan onder Eindcontrole Ronde 1 AI-fix in `docs/kalibratie.md`.
