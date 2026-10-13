# Zeus hervatten

**Dit bestand is de enige bron van waarheid voor stand, testaantallen en commit.** README, CLAUDE.md en de vaultnotities verwijzen hiernaar en nemen geen aantallen over.

## Stand (2026-10-13)
- **Tests:** `dotnet test Zeus.sln` gaf 230 tests groen (controle 2026-10-13, na de triggers). Draai het zelf opnieuw; dit getal veroudert.
- **Laatste commits:** zie `git log`. Recent: tactiektriggers uitgevoerd, pressing uit eigen waarneming (`be1b7f7`), kalibratieronde 2 met oprukken, wegwerptrap en wegtippen (`241754b`), passmodel zonder lekkage (`f1a0e0f`), runner-guard (`c1bec0e`). Controleer altijd zelf `git log` en `git status`: de werkmap kan ongecommitte wijzigingen bevatten.
- **Fase:** mechanische fase 1a, regelbasis 1b en de koppeling van fase 1c zijn gebouwd: vermoeidheid (`Match.Fatigue.cs`, `EnableFatigue`), teamspelfases (`Match.Phases.cs`, `EnableTeamPhases`, events `PhaseChange`) en blokwissels (`EnableBlockSwitches`, `BlockSelection`, events `BlockChange`) zijn in `Match` gekoppeld. Tactiektriggers `bal_naar_keeper`, `slechte_aanname` en `tegenstander_ontvangt_op_flank` (alleen `start_pressing` in de fase verdedigen) worden uitgevoerd volgens v0-aannames in `docs/blokwissels.md`; de overige voorwaarden en triggers met zone blijven inactief.
- **Kalibratie:** de aantallen per team zijn op één tactiekpaar (laag_blok_442 thuis, man_om_man_433 uit) bijgesteld naar Premier League 2015/16; zie `docs/kalibratie.md`. Geen realismeclaim.
- **Werkverdeling:** Claude levert data, referenties, ontwerp en kalibratie (`scripts/`, `calibration/`, `competitions/`, `data/`, vault); Codex levert sim-uitbreidingen (`sim/`, `godot/`). Beide mogen in dezelfde repo maar nooit tegelijk hetzelfde bestand. Lees de nieuwste git-log én het sessielog in Obsidian; commits van beide kunnen tussen checkpoints verschijnen.

## Controleren en opnemen
Vanuit C:\dev\zeus:

```powershell
dotnet test Zeus.sln
dotnet build godot/Zeus.Godot.csproj
.\.venv\Scripts\python.exe scripts\validate_tactics.py
dotnet run --project sim/Zeus.Runner -- --home tactics/laag_blok_442.json --away tactics/man_om_man_433.json --seed 42 --until-finished --out data/match.jsonl --every 10 --events data/events.jsonl --summary data/summary.json --replay-out data/match.replay.json
dotnet run --project sim/Zeus.Runner -- --replay data/match.replay.json --out data/replayed.jsonl --every 10
.\.venv\Scripts\python.exe scripts\compare_simulation.py data/summary.json --out data/benchmark.md
```

`--steps` is een technisch aantal ticks. `--until-finished` speelt twee helften inclusief toegevoegde pauzetijd, met een veiligheidslimiet van 150000 ticks. Finale snapshot wordt altijd uitgevoerd. De klok is totale simulatietijd inclusief beide helften en toegevoegde tijd; de echte pauze van 15 minuten wordt overgeslagen. Event.Period bewaart de helft. Replay bevat alle vaste invoer, seed, exacte ticks, buildfingerprint en verwachte eventhash. Nieuwe kernbuild vereist verse replay. Live externe incidents en eigen breinen (`brainFactory`) worden niet als inputtijdlijn opgenomen: een replay reproduceert alleen de standaardbreinen.

## Determinisme (precieze claim)
Zelfde seed, zelfde build **en zelfde platform/runtime** geeft dezelfde wedstrijd. De `BuildHash` in een replay is alleen de assembly-MVID (compiler-moduleversie), geen hash van platform, CPU of runtime. `Math.Sin/Cos/Atan2/Sqrt` zijn niet bitgarandeerd identiek op een andere CPU, OS of .NET-runtime, dus identiek resultaat op een andere machine is niet gegarandeerd. De Godot-replaycontrole is kort (een runner-replay van 20 stappen). **Cross-machine:** `GoldenMatchTests` (twee volledige wedstrijden, event- en snapshothash) legt de hashes vast voor deze machine. Draai `dotnet test Zeus.sln --filter GoldenMatch` op een andere machine/OS/CPU met dezelfde code: slaagt het, dan is de simulatie daar bit-identiek; faalt het zonder codewijziging, dan is dat bewijs van een platformverschil in `Math.*`. **Nog niet op een tweede machine uitgevoerd** (open). Na een bedoelde gedragswijziging de hashes in de test bijwerken en dat in de commit vermelden.

