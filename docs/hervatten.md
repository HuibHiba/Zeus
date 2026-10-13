# Zeus hervatten

Stand 2026-10-13: de mechanische taken van fase 1a (1–26) zijn uitgevoerd; vergelijking en kalibratie blijven open. 106 tests groen. Volgende fase 1b: regels, lijnhervattingen, doelen, strafschopgebied en buitenspel. De echte wedstrijd lijkt nog onvoldoende op voetbal: veel te veel duels/overtredingen en afwijkende passes.

## Controleren en opnemen
Vanuit C:\dev\zeus:

```powershell
dotnet test Zeus.sln
dotnet build godot/Zeus.Godot.csproj
.\.venv\Scripts\python.exe scripts\validate_tactics.py
dotnet run --project sim/Zeus.Runner -- --home tactics/laag_blok_442.json --away tactics/man_om_man_433.json --seed 42 --steps 54000 --out data/match.jsonl --every 10 --events data/events.jsonl --summary data/summary.json --replay-out data/match.replay.json
dotnet run --project sim/Zeus.Runner -- --replay data/match.replay.json --out data/replayed.jsonl --every 10
.\.venv\Scripts\python.exe scripts\compare_simulation.py data/summary.json --out data/benchmark.md
```

Data-map moet bestaan. Replay bevat de exacte configuratie, beide tactiekteksten, seed, aantal ticks, buildfingerprint en verwachte eventhash. De fingerprint gebruikt de compiler ModuleVersionId, niet het pad van een geladen DLL (Godot laadt bytes). Een nieuwe kernbuild vereist een nieuwe replay.

## Bekijken
Open godot/project.godot met Godot 4.7.2 .NET. Spatie pauzeert; 1/4 kiezen snelheid; D schakelt loopdoelen, passintenties, ids en eventpaneel in. Gebruik ZEUS_REPLAY met een absoluut replaypad om de opname exact af te spelen. Einde controleert de eventhash en pauzeert. ZEUS_DEBUG=1 start met debug. Meer in docs/debug-weergave.md.

## Gedrag en grenzen
- FormationBrain blijft beschikbaar voor geïsoleerde bewegingstests; de standaard is PassingBrain. Zicht, geheugen en vaste scaninterval staan in docs/waarneming.md. Zonder balbezit blijven stilstaande spelers scannen om een bal achter zich te vinden.
- Passkeuze gebruikt alleen waargenomen spelers; uitvoering brengt richting-/snelheidsfout aan. BallControl controleert per substep bereik, hoogte en relatieve snelheid; geen tweede passsuccesworp.
- Pressing wijst de dichtstbijzijnde verdediger aan die de bal kan waarnemen. Duelmodel gebruikt voorlopige vaste skill/discipline/geen vermoeidheid. Duels zijn actie-uitkomsten, niet scheidsrechterkaarten.
- Referee.Assess beoordeelt expliciete ernst/contact/context volgens een beperkte regelset. ReportIncident past geel/rood/tweede geel toe. Incomplete incidenten worden afgewezen. Actieve v0-duels melden alleen synthetische Careless-charge-scenario's, zonder vastgesteld lichaamscontact, SPA/DOGSO of strafschopgebied; dat is een open implementatiepunt. Oude CardModel-kansen zijn een ongebruikt experiment, niet actieve arbitrage.
- Stillegging 3 s, tegenstanders 9,15 m van de bal, daarna eenvoudige hervatting. Uit is nog een voorlopige grondhervatting voor de andere ploeg. Nemer staat iets binnen het veld; bij geen actieve nemer volgt Abandoned.
- Nog geen schoten, goals, strafschopuitvoering, buitenspel, echte ingooien/doelschoppen/corners, rust/kantwissel, blessuretijd, handbal, voordeel of tactische overtredingen. Nieuwe pure PitchRules kan parallel voorbereid zijn; check git-status vóór hervatten.

## Resultaten
Twee volledige runner-runs (opname en replay) hebben dezelfde event-/snapshot-hash. Godot korte replay is op exact 20 ticks gecontroleerd; onjuiste hash geeft één fout en exit1. Headless opstart en echte debugrenders zijn bekeken. Toetsen zijn nog niet handmatig bediend.

90 minuten kosten circa 4 s zonder regelmatige snapshot-output op deze laptop. Proef seed42 laag_blok_442 tegen man_om_man_433: thuis607 passes/73 overtredingen, uit836/53. Nul kaarten in deze synthetische Careless-proef is geen bewezen kaartkalibratie. Benchmarkmean485,025 uit760 PL-teamwedstrijden; zie docs/benchmarkvergelijking.md en lokale data/verification/benchmark.md. Geen passnauwkeurigheid afgeleid uit losse balcontacten.

## Bestanden
Passing.cs, BallControl.cs, Pressing.cs, DuelModel.cs, Referee.cs, Replay.cs en Statistics.cs zijn de nieuwe modules. Match.cs integreert ze. Officiële bronlinks en nog benodigde inputvelden staan in docs/scheidsrechter-regels.md en de gelijknamige vaultnotitie. Lees ook CLAUDE.md en het laatste sessielog in de vault.