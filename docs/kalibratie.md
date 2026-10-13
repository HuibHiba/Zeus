# Kalibratie van de simulatiekern (aantallen)

Datum 2026-10-13. Doel: de aantallen per team per wedstrijd dichter bij de Premier League 2015/16 (StatsBomb, `calibration/event_models.json`, `calibration/pass_360_model.json`, `data/reference/`) brengen. **Dit is kalibratie van aantallen op één tactiekenpaar, geen bewijs van voetbalrealisme.** Bronnen en definities: vaultnotitie `Referentie wedstrijdevents` en `Kansmodellen uit data`.

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
- Pass-succes per lengte (zie boven), kortste passes (< 10 m) komen bijna niet voor (n < 30 van 6500), terwijl dit in de data 24% is.
- Balbezitswissels 207 (doel 188), onderschept 15,9% (net onder 17-22%), tweede geel/rood 0,12 (doel 0,08) door herhaaldelijke fouten van dezelfde dichtstbijzijnde verdediger (de pressing kiest steeds één speler); fouten in de eigen derde 7% (data 20%), aanvallend derde 3% (data 27%): de plaats van de fouten klopt niet met de data.
- Duelpogingen 33,8 klopt in totaal, maar thuis 17 en uit 51 per team (pressing hangt van tactiek af).
- Kaartdrempels zijn kwantielen van een klonterige verdeling, geen regeluitleg; SPA/DOGSO blijven onbeoordeeld.

## Hermeting na de aftrapfix (commit 35547f5, 8 seeds, zelfde tactiekpaar)

De eindstand in de tabel hierboven is gemeten vóór de aftrapfix (aftrapnemer volgt kantwissel). Opnieuw gemeten met `calibration_run.py --label verificatie --seeds 8`; alle 8 wedstrijden bereikten `Finished` (de runner geeft anders exit 2 en het script stopt). Gemiddelde per team: passes 487, geslaagd 82,0%, onderschept 15,1%, wissels 203, duelpogingen 33,1, overtredingen 11,4, geel 1,62, rood+2e geel 0,38 (doel 0,08), schoten 18,9 (doel 13, p10-p90 7-20), doelpunten 2,0 (doel 1,3), 10,9% per schot. Corners 0, inworpen 0,6, buitenspel 0,3. De schoten en het rood liggen dus hoger dan de tabel; de rest is gelijk. Nog geen nieuwe kalibratiestap gedaan: de afwijkingen onder "Wat nog afwijkt" gelden onverkort.

## Aangepaste bestaande tests

Drie tests legden oude ongekalibreerde waarden vast en zijn minimaal aangepast: `DuelModelTests.ChancesAreBounded...` (bovengrens foul is nu `DuelConfig.Default.MaxFoul` i.p.v. 0,35); `RestartTests` (config met `AttemptRate: 1` zodat elk duel direct een overtreding is); `PassingTests` (`ChoosesOnlyVisibleTeammate...`: de tegenstander staat op x=12 i.p.v. 8 zodat de ontvanger niet buitenspel lijkt; `NoPassOptionTriggersScanning...`: `HeldBallSeconds = 5` omdat de standaard `HoldSeconds` nu 3,0 is). Nieuwe tests in `CalibrationTests.cs` (21): parameters, validatie, determinisme, conduct, buitenspelmijding, trapschild, ontvanger en baan afsnijden, schotmodel en keeperbereik.
