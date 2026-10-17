# Nachtplan 2026-10-17 → 18 (autonoom werk, ca. 12 uur)

Besluiten van de maker (vragenlijst, 2026-10-17) en het werkplan. Branches: werk op `demo/ui-polish` (integratie), release-kandidaat via fast-forward naar `demo/claude-afronding`. Per blok: klaar-als en stopcriterium. Verslag: bovenaan `docs/hervatten.md` ("Voor de maker") en in het vault-sessielog.

## Besluiten
| Vraag | Besluit |
|---|---|
| A1 Rechten | Maker zet acceptEdits + allowlist (dotnet, godot, python, git zonder push). Bij een prompt: blok overslaan, volgende blok. |
| A2 Slaapstand | Maker zet zelf "nooit" (netstroom, update 1 dag pauze). |
| A3 Push | Maker koos "ook main pushen" (privé-repo). Uitvoering pas aan het eind en alleen bij een volledig groene release-kandidaat; eerst `git bundle` in een lokale back-upmap. Nooit force-push. |
| A4 Verslag | `hervatten.md` + vault-sessielog; blokkades bovenaan. |
| B1 Merges | Ja, na groene build/smoke/tests; stop bij conflict in `sim/`. |
| B2 Release | `demo/ui-polish` → `demo/claude-afronding` (ff), export zonder `-Playable`. |
| B3 Rode test | Oorzaak zoeken; regressie door merge: merge terugdraaien. Goldens alleen na verklaarde wijziging en twee gelijke hashes. Na 2 uur niet groen: export van laatste groene commit. |
| B4 Scope | Vrij (binnen roadmap), maar elk punt max 1 uur en de release-kandidaat gaat voor. |
| C1 Kalibratie | Maker: doelgrens prof-amateur naar 55% profwinst. Alleen de profwinstgrens in de niveaucontrole; goalsgrenzen blijven. Documenteren in `docs/kalibratie.md`. |
| C2 Geluid | Aan: muziek 30%, effecten 60%, na technische analyse (piek, loop, loudness). |
| C3 Audioformaat | Alles PCM (import `compress/mode=0`), zodat het ook in verse checkouts en de export zo is. |
| C4 Discord | Discord-regel van de pagina; feedback via itch-reacties. |
| D1 Licenties | Restricted akkoord op huidige notices; parameters met StatsBomb-herkomst documenteren; conceptmail aan Wyscout/SkillCorner-auteurs klaarzetten (NIET versturen). Nooit Public zonder maker. |
| D2 Versie | `0.1.0-beta.1+<hash>`; lokale tag `v0.1.0-beta.1`. |
| D3 Opruimen | Gemergede worktrees (branches blijven) en oude `-dirty`-zips opruimen; stoppen met exporteren onder 10 GB vrij. |

## Werkblokken
1. Inventaris: `git status` per worktree, vrij geheugen/schijf. Klaar als: statusregel per branch.
2. Integratie in `demo/ui-polish`: fullscreen, review-sim (na eigen groene suite), later polish-branches. Klaar als: build groen, smoke 7/7.
3. Audio: PCM-import vastleggen, analyse (piek ≤ −1 dBFS, loop-naad), standaardvolume 30/60.
4. Kalibratie: profwinstgrens 55% vastleggen; meten op verse seeds 441–480 (sequentieel, 1 dotnet tegelijk); rapport in `kalibratie.md`.
5. Pagina: Discord-regel weg, Tactiekmaker-functies, geluid-zin ("generated for this game" i.p.v. "by the game itself"), cover/screenshots opnieuw.
6. Polish (vrij, max 1 uur per punt): kaft-binnenkant, rechthoek-selectie en raster in de editor, kleine UI-punten uit de review.
7. Volledige sim-suite (3 delen of één run), schone export, `playtest_exe.py -ExePath <exe>`, screenshots uit de release-exe.
8. Docs: `hervatten.md`, `build-notes.md` (risico 7), vault-sessielog, conceptmail (`docs/itch/conceptmail-auteurs.md`), StatsBomb-herkomst van parameters.
9. Back-up (`git bundle`), opruimen (D3), tag, ochtendrapport met SHA256 van de zip.

## Altijd
Geen push zonder dat blok 9 groen is; geen grenzen verruimen buiten C1; max 1 zware dotnet tegelijk; geheugen < 3 GB vrij: wachten.
