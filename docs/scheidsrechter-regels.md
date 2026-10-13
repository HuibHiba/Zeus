# Scheidsrechter: regels en simulatiegrenzen

Onderzoek en eerste implementatie op 2026-10-13. Regelprofiel: veldvoetbal, IFAB/KNVB 2026/27. De hieronder beschreven eerste subset is geïmplementeerd; de bredere mapping blijft ontwikkelrichting.

## Geïmplementeerde subset

`Referee.Assess(RefereeIncident)` beoordeelt `ContactAction`, expliciete nullable `Conduct`, `BallInPlay`, `ContactOrPunishableAttempt`, `InsideDefendingPenaltyArea` en `TacticalContext`. Niet-vastgestelde ernst/contact/poging geeft een onvolledig besluit. Vasthouden heeft geen ernst nodig om een overtreding vast te stellen. `TacticalContext.NotAssessed` houdt een overtredingsbesluit onvolledig; de ondersteunde scenario's geven expliciet `NoTacticalOffence` mee.

`Match.ReportIncident` past volledige besluiten toe, inclusief tweede geel. Onvolledige besluiten en strafschoppen worden vóór wedstrijdmutaties geweigerd. De pure beoordelaar kan een strafschop herkennen als de caller de plaatsvlag expliciet meegeeft; de wedstrijd kan die nog niet uitvoeren.

De duelroute levert voorlopig expliciet een synthetische `Careless` charge en veronderstelt daarbij geen SPA/DOGSO en een plek buiten het eigen strafschopgebied. Dit zijn beperkingen van het scenario, geen conclusies uit werkelijke contactgeometrie. Een tackle in het strafschopgebied wordt dus nog niet automatisch juist herkend. Het oude `CardModel` blijft als ongekalibreerd experiment bestaan maar is niet meer actief in de wedstrijdroute. Hands, indirecte overtredingen, voordeel en tactische uitzonderingen zijn niet geïmplementeerd. De hoofdagent rapporteert 106 geslaagde tests; zie de sessielog voor de uitgevoerde verificatie.

## Officiële basis

De KNVB publiceert zowel **Spelregels veldvoetbal 2026/27** als **Handboek arbitrage veldvoetbal 2026/27**. De spelregels bepalen overtredingen, sancties en hervattingen. Het arbitragehandboek behandelt vooral organisatie, opleiding, begeleiding en voorwaarden voor officials. Gebruik voor een tacklebeslissing Regel 12, aangevuld met Regels 5, 13 en 14; het handboek vervangt die niet.

Editiecontrole: de actuele IFAB-pagina toont 2026/27; de KNVB-downloadpagina noemt dezelfde editie. De titelpagina van het arbitragehandboek is gecontroleerd (68 PDF-pagina's). De Nederlandse spelregels zijn gecontroleerd bij Regel 12 (PDF-pagina 67 e.v.; document 143 pagina's), Regel 13 (77–79) en de spelregelwijzigingen. De verduidelijkingen van 14 augustus 2026 staan afzonderlijk op de KNVB-site. `latest` kan later wijzigen: sla editie en verificatiedatum in het regelprofiel op.

## Kernregels voor beoordeling

