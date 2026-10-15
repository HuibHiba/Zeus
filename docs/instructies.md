# Tactische instructies (v0)

De actieve fase en verdedigingsvariant leveren opdrachten aan `PassingBrain` via `Perception`. Alleen eigen waarneming en eigen opdrachtinformatie worden gebruikt. Er is geen nieuwe randomstroom. Afstanden en gedrag zijn v0-aannames; deze uitbreiding is geen realismeclaim.

## Configuratie en voorrang

`MatchConfig.Instructions` bevat veertien afzonderlijke schakelaars. **Standaard UIT:** `EnableLineHeight`, `EnablePressingForm`, `EnableTempo` en `EnableFindSpace`. De overige tien staan aan. `InstructionConfig.Disabled` schakelt alle veertien uit; bestaande teamfases, triggers, dieptelopen en keeperuitwegen houden hun eigen schakelaars. De configuratie gaat mee in replays. Runnerconfiguratie kan bijvoorbeeld expliciet `{"Instructions":{"EnableTempo":true}}` bevatten. Onbekende velden en ongeldige parameters worden geweigerd.

De vier uitgeschakelde effecten zijn ongekalibreerd. De oude lijnhoogtecorrectie verplaatste bestaande tekeningen tot 31,5 m; tempo gaf in de eerdere isolatieproef 706 in plaats van 495 passes en 2,33 in plaats van 14,50 schoten. Manpressing gaf 55 passes en 1 schot; gemengd 242 passes en 5 schoten. Vrije ruimte gaf 51,17 schoten in die proef en 50 voor één ploeg in de eerste symmetrische golden na de reviewcorrecties. Deze effecten zijn daarom alleen expliciet te activeren. De getekende verdedigingsposities blijven standaard de basis voor de lijn.

Positie-instructies gelden voor veldspelers buiten `stilstaand`. Bezit betekent `opbouw`, `aanval` of `balwinst`; verdedigen betekent `verdedigen` of `balverlies`. De volgorde bij botsende opdrachten is:

1. Teamgeometrie (compactheid; optioneel lijnhoogte), daarna individuele posities.
2. In bezit: `laat_zakken`, `kom_op`, `sluit_naar_binnen`, `loop_zestien_in`, optioneel `vrije_man_zoeken`; daarna harde grenzen `blijf_breed` en `blijf_achter`. Breedte wint dus van naar binnen lopen en een zestienloop; achterblijven wint van alle voorwaartse doelen en van de bestaande diepteloop. Bij exact centrale fasepositie (`y == 0,5`) doet `blijf_breed` niets; andere opdrachten blijven gelden. De kant wordt niet willekeurig gekozen.
3. In het brein winnen pressing en daarna mandekking van het gewone positiedoel. Ontvangen, een zichtbare losse bal en balacties kunnen eveneens van de formatie afwijken. Keepers behouden hun bestaande gedrag; een baldrager wordt niet vastgezet door `blijf_achter`.
4. Bij balbezit blijven keeperuitweg en noodwegwerptrap eerst. Voor `dribbel_in` volgt daarna eerst de schotcontrole (`GoalProbability >= MinGoalProbability`), vervolgens de keuze tussen een goede pass en indribbelen. Zonder deze instructie blijft de bestaande houdtijd-/steekpas-/schotvolgorde gelden.

## Spelerinstructies

