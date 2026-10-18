# Roadmap Gaffer's Notebook: van 2D-demo naar 3D-coachgame, Early Access en 1.0

**Intern werkdocument, geen belofte aan spelers.** Opgesteld 2026-10-18 na een vragenronde met de maker, met een planner-agent en een architect-agent (ECC). Bouwt voort op [meerjarenplan.md](meerjarenplan.md) (2026-2028) en vervangt de planning daarin. Rangorde bij tegenspraak: dit document voor de planning; vault `Besluiten` voor besluiten; vault `Licenties en Steam` voor licenties; [hervatten.md](hervatten.md) voor de actuele bouwstand. Herzien na elk go/no-go-moment.

Nummering: **G1-G10** zijn go/no-go-momenten, **M1-M11** mijlpalen, **SJ1-SJ12** de onderdelen van werkstroom G (Steam, juridisch, community), **T0-T10** de technische 3D-mijlpalen van de architect ([3d-architectuur.md](3d-architectuur.md)).

---

## 1. Besluiten van de maker (2026-10-18)

Alle antwoorden uit de vragenronde. Ze gelden als uitgangspunt tot de maker ze in `Besluiten` herziet.

| # | Onderwerp | Besluit |
|---|---|---|
| 1 | Character | De **coach**: eigen trainer maken, langs de lijn of in de dug-out, kijken vanuit zijn ogen. Het blijft een tactiekspel; je bestuurt geen spelers. |
| 2 | 3D-stijl | **Semi-realistisch**: realistischer modellen en animaties via asset packs, geen gezichtsanimatie, geen fotorealisme. |
| 3 | Managerlaag | Media en bestuur (persconferenties, bestuursvertrouwen, ontslag, fans); selectie en carrière **zonder contracten**; transfers **zonder onderhandeling** (vaste prijs, één budget). Geen financiële simulatie verder dan dat. |
| 4 | 2D | De notitieboekweergave **blijft naast 3D**, beide op dezelfde simulatie. |
| 5 | Geluid | Stadiongeluid dat meebeweegt (publiek reageert op kansen, goals, kaarten, stand; gezang per club; 3D-positie van bal, fluit, schot). **Geen commentaarstemmen.** |
| 6 | Budget | 8-10 uur per week maker-tijd (90 effectieve uur per kwartaal), **≤ €500 per jaar**. Agents bouwen; de maker beoordeelt, speelt, beslist. |
| 7 | Verkoop | **Steam Early Access, daarna 1.0.** Prijs EA €10-15. De gratis 2D-demo blijft op itch. Naam "Gaffer's Notebook" blijft als de merkcontrole slaagt. |
| 8 | Beleving | Moet gaan lijken op "iets als FIFA of Football Manager"; FIFA-realisme is bewust niet het doel. |
| 9 | Wedstrijdduur in 3D | **Hoogtepunten plus sleutelmomenten** (zoals FM): kansen, goals, kaarten en de minuten eromheen in 3D op 1x, de rest snel-vooruit met de 2D-kaart. Doel 8-12 minuten per wedstrijd. |
| 10 | Platform | **Windows**, redelijke laptop. Doel 60 fps op 1080p op integrated graphics van ongeveer 2020 of nieuwer (Iris Xe-klasse) in de laagste stand; zwakkere pc's krijgen 2D. |
| 11 | Spelwereld | **4 fictieve landen**, 2-3 divisies per land (eigen land 3), **1 Europees toernooi** (32 clubs). Ongeveer 150 clubs, 3000 spelers. |
| 12 | Seizoen | 16-18 clubs per divisie, 30-34 wedstrijden plus beker. |
| 13 | Volgorde | **Eerst realisme en 2D-polish, dan managerlaag, dan 3D.** 3D-haalbaarheidsproef in 2027-Q3. |
| 14 | EA-datum | Geen harde datum; kwaliteit bepaalt via go/no-go. |
| 15 | Community | **Discord-server** voor de game plus **devlogs** op itch (later Steam). |
| 16 | Multiplayer | Live 1-tegen-1 als lange termijn **na 1.0** (go/no-go G7). Asynchroon delen blijft. |
| 17 | Coach-diepte | Uiterlijk en naam; **reputatie en loopbaan** (aanbiedingen, ontslag); **coachstijl en vaardigheden** (kleine, uitgelegde effecten). **Eén coach per carrière** (opslag in `zeus_carriere.json`). |
| 18 | Stadion | **Eigen stadion per club dat meegroeit** met het niveau; clubkleuren, tenues en embleem zichtbaar in 3D; **weer, tijdstip en seizoen** (visueel). |
| 19 | Training en staf | Trainingsfocus per week **plus een kleine staf**: assistent, scout, fysio als personen met niveau. Geen FM-schema's, geen jeugdopleiding. |
| 20 | Mods | Tactieken delen (bestaat, uitbreiden); **bewerkbare competitie- en clubdata (JSON)**; **eigen tenues en emblemen als PNG**, ook in 3D. |
| 21 | 3D buiten de wedstrijd | **Korte 3D-scènes**: kleedkamer, persconferentie, dug-out. Geen beloopbare club. |
| 22 | Attributen | **Zichtbare cijfers** (1-20) zoals FM; de cijfers moeten meetbaar verschil maken in de simulatie. |
| 23 | Talen | Steam-versie **alleen Engels**. Nederlands blijft in de demo tot de open bèta (0.5) en wordt daarna niet meer bijgewerkt; ontbrekende vertalingen vallen terug op Engels. |
| 24 | Testhardware | Gewone pc met losse videokaart (NVIDIA of AMD) = ontwikkel- en "mooie" stand. Werklaptop met integrated graphics = ondergrens. Exacte gpu's vastleggen bij T0. |
| 25 | 3D-terugval | Haalt 3D de kwaliteitseis (G10) niet: **een kwartaal extra 3D, EA schuift op.** Geen EA met "experimenteel 3D". |
| 26 | Standaardcamera | **Tv-camera standaard**; coach-POV en tactische camera als keuze, één toets wisselt; minimap bij POV. |
| 27 | Bronnen | Mixamo via het bestaande Adobe-account; AI-gegenereerde geluiden en texturen alleen met vermelding in het AI-logboek; betaalde packs ≤ €300 per jaar, pas na de 3D-go; eigen opnames met vrienden voor gezangen. Geen bestaande clubliederen of popmelodieën. |
| 28 | Jurist | **Geen jurist; risico geaccepteerd.** Merkcontrole en licenties zelf. Gevolg: een merkbotsing of datalicentie kan later een naamswijziging of het schrappen van onderdelen afdwingen. |
| 29 | Testbuilds 3D | Itch **Restricted** voor testers; vanaf Coming Soon **Steam Playtest**. De publieke itch-demo blijft 2D. |
| 30 | Na EA | Geleerde AI (fase 9) en de volledige vriendenmodus (ladder, deelcode) komen **na EA**. |
| 31 | Datatoestemming | De Discord-berichten met toestemming van de databronnen bewaart de maker; kopie naar de vault (`02 Techniek`). |

