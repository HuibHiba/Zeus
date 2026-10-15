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
