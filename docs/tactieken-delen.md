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

## Deelcode, tegen de tactiek van een vriend, ladder zonder server (pc 2, 2026-10-19, branch pc2/vriendenmodus)

Geen nieuw uitwisselformaat en geen server: alles is de bestaande tactiek-JSON, compact verpakt, en een wedstrijd die beide vrienden op dezelfde seed deterministisch naspelen.

### Deelcode
- **Open > Delen > Kopieer deelcode** zet één regel op het klembord: `ZT1.<payload>.<controle>`. `ZT` = tactiek, `1` = versie, payload = Deflate van de tactiek-JSON in base64url (zonder `=`), controle = de eerste 8 hextekens van SHA-256 over de payload. Een voorbeeldtactiek wordt ca. 1,5-3 kB tekst; dat past in een chatbericht.
- Inlezen (`ShareCode.Decode`, in `sim/Zeus.Sim/ShareCode.cs`) weigert met een duidelijke reden: langer dan 65.536 tekens, geen drie delen, verkeerde soort (`ZT`/`ZL`), nieuwere versie, verkeerde controlesom, ongeldige base64url, uitgepakt groter dan 1 MiB (grens tijdens het uitpakken, niet erna), ongeldige UTF-8. Daarna gaat de JSON door de gewone `TacticEditorModel.Parse` en dezelfde naamlengtegrens als bestandsimport.
- **Plak tactiek van klembord** herkent een deelcode én gewone JSON (`TacticExchange.FromText`). Bestandsexport en -import blijven zoals ze waren.

### Speel tegen de tactiek van je vriend
- Menu > **Vrienden** (compacte knop onder de menu-items, naast Bibliotheek) > **Plak tactiek van vriend**: deelcode of JSON van het klembord gaat door de echte tactiekloader en wordt als eigen tactiek bewaard (`user://tactieken`, vrije naam met volgnummer). Daarna staat hij in het menu in de lijst van de uitploeg, met het gewone niveau (neutraal, amateur, halfprof, prof). Dat lijstje toonde eigen tactieken al; nieuw is alleen de plakknop.

### Ladder tussen vrienden
- **Speel ladderwedstrijd** speelt thuis tegen uit uit het menu op de menu-seed, gesimuleerd zonder live ingrijpen (geen wissels of bijsturen, geen tegenstanderwissels, geen vertrouwdheid), zodat een vriend precies dezelfde wedstrijd kan naspelen. De uitslag wordt een **uitslagcode** `ZL1.<payload>.<controle>` (zelfde verpakking als de deelcode) met: versie, build-hash van de simulator, seed, hash van beide tactieken (SHA-256 van de compacte JSON, 16 hextekens), beide tactieknamen, beide niveaus, uitslag en de event-hash van de wedstrijd. Klein genoeg voor een chatbericht.
- **Plak uitslagcode** van een vriend: de code wordt gecontroleerd (`Ladder.FromCode`), beide tactieken worden lokaal gezocht op hash (ingebouwd of eigen; ontbreekt er een, dan eerst de tactiek van de vriend plakken), en de wedstrijd wordt opnieuw gespeeld op seed en tactieken (`Ladder.Verify`). Alleen bij gelijke uitslag én gelijke event-hash telt hij mee. Afwijzingen: andere simulatorbuild (geen oordeel, wel afgewezen), andere tactieken, of een andere uitslag na naspelen (geknoeid).
- De ladder zelf is een tabel per tactiek (gespeeld, W-G-V, doelpunten, 3/1/0 punten) uit alle geaccepteerde uitslagen, bewaard als lijst van codes in `user://zeus_ladder.json` (via `SafeFile`, `ZEUS_NOSAVE=1` schrijft niets). Geen accounts, geen namen van personen, geen netwerk: wie welke tactiek speelt, spreken vrienden zelf af.
- Logica en tests: `sim/Zeus.Sim/ShareCode.cs`, `sim/Zeus.Sim/Ladder.cs`, `sim/Zeus.Sim.Tests/ShareCodeTests.cs`, `LadderTests.cs`. UI: `godot/Friends.cs` (overlay zoals Over), drie regels in `godot/Editor.cs`, `CopyShareCode` in `Editor.Sharing.cs`, één knop in `Menu.cs`, sleutels `friends.*`, `menu.friends`, `editor.copy_code`, `editor.code_copied` in beide talen.

### Uitdaging aan een vriend
- Na een uitdaging (gesimuleerd of live) staat onder de uitslagen **Kopieer uitdagingscode**: `ZC1.<payload>.<controle>` met build-hash, uitdagings-id, hash en naam van je tactiek, je niveau, de uitslagen van de N wedstrijden en je sterren (`sim/Zeus.Sim/ChallengeShare.cs`). Niet bij uitdagingen die zich aanpassen (de scout kiest een tegenstander die een vriend niet kan naspelen).
- Vrienden > **Plak uitdagingscode**: de tactiek wordt lokaal gezocht op hash (plak eerst de tactiek van je vriend), de uitdaging wordt met dezelfde tactiek, hetzelfde niveau en seeds 1..N nagespeeld (`ChallengeShare.Verify`); alleen bij gelijke uitslagen én sterren zegt het scherm "Klopt: je vriend haalde X sterren ... Nu jij!". Afwijzingen: andere build, onbekende/adaptieve uitdaging, andere tactiek, andere uitslagen. Niets wordt bewaard; het is een bewijs, geen ranglijst.
- Test: `ChallengeShareTests` (round-trip, naspelen klopt, geknoeide uitslag/build/tactiek/adaptief afgewezen).

Grenzen: determinisme geldt per build (de build-hash zit in de code); na een sim-wijziging zijn oude uitslagcodes niet meer na te spelen en worden ze afgewezen, de lokale ladder blijft staan. Een ladderwedstrijd is altijd een simulatie, niet een live gespeelde wedstrijd (die heeft ingrepen die een vriend niet kan naspelen).