Technische keuzes van de architect die geen makervraag waren: de renderer (Compatibility, Mobile of Forward+) wordt in de haalbaarheidsproef gemeten en mag projectbreed veranderen als alle 2D-tests groen blijven; op 12x en 20x is voetglijden geaccepteerd met de tactische camera als standaard; de coach staat in v1 op een vaste plek (lopen langs de lijn later); de bestaande gegenereerde geluiden blijven placeholder tot werkstroom E ze vervangt.

Afwijkingen van de PRD en het meerjarenplan: de PRD zegt "niet voor: transfers, financiën, media" (besluit 3 vervangt dat gedeeltelijk: wel media en transfers-lite, geen financiën); het meerjarenplan had 3D als optie achter G3 (nu hoofddoel), G7 "standaard nee" (nu "na 1.0"), vriendenmodus in 2027-Q3 (nu na EA) en EA P50 2028-Q2 (nu 2029-Q2).

---

## 2. Eindbeeld 1.0

Je maakt je eigen trainer (uiterlijk, naam, coachstijl) en neemt een fictieve club over, meestal op amateurniveau. Tussen de wedstrijden kies je een trainingsfocus, stuur je een kleine staf aan, beheer je je selectie (ontwikkeling, veroudering, blessures, schorsingen), koop en verkoop je spelers voor vaste prijzen binnen één budget, beantwoord je persvragen en houd je bestuur en fans tevreden. Haal je je doelen niet, dan word je ontslagen en kies je uit aanbiedingen van andere clubs; je reputatie groeit met resultaten. De wereld bestaat uit vier fictieve landen met eigen divisies, bekers en één Europees toernooi; alle data is bewerkbaar en tenues en emblemen zijn te vervangen. De kern blijft de vrije tactiekmaker met triggers, de meter, 25+ uitdagingen en een simulatie die binnen vastgelegde kalibratiegrenzen blijft. De wedstrijd volg je als hoogtepunten plus sleutelmomenten in semi-realistische 3D vanuit de tv-camera, vanuit de ogen van je coach langs de lijn, of tactisch van boven; of als volledige 2D-notitieboekwedstrijd. Het stadion van je club groeit met het niveau, het weer en het tijdstip wisselen, en het publiek reageert hoorbaar op kansen, goals, kaarten en de stand, met gezangen per club en zonder commentaar. Korte 3D-scènes tonen je coach in de kleedkamer, bij de pers en in de dug-out. Windows, Engels, via Steam; de gratis 2D-demo blijft op itch.

---

## 3. Werkstromen

| Werkstroom | Doel | Hoofdspoor in |
|---|---|---|
| **A** Speelbaarheid en speelgevoel | Nieuwe spelers begrijpen het in 10 minuten en willen verder | 2026-Q4, 2029-Q1 |
| **B** Simulatie-realisme | Lijkt op voetbal binnen de grenzen van meerjarenplan §4.4; tactiek en cijfers maken meetbaar verschil | 2027-Q1, 2027-Q2 |
| **C** Inhoud en meta | Triggers, uitdagingen, delen | bijspoor 2027-Q1, 2028-Q4 |
| **D** 3D-weergave, coach en scènes | Semi-realistische 3D-wedstrijd, coach-POV, stadion, scènes | 2028-Q1 t/m 2028-Q4 |
| **E** Geluid | Dynamisch stadiongeluid, positioneel in 3D, gezangen | bijspoor 2027-Q2, 2028-Q1, 2028-Q2 |
| **F** Managerlaag | Wereld, selectie, staf, bestuur, media, transfers-lite, fans, coachloopbaan, mods | 2027-Q3, 2027-Q4, 2028-Q2 |
| **G** Steam, juridisch, community | Steam-voorwaarden groen, Discord, devlogs | admin doorlopend; bijspoor 2028-Q4 t/m 2029-Q2 |

Regel: per kwartaal één hoofdspoor (circa 60% van de maker-tijd) en één bijspoor; administratie ≤ 10 uur per kwartaal daarbuiten. Agents mogen meer tegelijk bouwen; de maker beoordeelt niet meer dan twee sporen.

---

## 4. Onderdelen per werkstroom

Uren = maker-tijd (beoordelen, spelen, beslissen). Agentwerk staat er niet bij, behalve bij D (architect-raming). Kwartaal = kalenderplaats uit §5.

### A. Speelbaarheid

| # | Wat | Afhankelijk van | Klaar als | Uren | Kw |
|---|---|---|---|---|---|
| A1 | Speltest ronde 1: 1 proeftester, dan 4-5 spelers via Discord en DM (`playtest.md`) | demo online | `docs/playtest-v1.md` met top-10 knelpunten; ≥ 4 van 5 spelen de eerste wedstrijd zonder hulp uit; ≥ 3 van 5 willen een volgende versie | 15 | 2026-Q4 |
| A2 | Schone-pc-test (gewone pc) en test op de werklaptop | - | Start tot menu ≤ 10 s; eerste wedstrijd en opslaan werken; 2D op 20x ≥ 60 fps op integrated graphics 1080p; minimale systeemeisen genoteerd | 4 | 2026-Q4 |
| A3 | Regressiesuite in één script (sim, determinisme, autotests, smoke, exe) | - | Eén opdracht geeft groen of rood voor een kandidaat; doorlooptijd ≤ 45 min | 3 | 2026-Q4 |
| A4 | Speltest ronde 2 (ronde 1 plus 2 nieuwe) | B1 deel 1 | ≥ 50% van de top-10 uit ronde 1 opgelost of bewust geparkeerd; nieuwe top-10 | 10 | 2027-Q1 |
| A5 | **Hoogtepuntenmodus**: sim rekent vooruit, kansen/goals/kaarten plus omliggende minuten op 1x, rest snel-vooruit; waarom-rapport (3 grootste oorzaken) | A4 | Een wedstrijd in hoogtepuntenmodus duurt 8-12 minuten (mediaan over 20 wedstrijden); ≥ 90% van de goals en grote kansen (xG ≥ 0,2) zit in de hoogtepunten; meetplan §7: ≥ 8 van 10 genoemde oorzaken kloppen volgens de maker | 12 | 2027-Q2 |
| A6 | Speltest ronde 3: 8-10 mensen, deels onbekenden uit Discord, focus managerlaag | F1-F4 | ≥ 6 van 10 spelen een tweede seizoen uit zichzelf; mediaan tijd tussen twee wedstrijden 2-6 min | 12 | 2027-Q4 |
| A7 | 3D-speltests, 2 rondes van 5 testers (itch Restricted) | D3-D5; D6-D7 | Criteria van G10, M6 en M8 gemeten en bewaard | 20 | 2028-Q1, 2029-Q1 |
| A8 | EA-afwerking en Steam Playtest | M9 | 0 blokkerende fouten open; ≥ 8 van 10 nieuwe spelers voltooien de eerste wedstrijd zonder hulp; eerste sessie tot eerste 3D-hoogtepunt ≤ 15 min | 25 | 2029-Q1 |
| A9 | Release-checklist (meerjarenplan §6.3) per release | - | Alle punten groen of benoemd en geaccepteerd | 3 per release | elke release |

