# Intro en onboarding: bouwstand (branch `pc2/intro`)

Ontwerp: vault `01 Ontwerp/Tutorial en onboarding.md`. Dit bestand zegt wat er gebouwd is en welke aannames erbij zijn gemaakt.

## Gebouwd (ontwerp §12 stappen 1-4)

| Stap | Wat | Waar |
|---|---|---|
| 1 | Profielen-laag: `Profiles.Path`, `profiel.json`, `ZEUS_PROFILE`, `OnboardingState` v2 | `sim/Zeus.Sim/Profiles.cs`, `OnboardingState.cs`, `godot/Profiles.cs`, `ProfileStore.cs` |
| 2 | Mentor Pike: kaartje, subtitle-balk, 25 tutorialregels EN+NL, placeholder-portret | `sim/Zeus.Sim/Mentor.cs`, `godot/Mentor.cs`, `data/lang/*.json` (`mentor.*`) |
| 3 | Eerste start (naam, trainer, club, instellingen) en drie Pike-introkaartjes | `godot/FirstStart.cs`, haak in `godot/Menu.cs` |
| 4 | Drie tutorialscenario's | `data/tutorial/*.json`, `sim/Zeus.Sim/TutorialScenarios.cs` |
| 5 | Tutorialcontroller: stap per profiel (`OnboardingState.TutorialStep`, 2-4 = oefenpotjes A-C, 5 = echte competitiewedstrijd, 6 = sluitkaart), slotjes op home, knop "Trainingsveld: ..." per stap, oefenpotje als `ChallengeRun` met `Tutorial = true` | `sim/Zeus.Sim/TutorialFlow.cs`, `godot/Tutorial.cs`, haken in `Menu.cs`, `Main.cs`, `Season.cs` |

Debug-schakelaars (alleen debugbuild): `ZEUS_TUTORIAL=skip` (geen eerste start), `ZEUS_TUTORIAL=force` (ook in headless/screenshot), `ZEUS_PROFILE=<map>` (profiel `user://profielen/<map>/`).

## Nog niet gebouwd

- Nieuw home zonder aftrappaneel, scherm Vriendschappelijk (stap 4 van §12); het aftrappaneel staat er nog en krijgt in de tutorial een extra knop.
- Uitlegmomenten `mentor.first.*` (36) en "Pike zei hier" in `Welcome` (stap 6).
- Profielkeuzescherm, "Uitleg opnieuw", "Profiel wissen" (stap 7); de oude `Tour` en `welcome` blijven bestaan voor wie al een `profiel.json` heeft en de tutorial af heeft.
- Autotests voor alle tutorialstappen (22a: eerste start; 22b: eerste start, intro en oefenpotje A; B, C, de echte wedstrijd en de sluitkaart zijn niet doorgelopen) en `voice`-kanaal. Geen AI-artwork (open vraag 8).
- Tutorial: geen hint op home bij stap 5 ("speel je eerste competitiewedstrijd"), geen `late`-herhaling, geen vervolgregels `done`/`halftime`/`goal_*`; T4 als oefenwedstrijd bij een afgelopen seizoen of een beker die aan de beurt is (§5) ontbreekt; "Tutorial overslaan" op een tweede profiel ontbreekt.

## Aannames

