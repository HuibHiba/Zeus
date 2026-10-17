# Uitgangspunten voor de open bèta

De bèta is bedoeld voor spelers die de maker niet kennen. Zij moeten zelfstandig kunnen starten, een tactische keuze begrijpen, een volledige wedstrijd spelen, voortgang bewaren en een bruikbare foutmelding teruggeven. Alleen een nieuw etiket op de vriendenpreview is daarvoor onvoldoende. De actuele voortgang, testuitslagen en vrijgavebeslissing staan uitsluitend in `hervatten.md`.

## Wedstrijd en eerste sessie

De eis van de maker is circa vier tot vijf minuten per standaardwedstrijd, met een rustiger alternatief van ongeveer acht minuten. Via Esc > Spel > Wedstrijdduur kiest de speler standaard 20×, rustig 12× of analyse 4×. De keuze wordt bewaard en geldt ook voor de huidige wedstrijd. Negentig minuten duren bij 20× 270 seconden; blessuretijd komt erbij. Handmatige pauzes, coachen en een trage pc kunnen de echte duur verlengen. De simulator behoudt zijn tijdstap, volledige speelduur, vermoeidheid, wisselmomenten, gebeurtenislog en uitslag. Het versnellen van de presentatie mag geen wedstrijdresultaat veranderen.

De eerste sessie moet zonder uitleg van de maker werken: aftrappen, Spatie om stil te zetten, B om bij een onderbreking te wisselen of de tactiek aan te passen, vervolgens een leesbare eindkaart met statistieken en veldbezetting. Daarna kan de speler een eigen tactiek opslaan, opnieuw spelen of een uitdaging of competitie kiezen. Esc opent vanuit elk scherm één centrale instellingenpagina voor spel, beeld, taal, hulp, feedback en navigatie. De wedstrijd pauzeert zolang die pagina openstaat. Schattingen van de duur staan bij aftrappen en in de instellingen. Analyse is een bewuste langzame keuze, geen standaardinstelling.

De vijf- en achtminutenstanden zijn versnelde volledige wedstrijden. Zij mogen niet als een al gebouwde hoogtepuntmodus worden omschreven. De leesbaarheid van de actie moet met echte spelers worden beoordeeld. Als continu versnellen onvoldoende werkt, moet een afzonderlijke hoogtepuntpresentatie rustige stukken overslaan en belangrijke momenten leesbaar tonen; de simulator blijft dan nog steeds volledig doorrekenen.

## Wat de testbuild moet bieden

- Een samenhangende notitieboekinterface in menu, wedstrijd, tactiekmaker, selectie, uitdagingen, competitie, hulp en foutvensters, met leesbare bediening in Nederlands en Engels.
- Een duidelijke eerste stap, uitleg per scherm, zichtbare tempo- en pauzebediening en een eindkaart die de tactische uitkomst helpt beoordelen.
- Ingebouwde voorbeeldtactieken, eigen tactieken en een complete speelronde met zinvolle keuzes. Uitdagingsdoelen moeten bereikbaar en inhoudelijk begrijpelijk zijn.
- Betrouwbare lokale opslag, behoud van bestaande voortgang bij een update, reservekopieën en bescherming tegen beschadigde of nieuwere opslagformaten.
- Werkende annuleerknoppen bij langere berekeningen, bruikbare fouten en een feedbackknop in het hoofdmenu en tijdens het spelen.
- Een zelfstandige Windows-build met een unieke versie, volledige startinstructies, systeemeisen, bekende beperkingen, bronvermeldingen en licentieteksten.

Een account, multiplayer, winkel, automatische telemetrie en een complete Steam-integratie zijn geen voorwaarden voor deze eerste bèta. Een openbare feedbackbestemming en een distributieplek moeten wel bekend zijn vóór publicatie. Er mogen geen verzonnen links of stilzwijgende uploads in het spel worden gezet. Het huidige feedbackpakket blijft lokaal totdat de speler het zelf deelt.

## Vrijgavecriteria

1. Een verse uitgepakte release start buiten de ontwikkelmap en voltooit een standaardwedstrijd zonder doorspoelen. De echte duur en de leesbaarheid worden gecontroleerd; een verkorte helft is geen geldige vervanging voor die proef.
2. Dezelfde versie doorloopt live-uitdaging, competitie, wisselen, tactiek opslaan/importeren/exporteren, taalwisseling, annuleren en herstarten met bewaarde voortgang.
3. De simulatietests en relevante UI-/opslagcontroles slagen. Gewijzigde referentiehashes worden uitsluitend na verklaarde gedragsverandering en herhaalde identieke wedstrijden vernieuwd. Balansgrenzen worden niet verruimd om groen te krijgen.
4. De bewegings-AI blijft niet langdurig vaststaan. Verbeteringen worden op dezelfde invoer vergeleken, met alle uitslagen en ongewenste neveneffecten bewaard.
5. Het archief bevat alleen noodzakelijke spelbestanden en toegestane data. De verpakte assemblies passen bij de bronmanifesten en de versie is uniek genoeg om feedback te reproduceren.
6. Een schone Windows-pc en een speler zonder voorkennis bevestigen starten en de eerste speelronde. Een ontwikkelmachine of headless-proef bewijst dat niet volledig.
7. De bestaande bronvrijgavevoorwaarden zijn afgehandeld. Pas daarna worden distributiepagina, feedbackbestemming en publicatie ingericht. Een kandidaatbuild is nog geen openbare vrijgave.

## Onderzoek en ontwerpkeuzes

Geraadpleegd op 17 oktober 2026:

- [Steam Playtest](https://partner.steamgames.com/doc/features/playtest) beschrijft een afzonderlijke testomgeving, de mogelijkheid om toegang te beheren en feedback te verzamelen. Valve noemt een opvallende feedbackknop en een duidelijke bestemming voor reacties. Dit ondersteunt een openbare test met een afgebakende feedbackvraag; het schrijft geen speelduur van vijf minuten voor.
- [Steam-demo's](https://partner.steamgames.com/doc/store/application/demos) behandelt een demo als een afzonderlijk te beheren en te testen distributieproduct. Een deelbare zip is daarom een kandidaat voor distributie, geen bewijs dat de volledige openbare speelervaring al is vrijgegeven.
- [Football Manager: Match Day Experience](https://www.footballmanager.com/fm26/features/where-storytelling-evolves-fm26s-match-day-experience) gebruikt contextafhankelijke hoogtepunten en een overzicht tussen die momenten. De ontwerpconclusie voor dit spel is dat kijken en coachen begrijpelijk moeten blijven wanneer de wedstrijd wordt ingekort. Dat is een afweging voor de presentatie, geen aanleiding om de gesimuleerde voetbalwedstrijd korter te maken.

De vier- tot vijfminuteneis komt van de maker. Deze bronnen onderbouwen de test- en presentatieaanpak, niet een universele ideale demoduur.
