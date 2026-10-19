# Gaffer's Notebook: technische lijn van 2D naar semi-realistische 3D met coach-POV

Opgesteld 2026-10-18 door een architect-agent (ECC) vanaf `C:\dev\zeus-rc4` (branch `demo/recal`), alleen gelezen, niets gebouwd. Planning, criteria en volgorde staan in [roadmap-naar-1.0.md](roadmap-naar-1.0.md) (werkstroom D, mijlpalen T0-T10). Elke uitspraak over bestaande code verwijst naar bestand:regel op commit `7d15acf`; regelnummers verouderen, de bestandsnamen niet. Wordt bijgewerkt bij D0 (2027-Q2).

Makerbesluiten die dit document volgt: coach als character met POV en vrije camera's (tv-camera standaard); semi-realistisch met ingekochte packs, geen gezichtsanimatie; 2D blijft naast 3D op dezelfde sim; stadiongeluid zonder commentaar; hoogtepunten plus sleutelmomenten als wedstrijdmodus; één coach per carrière; Mixamo via het Adobe-account; ontwikkel-pc met losse gpu, werklaptop (integrated) als ondergrens.

## 1. Wat de simulatie nu levert

| Gegeven | Waar | Opmerking voor 3D |
|---|---|---|
| Tickduur 0,1 s (10 Hz) | Match.cs:4 (`StepSeconds = 0.1`) | Interpolatie nodig, het beeld draait op 60 fps |
| Speler: team, id, Pos (2D), Vel (2D), Heading (rad), kaarten, keeper, energie | Match.cs:96-98 (`PlayerSnapshot`) | Heading is lichaamsrichting; geen kijkrichting van het hoofd |
| Bal: Pos en Vel als Vec3 (Z = hoogte) | Match.cs:104 (`BallSnapshot`), Ball.cs:19 | Kopballen en hoge ballen af te leiden |
| Tick, tijd, status, stand, periode, kantwissel | Match.cs:107-109 | `EndsSwapped` nodig voor de cameraspiegeling |
| Snapshot opvragen | Match.cs:869-872 (`GetSnapshot`, nieuwe arrays per aanroep) | Bij 20x ongeveer 200 snapshots per seconde: GC-druk meten |
| Events: KickOff, Pass, BallControl, Interception, Duel, Foul, Card, Out, RestartTaken, Shot, Goal, Save, Offside, FirstTouch, Substitution, ... met tick, speler en locatie | Events.cs:7-14 | Balcontactmomenten op tick en speler |
| Publieke accessors: Tick, Status, stand, Ball, Possession, CarrierId, PhaseOf, TargetOf | Match.cs:382-406 | `CarrierId` geeft de dribbelanimatie; `PhaseOf` sfeer en camera |
| Popup-categorieën (Goal, kaarten, penalty, corner, rust, ...) | MatchNarrator.cs:9 | Direct bruikbaar voor publiek en geluid |
| Veldmaat 105 x 68 m | Pitch.cs:8-9 | Eén Godot-eenheid = 1 m |
| Penalty: keeper duikt via een snelheidsimpuls | Match.Rules.cs:230-236 | Duik af te leiden uit keepersnelheid plus Save-event |
| Interne intentie per speler (Pass, Shoot, Press, Scan, ...) | Brain.cs:3, privé veld Match.cs:155 | Niet publiek; nuttig maar niet nodig (§2.5) |
| Spelhervatting: nemer, reden, tot-tick | privé velden Match.cs:190-192 | Inworp- en cornerpose uit Out/RestartTaken-events |

**Ontbreekt voor 3D, allemaal aan de Godot-kant op te lossen:** animatietoestand (uit |Vel|, hoek tussen Vel en Heading, CarrierId en events); hoofd- en kijkrichting (render-side, kijkt naar de bal met beperkte nekhoek); trapmoment vooraf (snapshotbuffer, §2.3); scheidsrechter en assistenten (Referee.cs:17 is een statische regelklasse, geen entiteit; render-side diagonaalsysteem); publiek, coaches en bank (puur beeld); lengte en bouw per speler (uit squad-data of deterministisch uit het id).

## 2. Architectuur

### 2.1 Huidige stand

- Main.cs:1: "Display only: reads Zeus.Sim snapshots, no game logic." Main.cs is een Node2D (Main.cs:9) die ook de sim-lus met accumulator (Main.cs:468-500), skip (271-302), highlights via hersimulatie (395-466), replay-verificatie (550-565), eventverwerking en audio (530-542) en al het tekenen (567-634) bevat.
- Snelheid 20/12/4 uit de instellingen (SettingsMenu.cs:17), toegepast als `_acc += delta * _speed` (Main.cs:487).
- Highlights: `Highlights.Pick` geeft `Moment(StartTick, EndTick, Label)` (Highlights.cs:9, 18-42), afgespeeld op 1x (Main.cs:472), met determinismebewaking (Main.cs:454-455).
- Replay: `ReplayFile` met invoer plus commandotijdlijn, gebonden aan de exacte build (Replay.cs:8-11, 17-18, 50-51).

