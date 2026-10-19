# Eerste complete release-run (pc 2)

Stand 2026-10-19. Uitgevoerd op pc 2 (Windows 11, 32 GB RAM, RTX 3060 Ti, scherm 3440x1440 plus een tweede scherm) met `.\scripts\release.ps1 -AllowDirty` in een schone worktree `F:\dev\zeus-release`, branch `pc2/release-run` vanaf `origin/feat/q1-release-script` (b6e4d49). Uitvoer: `dist/release-GaffersNotebook-0.1.0-beta.1-20261019-125522/` (lokaal, buiten git).

## Uitkomst per laag (run 2, commit 343a5c8)

| Laag | Uitkomst | Duur |
|---|---|---|
| Schone controle | groen | - |
| Build (Release, `-m:1`) | groen | 0,2 min |
| Sim-tests | groen: 884 van 884 | 11,7 min |
| Smoke alle scenes | groen | 0,3 min |
| UI-autotests | groen; `10a_save_damaged` verwacht rood (zoals bedoeld) | 16,3 min |
| Export + zip (`build_demo.ps1`) | groen: `GaffersNotebook-0.1.0-beta.1.zip`, 97 MB | 0,6 min |
| Archiefcontrole | groen: 221 bestanden gelijk aan `build/`, geen `calibration/`, `check_release_content.py --package` groen | 0,1 min |
| Exetest (`playtest_exe.py` op de exe uit de uitgepakte zip) | **rood** | 6,7 min |
| SHA256 | niet gedraaid in de keten (stopte bij de exetest); handmatig: `e90c77cc...5788` over de zip (los berekend, niet als `SHA256.txt` in de uitvoer) | - |
| Lokale tag | niet gedraaid (geen `-Tag`, `-AllowDirty`) | - |

Totaal run 2: 36 min tot en met de rode exetest. Geheugen is niet gemeten; vrij RAM voor de start 17,5 GB van 31,9 GB, geen out-of-memory gemeld.

## Gevraagde controle: `ExportRelease` zonder `AutoTest.cs`

Groen. De stap `package` (`check_release_content.py --package` op de uitgepakte map) slaagde: notices, whitelist en lekcontrole in orde, en de typenaam `AutoTest` staat niet in `data_Zeus.Godot_windows_x86_64/Zeus.Godot.dll`. `--export-release` gebruikt dus de configuratie `ExportRelease` zonder testhaken. De exe start, de kaft opent en de rondleiding werkt (zie onder).

## Gevonden en opgelost in `scripts/`

1. **`build_demo.ps1`, stap 'Godot-licentieteksten'** (commit 343a5c8): Godot schrijft `WARNING: 2 ObjectDB instances were leaked at exit` op stderr; met `$ErrorActionPreference='Stop'` brak dat de stap af, hoewel de export zelf en het licentiebestand goed waren. Nu dezelfde stderr-afhandeling als de exportstap. Run 1 stond hierdoor rood bij 'export + zip'.
2. **`playtest_exe.py`, `_find_window`** (commit 5e85f59): het script pakte het eerste venster van het proces met een clientgrootte groter dan nul. Op een pc met NVIDIA-driver is dat een verborgen venster van klasse `NVOpenGLPbuffer`, dus alle toets- en muisberichten kwamen nooit in het spel aan; de hele speeltest bleef op de kaft staan (25 van de 50 stappen DEAD). Nu alleen een venster van klasse `Engine`. Daarna opent de kaft en werkt de rondleiding. Op de laptop viel dit niet op.

## Wat nog rood is: speeltest, verouderde klikcoördinaten

Met de vensterfix volgens een derde proef (alleen de exetest, 4,6 min): de exe leeft, 0 slechte logregels, afsluitcode 0, maar ca. 30 van de 50 stappen zijn DEAD. De eerste afwijking is stap 06: de Club-tab wordt op (692, 271) verwacht, in deze build staat het tabblad op ca. (692, 225), omdat het Instellingen-venster hoger staat (de tabs en rijen zijn na de laatste meting veranderd: Game/Display/Audio/Help/Club en een extra rij 'Opponents adapt'). Alle volgende stappen lopen daardoor uit de pas (de Instellingen blijven open). Dit is verouderd meetwerk in `scripts/playtest_exe.py` (klikcoördinaten, laatst gemeten bij RC6), geen fout in de exe of het pakket. Een herijking van ca. 30 coördinaten is niet gedaan; geschatte inzet 1-2 uur met schermafdrukken per stap.

## Niet gedraaid / voorbehoud

- De exetest is niet groen afgerond; SHA256 en de lokale tag zijn dus niet via `release.ps1` tot stand gekomen. Een volledige groene run vereist eerst de herijking hierboven.
- De speeltest draait verborgen met `--write-movie`; op dit scherm (3440x1442) wordt de 16:9-viewport gecentreerd; die letterbox-omrekening is in `click()` aanwezig en niet apart getoetst.
- Run 1 en run 2 hebben dezelfde build-, sim-, smoke- en autotestuitkomsten opgeleverd (884/884, alles groen behalve het verwachte rood).
