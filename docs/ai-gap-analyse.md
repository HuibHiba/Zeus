# Kloofanalyse positiespel: sim tegen echte wedstrijden (fase 1)

Datum 2026-10-17, branch `demo/ai-gap`. Doel: meten waar het positiespel van de spelers-AI het verst van echt voetbal af ligt, als basis voor gerichte fixes in fase 2. Er is in deze fase niets aan het sim-gedrag veranderd.

## Bronnen en werkwijze

- **Echt:** DFL/Sportec open data (CC BY 4.0), 7 Bundesliga-wedstrijden 2022/23, optische tracking 25 Hz van alle 22 spelers plus eventdata. Gemeten op 1 Hz (20 554 ploegmomenten per fase, 11 821 momenten met een duidelijke baldrager), alleen bal in spel. Balbezit = ploeg van het laatste balcontact-event van hooguit 6 s oud (zelfde regel als `scripts/dfl_templates.py`; komt in ~85% overeen met de DFL-bezitsvlag). Passes: 4 414 geslaagde passes uit de events, begin = positie van de passer, eind = positie van de ontvanger bij het volgende balcontact (tracking op 5 Hz).
- **Sim:** `Zeus.Runner --positional-probe` (nieuw, alleen-lezen dump), 60 wedstrijden: `positiespel_4231`–`counter_532`, `counter_532`–`laag_blok_442`, `laag_blok_442`–`positiespel_4231`, elk seeds 901-920, prof-niveau, standaard `MatchConfig`. Op 1 Hz in lopend spel (319 946 ploegmomenten, 202 918 met baldrager). Geslaagde pass = `Pass` (geen `clear`) direct gevolgd door `BallControl` van dezelfde ploeg.
- **Zelfde definities voor beide:** `scripts/ai_gap_metrics.py`. Orientatie per ploeg en helft uit de gemiddelde keeperpositie, zodat elke ploeg naar +x aanvalt. Baldrager = speler van de balbezittende ploeg die het dichtst bij de bal staat, hooguit 2,5 m. Veldspelers = alles behalve de keeper; linies = 4 diepste / middelste 4 / voorste 2 veldspelers (formatievrij, dus ook bij een 5-3-2 vergelijkbaar). Passlijn vrij = geen tegenstander binnen 2 m van het lijnstuk. Driehoek = twee teamgenoten die met de baldrager drie zijden van 8-20 m vormen. Balbezit voor "passes per balbezit" = aaneengesloten reeks passes/schoten van één ploeg. Schuiffactor = helling (kleinste kwadraten) van blok- of achterlijnpositie tegen balpositie: 1 = schuift elke meter mee, 0 = staat stil.
- **Ernst:** verschil van de sim-mediaan (bij tellingen: gemiddelde) met de echte mediaan, gedeeld door de echte interkwartielafstand (IQR). |z| >= 1 hoog, >= 0,5 middel, anders laag. Bij metrics per ploeg per wedstrijd (stilstand, schuiffactoren, passes) is de echte spreiding klein (14 ploeg-wedstrijden), dus z wordt groot; kijk daar ook naar het absolute verschil.

Reproduceren (data in `C:\dev\zeus\data`, niet in git; dump in een tijdelijke map, niet in git):

```
Zeus.Runner --positional-probe --level prof --first-seed 901 --seeds 20 --out-dir <tmp>   # vanuit de repo-root, ~4 min
python -I scripts/ai_gap_real.py      # ~5 min, schrijft docs/ai-gap/real_metrics.json
python -I scripts/ai_gap_sim.py <tmp> # schrijft docs/ai-gap/sim_metrics.json (ook per tactiek)
python -I scripts/ai_gap_compare.py   # schrijft docs/ai-gap/vergelijking.csv en deze tabel
```

## Resultaten