| Instructie | Schakelaar | Gedrag en grens |
| --- | --- | --- |
| `blijf_achter` | `EnableStayBack` | In bezit x hoogstens de eigen positie in de actieve verdedigingsvariant, zonder balvolgen; sluit ook dieptelopen uit. Geen collectieve restverdediging. |
| `laat_zakken` | `EnableDrop` | In opbouw minstens `DropMeters` (8 m) terug, hoogstens 8 m vóór de bekende bal. Zonder bekende bal geen effect. |
| `blijf_breed` | `EnableWidth` | In bezit aan de kant van de getekende fasepositie minstens `WideMeters` (25 m) van de lengteas, eventueel breder volgens de tekening. Negeert zijwaarts balvolgen. Exact centrale positie: geen breedtecorrectie. |
| `kom_op` | `EnableAdvance` | In aanval x `AdvanceMeters` (10 m) vooruit, begrensd door het veld. Geen overlaproute. |
| `sluit_naar_binnen` | `EnableCutInside` | In aanval y naar 45% van de oorspronkelijke centrumafstand. |
| `dek_man` + `dek` | `EnableMark` | Zonder bezit de zichtbare aangewezen veldtegenstander volgen, doelwaarts op `MarkGap` (1,5 m). Tegenstander en doel moeten binnen `MarkRadius` (18 m) van **de speler zelf** blijven, niet van `FormationTarget`. Onzichtbaar, uitgesloten of buiten bereik: gewone formatie. |
| `loop_zestien_in` | `EnableBoxRun` | In aanval met bekende bal in laatste derde: x minstens 11 m vóór de vijandelijke doellijn, y binnen ±12 m. Buitenspel blijft gelden. |
| `dek_ruimte_achter_lijn` | `EnableCoverDepth` | Zonder bezit anker `CoverDepthMeters` (7 m) terug als bekende bal in eigen helft ligt of een zichtbare tegenstander met >0,3 m/s naar eigen doel loopt. |
| `dribbel_in` | `EnableDribbleInside` | Bij onvoldoende passscore (`GoodPassScore` 1,5) maximaal `DribbleMeters` (7 m) vooruit en naar binnen. Eerst schieten als de schotdrempel gehaald is. De x-component blijft minstens `DribbleStopGoalDistance` (11 m standaard; ondergrens 1 m) van de doellijnen. Binnen 3 m van de lengteas stopt de voorkeur; ook na houdtijd + `ForceSeconds`. |
| `vrije_man_zoeken` | `EnableFindSpace` (**uit**) | Lokaal 3×3-raster (x 0/−6/+6 m, y 0/−8/+8 m); zoekt ruimte tussen twee waargenomen tegenstanderlijnen met minstens 6 m afstand. Voorkeur voor vooruit en afstand tot tegenstanders, straf voor afwijken en nabije ploeggenoten. Zonder waargenomen gat/ballocatie geen effect. Ongekalibreerd. |

`TacticLoader` weigert `dek_man` zonder `dek`, `dek` zonder `dek_man` en een onbekend doel met een `TacticLoadException` inclusief fase/spelerpad. Doel-ids komen uit de positie-ids van het tactieksjabloon (zoals de editor al gebruikt). In een wedstrijd met een andere tegenstanderopstelling kan dat id ontbreken: dan blijft de speler in formatie. De editor zet beide velden samen met `SetMarking`; los aanvinken zonder doel wordt geweigerd en uitschakelen verwijdert ook `dek`.

`start_pressing` is ook een vaste fase-instructie. Buiten eigen bezit perst een veldspeler een zichtbare bal, **ook als die los is**, ongeacht wie de zonale presser is. Het losse-balgeval is getest via een echte `Match` en de resulterende `Perception`/breinkeuze. De keeper doet hier niet aan mee. Zonder zichtbare bal geen directe pressintentie. De bestaande `loop_diep` blijft onder `AttackingRunConfig` vallen.

Dribbelen gebruikt de bestaande bal-aan-de-voetfysica. Een zichtbare verdediger binnen `DuelConfig.Reach` kan ook zonder pressintentie uitdagen; cooldown, `AttemptRate` en `DuelModel` bepalen de uitkomst. Geen tweede dribbelsuccesworp en geen tegenstander buiten de zichtlijst.

## Teamparameters en tijdelijke opdrachten

| Instelling | Schakelaar | Uitvoering |
| --- | --- | --- |
| `pressing_vorm=zone` | bestaand gedrag | Dichtstbijzijnde speler volgens eigen zicht perst de zichtbare bal. |
| `pressing_vorm=man/gemengd` | `EnablePressingForm` (**uit**) | Opt-in: lokale mankeuze binnen 18/9 m van het formatieanker; expliciete mandekking heeft voorrang. Man perst als de gekozen man binnen 3 m van de bal staat; de zonale presser blijft terugval. Gemengd behoudt eveneens de zonale presser. Geen collectieve toewijzing/doorgeven; ongekalibreerd en passief. Met de vlag uit blijft de zonale selectie actief; individuele `dek_man` staat daar los van. |
| `tempo` | `EnableTempo` (**uit**) | Opt-in houdtijd = `HoldSeconds × (1,5 − tempo)`. Geen snelheids-/acceleratie-effect. Ongekalibreerd. |
| `lijnhoogte` | `EnableLineHeight` (**uit**) | Opt-in oude experimentele correctie `(lijnhoogte − laagste getekende veldspeler-x) × 105 m`. Dit is geen gekalibreerde relatie met de tekening en kan haar sterk verschuiven. Standaard geen correctie; compactheid, balvolgen en individuele dekking blijven afzonderlijk werken. |
| `compactheid` | `EnableCompactness` | Verdedigende x/y-afstanden tot het zwaartepunt van de eigen getekende formatie × `1 − 0,4 × compactheid`; null/0 doet niets, 1 houdt 60% over. |

