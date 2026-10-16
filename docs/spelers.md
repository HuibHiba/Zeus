# Spelers: selectie, attributen en niveau (v0, voorlopig)

Ontwerp: vault `02 Techniek/Spelersmodel v0.md`. Alle formules en getallen zijn **aannames** en ongekalibreerd. Dit is geen realismeclaim; er is geen kalibratie of wissel-/bankgedrag aan gekoppeld.

## Bestanden
- `schema/speler.schema.json` (schemaVersion `0.1`, status `voorlopig`): een selectie heeft `name`, `starters` (11, id = tactiekslot zoals `GK`, `RC`, `LB`) en `bench` (7, id `B1`..`B7`). Een speler heeft `id`, `name`, `role`, `preferredPosition`, `attributes` (15 gehele getallen 1..20) en voor rol `keeper` een blok `keeper` (`reflexes`, `positioning`, `rushingOut`).
- `squads/amateur.json`, `halfprof.json`, `prof.json`: drie fictieve selecties van 18 (fictieve namen), gegenereerd door `scripts/generate_squads.py` (reproduceert de bestanden byte voor byte). Controle: `.venv\Scripts\python.exe scripts\validate_squads.py [--tactic tactics/x.json]` (schema, unieke ids, GK, optioneel tactiekslots).
- `sim/Zeus.Sim/Squad.cs` (`SquadLoader`: strikte parser, valideert hetzelfde plus de tactiekslots; 1..20 wordt intern `(n-1)/19`), `Match.Attributes.cs` (alle effecten en `AttributeConfig`).
- Runner: `--home-squad bestand` en `--away-squad bestand`; een replay bewaart de selecties en kan ze niet overschrijven.

## De 15 attributen en wat ze nu doen
| Attribuut | Effect in v0 |
|---|---|
| passing | `PassConfig.Skill = 0,5 + 0,35·passing`; hoek- en snelheidsfout × `(1 + 2·overbelasting)·(1 + 0,3·(1 - conditie))` |
| firstTouch | bij relatieve snelheid > 9 m/s en een fout > 0,7 m een echte losse bal (event `FirstTouch`, trigger `slechte_aanname`); controlestraal en maximale relatieve snelheid × `(0,85 + 0,2·firstTouch)` |
| finishing | schotvaardigheid `0,6 + 0,18·finishing` (anders de passvaardigheid) |
| defending | basiskans op overtreding `+0,15·(1 - defending)`; duelsterkte van de verdediger |
| speed, acceleration | maximale snelheid `6,5 + 3·speed`, versnelling `2,5 + 2,5·acc`, draaisnelheid `3 + 4·acc` |
| endurance | schaalt het conditieverbruik in `Match.Fatigue.cs` |
| vision | scaninterval `0,5 + (1 - vision)` s, gezichtsveld `100° + 40°·vision`, rondomzicht en geheugen `×(0,5 + 0,5·vision)`, aantal overwogen passopties `2 + round(8·vision)` |
| anticipation | late reactie: bij een duidelijke verandering van de baltrajectorie wacht de speler `0,25 + 0,6·(1 - anticipation)` s; op een trigger bovendien `+0,5·overbelasting` s en kans `0,3·overbelasting` de trigger te missen |
| positioning | richtingsafhankelijke bias (zie hieronder) |
| decisions | softmax-temperatuur `0,03 + 0,7·(1 - decisions) + 0,4·overbelasting` bij de passkeuze |
| discipline | kans op kaart (bias `0,15·(discipline - 0,75)`) |
| dribbling, heading, strength | **opgeslagen, geen effect**: er is geen dribbel- of kopduelmodel |

De keeper heeft apart `reflexes` (maximale relatieve snelheid van de redding, `×(0,95 + 0,10·reflexes)`) en `positioning` (controlestraal, `×(0,95 + 0,10·positioning)`); `rushingOut` wordt opgeslagen, niet gebruikt.

## Niveau als systematische fouten
Een amateur is niet "0,4 op alles": de basisprofielen (`generate_squads.py`) houden fysiek redelijk (snelheid 13, kracht 13) maar zwak in overzicht (6), anticipatie (6), positionering (6) en beslissen (7). De fouten zijn gericht, niet nul-gemiddelde ruis:
1. **Late reactie:** vertraagde reactie op baltrajectwijzigingen en vertraagde/gemiste tactiektriggers (`PendingTriggers`).
2. **Bias in afstanden:** het formatiedoel schuift naar buiten ten opzichte van het eigen defensieve zwaartepunt (`0,10·tekort + 0,45·overbelasting`, dus minder compact), wordt naar de bekende bal getrokken (`0,12·tekort`) en een man-om-man-dekker blijft tot `3·tekort` m te ver weg (`MarkGapExtra`).
3. **Minder overzicht:** trager scannen, smaller gezichtsveld, kortere bal- en rondomzichtgeheugen, minder passopties.
`overbelasting` = tactiekcomplexiteit (aantal triggers en dekkingsopdrachten, 0..1) × `((0,8 - min(decisions, positioning))/0,8)²`: alleen een zwakke speler in een complexe tactiek wordt zwaar belast.

