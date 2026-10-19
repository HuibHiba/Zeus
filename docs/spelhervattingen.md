# Spelhervattingen v1 (B6)

Een tactiek kan een optioneel blok `standaardsituaties` hebben (schema: `schema/tactic.schema.json`, model: `SetPieces` in `sim/Zeus.Sim/Tactic.cs`). Zonder blok, of met een ontbrekend veld, doet de sim wat hij altijd deed (EventHash ongewijzigd). Code: `sim/Zeus.Sim/Match.SetPieces.cs`, aangeroepen vanuit `BeginRestart` (plaatsing) en `TakeRestartKick` (richtpunt), beide in `Match.Rules.cs`.

## Velden

| Veld | Waarden | Betekenis |
|---|---|---|
| `corner_voor` | `korte_paal`, `verre_paal`, `kort`, `penaltystip` | eigen corner |
| `corner_tegen` | `zone`, `man` | corner van de tegenstander verdedigen |
| `palen` | 0..2 (standaard 2; de editor laat het veld weg bij "Standaard (2)") | verdedigers op de palen, bij `zone` en `man` |
| `vrije_trap_voor` | `voorzet`, `schot`, `kort` | eigen vrije trap in de aanvallende helft |
| `vrije_trap_tegen` | `zone`, `man` | vrije trap verdedigen binnen 35 m van het eigen doel |

## Wat de sim doet

Alle keuzes zijn deterministisch en gebruiken geen extra RNG (alleen `schot` trekt de kant, zoals een penalty). De "kopper" is de speler met de hoogste `Heading + 0,5 * Strength` als er attributen zijn, anders op rol (targetman, spits en centrale verdediger eerst); gelijke score: laagste index.

- **Aanval, plaatsing (bij het toekennen):** de vier beste koppers (de nemer en de keeper niet) krijgen vaste plekken: `korte_paal` 4-5 m voor de doellijn rond de dichtstbijzijnde paal, `verre_paal` idem bij de andere paal, `penaltystip` rond de penaltystip. `kort`: de dichtstbijzijnde medespeler komt op ca. 7 m van de nemer te staan, de rest staat zoals bij `penaltystip`.
- **Aanval, trap:** de nemer speelt met de bestaande passuitvoering (`PassExecution`) naar de eerste plek; bij corners en `voorzet` met een lob (`vz = g * afstand / (2 * snelheid)`, maximaal 12 m/s) zodat de bal ongeveer daar landt. `kort`: pass naar de partner, geen lob. `schot` (alleen onder 25 m, anders `voorzet`): `ShotExecution` (afwerking van de nemer, `ShotConfig.AimOffsetMeters`) op een hoek van het doel, gelogd als schot.
- **Verdediging `zone`:** eerst de paalspelers (op de palen, 0,6 m voor de lijn), dan vier verdedigers op 4,5 m van de doellijn (y -6, -2, 2, 6: het 5-metergebied) en twee rond de penaltystip (11 m). Verdedigers kiezen op kopkracht.
- **Verdediging `man`:** na de paalspelers krijgt elke aanvaller in het zestienmetergebied (op het moment van de hervatting) de dichtstbijzijnde vrije verdediger, 1 m aan de doelkant.

## Beperkingen

- Spelers worden bij de hervatting geplaatst (net als de bestaande 9,15 m-afstandsregel), niet laten lopen: de hervatting duurt 3 s stilstand en `Step` beweegt dan niemand. Geen animatie of looppad.
- De lob is een pure startsnelheid; luchtweerstand is niet gecompenseerd en de bal landt iets kort. Alleen na een gelobde routine is er een kopbalmodel (zie "Kopbalafwerking"); in open spel wordt de bal gecontroleerd zodra hij laag genoeg is (`PossessionMaxHeight`).
- Directe vrije trappen alleen (de sim kent geen aparte indirecte vrije trap als reden). Bij een vrije trap wordt de diepte van elk aanvalsslot begrensd tot de op-één-na-laatste tegenstander (plus 0,3 m), zodat de voorzet niet in buitenspel eindigt; een verdedigingsslot binnen 9,15 m van de bal wordt overgeslagen.
- Inworpen en doeltrappen blijven ongewijzigd.
- Zone en man kennen één vaste vorm (4 + 2); er is geen keuze van het aantal in het 5-metergebied.

## Kopbalafwerking

