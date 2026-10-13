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

## Terugspelen op de keeper (T3, v0-aannames)

Een baldrager kent bij zijn beslismoment ook de huidige positie en snelheid van zijn eigen actieve keeper binnen `PerceptionConfig.KeeperAwarenessMeters` (default 60 m), onafhankelijk van zijn kijkrichting en gewone zichtbereik. Dit modelleert dat hij zijn keeper kan lokaliseren door eerdere oriëntatie en communicatie; exacte kennis binnen die afstand is een v0-vereenvoudiging, geen extra wereldtoegang voor het brein. `PerceptionBuilder` voegt deze keeper één keer toe aan de alleen-lezen `VisiblePlayers`; die naam omvat dus ook deze kennis. Zonder balbezit geldt uitsluitend de gewone zichtfilter. De uitzondering toont geen tegenstander of uitgesloten keeper en kent ook een veldspeler die keeper is geworden. Nul schakelt de extra kennis uit.

`PassChoice` behandelt de keeper als uitweg wanneer een waargenomen tegenstander binnen `PassConfig.BackPassPressureMeters` (default 2 m, grens inbegrepen) staat, de baldrager zelf geen keeper is en strikt in zijn eigen helft staat. De pass moet achterwaarts zijn; kantwissel en uitploeg gebruiken hun eigen aanvalsrichting. Gewone afstandsgrenzen (`MinDistance` 3 m, `MaxDistance` 35 m), waargenomen snelheid voor het mikpunt en baanrisico blijven gelden. De keeperscore is `BackPassToKeeperScore` (default 2,2) minus `DistanceWeight * afstand` en `RiskWeight * risico`: onder deze drukvoorwaarde vervalt alleen de straf voor achterwaartse terreinwinst.

Een beschikbare voorwaartse veldspelerpass met gewone score minstens `BackPassForwardMinScore` (default 2,0) sluit de keeperoptie altijd uit, ongeacht diens score. Dit is de v0-definitie van een goede voorwaartse optie; zonder risico scoort een rechte pass van ongeveer 22 m bijvoorbeeld 2,0. Verder wint de keeper alleen met een positieve score die hoger is dan de beste andere optie. Zonder druk of in de aanvallende helft wordt hij ook bij een geforceerde pass niet gekozen. Een gekozen keeperuitweg wordt direct ingezet, vóór `HoldSeconds` en de noodwegwerptrap; de speler moet nog wel daadwerkelijk naar het mikpunt draaien. Alle criteria komen uit zijn eigen waarneming. Er is geen extra kansworp voor keuze of succes.

`BackPassToKeeperScore = 0` of `BackPassPressureMeters = 0` herstelt de oude gewone score voor een zichtbare keeper en het oude keepergedrag bij een aangekondigde pass. Combineer dit met `KeeperAwarenessMeters = 0` om de extra passkeuze en kennis uit te zetten. Alle nieuwe waarden moeten eindig en niet-negatief zijn en hebben een `Validate()`-controle.

Een keeper die als ontvanger een `ReceiveTarget` kent, loopt de aangekondigde pass tegemoet in plaats van op zijn gewone bewakingspunt te blijven. `KeeperReceiveAdvanceMeters` (default 3 m, nul schakelt dit vooruitkomen uit) verplaatst zijn mikpunt richting de bekende bal. Bij een zichtbare vliegende bal projecteert hij dat punt op de waargenomen baan, zodat hij een uitvoeringsfout kan opvangen; met alleen balgeheugen voorspelt hij geen verborgen beweging. Binnen 3 m volgt hij de bekende balpositie, zoals de bestaande ontvangstroute. Dit geeft bij de doellijn meer tijd voor voetcontrole. Na ontvangst gebruikt hij het gewone vasthouden en passen. De bestaande contactcode biedt na een eigen bewuste trap alleen veldspelercontrole (voethoogte en -bereik), ook in de eigen box; het grotere handbereik is alleen beschikbaar na de tegenstander. Dit is getest met een echte eigen pass gevolgd door contact op hand- en voethoogte. Een mislukt terugspelen kan nog steeds fysiek een eigen doelpunt opleveren.

### Hermeting T3 (2026-10-14)

Nulmeting en eindmeting: seeds 1 t/m 8, twee volledige helften, `laag_blok_442` thuis en uit. Het ongewijzigde `scripts/calibration_run.py` is via een tijdelijke wrapper geladen; uitsluitend de uitvoermap is verlegd naar `sim/Zeus.Runner/obj/t3_measurements` om binnen de taakscope te blijven, en de onafhankelijke wedstrijden zijn achtereenvolgens gedraaid. Labels `t3_before` en `t3_final2`. Passes sluiten wegwerptrappen uit; passsucces en schoten behouden de bestaande telling van het script. Keeperpasses zijn afzonderlijk geteld als events `Pass` waarvan `Outcome` begint met `to GK`; hervattingen beginnen met `restart` en tellen dus niet mee.

| Maat per team per wedstrijd | Voor T3 | Na T3 | Thuis na T3 | Uit na T3 |
| --- | ---: | ---: | ---: | ---: |
| Open-spel passes naar eigen keeper | 0 | 8,75 | 8,50 | 9,00 |
| Passes | 423,25 | 430,62 | 433,75 | 427,50 |
| Pass geslaagd % | 63,78 | 64,05 | 63,80 | 64,30 |
| Schoten | 18,81 | 18,88 | 19,00 | 18,75 |