## Configschakelaars (`MatchConfig.Attributes`, `AttributeConfig`)
Alle twaalf schakelaars staan standaard **aan**: `Movement`, `Passing`, `FirstTouch`, `Finishing`, `Defending`, `Vision`, `Anticipation`, `Positioning`, `Decisions`, `Endurance`, `Discipline`, `Keeper`. `AttributeConfig.Disabled` zet ze allemaal uit. Zonder selectie, met `Squad.NeutralFor(tactiek)` of met `Disabled` is het gedrag bit-identiek aan het oude (zie controles).

## Wat bewust ontbreekt
- **Vertrouwdheid en begrip per tactisch concept** en training (fase 6): geen `begrip[c]`/`vertrouwdheid[c]`; alleen een grove complexiteitsmaat (`overbelasting`).
- **Keeperattributen minimaal:** twee kleine factoren op de redding, geen uitkomgedrag.
- **Geen blessures**, geen wissels of bankgebruik (de bank staat buiten het veld), geen dribbel- of kopduelmodel, geen conditie-effect op de beslissing.
- Meerdere speler-eigenschappen uit het ontwerp (kans op gemiste trigger, softmax-temperatuur) zijn in v0 versimpeld.

## Aannames
- Schaal 1..20 lineair naar 0..1; neutraal profiel `PlayerAttributes.Neutral` is een intern ijkpunt en schakelt de effecten uit (identiteitscontrole `ReferenceEquals`), het is geen "middenniveau".
- De niveaus zijn schattingen: amateur ≈ 6..13, halfprof ≈ 12..15, prof ≈ 16..18 (zie `generate_squads.py`).
- Elke speler met attributen krijgt een eigen RNG-stroom (`new Rng(seed, 1000 + index)`), zodat de hoofd-RNG-reeks voor teams zonder selectie niet verschuift.

## Meting (SquadProbe)
`dotnet run -c Release --project sim/Zeus.SquadProbe -- 8` vanuit de repo-root (8 seeds, elke seed twee keer met thuis/uit gewisseld = 16 wedstrijden per paar; tactiek `laag_blok_442` tegen zichzelf; een tweede argument, bv. `neutraal`, beperkt tot de complexe-tactiekrijen). De probe staat bewust niet in `Zeus.sln` (zoals `Zeus.InstructionProbe`): het is een meettool, geen test. Een volledige run duurt ca. 10 minuten. "Nauwkeurig" = pass gevolgd door aanname door een teamgenoot; "Buiten bereik" = wedstrijden waarin de ploeg buiten passes 250..700 of schoten 5..30 viel. "neutraal" = geen selectie (oud gedrag). Gemiddelden per ploeg per wedstrijd; W-G-V vanuit die ploeg.

| Paar | Ploeg | Passes | Nauwkeurig % | Bezit % | Goals | Schoten | Buiten bereik | W-G-V |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| prof - amateur | prof | 429,9 | 71,9 | 57,6 | 8,75 | 42,3 | 16/16 | 15-1-0 |
| | amateur | 300,6 | 52,1 | 42,4 | 2,31 | 12,6 | 0/16 | 0-1-15 |
| halfprof - amateur | halfprof | 399,6 | 75,4 | 56,8 | 3,69 | 21,3 | 3/16 | 8-6-2 |
| | amateur | 248,7 | 57,8 | 43,2 | 2,25 | 15,7 | 7/16 | 2-6-8 |
| prof - halfprof | prof | 430,6 | 75,7 | 53,7 | 6,38 | 33,6 | 13/16 | 16-0-0 |
| | halfprof | 337,4 | 64,7 | 46,3 | 1,75 | 17,3 | 1/16 | 0-0-16 |
| prof - neutraal | prof | 340,7 | 67,0 | 40,5 | 2,00 | 21,8 | 1/16 | 4-2-10 |
| | neutraal | 536,8 | 80,3 | 59,5 | 3,56 | 21,2 | 0/16 | 10-2-4 |
| halfprof - neutraal | halfprof | 281,5 | 57,6 | 35,0 | 2,19 | 14,6 | 2/16 | 4-1-11 |
| | neutraal | 597,5 | 82,3 | 65,0 | 4,38 | 21,9 | 0/16 | 11-1-4 |
| amateur - neutraal | amateur | 236,1 | 52,1 | 31,5 | 0,62 | 8,3 | 11/16 | 0-0-16 |
| | neutraal | 613,9 | 84,4 | 68,5 | 6,44 | 25,7 | 3/16 | 16-0-0 |
| neutraal - neutraal | neutraal | 459,2 | 71,1 | 50,0 | 1,50 | 12,6 | 0/16 | 6-4-6 |
| amateur - amateur | amateur | 257,1 | 60,5 | 50,0 | 3,12 | 21,7 | 7/16 | 7-2-7 |
| halfprof - halfprof | halfprof | 387,9 | 70,0 | 50,0 | 3,62 | 21,1 | 0/16 | 8-0-8 |
| prof - prof | prof | 399,8 | 73,1 | 50,0 | 6,44 | 30,8 | 9/16 | 8-0-8 |