De zes bestaande triggervoorwaarden en hun fase-/zonegrenzen blijven gelijk. Hun acties kunnen bovenstaande spelerinstructies bevatten. `TriggerConfig.PressSeconds` (4 s) bepaalt de tijdelijke duur, ook na fasewissel. Herhaling verlengt; na afloop blijft de fase-instructie over. `dek_man` uit een trigger gebruikt `dek` van de actieve fase; ontbreekt het na fasewisseling, dan gewone formatie. `loop_diep` als triggeractie blijft inactief. Een speler die op zijn mandekkingsdoel staat en de bal niet ziet, scant actief.

## Verificatie en meting na review

De regressies toetsen beweging en spiegeling, prioriteiten, dribbel-schotvoorrang en begrenzing, dekking vanaf de spelerpositie, loaderfouten, losse-balpressing, opt-in-teamparameters en standaard uitgeschakelde vlaggen. Voor elk van de zes tactieken wordt met een echte wedstrijd en `FormationBrain`, beide teamrollen, stilgehouden hoge bal op het midden en zonder blok-/fasewissel de achterste veldspelerlijn over seconden 60–70 gemiddeld: maximaal ±8 m van de getekende lijn. Dit is een gecontroleerde geometrieproef; daadwerkelijk pressen en dekken mogen tijdens vrij spel afwijken.

KickShield, InterceptControl, buitenspel met/zonder lijnruis, de algemene MatchTests, balvolgen en hervattingsafstand hebben nu ook varianten met standaard instructieconfiguratie. Balvolgen houdt daarbij rekening met de expliciete compactheidsfactor. Bij buitenspel met standaard instructies betekent nul lijnruis alleen een exacte lijnwaarneming (apart getoetst); formatiebeweging en spelers die een pass kruisen kunnen nog buitenspel opleveren. De sterkere historische assertion «geen enkel buitenspel zonder ruis» blijft daarom alleen in de geïsoleerde variant gelden. De overige historische hervattingsfixtures blijven geïsoleerd om rode kaarten, wegloopgedrag en hervattingsprocedures onafhankelijk van tactische actiekeuzes te toetsen. Historische hashfixtures (`PreRound5Defaults`, keeperterugpassen, events en dieptelopen) behouden `InstructionConfig.Disabled`: zij bewaken juist de oude uitvoering. De nieuwe golden-wedstrijden toetsen de standaardconfiguratie inclusief minimaal 100 passes, 5 schoten en 10% balbezit per ploeg. De symmetrische golden gebruikt bovendien de strakkere grenzen 250–700 passes en 5–30 schoten per ploeg. De ruimere grenzen voor de ongelijke golden zijn degeneratiegrenzen; de ongelijke baseline overschreed al het gewenste bereik van 5–30 schoten, dus de golden is geen kalibratietest.

```powershell
dotnet test sim/Zeus.Sim.Tests -m:1 -o C:\dev\zeus\.tmp_codex_out
dotnet run -c Release --project sim/Zeus.InstructionProbe
dotnet run -c Release --project sim/Zeus.InstructionProbe -- --goldens
# Optionele geïsoleerde effectproef (oude opzet: 3 seeds, beide rollen):
dotnet run -c Release --project sim/Zeus.InstructionProbe -- tempo
```

De standaardprobe speelt alle 15 ongelijke paren van zes ongewijzigde tactieken, seeds 1–8, beide thuis/uit-rollen: 240 wedstrijden, 80 per tactiek. Passes/schoten/overtredingen zijn events per ploeg. Balbezit is het aandeel lopende spelticks volgens `Match.Possession` (ook bij een losse bal de laatst bezittende ploeg), gemiddeld per wedstrijd; hervattingspauzes tellen niet mee. Gestaakte wedstrijden tellen met hun werkelijke aantallen mee en worden apart vermeld. Optioneel `--league --cache <nieuw-bestand.jsonl>` bewaart resultaten per wedstrijd en kan dezelfde meting hervatten; gebruik een nieuw cachebestand na code-, tactiek- of configuratiewijzigingen.