### B. Simulatie-realisme

| # | Wat | Afhankelijk van | Klaar als | Uren | Kw |
|---|---|---|---|---|---|
| B0 | **Herkalibratie met de buitenspelfix aan** (loopt; `kalibratie.md` "Buitenspel aan voor RC7") | - | Profwinst tegen amateur ≥ 55% weer als harde grens in `LevelCalibrationTests`; 2,2-3,2 goals per niveaupaar op `--calibrate-levels` 441-480; golden-schotengrens terug naar een verklaarde waarde; startseed opnieuw bepaald; goldens alleen na een verklaarde wijziging | 4 | 2026-Q4 |
| B1 | Positiespel: de 5 kloven (`ai-gap-analyse.md`) | B0 | 2026-Q4: kloof 1 (blok schuift mee) factor ≥ 0,30 en kloof 2 (stilstand) ≤ 10%, beide binnen 1 IQR van echt; kloof 3 gehalveerd. 2027-Q1: alle 5 binnen 1 IQR of gedocumenteerd waarom niet; niveaucontrole en goldens groen zonder verruimde grenzen | 30 | 2026-Q4, 2027-Q1 |
| B7 | Niveaus via systematische fouten, en **zichtbare cijfers die meetbaar werken** | B1 | Passnauwkeurigheid prof 78-85%, halfprof 70-78%, amateur 62-72%; profwinst ≥ 60%; een speler met +4 op een attribuut (schaal 1-20) geeft een meetbaar effect op de bijbehorende maatstaf (bijv. passnauwkeurigheid +3 punten, n = 200 wedstrijden) | 12 | 2027-Q1 |
| B5 | Vermoeidheid kalibreren | B1 | Afstand per veldspeler per 90 min: prof 9,5-11,5 km, halfprof 9-11 km, amateur 8-10,5 km; herstel tussen wedstrijden zichtbaar in de selectie | 6 | 2027-Q1 |
| B-real | Meetplan §1 | B1, B7 | 8 van 10 maatstaven binnen de marge van het referentieprofiel | in B1/B7 | 2027-Q1 |
| B4 | Slimme tegenstander v1 in de competitie | B7 | Speel je 3 keer dezelfde stijl, dan past de tegenstander zich zichtbaar aan met een reden; je winst daalt meetbaar maar blijft > 0; profwinst ≥ 65% | 15 | 2027-Q2 |
| B6 | Spelhervattingen v1 (corner- en vrije-traproutines, zonaal of man-om-man) | tactiekmaker | Aandeel goals uit standaardsituaties binnen de referentieband (meetplan); ≥ 2 routines te kiezen in de editor | 15 | 2027-Q2 |
| B9 | Sim-events voor 3D en geluid: actiestart, balhoogte bij contact, kanswaarde per moment, publieksprikkels; alleen-lezen accessors (`Match.View.cs`), niets in `Snapshot` of `EventLog` | B6 | EventHash en uitslag identiek met en zonder accessors (determinismetest groen); alle events die D en E nodig hebben staan in een tabel | 4 | 2027-Q3 |
| B2/B3 | Training op data, geleerde AI | - | Na EA; G4 in 2029-Q4, standaard no-go | 0 tot EA | 2029+ |

### C. Inhoud en meta

| # | Wat | Afhankelijk van | Klaar als | Uren | Kw |
|---|---|---|---|---|---|
| C1 | Triggers v1 in de tactiekmaker (3-5 typen: "als bal bij hun linksback, zet rechtsbuiten druk") | - | Een trigger is in ≤ 2 min te maken; effect zichtbaar in de meter (≥ 1 maatstaf verschuift buiten de ruis, n = 60) | 12 | 2027-Q1 |
| C2 | Uitdagingen 11-25 in thema's, met 3D-hoogtepunten | B4, B6, D6 | Elke uitdaging geijkt met `--challenge-calibrate` op verse seeds; haalbaar met ≥ 2 verschillende tactieken; 1 ster bij 40-70% van de serieuze pogingen | 10 | 2028-Q4 |
| C3 | Vriendenmodus compleet (deelcode, speel tegen de tactiek van een vriend, ladder zonder server) | determinisme op 2 pc's | 2 vrienden spelen een ladder van 5 rondes met identieke uitslagen | 10 | EA-update 2029-Q3 |

### D. 3D-weergave, coach en scènes

Technische uitwerking: [3d-architectuur.md](3d-architectuur.md) (T0-T10). Alle 3D leest de simulatie via één `MatchDriver`; het beeld mag nooit `Match.Step` aanroepen; dezelfde seed geeft dezelfde EventHash in headless, 2D en 3D.

