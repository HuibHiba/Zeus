# Release-check: één opdracht, groen of rood

`scripts/release_check.py` draait de bestaande controles voor een release-kandidaat na elkaar (roadmap A3). Er draait nooit meer dan één zwaar dotnet- of Godot-proces tegelijk; dat is een eis van de machine (weinig geheugen).

## Draaien

```powershell
python scripts/release_check.py                                   # build, sim-tests, smoke, autotests
python scripts/release_check.py --exe build/GaffersNotebook.exe   # plus speeltest op de release-exe en pakketcontrole
python scripts/release_check.py --dry-run                         # toont alleen het plan (zelftest, start niets)
python scripts/release_check.py --skip test,smoke                 # stappen overslaan; --only build,autotest voor alleen die
```

Stappen in volgorde: `build` (`dotnet build Zeus.sln -c Release -m:1`), `test` (`dotnet test sim/Zeus.Sim.Tests -c Release --no-build -m:1 -- xUnit.ParallelizeTestCollections=false`), `smoke` (`scripts/smoke_godot.py`), `autotest` (`scripts/run_autotest.py --timeout 560`, zonder de bewust rode scripts), `autotest_expected_red` (alleen de bewust rode scripts), en met `--exe`: `playtest` (`scripts/playtest_exe.py`, heeft Pillow nodig) en `package` (`scripts/check_release_content.py --package`). Na afloop altijd `dotnet build-server shutdown`. Mislukt `build`, dan stopt het script meteen.

Logs: per stap een bestand in `--out` (standaard `local/release_check/<tijd>/`), plus `result.txt` met de eindtabel. Het script leest geen uitvoer van de deelscripts; het gebruikt alleen hun exitcodes.

Verwachte duur: circa 45-60 minuten (sim-tests ~10 min, autotests ~25-30 min waarvan `9a_challenges_all` ~9 min, speeltest ~10 min). De autotests en de speeltest hebben een venster nodig: niet headless, laat de pc met rust.

## Volledige release: `scripts/release.ps1`

Eén opdracht voor een kandidaat (meerjarenplan §6.2): schone controle → build → sim-tests → smoke → UI-autotests → export + zip (`build_demo.ps1`) → archiefcontrole → exetest → SHA256 → lokale tag. Het script knoopt de bestaande scripts aan elkaar (`release_check.py --only <stap>`, `build_demo.ps1`, `prepare_playable.ps1`) en stopt bij de eerste rode stap, met daarna een tabel groen/rood/niet gedraaid per laag.

```powershell
.\scripts\release.ps1 -DryRun                    # toont de stappen, start niets (alias -WhatIf)
.\scripts\release.ps1 -Tag v0.1.0-beta.2         # echte release vanaf een schone commit, met lokale tag
.\scripts\release.ps1 -AllowDirty                # proefrun op een vuile werkboom; zip krijgt '-dirty' in de naam
.\scripts\release.ps1 -ReleaseLabel 0.1.0-beta.2 # label doorgeven aan build_demo.ps1
```

- Vuile werkboom: weigert, tenzij `-AllowDirty`. `-Tag` samen met `-AllowDirty` weigert ook; een bestaande tag ook. Er wordt nooit gepusht.
- Archiefcontrole: zippaden (`prepare_playable.ps1`), uitpakken en elk bestand vergelijken met `build/` (vangt CRC-/kopieerfouten), geen `calibration/`, en `check_release_content.py --package` op de uitgepakte map (notices, whitelist, lekken).
- Exetest: `playtest_exe.py` op de exe uit de **uitgepakte zip**, niet uit `build/`.
- Uitvoer: `dist/release-GaffersNotebook-<label>[-dirty]-<tijd>/` met de zip, `SHA256.txt`, `logs/` (per laag) en `unpacked/`.
- Duur: ruim een uur; heeft een venster nodig (autotests, speeltest). Niet draaien terwijl andere zware dotnet-/Godot-processen lopen.

## Verwacht rood

`EXPECTED_RED` in het script bevat autotestscripts die bewust rood staan (nu `10a_save_damaged`, zie `docs/hervatten.md`). Die draaien apart; rood is daar het verwachte resultaat en telt niet als fout. Wordt zo'n script groen, dan meldt de tabel `ONVERWACHT GROEN` en is de uitkomst rood: haal het script dan uit `EXPECTED_RED`. De laatste stap van de speeltest (`50_quit_bevestigen_OK` DEAD) is al in `playtest_exe.py` zelf verwacht.

Exitcode 0 alleen als elke stap `GROEN` of `VERWACHT ROOD` is; anders 1. Onbekende stapnaam of niets gekozen: 2.
