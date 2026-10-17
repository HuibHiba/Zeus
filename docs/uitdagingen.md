# Uitdagingen: ijking van de drempels (2026-10-16, branch feat/drempels)

De zes uitdagingen (`challenges/*.json`) zijn gebouwd vóór de niveau-herschaling. Hier zijn niveaus (`niveau_tegenstander`) en drempels geijkt op "haalbaar met een redelijke tactiek, niet triviaal". Alleen JSON is aangepast; er is geen simulatiecode gewijzigd.

## Methode
- Per uitdaging en tactiek 30 wedstrijden (seed 1..30, speler thuis) met `ChallengeEvaluator.Play`-semantiek; "voor" gebruikt 20 wedstrijden. Speler-tactieken: `hoge_druk_433`, `positiespel_4231`, `counter_532` (de drie voorbeeldtactieken), plus `laag_blok_442`, `man_om_man_433`, `testcase_a_433`. Spelerniveaus amateur, halfprof en prof.
- Slagingskans = bootstrap (3000 keer 5 wedstrijden uit de 30 trekken) van het aandeel series dat alle criteria haalt. De echte uitslag in het spel is deterministisch (seed 1..5), dus kans en echte uitslag kunnen verschillen.
- Alleen de eerste poging van uitdaging 6 is gemeten (positiespel); na herhaling kiest de planner een voorbereide tegenstander.
- Meetscripts staan niet in de repo (`.tmp_meet`, lokaal, niet gecommit).

## Wijzigingen en meting (kans per tactiek, spelersniveau halfprof; prof tussen haakjes waar relevant)
Volgorde: hoge druk / positiespel / counter / laag blok.

| Uitdaging | Voor | Na | Kans na |
|---|---|---|---|
| 1 Houd de nul | tegenstander neutraal, tegen <= 1,0 en schoten tegen <= 12 | tegenstander halfprof, tegen <= 1,2, schoten tegen <= 24 | 1 / 0 / 18 / 66 % (prof 3 / 0 / 46 / 69) |
| 2 Beheers de bal | neutraal, bezit >= 55, ongeslagen >= 4 | alleen tegenstander halfprof | 66 / 68 / 0 / 1 % (prof 72 / 82 / 5 / 75) |
| 3 Counter de bezitsploeg | neutraal | alleen tegenstander halfprof | 45 / 0 / 33 / 67 % (prof 16 / 0 / 65 / 98) |
| 4 Scoor tegen het blok | neutraal, voor >= 2,0 en schoten >= 14 | halfprof, voor >= 1,0 en schoten >= 12 | 37 / 68 / 14 / 48 % (prof 79 / 70 / 3 / 83) |
| 5 Schone wedstrijd | neutraal, overtredingen <= 10, rood <= 0, ongeslagen >= 3 | tegenstander amateur, overtredingen <= 12, rood <= 2, ongeslagen >= 2 | prof-speler: 5 / 0 / 3 / 61 % |
| 6 Verras de scout | neutraal | alleen tegenstander halfprof | 67 / 75 / 37 / 75 % (prof 91 / 74 / 58 / 93) |

Voor de aanpassing (tegenstander neutraal, oude drempels) haalde bij halfprof- en profspelers vrijwel niemand uitdaging 1, 4 en 5 (0-10%, uitzondering `man_om_man_433` en `testcase_a_433`, zie onder); uitdaging 2 slaagde alleen met prof (hoge druk 42%, positiespel 63%, laag blok 84%) en uitdaging 3 en 6 waren wisselend (3: counter 57% bij halfprof; 6: 33-85%).

Redenen: een neutrale tegenstander is sterker dan elk niveau (zie spelers.md), en de oude drempels voor goals en schoten lagen boven wat de voorbeeldtactieken halen (laag blok: de voorbeeldtactieken scoren ca. 0,8-1,1 per wedstrijd, niet 2,0).

