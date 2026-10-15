# Kalibratie van de simulatiekern (aantallen)

Datum 2026-10-13. Doel: de aantallen per team per wedstrijd dichter bij de Premier League 2015/16 (StatsBomb, `calibration/event_models.json`, `calibration/pass_360_model.json` (de passmodellen hierin lekken de uitkomst; lekvrij: `event_models_v2.json` en `pass_360_model_v2.json`), `data/reference/`) brengen. **Dit is kalibratie van aantallen op één tactiekenpaar, geen bewijs van voetbalrealisme.** Bronnen en definities: vaultnotitie `Referentie wedstrijdevents` en `Kansmodellen uit data`.

## Meetopzet

`scripts\calibration_run.py` draait N seeds (standaard 8, `laag_blok_442` thuis tegen `man_om_man_433` uit), schrijft events naar `data/cal/` en print gemiddelden per team naast de doelen, plus thuis/uit apart, passuitkomst per bedoelde lengte en doelpunt per schotafstand:

```powershell
.\.venv\Scripts\python.exe scripts\calibration_run.py --build --label naam --seeds 8
```

Definities in het script: pass geslaagd = eerstvolgende balcontact is de bedoelde ontvanger (event `Pass` heeft nu `to <id> <bedoelde lengte in m>`); onderschept = eerstvolgend contact is van de tegenstander (events Interception of BallControl); uit = bal buiten, buitenspel, doelpunt of save; de rest is een contact van een andere medespeler. Balbezitswissels = events `PossessionChange` van beide teams samen (dit telt ook keepersaves en gewonnen duels). Duelpogingen = events `Duel`.

## Resultaten per stap (gemiddelde per team, doel in kolom 2)

De kolommen zijn cumulatief: elke stap bouwt op de vorige. Basis en stap 1 en 3 zijn 8 seeds, stap 2 is 32 seeds, eindstand 8 seeds (met 40 seeds: passes 487, geslaagd 82,0%, onderschept 15,0%, wissels 199, duelpogingen 33,4, overtredingen 11,4, geel 1,23, rood+2e geel 0,12, schoten 16,3, doelpunten 1,8, 11,6% per schot).

| maat | doel | basis | na 1 | na 2 | na 3 | na 4 (eind) |
|---|---:|---:|---:|---:|---:|---:|
| passes | 485 | 790 | 372 | 380 | 596 | 485 |
| pass geslaagd % | 79 | 39,3 | 32,1 | 32,2 | 82,2 | 81,2 |
| pass onderschept % | 17-22 | 42,8 | 53,5 | 53,9 | 15,8 | 15,9 |
| pass uit % | 2 | 4,0 | 3,8 | 3,5 | 0,5 | 0,4 |
| balbezitswissels (beide teams) | 188 | 1270 | 438 | 448 | 240 | 207 |
| duelpogingen | 33 | 718 | 35,5 | 34,3 | 15,1 | 33,8 |
| duel: overtreding % | 38 | 8,3 | 32,8 | 35,3 | 30,7 | 32,2 |
| duel: balwinst % | 37 | 31,5 | 35,6 | 35,8 | 31,5 | 38,2 |
| duel: losse bal % | 10 | 8,9 | 11,4 | 9,5 | 10,0 | 8,9 |
| overtredingen | 12,5 | 59,8 | 11,6 | 12,1 | 4,6 | 10,9 |
| gele kaarten (incl. 2e geel) | 1,58 | 0 | 0 | 1,0 | 1,3 | 1,7 |
| rood + 2e geel | 0,08 | 0 | 0 | 0,12 | 0,8 | 0,12 |
| schoten | 13 | 11,4 | 5,3 | 5,3 | 21,5 | 16,3 |
| doelpunten | 1,3 | 4,9 | 1,9 | 2,1 | 2,3 | 2,0 |
| doelpunt per schot % | 10 | 42,9 | 37,7 | 40,8 | 11,6 | 12,7 |
| buitenspel | 1,8 | 9,4 | 4,8 | 4,4 | 1,4 | 0,5 |
| inworpen | 23 | 22,1 | 9,3 | 8,9 | 1,0 | 1,2 |
| corners | 5,4 | 0 | 0 | 0 | 0 | 0 |

Eindstand per team (8 seeds): thuis (laag blok) 678 passes en 1 schot, uit (man-om-man) 292 passes en 31,5 schoten. Het gemiddelde verbergt dus een zeer ongelijke wedstrijd.

### Stap 1 - duelpogingen (`DuelConfig`, `Match.Rules.cs ResolveDuel`)

Per 'kansseconde' (pressende verdediger binnen `Reach` 1,2 m, cooldown 1 s) wordt nu met kans `AttemptRate` een duel geprobeerd; daarna de bestaande categorische uitkomst. Parameters (alle met opmerking in de code): `AttemptRate` 0,029 (basis: elke kans werd een duel), `BaseFoul` 0,34, `MaxFoul` 0,6, `SpeedFoul` 0,15, `ApproachFoul` 0,15, `IndisciplineFoul` 0,20, `FatigueFoul` 0,10, `BaseWin` 0,50, `SkillWin` 0,60, `LooseChance` 0,45. De uitkomstenmix (38/37/10/rest) is hiermee ca. 32/38/9. Belangrijke bevinding: de 718 duelpogingen waren geen druk maar een **stilstand**: een gepresste baldrager vond geen pass (risicoscore <= 0, `Scan` zonder beweging) en wachtte tot een duel de bal losmaakte (60% van de wedstrijdtijd). Na stap 1 daalden de passes daarom naar 372; stap 3 verhelpt dat.

### Stap 2 - kaarten (`DuelModel.ClassifyConduct`)

De duelroute kiest de gedragsclassificatie nu deterministisch uit de feiten van het duel: score = 0,5 x (sluitsnelheid / 8 m/s) + 0,5 x (|aanloophoek| / pi). Daarna `Reckless` (geel) vanaf een drempel per veldderde van de dader (eigen derde `RecklessOwnThird` 0,46, midden `RecklessMiddle` 0,737, aanvallend `RecklessAttacking` 0,75), `ExcessiveForce` (rood) vanaf `ExcessiveForceScore` 0,787, anders `Careless`. Er is geen RNG-trekking; `Referee.Assess` blijft de regelset en bepaalt kaart en hervatting. De drempels zijn kwantielen van de gemeten scoreverdeling (ca. 11% geel, ca. 0,3-0,5% direct rood per overtreding) en moeten bij elke wijziging aan duelgedrag opnieuw worden bepaald: de verdeling is erg klonterig (veel achtervolgingen van achteren). Resultaat bij 40 seeds (911 overtredingen): 10,8% geel (incl. 9 tweede gele), 0,1% direct rood, 1,0% tweede geel.

### Stap 3 - passen en onderscheppingen

