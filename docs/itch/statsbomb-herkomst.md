# StatsBomb-herkomst van spelparameters

Stand: 2026-10-17, branch `demo/claude-afronding`. Inventaris op basis van lezen en grep in `sim/`, `calibration/`, `docs/` en `scripts/`. **Geen juridisch advies.** Waar ik het niet kan aantonen staat "niet geverifieerd". Waarden komen uit de code van nu (`sim/Zeus.Sim/`); ik verzin er geen.

## Wat is aangetoond

- `docs/kalibratie.md` (2026-10-13) noemt als doel: "aantallen per team per wedstrijd dichter bij de Premier League 2015/16 (StatsBomb ...)". Doelwaarden staan in `scripts/calibration_run.py` (`TARGETS`, `PASS_TARGET` uit `calibration/event_models_v2.json`, `SHOT_TARGET`). Die doelen zijn StatsBomb-afgeleid (`scripts/extract_reference.py`, `fit_event_models.py`, `fit_pass_360.py`, `fit_shot_360.py`, `benchmark_pl.py`).
- `docs/bronvervanging-beta.md` zegt dat schotcoefficienten en kaartdrempels/penaltyfactor daarna door Wyscout-versies zijn vervangen, en dat de overige uitvoeringsparameters "aan oude schotconversie/oude tellingen zijn getuned" en niet herijkt.
- `calibration/` bevat nu alleen `wyscout_models.json` en `wyscout_duel_targets.json`: geen StatsBomb-bestanden in de werkmap. `scripts/build_demo.ps1` weigert `calibration/`, `statsbomb`, `event_models`, `pass_360`, `shot_360` e.d. in de zip.
- `sim/Zeus.Sim/Events.cs` noemt "StatsBomb-like" alleen als formaatnaam.

## Tabel

"Hoe onzeker": of de parameter (nog) op StatsBomb-tellingen rust. Hoog = kalibratiedoel was StatsBomb en niet herijkt; Laag = al vervangen door Wyscout.

| Parameter | Bestand | Bron / afleiding | Hoe onzeker |
|---|---|---|---|
| `KeeperSettings` straal 2,5 m / hoogte 2,2 m / 35 m/s (was 1,0 / 1,8) | `sim/Zeus.Sim/Match.cs` | `kalibratie.md` stap 4: afgestemd op doelpunt per schotafstand (10-16 m: 13,5% tegen doel 12,6%; overall doel ca. 10%). Doel uit PL 2015/16 StatsBomb (`SHOT_TARGET`); niet herijkt (`bronvervanging-beta.md`). | Hoog |
| `ShotConfig`: `AngleErrorBase` 1,25, `AngleErrorPerMeter` 0,0, `SpeedMean` 26, `SpeedErrorFraction` 0,15 | `sim/Zeus.Sim/Shooting.cs` | Schotruis/-snelheid samen met keeperbereik getuned op doelpunt-per-schot (`kalibratie.md` stap 4). Doel StatsBomb-afgeleid. | Hoog |
| `ShotConfig.MinGoalProbability` 0,08 | `Shooting.cs` | `kalibratie.md` stap 4 (0,08) en ronde 2 (0,125, tuning op schoten/team, doel 13 PL). In code staat 0,08 plus `PassConfig.BackPassShotXg`/`EarlyShotXg`: latere rondes; welke streefwaarde nu geldt is niet geverifieerd. | Middel (niet geverifieerd) |
| `GoalLogit*` (-0,22363 / -0,18228 / 0,16676 / 1,19441) | `Shooting.cs` | Wyscout-fit (`calibration/wyscout_models.json`, 45287 schoten). Eerder StatsBomb, vervangen. | Laag |
| `ParryCornerRate` 0,35 | `Shooting.cs` | Ronde 2: ingevoerd om corners te benaderen (doel 5,4/team, PL-tabel). Herkomst van het getal 0,35: niet geverifieerd. | Middel |
| `PassConfig`: `HoldSeconds` 2,4, `RiskWeight` 0,9, `ForwardWeight` 0,06, `DistanceWeight` 0,015, `AngleErrorRadians` 0,12, `SpeedErrorFraction` 0,12 | `sim/Zeus.Sim/Passing.cs` | `kalibratie.md` stap 3 en ronde 2: getuned op passes/team (485), pass geslaagd (79%), onderschept (17-22%), uit (2%). Doelen uit PL 2015/16 StatsBomb. Doc noemt `HoldSeconds` 3,0, code 2,4: latere wijziging, niet geverifieerd. | Hoog |
| `CarryMaxGoalDistance` 45, `CarryClearMeters` 7, `CarryConeRadians` 0,6, `ClearPressureMeters` 6, `ClearZoneMeters` 45, `ClearBelowScore` 0,9 | `Passing.cs` | Ronde 2: ingevoerd om een gedragstekort (geen oprukken) te verhelpen, effect gemeten tegen PL-aantallen (schoten, passes, inworpen). Waarden zelf: niet geverifieerd hoe gekozen. | Middel |
| `KickShieldMeters` 2,5 | `Match.cs` | `kalibratie.md` stap 3 punt 1: aangewezen als grootste oorzaak van 43-55% onderschepping (doel 17-22%). Eigen mechanisme; waarde gekozen op PL-onderscheppingscijfers. | Hoog |
| `InterceptControl` / `InterceptSettings` (code nu: straal 0,6 m, 9 m/s; doc ronde 2: 1,2 m / 9 m/s) | `Match.cs` | Getuned op onderschepping tegen PL-doel. Doc en code verschillen (0,6 tegen 1,2 m); latere wijziging niet geverifieerd. | Hoog |
| `LaneCutRunSpeed` 6,5, `LaneCutReactionSeconds` 0,4, `LaneCutMinBallSpeed` 4 | `Passing.cs` | Stap 3 punt 4: gedragsmechanisme, effect op onderschepping gemeten. Waarden zelf waarschijnlijk schattingen; niet geverifieerd. | Middel |
| `DuelConfig.AttemptRate` 0,08 | `sim/Zeus.Sim/DuelModel.cs` | Eerst 0,029 en 0,05 op StatsBomb (duelpogingen 33/team); nu herfit uit Wyscout contactfouls/team (code-commentaar en `bronvervanging-beta.md`). | Laag |
| `BaseFoul` 0,4, `MaxFoul` 0,6, `SpeedFoul`, `ApproachFoul`, `IndisciplineFoul`, `FatigueFoul`, `BaseWin` 0,50, `SkillWin`, `LooseChance` 0,45 | `DuelModel.cs` | `kalibratie.md` stap 1: getuned op uitkomstenmix (38/37/10) en overtredingen/team uit PL StatsBomb; niet apart herijkt. `BaseFoul` in doc 0,34, in code 0,4: latere wijziging, niet geverifieerd. | Hoog |
| `RecklessOwnThird` 0,5052, `RecklessMiddle` 0,5556, `RecklessAttacking` 0,7289, `ExcessiveForceScore` 0,8759, `PenaltyAreaFoulFactor` 1 | `DuelModel.cs` | Eerst StatsBomb-kwantielen (0,46 / 0,737 / 0,75 / 0,787 / 0,2); nu Wyscout (19,23 / 12,24 / 7,39% en 0,208% direct rood). | Laag |
| `PenaltyConfig` (`AimOffsetMeters` 3,1, `AngleErrorRadians` 0,24, `KeeperPushOffSpeed` 1,7, `KeeperDiveMeters` 2,2, `KeeperDiveSeconds` 0,8, `KeeperReachMeters` 1,6) | `sim/Zeus.Sim/Penalty.cs` | Getuned op 1000 penalties tegen conversie "echt ca. 75-78%" (`calibration_run.py`: `penalty_conversie_%` 76). Bron van die 75-78% (StatsBomb of algemeen): niet geverifieerd. | Middel (niet geverifieerd) |
| `OffsideMargin` -3,0 en buitenspelgedrag | `Passing.cs` | `kalibratie.md` ronde 3: sweep naar 1,8 buitenspel/team (doel PL StatsBomb; `docs/buitenspel.md`). | Hoog |