| metric | eenheid | echt mediaan (p25-p75) | sim mediaan (p25-p75) | verschil (in IQR echt) | ernst |
|---|---|---|---|---|---|
| teamlengte (in bezit) | m | 35.1 (31.0-39.7) | 27.9 (22.9-42.1) | -0.82 | middel |
| teambreedte (in bezit) | m | 45.7 (36.2-56.6) | 51.7 (50.0-56.9) | +0.30 | laag |
| afstand verdediging-middenveld (in bezit) | m | 16.5 (13.5-19.5) | 11.3 (9.3-18.3) | -0.87 | middel |
| afstand middenveld-aanval (in bezit) | m | 10.6 (8.0-13.6) | 10.0 (7.3-16.5) | -0.10 | laag |
| achterlijn t.o.v. bal (- = achter bal) (in bezit) | m | -8.5 (-19.1-1.4) | -20.9 (-29.5--13.3) | -0.60 | middel |
| convex-hull-oppervlak (in bezit) | m² | 1086 (794-1406) | 943 (797-1416) | -0.23 | laag |
| mediane snelheid veldspelers (in bezit) | m/s | 2.1 (1.5-2.9) | 2.3 (0.8-3.8) | +0.17 | laag |
| % stilstaand (<0,5 m/s) (in bezit) | % | 4 (4-4) | 20 (20-49) | +23.71 | hoog |
| % snel (>5,5 m/s) (in bezit) | % | 4 (4-4) | 18 (15-20) | +24.00 | hoog |
| achterlijn schuift mee met bal (lengte, m per m) (in bezit) | × | 0.56 (0.53-0.57) | 0.41 (0.12-0.43) | -3.53 | hoog |
| blok schuift mee met bal (lengte) (in bezit) | × | 0.56 (0.52-0.59) | 0.47 (0.26-0.49) | -1.28 | hoog |
| blok schuift mee met bal (breedte) (in bezit) | × | 0.37 (0.33-0.39) | 0.11 (0.09-0.12) | -3.97 | hoog |
| teamlengte (zonder bal) | m | 32.0 (27.1-37.8) | 27.2 (19.5-39.5) | -0.44 | laag |
| teambreedte (zonder bal) | m | 37.2 (32.2-41.8) | 36.0 (30.7-38.6) | -0.13 | laag |
| afstand verdediging-middenveld (zonder bal) | m | 12.8 (9.9-16.0) | 9.0 (6.8-9.9) | -0.62 | middel |
| afstand middenveld-aanval (zonder bal) | m | 12.8 (10.1-16.1) | 12.1 (7.0-19.1) | -0.11 | laag |
| achterlijn t.o.v. bal (- = achter bal) (zonder bal) | m | -17.9 (-29.7--7.1) | -26.0 (-37.1--16.4) | -0.36 | laag |
| convex-hull-oppervlak (zonder bal) | m² | 786 (618-979) | 569 (445-787) | -0.60 | middel |
| mediane snelheid veldspelers (zonder bal) | m/s | 2.4 (1.6-3.2) | 0.9 (0.0-2.3) | -0.91 | middel |
| % stilstaand (<0,5 m/s) (zonder bal) | % | 3 (3-4) | 42 (33-45) | +48.12 | hoog |
| % snel (>5,5 m/s) (zonder bal) | % | 5 (5-6) | 13 (12-15) | +7.80 | hoog |
| achterlijn schuift mee met bal (lengte, m per m) (zonder bal) | × | 0.52 (0.48-0.55) | 0.28 (0.10-0.34) | -3.90 | hoog |
| blok schuift mee met bal (lengte) (zonder bal) | × | 0.57 (0.53-0.59) | 0.36 (0.23-0.44) | -3.34 | hoog |
| blok schuift mee met bal (breedte) (zonder bal) | × | 0.41 (0.37-0.41) | 0.17 (0.15-0.19) | -5.69 | hoog |
| teamgenoten ≤10 m van baldrager | # | 0.0 (0.0-1.0; gem. 0.88) | 0.0 (0.0-1.0; gem. 0.67) | -0.21 | laag |
| teamgenoten ≤20 m van baldrager | # | 3.0 (2.0-5.0; gem. 3.45) | 2.0 (1.0-4.0; gem. 2.66) | -0.26 | laag |
| teamgenoten ≤30 m van baldrager | # | 6.0 (4.0-8.0; gem. 5.99) | 6.0 (4.0-7.0; gem. 5.57) | -0.11 | laag |
| vrije passopties 5-25 m | # | 4.0 (3.0-6.0; gem. 4.20) | 3.0 (2.0-4.0; gem. 3.15) | -0.35 | laag |
| driehoeken 8-20 m met baldrager | # | 1.0 (0.0-3.0; gem. 1.98) | 0.0 (0.0-2.0; gem. 1.49) | -0.16 | laag |
| dichtste teamgenoot | m | 10.1 (6.7-13.8) | 10.3 (6.7-14.0) | +0.04 | laag |
| aandeel vooruitgaande geslaagde passes | % | 60 (58-61) | 51 (33-53) | -3.68 | hoog |
| gem. lengte geslaagde pass | m | 19.5 (18.8-20.7) | 15.0 (14.4-15.6) | -2.41 | hoog |
| passes per balbezit (gem. per ploeg) | # | 4.4 (4.0-6.1) | 6.7 (4.6-7.8) | +1.08 | hoog |