### 2.2 Doelopbouw

```
Zeus.Sim (ongewijzigd)
   |  Match.Step / GetSnapshot / Events
MatchDriver (nieuw, Godot Node, geen tekenen)       <- uit Main.cs gehaald
   |  SnapshotBuffer (ring, ~64 ticks) + eventcursor
   +--> Main (2D, bestaand Node2D: tekent alleen)
   +--> Match3D (nieuw Node3D)
          VisualAdapter (puur C#, testbaar): tick -> VisualState per speler/bal/scheids
          PlayerView3D x22, BallView3D, RefereeView3D, CoachView3D
          CameraRig (TV standaard, Tactisch, CoachPOV, Volg)
          Stadium3D (statisch + publiek-MultiMesh)
   +--> CrowdModel (puur C#) -> AudioManager (bestaand autoload) + Crowd3D-emitters
   +--> HUD-overlay (bestaand): MatchPopups, PauseMenu, scorebug, eindkaart
```

Nieuwe klassen en scènes in `godot/`:
- `MatchDriver.cs` (Node): krijgt `_match`, `_prev/_curr`, `_acc`, `StepMatch`, `SkipChunk`, `HighlightProcess`, `CheckReplayEnd` en `ReadEvents` uit Main.cs (15-56, 280-302, 395-466, 468-500, 530-565). Publiceert C#-events `Stepped(Snapshot)`, `EventRead(Event)`, `RestMomentReached`. Main en Match3D zijn alleen abonnee.
- `SnapshotBuffer.cs` (puur C#): ringbuffer van (Snapshot, events per tick); `Sample(double visualTick)` geeft het geïnterpoleerde beeld.
- `VisualAdapter.cs` (puur C#, zonder Godot-types, testbaar in `godot/tests` naast `UserSaveFileTests.cs`): zet snapshot plus komende events om in `PlayerVisual { Pos3, Yaw, Speed, Locomotion, Action, ActionStartTime, LookAt }`, `BallVisual`, `RefereeVisual`.
- `Match3D.tscn` / `Match3D.cs` (Node3D): veld uit Pitch.Length/Width, instantieert de views, leest de driver.
- `PlayerView3D.tscn` (Node3D + Skeleton3D + AnimationTree, geen CharacterBody): krijgt alleen `PlayerVisual`. Tenue via shader-parameter uit `ClubIdentity.Kits` (ClubIdentity.cs:9-13).
- `CameraRig.cs`: vier modi, wisselen met C of 1-4; tv-camera standaard (makerbesluit 26).
- `CoachCreator.tscn`: §4.
- `CrowdModel.cs` (puur C#) en `Crowd3D.cs`: §5.
- `MatchView`-schakelaar in Settings en een sneltoets: Main.tscn of Match3D.tscn. Beide hangen aan dezelfde driver, dus wisselen midden in een wedstrijd kan zonder hersimulatie.

### 2.3 Tick naar 3D-staat, met vertraging

- Het beeld loopt **D = 4 ticks (0,4 s simtijd)** achter op de sim. Match3D tekent `visualTick = simTick - D + alpha`. Pass- en Shot-events zijn zo bekend vóór het visuele contactmoment; de trapanimatie (wind-up 0,25-0,4 s) start zodat het contactframe op de event-tick valt.
- Reële vertraging: 0,4 s op 1x, 0,1 s op 4x, 20 ms op 20x. Niet merkbaar: bijsturen gebeurt alleen bij rustmomenten (Match.Commands.cs:26, PauseMenu.cs:1).
- B-bijsturing: de driver stopt met stappen zodra `IsRestMoment` geldt en er een pauze is aangevraagd (Main.cs:495). Het beeld speelt de laatste D ticks uit, daarna opent het menu. Commando's gaan op de sim-tick (Match.Commands.cs:39-42). Een rustmoment duurt standaard 3 s (Match.cs:19) = 30 ticks, ruim meer dan D.
- Coördinaten: sim (x, y, z-omhoog) wordt Godot `Vector3(x, z, y)`. Die afbeelding spiegelt, dus yaw = `-Heading` plus een vaste modelcorrectie. In één functie met een unittest.

### 2.4 Interpolatie

- Positie: kubische Hermite tussen tick n en n+1 met Pos en Vel. Nu lineair (Main.cs:582).
- Heading: kortste-hoekinterpolatie. Nu niet geïnterpoleerd (Main.cs:584).
- Bal: Hermite met Vec3-Vel; bij een contactevent op de tick een harde knik, geen Hermite eroverheen.
- Wissel en rood: de speler verdwijnt nu meteen (Main.cs:580). In 3D een wegloopanimatie; de sim laat de rode kaart al naar de zijlijn lopen (Match.cs:42-45).
- Teleports bij hervatting (> 3 m per tick): knippen of korte fade, niet interpoleren.

### 2.5 Wat de sim extra kan exporteren (optioneel, pas na de proef)

Niets is nodig voor de proef. Als de afgeleide animatie te vaak fout zit: **alleen-lezen** accessors in een nieuw partieel bestand `sim/Zeus.Sim/Match.View.cs` (klasse `Match`, naast Match.cs:382-406):
- `IntentKind? IntentOf(Side team, string id)` uit `Player.Intent` (Match.cs:155): "kijkt rond" (Scan), "gaat drukken" (Press), loopactie.
- `(string? TakerId, string Reason, long UntilTick)? RestartInfo` uit Match.cs:190-192: juiste pose bij inworp, corner, doeltrap.

Harde regel: niets toevoegen aan `Snapshot`/`Canonical.Serialize` (Events.cs:46-68) of aan de `EventLog` (Events.cs:21-23). Dat verandert EventHash en SnapshotHash en daarmee goldens en replays. Een alleen-lezen accessor verandert geen gedrag.

### 2.6 Snelheden in 3D

| Snelheid | Ticks per frame (60 fps) | 3D-gedrag |
|---|---|---|
| 1x (hoogtepunten) | 0,17 | Volle animatie, tv-camera met snijden per Moment |
| 4x | 0,67 | Volle animatie; locomotie op snelheid, niet op tijd |
| 12x / 20x | 2-3,3 | Animatietempo afgetopt op ~3x, voetglijden geaccepteerd; tactische camera standaard; acties als korte pose-flits van 2-3 frames; balgeluid uit boven 8x zoals nu (AudioManager.cs:111, 131) |
| END-skip | duizenden | 3D niet bijgewerkt; bestaande banner (Main.cs:626-633) in de HUD |

Locomotie op snelheid: blend-positie in de AnimationTree volgt |Vel| in m/s; cyclus-afspeelsnelheid = |Vel| / natuurlijke clipsnelheid × min(speed, 3).

### 2.7 Replay en hoogtepunten

- Highlights: de driver doet de hersimulatie (Main.cs:395-466); Match3D tekent. Per Moment een openingsshot en balvolgen. Op het event-anker (Highlights.cs:41: 6 s voor, 2 s na) kan een replayhoek achter het doel volgen door het Moment twee keer af te spelen; de hersimulatie is deterministisch.
- Replaybestanden (ZEUS_REPLAY, Main.cs:82-95) werken zonder wijziging in 3D.
- Door de buffer: een "kijk nog eens"-knop die de laatste 64 ticks terugspoelt zonder hersimulatie.

## 3. Asset-pipeline binnen €500 per jaar

### 3.1 Bronnen (licentie bij download controleren en vastleggen in `LICENSES/` en `docs/assets-licenties.md`)

| Bron | Wat | Licentie | Kosten | Rol |
|---|---|---|---|---|
| **Mixamo** (Adobe) | Rigged characters, grote mocap-bibliotheek incl. voetbalclips (namen per clip controleren) | Gratis met Adobe-account (maker heeft er een); commercieel gebruik in games toegestaan; ruwe bestanden niet los herverdelen | €0 | Hoofdbron animaties |
| **MakeHuman / MPFB2** (Blender) | Parametrische mensen (lengte, bouw, huid, haar), standaardrig | Uitgevoerde modellen CC0 | €0 | Spelers en de basis van de coach-maker |
| **Quaternius** | "Universal Animation Library", karakterpacks | CC0 (gratis versies) | €0 | Locomotie-aanvulling; laagpolige stijl |
| **Kenney** | Laagpolige personages, sport- en decorassets | CC0 | €0 | Capsule- en decorfase, placeholders |
| **Sketchfab** | Stadionelementen, doelen, dug-out | Per model: alleen CC0/CC BY, nooit NC | €0 tot tientallen euro's | Decor |
| **Godot Asset Library** | Code en plugins | MIT per plugin | €0 | Tools |
| **Fab** (Epic) | Betaalde animatie- en karakterpacks, ook voetbal | Fab Standard License; per item controleren of het engine-onafhankelijk is | €20-80 per pack | 1-2 voetbalpacks als Mixamo gaten laat |
| **Freesound** | Publiek, fluit, balcontact | Per geluid; alleen CC0 en CC BY | €0 | Geluid |
| **Sonniss GameAudioGDC** | Jaarlijkse gratis bundel SFX | Royalty-free commercieel | €0 | Publieksbedden (inhoud controleren) |

Budget per jaar: €100-150 animatiepacks, €50-100 decor, €0-50 audio, rest reserve. AI-gegenereerde assets alleen met vermelding in het AI-logboek (makerbesluit 27).

### 3.2 Eén skelet en retargeting in Godot 4

1. Doelskelet: Godot `SkeletonProfileHumanoid`.
2. Per bron bij import een `BoneMap` op dat profiel en retarget aan; Godot hernoemt de botten, clips worden uitwisselbaar.
3. Animaties als losse `AnimationLibrary`-resources (`res://assets/anim/*.res`), zonder mesh; één library voor 22 spelers, scheids en coach.
4. Rootmotion uit; de positie komt altijd uit de sim, clips spelen in-place.
5. Per bron één testscène met dezelfde 5 clips op hetzelfde model; pas goedgekeurd als ellebogen, knieën en voeten kloppen.

### 3.3 Minimale animatieset

| # | Clip | Bron | Trigger in de adapter |
|---|---|---|---|
| 1-4 | Idle, wandelen, joggen, sprinten | Mixamo/Quaternius | |Vel| < 0,3 / 0,3-2 / 2-5 / > 5 m/s |
| 5 | Pass (binnenkant) | Mixamo | Pass-event |
| 6 | Schot / lange bal | Mixamo | Shot, of Pass met outcome "clear" |
| 7 | Kopbal | Mixamo | contact-event met bal Z > 1,3 m |
| 8 | Tackle (staand of sliding) | Mixamo/Fab | Duel/Foul |
| 9 | Keeper duik L/R | Mixamo | Save, of keeper-Vel zijwaarts > 3 m/s |
| 10 | Juichen | Mixamo | Goal: scorer plus 2 dichtstbijzijnde ploeggenoten |
| 11-12 | Scheids lopen/idle; fluit/wijst/kaart | gedeeld; Mixamo-gebaren | render-side; Foul/Card/Offside/Out |
| 13-14 | Achteruit en zijwaarts | Quaternius/Mixamo | hoek Vel-Heading > 110° of 60-110° |
| 15 | Aanname | Mixamo | BallControl/FirstTouch |
| 16 | Dribbel-jog | Mixamo | CarrierId == id |
| 17 | Inworp | Mixamo | RestartTaken na Out met ThrowIn |
| 18-19 | Keeper klaar / uittrap | Mixamo | IsKeeper |
| 20 | Vallen en opstaan | Mixamo | slachtoffer bij Foul |
| 21 | Teleurgesteld | Mixamo | tegenpartij bij Goal |
| 22-25 | Coach: staan, wijzen, klappen, armen omhoog, zitten | Mixamo | coach-events (§4) |

Proef: 1-10. Eerste 3D-build: ≈ 25. EA: ≥ 50. 1.0: ≥ 80 (roadmap D4, D8, D9).

### 3.4 Stapsgewijze opbouw

1. **Capsules** (T3): capsule met neus-kegel voor de heading, kleur per team. Bewijst adapter, camera's, snelheden, prestaties.
2. **Eén model** (T4/T5): één MakeHuman-man, 10 clips, tenuekleur via shader.
3. **Packs** (T5+): 3-4 lichaamsvarianten, haar- en huidvarianten, uitgebreide clips, LOD-keten.

## 4. Coach-POV en coach-maker

- **Camera:** `Camera3D` op ooghoogte (1,72 m, aanpasbaar aan de lengte van de coach) in de technische zone, ~1 m buiten de zijlijn bij de middenlijn. Modi: first-person; over-de-schouder (1,2 m achter, 0,4 m opzij); zittend in de dug-out (ooghoogte 1,15 m, dakrand in beeld). Tv-camera is de standaard (makerbesluit 26).
- **Hoofdbeweging:** muis of rechterstick, yaw ±110°, pitch -30/+20°. "Volg de bal" standaard aan als kritisch gedempte veer (~0,4 s) met kleine dode zone. Geen head-bob bij stilstaan; in v1 staat de coach op een vaste plek (lopen langs de lijn in D9).
- **Zichtbeperking:** vanaf de lijn zie je de verre kant slecht; daarom (1) een 2D-minimap in de hoek door de bestaande 2D-tekening (Main.cs:567-607) in een `SubViewport`; (2) een sneltoets naar de tactische camera; (3) POV geen standaard (ui-richting.md:258 wees POV eerder af op leesbaarheid).
- **B-bijsturing in 3D:** B vraagt een pauze aan zoals nu (Main.cs:172, 245-250). Bij het rustmoment staat de coach op, de camera gaat in 0,5 s naar het notitieboek in de handen, en het bestaande `PauseMenu` (PauseMenu.cs:23) opent in een CanvasLayer erboven. Na sluiten keert de camera terug. Substitution-event: speler staat op van de bank. TacticChange-event: coachgebaar. Puur beeld.
- **Coach-maker** (`CoachCreator.tscn`, vanuit het clubscherm naast de `ClubPicker`, ClubIdentity.cs:94):
  - Parameters: naam (zelfde opschoning als clubnaam, `Competition.CleanClubName` via ClubIdentity.cs:20), huidtint (8), lengte (1,60-2,00 m), bouw (3), haarstijl (≥ 8) en haarkleur (6), baard (4), bril, kleding (trainingspak, pak, jas) in clubkleuren (ClubIdentity.Kits), pet. Gezicht: vaste neutrale uitdrukking.
  - Techniek: MakeHuman-basis met 3-4 vooraf geëxporteerde lichaamsvarianten plus blendshapes voor lengte en bouw, anders schalen. Haar, bril en kleding als losse meshes op hetzelfde skelet.
  - Opslag: **per carrière** in `user://zeus_carriere.json` (Season.cs:14; makerbesluit 17), als één object `coach { version, name, indices..., reputation, style }`, met versie-upgrade via `UserSaveFile` (UserSaveFile.cs:11-12). Reputatie en coachstijl (roadmap F2, F12) horen bij hetzelfde object. Niet in `instellingen.json`.

## 5. Geluid

- **Bestaand:** autoload (project.godot:23), 2 muziekspelers, 6 sfx-stemmen, eigen eventstem (AudioManager.cs:12-17), niet-positioneel. Eventtoewijzing met rate-limit (AudioManager.cs:116-144), wedstrijdmomenten via `PopupKind` (147-156), uit in headless (34). Main roept `AudioManager.Event` per event aan (Main.cs:538).
- **`CrowdModel` (nieuw, puur C#, testbaar):** invoer per tick: events (Shot, Save, Goal, Card, Foul, Out met Corner, Offside, Penalty), balgevaar (bal in het laatste derde, afstand tot doel; snapshot plus `EndsSwapped`), stand en minuut (spanning bij klein verschil na minuut 75), thuis/uit van het balbezit. Uitvoer: `Intensity` 0..1 (geëffend, stijgt snel, zakt in ~4 s) en one-shots (ooh, juichen, fluitconcert bij kaart tegen thuis, zucht bij gemiste kans, applaus bij wissel). Deterministisch uit events, dus herhaalbaar in hoogtepunten. Later: fanvertrouwen en opkomst (roadmap F5) als schaalfactor.
- **AudioManager uitbreiden, geen tweede manager:** `Crowd(double intensity, ...)` beheert 3 lussende bedden (gemompel, opgewonden, brul) met crossfade. Bestaande `Sfx`-pool voor one-shots. Werkt **ook in 2D** (roadmap E1, 2027-Q2).
- **Gezang per club:** 6-10 eigen sjablonen (ritme met klappen, roep en antwoord op eigen melodie; **geen bestaande clubliederen of popmelodieën**). Per club deterministische keuze plus toonhoogte en tempo uit het club-id. Start bij spanningspieken of thuisvoorsprong, stopt bij tegengoal. Opname: eigen opnames met vrienden (makerbesluit 27), gelaagd; synthetisch alleen als placeholder.
- **3D:** `AudioStreamPlayer3D` op bal (trap, kopbal, paal), scheids (fluit), doel (net); 4 tribune-emitters met grote `unit_size`; luisteraar = actieve camera of `AudioListener3D` op het coachhoofd in POV; op 12x/20x bal-sfx gedempt zoals nu (AudioManager.cs:131). Bussen: Master, Music, Sfx, Crowd (nieuw), schuiven uit de instellingen (AudioManager.cs:26-30).

## 6. Prestaties en testen

- **Doelen (roadmap D7):** ontwikkel-pc 1080p Hoog ≥ 60 fps; werklaptop (Iris Xe-klasse) 1080p Laag gemiddeld ≥ 60 fps, 1% low ≥ 45; UHD 620-klasse ≥ 30 fps of automatisch 2D. RAM < 1,5 GB, VRAM < 1 GB, pakket ≤ 1 GB (nu 95,1 MB).
- **Renderer:** het project draait op `gl_compatibility` (project.godot:11, 32-34), projectbreed. Proef in T4: Compatibility, Mobile en Forward+ op dezelfde scène en dezelfde laptop; kiezen op fps en beeld; mag projectbreed veranderen als alle 2D-tests groen blijven. Lightmap-bake, upscalers en schaduwkwaliteit per renderer in de 4.7-documentatie controleren.
- **Maatregelen:** 22 skinned meshes met LOD0 ≤ 15k tris, LOD1 4k, LOD2 1,5k; één materiaal per spelerstype, tenue via `instance uniform`; textures 1K, VRAM-gecomprimeerd; publiek als `MultiMeshInstance3D` met impostors, nooit skinned; stadion statisch met gebakken licht en AO; één directional light, schaduw alleen op spelers en bal, blob-schaduw op Laag; `AnimationTree` met lagere update-rate op afstand; geen allocaties per frame (let op `GetSnapshot`, Match.cs:869-872); presets Laag/Midden/Hoog naast de bestaande fps-limiet (SettingsMenu.cs:39).
- **Testen:** `Match3D` in `SCENES` van `scripts/smoke_godot.py` (regel 21; headless rendert niet, vangt alleen script- en laadfouten); nieuw autotestscript `scripts/autotest/17a_match3d.json` naar het patroon van `4_match.json` (aftrap, 3D aan, camerawissels, B-pauze, END, hoogtepunten) met nieuwe stap `expect_fps <min> <seconden>` in AutoTest.cs (naast AutoTest.cs:61-72) op `Engine.GetFramesPerSecond`; determinismetest: dezelfde seed met 2D en met 3D geeft dezelfde `EventHash`; unittests voor `VisualAdapter` en `CrowdModel` in `godot/tests`; werklaptop handmatig met een vaste fps-log-run, uitkomsten bewaren.

## 7. Technische mijlpalen T0-T10

Agent-dagen = werk van agents; maker-uren = beoordelen, spelen, besluiten. De maker-uren zijn in de roadmap verwerkt in D0-D9.

| # | Mijlpaal | Klaar als | Agent-dagen | Maker-uren |
|---|---|---|---|---|
| T0 | Besluiten: min-spec laptop (merk, cpu, gpu, RAM), renderer-kandidaten, coördinaatconventie | Vastgelegd in `Besluiten` | 0,5 | 1-2 |
| T1 | `MatchDriver` uit Main.cs; Main tekent alleen | Autotests `4_match`, `8a_match_live`, `9e_challenge_live`, `11a_league` PASS; EventHash per seed gelijk aan vóór de refactor | 2-3 | 2 |
| T2 | SnapshotBuffer, VisualAdapter, Hermite en heading-interpolatie, vertraging D, unittests | Tests groen: continuïteit, hoekomslag, teleport-knip, event-lookahead | 2 | 1 |
| T3 | Capsule-3D: Match3D.tscn, veld, bal met Z, tactische en tv-camera, wisselen 2D/3D, hoogtepunten in 3D, smoke plus 17a-autotest | Hele wedstrijd op 4/12/20x en hoogtepunten zonder fout; ≥ 60 fps ontwikkel-pc; fps werklaptop gemeten | 3 | 3 |
| **T4** | **Go/no-go-proef (T1-T4 samen, timebox 3 weken):** één MakeHuman-model met 10 clips via retargeting, coach-POV v0 (vaste coach), CrowdModel v0 met 3 bedden, renderervergelijking | G3-criteria in de roadmap §6. Eerste no-go: kleinere scope of eenvoudigere stijl, G3 opnieuw in 2027-Q4; tweede no-go: 2D blijft het product. `MatchDriver` en `CrowdModel` blijven in elk geval | +5-7 | 8-10 |
| T5 | Animatieset v1 (≈ 25 clips), AnimationTree met locomotie-blend en acties, kopbal- en tackledetectie | Oogtest op 10 hoogtepuntclips: ≥ 8 zonder storende fout | 5-8 | 6 |
| T6 | Coach: character-maker, opslag in `zeus_carriere.json`, POV-modi, notitieboek-overgang bij B, minimap | Coach aanmaken, herstarten, coach gelijk; B-flow in 3D gelijk aan 2D (PauseMenu-autotest in 3D PASS) | 4-5 | 5 |
| T7 | Stadion en publiek: 3 stadiontypen, MultiMesh-publiek, CrowdModel v1, gezang, 3D-audio | Publiek reageert hoorbaar op goal, kans, kaart en spanning; 6 gezangsjablonen met licentie of eigen opname | 5-7 | 6 |
| T8 | Scheids en assistenten (render-side), fluit- en kaartgebaren | Scheids op ≥ 95% van de frames binnen 25 m van de bal en nooit in de baan van een pass (5 wedstrijden) | 2 | 1 |
| T9 | Prestaties: LOD, bakes, presets, GC-meting; stadion groeit mee, weer en licht | Doelen §6 op de werklaptop; pakket ≤ 1 GB; RAM < 1,5 GB | 4-6 | 5 |
| T10 | 3D-scènes (kleedkamer, pers, dug-out); EA-klaar: schone-pc-test met 3D, credits en licenties, screenshots, AI-logboek | Release-checklist groen met 3D | 4-5 | 7 |
| **Totaal** | | | **≈ 40-50** | **≈ 50-60** |

### Stand T2 (adapterlaag, nog niet gebouwd of getest)

Geschreven zonder te compileren; de hoofdsessie bouwt en test. Alles zonder Godot-types, in de globale namespace zoals `DebugViewState`.

- `godot/SimWorld.cs`: `SimToWorld(Vec2, height)`, `SimToWorld(Vec3)`, `WorldToSim`, `YawFromHeading(heading)` (= `-Heading + ModelYawOffset`, offset -π/2 voor een model dat naar -Z kijkt), `ForwardFromYaw`.
- `godot/SnapshotBuffer.cs`: ring (64) van (Snapshot, events, carrierId). `Push`, `Sample(visualTick)` geeft een hergebruikte `VisualFrame`, `EventsBetween(vanExclusief, totEnMet)`, `UpcomingEvents(vanTick, lookahead)`, `At`, `EventsAt`. Hermite op positie (Vel per tick = Vel × 0,1 s), kortste hoek op heading, bal-Hermite met een rechte lijn als de volgende tick een contactevent heeft (Pass, Shot, BallControl, FirstTouch, Save, Interception), teleport-knip bij > 3 m per tick. Een gat of terugspoelen in de ticks leegt de buffer.
- `godot/VisualAdapter.cs`: `Update(buffer, visualTick)` vult `Players` (`PlayerVisual`), `Ball` (`BallVisual`) en `Referee` (`RefereeVisual`). Drempels staan in `AdapterThresholds`; pure functies `ClassifyLocomotion`, `IsHeader`, `LateralSpeed`, `RefereeTarget`, `ConstrainReferee`.
- Tests: `godot/tests/VisualAdapterTests.cs`; de drie bronbestanden zijn als link toegevoegd aan `godot/tests/Zeus.Godot.Debug.Tests.csproj`. Draaien: `dotnet test godot/tests/Zeus.Godot.Debug.Tests.csproj`.
- Aansluiten op `MatchDriver` (T1): per gestapte tick `buffer.Push(snapshot, nieuweEventsVanDieTick, match.CarrierId)`; per frame `adapter.Update(buffer, simTick - D + alpha)` met `D = 4`.

Afwijkingen van het ontwerp:
- `ActionStartTime` is `ActionStartTick` (tick, geen seconden).
- Het `Goal`-event heeft geen speler; de scorer is de laatste schutter van die ploeg in de buffer, anders de dichtstbijzijnde speler bij het doel. Het slachtoffer van een `Foul` is de dichtstbijzijnde tegenstander bij de foutlocatie.
- De adapter leest gebeurtenissen rechtstreeks uit de buffer binnen `[tick - 50, tick + WindUpTicks]`; `UpcomingEvents` is er voor de driver en de tests.
- Fall, Celebrate en Disappointed starten op de event-tick; de overige acties `WindUpTicks` (3) ervoor. Dribbel en keeperduik op snelheid starten op de huidige tick.
- Scheids: begrensd op 2 m buiten de lijnen, loopt met maximaal 6 m/s, harde minimale afstand 8 m tot de bal en 3 m vrijheid van de baan van een bal in vlucht (> 2 m/s). Diagonaal wisselt met `EndsSwapped`. Assistenten volgen later.
- Een wissel verdwijnt niet uit de snapshot (zelfde slot, zelfde id); alleen spelers die in de volgende tick ontbreken krijgen `Leaving` en blijven staan. Rood: de speler loopt volgens de sim zelf naar de zijlijn (`SentOff`).

Kan later of weg om maker-uren te sparen: zittende dug-out-POV, wissel-scène, replayhoek achter het doel, lopen van de coach langs de lijn, weer.

**Stand T1 (demo/matchdriver, nog niet gebouwd of getest):** `godot/MatchDriver.cs` (Node, kind van Main, door Main in `_Ready` gemaakt; `Main.tscn` ongewijzigd) bezit nu `Match`, accumulator en snelheid, `StepMatch`, skip (`SkipChunk`), hoogtepunt-hersimulatie met determinismebewaking, `CheckReplayEnd`/ZEUS_REPLAY, `ReadEvents` (incl. `AudioManager.Event`) en de pauze-bij-rustmoment. Publieke API: `Initialize()`, `Advance(delta)`, `RequestPause()`, `Resume()`, `TogglePause()`, `SkipToEnd()`, `PickHighlights()`, `StartHighlights()`, `EndHighlights()`; eigenschappen `Match`, `Current`, `Previous`, `Alpha`, `Speed`, `Paused`, `Skipping`, `Highlighting`, `MatchOver`, `CanSteer`, `Replay` e.a.; events `EventRead`, `EventsRead`, `RestMomentReached`, `HighlightStarted`, `HighlightEnded`. Main houdt `_Draw`, HUD, eindkaart, pauzemenu, invoer en de debuglagen; publiek blijft `PanelX`, `CanCoach`, `RequestCoaching()`, `TryClosePauseMenu()` (AutoTest, Hotkeys en SettingsMenu ongewijzigd). De poort `python scripts/check_match_step.py` faalt als `.Step()` buiten MatchDriver.cs staat (uitgezonderd: `godot/tests/` en de offline simulaties in Editor.Test.cs en Meter.cs); draai hem naast `check_release_content.py` in de regressiecheck.

Verificatie door de hoofdsessie: (1) bouwen, `python scripts/check_match_step.py`; (2) autotests `4_match`, `8a_match_live`, `9e_challenge_live`, `11a_league` PASS; (3) `Zeus.Runner --until-finished` EventHash per seed gelijk aan vóór de refactor (de sim is niet aangeraakt; de UI mag de hash niet beïnvloeden); (4) UI-kant: neem met de vorige build een replay op en speel hem in beide builds met `ZEUS_REPLAY=<pad>`, beide moeten `Replay verified after N steps: <hash>` loggen met dezelfde hash, ook met `ZEUS_FINISH=1`; (5) handmatig: B tijdens wedstrijd, END, hoogtepunten (Esc na clip 1), `ZEUS_HIGHLIGHT` met `ZEUS_SHOT`.

## 8. Risico's

| Risico | Kans | Impact | Maatregel |
|---|---|---|---|
| Main.cs-refactor (T1) breekt de wedstrijdflow; autotests met klikcoördinaten (4_match.json:12, 15) | Middel | Hoog | Alleen verplaatsen, geen gedrag wijzigen; EventHash- en autotestvergelijking als poort |
| 2D en 3D groeien uit elkaar | Hoog zonder T1 | Hoog | Eén MatchDriver; views roepen nooit `Match.Step` aan; grep-test in de suite |
| Sim-wijziging "voor 3D" breekt goldens of replays | Middel | Hoog | Alleen alleen-lezen accessors (§2.5); nooit Snapshot/EventLog uitbreiden |
| Retargeting van gemengde bronnen | Hoog | Middel | Eén humanoid-profiel; testscène per bron; liever 1-2 bronnen goed |
| Afgeleide animatie klopt niet (trap zonder bal, kopbal laag) | Middel | Middel | Buffer D; drempels in één tabel; later `IntentOf` |
| Godot .NET-export: geen Web; macOS vraagt notarisatie; grotere export door .NET-runtime | Laag (Windows) | Middel | Eerst alleen Windows |
| Geheugen en pakketgrootte van assets | Middel | Middel | 1K-textures, gedeelde library, budget per asset; `check_release_content.py` uitbreiden |
| Werklaptop haalt het niet in Compatibility, of Forward+ start niet op oude gpu | Middel | Hoog | Renderer meten in T4; presets; 2D altijd beschikbaar |
| Coach-POV minder leesbaar dan 2D (ui-richting.md:258) | Hoog | Middel | POV optioneel, minimap, tv standaard; testercriterium in T4 |
| Licenties: Sketchfab-NC, Fab alleen-Unreal, echte clubliedmelodieën | Middel | Hoog | Register per asset in `LICENSES/` en `docs/assets-licenties.md`; alleen CC0/CC BY/betaald engine-vrij |
| Scope-creep (gezichten, eigen mocap) | Hoog | Middel | "Geen gezichtsanimatie" en "geen eigen mocap" vastgelegd; parkeerlijst |

Niet geverifieerd door de architect: clipnamen bij Mixamo, Fab-prijzen, de inhoud van de Sonniss-bundel, en wat de Compatibility-renderer in Godot 4.7 kan (lightmaps, upscaling). Controleren bij T0.
