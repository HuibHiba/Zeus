# Zeus hervatten

**Dit bestand is de enige bron van waarheid voor stand, testaantallen en commit.** README, CLAUDE.md en de vaultnotities verwijzen hiernaar en nemen geen aantallen over.

## Stand (2026-10-14)
- **Tests:** draai `dotnet test Zeus.sln -m:1 --nologo` zelf; het testaantal staat bewust niet hier (het veroudert). Na ronde 5 slaagt alles; de timingtest `NinetyMinutesRunInUnderTenSeconds` kan onder belasting falen en slaagt apart gedraaid.
- **Laatste commits:** zie `git log`. Recent (nacht 2026-10-13/14): buitenspel met dieptelopen en steekpassen (`68f2454`), terugspelen op de eigen keeper (`c877163`), zones en bezitwissel-triggers (`fadc432`), kalibratieronde 3 buitenspel (`416132c`), kalibratieronde 4 (`dbc23b2`), Godot-debugweergave (`b1c9227`), kalibratieronde 5 buitenspel (zie `git log`). Controleer altijd zelf `git log` en `git status`: de werkmap kan ongecommitte wijzigingen bevatten.
- **Fase:** mechanische fase 1a, regelbasis 1b en de koppeling van fase 1c zijn gebouwd: vermoeidheid (`Match.Fatigue.cs`, `EnableFatigue`), teamspelfases (`Match.Phases.cs`, `EnableTeamPhases`, events `PhaseChange`) en blokwissels (`EnableBlockSwitches`, `BlockSelection`, events `BlockChange`) zijn in `Match` gekoppeld. Alle zes tactiektriggervoorwaarden (`bal_naar_keeper`, `bal_terug_naar_middenvelder`, `balverlies_in_eigen_helft`, `balwinst_in_hoge_zone`, `slechte_aanname`, `tegenstander_ontvangt_op_flank`) worden uitgevoerd volgens v0-aannames in `docs/blokwissels.md`, met zones waar de voorwaarde er een heeft. Alleen de instructie `start_pressing` wordt uitgevoerd; andere instructies blijven inactief.
- **Kalibratie:** de aantallen per team zijn bijgesteld naar Premier League 2015/16 met het **symmetrische paar** (`laag_blok_442` tegen zichzelf) als referentiepaar; het ongelijke paar (laag_blok_442 thuis, man_om_man_433 uit) is ook gemeten en blijft scheef (tactisch effect, geen kalibratiefout); zie `docs/kalibratie.md`. Geen realismeclaim.
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
Open godot/project.godot met Godot 4.7.2 .NET. Spatie pauzeert; 1/4 kiezen snelheid; D schakelt alle debuglagen tegelijk; F, T, P, O en E per laag (zie `docs/godot-debug.md`). Paneel toont score/helft/status/events. ZEUS_REPLAY met absoluut pad speelt een opgenomen match en controleert de eventhash; einde pauzeert. Zie docs/debug-weergave.md.

## Gebouwd
- Beperkt zicht/ballgeheugen; passes naar waargenomen spelers; balfout zonder tweede pass-/goalworp; continu contact per fysicasubstep; pressing en duels.
- Rule subset Referee.Assess voor expliciete incidenten, met vereiste opponent/ernst/contact/context. Match controleert in-play en eindige locatie vóór effecten. Geen actieve randomkaartkeuze. Synthetische careless charges blijven actiemodelaannames; hun ligging bepaalt nu vrije trap of penalty.
- Geel, rood, tweede geel; uitgesloten spelers bewegen/controleren niet. Minder dan zeven -> Abandoned. Na rood voor keeper wordt een veldspeler voorlopig keeper; geen vervangings-/managerbediening.
- PitchRules hele-balcriterium; goals en aftrap; inworp, doelschop en corner op basis laatste aanraking. Hervattingen worden echt getrapt/gegooid. Directe goal-uitzonderingen en tweede-aanraking vóór een andere speler getest.
- Buitenspelpositie op het kickmoment; kicker zelf uitgesloten. Alleen bal spelen activeert v0-overtreding. Directe ontvangst uit inworp/doelschop/corner is vrijgesteld; deliberate save bewaart context. Interfereren zonder aanraking/body-part-uitbreiding ontbreekt nog.
- Twee helften, kantwissel/spiegeling, verloren pauzetijd toegevoegd, penalty verlengt een periode tot afgehandeld, eindsignaal. Replay bewaart het eindsignaal op de exacte laatste tick.
- Buitenspel: positie op het trapmoment (`OffsideRules`), dieptelopen en steekpassen met lijnruis, passkeuze en kandidaten bij de aanraking (v0, `docs/buitenspel.md`; één afwijking in de contactcode staat daar beschreven).
- Terugspelen op de eigen keeper onder druk, keeper-bewustzijn en rondomzicht op spelers (v0, `docs/waarneming.md`); pressing uit de eigen waarneming.
- Keeper kijkt naar bekende bal, kan achteruit/lateraal bewegen en volgt een zichtbare geschatte schotbaan. Binnen eigen box hogere/snellere controle; na eigen-team-kick/throw alleen voetcontrole beschikbaar. Keeperwaarden ongekalibreerd.

