# Meter en uitdagingen op lichtere laptops

De tactiekmeter heeft een knop **Annuleren** tijdens het vergelijken. Na het stoppen worden de tactiekkeuzes en **Vergelijken** weer beschikbaar. Er verschijnt geen gedeeltelijk rapport.

Uitdagingen tonen dezelfde knop bij **Simuleer alles** en tijdens het voorbereiden van een live uitdaging. De voorbereiding toont een percentage; daarna toont de balk de voortgang van de echte wedstrijden. Tijdens berekenen blijven tactiek, niveau en uitdaging vaststaan. Annuleren bewaart geen resultaat, uitdagingvoortgang of speelgeschiedenis. Na het stoppen kun je opnieuw beginnen. Terug naar het menu of het scherm sluiten stopt het rekenwerk ook.

## Rekenbudget en stoppen

`SimulationBatch` laat de interactieve berekeningen uit Meter en Challenges één voor één lopen. Als je snel van scherm wisselt, wacht het nieuwe werk tot het oude werk is gestopt. Een geannuleerde wachtende taak wordt niet gestart. Tokens blijven geldig totdat de taak klaar is; ook fouten uit verlaten schermen worden afgehandeld.

Uitdagingen en het voorbereiden van tegenstanders gebruiken standaard maximaal twee gelijktijdige wedstrijden, en bij twee beschikbare logische processors maximaal één. De meter blijft één wedstrijd tegelijk verwerken. De bestaande expliciete `maxParallel`-optie voor scripts/tests blijft beschikbaar, begrensd door het aantal taken en beschikbare processors minus één (minimaal één). Dit is een conservatieve standaard, geen meting van de snelheid op een fysieke zwakke laptop.

Annulering wordt in de wedstrijdlussen iedere 64 simulatieticks gecontroleerd; de planner wacht daarvoor niet meer tot het einde van een hele wedstrijd. Dat is geen vaste garantie in milliseconden: de duur van een tick hangt van de machine af. Het budget verandert de seeds, resultaatvolgorde of wedstrijdregels niet.

## Overdracht aan Claude

Werkmap `C:\dev\zeus-batches`, branch `feat/demo-cancellable-batches`, gebaseerd op `6d8f024`. Nog niet samengevoegd of in een export opgenomen. Deze branch bevat geen wijzigingen uit `feat/demo-tactic-sharing`; de twee kunnen afzonderlijk worden geïntegreerd.

Gewijzigd: `godot/Meter.cs`, `godot/Challenges.cs`, `sim/Zeus.Sim/Challenge.cs`, `sim/Zeus.Sim/OpponentPlanner.cs`, drie nieuwe `batch.*`-sleutels per taal, nieuwe `SimulationBatch.cs` en regressietests. Bij samenvoegen de tips/onboarding in Challenges en de bestaande taalsleutels behouden. Wedstrijdfysica en kalibratie zijn niet aangepast.

Nog handmatig in de uiteindelijke export controleren: knopplaatsing op een klein scherm, annuleren na een aantal afgeronde wedstrijden en herstarten met andere tactieken. Niet getest op een andere pc. De Godot-controle hieronder bedient echte signalen programmatisch en is geen muis-/layouttest.
