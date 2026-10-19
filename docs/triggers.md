# Triggers v1 (regels in de tactiekmaker)

Een trigger ("regel") geeft een speler tijdelijk een instructie of laat hem zijn getekende looppad lopen als een situatie zich voordoet. Roadmap C1: een trigger maken kost minder dan 2 minuten en het effect is zichtbaar in de meter.

## Typen

| Voorwaarde (`voorwaarde` in het JSON) | Fase | Wat doet hij |
| --- | --- | --- |
| `bal_naar_keeper` | verdedigen | Tegenstander speelt terug op zijn keeper. |
| `tegenstander_ontvangt_op_flank` | verdedigen | Tegenstander neemt de bal aan op de flank (>= 22 m van de lengteas). |
| `slechte_aanname` | verdedigen | Tegenstander neemt een pass slecht aan. |
| `bal_terug_naar_middenvelder` | verdedigen | Tegenstander speelt terug op een middenvelder. |
| `balverlies_in_eigen_helft` | balverlies | Gegenpressing: je verliest de bal in eigen helft. |
| `balwinst_in_hoge_zone` | balwinst | Je wint de bal in het aanvallende derde. |
| `tegenstander_heeft_bal_in_zone` (nieuw) | verdedigen | Zolang de tegenstander de bal heeft in de gekozen zone (leeg = overal), bv. "bal bij hun linksback, rechtsbuiten zet druk". |
| `achterstand_na_minuut_70` (nieuw) | elke fase | Zolang je vanaf minuut 70 achterstaat (bv. spits krijgt `kom_op`). |

De eerste zes en hun v0-aannames staan in [blokwissels.md](blokwissels.md). Een zone is derde (V, M, A) plus baan (BL, HL, C, HR, BR), uit het oogpunt van de ploeg die de regel heeft.

## Acties

Een actie is een speler plus een instructie uit de lijst van [instructies.md](instructies.md), of `pad` ("loop het getekende pad", JSON `"pad": true`; zie [pad.md](pad.md)). `pad` doet niets als de speler in die fase geen pad heeft. Een instructie duurt `TriggerConfig.PressSeconds` (4 s) en wordt bij herhaling verlengd.

## Evaluatie

- Gebeurtenisvoorwaarden (de eerste zes) gaan af op het moment van de gebeurtenis (`Match.FireTriggers`).
- Toestandsvoorwaarden (de twee nieuwe) worden in `Match.UpdateStateTriggers` elke `PressSeconds` getoetst zolang ze gelden, plaats = balpositie. Dat houdt de tijdelijke instructie aaneengesloten. Het eerste moment volgt `_nextStateTriggerTick`, niet de absolute tick. Een `TriggerFired`-event komt per regel en speler hoogstens eens per 20 s; een pad dat nog loopt wordt niet herstart.
- De voorwaarde moet bij de fase passen (`TriggerRules.IsSupported`); de editor kiest de fase automatisch. `achterstand_na_minuut_70` slaat de fasecontrole over en werkt in elke fase. Een regel in een verkeerde fase doet niets. Er is geen nieuwe waarneming nodig; alles volgt uit stand, klok en balpositie.
- Geen RNG-trekking voor de toetsing zelf; tactieken zonder de nieuwe voorwaarden blijven bit-identiek (historische hashfixtures veranderen niet).

## Beperkingen

- Minuut 70 is vast (`TriggerRules.BehindFromMinute`), niet instelbaar.
- Niet gebouwd: "tegenstander in laatste derde" en "linie zakt" als eigen type; de zone van `tegenstander_heeft_bal_in_zone` is relatief aan de eigen ploeg (V = eigen verdedigende derde), met `laat_zakken` als actie benader je het.
- Eén regel heeft in de editor één actie; meer acties vraagt meerdere regels (JSON en model ondersteunen meerdere).
- Duur is vast 4 s. Spelers met attributen kunnen een trigger vertraagd of gemist uitvoeren (zie [vertrouwdheid.md](vertrouwdheid.md)).

## Meten

De TEST-tab van de editor draait het huidige model (`_model.ToJson()`), dus mét de regel. De meter (`Meter.cs`) laadt tactiekbestanden; sla de variant met regel op onder een eigen naam en vergelijk hem met het origineel: dat is een eigen variant. Roadmap-maatstaf: >= 1 maatstaf verschuift buiten de ruis bij n = 60. Dat is **niet** gemeten.

Sim-bewijs: `TriggerTests.OpponentBallInZoneTriggerChangesAFixedMatch` (vaste seed: eventhash met regel verschilt van zonder, `TriggerFired` bestaat), `StateConditionsAreSupportedAndBehindNeedsMinuteSeventy`, `TacticMakerTests.PathActionTriggerRoundTripsThroughJson`. Autotest: `scripts/autotest/18a_triggers.json`.