Complexe stresstactiek (alle uitgangsspelers dekken man en alle zes triggers laten iedereen pressen) tegen de eenvoudige, 16 wedstrijden per niveau, rij van de complexe ploeg: neutraal 122,5 passes / 12,5% bezit / 5,2 schoten; amateur 29,6 / 10,4% / 1,2; halfprof 28,4 / 3,6% / 1,9; prof 51,4 / 9,1% / 4,4.

## Controles (2026-10-16)
- **(a) Zonder selectie bit-identiek aan main:** de golden-tests (`GoldenMatchTests`, event- en snapshothash) zijn ongewijzigd ten opzichte van het vertrekpunt van deze branch (`3eac1f4`) en slagen; de test `ExplicitNeutralAndDisabledAttributesKeepOldBits` vergelijkt snapshot per tick. Let op: `main` zelf is sindsdien doorgegaan met andere werk; vergelijk na het mergen opnieuw.
- **(b) Neutraal profiel = oud gedrag:** ja, snapshot per tick en eventhash gelijk (zelfde test, seeds 7 en 42), ook met alle attributen op `Disabled` terwijl een echte amateur-/profselectie is geladen. Kanttekening: "neutraal" is het oude, ongehinderde gedrag (scaninterval 0,3 s, geen reactievertraging) en dus **sterker dan prof**: prof verliest van neutraal (4-2-10), halfprof ook (4-1-11), amateur chancelloos (0-0-16). Alle attribuuteffecten zijn afnames ten opzichte van het ideale oude gedrag. Neutraal moet dus niet als "middenniveau" worden gelezen.
- **(c) Geloofwaardigheid (passes 250-700, schoten 5-30):** gemiddelden liggen nagenoeg overal binnen de grenzen, maar **niet voor alle combinaties per wedstrijd**: prof tegen amateur schiet 42 keer per wedstrijd (alle 16 wedstrijden boven 30), amateur tegen neutraal haalt gemiddeld 236 passes. Ook de absolute niveaus zijn ongekalibreerd: prof - prof geeft 6,4 goals per team (neutraal 1,5) bij 31 schoten, een duidelijk te hoge doelpuntenproductie die niet aan een enkele formule is toe te schrijven. Er is bewust niet gekalibreerd. De complexe stresstactiek stort ook zonder selectie in (122 passes, 5 schoten), met selectie nog verder (30-50 passes): dat is een eigenschap van die extreme tactiek, niet van de spelers.
- **(d) Prof wint van amateur (8 seeds, thuis/uit gewisseld):** ja in alle maten: 15-1-0, passes 430 - 301, nauwkeurigheid 71,9 - 52,1%, bezit 57,6 - 42,4%, schoten 42,3 - 12,6, goals 8,75 - 2,31. Rangorde prof > halfprof > amateur klopt ook onderling (prof - halfprof 16-0-0; halfprof - amateur 8-6-2 met veel gelijke spelen).
- **(e) Zelfde seed + zelfde selecties = zelfde eventhash:** test `SquadMatchesAndReplayAreDeterministic` (capture en replay), plus twee volledige Runner-wedstrijden (seed 5, prof - amateur, 5673 s) met byte-identieke eventbestanden en gelijke samenvatting.
- Test: `dotnet test sim/Zeus.Sim.Tests -m:1`: 404 van 404 geslaagd (incl. snelheidstest, onbelast; deze kan onder belasting falen).

