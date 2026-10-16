# Tactieken delen als bestand

In de tactiekmaker staan onder **Open → Delen** naast kopiëren/plakken twee keuzes:

- **Bestand exporteren…** schrijft de huidige tactiek als `.json` naar een gekozen plek. Stuur dat bestand via je eigen chat of mail naar een vriend. Dit werkt ook met nog niet opgeslagen bewerkingen; exporteren verandert de opgeslagen editorstatus niet.
- **Bestand importeren…** opent het bestand van een vriend. De echte tactiekloader controleert het voordat het de huidige tactiek vervangt. Bij onopgeslagen bewerkingen vraagt de editor eerst bevestiging. Klik daarna op **Opslaan** om een eigen kopie in je tactieken te bewaren.

Bij een bestaande naam stelt import een vrije naam met een volgnummer voor. De oorspronkelijke tactiek van de vriend en de eigen opgeslagen tactieken blijven behouden. Export naar een bestaande bestandsnaam gebruikt de overschrijfbevestiging van de bestandskiezer.

Er is geen nieuw uitwisselformaat: dezelfde JSON bevat alle fases, regels, instructies, getekende paden en onbekende uitbreidingsvelden. Import/export accepteert maximaal 1 MiB; dat is ruim boven de meegeleverde tactieken. Bestandstypen anders dan `.json` worden geweigerd. Zowel meldingen als menu-items zijn Engels/Nederlands.

## Overdracht aan de demosessie

Werkmap `C:\dev\zeus-tactic-sharing`, branch `feat/demo-tactic-sharing`, gestart vanaf `6d8f024`. Dit werk is apart gehouden van de actieve onboarding-, tips-, feedback- en AI-werkstromen. Niet op main samengevoegd.

Bestanden: nieuwe `godot/Editor.Sharing.cs`, `sim/Zeus.Sim/TacticExchange.cs` en `sim/Zeus.Sim.Tests/TacticExchangeTests.cs`; in `Editor.cs` alleen vier regels voor menu-acties en menu-items; zeven nieuwe `editor.*`-sleutels per taal. Bij samenvoegen bestaande onboarding-/tipwijzigingen in Editor en de taaltabellen behouden.

Integreren: lees eerst `git log feat/demo-tactic-sharing` en `git diff main...feat/demo-tactic-sharing`. Voeg deze branch pas samen nadat de andere editor-/taalwijzigingen zijn vastgelegd. Er is geen nieuwe exportzip gemaakt; bouw die na integratie van alle demotaken.

Nog handmatig controleren in de uiteindelijke demo: bestandskiezer openen, exporteren, via een andere gebruikersmap importeren, weigeren van ongeldige JSON, annuleren bij onopgeslagen wijzigingen en opnieuw opstarten na Opslaan. Headless laden controleert deze muisbediening niet.