Bij de genoemde fysieke acties tegen een tegenstander bepaalt de beoordeling de sanctie: onvoorzichtig (`Careless`) geen kaart wegens die ernst, onbesuisd (`Reckless`) geel, buitensporige inzet/gevaar voor veiligheid (`ExcessiveForce`) rood. Een kaart wegens tactische gevolgen kan daar nog bovenop komen. Dit is kwalitatief; IFAB geeft geen vaste snelheidsgrens of kans op geel. Vasthouden is een afzonderlijke overtredingsgrond. Alleen de bal spelen wist een gevaarlijke actie niet uit. [IFAB Regel 12.1 en 12.4](https://theifab.com/laws/latest/fouls-and-misconduct/)

Hands vereist beoordeling van opzet, onrechtmatig vergroot lichaam of het direct/onmiddellijk scoren door de speler na eigen hand/armcontact. Gewoon bal-armcontact bewijst geen hands. De doelman heeft een aparte uitzondering binnen het eigen strafschopgebied. [KNVB Spelregels, Regel 12, p. 67–68](https://www.knvb.nl/downloads/sites/bestand/knvb/30120/spelregels-veldvoetbal)

SPA (veelbelovende aanval verhinderen) kan geel opleveren. Bij een toegekende strafschop vervalt die SPA-kaart voor een poging de bal te spelen/duel om de bal en voor niet-opzettelijke strafbare hands. DOGSO (doelpunt/duidelijke scoringskans ontnemen) leidt gewoonlijk tot rood; bij strafschop en een poging de bal te spelen/duel om de bal wordt dat geel. Vasthouden/duwen krijgen deze uitzondering niet. Opzettelijke DOGSO-hands blijft rood; niet-opzettelijke strafbare DOGSO-hands met strafschop geeft geel. Afstand tot doel, spelrichting, balcontrole en posities/aantallen van verdedigers **en aanvallers** wegen mee. [KNVB Spelregels, Regel 12.4](https://www.knvb.nl/downloads/sites/bestand/knvb/30120/spelregels-veldvoetbal)

Voordeel vereist werkelijk voordeel voor het benadeelde team; blijft dat binnen enkele seconden uit, kom terug op de overtreding. Uitgestelde kaarten blijven in beginsel staan. SPA met geslaagd voordeel krijgt geen SPA-geel. Sinds 2026/27 vervalt de DOGSO-sanctie bij een doelpunt uit voordeel; een onafhankelijke sanctie voor bijvoorbeeld onbesuisd of ernstig gemeen spel blijft staan. Voordeel bij ernstig gemeen spel, geweld of een tweede gele kaart is uitzonderlijk en vereist een duidelijke scoringsmogelijkheid. [IFAB Regel 5.3](https://theifab.com/laws/latest/the-referee/), [KNVB wijzigingen, p. 12–13](https://www.knvb.nl/downloads/sites/bestand/knvb/30124/spelregelwijzigingen-veldvoetbal)

## Mapping van incident naar regel

Dit is een afgebakende set, geen volledige tabel voor alle 17 regels. `1a` betekent: als losse, gestructureerde invoer testbaar; `1b` betekent: waarneming en wedstrijdcontext nog toevoegen.

| Waarneming / incident | Regel | Uitkomst | Vereiste simulatie-invoer | Scope |
|---|---|---|---|---|
| Charge, push, kick, trip, strike, jump-at, tackle/challenge; severity expliciet | 12.1/12.4 | Directe vrije schop; ernst bepaalt basiskaart | Actor, tegenstander, actie, ernst, bal in spel, plaats | 1a expliciet; echte contactdetectie 1b |
| Poging tegenstander te trappen, slaan of laten struikelen | 12.1 | Kan strafbaar zijn zonder raak contact | Actiesoort en poging; ernst | 1a expliciet; bewegingen 1b |
| Vasthouden | 12.1 | Directe vrije schop; tactische gevolgen apart | Vastgesteld hold-contact; duur/begin/eindplaats | 1b |
| Handsgrond vastgesteld | 12.1 | Directe vrije schop, doelmanuitzondering | Armcontact, armhouding, opzet, doelmanrol, scoringsketen | 1b |
| Gevaarlijk spel / blokkeren zonder contact | 12.2 | Indirecte vrije schop | Contact ja/nee, gevaar, bal binnen speelafstand | 1b |
| Directe-vrijeschopovertreding in eigen strafschopgebied | 12.5 / 14 | Strafschop | Exacte incidentplaats en verdedigende partij | 1a als vlag; integratie 1b |
| Incident terwijl bal uit spel | 12.5 | Bestaande hervatting blijft; misconduct kan kaart krijgen | In-playstatus, bestaande hervatting, misconduct | 1a expliciet |
| Onbesuisd met al geel | 12.4 | Tweede geel en verwijdering | Geldige eerste waarschuwing, incidenternst | 1a |
| Buitensporige inzet | 12.4 | Direct rood, ook bij al geel | Ernst, duel om bal versus geweld | 1a |
| SPA/DOGSO | 12.4 | Tactische sanctie met uitzonderingen hierboven | Kanscontext, actie/opzet, strafschop, voordeel en afloop | 1b |
| Voordeel | 5.3/12.4 | Doorgaan of terugkomen; eventuele kaart later | Verwachte en gerealiseerde baat, pending incident/card | 1b |

De mapping verwijst naar [IFAB Regel 12](https://theifab.com/laws/latest/fouls-and-misconduct/) en [KNVB Regel 12](https://www.knvb.nl/downloads/sites/bestand/knvb/30120/spelregels-veldvoetbal).

## Eerste implementatiestap

Houd drie zaken apart:

1. Een spelersbrein kiest een actie: bijvoorbeeld pressen of tackelen.
2. Een uitvoeringsmodel bepaalt wat werkelijk gebeurt. Dit mag een gekalibreerde kanscomponent bevatten. Het resultaat is een `Incident` met expliciete feiten en herkomst (`Synthetic` of later `Physical`).
3. Een pure `Referee.Assess(incident, context)` past het vastgelegde regelprofiel toe. Geen nieuwe RNG-draw voor de juridische sanctie.

Gebruik bijvoorbeeld `Action`, `Severity`, `BallInPlay`, `AgainstOpponent`, `Position`, `InsideOwnPenaltyArea` en kaartgeschiedenis. Hands, SPA en DOGSO hebben aanvullende velden nodig. Ontbrekende gegevens betekenen `Incomplete`/`NotAssessed`, niet automatisch `NoOffence` en ook niet automatisch `Careless`. Een positief oordeel `NoOffence` vereist voldoende gegevens voor de ondersteunde regelset. Bewaar de regelreferentie en ondersteunde scope bij het besluit.

Een enum met `Charge`, `JumpAt`, `Kick`, `Push`, `Strike`, `Tackle`, `Trip`, `Hold` en later `Handball` is bruikbaar. Modelleer pogingen expliciet. Een opzettelijk gekozen tackle is niet automatisch een opzettelijke trap tegen de tegenstander, onbesuisd spel of geweld. Een actie-etiket alleen kan dus geen kaart bepalen.

De huidige v0 heeft posities/snelheden/kijkrichting, maar geen armen, benen, tacklecontact, botsingsimpuls of letselmechanisme. `DuelModel` geeft op grond van afstand, snelheid, benaderingshoek en eigenschappen een synthetische uitkomst. Het oude `CardModel` gaf vervolgens kaarten met een tweede kansmodel; dat is uit de actieve route gehaald. Die bestaande kansgetallen zijn spelmodelaannames, geen regelinterpretaties en niet empirisch gevalideerd.

Voor migratie: maak een oude `DuelOutcome.Foul` expliciet een **synthetisch onvoorzichtig incident** als tijdelijke scenarioaanname. Alleen wanneer die aanname letterlijk is meegegeven kan de scheidsrechter daaruit de basiskaart bepalen. Stop het willekeurige kaartmodel in de actieve wedstrijdroute. Test onbesuisd/buitensporig gedrag via expliciete incidenten. Claim daarbij uitsluitend beoordeling op ernst; SPA/DOGSO/handshouding/voordeel blijven onbeoordeeld totdat de context bestaat.

## Hervattingen

De vrije schop ligt in beginsel op de overtredingsplaats, met uitzonderingen voor het doelgebied. De bal moet stilliggen en is in spel wanneer getrapt en duidelijk bewogen. Tegenstanders staan doorgaans minstens 9,15 m weg; aanvallers minstens 1 m van een muur van drie of meer verdedigers. Een snelle vrije schop heeft uitzonderingen. Een indirecte vrije schop vereist een andere speleraanraking om te scoren. De nemer mag de bal niet opnieuw spelen voordat een andere speler die heeft geraakt. [IFAB Regel 13](https://theifab.com/laws/latest/free-kicks/)

De huidige automatische pauze en hervatting met balbezit is een prototype. Maak later `RestartType`, `RestartPosition`, `RestartTeam`, `BallInPlay` en aanrakingen sinds hervatting expliciet. Gebruik niet één generieke ground-restart voor inworp, hoekschop, doelschop, vrije schop en strafschop.

## Bronnen en rechten

- [IFAB Laws of the Game 2026/27, documentenindex](https://theifab.com/documents/); [officiële PDF](https://downloads.theifab.com/downloads/laws-of-the-game-202627-single-pages?l=en).
- [IFAB Regel 12](https://theifab.com/laws/latest/fouls-and-misconduct/), [Regel 5](https://theifab.com/laws/latest/the-referee/), [Regel 13](https://theifab.com/laws/latest/free-kicks/), [wijzigingen 2026/27](https://theifab.com/law-changes/latest/).
- [KNVB Spelregeldocumenten](https://www.knvb.nl/node/25256), [Spelregels veldvoetbal 2026/27 PDF](https://www.knvb.nl/downloads/sites/bestand/knvb/30120/spelregels-veldvoetbal).
- [KNVB spelregelwijzigingen 2026/27 PDF](https://www.knvb.nl/downloads/sites/bestand/knvb/30124/spelregelwijzigingen-veldvoetbal).
- [KNVB verduidelijkingen 14 augustus 2026 PDF](https://www.knvb.nl/downloads/bestand/30208/publicatieadviesgroepspelregelsaugustus2026).
- [KNVB Handboek arbitrage veldvoetbal 2026/27 PDF](https://www.knvb.nl/downloads/sites/bestand/knvb/30159/handboek-arbitrage-veldvoetbal); editie bevestigd via [downloads](https://www.knvb.nl/node/49357) en titelpagina.

Dit zijn auteursrechtelijk beschermde documenten; een open herdistributielicentie is niet vastgesteld. De PDF's zijn tijdens onderzoek tijdelijk in geheugen gelezen en niet in het project opgeslagen. Gebruik eigen samenvattingen en bronlinks; neem volledige boeken niet op in Git of de spelrelease zonder passende toestemming.
