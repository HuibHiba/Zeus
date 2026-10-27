# Intro en onboarding: bouwstand (branch `pc2/intro`)

Ontwerp: vault `01 Ontwerp/Tutorial en onboarding.md`. Dit bestand zegt wat er gebouwd is en welke aannames erbij zijn gemaakt.

## Gebouwd (ontwerp §12 stappen 1-4)

| Stap | Wat | Waar |
|---|---|---|
| 1 | Profielen-laag: `Profiles.Path`, `profiel.json`, `ZEUS_PROFILE`, `OnboardingState` v2 | `sim/Zeus.Sim/Profiles.cs`, `OnboardingState.cs`, `godot/Profiles.cs`, `ProfileStore.cs` |
| 2 | Mentor Pike: kaartje, subtitle-balk, 25 tutorialregels EN+NL, placeholder-portret | `sim/Zeus.Sim/Mentor.cs`, `godot/Mentor.cs`, `data/lang/*.json` (`mentor.*`) |
| 3 | Eerste start (naam, trainer, club, instellingen) en drie Pike-introkaartjes | `godot/FirstStart.cs`, haak in `godot/Menu.cs` |
| 4 | Drie tutorialscenario's | `data/tutorial/*.json`, `sim/Zeus.Sim/TutorialScenarios.cs` |

Debug-schakelaars (alleen debugbuild): `ZEUS_TUTORIAL=skip` (geen eerste start), `ZEUS_TUTORIAL=force` (ook in headless/screenshot), `ZEUS_PROFILE=<map>` (profiel `user://profielen/<map>/`).

## Nog niet gebouwd

- Stap 5 tutorialcontroller (stapmachine T0-T5, `ChallengeRun` met tutorialvlag, T4 via `Season.PlayLive`, ontgrendeling/slotjes, regels `tutorial.*` en `lock.*`).
- Nieuw home zonder aftrappaneel, scherm Vriendschappelijk (stap 4 van §12).
- Uitlegmomenten `mentor.first.*` (36) en "Pike zei hier" in `Welcome` (stap 6).
- Profielkeuzescherm, "Uitleg opnieuw", "Profiel wissen" (stap 7); de oude `Tour` en `welcome` blijven bestaan voor wie al een `profiel.json` heeft.
- Autotest-scripts bijwerken (stap 8) en `voice`-kanaal (stap 9). Geen AI-artwork (open vraag 8).

## Aannames

1. **Club voor `profiel.json`.** Tot "Naar de club" schrijven de ClubPicker-keuzes naar `ProfileStore.Editing` (in het geheugen), niet naar `instellingen.json`. Wie halverwege stopt, heeft dus geen profiel en krijgt de eerste start opnieuw. De oude `club_*`-sleutels blijven ongemoeid en worden alleen als beginwaarde gelezen.
2. **Seizoensdoel op P2 weggelaten.** `Competition.ExpectedGoal` rekent met tactiek- en selectiemappen; op P2 staat alleen "Amateurklasse · 6 clubs". Doel tonen kan bij stap 4/5 (home).
3. **Instellingen op P3 blijven globaal** (`match_pace`, `fullscreen`, `audio` in `instellingen.json`), niet per profiel zoals §8 voorstelt; verhuizen kan bij stap 7.
4. **Wanneer de eerste start verschijnt.** Alleen in een venster-run (niet headless, niet met `ZEUS_SHOT`) of met `ZEUS_TUTORIAL=force`, en alleen als `profiel.json` ontbreekt en niet geblokkeerd is (onleesbaar of nieuwere versie). Daarna spelen drie Pike-kaartjes; de tutorialstappen zelf volgen in stap 5. De oude tour en welkomstkaart worden in die run overgeslagen.
5. **Mentorregels.** Vijf ids per scenario (`start`, `hint`, `done`, `late`, `end`), vijf in de echte wedstrijd (`kickoff`, `halftime`, `goal_for`, `goal_against`, `end`), drie intro, twee afsluiting: 25 in plaats van de 24 uit §5. `mentor.ok` ("Got it") is de knop op het kaartje.
6. **Tutorialscenario's.** `criteria` is een formele `doelpunten_voor >= 0` (het bestand moet criteria hebben; de tutorial beoordeelt niet), 1 wedstrijd, beide ploegen amateur, `niveau_speler` vast. Ze staan in `data/tutorial/` en niet in `challenges/`, zodat ze niet in de lijst of de sterrentelling komen. `build_demo.ps1` en `check_release_content.py` nemen de map mee.
7. **Mondstand en duur** uit de tekst (`MentorTiming`): 2,5 s + 0,06 s per teken; klik op de balk sluit hem.
8. **Placeholder-portretten** (Pike en trainer) zijn in code getekend; de indices (uitdrukking 5, mond 4, huid 5, haar 6, kleding 4, kleur 10) zijn dezelfde als voor de latere PNG-lagen.

## Getest

Sim-tests (Debug, gericht): `OnboardingTests`, `OnboardingV2Tests`, `ProfileTests`, `ProfilePathGuardTests`, `MentorTests`, `TutorialScenarioTests`. Godot-project bouwt (Debug); headless start met `ZEUS_TUTORIAL=force` geeft geen fouten. **Niet getest:** de eerste start met muis en toetsenbord, uiterlijk van kaartje, balk en portretten (geen screenshot bekeken), een echte RC7-gebruikersmap, de volledige suite, autotests, smoke voor alle schermen.
