# Buitenspel — positiebepaling v0

Gebouwd en bronnen gecontroleerd op 2026-10-13; IFAB-editie 2026/27.

## Ondersteund

`OffsideRules.PositionsAtTouch(snapshot, attackingSide, attacksPositiveX, source)` levert een onveranderbare lijst speler-ID's in de oorspronkelijke snapshotvolgorde. Het is een pure functie zonder RNG en zonder wijziging van de wereld.

Leg het snapshot vast bij de aanraking door de ploeggenoot: bij een trap de eerste aanraking; bij een worp door de keeper zou de vrijgave het moment zijn. Een kandidaat moet op de helft van de tegenstander staan en dichter bij diens doellijn zijn dan zowel de bal als de voorlaatste tegenstander. Gelijke lijn telt als niet-buitenspel. De keeper telt als iedere tegenstander. Direct ontvangen uit doelschop, inworp of hoekschop is uitgezonderd. [IFAB Regel 11.1–11.3](https://theifab.com/laws/latest/offside/)

De aanvalrichting is een expliciete boolean: `true` voor +x, `false` voor -x. `SentOff` spelers zijn uitgesloten; met minder dan twee actieve tegenstanders of zonder bruikbare balpositie wordt de invoer afgewezen. Dit is een API-contract voor geldige wedstrijdsnapshots, geen extra spelregel. De uitzondering voor een hervatting geldt alleen rechtstreeks: de volgende aanraking van een ploeggenoot wordt opnieuw als `OpenPlay` beoordeeld.

## Positie en overtreding

De lijst bevat mogelijke buitenspelspelers, geen overtredingen. Voor de eerste integratie onthoudt de wedstrijd de lijst op het passmoment en fluit pas wanneer een kandidaat de bal daadwerkelijk speelt/raakt. Gebruik daarvoor de plek van die betrokkenheid voor een indirecte vrije schop. Ook teruglopen naar de eigen helft wist een eerdere buitenspelpositie niet uit. [IFAB Regel 11.2 en 11.4](https://theifab.com/laws/latest/offside/)

Een opzettelijk gespeelde bal door een tegenstander kan een eerdere buitenspelfase beëindigen. Een afwijking/rebound of redding doet dat niet; een redding kan ook door een veldspeler gebeuren. De regel beoordeelt controle en handelingsmogelijkheid, niet uitsluitend of de tegenstander de bal raakt. [IFAB Regel 11.2](https://theifab.com/laws/latest/offside/)

## Grenzen en vervolg

Spelercentra zijn een benadering: de simulatie heeft geen hoofd, romp, voeten of armen als afzonderlijke geometrie. Het officieuze resultaat wordt dus niet als lichaamsdeelprecisie gepresenteerd. Er zit geen kunstmatige tolerantie in de lijnvergelijking; de wereldcoördinaten worden strikt vergeleken.

De functie bewaart zelf geen fasehistorie. De wedstrijd moet aanrakingen, opzettelijk spelen, rebounds en reddingen afzonderlijk herkennen. Een generieke aanraking door de tegenstander mag niet automatisch alle kandidaten wissen. Zicht van de keeper blokkeren, een tegenstander hinderen, een duel aangaan, een duidelijke poging met impact en tijdelijk buiten het veld staan vallen buiten deze v0. Deze gevallen vereisen aanvullende acties/contact- en contextinformatie.

Tests dekken beide speelrichtingen, bal- en verdedigingslijn, middenlijn/eigen helft, gelijke lijn, twee veldspelers achter de keeper, uitsluiting van verwijderde spelers, directe hervattingsuitzonderingen, stabiele volgorde en onvolledige invoer.

## Bron

[Officiële IFAB Law 11 — Offside, editie 2026/27](https://theifab.com/laws/latest/offside/). Gecontroleerd op 2026-10-13. `latest` kan bij een nieuw seizoen veranderen; editie hoort daarom bij het wedstrijdregelprofiel. Dit document bevat een eigen samenvatting en geen overgenomen handboektekst.

## Dieptelopen en steekpassen (v0)

De beoordeling van buitenspel blijft bij `OffsideRules` en het bestaande contactpad in `Match`: een positie wordt op het trapmoment vastgelegd; pas balcontact activeert de overtreding. De wetsimplementatie is niet aangepast. Deze uitbreiding verandert uitsluitend de loopopdracht en passkeuze.

### Instellingen

`MatchConfig.AttackingRuns` bevat een `AttackingRunConfig`; weglaten gebruikt onderstaande defaults. Beide onderdelen staan standaard aan en zijn afzonderlijk uit te zetten. Alle numerieke waarden moeten eindig zijn; afstanden, ruis en bonus zijn niet-negatief, snelheid, loopduur en startafstand positief. Het herstelinterval moet minstens de loopduur zijn. `Match.Create` voert `Validate()` uit.

| Parameter | Default | Betekenis / v0-aanname |
| --- | ---: | --- |
| `EnableRuns` | `true` | Dieptelopen toestaan. |
| `EnableThroughBalls` | `true` | Een looppas voorbij de geschatte lijn toestaan en de ontvanger vlak vóór de trap opnieuw toetsen. |
| `RunDepthMeters` | 1,5 m | Loopdoel ligt deze afstand achter de eigen geschatte lijn. |
| `LineMisjudgementMeters` | 1,0 m | Standaardafwijking van onafhankelijke normale lijnruis, gemiddelde nul; geen maximale fout. Nul schakelt de ruis uit. |
| `ThroughBallBonus` | 0,8 | Extra score voor een toegestane steekpas, bovenop de bestaande afstand-, vooruitgang- en risicoscore. |
| `MinRunSpeed` | 0,3 m/s | Minimaal waargenomen snelheid in de aanvalsrichting voor een steekpas. |
| `RunStartDistanceMeters` | 14 m | Alleen lopen wanneer de geschatte lijn hoogstens deze afstand vóór de loper ligt. |
| `RunSeconds` | 4 s | Maximale aaneengesloten loopduur. |
| `RunCooldownSeconds` | 60 s | Minimale tijd tussen twee loopstarts van dezelfde speler, gemeten vanaf de vorige start. |

De pass gebruikt de bestaande `PassConfig.LeadSeconds` (0,35 s): doel = zichtbare positie + zichtbare snelheid × vooruitlooptijd, begrensd tot het veld. Er is geen aparte nauwkeurigheids- of succeskans voor steekpassen.

### Loopopdracht en informatiegrenzen

**V0-aanname:** de ploeg geeft maximaal twee spelers toestemming. Alleen tactiekrollen `spits`, `targetman` (als spits beschouwd) en `buitenspeler` komen in aanmerking; een spelers-id zoals `ST` geeft geen toestemming. Spitsen gaan vóór buitenspelers, met vervolgens ordinale id-volgorde. De baldrager, keeper en uitgesloten spelers zijn uitgesloten. Dit is eigen opdrachtinformatie, geen keuze op basis van verborgen tegenstanders. De vaste voorkeursvolgorde kan een buitenspeler zonder opdracht laten zolang twee spitsen beschikbaar zijn; er is nog geen tactische rotatie.

Het brein krijgt `CanRunInBehind`, `TeamBallCarrierId` en `LineMisjudgement` in zijn `Perception`. De loop begint alleen wanneer de ploeg daadwerkelijk een baldrager heeft, die medespeler zichtbaar is en op de middenlijn of in de aanvallende helft staat. Losse ballen en passes onderweg starten geen loop. De loper moet minstens twee tegenstanders zien en binnen `RunStartDistanceMeters` van zijn geschatte lijn staan. Het loopdoel behoudt de breedte van `FormationTarget` en gebruikt `IntentKind.RunInBehind`; de beweging volgt dezelfde fysica als `MoveToPosition`.

**V0-aanname:** de lijn is de op één na diepste zichtbare tegenstander, inclusief de keeper, in de eigen aanvalsrichting, plus de eigen lijnfout. Minder dan twee zichtbare tegenstanders geeft geen bruikbare schatting. Ontbrekende tegenstanders maken de foutloze schatting conservatief: de op één na diepste zichtbare tegenstander kan niet verder terug staan dan de echte op één na laatste tegenstander. De waarneming bevat geen verborgen lijn of tegenstanderpositie.

Een pass of schot beëindigt alle lopende dieptelopen meteen. De bedoelde ontvanger volgt vervolgens het doorgegeven passdoel; overige lopers keren naar hun eigen formatiedoel terug. Na balverlies of het verlopen van de loopduur dwingt `Match` een nieuwe beslissing af vóór de volgende beweging. Het herstelinterval blijft gelden als de loop voortijdig eindigt. Deze begrenzing voorkomt dat aanvallers permanent diep blijven staan.

### Passkeuze en lijnruis

De bestaande gewone pass mijdt een vooruitberekend doel voorbij de meest gevorderde zichtbare veldspeler minus `PassConfig.OffsideMargin`. **V0-aanname voor de uitzondering:** een ontvanger die voorwaarts loopt met minstens `MinRunSpeed` mag wel een doel voorbij die lijn krijgen, zolang zijn huidige positie volgens de passer nog gelijk aan of achter diens geschatte lijn ligt. Achter de bal of in de eigen helft geldt geen buitenspelpositie. `LeadSeconds` moet positief zijn. Een stilstaande of teruglopende ontvanger krijgt deze uitzondering niet; ook een geforceerde pass mag de voorwaarden niet omzeilen.

Een gekozen steekpas krijgt `ThroughBallBonus` en gaat, na de bestaande wachttijd `HoldSeconds`, vóór de schotkeuze. Gewone passen en wegwerptrappen houden hun bestaande score en uitvoeringsfout. Dit is een ontwerpaanname, geen gekalibreerde trainersvoorkeur.

Omdat een loper tijdens de draai naar de passlijn verder beweegt, geeft het standaardbrein bij ingeschakelde steekpassen `CheckOffsideLine` mee voor alle gekozen passes. Vlak vóór de trap maakt `Match` een nieuwe **gefilterde** waarneming van de passer en toetst de huidige ontvangerpositie opnieuw, met dezelfde eerder getrokken lijnfout. De loper mag gelijk aan de geschatte lijn staan; de marge op het vooruitberekende doel van een gewone pass wordt hier niet opnieuw afgetrokken. Bij afwijzing volgt de volgende tick een nieuwe beslissing. Eigen testbreinen die deze toets niet aanvragen kunnen nog bewust een buitenspelpass geven; de scheidsrechter blijft die beoordelen.

**V0-aanname:** `Match` trekt bij iedere beslissing twee wedstrijd-`Rng`-waarden in de bestaande vaste spelersvolgorde (thuis, uit, tactiekvolgorde). Box-Muller maakt daar één normale lijnfout van, zonder gecachte tweede waarde. Passer en loper krijgen onafhankelijke fouten; de fout wordt bij iedere scan vernieuwd. Een brein heeft geen RNG. Bij nul ruis of beide onderdelen uit worden geen extra waarden getrokken. Beide onderdelen uit reproduceert de oude wedstrijdhashes; een afzonderlijk uitgeschakeld onderdeel kan nog wel andere RNG-vervolgwaarden krijgen door het ingeschakelde onderdeel.

### Meting en grenzen

Gemeten met `scripts/calibration_run.py`, seeds 1 t/m 8, `laag_blok_442` tegen zichzelf, twee volledige helften. De meetdefinities zijn ongewijzigd: wegwerptrappen tellen niet als pass; passsucces betekent eerstvolgend relevant contact bij de bedoelde ontvanger. Meetuitvoer is tijdens deze taak onder `sim/tmp/` gezet om binnen de schrijfscope te blijven. De kopregel van het bestaande script noemt de uitploeg altijd `man_om_man_433`; de werkelijk gebruikte uit-tactiek was in beide metingen `laag_blok_442`.

De lokale meetbestanden zijn na afloop bewaard in `sim/Zeus.Runner/bin/t1-metingen/` (genegeerd door Git).

| Maat per team per wedstrijd | Vóór | Na | Na thuis | Na uit |
| --- | ---: | ---: | ---: | ---: |
| Buitenspel | 0,00 | 0,50 | 0,38 | 0,62 |
| Schoten | 18,44 | 18,81 | 18,50 | 19,12 |
| Passes | 422,25 | 423,25 | 424,50 | 422,00 |
| Pass geslaagd (%) | 66,68 | 63,78 | 63,99 | 63,57 |

Passvolume verandert met +0,24%, binnen de toegestane 25%. De gemiddelde schoten van beide ploegen liggen tussen 8 en 22. Buitenspel komt nu voor, maar het doel van circa 1,8 is niet gehaald; verdere kalibratie blijft open. Het lagere passsucces blijft eveneens een afwijking. Acht seeds geven geen betrouwbare uitspraak over zeldzame events of andere tactieken.

Verkennende metingen zonder afstandsbegrenzing gaven circa 89 schoten per team; daarom zijn korte lopen, startafstand en herstelinterval expliciete v0-aannames. Er is geen telling veranderd om de doelen te halen. De tests dekken voorwaarden, rollen, maximaal twee lopers, loopduur/herstelinterval, passkeuze met en zonder bonus, spiegeling, normale deterministische ruis, uitschakelen met oude hashes en volledige wedstrijden met buitenspel. De foutloze variant wordt apart over dezelfde seeds gecontroleerd op afwezigheid van buitenspelevents. Event- en snapshothashes van de bestaande goldenwedstrijden (ongelijk paar seed 42 en symmetrisch paar seed 7) zijn opnieuw met de Release-runner gegenereerd.
