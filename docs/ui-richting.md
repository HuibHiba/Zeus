# UI-richting: Gaffer's Notebook (voorstel, 2026-10-16, ontwerpronde)

Status: **voorstel**, nog niet gebouwd. Geen code in `godot/` gewijzigd. Bekijk `docs/ui-moodboard.html` in een browser voor palet, letters, componenten en twee schermschetsen. Contrasten hieronder zijn berekend (WCAG, relatieve luminantie), niet geschat.

## 1. Kern in vijf regels

1. **Je zit in de dugout, met een notitieboek op je knie.** Alles wat je *doet* (kiezen, plannen, wisselen, lezen) staat op **papier**; alles wat de wedstrijd *is* (het veld) staat op een **krijtbord**; de rand eromheen is **bankhout in lamplicht**.
2. **Twee oppervlakken, nooit een derde.** Donker (hout en bord, krijtletters) en licht (papier, inktletters). Geen glas, geen verloop, geen gloed.
3. **Eén accent: markeerstift-geel** (`Marker`), gebruikt als streep achter tekst of als vlak, nooit als lichtgevende rand. Teams zijn rood potlood en inktblauw, niet neon.
4. **Cijfers zijn typografie, geen decoratie.** Stencil/seinbord-cijfers voor score en rugnummers, tabelcijfers voor statistiek, handschrift alleen voor korte coach-aantekeningen.
5. **Rustig bewegen.** Pagina's schuiven, potloodstrepen worden getrokken, niets pulseert.

Wat het *niet* is: geen rood-goud-marine "premium sport", geen leer-met-stiksels skeuomorfisme, geen tv-uitzending.

## 2. Waarom dit onderscheidend is

De huidige stijl ("wedstrijdnacht": navy `#0E1621`, goud `#F2B705`, Barlow Condensed, afgeronde glaspanelen) is de standaard sportgame-look. De richting hier gebruikt een warm, papierachtig tegenwicht: de speler is een coach die schrijft en tekent, en de interface voelt als zijn gereedschap. De enige herkenbare erfenis is de geel-op-donker, maar dan als markeerstift (citroen `#F2C94C`, niet goud).

## 3. Palet (tokens)

C#-naam is wat in `UiTheme.cs` komt. Bestaande namen (`Bg`, `Panel`, `PanelLight`, `Accent`, `Text`, `Muted`, `Home`, `Away`, `Good`, `Bad`) blijven als alias bestaan zodat schermen niet in één keer breken.

### Donkere vlakken (hout en bord)

| Token | C#-naam | Hex | Gebruik |
|---|---|---|---|
| Bankhout | `Bench` (alias `Bg`) | `#1A1511` | achtergrond overal, bankrand |
| Bankhout licht | `BenchLight` (alias `PanelLight`) | `#2A221B` | verhoogde balk, knop op donker |
| Bord | `Board` | `#1F2A25` | krijtbord: veld, tactiekbord |
| Bordstreep | `BoardStripe` | `#26332D` | maaipatroon, 4% lichter dan Bord |
| Omslag | `Cover` | `#2F4A3C` | notitieboekomslag (alleen hoofdmenu) |
| Krijt | `Chalk` (alias `Text`) | `#EFE9DA` | tekst en lijnen op donker |
| Krijt gedempt | `ChalkDim` (alias `Muted`) | `#A39A88` | secundaire tekst op donker |

### Lichte vlakken (papier)

| Token | C#-naam | Hex | Gebruik |
|---|---|---|---|
| Papier | `Paper` (alias `Panel`) | `#EADFC6` | pagina, paneel, tabel |
| Papier schaduw | `PaperShade` | `#D8CBAA` | tabelrij om-en-om, ingedrukte knop, tab niet actief |
| Lijn | `Rule` | `#9DB4B0` | kladbloklijnen (decor, geen tekst) |
| Inkt | `Ink` | `#241E1A` | tekst op papier |
| Grafiet | `Graphite` | `#5B554C` | secundaire tekst op papier, potloodlijnen |

### Accent, teams, status