Gebouwd zonder te compileren of te meten (branch `feat/kopbal-afwerking`); startwaarden zijn schattingen. Code: `HeaderConfig`, `HeaderModel` en `TryHeader` in `sim/Zeus.Sim/Match.SetPieces.cs`; aangezet in `TakeRestartKick` (`ArmHeader`), gecontroleerd per fysica-substap in `UpdateBall` (`Match.cs`), vóór de gewone balcontrole.

**Wanneer:** alleen na een corner of vrije-trap-voorzet mét routine (de trap is een lob, dus alleen als de nemende ploeg een `standaardsituaties`-blok heeft). Zonder blok wordt het venster nooit geopend en doet de sim exact wat hij deed (EventHash gelijk, ook met andere `HeaderConfig`-waarden). `MatchConfig.HeaderFinishing = null` betekent de standaardwaarden; `new HeaderConfig(Enabled: false)` geeft de routines van de meting van 2026-10-19 (zonder kopbal). RNG wordt alleen in dit pad getrokken.

**Venster:** de bal daalt (vz < 0) tussen 1,3 en 2,6 m hoogte in het strafschopgebied van de verdediger. Staat de keeper binnen zijn reikwijdte (2,5 m) van de bal, dan is het zijn bal (gewone keeperscontrole). Anders: de dichtstbijzijnde aanvaller en verdediger binnen `Radius` (2 m, horizontaal; geen keeper, niet de nemer, geen aanvaller in buitenspelpositie). Eén duel per trap: wie het dichtst bij de bal is krijgt de eerste kans, de ander de tweede. Valt de bal onder 1,3 m zonder kopbal, dan gaat het spel verder zoals voorheen.

**Kansen** (`HeaderModel`, puur): met vaardigheid `s = (Heading + 0,5 Strength) / 1,5` (effectieve attributen; zonder attributen `NeutralSkill` 0,65):

```
gemeen  = max(0, 1 + 2,5 (s - 0,65)) * (1 - 0,5 * d/R) * (1 - 0,5 * min(1, |z - 1,95| / 0,65))
poging  = min(0,95, 0,7 * gemeen * exp(-max(0, afstand_doel - 6) / 8) * 0,7^verdedigers_binnen_1,5m * (vrije trap ? 0,6 : 1))
klaren  = min(0,95, (vrije trap ? 1,4 : 0,35) * gemeen)
```

Een poging is een `Shot`-event (uitkomst `header`) via `ShotExecution` vanaf de bal (hoogte blijft): spreiding `AngleErrorBase * 1,2`, gemiddelde snelheid 14 m/s, vz -1 m/s, richtpunt `AimOffsetMeters` aan de kant weg van de keeper (geen trekking), uitvoeringsvaardigheid = afwerking + 1,0 (s - 0,65). Klaren is een `Pass` (uitkomst `header clear`) van het doel af, 16 m/s, vz 5 m/s. Beide tellen als aanraking zoals een trap in open spel (buitenspelmoment, laatste aanraking, balbezit).

**Rekenwerk (verwachting, niet gemeten).** Corner `korte_paal`: neem aan dat in ~60% van de corners een aanvaller binnen 2 m van het venster staat; typisch d/R 0,5 (×0,75), hoogte ×0,8, afstand doel ≤ 6 m (×1), één verdediger dichtbij (×0,7): poging ≈ 0,7 × 0,42 ≈ 0,29; zone-verdedigers op de 5-meterlijn klaren eerst in ~half van die gevallen met ≈ 0,21, dus P(kopbalschot | corner) ≈ 0,6 × 0,9 × 0,29 ≈ 0,16. Met een geschatte conversie van 12-15% per kopbal van 4-6 m (keeper op de lijn, reik 2,5 m) wordt dat **≈ 2-2,5% goals per corner**. Bij 6,5 corners per wedstrijd is dat +0,13-0,16 goals per wedstrijd (+4,5-5,5% op 2,88). Let op: 3% per corner geeft alleen al +0,19 (+6,7%); de doelen "3% per corner" en "goals binnen +5%" botsen dus zolang open spel niet minder scoort. Daarom is de start conservatief (onderkant band).
Vrije-trap-voorzet: de aanvallers staan op ~11 m of verder; poging ≈ 0,7 × 0,6 × 0,47 (afstand ~12 m) × 0,7 × 0,6 ≈ 0,07, klaren ≈ min(0,95, 1,4 × 0,6) ≈ 0,84. Met ~85% van de voorzetten in het venster wordt grofweg 65% van de ballen die nu bij 11 m geland en aangenomen worden (de bron van de 27 vrije-trapgoals) weggekopt of in een zwakke kopbal omgezet. Verwachting: vrije-trapgoals van 0,45 naar ~0,15-0,2 per wedstrijd. Haalt dat de +5% niet, dan eerst `FreeKickClearBase` of `Radius` verhogen of (buiten dit model) de lob/het aantal aanvallers bij `voorzet` verlagen, zoals de beslisregel zegt.

