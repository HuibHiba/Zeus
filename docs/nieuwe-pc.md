# Nieuwe pc instellen en overdragen

Doel: een tweede pc (of een nieuwe Claude-sessie) kan zonder uitleg verder werken aan Gaffer's Notebook ("zeus"). Deze gids staat in GitHub zodat een verse pc hem als eerste kan lezen. De dynamische stand (welke branch, wat loopt, wat is de volgende stap) staat in de Obsidian-vault, notitie `Zeus - overdracht`. De bouwstand (testaantal, commit, kalibratie) staat alleen in `docs/hervatten.md`.

## Wat staat waar

| Wat | Waar | Opmerking |
|---|---|---|
| Code, tests, docs, taaltabellen, tactieken, squads | GitHub `https://github.com/HuibHiba/Zeus.git` | clonen volstaat voor bouwen, testen en spelen |
| Ontwerp, besluiten, sessielogs, overdracht | Obsidian-vault `Zeus online` | via Obsidian Sync (of OneDrive); niet in git |
| Build-uitvoer (`build/`, `dist/`, `playable/`) | alleen lokaal | opnieuw bouwen met `scripts/build_demo.ps1` |
| Grote data (`data/` behalve `data/lang`), StatsBomb-afgeleide modellen (`calibration/event_models*.json`, `pass_360_model*.json`, `shot_360_model.json`), `local/`, `*.local.json` | bewust niet in git | alleen nodig voor de Python-kalibratiescripts; bouwen, testen en spelen werken zonder |
| Claude-instellingen, plugins en geheugen | per pc, niet in git | opnieuw instellen, zie 'Claude op de nieuwe pc' |

## Installeren (Windows, PowerShell)

```powershell
winget install --id Git.Git -e
winget install --id Microsoft.DotNet.SDK.8 -e          # .NET 8 SDK (hier 8.0.425)
winget install --id Python.Python.3.12 -e              # elke recente Python 3 werkt voor de scripts (hier 3.14.7)
winget install --id GodotEngine.GodotEngine.Mono -e    # moet Godot 4.7.2 stable .NET zijn; check de versie
winget install --id Obsidian.Obsidian -e
```

