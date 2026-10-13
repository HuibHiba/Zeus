# Blokwissels: pure v0-selectie

`BlockSelection.Select(Tactic tactic, ScoreState score, int minute, double fatigue)` kiest alleen een `BlockHeight?`. De functie verandert geen wedstrijd, spelersposities, tactiek of RNG-state. De integratie moet voor het team de juiste scoretoestand, wedstrijdminuut en vermoeidheid leveren, en daarna de gekozen verdedigingsvariant gebruiken.

Begin bij `tactic.Phase(GamePhase.Verdedigen).Block`. Alle aanwezige voorwaarden binnen één blokwissel zijn conjunctief:

- `Score` moet gelijk zijn aan de teamrelatieve stand (`Voor`, `Gelijk`, `Achter`).
- `FromMinute` geldt vanaf de opgegeven minuut, inclusief die grens.
- `FatigueAbove` geldt strikt boven de grens; gelijk aan de grens voldoet niet.

Doorloop de regels in de opgegeven volgorde: **de laatste passende regel wint**. Dit is een expliciete gekozen v0-aanname, geen bewezen trainersvoorkeur. Iedere aanroep begint opnieuw bij het basisblok: er is geen geheugen of blijvende omschakeling wanneer een voorwaarde later vervalt. Als geen regel past, blijft het basisblok behouden, ook wanneer dat `null` is.

`fatigue` is een eindige waarde binnen [0,1], bedoeld als teamgemiddelde vermoeidheid uit langdurig energieverlies. De selector bepaalt die waarde niet. `minute` is niet-negatief; de integratie bepaalt hoe speeltijd en blessuretijd op de wedstrijdminuut worden afgebeeld. Er wordt niet automatisch op 90 minuten afgekapt.

Een doelblok van een blokwissel moet aanwezig zijn in `DefendVariants`. Een onbekend/ontbrekend doel wordt een expliciete fout, geen stilzwijgend genegeerde trainersinstructie. De loader controleert dit al voor JSON; de selector beschermt ook rechtstreeks gemaakte `Tactic`-records. Ongeldige enums, negatieve minuutgrenzen en niet-eindige of buiten [0,1] liggende vermoeidheidsgrenzen worden eveneens geweigerd. De bestaande loader controleert nog niet al die conditionele grenzen.

## Triggers (v0-aannames, uitgevoerd sinds 2026-10-13)

`TriggerRules` en `Match.FireTriggers` voeren drie voorwaarden uit voor de fase `verdedigen` met uitsluitend de instructie `start_pressing`. **Dit zijn expliciete aannames, geen vastgelegde trainerssemantiek**; ze staan in `TriggerConfig` en zijn aan te passen of uit te zetten (`MatchConfig.EnableTriggers`).

| Voorwaarde | Wanneer gaat hij af | Parameter |
| --- | --- | --- |
| `bal_naar_keeper` | Op het moment van trappen: een pass van de tegenstander is bedoeld voor zijn keeper (de beoogde ontvanger is de keeper). Een bal die de keeper toevallig krijgt, of een hervatting, telt niet. | n.v.t. |
| `slechte_aanname` | Bij balcontact van een beoogde ontvanger van de tegenstander: de bal komt harder aan dan hij kan verwerken, dus zijn snelheid ten opzichte van de ontvanger is minstens `BadTouchRelativeSpeed` (9 m/s; passes komen met 6-10 m/s aan, bij 9 gaat dit in ca. 3% van de ontvangsten af). De simulatie laat de bal na zo'n aanname nog niet wegspringen: een slechte aanname is hier een signaal, geen mislukte controle. | `BadTouchRelativeSpeed` |
| `tegenstander_ontvangt_op_flank` | Een beoogde ontvanger van de tegenstander neemt de bal aan op minstens `FlankFromMeters` (22 m) van de lengteas van het veld (\|y\|, links en rechts gelijk, ook na kantwissel). Geen ontvangst afgeleid uit een voorspelde bestemming. | `FlankFromMeters` |

Gemeenschappelijk:
- De trigger geldt alleen als de ploeg op dat moment in de fase van de trigger (`Verdedigen`) is.
- Alleen de genoemde spelers (niet de keeper, niet uitgesloten spelers) krijgen `IsPresser`, **bovenop** de gewone presser; meerdere pressers tegelijk zijn dus mogelijk. Een speler perst alleen als hij de bal ziet (eigen waarneming).
- Duur: `PressSeconds` (4 s), of tot zijn ploeg de bal weer heeft. Een nieuwe afgaande trigger verlengt de duur; er is geen aparte cooldown. Gelijktijdige triggers voegen de spelers samen.
- Elke afgaande trigger schrijft een event `TriggerFired` (spelers-id, plaats, `naam: voorwaarde`) voor de "waarom"-lijst.

Niet uitgevoerd (blijven inactief en worden nooit geïnterpreteerd): de voorwaarden `bal_terug_naar_middenvelder`, `balverlies_in_eigen_helft` en `balwinst_in_hoge_zone`; elke trigger met een `zone` (de betekenis van een zonecode is niet vastgelegd); andere fases dan `verdedigen`; andere instructies dan `start_pressing`.

Beperking: `bal_naar_keeper` gaat in de standaardsimulatie vrijwel nooit af, omdat de standaardbrein nooit terugspeelt op zijn keeper (0 van ca. 800 passes per wedstrijd; de keeper ligt buiten het gezichtsveld en vooruit spelen scoort beter). Het pad is bewezen met een eigen brein (`TriggerTests`). Het is de enige van de drie die de bijgeleverde tactieken (man-om-man) gebruiken.