Oorzaken van 43-55% onderschepping in de basis, in volgorde van invloed:
1. **Aanrakingsradius bij de trap**: een pressende tegenstander op < 1 m van de passer raakte de bal op het trapmoment (85% van de onderscheppingen op afstand 0 en tijd 0). Nu `MatchConfig.KickShieldMeters` 2,5: een tegenstander mag een pass/schot pas raken als die 2,5 m van de trapplek is.
2. **Onderschepvermogen los van ontvangerscontrole**: `MatchConfig.InterceptControl` (veldspelers van het team dat niet het laatst raakte): straal 2,0 m, hoogte 0,3 m, max relatieve snelheid 18 m/s (default `InterceptSettings`).
3. **Ontvanger liep niet naar de bal**; de dichtstbijzijnde medespeler van de passer jaagde op de bal. Nu `Perception.ReceiveTarget` en `TeamPassInFlight`: alleen de bedoelde ontvanger gaat naar het mikpunt, de rest houdt positie.
4. **Verdedigers sneden de baan niet af**: `PassConfig.LaneCutRunSpeed` 6,5, `LaneCutReactionSeconds` 0,4, `LaneCutMinBallSpeed` 4: de best geplaatste verdediger gaat naar het punt op de baan dat hij voor de bal haalt.
5. **Passkeuze**: opponenten binnen `LaneStartMeters` 2 m van de passer tellen niet als baanrisico; weging `ForwardWeight` 0,06, `DistanceWeight` 0,015, `RiskWeight` 0,4 (was 0,025/0,015/1,2); ontvanger die buitenspel lijkt (voorbij de meest gevorderde zichtbare veldspeler min `OffsideMargin` 0,5) wordt gemeden; na `HoldSeconds` + `ForceSeconds` (2,0) wordt toch de beste zichtbare optie gespeeld.
6. **Pass-tempo**: `HoldSeconds` 0,8 -> 3,0 (de baldrager houdt de bal even vast); passes 485 per team.

De pass-succeskans per lengte klopt **niet**: 10-20 m geeft 98% (doel 88-90%), 30-40 m 44% (doel 66%). Het totaal (81% geslaagd, 15,9% onderschept) klopt wel. De verdeling per lengte volgt uit de afstanden in de opstellingen (veel 10-20 m in de eigen lijn, 20-40 m door verdedigingslijnen) en niet uit een uitvoeringsfout; een verdeling per aantal tegenstanders in de passlijn (`pass_360_model`: 0 tegenstanders 94%) is niet getoetst omdat de events de positie van tegenstanders op het trapmoment niet bevatten.

### Stap 4 - balverlies en schoten (`Shooting.cs`, `MatchConfig.KeeperSettings`)

Schotbeslissing: schieten als de geschatte doelpuntkans (logistisch model uit `schot_doelpunt`, afstand en doelhoek, voetschot zonder druk) >= `ShotConfig.MinGoalProbability` 0,08; tot dat moment (binnen `DribbleMaxGoalDistance` 35 m) loopt de baldrager richting doel tot `DribbleStopGoalDistance` 11 m. Uitvoering: richtingsfout (driehoeksruis) = (`AngleErrorBase` 1,25 + `AngleErrorPerMeter` 0,0) x (1 - vaardigheid 0,75) rad; snelheid 26 m/s +- 15%. Keeper: `KeeperSettings` straal 2,5 m, hoogte 2,2 m, 35 m/s (was 1,0 / 1,8). Resultaat 10-16 m: 13,5% (doel 12,6%); overall 12,7% (doel ca. 10%). 0-10 m, 16-30 m hebben in deze tactieken te weinig schoten (n < 20) om te toetsen. Zonder dribbelen kwamen alle schoten van 22-30 m (formatielijn), met 21-43% doelpunt per schot.

## Wat nog afwijkt (eerlijk)

- **Ongelijke wedstrijd**: laag blok schiet ca. 1x, man-om-man 31x per wedstrijd; met een symmetrisch paar (8 seeds) zijn de schoten 0-5 per team, geslaagd 66-70% en wissels ca. 320. De kalibratie geldt dus voor dit ene paar; het gemiddelde van twee teams (16 schoten, 2,0 doelpunten) is gunstiger dan elk team apart.
- Inworpen 1,2 (doel 23), corners 0 (doel 5,4; keepersaves leiden nooit tot hoekschop), buitenspel 0,5 (doel 1,8), uitballen 0,4% (doel 2%): de bal verlaat het veld te zelden; niet gekalibreerd (geen stap hiervoor).
- **Passdoelen per lengte gecorrigeerd (2026-10-13):** de doelen 46,8 / 88,4 / 90,0 / 88,1 / 82,0 / 66,5 / 43,5% kwamen uit het oude passmodel op de werkelijke baan van de bal en bevatten uitkomst-lekkage. `calibration_run.py` gebruikt nu de lekvrije tabel op bedoelde lengte uit `calibration/event_models_v2.json` (90,1 / 90,3 / 90,1 / 89,1 / 85,0 / 73,7 / 55,1%; licht optimistische bovengrens omdat bij ca. 26% van de mislukte passes de beoogde ontvanger ontbreekt). Daarmee is de afwijking groter dan eerder gemeld: 20-30 m sim ca. 77% tegen 85%, 30-40 m ca. 40% tegen 74%, en passes < 10 m en > 40 m komen in de simulatie vrijwel niet voor.
- Pass-succes per lengte (zie boven), kortste passes (< 10 m) komen bijna niet voor (n < 30 van 6500), terwijl dit in de data 24% is.
- Balbezitswissels 207 (doel 188), onderschept 15,9% (net onder 17-22%), tweede geel/rood 0,12 (doel 0,08) door herhaaldelijke fouten van dezelfde dichtstbijzijnde verdediger (de pressing kiest steeds één speler); fouten in de eigen derde 7% (data 20%), aanvallend derde 3% (data 27%): de plaats van de fouten klopt niet met de data.
- Duelpogingen 33,8 klopt in totaal, maar thuis 17 en uit 51 per team (pressing hangt van tactiek af).
- Kaartdrempels zijn kwantielen van een klonterige verdeling, geen regeluitleg; SPA/DOGSO blijven onbeoordeeld.

## Hermeting na de aftrapfix (commit 35547f5, 8 seeds, zelfde tactiekpaar)

De eindstand in de tabel hierboven is gemeten vóór de aftrapfix (aftrapnemer volgt kantwissel). Opnieuw gemeten met `calibration_run.py --label verificatie --seeds 8`; alle 8 wedstrijden bereikten `Finished` (de runner geeft anders exit 2 en het script stopt). Gemiddelde per team: passes 487, geslaagd 82,0%, onderschept 15,1%, wissels 203, duelpogingen 33,1, overtredingen 11,4, geel 1,62, rood+2e geel 0,38 (doel 0,08), schoten 18,9 (doel 13, p10-p90 7-20), doelpunten 2,0 (doel 1,3), 10,9% per schot. Corners 0, inworpen 0,6, buitenspel 0,3. De schoten en het rood liggen dus hoger dan de tabel; de rest is gelijk. Nog geen nieuwe kalibratiestap gedaan: de afwijkingen onder "Wat nog afwijkt" gelden onverkort.

