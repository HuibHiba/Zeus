# Bouwnotities: een schone zip voor itch.io

Doel: na de afronding van de sim een zip maken waarvan de versie uit een **schone commit** komt (geen `-dirty`-achtervoegsel), zodat feedback en de itch-versie exact te herleiden zijn. Gebaseerd op `scripts/build_demo.ps1` en `scripts/prepare_playable.ps1` (alleen gelezen, niets gewijzigd; ik heb geen build gedraaid) en `docs/demo.md`, sectie "Export en zip".

## Hoe de versie ontstaat

`build_demo.ps1` bepaalt de versie zo:

```powershell
$hash  = (git rev-parse --short HEAD)
$dirty = git status --porcelain --untracked-files=normal   # elke uitvoer = dirty
if ($dirty) { $hash += '-dirty' }
$stamp = "$hash $buildTag"         # buildTag = yyyyMMdd-HHmmss van het moment van bouwen
```

- `build\VERSIE.txt` bevat `"<hash>[-dirty] <yyyyMMdd-HHmmss>"` (gescheiden door een spatie) en komt in de zip. De exe leest dit bestand en toont het rechtsonder in het menu en in de feedbackbestanden.
- De zipnaam is `dist\GaffersNotebook-<hash>[-dirty]-<buildTag>.zip`.
- Elke bouw krijgt dus een nieuwe tijdstempel: twee zips van dezelfde commit hebben verschillende bestandsnamen en een andere `VERSIE.txt`. De hash blijft gelijk.
- **Dirty** wordt al waar bij elk gewijzigd gevolgd bestand **en bij elk niet-genegeerd ongevolgd bestand** (`--untracked-files=normal`). Genegeerd zijn `.tmp_*/`, `build/`, `dist/`, `playable/`, `.godot/`, `bin/`, `obj/`, `*.import`, `*.cs.uid` en `data/` (zie `.gitignore`). Een nieuw, nog niet gecommit bestand in `docs/`, `godot/`, `sim/` of `scripts/` maakt de bouw dus dirty. Voorbeeld: op het moment van schrijven is `git status` leeg behalve de nieuwe map `docs/itch/`; vóór de commit zou een bouw dus `-dirty` krijgen.
- Alle bestaande zips in `dist\` zijn `-dirty` (en `GaffersNotebook-path-test.zip` is een testbestand van 161 bytes). Upload daarvan niets naar itch.

## Stappen na de sim-afronding

Alles in PowerShell, vanuit `C:\dev\zeus-codex-integratie`. Draai maar één dotnet-opdracht tegelijk (de build-server en de sim-agent kunnen elkaar hinderen).

1. **Zorg dat de sim-agent klaar is** en dat al zijn werk gecommit is op de branch die je wilt uitbrengen. Controleer dat geen `dotnet`-proces meer loopt:

   ```powershell
   Get-Process dotnet -ErrorAction SilentlyContinue
   ```

2. **Controleer dat de werkmap schoon is** (geen uitvoer verwacht):

   ```powershell
   git status --porcelain --untracked-files=normal
   ```

   Elke regel hier zou `-dirty` geven. Los het op door te committen, te stashen of het bestand in `.gitignore` te zetten als het echt lokaal is. Let in het bijzonder op een losse testklasse in `godot/` (bijvoorbeeld de eerder genoemde `PaceProbe.cs`): die wordt anders ook in de exe gecompileerd (`docs/hervatten.md` waarschuwt hiervoor). Zorg ook dat `godot\Zeus.Godot.sln` niet achterblijft (het script maakt en verwijdert hem zelf).

3. **Noteer de commit** en controleer dat het de bedoelde is:

   ```powershell
   git log -1 --format='%h %s'
   git branch --show-current
   ```

4. **Controleer de omgeving** die het script eist (anders stopt het direct):
   - Godot 4.7.2 mono console-exe op de standaardplek, of geef `-GodotExe` mee (zie risico's).
   - Exporttemplates in `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\` met `version.txt`, `windows_release_x86_64.exe` en `windows_debug_x86_64.exe`:

     ```powershell
     Test-Path "$env:APPDATA\Godot\export_templates\4.7.2.stable.mono\windows_release_x86_64.exe"
     ```

   - Meer dan 3 GB vrij geheugen (anders wacht het script maximaal 10 x 30 s). Sluit browsers en andere zware programma's; `-SkipMemoryCheck` slaat dit over.

5. **(Aanbevolen) Controleer de inhoud op echte namen** met het bestaande script (Python, geen dotnet):

   ```powershell
   python -I scripts\check_release_content.py
   ```

   Opmerking: dit script geeft nu exit 1 met 15 treffers, allemaal op het gewone woord `Read` in C#-code (bijvoorbeeld `SafeFile.cs`, `Season.cs`): een vals alarm van de ingebouwde namenlijst. Andere namen zijn niet gevonden (gecontroleerd op 2026-10-17). Controleer dat de lijst nog steeds alleen `Read` bevat.

6. **Bouwen:**

   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts\build_demo.ps1
   ```

   Het script doet: controle templates en geheugen, `build\` leegmaken, `VERSIE.txt`, `dotnet build` van `godot\Zeus.Godot.csproj`, Godot headless export (`--export-release 'Windows Desktop'`), speldata kopiëren uit `git ls-files` (tactics, squads, challenges, competitions, data/lang; alleen `.json`, met verbodspatronen), Godot-licentieteksten (`scripts\export_notices.gd`), .NET-licenties uit het runtimepakket, `WYSCOuT_NOTICE.txt` en `FONT_OFL.txt` kopiëren, `LEESMIJ.txt` schrijven, zip maken in `dist\`, en tot slot `scripts\prepare_playable.ps1` (zie risico's).
   Verwachte eindregel: `build/: <MB>; zip: ~70 MB -> ...dist\GaffersNotebook-<hash>-<tijd>.zip` en `Klaar.`

7. **Controleer het resultaat:**

   ```powershell
   $zip = Get-ChildItem dist\GaffersNotebook-*.zip | Sort-Object LastWriteTime | Select-Object -Last 1
   $zip.Name                                    # mag geen "-dirty" bevatten
   Get-Content build\VERSIE.txt                 # <hash> <tijd>, hash == git rev-parse --short HEAD
   (Get-FileHash $zip.FullName -Algorithm SHA256).Hash
   ```

   Pak de zip uit in een lege map en kijk: `GaffersNotebook.exe`, `VERSIE.txt`, `LEESMIJ.txt`, `GODOT_LICENSES.txt`, `DOTNET_LICENSE.txt`, `DOTNET_THIRD_PARTY_NOTICES.txt`, `WYSCOuT_NOTICE.txt`, `FONT_OFL.txt`, map `data\` en map `data_Zeus.Godot_windows_x86_64\`. Er mag geen `calibration\`, csv, jsonl of `.tmp_*` in staan. Start de exe vanaf een andere werkmap dan de repo (bijvoorbeeld via een snelkoppeling of vanuit `C:\Windows`) om te zien dat `data\` naast de exe wordt gevonden.

   Als `git status` na de bouw niet meer schoon is, is er iets in de repo geschreven: onderzoek dat voordat je uploadt.

8. **Upload** met butler volgens `instellingen.md`. Gebruik de korte hash uit `VERSIE.txt` in het `--userversion`-label.

## Als het niet schoon wordt

- Commit eerst alle bedoelde wijzigingen (ook deze map `docs/itch/`; de commit van deze notities is bedoeld om de werkmap schoon te houden).
- Bouwen vanuit een aparte schone checkout of worktree is de veiligste manier (`git worktree add ..\zeus-release <commit>`), maar let op: de repo gebruikt `.godot/` (niet in git) en het script leest `godot\.godot\mono\temp\obj\project.assets.json`; in een verse checkout bestaat die map niet totdat Godot de projectresources heeft geïmporteerd en de C#-assets zijn opgebouwd. Voor de eerste keer in een verse checkout: `godot.exe --headless --path godot --import` (Godot, geen dotnet-build) en dan het script. Dit is niet in deze sessie getest.

## Risico's en aandachtspunten in het script

1. **Hardcoded pad naar Godot.** De standaardwaarde van `-GodotExe` verwijst naar `C:\Users\lth\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_...\Godot_v4.7.2-stable_mono_win64_console.exe`. Op een andere machine of gebruiker faalt het script direct met "Godot-console-exe niet gevonden"; geef dan `-GodotExe <pad>` mee. Dit is de console-exe, niet de gewone editor-exe.
2. **Exporttemplates ontbreken** (`%APPDATA%\Godot\export_templates\4.7.2.stable.mono\`, ca. 1,9 GB uitgepakt). Het script controleert `version.txt` (moet exact `4.7.2.stable.mono` zijn) en de twee Windows-templates en stopt anders. Een nieuwe Godot-versie vraagt nieuwe templates en `-TemplatesVersion`.
3. **Godot geeft bij exportfouten vaak exit 0.** Het script vangt dat op met een controle op `^(ERROR|SCRIPT ERROR):`-regels en het bestaan van de exe. Een fout die niet met "ERROR:" begint, kan dus door het net glippen: bekijk de laatste 15 regels van de uitvoer die het script toont.
4. **De bouw volgt de werkmap, de data volgt git.** Code en resources komen uit de werkmap (inclusief niet-gecommitte wijzigingen), speldata uit `git ls-files` (de index). Een nieuw databestand dat nog niet met `git add` is toegevoegd, komt niet mee. Bij een schone werkmap is dit gelijk; daarom is stap 2 belangrijk.
5. **`-dirty` wordt vóór de bouw vastgesteld.** Wijzigingen die tijdens de bouw ontstaan (zoals een aangemaakte `godot\Zeus.Godot.sln`, die het script daarna verwijdert) tellen niet mee; wijzigingen die ná de dirty-controle maar vóór het exporteren worden gemaakt (door een andere agent of proces) ook niet. Draai niets anders in de repo tijdens de bouw.
6. **Tijdstempel in de naam.** `VERSIE.txt` en zipnaam bevatten de bouwtijd, dus de zip is niet reproduceerbaar byte voor byte en een tweede bouw geeft een andere bestandsnaam.
7. **Nevenwerking van `prepare_playable.ps1`** (aan het eind aangeroepen). Het pakt de zip uit onder `playable\`, start de echte exe headless met een tijdelijke `APPDATA` (30 s time-out), en **overschrijft de snelkoppeling `Gaffer's Notebook.lnk` op het echte Windows-bureaublad** (nu het OneDrive-bureaublad van de maker). De kopregel van `build_demo.ps1` en `docs/demo.md` zeggen nog "schrijft alleen naar build/ en dist/"; dat is achterhaald. Faalt de startcontrole of is het bureaublad niet beschikbaar, dan gooit het script een fout ná het maken van de zip: de zip in `dist\` is dan wel klaar, maar de eindregel `Klaar.` en de exitcode zijn niet groen. Wil je dat niet voor een release, voer dan alleen de stappen tot en met de zip uit (of laat de aanroep vervallen); het script heeft hiervoor geen schakelaar.
8. **LEESMIJ is hard in het script ingebakken** en noemt zich "open beta candidate / kandidaat open bèta", niet "demo". Het noemt de SkillCorner- en DFL-bronvermelding niet (zie `licenties-check.md`, punt 3) en heeft geen copyrightregel. Pas de tekst in het script aan na de sim-afronding als je die woorden niet wilt.
9. **Verbodspatronen en notices.** Het verbod `wyscout` (op het pad) is bedoeld voor databestanden; `WYSCOuT_NOTICE.txt` wordt pas ná de eindcontrole gekopieerd. Verplaats je die kopie ooit vóór de eindcontrole, dan valt de bouw om op zijn eigen verbodspatroon.
10. **Afhankelijkheid van `project.assets.json`.** De .NET-runtimelicenties worden gezocht via `godot\.godot\mono\temp\obj\project.assets.json`; ontbreekt die (verse checkout, schone `.godot`), dan stopt het script met "Licenties van de meegeleverde .NET-runtime niet gevonden" nadat de export al klaar is.
11. **Versiemetadata in de exe.** In `godot\export_presets.cfg` staan `file_version` en `product_version` vast op `0.1.0.0` en `application/copyright=""` is leeg; Windows Eigenschappen > Details toont dus geen copyright en niet de commit-hash. `modify_resources=true` vraagt bovendien dat Godot een `rcedit`-pad kent (Editor Settings > Export > Windows) om icoon en metadata te schrijven; of dat op deze machine is ingesteld, heb ik niet gecontroleerd (de vorige zips tonen wel hun icoon volgens `docs/demo.md`).
12. **Niet ondertekend.** `codesign/enable=false`; SmartScreen verschijnt dus (zie `instellingen.md`).
13. **Geen eigen controle op alleen-schone-commit.** Het script weigert niet bij `-dirty`; het zet alleen het achtervoegsel. Een `-dirty` zip kan dus per ongeluk naar itch gaan. Voorstel voor later (niet gedaan, script blijft ongewijzigd): `if ($dirty -and -not $AllowDirty) { throw }` of in de butler-stap `if ($zip.Name -match 'dirty') { stop }`.
14. **Archief zonder hoofdmap.** De zip heeft geen bovenliggende map; wie met "Hier uitpakken" werkt, krijgt ruim 200 bestanden los in zijn map. `LEESMIJ.txt` zegt "pak de hele zip uit naar een gewone map", en de itch-tekst herhaalt dat.
15. **Geheugen en buildservers.** Het script sluit aan het eind de dotnet-buildserver (`dotnet build-server shutdown`), maar bij een fout eerder in de keten blijft die kunnen draaien en bezet geheugen; sluit hem dan zelf.