1. **Club voor `profiel.json`.** Tot "Naar de club" schrijven de ClubPicker-keuzes naar `ProfileStore.Editing` (in het geheugen), niet naar `instellingen.json`. Wie halverwege stopt, heeft dus geen profiel en krijgt de eerste start opnieuw. De oude `club_*`-sleutels blijven ongemoeid en worden alleen als beginwaarde gelezen.
2. **Seizoensdoel op P2 weggelaten.** `Competition.ExpectedGoal` rekent met tactiek- en selectiemappen; op P2 staat alleen "Amateurklasse · 6 clubs". Doel tonen kan bij stap 4/5 (home).
3. **Instellingen op P3 blijven globaal** (`match_pace`, `fullscreen`, `audio` in `instellingen.json`), niet per profiel zoals §8 voorstelt; verhuizen kan bij stap 7.
4. **Wanneer de eerste start verschijnt.** Alleen in een venster-run (niet headless, niet met `ZEUS_SHOT`) of met `ZEUS_TUTORIAL=force`, en alleen als `profiel.json` ontbreekt en niet geblokkeerd is (onleesbaar of nieuwere versie). Daarna spelen drie Pike-kaartjes; de tutorialstappen zelf volgen in stap 5. De oude tour en welkomstkaart worden in die run overgeslagen.
5. **Mentorregels.** Vijf ids per scenario (`start`, `hint`, `done`, `late`, `end`), vijf in de echte wedstrijd (`kickoff`, `halftime`, `goal_for`, `goal_against`, `end`), drie intro, twee afsluiting: 25 in plaats van de 24 uit §5. `mentor.ok` ("Got it") is de knop op het kaartje.
6. **Tutorialscenario's.** `criteria` is een formele `doelpunten_voor >= 0` (het bestand moet criteria hebben; de tutorial beoordeelt niet), 1 wedstrijd, beide ploegen amateur, `niveau_speler` vast. Ze staan in `data/tutorial/` en niet in `challenges/`, zodat ze niet in de lijst of de sterrentelling komen. `build_demo.ps1` en `check_release_content.py` nemen de map mee.
7. **Mondstand en duur** uit de tekst (`MentorTiming`): 2,5 s + 0,06 s per teken; klik op de balk sluit hem.
8. **Placeholder-portretten** (Pike en trainer) zijn in code getekend; de indices (uitdrukking 5, mond 4, huid 5, haar 6, kleding 4, kleur 10) zijn dezelfde als voor de latere PNG-lagen.

9. **Ontgrendeling.** De competitie (`league`) is open vanaf stap 5, want de echte wedstrijd wordt daar gespeeld; Uitdagingen volgen na die wedstrijd, Vrienden na de sluitkaart. Zie `TutorialFlow`.
10. **RC7-spelers.** Zonder `profiel.json` krijgt ook een bestaande speler de eerste start (club vooringevuld, melding `profile.rc7_note` dat opslag, tactieken en sterren blijven). Er wordt niets overschreven of verplaatst: oefenpotjes slaan geen uitslag, voortgang of geschiedenis op. Zijn schermen zijn tot de sluitkaart vergrendeld (ontwerpbesluit 2). `ZEUS_TUTORIAL=skip` zet dit uit; `run_autotest.py` zet dat standaard.
11. **Tour en welkomstkaart** wijken voor de tutorial zolang die loopt (`Tutorial.Active`); de wedstrijdtour ("Coach with B") staat nog gewoon aan in de oefenpotjes.

## Getest

Sim-tests (Debug, gericht): `OnboardingTests`, `OnboardingV2Tests`, `ProfileTests`, `ProfilePathGuardTests`, `MentorTests`, `TutorialScenarioTests`. Godot-project bouwt (Debug); headless start met `ZEUS_TUTORIAL=force` geeft geen fouten. Verder: `TutorialFlowTests` (13). Echte Godot-runs (Debug, venster, screenshots bekeken op 1600x900-frame en 1280x720): autotest `22a_intro_firststart`, `22b_intro_tutorial` (eerste start, intro, slotjes, oefenpotje A tot het eindsignaal, terug naar home op stap 3), smoke schoon, en `1_menu`, `5_tour`, `6a`, `6b`, `12a`, `20a` PASS met `ZEUS_TUTORIAL=skip` als standaard in `run_autotest.py`. De placeholder-hoofden (Pike, trainer) zijn hersteld (ellips als polygoon, pet op de schedel).

**Niet getest:** oefenpotje B en C en de echte wedstrijd T4 in het venster, de sluitkaart, een echte RC7-gebruikersmap met opslag, de volledige suite, de overige autotests, een venster 1280x720 voor de tutorialknop.

## Stand 2026-10-27 (avond): home, uitlegmomenten, profielen