## Kalibratieronde 2 (2026-10-13): oprukken, wegwerptrap, wegtippen

Aanleiding: bij een symmetrisch paar (`laag_blok_442` tegen zichzelf) kwam het spel nooit verder dan 22 m van de middenlijn en waren er 0 schoten. De spitsen speelden ~130 keer per wedstrijd onderling (75% mislukt): niemand liep met de bal vooruit, tenzij binnen 35 m van het goal. Gemeten met `scripts\calibration_run.py --config <json>` (nieuw: gedeeltelijke `MatchConfig`-overrides, ook in `--replay-out` opgeslagen).

Nieuwe of gewijzigde instellingen (standaard in de code):

| instelling | was | nu | doel |
|---|---:|---:|---|
| `PassConfig.CarryMaxGoalDistance` / `CarryClearMeters` / `CarryConeRadians` | 0 (uit) | 45 m / 7 m / 0,6 rad | baldrager loopt vooruit als er in de kegel naar het goal geen tegenstander binnen 7 m staat |
| `PassConfig.ClearPressureMeters` / `ClearZoneMeters` / `ClearBelowScore` | n.v.t. | 6 m / 37 m / 0,9 | nieuwe `IntentKind.Clear`: onder druk in het eigen derde zonder goede pass lang en breed wegschieten (event `Pass`, uitkomst `clear`) |
| `PassConfig.AngleErrorRadians` | 0,06 | 0,12 | meer passen over de lijn (uit-passes 0,4% naar ca. 2%) |
| `PassConfig.RiskWeight` | 0,4 | 0,9 | passer mijdt betwiste banen sterker |
| `MatchConfig.InterceptControl` | straal 2,0 m, 18 m/s | 1,2 m, 9 m/s | tegenstander pakt een bal minder gemakkelijk dan de ontvanger |
| `ShotConfig.MinGoalProbability` | 0,08 | 0,125 | minder schoten uit slechte posities, nu men verder oprukt |
| `ShotConfig.ParryCornerRate` | n.v.t. | 0,35 | aandeel van de reddingen dat de keeper achter de lijn tipt: hoekschop |
| `DuelConfig.AttemptRate` | 0,029 | 0,05 | meer duelpogingen, nu spelers meer dribbelen |
| `PerceptionConfig.AllRoundMeters` | n.v.t. | 0 (uit) | gemeten (12 en 25 m), gaf geen verbetering; blijft beschikbaar |

Resultaat, 8 seeds, gemiddelde per team (na pressing uit eigen waarneming, triggers en deze wegwerp-instelling). **Symmetrisch paar** (`laag_blok_442` thuis en uit) is het referentiepaar, omdat het geen tactische scheefheid bevat. Een wegwerptrap telt hier niet als pass (in StatsBomb is dat een Clearance); `calibration_run.py` telt ze apart.

| maat | doel | symmetrisch | ongelijk paar (laag blok thuis, man-om-man uit) |
|---|---:|---:|---:|
| passes (zonder wegwerptrappen) | 485 | 383 | 411 |
| wegwerptrappen | niet gemeten | 21,5 | 7,9 |
| pass geslaagd % | 79 | 66,4 | 73,0 |
| onderschept % | 19,5 | 24,4 | 16,4 |
| uit % | 2 | 1,9 | 1,8 |
| duelpogingen | 33 | 29,0 | 29,2 |
| overtredingen | 12,5 | 9,6 | 10,1 |
| geel | 1,58 | 1,19 | 1,00 |
| schoten | 13 | 13,0 | 54,8 (laag blok 18,6, man-om-man 90,9) |
| doelpunten | 1,3 | 1,31 | 6,8 |
| doelpunt per schot % | 10 | 8,7 | 12,6 |
| inworpen | 23 | 10,4 | 7,9 |
| corners | 5,4 | 3,9 | 11,7 |
| buitenspel | 1,8 | 0,0 | 0,0 |

Eerlijke afwijkingen die blijven:
- **Ongelijk paar**: de man-om-man-ploeg overspeelt het lage blok (92 tegen 26 schoten, 8,6 doelpunten). Dat is een tactisch effect van deze twee bestanden (de ene ploeg dekt overal man-op-man en wordt niet doorgespeeld), geen kalibratiefout; er is geen data om het uitgangspunt "gelijke ploegen" te toetsen.
- **Buitenspel** blijft ongeveer 0: aanvallers lopen niet voorbij de laatste verdediger; `OffsideMargin` -3 gaf het ook niet. Er is geen diepteloop of steekpass.
- **Inworpen** 10 tegen 23: de bal gaat nog te zelden over de zijlijn. Spelers krijgen geen pass vanaf de buitenste ~6 m; ook is er geen afbuiging van de bal tegen spelers. De wegwerptrap (21 per team) en uit-passes (1,9%) leveren de rest.
- **Corners** 3,9 tegen 5,4 (symmetrisch), maar 11,7 in het ongelijke paar (veel schoten van één ploeg).
- **Passsucces** 66% tegen 79%, onderschept 24% tegen 19,5% (symmetrisch; ongelijk paar 73% en 16%): de extra uit-passes kosten succes en de ploegen delen dezelfde instellingen. Passes van 20-30 m slagen ca. 65% tegen 85%; 30-40 m 62-70% tegen 74% (het oude gat van 40% tegen 74% is dus verkleind); passes < 10 m en > 40 m komen nog nauwelijks voor.
- **Doelpunt per schot** 8,7% tegen 10% in het symmetrische paar (ongelijk paar 12,6%).
- Er is geen gekalibreerde variatie per speler of tactiek; alle spelers delen dezelfde instellingen.

## Aangepaste bestaande tests

Drie tests legden oude ongekalibreerde waarden vast en zijn minimaal aangepast: `DuelModelTests.ChancesAreBounded...` (bovengrens foul is nu `DuelConfig.Default.MaxFoul` i.p.v. 0,35); `RestartTests` (config met `AttemptRate: 1` zodat elk duel direct een overtreding is); `PassingTests` (`ChoosesOnlyVisibleTeammate...`: de tegenstander staat op x=12 i.p.v. 8 zodat de ontvanger niet buitenspel lijkt; `NoPassOptionTriggersScanning...`: `HeldBallSeconds = 5` omdat de standaard `HoldSeconds` nu 3,0 is). Nieuwe tests in `CalibrationTests.cs` (21): parameters, validatie, determinisme, conduct, buitenspelmijding, trapschild, ontvanger en baan afsnijden, schotmodel en keeperbereik.