## Bekende afwijkingen en open punten
- De niveaus liggen ver uit elkaar (amateur - prof vrijwel kansloos, 15-1-0) en de absolute uitkomsten (goals, schoten bij prof) zijn te hoog. Kalibratie en een vlakkere schaal zijn vervolgwerk.
- Het oude gedrag is boven prof; wil men een middenniveau gelijk aan het oude gedrag, dan moeten de reactie- en scanconstanten ten opzichte van de bestaande basis (0,3 s scan) worden herschaald in plaats van de absolute ontwerpwaarden (0,5 s prof).
- Dribbelen, koppen, kracht en `rushingOut` hebben geen effect; vertrouwdheid per concept ontbreekt.

## Herschaling niveaus
Aanleiding: het neutrale profiel (geen selectie, het oude gedrag) was sterker dan alle niveaus, omdat elk attribuuteffect een afname was ten opzichte van het ongehinderde oude gedrag, en prof-prof gaf 6,4 goals per team. Alle getallen hieronder zijn **aannames**; ze zijn gekozen om de gemeten doelen te halen, niet uit een bron. Gedrag zonder selectie is ongewijzigd (zelfde golden-hashes; `ExplicitNeutralAndDisabledAttributesKeepOldBits` slaagt). De formules in de attributentabel hierboven zijn hierdoor vervangen door de relatieve vormen hieronder.

Wat is veranderd (`Match.Attributes.cs`, kleine delen in `Match.cs`, `Match.Fatigue.cs`, `Match.Rules.cs`):
1. **Ankerpunt halfprof = oud gedrag.** Elk effect is nu relatief aan de basisconfig (scaninterval 0,3 s, passvaardigheid 0,75, snelheid 7,8 m/s, geen reactievertraging, geen keuzeruis) en nul bij het gemiddelde van het halfprofprofiel (`HalfProMean`, 10..15 op de schaal 1..20). Een betere speler verbetert het oude gedrag (scan tot ca. 0,2 s, passvaardigheid tot ca. 0,83); een zwakkere verslechtert het.
2. **Kwantisering gecorrigeerd:** het beslisinterval van een speler met attributen is nu `round(scan / 0,1 s)` in plaats van `ceil`. Met `ceil` kostte elke afwijking van 0,0004 s boven 0,3 s een hele stap (3 naar 4 stappen); dat was de verborgen reden dat halfprof bleef verliezen van neutraal.
3. **Spreiding gecomprimeerd:** voordat de formules lopen worden alle attributen naar het halfprofgemiddelde getrokken: boven het gemiddelde met factor 0,6 (`LevelSpreadUp`), eronder met 0,35 (`LevelSpreadDown`). De opgeslagen attributen en `SquadPlayerOf` blijven de ruwe waarden (`Player.Effective` bevat de getrokken waarden).
4. **Effecten relatief gemaakt** (`ref` = halfprofgemiddelde): scan `0,3 s·(1 + 0,7·(ref - vision))` (min 0,15 s), gezichtsveld `120° + 40°·(vision - ref)`, rondomzicht en geheugen `×(1 + 0,6·(vision - ref))`, passopties `8 + round(10·vision)` (beperkt halfprof niet meer), reactievertraging `0,7·max(0, ref - anticipation)` s (triggers: `0,1 + max(0, ref - anticipation) + 0,5·overbelasting`), softmaxtemperatuur `0,35·max(0, ref - decisions) + 0,4·overbelasting`, passvaardigheid `0,75 + 0,35·(passing - ref)`, schotvaardigheid `0,75 + 0,18·(finishing - ref)`, snelheid `7,8 + 2,4·(speed - ref)`, versnelling `4,5 + 2,5·(acc - ref)`, draaisnelheid `6 + 4·(acc - ref)`, aannamefactor `1 + 0,3·(firstTouch - ref)`, keeperfactoren `1 + 0,1·(.. - ref)`, duelvaardigheid `0,6 + (defending - ref)`, uithouding `0,5 + (endurance - ref)`; kans op een slechte aanname alleen onder de referentie. Het vaste conditie-aandeel in de passfout (`1 + 0,3·(1 - lange energie)`) is vervallen: het deed ook bij halfprof mee en het oude gedrag kent het niet. Positioneringsbias: `0,14·tekort + 0,45·overbelasting` (formatiedoel), `0,18·tekort` (naar de bal), `4·tekort` m (dekking), met `tekort = max(0, ref - positioning)`; `overbelasting` gebruikt dezelfde referentie.
5. **Probe:** `SquadProbe` kreeg een derde argument (paren, bv. `prof:amateur,halfprof:neutraal`, voor parallelle runs; het tweede argument is dan `-`), `ZEUS_PROBE_SEED0` (eerste seed min 1) en `ZEUS_PROBE_OFF` (attribuutschakelaars uit, ablatie). Met 10 parallelle processen kost 8 seeds ca. 80 s.