**Meten** (vanaf de repo-root na `dotnet build -c Release`, sequentieel; `P` en de varianten als in [kalibratie.md](kalibratie.md) "B6 spelhervattingen v1"):

```
dotnet sim/Zeus.Runner/bin/Release/net8.0/Zeus.Runner.dll --set-piece-stats --pairs $P --level prof --seeds 20 --first-seed 901 --out sp_baseline.jsonl
dotnet ... --set-piece-stats --pairs $P --level prof --seeds 20 --first-seed 901 --set-pieces corner_voor=korte_paal,corner_tegen=zone,palen=2,vrije_trap_voor=voorzet,vrije_trap_tegen=zone --out sp_np_header.jsonl
dotnet ... (zelfde) --no-headers --out sp_np_noheader.jsonl   # moet de meting van 2026-10-19 exact herhalen (eventHash per wedstrijd)
```

De uitvoer heeft een extra regel `header shots N, header clearances M`. De baseline-hashes moeten gelijk zijn aan die van 2026-10-19. Beslisregel: die van B6 in kalibratie.md (aandeel standaardgoals 20-35%, 2-4% per corner, goals per wedstrijd binnen de niveauband; geen testgrens verruimen). Bijstellen: goals per corner schaalt ongeveer lineair met `AttemptBase`; vrije-trapgoals dalen met `FreeKickClearBase`.

Tests: `SetPieceTests.HeaderAttemptChanceRisesWithHeading`, `WithoutBlockTheHeaderSettingsChangeNothing`, `HeaderRoutineIsDeterministic`, `NearPostCornerWithStrongHeadersGivesMoreHeaderAttemptsThanWithWeakHeaders` (100 seeds).

**Ronde 2 (na de eerste meting: 0 kopbalschoten, 7 keer weggekopt in 15 wedstrijden).** Rekenwerk aan de lob: een corner van ~31 m kreeg `PassExecution`-snelheid 7 + 0,25 × 31 = 14,8 m/s en vz = g·d/(2v) = 10,4 m/s. Zonder luchtweerstand landt die precies op het slot. Met de drag van `BallConfig` (k = ½ρC_dA/m ≈ 0,012 /m, bij ~18 m/s ≈ 4 m/s² vertraging) komt hij na ~2 s maar ~24 m ver: 6-8 m te kort. Hij zakt bovendien nog 2-4 m eerder door 2,6-1,3 m, ~8 m van het dichtstbijzijnde slot. Daarnaast liepen de geplaatste spelers bij de trap meteen naar hun formatieplek (tot ~10 m in 2 s). Aanpassingen, alleen met het kopbalmodel aan (`Enabled`; `--no-headers` blijft dus de meting van 2026-10-19):
1. `HeaderLob` zoekt met bisectie over de echte balfysica de horizontale snelheid waarbij de bal zonder fout dalend door `BestHeight` (1,95 m) gaat boven het richtpunt. vz = g·d/(2·nominale snelheid), begrensd op 7-12 m/s; de richtings- en snelheidsfout van de pass blijven.
2. Spelers in het strafschopgebied (geen keeper) blijven tijdens de vlucht (2·vz/g + 0,3 s) op hun plek.

Diagnose in `--set-piece-stats`: regel `header window: armed, entered, keeper within reach at entry, candidate within R, attempts rolled, succeeded, mean entry distance to nearest slot` (`Match.HeaderStats`, niet in events of snapshots).

## Editor en meting

Tabblad TEAM, paneel "Standaardsituaties": een dropdown per veld, de eerste keuze ("Zoals in het spel") verwijdert het veld; één uitlegzin per keuze. Autotest: `scripts/autotest/19a_setpieces.json`. Meting: `Zeus.Runner --set-piece-stats`; commando's en beslisregel in [kalibratie.md](kalibratie.md), "B6 spelhervattingen v1". De referentieband (20-35% van de goals, 2-4% per corner) is een aanname.

Tests: `SetPieceTests` (per routine, determinisme) en `TacticEditorModelTests.SetPieceChoicesRoundTripAndTheEmptyBlockDisappears`.