## Kalibratieronde 3: buitenspel

Doel: het symmetrische paar (`laag_blok_442` tegen zichzelf) op ca. 1,8 buitenspel per team per wedstrijd (acceptabel 1,2 tot 2,6), zonder dat andere maten verslechteren. Uitgangspunt na T1 (diepteloop, steekpas, lijnruis, zie `docs/buitenspel.md`): 0,4.

Sweep met `calibration_run.py --config` (4 en 16 seeds, één variabele tegelijk en combinaties). Buitenspel is zeldzaam (8 teamwedstrijden à 0,5 = 4 events), dus 4 seeds zijn te ruw; keuzes zijn op 16 seeds gemaakt.

| variant (16 seeds, symmetrisch) | buitenspel | schoten |
|---|---:|---:|
| T1-defaults | 0,44 | 19,6 |
| `LineMisjudgementMeters` 2,0 | 0,53 | 18,9 |
| `RunCooldownSeconds` 30 | 0,47 | 19,5 |
| `RunDepthMeters` 3,0 + cooldown 30 | 1,12 | 19,3 |
| `RunDepthMeters` 3,0 + cooldown 30 + ruis 2,0 | 0,84 | 19,3 |
| `RunDepthMeters` 4,5 + cooldown 30 + ruis 2,0 + `RunStartDistanceMeters` 20 | 1,62 | 27,8 (te veel) |
| `PassConfig.OffsideMargin` -1,5 (alleen dit) | 1,91 | 18,2 |
| `PassConfig.OffsideMargin` -2,0 | 1,75 | 19,5 |

Waargenomen: de T1-instellingen hebben weinig hefboom. `ThroughBallBonus` 2,0 gaf een identieke wedstrijd (de bonus beslist nooit tussen opties), `MinRunSpeed` geen verschil, en een grotere `RunStartDistanceMeters`/`RunSeconds` laat de schoten exploderen (50-60 per team) zonder veel meer buitenspel. De dieptelopen worden bovendien bij het trappen opnieuw getoetst (`CheckOffsideLine`), zodat een fout alleen via lijnruis ontstaat. De bestaande `PassConfig.OffsideMargin` is de effectieve knop.

Gekozen: alleen `PassConfig.OffsideMargin` van 0,5 naar **-1,5** (v0-aanname: een passer accepteert een doel tot 1,5 m voorbij de geschatte lijn en gokt daarmee op de timing van de loper). De T1-parameters blijven op hun defaults.

Resultaat, 8 seeds, gemiddelde per team:

| maat | doel | symmetrisch voor | symmetrisch na | ongelijk paar voor | ongelijk paar na |
|---|---:|---:|---:|---:|---:|
| buitenspel | 1,8 | 0,38 | 2,19 | 1,00 | 2,25 |
| schoten | 13 | 18,9 | 18,3 | 40,7 | 42,3 |
| doelpunten | 1,3 | 2,4 | 2,1 | 4,75 | 6,0 |
| passes | 485 | 430,6 | 432,3 | 423,5 | 405,9 |
| pass geslaagd % | 79 | 64,1 | 63,1 | 77,0 | 76,2 |
| onderschept % | 19,5 | 27,9 | 29,3 | 11,9 | 12,7 |
| uit % | 2 | 1,8 | 2,0 | 2,5 | 2,7 |
| overtredingen | 12,5 | 7,9 | 6,5 | 11,3 | 8,3 |
| inworpen | 23 | 8,4 | 7,4 | 9,4 | 8,4 |
| corners | 5,4 | 4,2 | 3,6 | 9,4 | 9,8 |

Eerlijke afwijkingen: het symmetrische paar zit op 2,2 (16 seeds: 1,9), binnen 1,2-2,6 maar boven 1,8; het verschil in andere maten ligt binnen de seed-ruis, maar het verschil in overtredingen (7,9 naar 6,5) is mogelijk niet alleen ruis. Schoten (18-19), passsucces (63%), onderschept (29%), inworpen en overtredingen blijven ver van de doelen; dat waren al open punten en zijn hier niet aangepakt. De buitenspelbreedte is een aanname, niet gemeten tegen echte data; het ongelijke paar gaf bij 8 seeds ook 2,25 buitenspel maar de tegenstander-tactiek verschilt sterk.

Aangepaste tests: `AttackingRunTests` en `DisabledBackpassesRetainThePreT3FullMatchHashes` geven nu `OffsideMargin: 0.5` expliciet mee (zij testen T1/T3-gedrag onder de oude marge); nieuwe test `DefaultOffsideMarginAcceptsATargetSlightlyBeyondTheLineButNotFarBeyondIt`. Beide golden-hashes zijn bijgewerkt.

## Kalibratieronde 4: passsucces, inworpen, overtredingen en kaarten

Doel: het symmetrische paar (`laag_blok_442` tegen zichzelf, 8 seeds) dichter bij de doelen voor passsucces/onderscheppen, inworpen, overtredingen en geel, zonder de schoten (13) en uit-passes (2%) te verliezen. Geen nieuw mechanisme: alleen defaults aangepast (v0-aannames, alle gekalibreerd op aantallen). Sweep met `calibration_run.py --config`, per variant 8 seeds; steeds beide paren getoetst voor de eindkeuze.

Gewijzigde defaults:

| instelling | was | nu | reden |
|---|---:|---:|---|
| `MatchConfig.InterceptControl` (`ControlRadius`) | 1,2 m | 0,6 m | tegenstander pakt een bal alleen vlak bij zijn voeten; onderschept 29% naar 14% |
| `PassConfig.HoldSeconds` | 3,0 | 2,4 | de baldrager speelt eerder; minder tijd voor druk, hogere passtelling en -succes (zeer gevoelig: 2,0 geeft 86% en 0,9 schoten) |
| `PassConfig.ClearZoneMeters` | 37 | 45 | meer wegwerptrappen onder druk; dat levert de inworpen (bal gaat via clears en wegtrappen over de zijlijn) |
| `ShotConfig.MinGoalProbability` | 0,125 | 0,08 | met snellere passes komt het spel minder ver; een lagere drempel houdt de schoten op ca. 13 |
| `DuelConfig.AttemptRate` / `BaseFoul` | 0,05 / 0,34 | 0,065 / 0,40 | meer duelpogingen en overtredingen |
| `DuelConfig.RecklessOwnThird` / `RecklessMiddle` / `RecklessAttacking` | 0,46 / 0,737 / 0,75 | 0,44 / 0,70 / 0,70 | de kaartdrempels zijn kwantielen van de duelscore en moesten na deze wijziging opnieuw omlaag (zie stap 2) |

Resultaat, gemiddelde per team, 8 seeds:

| maat | doel | symmetrisch voor | symmetrisch na | ongelijk paar voor | ongelijk paar na |
|---|---:|---:|---:|---:|---:|
| passes | 485 | 432 | 466 | 406 | 386 |
| pass geslaagd % | 79 | 63,1 | 75,0 | 76,2 | 75,6 |
| onderschept % | 19,5 | 29,3 | 14,2 | 12,7 | 10,6 |
| uit % | 2 | 2,0 | 1,7 | 2,7 | 1,2 |
| duelpogingen | 33 | 23,3 | 31,3 | 30,9 | 28,7 |
| overtredingen | 12,5 | 6,5 | 10,2 | 8,3 | 10,4 |
| geel | 1,58 | 0,44 | 0,62 | 1,25 | 1,75 |
| schoten | 13 | 18,3 | 12,4 | 42,3 | 37,8 |
| doelpunten | 1,3 | 2,12 | 0,94 | 6,0 | 4,9 |
| inworpen | 23 | 7,4 | 18,7 | 8,4 | 16,0 |
| corners | 5,4 | 3,6 | 2,8 | 9,8 | 8,6 |
| buitenspel | 1,8 | 2,2 | 0,7 | 2,3 | 1,2 |
| wegwerptrappen | niet gemeten | 11,7 | 37,4 | 2,1 | 20,9 |

Passsucces per bedoelde lengte (symmetrisch, na): 10-15 m 95% (doel 90), 15-20 m 81% (89), 20-30 m 65% (85), 30-40 m 70% (74); < 10 m en > 40 m komen nog nauwelijks voor.

Wat niet lukte of afwijkt:
- **Onderschept** zakt onder het doel (14% tegen 17-22%) terwijl geslaagd (75%) nog onder 79% blijft; de rest (ca. 9%) is een contact van een andere medespeler. De passer kiest dus te vaak een ontvanger die een andere medespeler eerst raakt; niet nader onderzocht. Een grotere `InterceptControl.ControlRadius` (0,8-0,9) geeft meer onderschepping maar minder schoten; de gekozen combinatie is een compromis, geen unieke oplossing.
- **Inworpen** 18,7 tegen 23: bijna alleen via de 37 wegwerptrappen per team (doel in data ca. 20-25 clearances; dit is dus aan de hoge kant). Er is geen balverlies door slechte aanname of afbuiging; aanraking van de bal door een speler levert altijd volledige controle. Dit is een aanname-gedreven telling, geen realistische oorzaak.
- **Corners** 2,8 tegen 5,4 (ongelijk paar 8,6): niet gekalibreerd (hangt aan het aantal schoten en `ShotConfig.ParryCornerRate`).
- **Doelpunten** 0,94 tegen 1,3 (5,5% per schot, doel 10%) bij 8 seeds: onder het doel; een lagere schotdrempel haalt wel meer schoten maar niet meer kwaliteit. Buitenspel 0,7 tegen 1,8: de kalibratie van ronde 3 (zie boven) verschoof door de snellere passes.
- **Geel** 0,62 tegen 1,58 in het symmetrische paar (1,75 in het ongelijke): de kaartdrempels zijn gekalibreerd op het ongelijke paar en op één verdeling van de duelscore; ze zijn gevoelig (0,55 gaf 3,5 geel).
- Passes per team 466 tegen 485; wegwerptrappen zitten er niet in.
- Eerlijkheid: alle waarden zijn op 8 seeds gekozen; kleine verschillen (bijv. in schoten en doelpunten) zijn ruis.

Aangepaste tests: `GoldenMatchTests` (nieuwe hashes), `DefaultDuelMix...` (nieuwe verwachte kansen bij `BaseFoul` 0,40). Tests die een historische hash vastleggen of tegenstanders dicht bij de trapplek nodig hebben (`KeeperBackPassTests`, `AttackingRunTests`, `KickShield...`) gebruiken `PreRound4Defaults` (testhulp met de oude defaults) zodat ze hun eigen onderwerp blijven toetsen. Nieuw: `Round4DefaultsAreValidAndMatchTheDocumentedCalibration`.

## Open punt: rondomzicht (documentcontrole 2026-10-14)

De tabel van ronde 2 zet `PerceptionConfig.AllRoundMeters` op 0 (uit). De huidige standaard in de code is **25 m** (rondomzicht op spelers, zie `docs/waarneming.md`), en de codecommentaar verwijst daarvoor naar een meting van ronde 3 op zes paren. Die meting staat niet in een tabel van dit document. Tot die is vastgelegd, is de 25 m een v0-aanname zonder hier beschreven onderbouwing. Gemeten op 2026-10-14, zie de sectie hieronder: de 25 m blijft.

## Kalibratieronde 5: buitenspel

Doel: buitenspel terug naar ca. 1,8 per team per wedstrijd. Na de passnelheid- en ronde-4-wijzigingen was het gezakt naar 0,7-0,8 (symmetrisch paar `laag_blok_442` tegen zichzelf, zie ronde 4).

Sweep met `calibration_run.py --config`:

| variant | buitenspel |
|---|---:|
| `PassConfig.OffsideMargin` alleen, -1,5 tot -6 | max. 0,8-1,3 |
| marge -3 + `RunDepthMeters` 3,0 + `RunCooldownSeconds` 30 + `ThroughBallBonus` 1,6 (16 seeds) | 1,69 |

De marge alleen haalt het doel niet; pas de combinatie met diepere en vaker gestarte dieptelopen en een zwaardere steekpasbonus doet dat.

Gekozen defaults: `PassConfig.OffsideMargin` -1,5 naar **-3,0**, `AttackingRunConfig.RunDepthMeters` 1,5 naar **3,0**, `ThroughBallBonus` 0,8 naar **1,6**, `RunCooldownSeconds` 60 naar **30**.

Resultaat (gemiddelde per team):

| maat | doel | symmetrisch voor | symmetrisch na (32 seeds) | ongelijk voor | ongelijk na (16 seeds) |
|---|---:|---:|---:|---:|---:|
| buitenspel | 1,8 | 0,7-0,8 | 1,59 | 1,44 | 2,03 |
| schoten | 13 | n.v.t. | 13,4 | 40,6 | 43,8 |
| passes | 485 | n.v.t. | 460 | n.v.t. | n.v.t. |
| pass geslaagd % | 79 | n.v.t. | 74,9 | n.v.t. | n.v.t. |
| onderschept % | 19,5 | n.v.t. | 14,2 | n.v.t. | n.v.t. |
| doelpunten | 1,3 | n.v.t. | 1,06 | n.v.t. | n.v.t. |

("n.v.t." = in deze ronde niet opnieuw vastgelegd.)

