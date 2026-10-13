# Godot debug-weergave (T6)

Open `godot/project.godot` met Godot 4.7.2 .NET. Rood is thuis, blauw is uit.
De standaardweergave toont het veld, de bal, score, helft, wedstrijdstatus en kaarten.
Debug-lagen staan standaard uit. De weergave verwerkt snapshots en events; zij wijzigt
geen wedstrijdtoestand en leest geen intenties, doelen of waarnemingen uit `Match`.

## Bediening

| Toets | Functie |
| --- | --- |
| Spatie | Pauzeren of verder spelen |
| 1 / 4 | Snelheid 1× / 4× |
| D | Alle debug-lagen gezamenlijk aan of uit |
| F | Fases per ploeg in het paneel en naast iedere actieve speler |
| T | Afgaande triggers bij de speler en kort in het paneel |
| P | Laatste pass: gele pijl en cirkel bij de beoogde ontvanger |
| O | Buitenspellijnen per aanvallende ploeg en ringen om spelers voorbij de lijn |
| E | De laatste events in het rechterpaneel |

De lagen kunnen onafhankelijk worden geschakeld, ook tijdens pauze of replay.
Onderaan staat per laag of deze aan is. `ZEUS_DEBUG=1` zet alle lagen aan bij het starten.
De D-toets wisselt de gezamenlijke stand; de individuele toetsen veranderen die stand niet.
Spelers-id's verschijnen zodra een veldlaag aan staat.

## V0-aannames en grenzen

- Fases volgen uitsluitend `PhaseChange`, met het bestaande eventformaat
  `Van->Naar: reden`. Opbouw, aanval, balverlies, verdedigen, balwinst en stilstaand
  worden als Nederlands label getoond. Het event heeft een ploeg, geen individuele
  spelerfase: iedere speler krijgt de fase van zijn ploeg. Tot het eerste fase-event
  van die ploeg staat er **onbekend**, ook als teamfases zijn uitgeschakeld.
- `DebugViewConfig.TriggerSeconds` is standaard **4 seconden simulatietijd**.
  `TriggerFired` toont de eventnaam/voorwaarde en speler, met een oranje ring.
  Pauzeren bevriest de melding; bij 4× verdwijnt deze viermaal zo snel in echte tijd.
  Alle nog actieve meldingen worden bij hun speler getoond; het paneel toont de laatste twee.
- `DebugViewConfig.RecentEventCount` is standaard **6**, geldig van 1 tot en met 6
  vanwege de vaste paneelhoogte. De eventhistorie wordt ook bijgehouden als E uit staat.
  Beide instellingen hebben een `Validate()`-controle; er zijn geen nieuwe simulatieparameters.
  Lange teksten worden afgebroken of ingekort; de volledige events blijven in de export.
- De laatste pass komt uit het laatste `Pass`-event, inclusief de beginplaats en
  `to <id>` als beoogde ontvanger. De ontvangerpositie wordt vastgezet uit de snapshot
  direct na die simulatiestap. De gele pijl geeft deze verbinding weer. **Het exacte
  mikpunt, vooruitlopen en de uitvoeringsfout ontbreken in het event**, dus dit is geen
  exacte balbaan of passintentie. De lijn blijft tot de volgende pass staan.
  Bij een wegwerptrap of hervatting zonder ontvanger verdwijnt de lijn en toont het
  paneel de eventomschrijving; er wordt geen ontvanger geraden.
- O toont de **actuele positiegrens**, per aanvallende ploeg: tweede laatste actieve
  tegenstander, bal en middenlijn, in de aanvalsrichting. De lijn kleurt zalm voor thuis
  en lichtblauw voor uit. Ringen komen uit de bestaande pure `OffsideRules.PositionsAtTouch`
  in de simulatiekern, toegepast op de huidige snapshot. Kantwissels en uitgesloten
  spelers worden meegenomen. Dit zijn mogelijke buitenspelposities, geen vastgestelde
  overtredingen en geen bewaarde beoordeling op het trapmoment. Directe ontvangst uit
  bepaalde hervattingen kan vrijgesteld zijn; alleen het echte `Offside`-event betekent
  dat de simulatie een overtreding heeft vastgesteld. Zonder bal of twee actieve
  tegenstanders wordt geen lijn getoond.
- Beweging is geïnterpoleerd; debug-lijnen gebruiken de discrete snapshots. Daardoor
  kunnen ringen tijdens beweging iets van de spelers afwijken.

## Replay en controle

Zet `ZEUS_REPLAY` op het absolute pad van een runner-replaybestand. Godot leest de
opgenomen tactieken, configuratie en seed, stopt op de exacte laatste tick, controleert
de eventhash en blijft gepauzeerd. De simulatiebuild moet overeenkomen met de opname.
Debug-schakelaars veranderen geen simulatie-invoer, RNG of hashcontrole.

```powershell
dotnet build godot/Zeus.Godot.csproj
dotnet test godot/tests/Zeus.Godot.Debug.Tests.csproj
dotnet test Zeus.sln
```

De aanvullende xUnit-tests staan binnen `godot/tests/` om binnen de T6-scope te blijven;
ze staan niet in `Zeus.sln`. Ze toetsen fase-events, meldingsduur, begrensde historie,
passontvangers, buitenspelposities bij kantwissel en gelijke wedstrijdhashes met en zonder
weergavestatus. De bestaande verwijzing naar loopdoelen/passintenties in
`docs/debug-weergave.md` beschrijft de eerdere weergave; voor T6 geldt dit document.

Gecontroleerd in deze omgeving: Godot-build zonder fouten, de volledige simulatiesuite
(inclusief ongewijzigde golden-hashes), de aanvullende debugtests,
een headless start, twee debugrenders en een replay van 20 stappen met bevestigde
eventhash. De renders tonen fases, passpijl en beide lijnen. Toetsbediening en een
live afgaande trigger zijn niet interactief uitgeprobeerd. NuGet kon de online
kwetsbaarheidsgegevens niet ophalen (`NU1900`); Godot meldde dat de systeemcertificaten
niet gelezen konden worden, maar startte en speelde de replay wel af.
