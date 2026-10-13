# Zeus hervatten

Stand 2026-10-13: mechanische fase 1a en een eerste regelbasis van 1b zijn gebouwd. 160 tests groen, inclusief 8 tests van de voorbereide vermoeidheidsmodule voor 1c. Geen volledige IFAB- of realismeclaim. Claude werkt daarnaast aan scripts/calibration en referentiedata; Codex aan sim/, godot/ en de technische overdracht. Lees de nieuwste git-log én het sessielog in Obsidian; commits van beide kunnen tussen checkpoints verschijnen.

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

`--steps` is een technisch aantal ticks. `--until-finished` speelt twee helften inclusief toegevoegde pauzetijd, met een veiligheidslimiet van 150000 ticks. Finale snapshot wordt altijd uitgevoerd. De klok is totale simulatietijd inclusief beide helften en toegevoegde tijd; de echte pauze van 15 minuten wordt overgeslagen. Event.Period bewaart de helft. Replay bevat alle vaste invoer, seed, exacte ticks, compiler-buildfingerprint en verwachte eventhash. Nieuwe kernbuild vereist verse replay. Live externe incidents/custom brains worden nog niet als inputtijdlijn opgenomen.

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
Nog geen handen/benen/botsingskracht voor tackle-ernst, SPA/DOGSO, voordeel, opzettelijk hands, keeper-8-secondenadministratie, indirecte-overtredingen buiten buitenspel/doubletouch, specifieke goal-area/wall/quick-kick-excepties, penalty-encroachment/accidental-doubletouch, paal/lat/netbotsing of bijzondere wedstrijdreglementen. De keepercontrole maakt nog geen expliciet hand-held-state. Role-rescue na rood is een prototype. Vermoeidheid staat klaar als pure functie maar nog niet in Match. De echte zes fases/blokwissels/triggers volgen in 1c.

## Proef
Seed42 laag_blok_442 tegen man_om_man_433: eindstatus Finished, 59730 ticks/5973s inclusief 573s onderbrekingen, uitslag0–9, 31 schoten/22 saves, 678/864 passes, 54/48 overtredingen. 28 offsides, 4 doubletouch, 191 hervattingen. Nul kaarten omdat automatische incidents alleen Careless zijn. Circa5s headless zonder regelmatige snapshotuitvoer. Veel te veel duels/fouls, geen valide realisme. Docs/fase1b-proef.md beschrijft definities. Opname/replay geven gelijke event- én snapshot-hashes; regeltests en korte Godot replay zijn apart gecontroleerd.

## Vervolg
Fase1c: Fatigue.cs voorbereide twee reservoirs integreren, echte zes spelfases en bloc-/triggergedrag. Daarna vergelijken met Claude's referenties en fysische incidenten verfijnen; geen outcome-kansmodel dubbel over fysica heen leggen.