Eerlijke afwijkingen:
- Buitenspel 1,59 blijft onder het doel 1,8 (wel binnen de eerder gehanteerde 1,2 tot 2,6).
- De marge van -3 m is een v0-aanname zonder echte data: een passer accepteert een doel tot 3 m voorbij de geschatte lijn. Zo ruim is dat waarschijnlijk niet realistisch; het is de knop die het doel het dichtst benaderde.
- Het ongelijke paar (tegen `man_om_man_433`) heeft veel meer schoten (43,8 tegen doel 13); dat was al een open punt en is niet aangepakt.
- Doelpunten 1,06 liggen onder 1,3 en onderschept 14,2% onder 19,5%; ook niet aangepakt.

Aangepaste tests: tests met historische hashes of oude-geometrie-intentie (`KeeperBackPassTests.DisabledBackpassesRetainThePreT3FullMatchHashes`, `AttackingRunTests` (volledige-wedstrijdhash en looppasgeometrie), `PenaltyTests.DefaultCautionGivesFarFewerPenaltiesThanNoCaution`) gebruiken de testhulp `PreRound5Defaults` met de oude waarden; `CalibrationTests.DefaultOffsideMarginAcceptsATarget...` en `AttackingRunTests.SettingsHaveDefaultsValidateAndRoundTrip` toetsen de nieuwe defaults; nieuwe test `Round5OffsideDefaultsAreFixedAndValid`. Beide golden-hashes in `GoldenMatchTests` zijn opnieuw met de Release-runner gegenereerd.

## Meting rondomzicht (`PerceptionConfig.AllRoundMeters`), 2026-10-14

Doel: de standaard van 25 m onderbouwen, want de tabel van ronde 2 legt alleen 0 m vast. Opzet: symmetrisch paar (`laag_blok_442` thuis en uit), 16 seeds (1-16) per waarde, commit 21998ac, alle overige instellingen op hun standaard. Config per run: `{"Perception": {"AllRoundMeters": <waarde>}}` via `scripts\calibration_run.py --away tactics/laag_blok_442.json --seeds 16 --config <json>`. Gemiddelde per team per wedstrijd (de kopregel van het script noemt het uit-team nog `man_om_man_433`; dat is hier onjuist, beide teams zijn laag blok).

| maat | doel | 0 m | 10 m | 15 m | **25 m (standaard)** | 35 m |
|---|---:|---:|---:|---:|---:|---:|
| passes | 485 | 414 | 410 | 445 | 460 | 455 |
| pass geslaagd % | 79 | 72,0 | 71,0 | 75,3 | 75,6 | 71,6 |
| onderschept % | 17-22 | 14,4 | 13,3 | 14,4 | 13,9 | 14,4 |
| uit % | 2 | 2,7 | 3,7 | 1,9 | 1,8 | 3,5 |
| schoten | 13 | 5,7 | 6,2 | 7,4 | 12,1 | 20,3 |
| doelpunten | 1,3 | 0,34 | 0,56 | 0,47 | 0,88 | 1,41 |
| buitenspel | 1,8 | 0,00 | 0,00 | 0,00 | 0,81 | 3,56 |
| inworpen | 23 | 22,1 | 27,4 | 19,3 | 17,3 | 17,7 |
| corners | 5,4 | 1,7 | 1,5 | 1,9 | 2,8 | 4,6 |
| overtredingen | 12,5 | 11,5 | 12,2 | 10,3 | 10,3 | 9,8 |
| geel | 1,58 | 1,19 | 1,41 | 1,16 | 0,75 | 0,62 |

Waarneming per maat:
- **Schoten en buitenspel** lopen duidelijk op met de straal: schoten 5,7 / 6,2 / 7,4 / 12,1 / 20,3 en buitenspel 0 / 0 / 0 / 0,8 / 3,6 (0 m, 10 m, 15 m, 25 m, 35 m). Dit is het enige effect dat groter lijkt dan de seed-ruis. Het doel (13 schoten, 1,8 buitenspel) ligt tussen 25 m en 35 m; 25 m komt het dichtst bij de schoten (12,1), 35 m schiet erover (20,3) en geeft te veel buitenspel (3,6).
- **Passsucces** is 71-72% bij 0, 10 en 35 m en 75% bij 15 en 25 m: geen monotoon verband, dus waarschijnlijk ruis of een wisselwerking, geen aanwijzing voor een optimum. Onderschept is overal 13-14%, onder het doel.
- Inworpen, overtredingen en geel verschillen per waarde, maar zonder eenduidig patroon; geel 0,6-1,4 is bij 16 seeds (ca. 11-22 kaarten per run) niet te onderscheiden van ruis.
- Doelpunten volgen de schoten (0,34 tot 1,41), met ca. 3-6% per schot onder het doel van 10%.

Eerlijke onzekerheid: 16 seeds per waarde, één tactiekpaar, één commit. Zeldzame events (buitenspel, geel, doelpunten, rood) hebben grote ruis; verschillen van enkele tienden daarin zijn niet te duiden. Er is geen replicatie met andere seeds en geen betrouwbaarheidsinterval berekend. De 25 m is gekozen omdat die schoten (12,1 tegen 13) en buitenspel (0,8 tegen 1,8) het dichtst bij de doelen brengt, niet omdat 25 m fysiologisch of tactisch bewezen is. Het zou ook 20 of 30 m kunnen zijn; die zijn niet gemeten. De doelen gelden voor het spel van twee gelijke ploegen en een ander tactiekpaar kan anders reageren (ronde 2 meldde 12 en 25 m zonder verbetering, op een ongelijk paar en vóór latere rondes).

Besluit: **`AllRoundMeters` blijft op 25 m.** Het is een kalibratie-aanname op aantallen (schoten en buitenspel), geen realisme-claim. Het open punt "rondomzicht" is hiermee onderbouwd door deze meting; de verwijzing naar de "zes paren van ronde 3" in de codecommentaar is niet gecontroleerd. Een eventuele fijnere afstelling (20 of 30 m) en een controle op een ongelijk paar zijn open.

## Ronde 6, deel 1: man-dekking (2026-10-15)

**Aanleiding.** Een brede meting over twaalf paren van de zes tactieken (`scripts/calibration_pairs.py`, standaard 12 paren) liet zien dat de man-om-man-tactiek tegen zichzelf (MM) de wedstrijd bijna stillegt: gemiddeld 24,6 passes en 4,3 schoten per team per wedstrijd (referentie 347-625 en 7-20), en dat in een paar met een testtactiek twee wedstrijden werden gestaakt (minder dan zeven spelers).

**Oorzaak (ablatie, 5 seeds, paar MM en TT).** Met alle instructies uit geeft MM 494 passes en 20 schoten; met alleen `EnableMark=false` 495 passes en 24 schoten. De man-dekking (`dek_man`) is dus de oorzaak: elke verdediger stond op 1,5 m achter zijn man, over het hele veld, ook ver van de bal. Als beide teams dat doen, krijgt niemand ruimte om te passen. De hoge overtredingen van `testcase_a_433` (ca. 22-24 per team) blijven bestaan met alle instructies uit en komen dus uit de basisinstellingen van pressing en duels, niet uit de instructies.

