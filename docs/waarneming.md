# Waarneming en eerste beslisbrein (taak 14)

`PerceptionBuilder` filtert een momentopname voor één speler. `IPlayerBrain.Decide(Perception)` krijgt die waarneming en geeft een `Intent` terug. Een brein krijgt geen `Match` of volledige wereldtoestand. `FormationBrain` kiest lopen naar de tactiekpositie of positie houden; het standaardbrein in `Match.Create` is inmiddels `PassingBrain` (passen, schieten, rondkijken, pressen). Dit document beschrijft de waarnemingslaag uit taak 14; de huidige teststand staat in `docs/hervatten.md`.

## Volgorde binnen een tick
1. Spelers die hun startvertraging voorbij zijn worden actief; controle van een gedragen bal wordt vóór beweging gecontroleerd.
2. Wanneer minstens één speler aan een beslissing toe is, wordt één snapshot vóór beweging gemaakt.
3. Alle spelers die aan de beurt zijn lezen diezelfde snapshot. Ze zien alleen spelers in hun gezichtsveld en een zichtbare of nabije bal. Zij onthouden de laatst waargenomen bal kort.
4. Het tactieksjabloon schuift op basis van die bekende balpositie; zonder balgeheugen valt het terug op zijn basispositie. Het brein kiest een intentie en bewaart die tot de volgende beslissing.
5. Alle spelers bewegen. Daarna volgen balfysica en balbezit. Een wijziging wordt bij de volgende scan waargenomen.

Deze volgorde voorkomt dat een later verwerkte speler de al verplaatste positie van een eerdere speler ziet. Beslismomenten volgen gehele ticks, niet de renderframerate. Een interval van 0,25 s bij stappen van 0,1 s wordt 3 ticks = 0,3 s; de startvertraging bepaalt het eerste moment per speler.

## Voorlopige instellingen
`MatchConfig.Perception` accepteert een `PerceptionConfig`. Defaults: gezichtsveld 120 graden, bereik 60 m, nabijheidswaarneming van de bal binnen 1,5 m en maximaal 1,5 m hoogte, scan/beslissing elke 0,3 s, balgeheugen 1 s. Alle waarden zijn ontwerpinstellingen, geen bewezen natuurkundige of fysiologische waarden. Er is geen occlusie door andere spelers en geen aparte hoofdbeweging.

De baldrager voelt zijn eigen bal. Een eerder geziene bal behoudt zijn oude positie en snelheid met de waarnemingstick; verborgen beweging wordt nooit uit de wereld gelezen of geëxtrapoleerd. De zichtbare spelerslijst is alleen-lezen en bevat geen zelfverwijzing. Andere spelers worden nog niet onthouden. Teamfase en tactiekdoel zijn eigen opdrachtinformatie, geen zicht op de tegenstander.

## Uitbreiden
`Match.Create` heeft een optionele `brainFactory(side, id)` waarmee iedere speler een eigen brein krijgt. De standaard is `FormationBrain`. `PerceptionOf` en `IntentOf` geven de laatste waarneming en intentie voor inspectie; voordat de speler actief is kunnen die null zijn. Ongeldige intenties met niet-eindige doelen worden vóór beweging afgewezen; doelen buiten het veld worden begrensd.

`IntentKind` kent zeven soorten (`Brain.cs`): `MoveToPosition`, `HoldPosition`, `Pass`, `Scan`, `Press`, `Shoot` en `KeeperGuard`. Een `Intent` heeft een doelpositie, optioneel een `ReceiverId` (pass) en een `FacingTarget`. De doelpositie wordt tussen beslissingen vastgehouden. Het brein kiest de actie, de simulatie voert hem uit. Geef het brein geen toegang tot verborgen posities.

## Beslisinterval (zoals de code het doet)
De wedstrijdstap is `StepSeconds` = 0,1 s (10 Hz). Per speler volgt een beslissing elke `_decisionTicks` ticks: `ceil(ScanIntervalSeconds / StepSeconds)`, standaard 0,3 s = 3 ticks. De eerste beslissing per speler komt na een willekeurige startvertraging tot `MaxReactionSeconds` (0,3 s), afgerond op ticks. Vermoeidheid kan extra beslisvertraging toevoegen (`docs/vermoeidheid.md`). De bal rekent met 5 substappen per stap (50 Hz).

## Beperking: pressing gebruikt de wereldtoestand
`PressingAssignment.Select` (in `Match`, doorgegeven als `IsPresser` in de waarneming) kiest de presser uit de volledige wereldtoestand (de snapshot), niet uit de eigen waarneming van het team of de spelers. De aangewezen presser moet de bal wel kunnen waarnemen, maar de keuze wie het is, volgt niet uit wat een speler zelf weet. Dat is een afwijking van het principe dat een brein alleen eigen waarneming gebruikt.

## Verificatie en grenzen
Bij taak 14 zijn 61 tests geschreven en gecontroleerd (historisch aantal; de actuele stand staat in `docs/hervatten.md`): cone/range-grenzen, omgekeerde kijkrichting, filtering van beide teams, alleen-lezen zichtlijst, kort balgeheugen en verval, nabije en hoge ballen, verborgen balpositie, afronden van beslisintervallen, vervangbaar brein, gedeelde snapshot vóór beweging en afwijzen van NaN-intenties. De eerdere beweging-, bezit- en determinismetests blijven groen. Volledige runs met seed 42 zijn identiek, seed 43 heeft andere events. Baseline-golden-hashes zijn ongewijzigd. Godot-build en een headless opstart/loopcontrole slagen.

Nog geen afzonderlijke attributen per speler, meetruis, informatievertraging naast het scaninterval, tactisch begrip, scanbeweging, passkeuze, waarnemingsgeheugen van tegenstanders of pressure/space-grid. Deze laag is regelgebaseerd en vormt de basis voor verdere kalibratie.
