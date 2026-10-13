# Veldgrenzen en doelpunten

`PitchRules.Evaluate(before, after, lastTouch, ballRadius)` beoordeelt een lineair bal-substapsegment zonder wedstrijdtoestand of toeval. Het eerste moment waarop de hele bal een lijn passeert bepaalt `PitchExit`; thuis valt in deze wereld naar +x aan. De andere richting wordt later bij rust expliciet gespiegeld.

## Officiële basis

Gecontroleerd op 2026-10-13 tegen IFAB 2026/27:

- De binnenafstand tussen doelpalen is 7,32 m; de onderkant van de lat staat 2,44 m boven de grond. [IFAB Law 1](https://www.theifab.com/laws/latest/the-field-of-play/)
- De hele bal moet de zij- of achterlijn passeren om buiten te zijn, ook in de lucht. [IFAB Law 9](https://www.theifab.com/laws/latest/the-ball-in-and-out-of-play/)
- Een doelpunt vereist dat de hele bal tussen de palen en onder de lat de doellijn passeert, zonder overtreding door de scorende ploeg. [IFAB Law 10](https://www.theifab.com/laws/latest/determining-the-outcome-of-a-match/)
- Na een zijlijnuitgang krijgt de tegenstander van de laatste aanraker de inworp, vanaf de plek waar de bal het veld verliet. [IFAB Law 15](https://www.theifab.com/laws/latest/the-throw-in/)
- Na een achterlijnuitgang zonder doelpunt krijgt de verdedigende ploeg een doeltrap als de aanvaller als laatste raakte; bij een laatste aanraking door de verdediger krijgt de aanvallende ploeg een hoekschop. Een doeltrap mag overal in het doelgebied genomen worden; een hoekschop vanuit de hoek die het dichtst bij de uitgang ligt. [IFAB Law 16](https://www.theifab.com/laws/latest/the-goal-kick/), [IFAB Law 17](https://www.theifab.com/laws/latest/the-corner-kick/)

## Geometrie en API

Het gekozen veld is 105 × 68 m. De radius is standaard 0,11 m en configureerbaar; deze radius is een simulatieparameter, geen exacte IFAB-balradius. Uitgang volgt pas wanneer het balmiddelpunt verder komt dan ±(52,5 + radius) of ±(34 + radius). Precies tegen de drempel aan blijven is nog niet buiten. De geretourneerde fractie is het grensmoment van een daadwerkelijke doorgang naar buiten.

Een doelpunt vereist op dat moment `abs(y) + radius <= 3.66`, `z + radius <= 2.44` en `z >= 0`. De huidige natuurkunde gebruikt z=0 voor een rollende bal; de extra radius bovenaan is daarom een conservatieve benadering en geen nieuw verticaal coördinatenstelsel. Een eigen doelpunt telt voor de ploeg die dat doel aanvalt, ongeacht `lastTouch`.

`PitchExit` bevat `Kind` (`Goal`, `ThrowIn`, `GoalKick`, `Corner`), `RestartTeam`, optioneel `ScoringTeam`, `Position` en `Fraction`. `Position` is de geïnterpoleerde XY van het balmiddelpunt bij de doorgang, dus één radius buiten de nominale lijn; de caller kiest de juiste herstartplek. Na een goal krijgt de niet-scorende ploeg de aftrap. [IFAB Law 8](https://www.theifab.com/laws/latest/the-start-and-restart-of-play/)

Als beide assen in hetzelfde segment buiten komen, wint de eerste doorgang. Bij een exact gelijktijdige doorgang bij een hoek kiest de simulatie de achterlijn; dit is een deterministische numerieke keuze, geen IFAB-voorschrift. Al buiten beginnende segmenten leveren voor diezelfde lijn geen herhaalde gebeurtenis op. Gebruik korte fysicasubstappen en splits rond stuiters.

Dit model behandelt alleen open-spelgeometrie: nog geen palen-/latbotsing, indirecte spelhervattingen, rechtstreeks scoren uit een inworp, overtredingen vóór een doelpunt, buitenspel of rustspiegeling. De caller moet die context beoordelen; het model bepaalt geen tweede doelpunt- of succeswaarschijnlijkheid.
