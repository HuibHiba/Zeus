# Teambeweging en voetbalreferenties

De tactiektekening beschrijft rolposities per fase. Een echte ploeg beweegt ook tussen die ankers: ploeggenoten bieden verschillende passhoeken, de achterste lijn bewaakt de ruimte achter de baljager en een speler sluit aan zodra de bal verder komt. Voortdurend willekeurig lopen is geen vervanging voor dit gedrag.

## Geraadpleegde primaire bronnen

- [SkillCorner Open Data](https://github.com/SkillCorner/opendata) bevat tracking, dynamische gebeurtenissen en spelfasen. De bestaande lokale kopie staat op commit `4340d274572876239c154c90bc507a9b3250a656`; de repo vermeldt MIT en vraagt bronvermelding. De bestaande projectvoorwaarden voor productvrijgave blijven van toepassing. Ruwe bestanden en echte spelersnamen gaan niet in de game.
- [FIFA: John Peacock, Passing and receiving](https://www.fifatrainingcentre.com/en/practice/elite-sessions/in-possession/john-peacock-passing-and-receiving.php) benadrukt bewegen binnen de eigen zone, breedte, aanspeelhoeken en een verbindende middenvelder. [FIFA: Building from the back](https://www.fifatrainingcentre.com/en/practice/elite-sessions/in-possession/building_from_the_back_skinner.php) beschrijft steun rond de bal en ruimte maken voor de volgende pass. Dit zijn voetbalprincipes, geen numerieke simulatieparameters.
- [HELIOS Base](https://github.com/helios-base/helios-base/tree/66fd63d6f9d022423b8c4430668a2b8510353caf) is een openbare RoboCup-voetbalploeg. De geraadpleegde speler- en strategiecode gebruikt verschillende roltaken en balafhankelijke posities. De hoofdlicentie noemt MIT, maar de geraadpleegde bronbestanden bevatten GPL-headers. Er is geen code overgenomen; dit is uitsluitend een ontwerpverwijzing.
- [Metrica sample-data](https://github.com/metrica-sports/sample-data) is bekeken als mogelijke trackingreferentie. De README vraagt erkenning van de bron zonder een eenduidige formele productlicentie. Deze bron is niet gebruikt voor productparameters.

## Van waarneming naar gedrag

De bestaande SkillCorner-afleiding onderscheidt `build_up`, `create` en `finish`. Een centrale verdediger staat daarin gemiddeld ongeveer op x = 0,18, 0,36 en 0,60, een spits op ongeveer 0,39, 0,61 en 0,82. Dat zijn gemiddelden uit een bepaalde steekproef, geen universeel verplichte formaties. Een ploeg die uitsluitend tussen de uiterste opbouw- en aanvalsposities schakelt, mist het geleidelijk aansluiten tijdens het creëren van een aanval.

De simulator gebruikt daarom een geleidelijke tussenpositie op basis van de reeds bekende balpositie. De getekende posities blijven de ankers. Instructies voor breed blijven, achterblijven, expliciete overgangsfases en looppaden houden hun betekenis. Lokale steun kiest een vrije passhoek en bewaart ruimte tot ploeggenoten. Verdedigend kunnen een terugvallende lijn en één dekker achter de baljager voorkomen dat de rest uitsluitend op een verouderde positie wacht.

De afstandsgrenzen en de menging tussen ankers zijn voorlopige modelkeuzes. Er is geen neuraal netwerk getraind en er worden geen verborgen bal- of tegenstanderposities aan de spelers gegeven. Tracking wordt gebruikt om gedrag te onderzoeken en te toetsen, niet om echte wedstrijden of spelersnamen in het product te kopiëren. Actuele testuitslagen, kandidaatkeuzes en open gebreken staan uitsluitend in `hervatten.md`.

## Wat de vergelijking moet meten

Een verbetering moet naast beweging ook samenhang laten zien: vrije aanspeelpunten, behoud van rolafstand en ploegstructuur, reageren na balverplaatsing en afwezigheid van lange gezamenlijke stilstand. Meer gelopen meters alleen is onvoldoende. Passes, schoten, goals, overtredingen en ongewenste neveneffecten blijven onderdeel van dezelfde vergelijking.

Tracking bevat camera-uitsneden, detectiefouten en extrapolatie. Gedetecteerde beweging over een halve seconde kan daarom niet zonder correctie gelijk worden gesteld aan de momentane simulatiewaarneming. Een gunstig stilstandspercentage is evenmin bewijs dat het hele spel realistisch is.
