# Looppaden per speler en fase

Een speler kan per volledig uitgeschreven fase (ook een verdedigingsvariant) een `pad` krijgen. Punten gebruiken dezelfde teamrelatieve coördinaten als `pos`: x van eigen naar vijandelijke doellijn, y van links naar rechts. Uitspelers en de tweede helft worden automatisch gespiegeld.

```json
{
  "id": "LB",
  "pos": [0.28, 0.15],
  "pad": [[0.32, 0.15], [0.36, 0.25]],
  "pad_snelheid": "joggen",
  "pad_wacht": 0.5
}
```

`pad` bevat 1–8 punten van precies twee eindige getallen binnen 0..1. Een leeg pad is ongeldig; verwijder het veld om geen pad te gebruiken. `pad_snelheid` is `wandelen` (maximaal 2 m/s), `joggen` (standaard, maximaal 4 m/s) of `sprint` (de actuele maximumsnelheid van het spelersprofiel). Vermoeidheid kan die grenzen verder verlagen. `pad_wacht` is 0–5 seconden per punt, standaard 0; wachttijden worden naar boven afgerond op gehele simulatieticks.

Bij de eerste simulatietick van een fase loopt de speler eenmaal langs de punten en daarna naar de onverschoven `pos` van die fase. Aankomst vereist een afstand en snelheid van maximaal 0,02 m respectievelijk m/s. Na de laatste terugkeer neemt het gewone brein het over, inclusief volg-bal en instructies. Tijdens het pad heeft de route voorrang op alle beslissingen van het brein: ook pressing, dieptelopen en passen. Balcontrole, duels, buitenspel, hervattingen, vermoeidheid en veldbegrenzing blijven via de bestaande wedstrijdlogica lopen. Een nieuwe fase, kantwissel of rode kaart breekt de route af. Tijdens een hervattingspauze beweegt niemand langs een pad; hervatting start de route van de nieuwe fase.

Een blokwissel binnen dezelfde fase herstart de route niet; de route en terugkeerpositie blijven die van het fasemoment. De volgende fase gebruikt de dan geselecteerde variant. Een verwijzingsfase (`gelijk_aan`) erft ook het pad, maar heeft een eigen startmoment.

Een bestaande triggervoorwaarde kan het pad van de speler in de huidige fase opnieuw starten, eventueel samen met een instructie:

```json
{"speler": "LB", "pad": true}
```

```json
{"speler": "LB", "pad": true, "instructie": "start_pressing"}
```

De zes bestaande voorwaarden en hun fase-/zonebeperkingen blijven gelden. Een padactie zonder pad in de actuele fase doet niets. Een herstart begint opnieuw bij punt 1. Er is geen nieuwe triggeractie om een willekeurige fase te kiezen: de bestaande teamfaselogica start de route bij een fasewissel.

`MatchConfig.EnablePaths` staat standaard aan; `false` schakelt fase- en triggerstarts uit. Zonder paden blijft de wedstrijd bit-identiek, inclusief RNG en bestaande golden-hashes. `PathStart` meldt `phase start` of `trigger`; `PathEnd` meldt `completed`, `phase changed`, `restarted` of `sent off`. Deze events zijn alleen debug-informatie, geen nieuwe voetbalregels.

## Model-API voor de tekeneditor

`TacticEditorModel.Paths.cs` voegt toe:

- `GetPath(phase, playerId)`: een losse lijst relatieve punten; leeg als er geen pad is.
- `SetPath(phase, playerId, points, speed: null, wait: null)`: vervangt de punten; optionele instellingen blijven behouden wanneer ze niet worden meegegeven.
- `ClearPath(phase, playerId)`: verwijdert pad, snelheid en wachttijd.

Deze mutaties gebruiken de bestaande transactie en echte loader: ongeldige invoer wordt geweigerd en de volledige JSON wordt teruggezet. Bij bewerken worden aliases onafhankelijk gemaakt, zoals bij andere spelermutaties. Godot-bediening met de muis valt buiten deze wijziging. Kopiëren neemt de volledige fase inclusief paden over; de bestaande spiegeltool spiegelt alleen `pos`, dus de tekeneditor moet desgewenst ook de punten spiegelen met `SetPath`.

## Controle

`PathTests` controleert puntvolgorde, wachten, terugkeer, eenmalige uitvoering, faseonderbreking, snelheden, begrenzing, spiegeling, uitschakelen, volg-bal/instructievoorrang, determinisme, triggerstarts en validatie/rollback. Draai de volledige suite vanuit deze worktree met een eigen uitvoermap:

```powershell
dotnet test Zeus.sln -m:1 --nologo -o C:\dev\zeus-tekenen\.tmp-pad-tests
C:\dev\zeus\.venv\Scripts\python.exe scripts/validate_tactics.py
```