## Verse checkout: Godot-import (gecontroleerd 2026-10-17)
Een nieuwe worktree heeft geen `godot/.godot/`-cache en `*.import` is genegeerd. Draai vóór rooktest of export tweemaal `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path godot --import` (de eerste ronde importeert niet alle nieuwe lettertypen). Daarna: `python scripts/smoke_godot.py --root <map>` met aparte `APPDATA` en `ZEUS_NOSAVE=1` gaf voor alle zeven schermen OK op `8ebd89f`.

## UI-autotests (ZEUS_AUTOTEST)

`godot/AutoTest.cs` speelt een script af met **echte invoer**: muis- en toetsevents gaan via `Input.ParseInputEvent`, dus door dezelfde route als een echte muis (knoppen, sliders, tabs, popups, sneltoetsen). Alleen in debug- en editorbuilds: de driver leest `ZEUS_AUTOTEST` via `GameSession.DebugEnvironment`, die in een release altijd leeg is, dus in de itch-zip bestaat hij niet.

Script: een JSON-lijst met stappen (of een tekstbestand met één stap per regel; `#` is commentaar). Coördinaten zijn viewportcoördinaten (1520x780), ongeacht de venstergrootte.

| Stap | Betekenis |
|---|---|
| `wait 30` / `wait 1.5s` | frames of seconden wachten |
| `click Next` | de bovenste zichtbare knop of tab met deze tekst (of tooltip), hoofdletterongevoelig; wacht tot de timeout tot hij er is |
| `click 760 400 [shift]` | klik op een punt, eventueel met Shift |
| `click_node music_volume 0.8 0.5` | node op naam (Godot-patroon) of `#Klasse`, op een fractie van zijn rechthoek |
| `drag 229 205 305 415` | indrukken, in 12 stappen slepen, loslaten |
| `key Z ctrl` / `key Escape` | Godot-toetsnaam, eventueel `ctrl`/`shift` |
| `expect_node TacticLibrary` / `expect_node !#Cover` | zichtbare node bestaat (of met `!`: niet meer) |
| `expect_text 80%` / `expect_text !Skip tour` | zichtbare Label, RichTextLabel of knop bevat de tekst (of niet) |
| `timeout 60` | hoe lang `expect_*` en `click` blijven proberen (standaard 5 s) |
| `shot naam` | screenshot `naam.png` |
| `scene Editor` / `quit` | ander scherm / stoppen |

Uitvoer: `autotest.log` met PASS/FAIL per stap in `ZEUS_AUTOTEST_OUT` (standaard `user://autotest`), exitcode 0/1. Getekende tekst (spelerkaart, clipbanner van de samenvatting) is geen node: die controleer je met `shot`.

Runner: `python scripts/run_autotest.py [--only 1_menu,4_match] [--root C:/dev/zeus]` draait elk `scripts/autotest/*.json` met een **venster** (niet headless), een eigen lege APPDATA in `.tmp_appdata/autotest_<naam>` (dus eerste start met kaft en tour), `ZEUS_NOSAVE=1` en `ZEUS_LANG=en`. Regels `env NAAM=WAARDE` in een script zet de runner als omgevingsvariabele. Log, Godot-uitvoer en screenshots komen in `.tmp_autotest/<naam>/`. Het project moet gebouwd zijn (of geef `--build`).
