# Taken voor de nachtwerker

Elke sectie hieronder is de opdracht van een taak in `tasks.json` (veld `prompt: "taken.md#<id>"`). De gemeenschappelijke regels
staan in `REGELS.md` en worden er door het script voor gezet.

### TAAK T1
## Doel
In de simulatie komt buitenspel bijna niet voor (0 per team per wedstrijd, doel ca. 1,8). Oorzaak: aanvallers staan op hun
formatiepositie en lopen nooit achter de laatste lijn; de passer mijdt bovendien elke ontvanger voorbij de laatste zichtbare
verdediger. Bouw twee dingen, beide uit te zetten via config:

1. **Diepteloop**: een aanvaller zonder bal (maximaal twee per ploeg tegelijk, alleen spitsen en buitenspelers volgens hun rol
   in de tactiek) loopt, terwijl zijn ploeg de bal heeft en de baldrager op de middenlijn of in de aanvallende helft is, naar een
   punt net voorbij de door hem geschatte laatste lijn van de tegenstander (`RunDepthMeters`, bijvoorbeeld 1,5 m erachter). Hij
   schat die lijn alleen uit wat hij ziet (zijn `Perception`). Na de pass of na een verloren bal keert hij terug naar zijn
   formatiepositie. Gebruik een nieuw intent of hergebruik `MoveToPosition`; kies wat het best in de bestaande code past.
2. **Steekpas**: `PassChoice` mag een ontvanger voorbij de geschatte lijn kiezen als hij een looppas maakt (richting en snelheid
   uit de waarneming, `LeadSeconds` voor de afstand), met een bonus `ThroughBallBonus`. De inschatting van de lijn door de
   passer en de loper heeft een fout `LineMisjudgementMeters` (standaard ca. 1,0 m, normale ruis): zonder fout is buitenspel
   uitgesloten, met fout ontstaat het zoals in het echt. De ruis moet deterministisch zijn: `Match` trekt de waarden uit de
   wedstrijd-`Rng` in vaste volgorde en geeft ze via de `Perception` aan het brein, of een vergelijkbare constructie. Een brein
   mag zelf geen `Rng` hebben.

De werkelijke beoordeling blijft bij de bestaande `OffsideRules` en `Match`. Pas de wetsimplementatie niet aan.

## Eisen
- Nieuwe instellingen in een config-record met default en `Validate()`; standaard AAN met voorzichtige waarden.
- Tests: loopintent (alleen onder de voorwaarden, maximaal twee), steekpas gekozen en niet gekozen, ruis deterministisch
  (zelfde seed, zelfde uitkomst), uitgeschakeld = oud gedrag, en een wedstrijdtest waarin met de feature aan over meerdere
  seeds minstens één `Offside`-event ontstaat.
- Werk de golden-hashes bij (regel 5 van de regels) en beschrijf de v0-aannames in `docs/buitenspel.md` (nieuw, Nederlands).
- Meet na afloop 8 seeds van het symmetrische paar met `calibration_run.py` en meld buitenspel per team, schoten, passes en
  pass geslaagd%. Je hoeft het doel van 1,8 niet exact te halen (een andere taak kalibreert), maar het moet duidelijk groter
  zijn dan 0 en het spel mag niet instorten (schoten tussen 8 en 22 per team, passes niet meer dan 25% verschil met voor je werk).

### TAAK T2
## Doel
Kalibreer de buitenspel-instellingen die taak T1 bouwde (diepteloop, steekpas, `LineMisjudgementMeters`) zodat het symmetrische
paar (`laag_blok_442` tegen zichzelf) op ca. 1,8 buitenspel per team per wedstrijd uitkomt (acceptabel: 1,2 tot 2,6), zonder dat
de andere kernmaten verslechteren.

## Werkwijze
- Lees `docs/buitenspel.md` (door T1 geschreven) en de parameters. Bouw: `dotnet build sim/Zeus.Runner -c Release`.
- Sweep met `--config` (zie `docs/kalibratie.md`): maak per variant een JSON met alleen de te wijzigen records, bijvoorbeeld
  `{"Passing":{"ThroughBallBonus":0.5}}`, en draai 4 seeds per variant, daarna 8 seeds voor de gekozen. Houd bij: buitenspel,
  schoten (doel 13, acceptabel 8 tot 18), doelpunten, passes, pass geslaagd%, onderschept%, uit%.
- Zet de gekozen waarden als default in de code, werk de golden-hashes bij, voeg een sectie "Kalibratieronde 3: buitenspel" toe
  aan `docs/kalibratie.md` met voor/na-tabel en eerlijke afwijkingen. Als 1,8 niet haalbaar is zonder andere maten te breken,
  kies dan de beste afweging en leg uit waarom.

### TAAK T3
## Doel
De standaardbrein speelt nooit terug op de eigen keeper (0 van ca. 800 passes per wedstrijd). Daardoor gaat de trigger
`bal_naar_keeper` (tegenstander speelt terug op zijn keeper) in de simulatie vrijwel nooit af. In het echte voetbal speelt een
ploeg onder druk regelmatig terug. Bouw terugspelen op de keeper, met als doel ca. 5 tot 12 open-spel passes naar de eigen
keeper per team per wedstrijd.