## Bevindingen die niet via de JSON op te lossen zijn
- **Uitdaging 5 is zwak haalbaar.** Tegen `man_om_man_433` (halfprof) maken hoge druk en positiespel 22-27 overtredingen per wedstrijd en krijgen ze in 75-85% van de wedstrijden minstens één rode kaart; de passieve tactieken (counter, laag blok) zijn schoon maar verliezen bijna altijd (ongeslagen 0-1 van 30). Geen enkele voorbeeldtactiek haalt >= 60% tegen een halfprof-tegenstander, ook niet met ruime drempels (beste: 29% bij overtredingen <= 30, rood <= 5). Alleen met een profteam tegen een amateur-man-om-man haalt laag blok 61%. Een mengtactiek (druk met regels) is niet gemeten.
- **Afgebroken wedstrijden.** Meerdere wedstrijden eindigden als `Abandoned` (vermoedelijk te veel rode kaarten), vooral tegen `man_om_man_433` en met `man_om_man_433` of `testcase_a_433` als eigen tactiek. `ChallengeEvaluator.Play` gooit dan een `InvalidOperationException` ("werd niet afgerond"), dus de uitdaging mislukt met een fout in plaats van een uitslag. In de meting is een afgebroken wedstrijd als mislukte serie geteld. Dit is een simulatie- of scheidsrechterkwestie, niet aangepast.
- **Uitbuitende tactieken.** `man_om_man_433` en `testcase_a_433` als eigen tactiek maken tegen laag blok en counter 4-10 goals en 65-100 schoten per wedstrijd en slagen daardoor 94-100% bij uitdaging 4 (en 92-98% bij 3, 95-100% bij 6). Dat is een simulatiegat (man-marking-/testtactiek), geen drempelkwestie; met de bestaande criteriatypen (alleen "minstens"/"hoogstens" op gemiddelden) is het niet te scheiden van een goede tactiek zonder de voorbeeldtactieken ook te laten falen. Een echt nietsdoende of willekeurige tactiek is niet gemeten, omdat er geen zo'n tactiekbestand bestaat.
- Met 5 wedstrijden is de uitslag voor één seedreeks ruis: bij uitdaging 1 en 4 ligt de echte uitslag (seed 1..5) soms boven, soms onder de kans.
- Voorbeeldtactiek-eis (>= 60% voor minstens één voorbeeldtactiek): voldaan voor 1 (laag blok), 2, 3 (laag blok), 4 (positiespel), 6, en 5 alleen in de genoemde profteam-opstelling. Voor 1, 3 en 5 is dat laag blok, niet een van de drie hoofdvoorbeelden; met de drie hoofdvoorbeelden alleen slagen 2, 4 en 6 boven 60%.

## Uitdagingen live (Claude, 2026-10-16, branch feat/uitdagingen-live)

Probleem van de maker: bij uitdagingen speel je geen wedstrijden zelf. Nu kies je per uitdaging tussen twee knoppen: **Speel live** (primair) en **Simuleer alles** (het bestaande, snelle gedrag; ook 'Bekijk wedstrijd 1 live' blijft daar).

Live-stroom:
- `godot/ChallengeRun.cs` is het sessie-object (`GameSession.Challenge`): uitdaging, gekozen tactiek en niveau, de echt gekozen tegenstander (ook de adaptieve planner en `niveau_tegenstander`), de bereikte `ChallengeMatchResult`s en `InMatch`. Wedstrijd n gebruikt seed n (1..N, zoals `ChallengeEvaluator.Play`), speler thuis (`PlayerSide` Home, `InCompetition` uit).
- Main speelt de wedstrijd gewoon (pauzemenu, wissels, popups, snelheid, doorspoelen). Aan het eind bewaart Main `ChallengeEvaluator.ToResult(MatchStatistics.FromMatch(...))` in de run en toont `[Enter] volgende wedstrijd (n/N)` of `[Enter] toon resultaat`. Een gestaakte wedstrijd telt met de toegekende uitslag (zoals in de simulatie) en sluit de wedstrijd ook af.
- Tussenstand (Challenges, na elke wedstrijd): uitslag, per doel de regel uit `ChallengeEvaluator.Evaluate` plus `op koers` / `nog haalbaar` / `niet meer haalbaar`, de uitslagen tot nu toe. De nieuwe helper `ChallengeEvaluator.Standing(spec, resultaten)` (sim, getest in `ChallengeLiveTests`) geeft per doel `OnTrack` (nu gehaald) en `Possible`: of het doel nog gehaald kan worden als alle overige wedstrijden voor dat doel zo gunstig mogelijk verlopen (een bovengrens met extreme maar eindige waarden, geen voorspelling).
- Na de laatste wedstrijd evalueert Challenges met dezelfde `Evaluate`, met dezelfde weergave als het simulatiescherm; bij 'gehaald' gaat `ChallengeProgress.MarkDone` en komt de speelstijl in de geschiedenis, zoals bij de simulatie. Met dezelfde resultaten geeft live en simulatie hetzelfde oordeel (test).
- Esc in de wedstrijd of in het tussenscherm: eerst een bevestiging ('Uitdaging afbreken?'); afbreken bewaart niets. Een wedstrijd die je voortijdig verlaat telt niet; je kunt hem opnieuw spelen of de uitdaging afbreken. Terug naar het menu wist de run.
- Alleen het keuzescherm (tactiek, niveau) is tijdens een run vergrendeld; de uitdagingenlijst ook.