Keeperpasses per seed: thuis 11/6/8/10/9/7/8/9, uit 8/10/10/7/5/12/14/6. Het doel van circa 5–12 wordt dus gemiddeld gehaald, niet in iedere afzonderlijke wedstrijd. Van 140 terugpasses volgt op 133 het eerste balcontact van de eigen keeper; vier gaan eerst naar een andere medespeler en drie worden een eigen doelpunt. De keeper speelt na ontvangst weer uit; dit vervolg is ook met de standaardbreinen over meerdere seeds getest. Passsucces blijft onder het kalibratiedoel van 79% en schoten blijven boven 13; deze taak kalibreert die afwijkingen niet. De eigen doelpunten blijven een beperking van uitvoeringsfout, voetbereik en reactietijd, zonder extra succesworp of kunstmatige bescherming van het doel.

Het ongelijke paar is daarnaast met dezelfde eindbuild over seeds 1 t/m 8 gemeten (`t3_final2_unequal`): keeperpasses thuis 8,38 en uit 4,38, en 15 `TriggerFired`-events met `bal_naar_keeper` voor Away (`man_om_man_433`). De uitploeg zit hier onder het keeperpassdoel; de afstelling is op het symmetrische referentiepaar gebaseerd en garandeert geen vaste aantallen onder een andere tactische druk. Ook de schoten blijven scheef (13,63 thuis, 67,75 uit). Het automatische wedstrijdscenario gebruikt standaardbreinen en controleert deze trigger specifiek voor Away. Beide golden-wedstrijden (ongelijk seed 42, symmetrisch seed 7) hebben nieuwe event- en snapshot-hashes; de uitgeschakelde keeperuitbreiding wordt apart tegen de oorspronkelijke seed-7-hashes gecontroleerd. De historische hash-fixture in `AttackingRunTests` schakelt nu ook T3 expliciet uit en behoudt zijn oorspronkelijke hashasserties.

## Uitbreiden
`Match.Create` heeft een optionele `brainFactory(side, id)` waarmee iedere speler een eigen brein krijgt. De standaard is `FormationBrain`. `PerceptionOf` en `IntentOf` geven de laatste waarneming en intentie voor inspectie; voordat de speler actief is kunnen die null zijn. Ongeldige intenties met niet-eindige doelen worden vóór beweging afgewezen; doelen buiten het veld worden begrensd.

`IntentKind` kent zeven soorten (`Brain.cs`): `MoveToPosition`, `HoldPosition`, `Pass`, `Scan`, `Press`, `Shoot` en `KeeperGuard`. Een `Intent` heeft een doelpositie, optioneel een `ReceiverId` (pass) en een `FacingTarget`. De doelpositie wordt tussen beslissingen vastgehouden. Het brein kiest de actie, de simulatie voert hem uit. Geef het brein geen toegang tot verborgen posities.

## Beslisinterval (zoals de code het doet)
De wedstrijdstap is `StepSeconds` = 0,1 s (10 Hz). Per speler volgt een beslissing elke `_decisionTicks` ticks: `ceil(ScanIntervalSeconds / StepSeconds)`, standaard 0,3 s = 3 ticks. De eerste beslissing per speler komt na een willekeurige startvertraging tot `MaxReactionSeconds` (0,3 s), afgerond op ticks. Vermoeidheid kan extra beslisvertraging toevoegen (`docs/vermoeidheid.md`). De bal rekent met 5 substappen per stap (50 Hz).

## Pressing uit eigen waarneming
`PressingAssignment.IsPresser(Perception)` (gezet door `Match` als `IsPresser` in de waarneming, alleen voor de verdedigende ploeg zolang iemand de bal heeft) laat een verdediger pressen als hij de bal ziet en geen zichtbare medespeler (keeper telt niet) dichter bij de bal staat; bij gelijke afstand wint de lagere id. De keuze gebruikt dus alleen wat de speler zelf ziet. Gevolg: twee verdedigers die elkaar niet zien kunnen allebei pressen, en niemand perst als niemand de bal ziet. Dit verving een toewijzing uit de volledige wereldtoestand (`PressingAssignment.Select`). Eigen telling: gedrag hermeten in `docs/kalibratie.md`.

## Verificatie en grenzen
Bij taak 14 zijn 61 tests geschreven en gecontroleerd (historisch aantal; de actuele stand staat in `docs/hervatten.md`): cone/range-grenzen, omgekeerde kijkrichting, filtering van beide teams, alleen-lezen zichtlijst, kort balgeheugen en verval, nabije en hoge ballen, verborgen balpositie, afronden van beslisintervallen, vervangbaar brein, gedeelde snapshot vóór beweging en afwijzen van NaN-intenties. De eerdere beweging-, bezit- en determinismetests blijven groen. Volledige runs met seed 42 zijn identiek, seed 43 heeft andere events. Baseline-golden-hashes zijn ongewijzigd. Godot-build en een headless opstart/loopcontrole slagen.

Nog geen afzonderlijke attributen per speler, meetruis, informatievertraging naast het scaninterval, tactisch begrip, scanbeweging, passkeuze, waarnemingsgeheugen van tegenstanders of pressure/space-grid. Deze laag is regelgebaseerd en vormt de basis voor verdere kalibratie.
