# Zeus hervatten

Stand 2026-10-13: fase 1a taken 1–14. Codex heeft het ongecommitte werk na Claudes sessielimiet gecontroleerd en afgemaakt, en daarna waarneming en een eerste beslisbrein toegevoegd. Ontwerp en afspraken staan in de Obsidian-vault Zeus online.

## Controleren
Vanuit de repo:

```powershell
dotnet test Zeus.sln
dotnet build godot/Zeus.Godot.csproj
.\.venv\Scripts\python.exe scripts\validate_tactics.py
dotnet run --project sim/Zeus.Runner -- --home tactics/laag_blok_442.json --away tactics/man_om_man_433.json --seed 42 --steps 54000 --out data/wedstrijd.jsonl --every 10
```

61 tests groen; drie tactieken valideren. 90 minuten zonder regelmatige snapshot-output duren circa 1,5 s op deze laptop (voor taak 14 circa 0,3 s). Dit is nog geen prestatiemeting van een volledige wedstrijdsimulatie: passkeuze en duels ontbreken.

## Bekijken
Open `godot/project.godot` met Godot 4.7.2 .NET en druk F6/F5 om de scène te draaien. Spatie pauzeert, 1 en 4 kiezen de snelheid. Rood is thuis, blauw is uit; witte streepjes tonen de kijkrichting. De bal en het balbezit staan in beeld.

De weergave verwijst naar de echte simulatiekern met een ProjectReference. Een accumulator voert vaste stappen van 0,1 s uit; speler- en balposities worden geïnterpoleerd. De standaardtactieken komen uit `../tactics` ten opzichte van het Godot-project. `ZEUS_TACTICS_DIR`, `ZEUS_HOME` en `ZEUS_AWAY` kunnen dat vervangen. Dit is een ontwikkelproject; tactieken zijn nog niet verpakt voor een export.

Voor de uitgevoerde rendercontrole is de compatibility-renderer gebruikt; de twee beelden en logs staan lokaal in `data/verification/` (genegeerd door git). Opstarten, renderen en lopen zijn gecontroleerd; de toetsen zijn nog niet handmatig in een interactieve sessie getest.

## Kern en beperkingen
- `Ball.cs`: rollen met rolweerstand en kwadratische luchtweerstand; vrije vlucht met zwaartekracht, luchtweerstand en stuit. Vijf substappen van 0,02 s per standaardtick. Impacttijd wordt geïnterpoleerd. De restitutie en rolweerstand blijven aannames uit de vault.
- `Match.cs`: aftrapnemer op de middenstip; de bal volgt de voeten van de baldrager. Controle wordt vóór bewegen getoetst, zodat sprinten geen kunstmatig balverlies veroorzaakt. `SetBall` geeft de bal vrij. Een lage, langzame bal binnen bereik kan worden opgepakt; een bezitwissel levert een event op.
- Fases: alleen opbouw voor de ploeg met bezit en verdedigen voor de andere ploeg. Bij een vrije bal blijft het laatste team als bezittende ploeg gelden. Volledige fases, faseovergangen en blokwissels volgen later.
- `volg_bal`: doelpositie verschuift met de waargenomen of kort onthouden balpositie; zonder bekende positie gebruikt de speler het basissjabloon. Spiegelen voor uit is getest. Versterking 0,5 is een ongekalibreerde instelling.
- `Perception.cs` en `Brain.cs`: beperkte waarneming, balgeheugen en vervangbare `IPlayerBrain.Decide(Perception)`. De eerste regel houdt de tactiekpositie aan. Zie `docs/waarneming.md` voor instellingen en uitvoervolgorde.
- Geen passkeuze, onderscheppingsmodel, schoten, goals, overtredingen, kaarten of uit-regels. Zonder beslissingen lopen spelers naar hun doelen en blijven daar staan.

## Hersteld en getest
De uitroltest rekent nu met de analytische stoptijd inclusief luchtweerstand. De stuitende schottest kijkt naar de omkering van de verticale snelheid: een grondcontact kan tussen snapshots vallen. Nieuwe tests bewaken balbezit tijdens sprinten, bezitwissel met beide fases, vrijgeven van een snelle bal, geen controle van een hoge bal en balvolgen voor beide teams.

De golden hashes zijn bewust gewijzigd vanwege de bal in snapshots, aangepaste faseposities en de aftrapnemer op de middenstip. Twee onafhankelijke 90-minuten-runs met seed 42 geven eventhash `a1c48130396ac98af05c0b07ea1f34ab4df51078f265c51d8492a69acd79e9d5`; seed 43 geeft `29a8ce8eda47e499a7e3fe52f6e9678939f62cc036fc3942023d00959d2e4b58`. Een eindsnapshot kan bij verschillende seeds gelijk zijn omdat iedereen uiteindelijk dezelfde doelen bereikt.

## Volgende taak
Taak 15: passkeuze via zichtbare medespelers in de waarneming. Taak 16: uitvoeringsfout en fysische uitvoering. Verwerk de architect-review uit Simulatieontwerp v0; uitvoeringsfout en fysische uitkomst moeten afzonderlijk blijven, zodat passsucces niet dubbel wordt bepaald. Taak 14 veranderde de golden hashes van de basistactieken niet: deze regel houdt dezelfde formatie aan.