## Wat
- De baldrager kent zijn eigen keeper ook als die buiten zijn gezichtsveld staat (een speler weet waar zijn keeper staat); doe
  dat via de waarneming-laag, zonder het principe te breken dat een brein alleen zijn eigen waarneming gebruikt. Voeg een
  instelling toe, bijvoorbeeld `PerceptionConfig.KeeperAwarenessMeters`, en leg in `docs/waarneming.md` uit wat dit modelleert.
- `PassChoice`/`PassingBrain` krijgt de keeper als optie voor de pass, met een score die vooral telt als de baldrager onder druk
  staat (tegenstander dichtbij) en in de eigen helft is, en nooit als hij vooruit kan spelen met een goede score. Instellingen
  in `PassConfig` met default en `Validate()`, bijvoorbeeld `BackPassToKeeperScore` en `BackPassPressureMeters`.
- De keeper moet de bal zonder handen verwerken (een bewuste terugspeelpass mag niet met de handen worden gepakt): controleer in
  de bestaande keeper-/controlecode dat dat al zo is en pas het anders aan.
- Na ontvangst speelt de keeper de bal weer uit met het bestaande gedrag (pass); hij mag niet vastlopen.

## Eisen
- Tests: de keeper wordt als optie gekozen onder druk en niet zonder druk; uitzetten = oud gedrag; een wedstrijdtest waarin
  over meerdere seeds minstens één `TriggerFired` met `bal_naar_keeper` ontstaat voor de ploeg met die trigger
  (`man_om_man_433` is de Away-ploeg met die trigger; `laag_blok_442` speelt thuis).
- Werk de golden-hashes bij (regel 5). Meld in je verslag hoeveel passes naar de keeper je meet per team per wedstrijd (tel
  events `Pass` waarvan de uitkomst begint met `to GK`) en de uitkomst van `calibration_run.py` voor 8 seeds van het
  symmetrische paar (passes, pass geslaagd%, schoten).

### TAAK T4
## Doel
Tactiektriggers zijn nu beperkt: alleen de fase `verdedigen`, geen `zone`, en drie voorwaarden. Lees `docs/blokwissels.md` (sectie
"Triggers"), `sim/Zeus.Sim/TriggerRules.cs`, `Match.cs` (`FireTriggers`), `schema/tactic.schema.json` en
`sim/Zeus.Sim/TacticLoader.cs`. Breid dit uit met de volgende v0-aannames. De gebruiker heeft ze goedgekeurd als voorstel, je
hoeft er geen vragen over te stellen; leg ze vast in `docs/blokwissels.md`.

1. **Zonecodes** `[VMA]-(BL|HL|C|HR|BR)`: het eerste deel is de lengtezone van de gebeurtenis, gezien vanuit de ploeg die de
   trigger heeft: V = verdedigend derde (eigen doel), M = middenderde, A = aanvallend derde (doel van de tegenstander), op
   `x_rel` 0 tot 1/3, 1/3 tot 2/3, 2/3 tot 1 (zoals `BallRel` in `Match` dat berekent, ook na de kantwissel). Het tweede deel
   is de baan in de breedte, gezien in aanvalsrichting van die ploeg (y 0 = links): BL = buitenbaan links (y_rel < 0,2),
   HL = half links (0,2 tot 0,4), C = centraal (0,4 tot 0,6), HR = half rechts (0,6 tot 0,8), BR = buitenbaan rechts (> 0,8).
   Een trigger met zone gaat alleen af als de plaats van de gebeurtenis in die zone ligt. Zonder zone blijft het gedrag
   hetzelfde. Zet de grenzen in `TriggerConfig` (met `Validate()`), niet als losse getallen.
2. **`bal_terug_naar_middenvelder`** (fase `verdedigen`): de tegenstander speelt, op het moment van trappen, een pass naar een
   medespeler wiens rol in het rooster een middenvelder is (`controlerende_middenvelder`, `box_to_box`,
   `aanvallende_middenvelder`) waarbij de pass naar achteren gaat (vooruitcomponent in aanvalsrichting van de passer kleiner
   dan -0,3 van de passlengte). Plaats = beginpunt van de pass.
3. **`balverlies_in_eigen_helft`** (fase `balverlies`): de ploeg met de trigger verliest de bal (balbezitwissel naar de
   tegenstander) op de eigen helft (x_rel < 0,5). Plaats = plek van de wissel. De fase van de ploeg is dan `Balverlies`.
4. **`balwinst_in_hoge_zone`** (fase `balwinst`): de ploeg wint de bal op x_rel >= 2/3 (aanvallend derde). Plaats = plek van de
   wissel. De fase van de ploeg is dan `Balwinst`.
- Alleen de instructie `start_pressing` blijft uitgevoerd; andere instructies (zie de `instructie`-enum in het schema) blijven
  inactief en worden niet geïnterpreteerd. `IsSupported` staat de fasen `balverlies` en `balwinst` alleen toe voor de voorwaarden
  die erbij horen, elke voorwaarde in zijn eigen fase.