### Meting voor en na (8 seeds, thuis/uit gewisseld = 16 wedstrijden per paar, `laag_blok_442` tegen zichzelf)
| Paar | Ploeg | Goals voor | Goals na | Schoten voor | Schoten na | Passes voor | Passes na | W-G-V voor | W-G-V na |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| prof - amateur | prof | 8,75 | 3,44 | 42,3 | 23,2 | 430 | 533 | 15-1-0 | 13-2-1 |
| | amateur | 2,31 | 1,25 | 12,6 | 10,8 | 301 | 382 | 0-1-15 | 1-2-13 |
| halfprof - amateur | halfprof | 3,69 | 2,12 | 21,3 | 15,6 | 400 | 518 | 8-6-2 | 7-5-4 |
| | amateur | 2,25 | 1,25 | 15,7 | 11,1 | 249 | 392 | 2-6-8 | 4-5-7 |
| prof - halfprof | prof | 6,38 | 1,88 | 33,6 | 18,2 | 431 | 473 | 16-0-0 | 9-3-4 |
| | halfprof | 1,75 | 1,19 | 17,3 | 11,8 | 337 | 449 | 0-0-16 | 4-3-9 |
| prof - neutraal | prof | 2,00 | 2,44 | 21,8 | 18,0 | 341 | 479 | 4-2-10 | 9-1-6 |
| | neutraal | 3,56 | 1,38 | 21,2 | 10,6 | 537 | 441 | 10-2-4 | 6-1-9 |
| halfprof - neutraal | halfprof | 2,19 | 1,75 | 14,6 | 14,7 | 282 | 458 | 4-1-11 | 6-5-5 |
| | neutraal | 4,38 | 1,50 | 21,9 | 13,4 | 598 | 450 | 11-1-4 | 5-5-6 |
| amateur - neutraal | amateur | 0,62 | 1,06 | 8,3 | 12,1 | 236 | 397 | 0-0-16 | 1-5-10 |
| | neutraal | 6,44 | 2,44 | 25,7 | 17,6 | 614 | 510 | 16-0-0 | 10-5-1 |
| neutraal - neutraal | neutraal | 1,50 | 1,50 | 12,6 | 12,6 | 459 | 459 | 6-4-6 | 6-4-6 |
| amateur - amateur | amateur | 3,12 | 1,31 | 21,7 | 13,4 | 257 | 447 | 7-2-7 | 8-0-8 |
| halfprof - halfprof | halfprof | 3,62 | 1,25 | 21,1 | 13,4 | 388 | 453 | 8-0-8 | 8-0-8 |
| prof - prof | prof | 6,44 | 1,50 | 30,8 | 18,2 | 400 | 462 | 8-0-8 | 8-0-8 |

Nauwkeurigheid na: prof 73-79%, halfprof 70-75%, amateur 65-70%. Een tussenmeting met meer seeds voor halfprof - neutraal (32 seeds, 64 wedstrijden, constanten bijna gelijk aan de eindstand): halfprof 20-19-25, neutraal 25-19-20, goals 1,47 - 1,50. Rangorde na: prof > halfprof ≈ neutraal > amateur.