Sim per tactiek (mediaan; volledige cijfers in `docs/ai-gap/sim_metrics.json`):

| tactiek | stil zonder bal | stil in bezit | schuif x / y zonder bal | achterlijn t.o.v. bal in bezit | verd-mid zonder bal | vooruit | passlengte | passes/balbezit | vrije opties | driehoeken |
|---|---|---|---|---|---|---|---|---|---|---|
| positiespel_4231 | 30% | 20% | 0.45 / 0.15 | -16.7 m | 8.9 m | 52% | 15.1 m | 7.9 | 3.3 | 1.3 |
| counter_532 | 42% | 21% | 0.33 / 0.18 | -19.8 m | 6.4 m | 40% | 14.2 m | 6.0 | 3.7 | 2.7 |
| laag_blok_442 | 45% | 55% | 0.22 / 0.21 | -40.5 m | 9.7 m | 35% | 15.6 m | 4.5 | 2.3 | 0.5 |
| echt (DFL) | 3% | 4% | 0.57 / 0.41 | -8.5 m | 12.8 m | 60% | 19.5 m | 4.4 | 4.2 | 2.0 |

Wat klopt al redelijk: teambreedte zonder bal, afstand middenveld-aanval, afstand tot de dichtstbijzijnde teamgenoot, aantal teamgenoten binnen 30 m, hull in bezit en de mediane loopsnelheid in bezit. De verdeling in de sim is wel steeds veel breder (p25-p75 van teamlengte 23-42 m tegen 31-40 m echt): de vorm springt tussen twee standen in plaats van geleidelijk te ademen.

## Top-5 kloven

### 1. Het blok schuift nauwelijks mee met de bal (vooral zijwaarts)

Meting: zijwaartse schuiffactor 0,11 (in bezit) en 0,17 (zonder bal) tegen 0,37 en 0,41 echt; in de lengte 0,28-0,36 tegen 0,52-0,57. Gevolg: achterlijn zonder bal 26 m achter de bal (echt 18 m), in bezit 21 m (echt 8,5 m); bij laag blok in bezit zelfs 40 m.

Hypothese: de formatiedoelen volgen de bal met `volg_bal × BallFollowGain` en die versterking is 0,5, expliciet "First agreement, not calibrated" (`Match.cs:8-10`, toegepast in `Match.cs:633-636` in `TargetFor`). Met de huidige `volg_bal` van 0,25-0,4 (`tactics/*.json`) geeft dat precies de gemeten 0,12-0,2 zijwaarts. Daarnaast laat `Instructions.cs:85` de lijn alleen naar achteren meegaan (`Math.Min(0, ...)`): als de bal opschuift, schuift de achterlijn niet voorbij zijn formatieplek mee. Bij laag blok hebben opbouw en aanval maar `volg_bal [0.1, 0.2]`, vandaar 55% stilstand in bezit.

Minimale fix: `BallFollowGain` 0,5 naar 1,0 (een getal in `MatchConfig`; geen tactiekwijziging). Verwachte meting: schuiffactor zijwaarts ongeveer 0,22 in bezit en 0,34 zonder bal, in de lengte zonder bal ongeveer 0,5; achterlijn zonder bal van -26 naar ongeveer -20 m. Tweede stap als de lengte achterblijft: `Instructions.cs:85` symmetrisch maken (lijn mag ook tot ~10 m met de bal opschuiven). Risico: aantallen (schoten, onderscheppingen) verschuiven, dus kalibratie in `docs/kalibratie.md` opnieuw draaien (zie ook `docs/ai-diagnose.md`, optie D).

### 2. Bewegen is aan/uit: veel stilstaan en veel sprinten, weinig joggen

Meting: zonder bal staat 42% van de veldspelers stil (echt 3%), in bezit 20% (echt 4%); tegelijk loopt 13-18% harder dan 5,5 m/s (echt 4-5%). Mediane snelheid zonder bal 0,9 m/s tegen 2,4 m/s.

Hypothese: `Mover.Step` zet de topsnelheid altijd op `p.MaxSpeed` (`Mover.cs:30`) en remt pas vlak voor het doel tot nul af (`Mover.cs:26`, `:49`, `:64`). Elke verschuiving van het doel, hoe klein ook, wordt dus een sprint en daarna stilstand. Alleen paden krijgen een wandel- of jogsnelheid (`Match.cs:512-514`). Na aankomst geeft `FormationBrain` `HoldPosition` (`Brain.cs:17-20`) of `PassingBrain` `Scan` (`Passing.cs:352-353`), en de doelen bewegen pas weer bij de volgende beslissing met een nieuw (zwak meeschuivend, kloof 1) doel. Verdedigers zonder balzicht zien de bal maar elke 0,9 s (`Match.cs:605`, `BallGlanceSeconds`).