- Duur en gedrag blijven zoals nu (`PressSeconds`, tot eigen bezit, `TriggerFired`-event).

## Eisen
- Pure functies in `TriggerRules` voor de zone-toets en de voorwaarden, met unit-tests voor grenzen (inclusief spiegeling bij
  de kantwissel en voor de Away-ploeg).
- Wedstrijdtests: per nieuwe voorwaarde minstens één test dat hij afgaat (gebruik een eigen brein of een tactiekfragment in de
  test zoals bij `TriggerTests`; een extra testtactiek mag onder `tactics/test_triggers.json`).
- Werk de golden-hashes alleen bij als het standaardgedrag verandert (de bestaande tactieken gebruiken geen nieuwe
  voorwaarden, dus meestal niet). Documenteer in `docs/blokwissels.md`.

### TAAK T5
## Doel
Na de taken T1, T3 en T4 is het gedrag veranderd. Kalibreer de belangrijkste resterende afwijkingen van het symmetrische paar
(`laag_blok_442` tegen zichzelf, 8 seeds), in deze volgorde van belang, zonder de eerdere successen te breken (schoten 13,
doelpunten 1,3, uit-passes 2%):

1. Pass geslaagd% van ca. 66% naar richting 79% en onderschept% van ca. 24% naar richting 19,5%.
2. Inworpen van ca. 10 naar richting 23 per team (de bal gaat te zelden over de zijlijn).
3. Corners van ca. 4 naar richting 5,4; overtredingen naar 12,5; geel naar 1,58.
4. Passes zelf (aantal) naar richting 485 en de pass-succeskans per lengte (zie het einde van de uitvoer van `calibration_run.py`).

## Werkwijze
- Begin met een meting van de huidige stand (8 seeds) en leg die vast. Sweep met `--config` (zie `docs/kalibratie.md`); beoordeel
  elke variant op beide paren, het symmetrische en het ongelijke.
- Je mag instellingen aanpassen (`PassConfig`, `MatchConfig.InterceptControl`, `DuelConfig`, `ShotConfig`, ...). Je mag ook één kleine
  nieuwe aanname toevoegen (bijvoorbeeld ontvangersdruk of een afbuigkans op de zijlijn) als hij gedocumenteerd, getest en
  aanvaardbaar eenvoudig is. Geen grote nieuwe mechanismen.
- Zet de beste waarden als default, werk de golden-hashes bij, voeg "Kalibratieronde 4" toe aan `docs/kalibratie.md` met voor/na en
  de afwijkingen die blijven. Rapporteer eerlijk als een doel niet haalbaar bleek.

### TAAK T6
## Doel
Breid de Godot-weergave (`godot/`) uit met een debug-weergave, zodat een gebruiker een wedstrijd kan bekijken en begrijpen. De
weergave leest alleen snapshots en events uit de simulatiekern; er komt geen spellogica in Godot.

## Wat
- Toon per speler en team de fase (opbouw, aanval, balverlies, verdedigen, balwinst, stilstaand) uit de `PhaseChange`-events.
- Toon afgaande triggers (`TriggerFired`-events) kort in beeld met naam en speler, plus een lijst met de laatste events.
- Toon de passlijn (richting en beoogde ontvanger) van de laatste pass en markeer buitenspelposities (laatste lijn per ploeg).
- Schakelaars om de debug-lagen aan en uit te zetten (toets), standaard een nette weergave. Het afspelen van een replay moet
  blijven werken.
- Beschrijf het gebruik in `docs/godot-debug.md` (Nederlands).

## Eisen
- `dotnet build godot\Zeus.Godot.csproj` moet slagen zonder fouten; `dotnet test Zeus.sln` blijft groen.
- Als Godot zelf niet gestart kan worden in deze omgeving, verifieer dan met de build en met een korte beschrijving wat je niet
  kon uitproberen. Zeg dat eerlijk in je verslag.

### TAAK T7
## Doel
Sluit de nacht af: breng de documentatie in lijn met wat er die nacht gebeurd is. Dit is de laatste taak.

## Wat
- Lees `git log --oneline -30` en de commits met "Nachtwerker: taak ..." in de body, en de nieuwe docs (`docs/buitenspel.md`,
  `docs/godot-debug.md`, `docs/kalibratie.md`, `docs/blokwissels.md`, `docs/waarneming.md`).
- Werk `docs/hervatten.md` bij: sectie Stand (testaantal door zelf `dotnet test Zeus.sln` te draaien, laatste commits, fase), de
  open punten, en wat er nu in de simulatie zit. Houd `docs/hervatten.md` de enige bron voor testaantal en commit; kopieer die
  niet naar README.md of CLAUDE.md. Pas README.md en CLAUDE.md alleen aan waar iets feitelijk niet meer klopt.
- Controleer dat de claims in de docs (triggers, pressing, buitenspel, terugspelen, kalibratie) overeenkomen met de code; pas
  de docs aan, niet de code.
- Draai `dotnet test Zeus.sln` en vermeld het resultaat. Meld afwijkingen die je vond maar niet kon oplossen.