| # | Wat | Afhankelijk van | Klaar als | Uren | Kw |
|---|---|---|---|---|---|
| D0 | Architectuurbesluiten (T0): min-spec laptop en gpu's, renderer-kandidaten, coördinaatconventie, G3-datum | B9 concept | Vastgelegd in `Besluiten`; architect-rapport bijgewerkt | 2 | 2027-Q2 |
| D1 | **Haalbaarheidsproef** (T1-T4, timebox 3 weken agentwerk): `MatchDriver` uit `Main.cs`, snapshotbuffer, capsule-3D, één MakeHuman-model met 10 Mixamo-clips, coach-POV v0, publieksmodel v0, renderervergelijking | D0, B9 | **G3-criteria** (§6) | 10 | 2027-Q3 |
| D2 | Stijl- en assetkeuze, licentieregister `docs/assets-licenties.md`, één humanoid-skelet (SkeletonProfileHumanoid), retargeting-testscènes per bron | G3 go | ≤ €300 uitgegeven; 0 assets zonder vastgelegde licentie; AI-assets gemarkeerd; per bron een testscène met 5 clips zonder kapotte schouders of voeten (oordeel maker) | 10 | 2027-Q4 |
| D3 | 3D-wedstrijd basis: veld 105x68, 3 stadiontypen (sportpark, halfprof, prof), 22 spelers in clubtenue (shader-parameter uit `ClubIdentity.Kits`), bal met hoogte, scheidsrechter en assistenten (render-side), tv-camera en tactische camera, hoogtepuntenmodus in 3D | D2, A5 | Hash headless = 2D = 3D op 20 seeds; getekende positie p95 ≤ 0,5 m van de sim-positie; wedstrijd laden ≤ 10 s op de werklaptop, ≤ 5 s op de ontwikkel-pc; hoogtepunten uit A5 spelen af in 3D | 18 | 2028-Q1 |
| D4 | Animatieset v1 (≥ 25 clips: locomotie 4 richtingen, pass, schot, kopbal, aanname, dribbel, tackle, sliding, vallen, keeper duik/vang/uittrap, inworp, juichen, teleurgesteld, scheidsgebaren) met AnimationTree, locomotie op snelheid, lookahead van 4 ticks voor trapmomenten | D3 | ≥ 25 clips in gebruik; in 10 hoogtepuntclips ≥ 8 zonder storende fout (verkeerde clip, glijden op 1x, door de bal heen) volgens 3 kijkers; oogtest "beweging" ≥ 3,0/5 | 20 | 2028-Q1 |
| D5 | **Coach-creator en coach-POV**: naam, huidtint (8), lengte, bouw (3), kapsel (≥ 8), haarkleur, baard, bril, kleding (≥ 10 combinaties in clubkleuren); POV first-person en over-de-schouder, dug-out zittend; minimap via SubViewport; B-bijsturing opent het notitieboek in de handen van de coach; wissel- en tactiekwijzigingsgebaren | D3 | Coach maken ≤ 3 min; ≥ 6 lichaamspresets; FOV instelbaar; 0 van 5 testers misselijk na 10 min POV; ≥ 3 van 5 zeggen vanuit POV goed of de eigen linie hoog of laag staat; één toets wisselt camera ≤ 0,5 s | 15 | 2028-Q2 |
| D10 | **Korte 3D-scènes**: kleedkamer (teampraatje voor en na), persconferentie, dug-out; coach-animaties (staan, wijzen, klappen, zitten) | D5, F4 | Elke scène ≤ 20 s zonder invoer en overslaanbaar; laadtijd per scène ≤ 3 s; persconferentie-UI (F4) werkt in de scène | 10 | 2028-Q3 |
| D6 | Live wisselen 2D ↔ 3D; replay en "kijk nog eens" (laatste 64 ticks) in 3D; replayhoek achter het doel bij goals | D3 | Wisselen ≤ 1 s zonder verandering van de uitslag (hash gelijk); replayhoek bij ≥ 90% van de goals | 6 | 2028-Q3 |
| D7 | Prestaties en presets (Laag/Midden/Hoog), LOD-keten (15k/4k/1,5k tris), publiek als MultiMesh, gebakken licht, GC-meting, systeemeisen | D4, D5 | Ontwikkel-pc 1080p Hoog ≥ 60 fps; **werklaptop (Iris Xe-klasse) 1080p Laag gemiddeld ≥ 60 fps, 1% low ≥ 45**; oudere integrated (UHD 620-klasse) ≥ 30 fps of automatisch 2D; pakket ≤ 1 GB; RAM < 1,5 GB, VRAM < 1 GB; geen geheugengroei > 10% over een volle wedstrijd | 10 | 2028-Q3 |
| D8 | Animatieset v2 (≥ 50 clips) en sfeer: publieksmodellen, wisselbank, warming-up, grensrechters | D4 | ≥ 50 clips; oogtest "voelt als een voetbalgame" ≥ 3,5/5 bij 3 kenners en 5 hoogtepuntreeksen | 12 | 2028-Q4 |
| D11 | **Stadion groeit mee** (3 typen × 2 stadia: hekjes → tribune → overdekt → groot), opkomst uit fanvertrouwen (F5), tenues en emblemen uit PNG (F11) op spelers, vlaggen en spandoeken; **weer, tijdstip en seizoen** (dag, avond onder lampen, regen, winterlicht) | D7, F5, F11 | ≥ 6 stadionvarianten; opkomst en publieksdichtheid volgen F5; ≥ 4 weer/licht-combinaties; fps-doelen van D7 blijven gehaald in de zwaarste combinatie | 10 | 2028-Q4 |
| D9 | Animatieset naar ≥ 80 clips, coach loopt langs de lijn, polish | D8 | ≥ 80 clips; fps-doelen D7 gehaald | 8 | EA-update 2029-Q3 |

### E. Geluid (geen commentaarstemmen)