Nog niet / let op: de menu-optie 'Tegenstander wisselt' en `GameSession.OpponentSubstitutions` gelden ook voor live uitdagingen (de simulatie gebruikt ze niet); verwacht dus een licht ander resultaat als die aan staat. Een tussenstand wordt niet bewaard als je het spel sluit.

Debug (alleen debug/editor): `ZEUS_LIVE=1` start live meteen en gaat zelf door (met `ZEUS_FINISH=1 ZEUS_AUTOBACK=1` een volledige run zonder invoer), `ZEUS_LIVEFAKE="2-0,1-1"` toont een tussenstand met verzonnen uitslagen, `ZEUS_NOSAVE=1` laat voortgang en geschiedenis ongemoeid.

## Tips en aanbevolen start (feat/demo-tips)

Elke uitdaging heeft nu `tip_nl/tip_en` (hulp), `uitleg_*` (het doel in gewone taal), `start_tactiek` (bestandsnaam van een voorbeeldtactiek die slaagt) en `start_nl/start_en` (korte toelichting). De drempels zijn niet veranderd. Het uitdagingenscherm toont de tip en de aanbevolen start in een kader bovenaan de rechterkolom, naast Speel live; het tussenoverzicht herhaalt het kader zodra een doel 'niet meer haalbaar' is. De beweringen komen uit de meting hierboven (halfprof, 30 wedstrijden):
- 1 Houd de nul: laag blok 66%, hoge druk 1%, positiespel 0%, counter 18%. Start: Laag blok 4-4-2.
- 2 Beheers de bal: positiespel 68%, hoge druk 66%, counter 0%, laag blok 1%. Start: Positiespel 4-2-3-1.
- 3 Counter de bezitsploeg: laag blok 67%, counter 33%, hoge druk 45%, positiespel 0%. Start: Laag blok 4-4-2.
- 4 Scoor tegen het blok: positiespel 68%, laag blok 48%, hoge druk 37%, counter 14%. Start: Positiespel 4-2-3-1.
- 5 Schone wedstrijd: alleen laag blok met niveau Prof tegen de amateurploeg haalt ca. 61%; druk en positiespel geven 22-27 overtredingen en vaak rood. De tip raadt daarom af continu te drukken en wijst op niveau Prof.
- 6 Verras de scout: alleen de eerste poging is gemeten (positiespel); de tip is gebaseerd op het gedrag van de planner (zelfde stijl drie keer = voorbereid).
Test: `ChallengeTests.EveryChallengeHasATipAndStartInBothLanguages`.

## Uitdagingen 7-10, sterren en vrij te spelen tactieken (demo/f-challenges)

- Nieuw: 7 Win de tweede bal (tegen lange bal), 8 Neem het initiatief (tegen flankdruk), 9 Koel hoofd (tegen vleugelspel, met kaarten), 10 De topploeg (tegen hoge druk op prof). Drempels en meting: `docs/kalibratie.md`, sectie "Uitdagingsdrempels 7-10 en sterren".
- JSON: optioneel `ster_2` en `ster_3` (lijsten met criteria, zelfde vorm als `criteria`) bovenop de gewone doelen. `ster_3` zonder `ster_2` is ongeldig. `ChallengeEvaluator.Stars` telt de sterren (0 = niet gehaald).
- Voortgang (`user://zeus_progress.json`): elk item in `gehaald` krijgt `sterren` (beste poging blijft staan). Een item zonder `sterren` (oude save) telt als 1 ster; oudere versies negeren de extra sleutel. `ZEUS_NOSAVE=1` schrijft niets.
- Vrijspelen (`TacticUnlocks` in de sim): lange bal 4-4-2 bij 3, flankdruk 3-5-2 bij 7, vleugelspel 4-3-3 bij 12 sterren. In de tactiekkeuze van Uitdagingen en van de eigen ploeg in het Menu staan ze tot dan uitgeschakeld als "vergrendeld (X sterren)"; eigen tactieken zijn nooit vergrendeld. Competitie, editor, meter en selectie zijn niet vergrendeld.
- Het uitdagingenscherm toont sterren per uitdaging in de lijst, het totaal en de drempels per tactiek onder de lijst, de extra sterdoelen bij de doelen en na afloop de behaalde sterren en wat nieuw is vrijgespeeld.
