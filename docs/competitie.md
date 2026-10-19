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
- **Seizoensdoel (v0):** bij de start van elk seizoen bepaalt de club een doel uit de sterkte van de eigen selectie t.o.v. de andere vijf clubs (`Competition.ExpectedGoal`: gemiddelde veldattributen van de basiself bij de gekozen tactiek, een speler buiten zijn eigen rol telt voor 90%). Bovenste derde = promotie (top 2), midden = bovenste helft (top 3), onderste derde = handhaving (niet laatste); gelijke clubs delen hun plek. Het doel staat als optioneel veld `Goal` in `zeus_carriere.json` (niet geschreven als het leeg is, oude bestanden blijven gelijk); het eindscherm meldt doel gehaald / niet gehaald. Een nieuwe carrière begint met een introkaart (club + doel).
- **Eigen club:** naam (max. 24 tekens, geen controletekens, niet leeg), twee tenuekleuren uit 10 en een embleem uit 8 vormen, in `instellingen.json` (`club_name`, `club_kit1`, `club_kit2`, `club_crest`; met `ZEUS_NOSAVE` alleen in het geheugen). Te kiezen op de introkaart en in Instellingen > Club. Alleen weergave (tabel, schema, intro, scorebord en eindkaart van competitiewedstrijden): intern blijft de club `FC Zeus` (vertrouwdheid, volgorde bij gelijke stand). Op het veld houden de spelers rood/blauw.
- **Visuele controle:** `ZEUS_SHOT=<prefix>` geeft `<prefix>_season.png`; `ZEUS_CAREER_FILE=<pad>` gebruikt een ander opslagbestand (om de echte carrière niet aan te raken); `ZEUS_ROUNDS=n` speelt n rondes vooraf; `ZEUS_LIVE=1` met `ZEUS_FINISH=1` en `ZEUS_AUTOBACK=1` test de hele lus (Season -> live wedstrijd -> uitslag terug -> Season; `<prefix>_matchend.png` is het eindscherm).

## Gecontroleerd
`CompetitionTests` (schema, seeds, stand en tiebreaks, promotie/degradatie aan de randen, JSON-rondreis en beschadigde bestanden, reproduceerbare ronde, een heel seizoen met korte wedstrijden, annuleren), rooktest met scherm Season, en screenshots van beginstand, tussenstand, eindscherm van het seizoen, het live-eindscherm en een beschadigd bestand. De live-uitslag van ronde 1 (3-3) kwam overeen met de gesimuleerde uitslag van dezelfde ronde (zelfde seed).

## Bekend en open
- Niveaus op `amateur` geven grote uitslagen (bijv. 13-2); dat komt uit de nog niet gekalibreerde niveauschaal (`docs/spelers.md`), niet uit de competitiecode. De stand is dus voorlopig onrealistisch ruw.
- Geen selectiebeheer, transfers, financiën, blessures of geschorsten; alleen de tactiek en het niveau van de divisie tellen. Geen bekers, geen meerdere profielen/opslagplaatsen.
- Een live wedstrijd met wissels of tactiekwijzigingen in het pauzemenu geeft een andere uitslag dan de simulatie van dezelfde ronde; dat is bedoeld.
- Een afgebroken live wedstrijd (Esc) levert geen uitslag; je speelt hem opnieuw of simuleert hem.


## Vertrouwdheid in de competitie

De minimale koppeling is gebouwd: FC Zeus speelt live en gesimuleerd met een eigen staat in de carrière-JSON. Formaatversie 2 voegt Familiarity, TrainedThisRound en PendingDemand toe. Versie 1 en ontbrekende nieuwe velden blijven leesbaar; de UI initialiseert ontbrekende vertrouwdheid op nul. Ongeldige waarden/team/leerperiode worden als beschadigd bestand behandeld. Een losse sim-aanroep zonder staat houdt het oude wedstrijdgedrag.

Boven stand en schema staat een compact tweetalig paneel met alle zeven concepten, eis inclusief complexiteit, huidige basisgemiddelden en voortgangsbalken. Train voert rechtstreeks een collectieve training uit op de competitiestaat: kies concept en intensiteit, maximaal eenmaal per ronde. Het gebruikte trainingsmoment wordt opgeslagen. De volgende speelronde opent een nieuw leerbudget. AfterMatch wordt eenmaal uitgevoerd wanneer de volledige ronde succesvol wordt verwerkt; PendingDemand bewaart bij live de tactiekeis aan het eindsignaal, ook over herstart. Bij het nieuwe seizoen blijven waarden behouden. AI is vrijgesteld van vertrouwdheidsstraffen (vaste referentie 0,7 per niveau).

Voorlopige grenzen: alle opgeslagen selectie-ids leren mee, inclusief bank; ids zijn nog slots en tussentijdse tactieken worden niet naar minuten gewogen. Training veroorzaakt geen vermoeidheid. De vierseedproef meet uitvoeringstekorten, geen betrouwbare relatie met doelpunten of winst. Zie ook vertrouwdheid.md, dezelfde sectie.