Baseline: schone snapshot van **08af9e2**, HEAD bij aanvang. Tijdens het werk heeft een andere sessie HEAD verder gebracht; de baseline blijft bewust vast. `git worktree add ../zeus-baseline HEAD` is geprobeerd, maar de sandbox weigerde schrijven naar `.git/worktrees/zeus-baseline`. Daarom `git archive` van die HEAD onder `sim/Zeus.InstructionProbe/obj/baseline`, met alleen dezelfde generieke `LeagueProbe.cs` als meetinstrument toegevoegd. Geen stash, wijzigingen aan de hoofdwerkmap of bestaande tactieken. Beide versies lezen dezelfde zes huidige `tactics/`-bestanden. De tijdelijke baseline is na de meting verwijderd.

Per cel: baseline → standaardconfig na review; gemiddelde per tactiek per wedstrijd (80 wedstrijden per rij).

| Tactiek | Passes | Schoten | Balbezit % | Overtredingen | Gestaakt (van 80) |
| --- | ---: | ---: | ---: | ---: | ---: |
| counter_532 | 331.57 → 339.07 | 15.50 → 19.96 | 37.52 → 39.31 | 6.79 → 6.41 | 0 → 0 |
| hoge_druk_433 | 363.10 → 364.26 | 21.19 → 20.54 | 38.64 → 52.27 | 14.28 → 11.86 | 2 → 2 |
| laag_blok_442 | 546.23 → 452.43 | 26.15 → 19.43 | 60.43 → 60.16 | 3.50 → 3.34 | 0 → 0 |
| man_om_man_433 | 564.05 → 329.95 | 41.45 → 25.52 | 61.22 → 34.32 | 5.34 → 3.89 | 1 → 1 |
| positiespel_4231 | 451.36 → 375.39 | 22.07 → 32.09 | 48.00 → 47.04 | 9.09 → 9.50 | 0 → 1 |
| testcase_a_433 | 531.25 → 459.12 | 64.26 → 40.99 | 54.20 → 66.90 | 7.08 → 4.30 | 1 → 2 |

In totaal 2 → 3 gestaakte wedstrijden (elke staking komt in twee tactiekrijen terug). De genoemde vlaggen staan uit in de nieuwe standaardconfig; individuele instructies en compactheid blijven effect hebben. De tactieken verschillen duidelijk, maar het kalibratiedoel is **nog niet volledig gehaald**: positiespel_4231 (32,09) en testcase_a_433 (40,99) blijven boven 30 schoten. De baseline had bij man_om_man_433 41,45 en bij testcase_a_433 64,26 schoten; bij enkele tactieken zijn er te weinig overtredingen. De reviewfixes voorkomen de bekende extreme teamparameter-effecten, maar maken de bestaande ongelijke tactiekparen niet ineens realistisch. Verdere afstelling vraagt een afzonderlijke kalibratieronde. Acht seeds geven geen bewijs van statistische significantie.

Eindcontrole na de metingen: de volledige Release-suite slaagt met **381 tests**, inclusief snelheidstest. Tijdens de gelijktijdige probes overschreed alleen de snelheidstest haar tiensecondenlimiet; de volledige herhaling zonder probes is groen. `git diff --check` slaagt.

## Golden hashes

Beide standaard golden-hashes zijn **bewust vernieuwd** wegens de vier uitgeschakelde defaults, dekking vanaf de speler en gecorrigeerd dribbelgedrag. Ondergrenzen worden vóór de hash gecontroleerd; een passieve wedstrijd kan dus niet stilzwijgend worden goedgekeurd met een nieuwe hash. Historische hashes blijven behouden.

| Wedstrijd (thuis steeds laag_blok_442) | Eventhash | Snapshothash |
| --- | --- | --- |
| man_om_man_433, seed 42 | `0ce1e97e0e09a70c19bf4f8ed0529f8a415cc638014eaab8e7bb8dfe4f84f610` | `ce1713847e3bd0ed00ee38bb8bcb635a71d39ef1d2e7c5a0753f5fbf7f22abbc` |
| laag_blok_442, seed 7 | `828be0ca1cab823a05a8ba78ec5ce98fb9aeca5dbe9c1fdb9099cd87cce6dc7c` | `3e1efdf5f9c79097ca626fceaf94b0b9601b2108707de498a957fa40421ef71e` |