Niet in de tabel: parameters waar ik geen kalibratiespoor op StatsBomb vond (bv. `Fatigue.cs`, `CardModel.cs`, `Perception.cs`; de code noemt ze zelf "uncalibrated"/"provisional").

## Wat dit betekent (feitelijk)

- Geen StatsBomb-data of -modelbestand in de zip of in `calibration/` van de werkmap. Wat overblijft zijn eigen uitvoeringsparameters die op StatsBomb-tellingen zijn afgesteld (Hoog in de tabel).
- `herkomst-bronnen.md` zegt: "nu **geen** vermelding in LEESMIJ". Dat is verouderd: `scripts/build_demo.ps1` (regels 171 en 229) zet nu wel een StatsBomb-credit in de LEESMIJ ("tuned with the help of StatsBomb Open Data"). Dat verandert niets aan de open vraag in `licenties-check.md` punt 2 (art. 1.2.2 over afgeleide analyse); beslis of de regel blijft.
- Git-historie bevat de oude StatsBomb-modellen (`1b8e7a2`, `f1a0e0f`, verwijderd in `04cfd61`, volgens `herkomst-bronnen.md`; hier niet nagelopen).

## Niet geverifieerd

- Of de huidige waarden na de laatste ronden nog kloppen met `docs/kalibratie.md`: doc en code verschillen op minstens `HoldSeconds`, `BaseFoul`, `InterceptControl`.
- Welke commit welke waarde op welke StatsBomb-meting zette: `git log`/`blame` niet gedraaid.
- `data/reference/` en `data/pl_1516_team_match.csv` niet geopend.
- Of "getuned op een telling" onder "any analysis derived" valt: juridisch, niet beantwoord.

## Mogelijke vervolgstappen (niet gedaan)

1. Herijken tegen Wyscout (`bronvervanging-beta.md`, "Minimale vervolgstap") en vastleggen welke Hoog-rijen daarna Laag zijn.
2. Besluiten of de StatsBomb-regel in de LEESMIJ blijft.
3. Repo-historie opschonen of privé houden voor publicatie.