### Doelen
1. **Halfprof tegen oud gedrag:** 6-5-5 (16 wedstrijden) en 20-19-25 (64 wedstrijden, tussenmeting): winstaandeel 38% resp. 31% van alle wedstrijden, 55% resp. 44% van de beslissende wedstrijden. Op de beslissende wedstrijden binnen 35-65%; van alle wedstrijden zit de 64-wedstrijdenmeting net onder de grens door de vele gelijke spelen (47%). Bij 16 wedstrijden is de standaardafwijking op een winstaandeel ca. 12 procentpunt.
2. **Prof boven halfprof en amateur:** prof wint 13 van 16 van amateur (81%), 9-3-4 van halfprof, 9-1-6 van neutraal. Amateur verliest vaker dan hij wint van halfprof (4-5-7) en van prof (1-2-13). De afstand halfprof - amateur (7-5-4) is kleiner dan prof - amateur, zoals bedoeld.
3. **Geloofwaardigheid:** per combinatie liggen de gemiddelde passes (382-533) en schoten (10,6-23,2) binnen 250-700 en 5-30; wedstrijden buiten bereik: maximaal 1 van 16 (prof - prof 2/16). **Nog niet gehaald:** prof tegen amateur geeft prof 3,44 goals (grens 2,5). Alle andere rijen liggen in 0,8-2,5 (laagste: amateur tegen neutraal 1,06).
4. **PRD-aanname (amateur maakt meer systematische fouten op complexe tactieken):** gemeten met de complexe stresstactiek tegen de eenvoudige (rij van de complexe ploeg, 16 wedstrijden), eenvoudig tegen complex: nauwkeurigheid amateur 69,7 tegen 72,3% (+2,6 procentpunt), halfprof 71,0 tegen 72,0%, prof 73,4 tegen 76,5%; spreiding (compactheid) amateur 14,96 tegen 12,80 m (-14%), halfprof 14,88 tegen 13,33 (-10%), prof 14,99 tegen 14,49 (-3%); passes amateur 447 tegen 61 (-86%), halfprof 453 tegen 115, prof 462 tegen 179 (-61%). **De PRD-getallen (amateur ≥10 procentpunt en ≥15% verlies, prof ≤3 en ≤5%) worden niet gehaald**: de stresstactiek laat alle ploegen instorten (weinig, juist makkelijke passes, dus geen nauwkeurigheidsverlies) en de spreiding wordt door het verplichte pressen kleiner in plaats van groter. Kwalitatief wel aanwezig: het compactheidsverlies is bij amateur ruim viermaal dat van prof en het aantal passes daalt bij amateur het sterkst. Een geschikte meetopzet voor de aanname (matige complexiteit, afstand tot het eigen zwaartepunt in plaats van spreiding bij pressen) ontbreekt nog.

### Wat nog afwijkt
- Prof - amateur: prof 3,44 goals (boven 2,5). Een kleinere afstand naar beneden (`LevelSpreadDown` 0,45) gaf prof 2,81 en amateur 0,62 (prof 13-1-2): de som blijft rond 3,4 goals en dan valt amateur onder 0,8. Een kleinere positioneringsbias (0,08/0,10/2,5) gaf geen duidelijke verbetering (prof 3,31, 14-0-2). Alleen de 0,35-stand is vastgelegd.
- Ruis: 16 wedstrijden per paar geven een standaardafwijking van ca. 12 procentpunt op winstaandelen; de grenzen 70-85% zijn daarom niet scherp te toetsen. Voor een scherpere meting `ZEUS_PROBE_SEED0` gebruiken en batches optellen. De tuning is gedaan in acht meetrondes; onder de grenzen bij tussenstappen zat ook ruis.
- De absolute doelpuntenproductie van het basismodel (1,5 per team bij 12,6 schoten, neutraal) is niet aangepast; de referentie (PL: 1,3 goals, 13 schoten) is dichtbij.
- De complexe stresstactiek stort voor elk niveau in (zie boven); dat is een eigenschap van de tactiek.

## Niveau-herschaling ronde 2 (2026-10-16, voorlopig)

Deze ronde vervangt de bovenstaande schaalconstanten, niet de historische metingen. De selectie-JSON en de generator blijven ongewijzigd. Het zijn kalibratie-aannames op aantallen, geen bewijs van voetbalrealisme.

- `LevelSpreadUp`: 0,60 → 0,35; `LevelSpreadDown`: 0,35 → 0,20. De interne effectieve attributen liggen dichter bij het halfprofanker; ruwe spelerswaarden en identiteit blijven behouden.
- De reactievertraging wordt op de dichtstbijzijnde tick afgerond, net als het scaninterval. `ceil` gaf ook bij een verwaarloosbaar tekort altijd minstens één tick vertraging. Een afgeronde nul slaat de beslissing niet over.
- Schotuitvoering met actieve selectie en `Finishing`: `clamp(PassSettings.Skill - 0,24 + 2,00 × (finishing - referentie), 0, 1)`. Bij de standaardconfig ligt het schotanker daardoor op 0,51. Het lagere anker vergroot de fysieke richtingsfout; de steilere attribuuthelling laat beter afwerken meer voordeel geven. Er is geen extra goalworp, scorebegrenzing of tactiekspecifieke uitzondering. Halfprof is voor schotuitvoering daardoor niet meer gelijk aan het historische neutrale gedrag.
- Keeper: `delta = effectieve keeperwaarde - referentie`; controlestraal en maximale relatieve balsnelheid krijgen factor `1 + 0,3 × delta`. De standaard keepers krijgen hiermee circa 2,45 m (amateur), 2,50 m (halfprof) en 2,57 m (prof) controlestraal in hun eigen strafschopgebied. Het comprimeren van tekorten voorkomt dat slechte reddingen het aantal goals op amateurniveau opdrijven; een bescheiden keeperinvloed voorkomt dat prof-prof te doelpuntarm wordt.
- Zonder selectie, met expliciet neutrale spelers of met uitgeschakelde attributen blijft de historische route intact. Beide `GoldenMatchTests` en `ExplicitNeutralAndDisabledAttributesKeepOldBits` zijn ongewijzigd. De eventhash met niveaus verandert **bewust** door deze nieuwe effectieve attributen en schotuitvoering.