| Token | C#-naam | Hex | Gebruik |
|---|---|---|---|
| Marker | `Marker` (alias `Accent`) | `#F2C94C` | primaire knop, selectie, streep onder titel; **nooit als tekstkleur op papier** |
| Thuis krijt | `HomeChalk` | `#F07A5A` | team op donker (veld, scorebalk) |
| Thuis inkt | `HomeInk` | `#B3341F` | team op papier (potloodrood) |
| Uit krijt | `AwayChalk` | `#7FB2E0` | team op donker |
| Uit inkt | `AwayInk` | `#2C5A8C` | team op papier |
| Goed | `Good` | `#3E6B3A` papier, `#8CC48A` donker | winst, groen vinkje |
| Fout | `Bad` | `#8E1F14` papier, `#F28B7A` donker | rode kaart, afkeur; **altijd met icoon of doorhaling**, want dicht bij Thuis-rood |

### Gecontroleerde contrasten

| Paar | Verhouding | Oordeel |
|---|---|---|
| Inkt op Papier | 12,4 | AAA |
| Grafiet op Papier | 5,6 | AA (ook klein) |
| Grafiet op Papier schaduw | 4,6 | AA, minimaal 17 px |
| HomeInk op Papier | 4,6 | AA, minimaal 17 px of vet |
| AwayInk op Papier | 5,4 | AA |
| Good (`#3E6B3A`) op Papier | 4,7 | AA |
| Bad (`#8E1F14`) op Papier | 6,7 | AA |
| Inkt op Marker | 10,4 | AAA |
| Krijt op Bankhout | 15,0 | AAA |
| Krijt gedempt op Bankhout | 6,5 | AA |
| Krijt op Bord | 12,2 | AAA |
| Krijt gedempt op Bord | 5,3 | AA |
| Marker op Bord | 9,4 | AAA (als tekst op donker) |
| HomeChalk op Bord | 5,4 | AA |
| AwayChalk op Bord | 6,6 | AA |
| Krijt op Omslag | 8,0 | AAA |
| Marker op Omslag | 6,1 | AA |
| Marker op Papier (`#EADFC6`) als tekst | 1,2 | **verboden**, alleen als vlak onder inkt |

Kleurenblind: Thuis en Uit hangen nooit alleen aan kleur. Thuis is een **gevulde schijf**, Uit een **ring met donkere vulling** (zie spelerschip). De scorebalk zet de teamnaam er altijd bij.

## 4. Typografie

Maximaal drie families, allemaal SIL Open Font License 1.1 (mag gebundeld, ook commercieel; de licentietekst moet meegeleverd worden, zie `godot/assets/fonts/OFL.txt`).

| Rol | Familie | Gewichten | Licentie | Waarom |
|---|---|---|---|---|
| Kop, score, rugnummers, knoplabels | **Big Shoulders Display** (Patric King / Chicago, Google Fonts) | ExtraBold, Bold | OFL 1.1 | seinbord-/stadionbordgevoel, smaller en strakker dan Barlow Condensed, duidelijk andere cijfers |
| Tekst en tabellen | **IBM Plex Sans** (IBM, Google Fonts) | Medium, SemiBold | OFL 1.1 | nuchter en goed leesbaar bij 17 px, ondersteunt Nederlands (diakrieten), geen Inter/Roboto-uitstraling; cijfers via OpenType `tnum` (tabelcijfers) |
| Aantekening | **Kalam** (Indian Type Foundry, Google Fonts) | Regular | OFL 1.1 | losse coach-pen; alleen korte regels (maximaal ongeveer 40 tekens) |

