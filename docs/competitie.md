# Competitie en dunne carrière (v0, VOORLOPIG)

PRD fase 10 ("dunne carrière: van amateurclub naar de top, selectie en wedstrijdschema"). **Alles hier is een werkhypothese**: clubs, niveaus, aantal teams, promotie- en degradatieregels en seeds kunnen veranderen. De clubnamen zijn verzonnen; er zijn bewust geen echte clubs of namen gebruikt.

## Simulatiekern (`sim/Zeus.Sim/Competition.cs`)
- **Divisies:** Amateurklasse (niveau `amateur`), Halfprofklasse (`halfprof`), Profklasse (`prof`). Elke divisie heeft 6 clubs: jouw club (**FC Zeus**, index 0) en 5 vaste verzonnen tegenstanders met elk een vaste tactiek uit `tactics/`. In een divisie spelen alle teams op het niveau van die divisie (`SquadFactory`), de speler dus ook.
- **Schema:** dubbele ronde, 10 speelrondes van 3 wedstrijden, circle-method; tweede helft = dezelfde paren met thuis en uit omgedraaid. Elk team speelt 5 keer thuis en 5 keer uit.
- **Stand:** 3/1/0 punten, dan doelsaldo, dan doelpunten voor, dan clubnaam (ordinaal) als laatste tiebreak.
- **Seeds:** `Competition.Seed(seizoen, ronde, thuis, uit)` (splitmix-achtige menging, nooit 0, binnen 47 bit). Dezelfde staat geeft dus altijd dezelfde simulatie, ook voor de live wedstrijd (dezelfde seed, tactieken en niveaus als de simulatie).
- **Wedstrijden:** via `Match` en `MatchStatistics` (alleen de doelpunten worden bewaard). `PlayRound` voltooit steeds een hele ronde; de speler kan zijn eigen uitslag van een live wedstrijd meegeven, de andere wedstrijden worden gesimuleerd. **Maximaal 3 wedstrijden tegelijk** (`Competition.MaxParallel`); resultaten komen in schemavolgorde, dus onafhankelijk van threadtiming. `PlayRest` simuleert de rest van het seizoen ronde voor ronde en is annuleerbaar.
- **Promotie/degradatie (v0):** top 2 promoveren naar het volgende niveau, de laatste degradeert naar het vorige niveau; niet in de bovenste divisie (promotie) of de onderste (degradatie). Bij een nieuw seizoen blijft de gekozen tactiek; de tegenstanders wisselen met de divisie, in dezelfde divisie blijven ze gelijk (met andere seeds).
- **Seizoenstoestand (`SeasonState`):** versie, seizoen, divisie, afgeronde rondes, gekozen tactiek van de speler, alle uitslagen en de seizoenshistorie, als JSON (`ToJson`/`Parse`). `Parse` valideert en gooit `FormatException` bij alles wat niet klopt (aantal uitslagen moet bij de ronde passen).

## Godot (`godot/Season.cs`, `Season.tscn`)
- Menu-item **Competitie** (boven Uitdagingen). Het scherm toont de stand (▲ promotie, ▼ degradatie, eigen club goud), het speelschema met uitslagen (scrolt naar de volgende ronde), en rechtsboven de volgende wedstrijd met de knoppen **Speel volgende wedstrijd (live)**, **Simuleer volgende wedstrijd**, **Simuleer de rest van het seizoen** (achtergrondtaak met voortgangsbalk, zoals bij Uitdagingen; Esc annuleert), een tactiekkeuze (alle bestanden uit `tactics/`) en **Tactiek aanpassen** (opent de editor en komt daarna terug naar de competitie, via `GameSession.ReturnScene`). Aan het einde van het seizoen staat er een samenvatting met promotie/degradatie en de knop **Start seizoen N+1**.
- **Live wedstrijd:** Season zet `GameSession` (thuis/uit, seed, niveaus, `InCompetition`) en laadt `Main`. Main (minimaal gewijzigd) schrijft bij de eindstand `GameSession.LastResult` en toont op het eindscherm één knop "[Enter] terug naar de competitie" (heatmaps en pauzemenu blijven zoals ze waren); Esc gaat in een competitiewedstrijd naar Season. Season verwerkt de uitslag één keer, alleen als `LastResultRound` nog de ronde is die aan de beurt is, en simuleert dan de rest van die ronde.
- **Opslag:** `user://zeus_carriere.json` na elke wijziging. Een beschadigd of onleesbaar bestand wordt genegeerd (melding op het scherm, nieuw seizoen); het scherm breekt nooit.
- **Visuele controle:** `ZEUS_SHOT=<prefix>` geeft `<prefix>_season.png`; `ZEUS_CAREER_FILE=<pad>` gebruikt een ander opslagbestand (om de echte carrière niet aan te raken); `ZEUS_ROUNDS=n` speelt n rondes vooraf; `ZEUS_LIVE=1` met `ZEUS_FINISH=1` en `ZEUS_AUTOBACK=1` test de hele lus (Season -> live wedstrijd -> uitslag terug -> Season; `<prefix>_matchend.png` is het eindscherm).

## Gecontroleerd
`CompetitionTests` (schema, seeds, stand en tiebreaks, promotie/degradatie aan de randen, JSON-rondreis en beschadigde bestanden, reproduceerbare ronde, een heel seizoen met korte wedstrijden, annuleren), rooktest met scherm Season, en screenshots van beginstand, tussenstand, eindscherm van het seizoen, het live-eindscherm en een beschadigd bestand. De live-uitslag van ronde 1 (3-3) kwam overeen met de gesimuleerde uitslag van dezelfde ronde (zelfde seed).

## Bekend en open
- Niveaus op `amateur` geven grote uitslagen (bijv. 13-2); dat komt uit de nog niet gekalibreerde niveauschaal (`docs/spelers.md`), niet uit de competitiecode. De stand is dus voorlopig onrealistisch ruw.
- Geen selectiebeheer, transfers, financiën, blessures of geschorsten; alleen de tactiek en het niveau van de divisie tellen. Geen bekers, geen meerdere profielen/opslagplaatsen.
- Een live wedstrijd met wissels of tactiekwijzigingen in het pauzemenu geeft een andere uitslag dan de simulatie van dezelfde ronde; dat is bedoeld.
- Een afgebroken live wedstrijd (Esc) levert geen uitslag; je speelt hem opnieuw of simuleert hem.
