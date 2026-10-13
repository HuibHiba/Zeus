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

`TriggerRules` en `Match.FireTriggers` voeren zes voorwaarden uit in hun eigen fase met uitsluitend de instructie `start_pressing`. **Dit zijn expliciete aannames, geen vastgelegde trainerssemantiek**; ze staan in `TriggerConfig` en zijn aan te passen of uit te zetten (`MatchConfig.EnableTriggers`).

| Voorwaarde | Wanneer gaat hij af | Parameter |
| --- | --- | --- |
| `bal_naar_keeper` | Op het moment van trappen: een pass van de tegenstander is bedoeld voor zijn keeper (de beoogde ontvanger is de keeper). Een bal die de keeper toevallig krijgt, of een hervatting, telt niet. | n.v.t. |
| `bal_terug_naar_middenvelder` | In `verdedigen`, op het trapmoment van een open-spel pass van de tegenstander: de roosterrol van de bedoelde ontvanger is `controlerende_middenvelder`, `box_to_box` of `aanvallende_middenvelder`. De vooruitcomponent in de aanvalsrichting van de passer is strikt kleiner dan minus `BackwardPassFraction` maal de bedoelde passlengte. Plaats = beginpunt bij de voeten van de passer; het mikpunt bepaalt de richting, niet de latere uitvoeringsfout. | `BackwardPassFraction` (0,3) |
| `balverlies_in_eigen_helft` | In `balverlies`: balbezit gaat naar de tegenstander op x_rel strikt kleiner dan `OwnHalfEnd`. Plaats = contactplek of duelplek. | `OwnHalfEnd` (0,5) |
| `balwinst_in_hoge_zone` | In `balwinst`: eigen ploeg krijgt balbezit op x_rel minstens `AttackingThirdStart`. Plaats = contactplek of duelplek. | `AttackingThirdStart` (2/3) |
| `slechte_aanname` | Bij balcontact van een beoogde ontvanger van de tegenstander: de bal komt harder aan dan hij kan verwerken, dus zijn snelheid ten opzichte van de ontvanger is minstens `BadTouchRelativeSpeed` (9 m/s; passes komen met 6-10 m/s aan, bij 9 gaat dit in ca. 3% van de ontvangsten af). De simulatie laat de bal na zo'n aanname nog niet wegspringen: een slechte aanname is hier een signaal, geen mislukte controle. | `BadTouchRelativeSpeed` |
| `tegenstander_ontvangt_op_flank` | Een beoogde ontvanger van de tegenstander neemt de bal aan op minstens `FlankFromMeters` (22 m) van de lengteas van het veld (\|y\|, links en rechts gelijk, ook na kantwissel). Geen ontvangst afgeleid uit een voorspelde bestemming. | `FlankFromMeters` |

Gemeenschappelijk:

- De trigger geldt alleen als de ploeg op dat moment in de fase van de trigger (`Verdedigen`, `Balverlies` of `Balwinst`, volgens de betreffende voorwaarde) is.
- Alleen de genoemde spelers (niet de keeper, niet uitgesloten spelers) krijgen `IsPresser`, **bovenop** de gewone presser; meerdere pressers tegelijk zijn dus mogelijk. Een speler perst alleen als hij de bal ziet (eigen waarneming).
- Duur: `PressSeconds` (4 s), of tot zijn ploeg de bal weer heeft. Een nieuwe afgaande trigger verlengt de duur; er is geen aparte cooldown. Gelijktijdige triggers voegen de spelers samen.
- Elke afgaande trigger schrijft een event `TriggerFired` (spelers-id, plaats, `naam: voorwaarde`) voor de "waarom"-lijst.

### Zones en bezitwissels (T4, v0-aannames)

Een optionele `zone` heeft de vorm `[VMA]-(BL|HL|C|HR|BR)`. De plaats van de gebeurtenis wordt met dezelfde omzetting als `BallRel` naar ploegrelatieve coördinaten gebracht: x_rel van eigen doel naar vijandelijk doel, y_rel van links naar rechts in aanvalsrichting. Beide assen spiegelen voor Away en na kantwissel. Zonder zone blijft de oude toets ongewijzigd. Ongeldige zonecodes blijven inactief.

De grenzen staan in `TriggerConfig`:

- V: x_rel < `DefensiveThirdEnd` (1/3); M: vanaf die grens tot `AttackingThirdStart` (2/3); A: vanaf die grens.
- BL: y_rel < `LeftOutsideEnd` (0,2); HL: vanaf die grens tot `LeftHalfEnd` (0,4); C: vanaf die grens tot `CentreEnd` (0,6); HR: vanaf die grens tot en met `RightHalfEnd` (0,8); BR: strikt boven die grens.
- Op de lengte- en breedtegrenzen begint de volgende zone, behalve exact 0,8: dat blijft HR, conform BR > 0,8. Doellijnen en zijlijnen behoren tot hun aangrenzende zone.

`Validate()` vereist eindige, strikt oplopende zonegrenzen tussen 0 en 1, `OwnHalfEnd` strikt tussen 0 en 1 en `BackwardPassFraction` binnen [0,1]. Alle bestaande instellingen en hun controles blijven gelden.

Een bezitwissel uit fysiek balcontact (ook een vastgehouden redding) of een gewonnen duel wordt bewaard tot de bestaande fase-update. Dan worden beide voorwaarden getoetst op de opgeslagen wisselplek en de actuele fase; er komt geen extra fase-update of kansworp bij. Een losse bal zonder nieuwe eigenaar, een hervatting en een redding die tot een corner wordt getipt gelden niet als bezitwissel. Met uitgeschakelde teamfases of zonder overgangsduur ontstaan geen fases `Balverlies`/`Balwinst` en blijven die triggers inactief. Een balwinsttrigger schrijft het gewone event en geeft dezelfde pressingduur; eigen bezit beëindigt die pressing bij de volgende beslissing, zoals voorheen.

Niet uitgevoerd (blijven inactief en worden nooit geïnterpreteerd): iedere voorwaarde in een andere fase dan hierboven genoemd, en andere instructies dan `start_pressing`. Een trigger met een andere instructie blijft als geheel inactief, ook bij gemengde acties.

T4 verandert het standaardgedrag niet: de bestaande tactieken gebruiken geen nieuwe voorwaarden of zones. De bestaande golden-hashes blijven behouden en worden met de volledige testsuite gecontroleerd. Geen kalibratiemeting voor deze taak; de uitbreiding wordt met pure grenscontroles en fysieke wedstrijdscenario's geverifieerd.