**Wijziging (`InstructionConfig`, alle aannames, ongekalibreerd).**
- `MarkBallRadius = 15` (nieuw): een man wordt alleen dichtbij gedekt als hij binnen 15 m van de bal is (bal-georiënteerd dekken, zoals echte man-dekking); verder weg houdt de dekker zijn formatiepositie.
- `MarkGap` 1,5 → 8 m en `MarkRadius` 18 → 12 m. Een sweep (gap 4,5 / 6 / 8 m) gaf voor MM 83 / 129 / 245 passes en 1,9 / 4,1 / 9,0 overtredingen per team: de gewenste richting, maar met 8 m is `dek_man` bewust los totdat het drukmodel voor passkeuze is gekalibreerd. Dit is dus geen realistische 'strakke' dekking.

**Meting voor en na** (alle waarden per team per wedstrijd; 'voor' = 12 seeds op de uitgangssituatie van de instructie-commit, 'na' = 8 seeds, andere seeds dus indicatief):

| paar | schoten voor → na | passes voor → na | overtredingen voor → na |
|---|---:|---:|---:|
| MM (man tegen man) | 4,3 → 10,9 | 24,6 → 221,4 | 0,8 → 7,9 |
| LM (laag blok thuis, man uit) | 27,5 → 20,8 | 237,8 → 216,9 | 5,9 → 5,5 |
| ML | 26,5 → 25,2 | 270,7 → 315,0 | 6,4 → 8,6 |
| TT (testcase A) | 14,4 → 13,9 | 582,6 → 583,5 | 22,7 → 23,8 |
| TH | 18-21 | 453 → 472 | 18,7 → 19,4 |
| gestaakte wedstrijden | 2 | | 0 |

Aangepaste tests: de geometrietests van de dekking (`InstructionTests`) en `CalibrationTests.InterceptControl...` leggen de oorspronkelijke strakke dekking expliciet vast (`MarkRadius 18, MarkGap 1,5, MarkBallRadius 1000`); de golden-hash van `laag_blok_442` tegen `man_om_man_433` (seed 42) is **bewust vernieuwd** omdat het gedrag van `dek_man` bewust veranderde (nieuwe event-hash `fe463a1d…`, snapshot-hash `a4337a6f…`).

**Wat nog afwijkt** (voor deel 2): `hoge_druk_433` produceert 34 schoten per team (HH, HC, CH 34-49) en weinig overtredingen (ca. 5), `positiespel_4231` en `counter_532` hebben te weinig overtredingen (1,6-3,6) en passes (ca. 330-337), `testcase_a_433` heeft 22-24 overtredingen en 5,5 gele kaarten, en in TH blijven soms kaartcascades over. Het zijn kalibratie-aannames op aantallen, geen bewijs van voetbalrealisme.

**Bijkomende vondst: een bal die niemand ophaalt.** Na de dekwijziging stond bij seed 42 (laag blok tegen man-om-man) de bal vanaf tick 4156 meer dan 1300 stappen stil: de man-dekking gaf een loopdoel terug voordat de code voor een losse bal werd bereikt, en niemand zag of haalde de bal. Gevonden door `PassingTests.StationaryPlayersScanAndMatchRemainsActiveAfterEarlyPasses`. Oplossing in `PassingBrain.Decide`: voor een speler met een dekopdracht gaat een losse, bekende bal voor als hij de dichtstbijzijnde (zichtbare) teamgenoot is. Daarna blijft de wedstrijd bij seed 42 actief (91 passes in de eerste 10 minuten, laatste pass op tick 5927).


## Ronde 6, deel 2: wegtrappen (2026-10-15)

**Aanleiding.** Na deel 1 bleef `hoge_druk_433` tegen zichzelf (HH) op ca. 34 schoten, 14-16 buitenspel en 5 overtredingen per team staan. Standaardmeting: `scripts/calibration_pairs.py --seeds 8` (12 paren, seeds 1-8; dezelfde seeds voor en na, de simulatie is deterministisch).

**Diagnose (per schot teruggelopen via de events en snapshots, HH seed 1-8).**
- 43% van de 543 schoten volgt in dezelfde balbezitsketen op een wegtrap (`Pass ... clear`); 66% van de balbezitswisselingen in HH bevat een wegtrap (positiespel 77%, laag blok 31%). Een wegtrap is het mechanisme dat de achterlijn van een hoge-druk-team in een keer passeert.
- Oorzaak: een wegtrap kreeg dezelfde snelheidsformule als een pass (`7 + 0,25 x afstand`, begrensd op 16 m/s) en de bal rolt met `RollDeceleration` 0,98 m/s2. De bal verliest dus pas na ca. 90 m genoeg snelheid (9 m/s, de grens van `InterceptControl`) om door een tegenstander te worden gestopt, en rolt tot 130 m ver. Een wegtrap naar het richtpunt (25 m voor de doellijn, y = +-28) rolde 60-70 m door langs iedereen heen; de wegtrapper of zijn hoge voorhoede haalde hem onbedreigd op, waarna een schot uit 10-16 m volgde. Dit verklaart ook de buitenspel (16 per team) en de weinige duels en overtredingen (5): de tegenstander stond nooit in de buurt van de bal.
- Bijverschijnsel: het aantal inworpen (ronde 4) kwam vooral van wegtrappen die door hun snelheid over de zijlijn rolden. Dat was geen model van inworpen.

**Wijziging (`PassConfig.ClearStopsAtTarget`, standaard aan).** Een wegtrap krijgt de snelheid waarmee de rollende bal bij het richtpunt tot stilstand komt: `v = sqrt(2 x RollDeceleration x afstand)`, begrensd op 4-16 m/s, met dezelfde uitvoeringsruis als een pass. Een bal van ca. 9 m/s over 45 m is langzaam genoeg om door tegenstanders te worden betwist. Geen tactiekspecifieke uitzondering, alleen de natuurkunde van de trap.

**Meting voor en na** (gemiddelde per team per wedstrijd, 8 seeds; 'voor' = begin van deze ronde):