## Blessures en schorsingen (carrière v1, pc 2, 2026-10-19, branch pc2/carriere)

Alleen voor de eigen club; de AI-clubs spelen altijd compleet.
- **Schorsing:** een rode kaart (direct of tweede geel) van een eigen speler kost de volgende speelronde. Uit een gesimuleerde ronde komt dat uit de wedstrijdgebeurtenissen; uit een live wedstrijd geeft Main de namen door (`GameSession.LastRedCards`), Season bewaart ze in `Pending.RedCards` zodat Esc of een herstart ze niet verliest.
- **Blessure:** na elke ronde rolt elke basisspeler die niet afwezig was met kans 3% (`Absences.InjuryChance`) op een blessure van 1-3 rondes. De worp is geseed op seizoen en ronde (`Competition.Seed(seizoen, ronde, 6, 6)`), dus dezelfde staat geeft dezelfde lijst.
- **Vervanging:** `Absences.WithoutAbsent` vult elke afwezige basisplaats vanaf de bank: keeper voor keeper, anders dezelfde rol, dan dezelfde rolgroep, dan een willekeurige veldspeler. Slots en tactiek blijven gelijk; de afwezige gaat naar de bank (en kan dus niet gewisseld worden). Is de bank op, dan speelt hij toch. Simulatie (`Competition.PlayRound` via `PlayerSquad`) en live (`MatchDriver`, `GameSession.CompetitionAbsent`) gebruiken dezelfde functie.
- **Opslag:** `SeasonState.Absences` (lijst van `{Player, Reason "rood"|"blessure", RoundsLeft 1..10}`), niet geschreven als null; oude bestanden blijven byte-gelijk en leesbaar. `Parse` weigert ongeldige redenen of aantallen.
- **Scherm:** boven de tactiekkeuze staat "Niet beschikbaar: naam (geschorst, nog 1), naam (geblesseerd, nog 2)". Namen zijn de fictieve namen uit `squads/<niveau>.json` (slot-ids veranderen bij het passen, de naam niet).
- Tests: `AbsenceTests`. Niet gebouwd: spelersontwikkeling, "nieuwe speler kiezen".

## Beker (carrière v1, pc 2, 2026-10-19, branch pc2/carriere)

`sim/Zeus.Sim/Cup.cs`, gesimuleerd (geen live bekerwedstrijd in v1).
- **Deelnemers:** de 6 clubs van je divisie plus 2 gasten: de eerste twee tegenstanders van de divisie erboven (in de Profklasse: van de Halfprofklasse). Iedereen speelt op het niveau van jouw divisie. Loting = geseede schudding per seizoen (`Competition.Seed(seizoen, 20, 0, 0)`); paren (1,2), (3,4), (5,6), (7,8), de eerste thuis.
- **Rondes:** kwartfinale na speelronde 3, halve finale na 6, finale na 8 (`Cup.DueAfterLeagueRound`). Is een bekerronde aan de beurt, dan staat die bovenaan in het actieblok ("Eerst de beker: ..."), is **Speel volgende wedstrijd (live)** uitgeschakeld en heet de simuleerknop "Simuleer bekerronde". "Simuleer de rest van het seizoen" speelt bekerrondes mee zodra ze aan de beurt zijn. Ben je uitgeschakeld, dan wordt de ronde zonder jou gespeeld.
- **Eén wedstrijd per ronde**, seed `Competition.Seed(seizoen, 21 + ronde, i, j)` met i, j de lotingsposities; eigen selectie via `Competition.PlayerSquad` (afwezigen tellen dus ook in de beker). **Gelijkspel:** strafschoppenserie (`sim/Zeus.Sim/Shootout.cs`): vijf per ploeg, daarna om en om tot er een verschil is; de kans per strafschop is 0,76 plus 0,20 maal (afwerking+beslissingen van de nemer minus 0,5) minus 0,16 maal (reflexen+positiespel van de keeper minus 0,5), begrensd op 0,40..0,95, nemers op volgorde van afwerking, alles op de wedstrijdseed. Niet op het veld gespeeld. Een rode kaart van een eigen speler in de beker kost de volgende competitieronde (`Absences.Suspend`, zonder blessureworp).
- **Opslag:** `SeasonState.Cup` (lijst van `{Round 0..2, Home, Away, HomeGoals, AwayGoals, Winner}`, niet geschreven als null); `SeasonRecord.CupWins` (0..3, 3 = bekerwinst; 0 in oude bestanden). `Parse` controleert aantallen per ronde, clubnamen en dat de winnaar een van beide is.
- **Scherm:** onder het speelschema staat BEKER per ronde (paren met "–" zolang de ronde nog niet gespeeld is); de seizoensafsluiting meldt de bekerwinnaar; de geschiedenis toont "beker: halve finale/finale/gewonnen".
- Tests: `CupTests` (loting, rondes aan de beurt, reproduceerbaar en opgeslagen, volgend seizoen). Autotest `scripts/autotest/11b_career_cup.json`.