## Grenzen
Nog geen handen/benen/botsingskracht voor tackle-ernst, SPA/DOGSO, voordeel, opzettelijk hands, keeper-8-secondenadministratie, indirecte-overtredingen buiten buitenspel/doubletouch, specifieke goal-area/wall/quick-kick-excepties, penalty-encroachment/accidental-doubletouch, paal/lat/netbotsing of bijzondere wedstrijdreglementen. De keepercontrole maakt nog geen expliciet hand-held-state. Role-rescue na rood is een prototype. Vermoeidheid, teamfases en blokwissels zijn in Match gekoppeld (zie Stand); alle zes triggervoorwaarden worden uitgevoerd volgens v0-aannames (`docs/blokwissels.md`). Pressing (`PressingAssignment.IsPresser`) volgt uit de eigen waarneming van de verdediger (`docs/waarneming.md`).

## Proef en kalibratie
De cijfers van de eerste proef (seed 42, uitslag 0-9, 54/48 overtredingen) zijn vervangen door de kalibratie. Actuele aantallen, meetopzet (`scripts\calibration_run.py`) en parameters staan in `docs/kalibratie.md`; dit bestand neemt ze niet over. Laatste kalibratieronde: ronde 5 (buitenspel) op het symmetrische paar, daarvoor ronde 4. De open afwijkingen (passsucces, onderschepping, inworpen, corners, doelpunten per schot, kaarten en het ongelijke tactiekpaar) staan in `docs/kalibratie.md` onder ronde 4 en "Wat nog afwijkt". Dit is kalibratie van aantallen op een referentiepaar, geen bewijs van voetbalrealisme. `docs/fase1b-proef.md` is de historische mechanische proef. Opname/replay geven gelijke event- én snapshot-hashes; regeltests en een korte Godot-replay zijn apart gecontroleerd.

## Vervolg
Gedaan: pressing uit eigen waarneming, alle zes triggervoorwaarden met zones (v0), symmetrisch referentiepaar, `GoldenMatchTests`, buitenspel met dieptelopen en steekpassen (v0), terugspelen op de keeper (v0), en kalibratierondes 3 en 4. Nog open:
- **Buitenspel:** de contactcode wist de kandidaten bij elke niet-redding-aanraking; het opzettelijk/afwijking-onderscheid ontbreekt (`docs/buitenspel.md`).
- **Kalibratie:** de afwijkingen uit ronde 4 (onderschepping, inworpen, corners, doelpunten per schot, kaarten, buitenspel na de snellere passes); het rondomzicht van 25 m is nog niet met een eigen meetronde onderbouwd (`docs/kalibratie.md`).
- **Triggers:** instructies anders dan `start_pressing` blijven inactief.
- `dotnet test Zeus.sln --filter GoldenMatch` op een tweede machine, daarna fysische incidenten verfijnen; geen outcome-kansmodel dubbel over fysica heen leggen. De passmodellen zonder uitkomstlek zijn `calibration/event_models_v2.json`, `pass_360_model_v2.json` en `wyscout_models.json` (de StatsBomb-modellen staan alleen lokaal; de oude `pass_360_model.json` lekt de uitkomst en is niet bruikbaar als kans vóór de pass).

## Effectiviteitsmeter (Claude, 2026-10-15)
`scripts\tactic_meter.py A.json B.json --n 20` speelt elke seed twee keer met omgewisselde thuis/uit-rol en schrijft `data/meter/rapport.md`: uitslagen, gemiddelden, balbezit en een "waarom"-lijst met de drie grootste verschillen. Eerste mijlpaal-3-onderdeel (PRD); geen realismeclaim. Heatmaps en dashboard in Godot volgen.

## Godot-app: menu, editor, eindscherm (Claude, 2026-10-15)
Startscène is nu `Menu.tscn`: kies thuis- en uittactiek uit `tactics/`, seed, start wedstrijd of open de editor; Esc keert uit een wedstrijd terug naar het menu. `Editor.tscn`/`Editor.cs`: spelers per spelfase slepen, opslaan als nieuw bestand in `tactics/` via `TacticEditorModel` (`sim/Zeus.Sim`, valideert met de echte loader; `gelijk_aan`-fases worden zelfstandig bij bewerken). Na het eindsignaal toont `Main` een eindscherm (uitslag, schoten, passes, balbezit, overtredingen, buitenspel uit de eventstroom). Gecontroleerd: build en headless laden van alle drie de scènes; **niet** handmatig geklikt (sleep, opslaan, eindscherm moeten nog door een mens worden bekeken). Instructies, triggers en blokwissels zijn nog niet bewerkbaar in de editor.