| # | Wat | Afhankelijk van | Klaar als | Uren | Kw |
|---|---|---|---|---|---|
| E0 | Bestaande audio beluisteren; licentieregister starten (effecten zijn gegenereerd en nog niet beoordeeld) | - | Maker heeft alles gehoord; per bestand bron, licentie en AI-herkomst vastgelegd | 1 | 2026-Q4 |
| E1 | **Dynamisch publiek, ook in 2D**: `CrowdModel` (puur C#, deterministisch uit events, stand, minuut, balgevaar) met intensiteit 0-1 en one-shots (ooh, juichen, fluitconcert, zucht, applaus); 3 lussende bedden in `AudioManager` | B9 deels | ≥ 7 reactietypen; reactie ≤ 0,5 s na het event; blindtest: ≥ 4 van 5 testers horen verschil tussen 0-0 in minuut 10 en 2-2 in minuut 88; 0 bronnen zonder licentie | 8 | 2027-Q2 |
| E2 | Positioneel 3D-geluid: `AudioStreamPlayer3D` op bal, scheids, doelnet; 4 tribune-emitters; luisteraar op camera of coachhoofd | D3 | ≥ 4 van 5 testers wijzen links/rechts goed aan met gesloten ogen; 0 afspeelfouten in de log over een volle wedstrijd | 6 | 2028-Q1 |
| E3 | Gezangen per club: 6-10 eigen sjablonen (eigen opnames met vrienden, gelaagd), per club deterministisch gekozen met toonhoogte en tempo uit het club-id; start bij spanning of voorsprong, stopt bij tegengoal | E1, F5 | ≥ 6 sjablonen opgenomen; elke profclub heeft een herkenbaar gezang; geen bekende melodie (controle door 3 luisteraars); licenties 100% | 10 | 2028-Q2 |
| E4 | Mix: bussen Master/Music/Sfx/Crowd, aparte schuiven, luidheidsdoel, ducking bij menu's | E1-E3 | Gemiddelde luidheid binnen ±2 LU van het doel; geen clipping in een volle wedstrijd; schuiven werken per bus | 5 | 2028-Q3 |

### F. Managerlaag (geen contracten, geen onderhandeling, geen financiële simulatie)

| # | Wat | Afhankelijk van | Klaar als | Uren | Kw |
|---|---|---|---|---|---|
| F0 | Ontwerpdocument managerlaag: grenzen, "niet bouwen"-lijst per onderdeel, datamodel (wereld, club, speler met cijfers 1-20, staf, coach), opslagformaat (uitbreiding `zeus_carriere.json`, versie-upgrade via `UserSaveFile`), bewerkbare JSON-data | besluit 3 in `Besluiten` | Maker keurt goed; elk F-onderdeel heeft een "niet bouwen"-lijst; datamodel in `docs/managerlaag.md` | 5 | 2027-Q1 |
| F9 | **Wereld v1**: 4 fictieve landen, 2-3 divisies (eigen land 3), 16-18 clubs per divisie, nationale beker per land, 1 Europees toernooi (32 clubs), promotie/degradatie, kalender; generatie van ~150 clubs en ~3000 spelers met fictieve namen per land | F0 | Deterministische generatie uit een seed; een seizoen voor alle competities rekent headless in ≤ 10 min op de ontwikkel-pc; `CompetitionTests` groen; geen naam die een echte club of speler nabootst (controle met een woordenlijst) | 10 | 2027-Q3 |
| F1 | **Selectie en carrière v1**: meerdere seizoenen, ontwikkeling en veroudering, blessures, schorsingen, stoppende spelers en instroom, zichtbare cijfers 1-20 | F0, F9, B5 | De maker speelt 3 seizoenen achter elkaar zonder blokkerende fout; headless 100 carrières × 10 seizoenen: gemiddelde cijfers per divisie blijven binnen ±10% van seizoen 1; oude opslagversies laden | 18 | 2027-Q3 |
| F2 | **Bestuur**: seizoensdoel (bestaat), bestuursvertrouwen 0-100 met redenen, waarschuwing, ontslag, nieuwe club kiezen uit ≥ 3 aanbiedingen; **coachreputatie** 0-100 | F1 | Elke wijziging van vertrouwen en reputatie toont een reden; headless: 20-40% ontslagen na twee keer het doel missen, ≤ 5% bij halen; aanbiedingen passen bij de reputatie (geen profclub bij reputatie < 40) | 12 | 2027-Q4 |
| F3 | **Transfers-lite**: vaste prijs per speler (uit cijfers, leeftijd, divisie), één transferbudget per seizoen, 2 periodes, AI-clubs kopen en verkopen | F1 | Kopen of verkopen in ≤ 3 klikken; geen contract-, salaris- of onderhandelingsscherm; na 10 seizoenen headless heeft geen club > 50% van de top-10-spelers van een divisie; prijzen stijgen monotoon met de cijfers | 12 | 2027-Q4 |
| F4 | **Media**: persconferentie voor en na de wedstrijd (3-4 vragen, 3 antwoorden), effect op moraal, bestuur en fans; later in de 3D-scène (D10) | F2 | ≤ 90 s per persconferentie; v1 ≥ 30 vragen, EA ≥ 80, 1.0 ≥ 150; dezelfde vraag niet binnen 5 wedstrijden; overslaan kan altijd | 12 | 2027-Q4 |
| F10 | **Training en staf**: trainingsfocus per week (tactiek, conditie, standaardsituaties, individueel); assistent, scout en fysio als personen met niveau 1-20; assistent neemt training over, scout verbetert tegenstanderinfo in de TEST-tab, fysio verkort blessures | F1 | Elke focus heeft een meetbaar effect (vertrouwdheid, conditie of blessureduur; n = 100 carrières); stafniveau verschuift dat effect ≥ 20%; staf wisselen in ≤ 3 klikken, geen salarisscherm | 8 | 2027-Q4 |
| F12 | **Coachstijl en vaardigheden**: 3-4 stijlen (motivator, tacticus, opleider, ...) met kleine uitgelegde effecten; groei met ervaring | F2 | Elk effect staat in één zin in het spel en is meetbaar (n = 100 carrières, effect 5-15% op de genoemde maatstaf); geen stijl wint > 55% van de onderlinge vergelijkingen | 6 | 2028-Q2 |
| F5 | **Fans**: fanvertrouwen en opkomst, gekoppeld aan geluid (E1/E3), stadion (D11) en bestuur | F2, E1 | Opkomst en publieksintensiteit volgen het fanvertrouwen met zichtbare reden | 6 | 2028-Q2 |
| F6 | Inbox en nieuwsfeed, seizoensoverzicht, Europese resultaten | F1-F5 | Mediaan tijd tussen twee wedstrijden bij testers 2-6 min | 8 | 2028-Q2 |
| F7 | Balans en lange run | F1-F6 | 100 carrières × 10 seizoenen zonder crash; alle grenzen uit F1-F3, F10, F12 groen | 8 | 2028-Q2 |
| F11 | **Mods en data**: competitie- en clubdata als leesbare JSON in een modmap, tenues en emblemen als PNG, validatie met duidelijke foutmelding, deelcode voor tactieken | F9 | Een gewijzigde club (naam, kleuren, embleem-PNG) verschijnt zonder herstart in 2D en 3D; kapotte JSON geeft een melding met regelnummer en het spel start toch; `docs/mods.md` | 6 | 2028-Q3 |

### G. Steam, juridisch, community (SJ1-SJ12)

| # | Wat | Afhankelijk van | Klaar als | Uren | Kw |
|---|---|---|---|---|---|
| SJ1 | Merkcontrole "Gaffer's Notebook" (EUIPO, BBIE, USPTO, klassen 9/28/41), zelf | - | Uitkomst in `Besluiten`; bij een botsing start de naamzoektocht vóór de Steam-pagina | 4 | 2026-Q4 |
| SJ2 | Datatoestemming: Discord-berichten naar de vault; conceptmails versturen of bewust niet | - | Kopie in `02 Techniek`; in `Besluiten` vastgelegd wat is toegestaan | 2 | 2026-Q4 |
| SJ12 | **Discord-server en devlogs**: kanalen (aankondigingen, bugs, ideeën, builds), regels, eerste devlog op itch | - | Server staat; elke release krijgt een devlog ≤ 300 woorden binnen een week | 4 + 1 per release | 2026-Q4 |
| SJ3 | StatsBomb-vrij | B | Geen parameter met StatsBomb-herkomst in `sim/` (`bronvervanging-beta.md`) | 3 | 2027-Q2 |
| SJ4 | Werkgever: cao/contract over nevenwerk en IP | - | Nagegaan en zo nodig gemeld vóór de eerste uitgave voor Steam | 2 | 2027-Q3 |
| SJ5 | AI-logboek en register van asset- en audiolicenties | D2, E0 | Per release bijgewerkt; 0 onbekende herkomsten | 1 per kw | vanaf 2027-Q4 |
| SJ7 | Ondernemingsvorm: eenmanszaak, KvK, btw/KOR | SJ4 | Ingeschreven of datum vastgelegd | 6 | 2028-Q3 |
| SJ8 | G5: Steam ja/nee en prijs (€9,99-14,99) | M7 | Besluit in `Besluiten` | 2 | 2028-Q2 |
| SJ9 | Steamworks: aanmelden, fee $100, verificatie (10-15 werkdagen), storepagina zonder echte namen, trailer met 2D en 3D, AI-disclosure; Coming Soon ≥ 3 maanden vóór EA | SJ7, SJ8 | Coming Soon publiek | 15 | 2028-Q4 |
| SJ10 | Steam Playtest en build-review | SJ9, M9 | Build en pagina goedgekeurd door Valve | 8 | 2029-Q1 |
| SJ11 | EA-lancering en de eerste 4 weken (alleen fixes) | G6 | Gelanceerd; updateritme 6-8 weken vastgelegd | 20 | 2029-Q2 |

SJ6 (jurist) vervalt door besluit 28. Risico staat in §8.

**Uren per werkstroom tot EA:** A ≈ 125, B ≈ 115, C ≈ 22, D ≈ 125, E ≈ 30, F ≈ 115, G ≈ 80. Samen ≈ 610 uur plus ≈ 60 uur release-checklists en devlogs = **≈ 670 uur = 7,5 kwartalen van 90 uur**. Daarom ligt EA op zijn vroegst in 2029-Q2.

---

## 5. Kwartaalplanning

| Kw | Hoofdspoor | Bijspoor | Admin (≤ 10 u) | Release | Go/no-go | Maker-uren |
|---|---|---|---|---|---|---|
| **2026-Q4** | A: A1, A2, A3 | B: B0, B1 deel 1 | SJ1, SJ2, SJ12, E0 | **0.2** (update van de publieke itch-demo, 2D) | **G1** eind dec | ≈ 55 |
| **2027-Q1** | B: B1 af, B7, B5 | C: C1; A4 | F0 | **0.3** | - | ≈ 65 |
| **2027-Q2** | B: B4, B6 | A+E: A5 hoogtepuntenmodus, E1 publiek | SJ3, D0 | **0.4** open-bètakandidaat | **G2** eind juni | ≈ 62 |
| **2027-Q3** | F: F9 wereld, F1 selectie | D: D1 haalbaarheidsproef; B9 | SJ4 | **0.5** (Public-update itch; laatste met NL) | **G3** eind sep | ≈ 60 |
| **2027-Q4** | F: F2, F3, F4, F10 | D: D2 assets; A6 speltest 3 | SJ5 | **0.6** | - | ≈ 70 (krap) |
| **2028-Q1** | D: D3, D4 | E: E2; A7 ronde 1 | SJ5 | **0.7** eerste 3D-build (itch Restricted) | **G9** managerlaag | ≈ 62 |
| **2028-Q2** | D: D5 coach | F: F12, F5, F6, F7; E3 | SJ8 | **0.8** | **G10** 3D-kwaliteit, **G5** Steam | ≈ 65 |
| **2028-Q3** | D: D10 scènes, D6, D7 | F: F11 mods; E4 | SJ7 | **0.9** | - | ≈ 55 |
| **2028-Q4** | D: D8, D11 stadion en weer | C: C2; G: SJ9 Coming Soon | - | **0.10** inhoud compleet | - | ≈ 60 |
| **2029-Q1** | A: A8 afwerking, A7 ronde 2 | G: SJ10 Playtest | - | **0.11** EA-kandidaat; feature freeze 6 weken vóór EA | **G6** 4 weken vóór EA | ≈ 60 |
| **2029-Q2** | G: SJ11 lancering | A: fixes | - | **Early Access** (P50) | - | ≈ 40 + fixes |
| 2029-Q3 | EA-update 1: C3 vriendenmodus, D9 | balans, F4 ≥ 150 vragen | - | EA-update 1 | - | - |
| 2029-Q4 | EA-update 2: inhoud | B2-verkenning (optioneel) | - | EA-update 2 | **G4** geleerde AI | - |
| 2030-Q1 | Afronding 1.0 | afwerking | - | **1.0** (P50) | **G8** | - |
| 2030-Q2 | - | - | - | - | **G7** live 1-tegen-1 | - |

Het restant tot 90 uur per kwartaal is buffer voor vakanties, toetsweken en speelavonden. Schuift 2027-Q4, dan schuift alles erna een kwartaal; dat is ingecalculeerd in de P90-data.

### Budget (≤ €500 per jaar)

| Jaar | Uitgaven | Opmerking |
|---|---|---|
| 2026 | €0 | Merkcontrole zelf; Discord gratis |
| 2027 | D1 ≤ €50 (liefst CC0), D2 asset packs ≤ €300, audio ≤ €100 | D2 pas na G3 go |
| 2028 | Steam-fee $100 (≈ €95), extra animaties ≤ €200, audio en opnames ≤ €100, KvK (bedrag verifiëren) | Geen jurist (besluit 28) |
| 2029 | Updates en assets ≤ €300; reserve | Steam-fee terug bij $1.000 omzet |

---

## 6. Mijlpalen en go/no-go-momenten

### Mijlpalen

| M | P50 / P90 | Versie | Harde criteria |
|---|---|---|---|
| **M1** Eerste mensen | 2026-12 / 2027-02 | 0.2 | A1 verwerkt in `playtest-v1.md`; ≥ 4 van 5 voltooien de eerste wedstrijd zonder hulp; schone-pc-test geslaagd; B0: profwinst ≥ 55% weer hard; regressiesuite in één script; Discord staat |
| **M2** Lijkt op voetbal | 2027-03 / 2027-09 | 0.3 | Oogtest ≥ 3,0/5 (3 kenners, 5 replays); 8 van 10 realisme-maatstaven; kloven 1-5 binnen 1 IQR; profwinst ≥ 60%; stilstand ≤ 10%; meeschuiven 0,30-0,45; cijfers 1-20 werken meetbaar |
| **M3** Open bèta Public | 2027-06 / 2027-12 | 0.4/0.5 | `open-beta.md` criteria 1-7; G2 1-5; StatsBomb-vrij; profwinst ≥ 65%; hoogtepuntenmodus 8-12 min; publiek reageert hoorbaar |
| **M4** 3D go en wereld v1 | 2027-09 / 2027-12 | 0.5 | G3 go; F9 en F1 klaar; de maker speelt 3 seizoenen in 4 landen zonder blokkerende fout |
| **M5** Managerlaag v1 | 2027-12 / 2028-06 | 0.6 | F2, F3, F4, F10 klaar; A6: ≥ 6 van 10 spelen een tweede seizoen; ≥ 30 persvragen; staf en training meetbaar |
| **M6** Eerste 3D-wedstrijd | 2028-03 / 2028-09 | 0.7 | D3, D4 klaar; hash 2D = 3D = headless; ≥ 25 clips; oogtest beweging ≥ 3,0; werklaptop ≥ 60 fps Laag; laden ≤ 10 s; positioneel geluid |
| **M7** Coach en Steam-besluit | 2028-06 / 2028-12 | 0.8 | D5 coach-creator en POV; F5-F7, F12; 100 × 10 seizoenen zonder crash; G10 gehaald; G5 go |
| **M8** Scènes, prestaties, mods | 2028-09 / 2029-03 | 0.9 | D10, D6, D7, E4, F11 klaar; fps-doelen D7; pakket ≤ 1 GB |
| **M9** Inhoud compleet, Coming Soon | 2028-12 / 2029-06 | 0.10 | D8, D11, C2 klaar; ≥ 50 clips; oogtest "voetbalgame" ≥ 3,5/5; stadion groeit; weer; licentieregister 100%; Coming Soon publiek |
| **M10** Early Access | 2029-Q2 / 2030-Q1 | EA | G6 go; A8: ≥ 8 van 10 nieuwe spelers voltooien de eerste wedstrijd zonder hulp; Playtest zonder blokkerende fout; eerste 4 weken alleen fixes |
| **M11** 1.0 | 2030-Q1 / 2030-Q4 | 1.0 | G8 go; ≥ 80 clips; ≥ 150 persvragen; ≥ 25 uitdagingen; vriendenmodus; geen blokkerende fout in de laatste 2 EA-updates; Steam-reviews ≥ 70% positief op ≥ 50 reviews (indicator, geen harde eis) |

### Go/no-go-momenten

| G | Wanneer | Go als | Anders |
|---|---|---|---|
| **G1** | eind dec 2026 | Oogtest ≥ 3,0 **en** ≥ 3 van 5 testers willen verder | Oogtest < 3,0: 2027-Q1 alleen B1. Kern niet begrepen: concept heroverwegen |
| **G2** | eind juni 2027 | Open-bètavoorwaarden 1-5 (meerjarenplan) | Kwartaal later |
| **G3** | eind sep 2027 | (a) 22 geanimeerde spelers, bal en scheids: ≥ 60 fps ontwikkel-pc en ≥ 60 fps Laag op de werklaptop (≥ 30 bij UHD 620-klasse); (b) ≥ 3 van 5 testers vinden de tv- of tactische 3D-camera minstens zo leesbaar als 2D en POV "leuk" of "bruikbaar"; (c) alle proef-assets met vastgelegde licentie die verkoop toestaat; (d) herraming T5-T10 door de architect ≤ 3 kwartalen agentwerk en ≤ 110 maker-uren; (e) EventHash ongewijzigd door `MatchDriver` | Eenvoudigere stijl of kleinere D-scope, G3 opnieuw in 2027-Q4. `MatchDriver` en `CrowdModel` blijven sowieso |
| **G9** | eind 2028-Q1 | ≥ 3 van 5 testers (A6/A7) noemen bestuur, media of staf als positief; mediaan tijd tussen wedstrijden ≤ 6 min | F snoeien: media en staf naar na EA; bestuur en transfers-lite blijven |
| **G10** | eind 2028-Q2 | M6 gehaald plus D5-criteria | **Een kwartaal extra D; alles erna schuift** (besluit 25) |
| **G5** | 2028-Q2 | Steam-voorwaarden (meerjarenplan §3.1) groen of met datum; M7 | Geen fee; EA kwartaal later |
| **G6** | 4 weken vóór EA | §3.1 groen, release-checklist groen, Playtest zonder blokkerende fout, de maker speelt het zelf nog graag | Per maand uitstellen |
| **G4** | 2029-Q4 | Eerste geleerde onderdeel beter op de toetsset, deterministisch, alleen toegestane data | Gekalibreerde AI blijft |
| **G8** | 2030-Q1 | EA-feedback stabiel; M11 | EA verlengen met nieuw plan |
| **G7** | 2030-Q2 | Maker wil het **en** ≥ 20% van de EA-spelers gebruikt de asynchrone vriendenmodus | Geen live multiplayer |

---

## 7. Technische 3D-lijn (samenvatting van [3d-architectuur.md](3d-architectuur.md))

- **De sim verandert niet.** Per tick (0,1 s) levert ze positie, snelheid en heading van 22 spelers, bal met hoogte, stand, periode en een eventlog. Animatie wordt in Godot afgeleid uit snelheid, heading en events. Scheidsrechter, publiek en coach zijn puur beeld.
- **Eerst `MatchDriver`** uit `Main.cs` halen (sim-lus, skip, highlights, replay, eventverwerking). 2D en 3D zijn daarna alleen abonnees. Poort: autotests `4_match`, `8a`, `9e`, `11a` PASS en EventHash per seed gelijk aan vóór de refactor.
- **Snapshotbuffer met 4 ticks vertraging** zodat pass- en schotanimaties op tijd inzetten; Hermite-interpolatie op positie, kortste hoek op heading; teleports bij hervattingen knippen.
- **Snelheden:** 1x (hoogtepunten) en 4x volle animatie; 12x/20x tactische camera, animatietempo afgetopt, voetglijden geaccepteerd.
- **Assets:** MakeHuman/MPFB2 (CC0) voor spelers en coach-maker; Mixamo (Adobe-account, commercieel gebruik toegestaan, ruwe bestanden niet herverdelen) en Quaternius (CC0) voor animaties; Kenney (CC0) voor placeholders; Sketchfab alleen CC0/CC BY; Fab-packs €20-80 per stuk. Eén `SkeletonProfileHumanoid`, BoneMap-retargeting, animaties als gedeelde `AnimationLibrary`, geen rootmotion.
- **Coach:** camera op ooghoogte in de technische zone; yaw ±110°, pitch -30/+20°; "volg de bal" als gedempte veer; minimap via SubViewport; B-bijsturing brengt het notitieboek in beeld en opent het bestaande `PauseMenu`. Opslag per carrière in `zeus_carriere.json` (besluit 17).
- **Geluid:** `CrowdModel` (puur C#) → `AudioManager` met 3 bedden en one-shots; in 3D `AudioStreamPlayer3D` op bal, scheids, doelnet en 4 tribune-emitters; bus Crowd erbij.
- **Prestaties:** renderer meten in T4 (project staat op `gl_compatibility`); LOD 15k/4k/1,5k; één materiaal per spelerstype, tenue via instance uniform; publiek als MultiMesh; gebakken licht; geen allocaties per frame.
- **Testen:** `Match3D` in `smoke_godot.py`; autotest `17a_match3d.json` met nieuwe stap `expect_fps`; determinismetest 2D vs 3D; unittests voor `VisualAdapter` en `CrowdModel` in `godot/tests`.
- **Mijlpalen T0-T10:** ≈ 40-50 agent-dagen en ≈ 50-60 maker-uren (de uren zijn in D0-D9 verwerkt).

---

## 8. Risico's en maatregelen

| Risico | Kans | Impact | Maatregel |
|---|---|---|---|
| De maker is het knelpunt (≈ 670 uur werk tegen 90 per kwartaal) | Hoog | Hoog | Max 2 sporen; elk agentresultaat eindigt met "wat moet de maker zien"; G9/G10 laten snoeien toe; buffer per kwartaal; P90-data |
| Animatie en simulatie lopen uit elkaar (voetglijden, trap zonder bal) | Hoog | Hoog | Lookahead van 4 ticks; p95 ≤ 0,5 m; ≤ 2 fouten per 10 clips; de sim gaat altijd voor |
| 3D tast het determinisme aan | Middel | Hoog | Hash-test headless/2D/3D bij elke merge; beeld leest alleen; grep-test dat views nooit `Match.Step` aanroepen |
| `Main.cs`-refactor breekt de wedstrijdflow en autotests met klikcoördinaten | Middel | Hoog | Alleen verplaatsen, geen gedrag wijzigen; EventHash en autotests als poort |
| Assetlicenties (NC per ongeluk, Fab alleen-Unreal, Mixamo-herverdeling) | Middel | Hoog | Register vóór aankoop; alleen CC0/CC BY/betaald-engine-vrij; niets kopen vóór G3 |
| Retargeting van gemengde bronnen geeft kapotte schouders en voeten | Hoog | Middel | Eén humanoid-profiel; per bron een testscène; liever 2 bronnen goed dan 5 half |
| Werklaptop haalt 60 fps niet | Middel | Middel | Presets en renderschaal; renderer-meting in T4; 2D blijft de lichte weergave; eerlijke systeemeisen |
| Dubbel onderhoud 2D en 3D | Hoog | Middel | Eén `MatchDriver`, dezelfde events; gedeelde HUD-panelen; 2D-tests blijven in de suite |
| Managerlaag groeit uit tot FM (scope) | Hoog | Middel | F0 met "niet bouwen"-lijst; G9; geen contracten, onderhandeling, financiën, jeugd |
| Wereld van 150 clubs is niet te balanceren | Middel | Middel | F9 deterministisch en headless meetbaar; F7 100 × 10 seizoenen; grootte pas vergroten als F7 groen is |
| Geen jurist: merk of datalicentie blokkeert Steam | Middel | Hoog | SJ1 en SJ2 in 2026-Q4; naam kan nog veranderen tot SJ9; data per bron vervangbaar; risico geaccepteerd (besluit 28) |
| Gezangen raken bestaande melodieën | Laag | Middel | Alleen eigen opnames; controle door 3 luisteraars |
| Werkgever en IP (deels gebouwd op de werklaptop) | Laag | Hoog | SJ4 vóór de eerste uitgave voor Steam |
| Motivatie over 3,5 jaar | Middel | Hoog | Elke 3 maanden een release en een devlog; Discord; pauzes zonder schuldgevoel |
| Godot 4.7 .NET-3D niet sterk genoeg voor 22 geanimeerde spelers in semi-realisme | Middel | Hoog | D1 toetst dit vóór uitgaven; alternatief alleen via de architect en een makerbesluit |
| Steam-regels voor AI-disclosure veranderen | Middel | Middel | AI-logboek per release; lezen bij SJ9 |
| Alleen Engels: Nederlandse testers haken af | Laag | Laag | NL blijft in de demo tot 0.5; Discord in het Nederlands mag |

---

## 9. Bewust niet bouwen

- Commentaarstemmen (5); gezichtsanimatie en lipsync (2); eigen motion capture.
- Contracten, salarissen, onderhandelen, makelaars, financiën buiten één transferbudget en vaste prijzen (3).
- Zelf spelers besturen (1); jeugdopleiding (19); FM-trainingsschema's (19).
- Echte clubs, spelers en competities; officiële import van echte data (mods door spelers zijn hun zaak, 20).
- Beloopbare 3D-club (21); fotorealisme (2).
- Live multiplayer vóór 1.0 (16); mobiel, VR, Mac, Linux en Steam Deck bij EA (10).
- Vrouwenvoetbal en jeugdteams in het ontwerp (Besluiten 2026-10-12).
- Stadionbouw als spelmechaniek, sponsors, kaartverkoop (stadion groeit automatisch met niveau, 18).
- Telemetrie of automatisch versturen van gegevens.
- Geleerde AI vóór EA (30); 3D in de gratis itch-demo (29).
- Jurist (28); Nederlands na 0.5 (23).

---

## 10. Eerste stappen (2026-Q4, vanaf 2026-10-19)

1. **B0 herkalibratie** op branch `demo/recal` (loopt; basismeting counter prof-amateur 40% winst = FAIL, dat is het werkpunt).
2. **B1 deel 1**: kloof 1 en 2 op een eigen branch, meten met `--positional-probe` en `--calibrate-levels`.
3. **Maker:** A1 proeftester kiezen; Discord-server aanmaken (SJ12); merkcontrole (SJ1); Discord-berichten naar de vault (SJ2); audio beluisteren (E0); schone-pc-test op de gewone pc (A2).
4. **0.2** eind december: A1-fixes plus B0/B1 deel 1, dan G1.

*Volgende herziening: na G1 (januari 2027), daarna elk kwartaal en na elk go/no-go.*
