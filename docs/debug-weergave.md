# Debugweergave

De Godot-weergave heeft een rechterpaneel met wedstrijdstatus, passes, kaarten en de laatste zes events. Uitgesloten spelers verdwijnen van het veld; een gele kaart staat boven de speler. Het paneel toont maximaal twee uitgesloten spelers bij naam en het totale aantal.

Druk **D** voor loopdoelen (witte kruisen met verbindingslijnen), passintenties (gele lijnen met rondjes), spelers-id's en de waarnemingsomvang/intentiesoort van de huidige balhouder. Dit leest uitsluitend bestaande snapshots en inspectiemethoden van de simulatie; er staan geen voetbalbeslissingen in Godot. Doelen zijn de huidige simulatietoestand, terwijl beweging geïnterpoleerd wordt.

Spatie pauzeert; **1** en **4** kiezen snelheid. `ZEUS_DEBUG=1` zet debug aan bij starten. Het venster is 1520 × 780 pixels zodat het paneel buiten het veld blijft. Eventtekst wordt ingekort om binnen het paneel te passen; de volledige eventinformatie blijft in de simulatie/export aanwezig. De passenteller verwerkt elk toegevoegd event één keer.

`ZEUS_REPLAY` kan naar een replaybestand wijzen. De weergave leest de opgenomen tactieken, configuratie en seed, speelt de exacte stappen zichtbaar af, controleert de eventhash en pauzeert aan het einde. Een replay vereist dezelfde simulatiebuild. Een voltooide replay blijft gepauzeerd wanneer je spatie indrukt.

Gecontroleerd op 2026-10-13: Godot-build zonder waarschuwingen, normale headless smoke en debugrenders. Een runner-replay van 20 stappen is in Godot op precies tick 20 gestopt met dezelfde eventhash (`b0b20881…`). Opstartfouten stoppen met exitcode 1 en één foutmelding; een mislukte eindcontrole stopt eveneens met exitcode 1.
