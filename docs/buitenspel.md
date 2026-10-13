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