## Bekijken
Open godot/project.godot met Godot 4.7.2 .NET. Spatie pauzeert; 1/4 kiezen snelheid; D toont doelen, passintenties en ids. Paneel toont score/helft/status/events. ZEUS_REPLAY met absoluut pad speelt een opgenomen match en controleert de eventhash; einde pauzeert. Zie docs/debug-weergave.md.

## Gebouwd
- Beperkt zicht/ballgeheugen; passes naar waargenomen spelers; balfout zonder tweede pass-/goalworp; continu contact per fysicasubstep; pressing en duels.
- Rule subset Referee.Assess voor expliciete incidenten, met vereiste opponent/ernst/contact/context. Match controleert in-play en eindige locatie vóór effecten. Geen actieve randomkaartkeuze. Synthetische careless charges blijven actiemodelaannames; hun ligging bepaalt nu vrije trap of penalty.
- Geel, rood, tweede geel; uitgesloten spelers bewegen/controleren niet. Minder dan zeven -> Abandoned. Na rood voor keeper wordt een veldspeler voorlopig keeper; geen vervangings-/managerbediening.
- PitchRules hele-balcriterium; goals en aftrap; inworp, doelschop en corner op basis laatste aanraking. Hervattingen worden echt getrapt/gegooid. Directe goal-uitzonderingen en tweede-aanraking vóór een andere speler getest.
- Buitenspelpositie op het kickmoment; kicker zelf uitgesloten. Alleen bal spelen activeert v0-overtreding. Directe ontvangst uit inworp/doelschop/corner is vrijgesteld; deliberate save bewaart context. Interfereren zonder aanraking/body-part-uitbreiding ontbreekt nog.
- Twee helften, kantwissel/spiegeling, verloren pauzetijd toegevoegd, penalty verlengt een periode tot afgehandeld, eindsignaal. Replay bewaart het eindsignaal op de exacte laatste tick.
- Keeper kijkt naar bekende bal, kan achteruit/lateraal bewegen en volgt een zichtbare geschatte schotbaan. Binnen eigen box hogere/snellere controle; na eigen-team-kick/throw alleen voetcontrole beschikbaar. Keeperwaarden ongekalibreerd.

## Grenzen
Nog geen handen/benen/botsingskracht voor tackle-ernst, SPA/DOGSO, voordeel, opzettelijk hands, keeper-8-secondenadministratie, indirecte-overtredingen buiten buitenspel/doubletouch, specifieke goal-area/wall/quick-kick-excepties, penalty-encroachment/accidental-doubletouch, paal/lat/netbotsing of bijzondere wedstrijdreglementen. De keepercontrole maakt nog geen expliciet hand-held-state. Role-rescue na rood is een prototype. Vermoeidheid, teamfases en blokwissels zijn in Match gekoppeld (zie Stand); drie tactiektriggers worden uitgevoerd volgens v0-aannames (`docs/blokwissels.md`). Pressing (`PressingAssignment.IsPresser`) volgt uit de eigen waarneming van de verdediger (`docs/waarneming.md`).

## Proef en kalibratie
De cijfers van de eerste proef (seed 42, uitslag 0-9, 54/48 overtredingen) zijn vervangen door de kalibratie. Actuele aantallen, meetopzet (`scripts\calibration_run.py`) en parameters staan in `docs/kalibratie.md`. Samenvatting eindstand (8 seeds, gemiddelde per team): 485 passes, 81,2% geslaagd, 15,9% onderschept, 10,9 overtredingen, 1,7 gele kaarten, 16,3 schoten, 2,0 doelpunten. **Nog afwijkend:** inworpen (1,2 tegen doel 23), corners (0 tegen 5,4), buitenspel (0,5 tegen 1,8), passkans per lengte (10-20 m 98% tegen 88-90%; 30-40 m 44% tegen 66%) en het ongelijke tactiekpaar (laag blok ca. 1 schot, man-om-man ca. 31 per wedstrijd). Dit is kalibratie van aantallen op een tactiekpaar, geen bewijs van voetbalrealisme. `docs/fase1b-proef.md` is de historische mechanische proef. Opname/replay geven gelijke event- én snapshot-hashes; regeltests en een korte Godot-replay zijn apart gecontroleerd.

## Vervolg
Kalibratie van de resterende afwijkingen (hierboven) en een symmetrisch tactiekpaar; triggers (`bal_naar_keeper`, `slechte_aanname`, `tegenstander_ontvangt_op_flank`) pas uitvoeren nadat hun semantiek is vastgelegd; pressing uit eigen waarneming; cross-machine golden-hash-test. Daarna fysische incidenten verfijnen; geen outcome-kansmodel dubbel over fysica heen leggen. Let op: de passmodellen in `calibration/` lekken de uitkomst (eindpunt) en zijn pas bruikbaar als kans vóór de pass na een herfit.