Minimale fix: in `Match.cs` vlak voor `Mover.Step` (regel 511-515) voor `MoveToPosition`/`HoldPosition` zonder bal, pressing, ontvangst of loopactie de topsnelheid begrenzen tot `max(2, afstand / 1,5 s)`: een speler joggt naar een doel dat hij in ongeveer 1,5 s kan halen in plaats van te sprinten en te wachten. Verwachte meting: snel-aandeel zonder bal 13% naar 5-7%, in bezit 18% naar 6-8%; stilstand zonder bal 42% naar ongeveer 20-25% (samen met fix 1 lager, omdat doelen dan voortdurend schuiven). Risico: verdedigers komen iets later aan; buitenspel- en onderscheppingsaantallen controleren.

### 3. Steriel passpel: te kort, te weinig vooruit, te veel passes per balbezit

Meting: 51% van de geslaagde passes gaat vooruit (gepoold over alle sim-passes zelfs 44%) tegen 60% echt; gemiddelde lengte 15,0 m tegen 19,5 m; 6,7 passes per balbezit tegen 4,4. Bij laag blok en counter gaat maar 35-40% vooruit. In de events zie je het patroon "buitenspeler naar back, back naar middenvelder, terug naar buitenspeler".

Hypothese: de passkeuze (`Passing.cs:135-147`) telt het risico op als som over alle zichtbare tegenstanders (tot 2 punten per tegenstander: lijn binnen 3 m en ontvanger binnen 4 m, gewicht 0,9), terwijl vooruit maar 0,06 per meter oplevert en lengte 0,015 per meter kost (`Passing.cs:22-25`). Een pass vooruit in een blok met meerdere tegenstanders verliest daardoor bijna altijd van een vrije breedte- of terugpass. De pass binnen 3 m van de buitenspellijn wordt overgeslagen (`Passing.cs:128-134`), wat vooruit nog verder beperkt.

Minimale fix: `ForwardWeight` 0,06 naar 0,10 en `DistanceWeight` 0,015 naar 0,005 (`Passing.cs:23-24`, alleen standaardwaarden). Verwachte meting: vooruit 51% naar 56-60%, passlengte 15 naar 17-18 m, passes per balbezit 6,7 naar ongeveer 5,5. Alternatief als het balverlies te hard stijgt: risico per tegenstander als maximum in plaats van som (`Passing.cs:136-144`). Risico: meer onderscheppingen; passkalibratie (`fit_pass_360.py`-doelen) opnieuw controleren.

### 4. Linies te dicht op elkaar, ploeg te kort

Meting: afstand verdediging-middenveld zonder bal 9,0 m (echt 12,8 m), in bezit 11,3 m (echt 16,5 m); teamlengte in bezit 27,9 m (echt 35,1 m); hull zonder bal 569 m² (echt 786 m²). `positiespel_4231` verdedigt met een teamlengte van maar 16,7 m.

Hypothese: zonder bal trekt compactheid zowel x als y met `1 - 0,4 × compactheid` naar het midden (`Instructions.cs:73-78`; laag blok heeft compactheid 0,9, dus 36% inkrimping) en `DefensiveSupport.Target` trekt nog een speler tot 8 m naar het dekpunt achter de drukzetter (`DefensiveSupport.cs:16-21`), wat de middenlinie verder op de verdediging drukt. In bezit blijft de achterlijn achter (kloof 1) terwijl de voorhoede de aanvalsvorm haalt via de opbouw-naar-aanval-interpolatie (`Match.cs:621-631`): het middenveld zakt mee naar achteren en de linies schuiven in elkaar.

Minimale fix: de compactheidsfactor in de lengte halveren (`Instructions.cs:75`: `0,4` naar `0,2` voor x, y laten zoals hij is). Verwachte meting: verd-mid zonder bal 9 naar ongeveer 11-12 m, hull zonder bal 569 naar ongeveer 680 m². Met fix 1 komt de achterlijn in bezit op; verwacht teamlengte in bezit van 28 naar ongeveer 32 m. Risico: laag blok wordt minder compact; controleren met de schoten-tegen-doelen voor laag blok (<= 20).

### 5. Minder aanspeelbaarheid rond de baldrager