## Versterking aan het eind van een seizoen (carrière v1, pc 2, 2026-10-19, branch pc2/carriere)

`sim/Zeus.Sim/Signings.cs`: geen transfermarkt en geen geld, alleen de "eenvoudige nieuwe speler kiezen" uit het meerjarenplan.
- **Aanbod:** op het eindscherm van het seizoen (vóór "Start seizoen N+1") biedt de club drie veldspelers aan in drie verschillende rollen uit het sjabloon van de divisie waarin je volgend seizoen speelt. Elk attribuut ligt 0,06-0,10 boven het gemiddelde van die rol in het sjabloon (gecapt op 1), de namen zijn fictief. Alles geseed op seizoen en divisie (`Competition.Seed(seizoen, 30, divisie, 0)`). Je kiest er één of "Geen versterking"; daarna pas kan het volgende seizoen starten. Het aanbod komt één keer per seizoen (`LastSigningSeason`).
- **Toepassing:** `Signings.Apply` zet elke versterking in de basis op de plek van de zwakste basisspeler met dezelfde rol (gemiddelde van de 15 attributen); die speler gaat naar de bank in plaats van de zwakste bankveldspeler, dus het blijft 11 + 7. Heeft je tactiek die rol niet, dan zit hij op de bank. Volgorde in `Competition.PlayerSquad`: sjabloon passen, versterkingen, dan afwezigen. Live gebruikt dezelfde selectie (`GameSession.CompetitionSquad`, gezet door Season bij "Speel volgende wedstrijd").
- **Opslag:** `SeasonState.Signings` (lijst `SquadPlayer`) en `LastSigningSeason`; niet geschreven als leeg/0, oude bestanden blijven byte-gelijk. `Parse` weigert keepers, onbekende rollen, attributen buiten 0..1 en meer dan drie per seizoen.
- Grens: versterkingen gaan mee over divisies (het sjabloon wisselt, zij niet), dus na promotie kan een oude versterking zwakker zijn dan het nieuwe sjabloon; hij vervangt dan alsnog de zwakste in zijn rol.
- Tests: `SigningTests`.

## Ontwikkeling en veroudering (carrière v1, pc 2, 2026-10-19, branch pc2/carriere)

`sim/Zeus.Sim/Development.cs`. De sjablonen `squads/<niveau>.json` hebben nu per speler een optioneel veld `age` (16..40; ontbreekt het, dan telt de speler als 27). De meegeleverde leeftijden zijn verzonnen (18-36).
- **Stap per afgesloten seizoen** (`Development.StepFor`): jonger dan 24 +0,02 op elk attribuut; 24-29 niets; 30-32 -0,02; 33 en ouder -0,04. Per speler begrensd op +0,10 en -0,20. Keeperattributen schuiven mee.
- **Per carrière:** `SeasonState.Development` is een delta per spelersnaam (niet geschreven als leeg; oude bestanden blijven byte-gelijk). `Competition.StartNextSeason(state, squadsDirectory)` telt voor alle 18 spelers van het sjabloon van de divisie die je net speelde, plus je versterkingen, een stap op bij hun leeftijd in dat seizoen. `Competition.PlayerSquad` past de delta's toe (volgorde: sjabloon passen, versterkingen, ontwikkeling, afwezigen) en toont leeftijden als "leeftijd in het sjabloon + afgesloten seizoenen".
- **Grens:** de sjablonen wisselen met de divisie, dus na promotie of degradatie heb je andere spelers; de delta's van de oude namen blijven staan en tellen weer mee als je terugkeert. De AI-clubs ontwikkelen niet. Geen pensioen: een 36-jarige blijft met -0,20 beschikbaar.
- **Scherm:** in het actieblok staat "Ontwikkeling sinds seizoen 1: X gegroeid, Y ouder geworden".
- Tests: `DevelopmentTests` (leeftijden in de sjablonen en loader-grens, stappen en begrenzing, toepassen incl. keeper, volgend seizoen + opslag).

Stand B4 (slimme tegenstander): staat al standaard aan (`GameSession.AdaptiveOpponent = true`, instelling `adaptive_opponent`), met de reden op het scherm (`Adaptation.ReasonKey`); hier niets aan veranderd.

## Scouting met onzekerheid (B4, pc 2, 2026-10-19, branch pc2/carriere)

`sim/Zeus.Sim/Scouting.cs`: onder de tegenstanderregel staat "Scouting: ze drukken hoog, lage verdedigingslinie (waarschijnlijk)". De waarheid is `PlayStyleProfile.Sample(tactiek)` (druk, linie, tempo in 0..1); de scout ziet elke waarde met geseede ruis (`Competition.Seed(seizoen, 40 + ronde, 1, 2)`) van ±0,05 (prof), ±0,12 (halfprof) of ±0,25 (amateur). Binnen 0,15 van het midden zegt hij niets; buiten de band plus de ruis is hij zeker, anders "waarschijnlijk". Alleen tekst: de tegenstander speelt zijn echte tactiek. Test: `ScoutingTests`.