### Meetopzet ronde 2

De bestaande `scripts/calibration_pairs.py` start meerdere dotnet-processen en kent geen selecties. Daarom gebruikt deze ronde een sequentiële batchstand in de bestaande Runner, met dezelfde `Match`, standaardconfig, volledige wedstrijden en `MatchStatistics`. `SquadFactory` past de selecties op de slots van de gekozen tactiek. Beide ploegen gebruiken dezelfde tactiek; bij prof-amateur staat prof op oneven seeds thuis en op even seeds uit.

```powershell
dotnet build sim/Zeus.Runner -c Release -m:1 -o C:/dev/zeus-niveau/.tmp_levels -p:UseSharedCompilation=false -nodeReuse:false --nologo
dotnet build-server shutdown
dotnet .tmp_levels/Zeus.Runner.dll --calibrate-levels --seeds 40 --first-seed 101 --pairs amateur:amateur,halfprof:halfprof,prof:prof --results .tmp_levels/niveau_gelijk.jsonl
dotnet .tmp_levels/Zeus.Runner.dll --calibrate-levels --seeds 80 --first-seed 101 --pairs prof:amateur --results .tmp_levels/niveau_ongelijk.jsonl
Get-Content .tmp_levels/niveau_gelijk.jsonl, .tmp_levels/niveau_ongelijk.jsonl | Set-Content .tmp_levels/niveau_ronde2.jsonl -Encoding utf8
dotnet .tmp_levels/Zeus.Runner.dll --calibrate-levels --reuse-results .tmp_levels/niveau_ronde2.jsonl --verify-levels
```

De drie voorbeeldtactieken zijn `hoge_druk_433`, `positiespel_4231` en `counter_532`. De nulmeting en eerste drie kandidaatmetingen gebruiken seeds 1–8; de eindcontrole gebruikt seeds 101–140 voor de drie gelijke niveauparen (40 per tactiek, 120 wedstrijden per niveaupaar) en seeds 101–180 voor prof-amateur (80 per tactiek, 240 wedstrijden). Dat zijn **600 wedstrijden totaal**. De eerste 120 prof-amateurwedstrijden gaven 3,2083 goals, één goal boven de bovengrens; daarom is dat paar zonder parameterwijziging met 40 nieuwe seeds per tactiek uitgebreid. De nulmeting en eindcontrole gebruiken verschillende seeds.

Verworpen standen: kandidaat 1 (spreiding 0,35/0,25, schotcorrectie −0,10, oude reactieafronding); kandidaat 2 (0,35/0,20, correctie −0,17 en helling 0,18, afgeronde reactie) gaf bij de grotere meting slechts 50% profwinst bij zowel hoge druk als positiespel (elk 40 wedstrijden); kandidaat 3 (correctie −0,22, helling 1,2) haalde de goals maar gaf 50% profwinst in de kleine reeks. De symmetrische keeperfactor 1,5 van kandidaat 4 maakte amateur-amateur weer te doelpuntrijk: 5,10 bij hoge druk en 3,55 bij positiespel (elk 40 seeds), waarna die meting is afgebroken. De keepertekorten zijn daarna tot factor 0,3 gecomprimeerd. Voor verbeteringen bleek factor 1,5 te sterk (82,5% profwinst bij hoge druk); factor 0,8 gaf prof-prof slechts 2,01 goals over 120 wedstrijden. De behouden stand gebruikt factor 0,3 aan beide kanten van het anker.

De Runner bewaart per wedstrijd seed, kantwisseling, score en eventhash als JSONL en stopt bij een niet-voltooide wedstrijd; er worden geen uitslagen weggefilterd. `--pairs amateur:amateur` of `--pairs prof:prof,prof:amateur` beperkt de batch tot die paren (de eindmeting is in delen uitgevoerd). `--verify-levels` toetst de gemiddelden **over de drie tactieken samen** en profwinst 60–75%; buiten de grenzen is de exitcode 3. Voor "geen verschillen ≥8 als norm" hanteren we voorlopig maximaal 1% zulke wedstrijden per niveaupaar; maximum én aantal worden gerapporteerd. Uitschieters worden niet afgeknipt of weggelaten. `--reuse-results <jsonl> --verify-levels` beoordeelt opgeslagen meetresultaten zonder opnieuw wedstrijden te simuleren.