- Godot: de scripts zoeken de console-exe `Godot_v4.7.2-stable_mono_win64_console.exe` onder `%LOCALAPPDATA%\Microsoft\WinGet\Packages`. Staat Godot elders, zet `GODOT_EXE`.
- Exporttemplates (alleen nodig voor een demo-zip): `Godot_v4.7.2-stable_mono_export_templates.tpz` van de godotengine/godot-release, uitpakken naar `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\` (inhoud van de map `templates`, ca. 1,9 GB, met `version.txt`). Zie `docs/demo.md`, 'Export en zip'. `scripts/build_demo.ps1` weigert zonder.
- Python-pakketten (alleen voor data- en playtestscripts): `pip install pillow` is genoeg voor `playtest_exe.py`; `pip install -r requirements.txt` voor de volledige notebooks en kalibratie.
- Claude Code: installeren volgens de officiële instructies op claude.com en inloggen met het account van de maker.

## Repo ophalen

```powershell
mkdir C:\dev
git clone https://github.com/HuibHiba/Zeus.git C:\dev\zeus
cd C:\dev\zeus
git fetch --all
git checkout <branch uit de overdrachtsnotitie>
```

Inloggen bij GitHub gaat via de browser (Git Credential Manager) als HuibHiba. Bouw niet in OneDrive: houd de repo in `C:\dev`.

Werkbomen (`git worktree add C:\dev\zeus-<naam> <branch>`) zijn lokaal en reizen niet mee; alleen gepushte branches reizen mee. Een verse werkboom heeft twee keer `godot --headless --path godot --import` nodig voordat een build werkt.

## Eerste controle

```powershell
dotnet build Zeus.sln -c Release -m:1
dotnet test sim/Zeus.Sim.Tests -c Release -m:1 -- xUnit.ParallelizeTestCollections=false    # ca. 10 min; aantal in docs/hervatten.md
python scripts/smoke_godot.py --root C:/dev/zeus
.\play.bat                                                                                   # start het spel
```

De rest (autotest, kalibratie, positional probe, export, speeltest op de release-exe) staat in `docs/hervatten.md` en `docs/demo.md`.

Zware processen: op de eerste pc was het geheugen krap (ca. 5 GB vrij). Daarom één zwaar dotnet- of Godot-proces tegelijk, dotnet in Release met `-m:1`, xUnit-parallelisme uit en `dotnet build-server shutdown` achteraf. Heeft de nieuwe pc ruim RAM, dan mag dat ruimer.

## Afspraken met de maker (onveranderd)

- Taal: Nederlands in uitleg, commits en notities; code en identifiers in het Engels. Geen voornaamwoorden raden: they/them.
- Niets naar `main` pushen of mergen zonder expliciet akkoord. Nooit force-pushen. Feature-branches mogen wel naar GitHub, de maker wil dat alles online staat.
- Niets publiceren. De itch.io-pagina blijft Restricted; de upload en eventuele Public-zetting zijn voor de maker. De conceptmails (`docs/itch/conceptmail-auteurs.md`) nooit versturen.
- Bronvermeldingen van databronnen blijven staan (besluit maker 2026-10-18).
- Geen grote data of secrets in git. `data/` is genegeerd.
- Versie `0.1.0-beta.1+<hash>`, zip `GaffersNotebook-0.1.0-beta.1.zip`. Goldens (`GoldenMatchTests`) alleen vernieuwen na een uitgelegde wijziging en twee identieke hashes.
- Commits eindigen met de sessieregel van Claude Code (`Claude-Session: ...`) als de omgeving die opgeeft.
- Vault (Obsidian): niets verwijderen of hernoemen zonder te vragen, `.obsidian/` niet aanpassen, elke sessie sluiten met een sessielog.
- `C:\dev\zeus-ai-fix` op de eerste pc is niet van Claude; daar blijf je af.

## Overdragen tussen pc's

Aan het eind van een sessie, op de pc die je verlaat:

1. Alles committen op de werkbranch en pushen: `git push origin <branch>` (nooit `main`). Eerst `git status` en `git log origin/<branch>..` controleren; er mag niets ongepusht achterblijven.
2. Docs bijwerken: `docs/hervatten.md` (bouwstand) en, bij een besluit, `01 Ontwerp/Besluiten.md` in de vault.
3. In de vault een nieuw blok **bovenaan** `04 Planning/Zeus - overdracht.md` zetten: datum en pc, branch en commit (en dat het gepusht is), wat nu loopt of half af is, de volgende stappen, openstaande keuzes van de maker. Oude blokken blijven staan; niets verwijderen.
4. Sessielog in `05 Sessielogs/`.

Aan het begin van een sessie, op de pc die je oppakt:

1. Obsidian laten syncen en het bovenste blok van `Zeus - overdracht` lezen.
2. `git fetch --all`, de genoemde branch uitchecken en `git log -1` vergelijken met de commit in het blok.
3. `docs/hervatten.md` lezen voor de bouwstand en de eerste controle uitvoeren.
4. Pas daarna doorwerken. Werk nooit tegelijk op twee pc's aan dezelfde branch.

## Claude op de nieuwe pc

- In de repo staan `CLAUDE.md` en `AGENTS.md` met de code-afspraken; Claude Code leest `CLAUDE.md` vanzelf.
- Per-pc, niet in git: Claude-plugins en persoonlijk geheugen. De sessies op de eerste pc gebruikten de plugins ponytail en caveman (korte antwoorden) en ECC (reviewagents, hooks). Installeer ze alleen als de maker dat wil; ze zijn geen vereiste voor bouwen.
- De ECC-hooks (GateGuard) eisen dat Claude voor de eerste schrijfactie op een bestand de feiten noemt (aanroepers, rollback, letterlijke opdracht). Dat is normaal gedrag, geen fout.
- Eerste prompt voor een verse Claude-sessie: "Lees docs/nieuwe-pc.md en het bovenste blok van `Zeus - overdracht` in de vault, en ga daar verder."