**Af en getest (Debug, venster):**
- Nieuwe home (`godot/HomeCard.cs`, haken in `Menu.cs`/`Season.cs`/`GameSession.cs`): clubkaart (embleem, clubnaam, divisie, positie, seizoensdoel), trainer, volgende wedstrijd en "Speel wedstrijd". Die knop zet `GameSession.AutoPlayLive` en laadt Season, dat de wedstrijd zelf start (eenmalig; zonder doel of bij beker/uitslag in behandeling staat er "Open de competitie"). Alleen met profiel en afgeronde tutorial (`Tutorial.Explaining`); zonder profiel of met `ZEUS_TUTORIAL=skip` blijft de oude home, zodat alle bestaande autotests ongewijzigd werken. Het aftrappaneel is nu "Vriendschappelijk": dezelfde scene, knoppen `menu_friendly` en `menu_back_club` (`GameSession.FriendlyMode`), geen eigen scene. De checklist "Je eerste stappen" is weg voor profielen die de tutorial af hebben.
- 34 uitlegmomenten (`sim/Zeus.Sim/MentorFirst.cs`, tekst `mentor.first.*` in en/nl, `Mentor.Explain`): eenmaal per profiel via `OnboardingState.seen`, kaartje of subtitle zoals de tabel in het ontwerp, haken in Season, Selectie, Tactiekmaker, Bibliotheek, Meter, Uitdagingen, Vrienden, Vriendschappelijk, Wedstrijd, Instellingen en Hulp. De hulp (?) toont "Wat Pike hier zei" met de al gehoorde regels van dat scherm. Kaartje en subtitle staan nu op canvaslaag 80/81 (boven de overlays op laag 70). NB: het ontwerp noemt 36 momenten; de tabel telt er 34.
- Tests: `MentorFirstTests` (ids uniek, tekst in beide talen, gezien-logica), `ProfileAdminTests`; autotests 23a_home, 1_menu, 2_library, 7a, 22a, 22b PASS. `ProfilePathGuardTests` stond rood sinds de RC7-melding (`FirstStart.HasOldSave` kijkt bewust naar de hoofdmap): `FirstStart.cs` staat nu op de toegestane lijst.

**Niet af (ongecommit in de code, concept apart):**
- Profielkeuze bij 2 of meer profielen, nieuw profiel en "Profiel wissen" met bevestiging. Sim-hulp staat erin en is getest (`ProfileLayout.NextName`, `WipedName`, `MainFiles`). De Godot-kant staat als concept in `docs/intro-profileadmin-concept.cs.txt` (`ProfileAdmin`: keuzescherm, `NewProfile`, `Wipe`); die is niet gebouwd of getest. Hervatten: bestand terug naar `godot/ProfileAdmin.cs`; toevoegen: `ProfileStore.Reset()` (`_loaded = false; _current = null; _blocked = false; _loadedFor = ""; Editing = null;`) en `OnboardingProgress.Reset()` (`_state = null;`) plus `ClearSeen()`; loc-sleutels `profile.pick_title`, `profile.new`, `profile.main`, `settings.explain_again`, `settings.explain_done`, `settings.wipe_profile`, `settings.wipe_confirm` (en/nl); in `Menu.Onboarding()` als eerste tak `if (ProfileAdmin.Needed) ProfileAdmin.Pick(this)`; in `SettingsMenu` (Hulp-tab, onder de tourknop, alleen op Menu) knoppen "Uitleg opnieuw" (`OnboardingProgress.ClearSeen`), "Profiel wissen" (ConfirmationDialog, dan `ProfileAdmin.Wipe`) en "Nieuw profiel"; autotest 23b met `userfile profielen/p2/profiel.json=...`. De oude tourknop blijft staan (autotests gebruiken hem).
- Niet gedaan: hint op home bij stap 5, vervolgregels `done`/`halftime`/`goal_*`, T4 als oefenwedstrijd bij afgelopen seizoen, "Tutorial overslaan" op een tweede profiel, `voice`-kanaal, uitlegmomenten `season.familiarity`, `editor.drag`, `editor.paths`, `challenges.live`/`scenario`/`star` (niet bedraad; ids staan wel in `MentorFirst` en de taaltabel, `Mentor.Explain(this, "<id>")` op de juiste plek is genoeg), geen echte RC7-gebruikersmap geprobeerd, volledige suite en overige autotests niet gedraaid, smoke niet opnieuw.
- Bekend: in de nieuwe home levert "Speel wedstrijd" bij een gloednieuwe carrière zonder doel eerst de competitie (intro) op; de live-route is niet in een autotest doorlopen.