Meting (ernst laag op IQR, maar consequent ~25% lager): vrije passopties 3,15 tegen 4,20 gemiddeld, teamgenoten binnen 20 m 2,66 tegen 3,45, driehoeken 1,49 tegen 1,98. Bij laag blok 2,3 opties en 0,5 driehoeken.

Hypothese: `SupportPlay.Target` grijpt alleen in als de eigen plek 6-32 m van de baldrager ligt (`SupportPlay.cs:13`), zoekt op een raster van maar ±4 m bij ±5 m (`SupportPlay.cs:35-36`) en straft elke meter van de formatieplek af met 0,4 (`SupportPlay.cs:28`), terwijl de vrije lijn maar tot 4 m meetelt (`SupportPlay.cs:19`). Een speler die in de schaduw van een tegenstander staat, kan zich dus niet ver genoeg vrijlopen. Waarneming speelt mee: teamgenoten buiten de kijkhoek en verder dan 25 m zijn onbekend (`Perception.cs:13`), dus de baldrager zelf ziet minder opties.

Minimale fix: raster naar ±8 m bij ±8 m en de afstandsstraf 0,4 naar 0,2 (`SupportPlay.cs:28`, `:35-36`). Verwachte meting: vrije passopties 3,15 naar ongeveer 3,7, driehoeken 1,5 naar ongeveer 1,8, teamgenoten binnen 20 m naar ongeveer 3. Risico: spelers verlaten hun zone vaker; breedte in bezit (nu al 51,7 m tegen 45,7 m) niet verder laten groeien.

Overige opvallende punt, niet in de top-5: de sim speelt in bezit te breed (51,7 m tegen 45,7 m, p25 al 50 m): buitenspelers staan vast tegen de zijlijn (`Instructions.cs:137-138`, `blijf_breed`, en de formatieplekken).

## Wat de data niet kan zeggen

- **Klein en eenzijdig:** 7 wedstrijden, één competitie (Bundesliga, topniveau). Geen tactieklabels, dus "echt" is één gemiddelde stijl; een echte counterploeg of laag blok kan van deze mediaan afwijken. De sim-tactieken zijn testtactieken op prof-niveau, geen Bundesliga-ploegen.
- **Bezit en baldrager zijn afgeleid:** echt bezit uit events (~85% overeenstemming met de DFL-vlag); de baldrager is in beide bronnen "dichtstbijzijnde speler binnen 2,5 m", dus duels en losse ballen tellen deels mee.
- **Passes:** het passeinde in de echte data is de ontvanger bij het volgende balcontact (kan na een aanname-dribbel zijn, maakt passes iets langer); in de sim is het de aannameplek. "Passes per balbezit" is een benadering via reeksen van één ploeg; DFL-clearances zijn geen `Play`, sim-`clear` is weggelaten. Passen naar de keeper tellen mee in beide.
- **Snelheid:** DFL `S` is een gefilterde snelheid; de sim geeft de momentane snelheid. Voor stilstand maakt dat weinig uit (sim-spelers staan echt op 0), voor het sprintaandeel kan de filtering de echte waarde iets drukken.
- **1 Hz:** sneller gedrag (scannen, korte aanpassingen voor een pass) is niet zichtbaar; tijd aan de bal en het moment van passen zijn niet gemeten.
- **Vrije passlijn:** puur geometrisch (2 m van het lijnstuk), zonder balhoogte, balsnelheid of reactietijd van tegenstanders; het zegt niets over of de pass echt aankomt.
- **Waarom:** tracking laat zien waar spelers staan, niet wat ze van plan waren of wat de coach vroeg. De oorzaken hierboven zijn hypotheses uit de code, geen bewijs; elke fix moet met dezelfde probe opnieuw gemeten worden.
- **SkillCorner (10 A-League-wedstrijden)** is niet gebruikt: broadcast-tracking, spelers buiten beeld ontbreken of zijn geschat, wat teamlengte, hull en passopties systematisch vertekent.

## Bestanden

- `sim/Zeus.Runner/PositionalProbe.cs` (+ hook in `Program.cs`): framedump, verandert het sim-gedrag niet.
- `scripts/ai_gap_metrics.py` (gedeelde definities met zelftest), `scripts/ai_gap_real.py`, `scripts/ai_gap_sim.py`, `scripts/ai_gap_compare.py`.
- `docs/ai-gap/real_metrics.json`, `docs/ai-gap/sim_metrics.json` (ook per tactiek), `docs/ai-gap/vergelijking.csv`: alleen afgeleide statistiek.