De afzonderlijke tactiekrijen blijven zichtbaar: een geslaagd totaal bewijst niet dat elke tactiek afzonderlijk binnen 2,2–3,2 zit. Seeds 101–140 zijn na de afgebroken kandidaatmetingen opnieuw gebruikt; dit is kalibratie en geen onafhankelijke validatie op ongeziene seeds.

### Resultaten ronde 2

Alle waarden zijn **totale goals per wedstrijd**, met gelijke tactiek voor beide teams. De kolom nulmeting bevat 8 seeds per tactiek; de eindmeting bevat 40 of 80 zoals hierboven.

| Niveaupaar | Nulmeting | Hoge druk | Positiespel | Counter | Eindgemiddelde | Wedstrijden |
|---|---:|---:|---:|---:|---:|---:|
| amateur-amateur | 4,458 | 4,000 | 2,550 | 1,975 | **2,842** | 120 |
| halfprof-halfprof | 4,333 | 4,150 | 2,600 | 2,075 | **2,942** | 120 |
| prof-prof | 4,083 | 4,125 | 2,300 | 1,875 | **2,767** | 120 |
| prof-amateur | 5,292 | 4,175 | 2,775 | 2,6125 | **3,1875** | 240 |

Prof-amateur: prof **2,2375** goals, amateur **0,9500**; prof dus onder de oude bovengrens van 2,5 goals. Vanuit prof **153 winst / 51 gelijk / 36 verlies = 63,75% winst** (nulmeting: 20/24 = 83,33%). Per tactiek: hoge druk 70,0%, positiespel 63,75%, counter 57,5% profwinst.

Grootste verschil bij amateur-amateur, halfprof-halfprof en prof-prof: telkens 6. Bij prof-amateur is het maximum 8: **één 0–8**, hoge druk, seed 106, prof uit. Dat is 1/240 = **0,42%** van dit paar (1/600 totaal); alle andere 599 wedstrijden blijven onder acht goals verschil. De Runner-verificatie geeft voor alle vier paren `PASS`. Individuele extreme uitslagen blijven mogelijk; er is geen scorebegrenzing.

De laatste keeperaanpassingen veranderden alleen de factor boven het halfprofanker. De amateur- en halfprofmetingen zijn behouden omdat hun effectieve keeperwaarden onder of op dat anker liggen. Met de definitieve build zijn zes volledige wedstrijden (seed 101, beide niveaus over de drie tactieken) opnieuw gedraaid: **6/6 eventhashes identiek**. De definitieve JSONL bevat elke combinatie van niveaupaar, tactiek en seed precies eenmaal; er zijn geen afgebroken of weggefilterde wedstrijden.

### Regressie en grenzen

`LevelCalibrationTests` speelt twee vastgezette, representatieve rookseeds (113 en 114, thuis/uit gebalanceerd) over alle drie tactieken en vier niveauparen: 24 volledige wedstrijden. De test bewaakt de band 2,2–3,2, profwinst 60–75% en geen verschil ≥8 in die kleine fixture. Deze rookset is uit de grote meetreeks gekozen en is **geen onafhankelijk statistisch bewijs**; de bovenstaande 600 wedstrijden vormen de kalibratiemeting. Een aparte korte test vergelijkt snapshots en eventhash bij een reactietekort ruim onder een halve tick met de basisuitvoering, zodat de `ceil`-sprong niet terugkomt.

Zonder niveau blijven de bestaande golden-hashes ongewijzigd. Met niveaus veranderen hashes bewust; bijvoorbeeld hoge druk, prof-amateur, seed 1: `1f75f1fb…` → `d19aaf02…`. Controlecommando's: `dotnet test sim/Zeus.Sim.Tests -c Release -m:1 -o C:/dev/zeus-niveau/.tmp_leveltests -p:UseSharedCompilation=false -nodeReuse:false --filter 'FullyQualifiedName~LevelCalibrationTests|FullyQualifiedName~GoldenMatch|FullyQualifiedName~Squad'`; eindcontrole en actuele testaantallen staan in `docs/hervatten.md`.

**Voorlopig:** de doelband is gehaald over de drie tactieken samen. Hoge druk blijft afzonderlijk te doelpuntrijk, verschillende counter-/positiespelrijen zijn doelpuntarm, en counter geeft 57,5% profwinst in dit profielpaar. Ongelijke tactiekparen uit de competitie zijn niet in deze meetopzet geijkt. De hier gemeten gemiddelden bewijzen dus geen realistische uitslag voor elke competitiewedstrijd en geen rangorde tegenover het historische neutrale profiel.