Optionele vierde: **IBM Plex Mono** Medium alleen als de `tnum`-instelling in Godot niet werkt (zie risico's), voor kolommen in de meter. Niet doen zonder nood.

Schaal (op het ontwerpformaat 1520x780, zie risico's over schaling):

| Naam | Font | Grootte | Gebruik |
|---|---|---|---|
| `Cover` | Big Shoulders ExtraBold | 120 | alleen woordmerk op omslag |
| `Score` | Big Shoulders ExtraBold | 44 | score in scorebalk, eindstand tot 96 |
| `Title` | Big Shoulders Bold | 44 | paginatitel, hoofdletters |
| `Heading` | Big Shoulders Bold | 28 | sectie, tabkop |
| `Body` | Plex Sans Medium | 18 | lopende tekst |
| `Table` | Plex Sans Medium, tnum | 18 | tabelcellen, rechts uitlijnen |
| `Small` | Plex Sans Medium | 17 | hulpregel, **ondergrens** (huidig 15 is te klein na schaling) |
| `Note` | Kalam | 24 | aantekening, nooit kleiner |

Regels: koppen in HOOFDLETTERS met 1 px extra letterspatiëring; tabellen rechts uitgelijnd op cijfers; nooit meer dan twee lettertypen in één component; nooit cursief nabootsen.

## 5. Spacing, vorm en materiaal

- **Raster 8 px**: ruimtetokens `S1=4, S2=8, S3=16, S4=24, S5=40`.
- **Hoeken**: papier en tabs **0 px** of 2 px (papier is recht gesneden); knoppen 3 px; chips 999. Geen 10-14 px "kaartjes" meer.
- **Papier** = `Paper`, rand 1 px `PaperShade`, schaduw als 3 px offset-rechthoek in `#000` op 35% (geen blur). Korrel: één vooraf gebakken tegel-texture (`NoiseTexture2D` of kleine PNG), laag-contrast, **niet** per frame tekenen.
- **Kladbloklijnen**: elke 28 px een 1 px lijn `Rule` (55% dekking), marge-lijn `HomeInk` 50% op 56 px van de linkerrand. Dit is de verticale ritmeheerser: tekstregels in lopende tekst vallen op de lijn (regelhoogte 28).
- **Perforatie**: een kolom gaatjes (cirkels, Bankhout-kleur) op de linkerrand van elke pagina, vast patroon, geen animatie.
- **Tape**: 40x16 px `Marker` op 85% dekking, om titels vast te plakken; hooguit één per scherm.
- **Hout**: Bankhout met vooraf gebakken horizontale nerf (laag contrast), alleen de bankrand en de achtergrond van het menu.
- **Krijt**: lijnen op Bord met 1,5 px, afgeronde uiteinden, 82% dekking (zoals nu `GameArt.Chalk`); een lichte "stof"-textuur in de hoeken, vooraf gebakken.

## 6. Componentregels

**Knop (secundair)**: vlak `PaperShade` (op papier) of `BenchLight` (op hout), tekst Plex SemiBold 20, 3 px hoek. Hover: onderstreping 3 px `Marker` onder de tekst (geen kleurwisseling van het vlak). Pressed: vlak 1 px omlaag en donkerder. Focus: 2 px `Ink`/`Chalk` omranding met 2 px tussenruimte (toetsenbord blijft bruikbaar).
**Knop (primair)**: vlak `Marker`, tekst `Ink` Big Shoulders Bold 30, onderste rand 4 px `#B8932A` (dikker rand = "ingedrukt potlood"). Hover: vlak `#F7D66E`. Eén per scherm.
**Menu-item**: Big Shoulders 38 hoofdletters in `Chalk`; hover: markeerstreep (rechthoek `Marker` 85%, 18 px hoog) achter de onderste helft van het woord, tekst wordt `Ink`. Geen balkje links meer.
**Paneel**: pagina, zie §5. Geen paneel in een paneel: binnen een pagina scheiden alleen een lijn of ruimte.
**Tabel**: kop Big Shoulders 20 op `PaperShade`, rijen 32 px, om-en-om `Paper`/`#E2D6BC`, cijfers rechts, de eigen rij met markeerstreep `Marker` 45% achter de rij. Geen verticale lijnen, alleen een dunne potloodlijn onder de kop.
**Tab**: paginarandtabs rechts van het notitieboek (verticaal gestapeld) of boven op het papier. Actief: `Paper` en 6 px uitgeschoven; inactief: `PaperShade`. Tekst Big Shoulders 22 hoofdletters.
**Tooltip**: klein stuk papier (`Paper`, 2 px hoek, 3 px offsetschaduw), Plex Medium 17, 8x6 px marge, verschijnt na 400 ms; **geen** pijl.
**Scorebalk**: zie §7.2. Hoogte 48, een bord van `Bankhout licht` met teamnaam in team-krijt, score in `Marker`, klok in `Chalk`. Rechte randen, geen schuine uiteinden; een 3 px `Marker`-lijn eronder blijft als herkenbaar detail.
**Spelerschip**: Thuis = gevulde schijf `HomeChalk` met `Bord`-donker rugnummer; Uit = ring 3 px `AwayChalk` met `Bord`-vulling en krijtnummer. Rugnummer in Big Shoulders 14-16; kijkrichting als klein krijtstreepje i.p.v. wit driehoekje; geselecteerd = `Marker`-ring 3 px; van het veld = doorgestreept met `Bad`. Geen drop-shadow-blur (de huidige 2,5 px offsetschaduw mag blijven, maar als vlakke 30%-schijf).
**Pauzemenu**: het notitieboek schuift open, zie §7.3.
**Invoer (LineEdit, SpinBox)**: onderstreping in potlood (1 px `Graphite`), focus 2 px `Ink`; geen omrande doos.
**Chip/vink**: kleine krijtcirkel of potloodstreep-vinkje; actief = `Marker`-vlak met `Ink`-tekst.
**Meters (tempo, druk, lijn, compactheid)**: tien potloodstreepjes (turven) in plaats van gekleurde blokjes; de gevulde streepjes in team-ink op papier, team-krijt op donker.

## 7. Per scherm

Ontwerpformaat blijft 1520x780 (`project.godot`). Alle maten daarop.

### 7.1 Hoofdmenu: het omslag

Metafoor: het boek ligt dichtgeklapt op de bank, rechts opengeslagen op de eerste bladzijde met de wedstrijdopzet. Lamplicht valt van linksboven.

```
+----------------------------------------------------------------------------------+
| (hout, lamplicht linksboven)                                                     |
|  +-------------------+        +------------------------------------------------+ |
|  | OMSLAG (Cover)    |        | pagina (Paper) met perforatie links            | |
|  |                   |        |  WEDSTRIJD OPZETTEN   [tape]                    | |
|  |  GAFFER'S         |        |  ----------------------------------------      | |
|  |  NOTEBOOK         |        |  THUIS            |   UIT                      | |
|  |  ===== (Marker)   |        |  [counter 532 v]  |   [hoge druk 433 v]        | |
|  |  Vol. 1  (Kalam)  |        |  tempo   ||||||||..  |  tempo ||||||||||.       | |
|  |                   |        |  druk    |||.......  |  druk  |||||||||.        | |
|  |  COMPETITIE       |        |  ...                                           | |
|  |  UITDAGINGEN      |        |  seed [ 1 ] [ nieuw ]  (Kalam: "zelfde seed =  | |
|  |  SELECTIE         |        |        zelfde wedstrijd")                      | |
|  |  TACTIEKMAKER     |        |  [   AFTRAP   ]  (primair, Marker)             | |
|  |  TACTIEK-METER    |        |                                                | |
|  |  AFSLUITEN        |        +------------------------------------------------+ |
|  +-------------------+  elastiekband (zwart, 10 px) over de rand van het omslag    |
| (voetregel: toetsen, ChalkDim 17)                                                |
+----------------------------------------------------------------------------------+
```

- Links: omslag `Cover` met doekstructuur (vooraf gebakken), titel Big Shoulders `Cover` 120 in `Chalk` (het woordmerk staat op een `Paper`-etiket van 460x190 op het omslag), `Marker`-streep, Kalam-regel "Vol. 1" of het seizoen. Menu-items eronder in `Chalk`.
- Rechts: pagina met de huidige opzetvelden. De dropdowns worden onderstreepte invoer, niveau en seed idem.
- **Geen** bewegende spelers meer als achtergrond. De huidige `MenuBackdrop` hertekent elk frame (`QueueRedraw` in `_Process`) met 22 spelers en een groot veld; vervang door een statische compositie, hooguit een zachte lamp-flikkering van 1-2% die niet vaker dan 10 keer per seconde hertekent.
- Het lamplicht is een voorgebakken radiale `GradientTexture2D` (zoals nu de vignet).

### 7.2 Wedstrijdscherm: het dugout-kader

Metafoor: je kijkt vanuit de bank naar het veld. Het kader is de dugout: dakrand boven met scorebalk eronder opgehangen, bankrand onder met de reserves erop, en rechts het notitieboek op je knie.

```
y=0   +--------------------------------------------------------------------------+
      | dakrand: Bankhout, lamplichtvlek                                         |
      |  [ THUIS 2 | 1 UIT ]  [ 63:12  HELFT 2 | 4x ]                           |  48 px
y=48  +--------------------------------------------+-----------------------------+
      |                                            | notitieboek (Paper) 360 br  |
      |   KRIJTBORD (Bord, krijtlijnen 1,5 px)     |  LIVE                       |
      |   105 x 68 m  op 10 px/m = 1050 x 680      |  schoten   36 | 14          |
      |   spelers: Thuis schijf, Uit ring          |  passes   341 | 439         |
      |   bal: wit krijtcirkeltje                  |  balbezit  42 | 58  (turven)|
      |                                            |  ---- potloodlijn ----      |
      |                                            |  Aantekening (Kalam):       |
      |                                            |  "58' geel voor 4 - let op" |
y=732 +--------------------------------------------+                             |
      | bankrand (hout): reservechips  [GK 1] [DF 12] [MF 14] ...  | B wissels   |  48 px
y=780 +--------------------------------------------+-----------------------------+
```

- **Het veld als krijtbord, niet als tv-beeld.** Reden: leesbaarheid van 22 schijven, rust, past bij de tactiekbord-taal (spelers en lijnen zijn dezelfde objecten in editor en wedstrijd), en veel goedkoper dan een schaduwrijk gras. Maaipatroon blijft, maar als 4%-streep `BoardStripe`.
- **Hoe de coach "in" de dugout zit**: (a) dakrand en bankrand omkaderen het veld, met vignet dat naar de rand donkerder wordt; (b) de reserves zitten als chips op de bankrand, met conditiestreepje (turf) en een Marker-vlak bij een speler die warmloopt (v0, nu alleen decor tot de sim dat levert); (c) het notitieboek rechts is je eigen aantekenboek, waarin events als korte handgeschreven regels verschijnen in plaats van een log in tabelvorm. Geen perspectiefbeeld, geen 3D.
- Scorebalk: links staat THUIS in `HomeChalk` en rechts UIT in `AwayChalk`, score in Big Shoulders 44 `Marker`, klok in `Chalk`; de eigen kant (in de competitie) krijgt "JIJ" als klein `Marker`-etiketje. Geen schuine uiteinden meer.
- **Overlays in de wedstrijd**: doelpunt = een strook papier schuift van de bovenrand ("GOAL" in Big Shoulders 64, tape, 1,6 s), geen schermvullende banner, het spel loopt door. Rood/geel = een kaartje (rechthoek 14x20) bij de speler plus een regel in het notitieboek.
- Het eindscherm wordt een bladzijde (`Paper`) over het bord: tabel links, heatmaps rechts in teamkleuren op papier (gedempt, korrelraster).

Het veld verschuift 2 px (`Origin` y van 390 naar 392) zodat top en bank 48 px krijgen; de huidige score bug die over het veld hangt verdwijnt.

### 7.3 Pauzemenu en wissels: het notitieboek gaat open

Trigger blijft `B` en wachten op een rustmoment (zie `Main.RequestPause`). Visueel: het veld dimt naar 55% helderheid en 30% minder verzadiging (één `ColorRect` met `Bankhout` 45% erover), het notitieboek schuift in 220 ms van onder de bankrand omhoog en blijft 1040x640 in het midden.

```
        +-----------------------------+-----------------------------+--+
        | LINKERPAGINA                | RECHTERPAGINA               |W |
        | OP HET VELD       (Heading) | REGELS EN WISSELS           |I |
        |  1 GK  Dijk        ||||||   |  Wissels over: 3            |S |
        |  4 CB  Vos         |||||    |  [14 Maas  -> 4 Vos ]       |S |
        |  ...  (conditie als turven) |  Tactiekwijziging:          |E |
        |                             |   druk  |||||||...  [-][+]  |L |
        | BANK                        |   lijn  ||||||....          |S |
        |  12 DF  Smit  ||||||||      |  (Kalam: "pas na de 60e")   |  |
        |                             |  [ TERUG NAAR HET VELD ]    |T |
        +-----------------------------+-----------------------------+--+
```

- Twee-pagina's-opslag, tabs op de rechterrand: **Wissels**, **Tactiek**, **Notities** (de laatste is nieuw, uitbreidbaar; voor nu leeg of de gebeurtenissen).
- Een wissel is een **potloodpijl** van de bank-rij naar de speler op het veld, bevestigd met Enter. De wissel staat daarna als doorgehaalde naam en nieuwe naam in de lijst.
- Conditie en vertrouwdheid als turven plus getal rechts (Plex tnum).
- Terug: Enter/Esc zoals nu (`Main._UnhandledKeyInput`); de pagina schuift terug in 160 ms.

### 7.4 Tactiekmaker: het tactiekbord

Het hele scherm is het krijtbord (`Board`), niet papier: dit is het enige scherm waar je tekent zoals een coach.

- Midden: veld vergroot, spelers als schijven met rugnummer; paden en looplijnen als **krijtlijnen met pijlpunt** (stippel = optioneel, volle lijn = vast). Kleur `Chalk`, selectie `Marker`.
- Links: gereedschapsrij op `BenchLight` (selecteer, pad tekenen, wissen, nieuwe fase) met pictogrammen (§9) en sneltoetsen in `ChalkDim`.
- Rechts: een **papieren strook** (kleine pagina, 360 br) met eigenschappen van de geselecteerde speler en de instructies; tempo/druk/lijn/compactheid als turven met knopjes.
- Fasen (opbouw, balverlies enz.) als **tabs boven het bord** op een houten lijst.
- Fouten van de validator als rode potloodopmerking in Kalam in de marge ("dek_man mist doel") met het `Bad`-icoon.

### 7.5 Meter, selectie, competitie, uitdagingen: pagina's

Allemaal `Paper` op `Bankhout`, met dezelfde paginabouwsteen (§5), perforatie en één titel met tape.

- **Meter**: linkerpagina = invoer en voortgang (turven als voortgangsbalk), rechterpagina = uitkomsten: W/G/V als drie grote Big Shoulders-cijfers, "waarom"-lijst als genummerde regels (klikbaar = markeerstreep), heatmaps op papier met korrelraster.
- **Selectie**: een **opstellingsformulier** (teamsheet): rij per speler met rugnummer in stencil-Big Shoulders, positie, rol, conditie (turf), vertrouwdheid; de basis staat boven de potloodlijn, de bank eronder.
- **Competitie**: de ranglijst als tabel met de eigen rij in `Marker`-streep; de speelronde als kruisjeslijst; speeluitslagen als Kalam-aantekening naast de rij ("2-1 thuis, 63% balbezit").
- **Uitdagingen**: een lijst met afvinkvakjes (potloodvinkje als af, `Marker` als beste); voorwaarden in Plex, een Kalam-regel als hint.

## 8. Beweging

- Duur: 120 ms (hover, focus), 160-220 ms (pagina schuift), 1,6 s (doelpuntstrook). Easing: `Tween.TransitionType.Cubic` out; geen veer/bounce.
- **Pagina schuift** op de y-as, nooit roteert; het notitieboek buigt niet.
- **Potlood trekt**: een onderstreping of pijl wordt in 120-200 ms getekend door de lengte te animeren (`Line2D` of eigen `_Draw` met voortgang 0..1).
- **Markeerstreep** schuift in 120 ms van links naar rechts achter de tekst.
- Geen pulserende glow, geen schermschudden, geen parallax. Respecteer een instelling "weinig beweging" (alle duur 0).
- Alleen elementen met een wijziging hertekenen; geen `QueueRedraw` per frame buiten het veld.

## 9. Iconografie

Eigen pictogrammen als `_Draw`-paden of kleine SVG's die Godot als texture importeert, 2 px lijn, geen vulling, einden afgerond, potlood-/krijtgevoel (lichte onregelmatigheid via een vast hoekverschil, niet random per frame). Set (24 px): vinkje, kruis, wissel (twee pijlen), tactiekbord, ranglijst, bal, fluit, gele/rode kaart (rechthoek), klok, tandwiel (nauwelijks nodig), pijl/tekengereedschap, gum, hand/selecteer. Geen emoji, geen unicode-pijlen als vervanging (huidige `↻`, `▶` hangen van het font af).

## 10. Afgewezen ideeën

1. **Dugout in perspectief (3D/POV, veld als tv-beeld).** Mooi in een mockup, maar slechter leesbaar, duur om te tekenen en het veld moet de 22 spelers kunnen dragen. Het kader is genoeg om het "dugout"-gevoel te geven.
2. **Leer, stiksels, koffievlekken, gescheurd papier.** Skeuomorfe kitsch; kost texturen en maakt elk scherm drukker. Papier blijft schoon met alleen korrel en perforatie.
3. **Handschrift voor alle tekst.** Slecht leesbaar bij 1366x768 en ongeschikt voor tabellen. Handschrift alleen voor korte aantekeningen.
4. **Neon, glow en glasmorphism op donker** ("esports"). Generiek, vermoeiend, botst met het papier.
5. **Scheef gedraaid papier.** Roteren van tekst in `Control`/`_Draw` geeft wazige letters en lastige hit-boxes. Alleen rechte pagina's met offsetschaduw.
6. **Gouden accent blijven gebruiken als gloeiende rand/knop.** Wat het nu typeert als sportgame-standaard; het accent wordt een mat markeerstiftvlak.
7. **Tv-broadcast scorebalk met schuine uiteinden** (huidige `DrawScoreBug`). Dat is de uitzendstijl, niet de coach-stijl; wordt een recht bord.

## 11. Uitvoeringsrisico's in Godot

1. **`_Draw` en fillrate.** `MenuBackdrop._Process` hertekent elk frame een veld plus 22 spelers; de wedstrijd tekent elk frame alles. Papiertextuur, hout, krijtstof en vignet moeten **vooraf gebakken texturen** zijn (`NoiseTexture2D`, `ImageTexture`, of kleine PNG's die tegelen), niet iets dat elk frame wordt opgebouwd. Pagina's tekenen met `StyleBoxTexture` (9-slice) of `NinePatchRect`, kladbloklijnen met één `DrawMultiline`.
2. **Tekstschaling bij 1366x768.** Project is 1520x780 met `canvas_items`/`keep`; bij 1366x768 is de schaal ongeveer 0,9 (kleine vensters en laptops nog lager). Daarom `Small` minimaal 17 en Kalam minimaal 24 op ontwerpformaat. Zet voor Big Shoulders **MSDF** aan (scherp bij elke schaal) en voor Plex Sans grijswaarden-antialiasing met *light* hinting en subpixel-positionering; test echt op 1366x768 en 1280x720, niet alleen op de maker-resolutie.
3. **`tnum` (tabelcijfers) in Godot.** Opentype-features via `FontVariation.OpenTypeFeatures` moeten werken met runtime geladen bytes (`FontFile.Data` zoals nu in `GameFonts`); controleer dit op dag één, anders val terug op Plex Mono voor cijfers.
4. **Runtime font-laden.** `GameFonts` leest nu bytes met `FileAccess.GetFileAsBytes`; dat omzeilt de importer en dus de import-opties (MSDF, hinting). Overweeg `res://`-import als `.ttf`/`.fontdata` met vaste instellingen of stel de eigenschappen op de `FontFile` in code in.
5. **Diakrieten en lange Nederlandse woorden** ("Tactiekwijziging", "Overtredingen") in een smal lettertype; knoppen mogen niet afkappen. Meet met `Font.GetStringSize` en geef knoppen `CustomMinimumSize` op basis van de langste vertaling (Nederlands én Engels).
6. **Tweetaligheid.** De huidige UI is Nederlands; de spelnaam en Engels als hoofdtaal vragen een lokalisatielaag (CSV/`tr()`). Dat is een apart werkpakket, maar breedtes in dit ontwerp zijn daarop gemeten.
7. **Contrast na vignet.** Een vignet over het bord verlaagt de randcontrasten; houd het onder 35% bij de randen en meet krijtlijnen (82% krijt) opnieuw.
8. **Weinig geheugen op de ontwikkelpc.** Texturen klein houden (512x512 tegels), geen 4K-assets; bouwen en testen gebeurt in andere stromen.

## 12. Migratieplan

### 12.1 Assets (nog niet downloaden)

Plaats in `godot/assets/fonts/` (naast de bestaande Barlow-bestanden en `OFL.txt`):

| Bestand | Bron |
|---|---|
| `BigShouldersDisplay-ExtraBold.ttf`, `BigShouldersDisplay-Bold.ttf` | Google Fonts "Big Shoulders Display" (statische instanties, anders de variabele font en weights in code) |
| `IBMPlexSans-Medium.ttf`, `IBMPlexSans-SemiBold.ttf` | Google Fonts "IBM Plex Sans" of de IBM/plex-repo |
| `Kalam-Regular.ttf` | Google Fonts "Kalam" |
| (optioneel) `IBMPlexMono-Medium.ttf` | alleen bij de `tnum`-terugval |

Voeg de bijbehorende OFL-teksten aan `OFL.txt` toe (elke familie heeft een eigen auteursregel). Controleer vóór bundelen dat de gedownloade bestanden de licentie `OFL 1.1` dragen. Barlow-bestanden pas verwijderen als niets ze meer gebruikt.

Geen downloads voor papier/hout/krijt: die texturen worden procedureel gebakken bij de start (klein `Image`, eenmalig) of komen als kleine eigen PNG's in `godot/assets/ui/`.

### 12.2 Volgorde van bestanden

1. `godot/UiTheme.cs`: nieuwe tokens (§3) met oude namen als alias; `Create()` krijgt de nieuwe stijlen (knop, primair, menu-item, tab, chip, paneel, invoer); constanten voor spacing en lettergroottes (`Small` 17). Dit alleen al verandert alle schermen zonder ze aan te raken.
2. `godot/GameArt.cs`: `GameFonts` uitbreiden met `Kop`, `Tekst`, `Hand` (en mogelijk `Mono`); `DrawPitch` wordt krijtbord (kleuren Bord/Bordstreep, lijnbreedte blijft), `DrawPlayer` krijgt schijf/ring-variant, `DrawScoreBug` wordt rechthoekig bord, `DrawVignette` zachter.
3. Nieuw: `godot/UiPaper.cs`: helper voor pagina (stijlbox met korrel, perforatie, kladbloklijnen, offsetschaduw), tape, bankrand en markeerstreep; gebruikt door alle schermen.
4. `godot/MenuBackdrop.cs`: vervangen door statisch omslag/bankhout met lampvlek, woordmerk "Gaffer's Notebook"; geen bewegende spelers; `Exclude` vervalt.
5. `godot/Menu.cs`: layout omslag links, pagina rechts (§7.1), dropdowns als onderstreepte invoer.
6. `godot/Main.cs` (wedstrijdscherm): `Origin` naar (600, 392), top- en bankrand, `BuildHud` met bankrand en reserves, livepaneel als pagina, eindscherm als pagina. Dit raakt gedrag niet; alleen tekenen en HUD. Let op: **niet tegelijk met een andere stroom aanpassen** (afspraak "nooit tegelijk hetzelfde bestand").
7. `godot/PauseMenu.cs`: boek-open-layout met tabs (§7.3).
8. `godot/Editor.cs`, `godot/FormationPreview.cs`: tactiekbord (§7.4).
9. `godot/Meter.cs`, `godot/Heatmap.cs`, `godot/SquadScreen.cs`, `godot/Season.cs`, `godot/Challenges.cs`: paginascherm per scherm (§7.5).
10. Opruimen: Barlow-bestanden en oude aliassen weg, `docs/hervatten.md` bijwerken, `scripts/smoke_godot.py` draaien.

### 12.3 Dunne eerste versie versus volledig

**Dunne eerste versie** (waar de richting al zichtbaar wordt, ruwweg 1-2 dagen werk):
- stap 1 (tokens, nieuwe stijlen, lettergroottes), de drie nieuwe lettertypes laden (stap 2 voor `GameFonts`), `UiPaper` met alleen pagina en markeerstreep;
- stap 4 en 5: hoofdmenu als omslag + opzetpagina;
- stap 6: alleen kader van het wedstrijdscherm (scorebalk, krijtbord, bankrand, livepaneel als pagina), eindscherm blijft voorlopig zoals het is;
- alle andere schermen erven de nieuwe kleuren en letters via het thema en zien er "ongeveer goed" uit (papierpaneel op hout).

**Volledige uitwerking** (daarna, per scherm te plannen): pauzemenu als boek met tabs en potloodpijl-wissel, tactiekmaker als bord met krijtpaden, meter/selectie/competitie/uitdagingen als eigen pagina's, aantekeningen in Kalam gevoed door events, iconenset, bewegingen (§8), Engelse hoofdtaal via lokalisatie.

Beslismoment na de dunne versie: screenshots op 1366x768 en 1520x780 naast de huidige; bevalt de richting niet, dan is alleen het thema te herzien en is er niets weggegooid dat dure assets kostte.

## 13. Open vragen voor de maker

- Mag het veld echt een krijtbord zijn (donker, geen gras)? Dit is de grootste zichtbare beslissing en de meest omkeerbare: één kleurwissel in `GameArt.DrawPitch`.
- Wil je Engels als hoofdtaal in de UI vanaf de eerste versie of pas later? Bepaalt of Kalam-aantekeningen en knopbreedtes meteen in twee talen moeten.
- Is "Gaffer's Notebook" de definitieve titel? Het woordmerk, het omslag en de letters zijn daarop gebouwd.