| paar | schoten voor | na | passes voor | na | overtredingen voor | na | buitenspel voor | na |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| LL | 12,4 | 13,1 | 421 | 426 | 11,6 | 11,7 | 3,3 | 3,1 |
| MM | 28,0 | 27,2 | 458 | 466 | 15,3 | 15,4 | 5,7 | 3,8 |
| TT | 13,9 | 15,6 | 584 | 582 | 23,8 | 23,6 | 11,4 | 12,1 |
| HH | 33,8 | 28,9 | 337 | 321 | 4,8 | 9,2 | 15,6 | 6,4 |
| PP | 21,6 | 21,0 | 337 | 289 | 3,6 | 10,8 | 0,2 | 2,4 |
| CC | 16,1 | 19,7 | 328 | 289 | 1,6 | 8,2 | 1,4 | 2,2 |
| LM | 45,2 | 44,3 | 408 | 389 | 10,1 | 11,6 | 5,5 | 4,3 |
| ML | 40,1 | 55,0 | 369 | 438 | 9,3 | 9,6 | 5,2 | 5,8 |
| HC | 34,5 | 24,9 | 375 | 332 | 3,7 | 11,1 | 5,6 | 2,5 |
| CH | 36,8 | 23,1 | 357 | 305 | 4,2 | 10,6 | 4,6 | 2,8 |
| PL | 17,1 | 20,1 | 374 | 342 | 7,1 | 8,9 | 5,6 | 3,4 |
| TH | 17,8 | 15,4 | 472 | 435 | 19,4 | 17,4 | 20,7 | 12,8 |

SCORE van het script (lager is beter): schoten 0,55 -> 0,55, overtredingen 0,78 -> 0,33, passes 0,30 -> 0,35, totaal 1,09 -> 0,89. Gestaakte wedstrijden: 2 (MM) -> 1 (TH). Overtredingen liggen nu in alle paren behalve TT (23,6) en CC (8,2, net buiten 8-18 per team-slot) binnen 8-18; de overtredingen van PP, CC, HH, HC en CH (1,6-4,8 -> 8-11) zijn hersteld omdat verdedigers de bal nu echt kunnen betwisten. Inworpen daalden sterk (LL 17 -> 3, HH 46 -> 7, doel 23): zie hieronder.

**Geprobeerd en verworpen (alles gemeten met 8 seeds; alleen behouden wat de SCORE verbeterde).**

| poging | uitkomst |
|---|---|
| richtafstand van de wegtrap begrensd op 45 m (zonder snelheidsaanpassing) | HH 34,2 schoten: geen effect, de bal rolde toch door |
| `MatchConfig.BallFollowGain` 1,0 / 1,6 (achterlijn volgt de bal sterker) | HH 39 / 44 schoten, erger: het blok schuift mee en laat ruimte achter |
| richtafstand 45 m naast de stoprol | gelijk aan alleen de stoprol (0,66 tegen 0,64 op 4 paren); weggelaten |
| `ClearZoneMeters` 45 -> 38 / 34 / 30 / 20 | passes herstellen (HH 384-437) maar PP en CC vallen bij 34 naar 3-5 schoten (klif tussen 38 en 34: balbezitsploegen komen niet meer tot schieten). Totaal 0,94 bij 38 tegen 0,89 bij 45; 45 blijft |
| dekking in het doelgebied (verdediger gaat aan de doelkant van een vrije tegenstander), met en zonder rondom-zicht op de bal van 12 m | geen effect op LM (72-77 schoten voor M); TT kreeg 3 gestaakte wedstrijden. Teruggedraaid |
| dribbel richting doel vraagt dezelfde vrije kegel als de carry | LL zakt naar 5,5 schoten, LM blijft 72. Teruggedraaid |
| twee drukzetters als de bal binnen 30 m van het eigen doel is | geen effect op LM (74 schoten). Teruggedraaid |

**Wat nog afwijkt (eerlijk).**
- **HH**: 28,9 schoten (p90 = 20) en 321 passes (p10 = 347). De wegtrap-oorzaak is voor ca. de helft opgelost (34 -> 29 schoten, buitenspel 16 -> 6). Wat blijft: balwinst hoog op het veld door de druk (in de zone 7-22 m voorbij de middenlijn begint 32% van de balbezitsketens met een schot) en de dribbel richting doel tijdens de vasthoudtijd.
- **LM / ML (man-om-man tegen laag blok)**: 44-55 schoten voor het man-team (tot 84 in een slot, tegen 24 voor de tegenstander). Diagnose per schot: de spits van M staat op 0,78 van het veld (23 m van het doel) tussen de twee middenverdedigers van L en schiet na de vasthoudtijd van 2,4 s uit ca. 13 m, steeds met twee verdedigers op 4-6 m die niet ingrijpen. Het model kent geen botsing of obstructie van spelers: een dribbelaar loopt dwars door twee verdedigers heen en alleen de duelkans (6,5% per seconde per drukzetter binnen bereik) kan hem stoppen. Dat is een modelbeperking, geen kalibratieparameter; dekking, tweede drukzetter en dribbelbeperking (zie boven) veranderden dit niet. De aanname `MarkGap`/`MarkBallRadius` van deel 1 is niet bijgesteld: M domineert ook met `dek_man` uit.
- **PP / CC / HC / CH / PL** hebben 289-342 passes (p10 = 347). Met `ClearZoneMeters` 45 loopt veel balbezit via wegtrappen; lager zetten herstelt de passes maar laat de schoten van PP en CC instorten (klif).
- **TT**: 23,6 overtredingen (p90 = 18) en 5,6 geel. Onderzocht: `pressing_intensiteit` wordt in het model niet gelezen, het ligt dus niet aan die basisinstelling. TT heeft de langste balbezitstijd (56% van de tijd met de bal bij een speler, tegen 25-38% elders), en in 45% daarvan staat een tegenstander binnen 1,2 m van de baldrager (laag blok 30%, PP 28%). Duelpogingen zijn evenredig met die tijd: TT 59 pogingen per team tegen 23-36 elders. Niet opgelost.
- **Inworpen**: 3-8 (doel 23). Dit is de prijs van de nieuwe wegtrap en geen doel van deze ronde. Een realistische oorzaak van inworpen (hoge ballen en mislukte controle die over de zijlijn gaan) is niet gemodelleerd.
- **TH** heeft nog 12,8 buitenspel en 1 gestaakte wedstrijd (kaartcascade).

**Aangepaste tests.** Golden-hashes van `laag_blok_442` tegen `man_om_man_433` (seed 42) en `laag_blok_442` tegen zichzelf (seed 7) zijn bewust vernieuwd (event-hashes `418675db...` en `77c96fae...`). De testhulpen `PreRound4Defaults` en `PreRound5Defaults` zetten `ClearStopsAtTarget` uit zodat historische hash- en geometrietests hun eigen onderwerp blijven toetsen; `AttackingRunTests.FullMatchesProduceOffsideWithNoise...` zet het uit voor de run zonder lijnfout, want een wegtrap die naast een spits voorbij de lijn tot stilstand komt is terecht buitenspel. Nieuwe test `CalibrationTests.ClearanceIsKickedToComeToRestAtItsAimPointByDefault`. Volledige suite: 404 tests groen (de snelheidstest apart gedraaid, want die faalt onder belasting).

Opmerking: het aantal metingen ging boven de afgesproken acht meetronden (elke variant kreeg een eigen 8-seed-meting); alleen de wegtrap-oplossing is behouden.
