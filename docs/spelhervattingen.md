# Spelhervattingen v1 (B6)

Een tactiek kan een optioneel blok `standaardsituaties` hebben (schema: `schema/tactic.schema.json`, model: `SetPieces` in `sim/Zeus.Sim/Tactic.cs`). Zonder blok, of met een ontbrekend veld, doet de sim wat hij altijd deed (EventHash ongewijzigd). Code: `sim/Zeus.Sim/Match.SetPieces.cs`, aangeroepen vanuit `BeginRestart` (plaatsing) en `TakeRestartKick` (richtpunt), beide in `Match.Rules.cs`.

## Velden

| Veld | Waarden | Betekenis |
|---|---|---|
| `corner_voor` | `korte_paal`, `verre_paal`, `kort`, `penaltystip` | eigen corner |
| `corner_tegen` | `zone`, `man` | corner van de tegenstander verdedigen |
| `palen` | 0..2 (standaard 2) | verdedigers op de palen, bij `zone` en `man` |
| `vrije_trap_voor` | `voorzet`, `schot`, `kort` | eigen vrije trap in de aanvallende helft |
| `vrije_trap_tegen` | `zone`, `man` | vrije trap verdedigen binnen 35 m van het eigen doel |

## Wat de sim doet

Alle keuzes zijn deterministisch en gebruiken geen extra RNG (alleen `schot` trekt de kant, zoals een penalty). De "kopper" is de speler met de hoogste `Heading + 0,5 * Strength` als er attributen zijn, anders op rol (targetman, spits en centrale verdediger eerst); gelijke score: laagste index.

- **Aanval, plaatsing (bij het toekennen):** de vier beste koppers (de nemer en de keeper niet) krijgen vaste plekken: `korte_paal` 4-5 m voor de doellijn rond de dichtstbijzijnde paal, `verre_paal` idem bij de andere paal, `penaltystip` rond de penaltystip. `kort`: de dichtstbijzijnde medespeler komt op ca. 7 m van de nemer te staan, de rest staat zoals bij `penaltystip`.
- **Aanval, trap:** de nemer speelt met de bestaande passuitvoering (`PassExecution`) naar de eerste plek; bij corners en `voorzet` met een lob (`vz = g * afstand / (2 * snelheid)`, maximaal 12 m/s) zodat de bal ongeveer daar landt. `kort`: pass naar de partner, geen lob. `schot` (alleen onder 25 m, anders `voorzet`): `ShotExecution` op een hoek van het doel, gelogd als schot.
- **Verdediging `zone`:** eerst de paalspelers (op de palen, 0,6 m voor de lijn), dan vier verdedigers op 4,5 m van de doellijn (y -6, -2, 2, 6: het 5-metergebied) en twee rond de penaltystip (11 m). Verdedigers kiezen op kopkracht.
- **Verdediging `man`:** na de paalspelers krijgt elke aanvaller in het zestienmetergebied (op het moment van de hervatting) de dichtstbijzijnde vrije verdediger, 1 m aan de doelkant.

## Beperkingen

- Spelers worden bij de hervatting geplaatst (net als de bestaande 9,15 m-afstandsregel), niet laten lopen: de hervatting duurt 3 s stilstand en `Step` beweegt dan niemand. Geen animatie of looppad.
- De lob is een pure startsnelheid; luchtweerstand is niet gecompenseerd en de bal landt iets kort. Er is geen apart kopbalmodel: de bal wordt gecontroleerd zodra hij laag genoeg is (`PossessionMaxHeight`).
- Directe vrije trappen alleen (de sim kent geen aparte indirecte vrije trap als reden). Bij een vrije trap worden aanvallers in de zestien niet tegen buitenspel beschermd; een slot binnen 9,15 m van de bal wordt overgeslagen.
- Inworpen en doeltrappen blijven ongewijzigd.
- Zone en man kennen één vaste vorm (4 + 2); er is geen keuze van het aantal in het 5-metergebied.

## Editor en meting

Tabblad TEAM, paneel "Standaardsituaties": een dropdown per veld, de eerste keuze ("Zoals in het spel") verwijdert het veld; één uitlegzin per keuze. Autotest: `scripts/autotest/19a_setpieces.json`. Meting: `Zeus.Runner --set-piece-stats`; commando's en beslisregel in [kalibratie.md](kalibratie.md), "B6 spelhervattingen v1". De referentieband (20-35% van de goals, 2-4% per corner) is een aanname.

Tests: `SetPieceTests` (per routine, determinisme) en `TacticEditorModelTests.SetPieceChoicesRoundTripAndTheEmptyBlockDisappears`.
