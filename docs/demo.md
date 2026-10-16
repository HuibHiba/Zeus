# Windows-demo WP2 (voorlopig)

De zichtbare naam en versie komen uit `godot/GameInfo.cs`. De titel is Gaffer's Notebook; `ShortTitle` verwijst voorlopig naar dezelfde constante. `VERSIE.txt` naast de exe heeft voorrang, daarna (alleen in de editor) `VERSIE.txt` in de repo-root. Een ontbrekend, leeg of onleesbaar bestand geeft `dev`. De autoload `Hotkeys` zet de venstertitel voor alle startscènes; Menu geeft de titel door aan zijn achtergrond. De interne projectnaam blijft Zeus online.

## Bestanden naast de demo

`GameSession.DataRoot()` kiest met `OS.HasFeature("editor")` de repo-root boven `godot/`. Een export kiest uitsluitend `<exe-map>/data/`, onafhankelijk van de huidige werkmap. `TacticsDir`, `SquadsDir`, `ChallengesDir` en `CompetitionsDir` zijn submappen hiervan. Competities zijn momenteel nog in de sim-code gedefinieerd; de helper reserveert de bestandslocatie.

Een distributiemap moet bevatten:

```text
GaffersNotebook.exe (+ Godot/.NET-exportbestanden)
VERSIE.txt                         (optioneel)
data/
  tactics/*.json
  squads/*.json
  challenges/*.json
  competitions/                    (gereserveerd)
  lang/en.json
  lang/nl.json
```

De taaltabellen komen uit `data/lang/` in de repo. Kopieer de benodigde speldata expliciet naar de externe data-map bij export; Godot pakt bestanden buiten zijn projectmap niet automatisch in. Kopieer niet blind de gehele lokale `data/`-map: daarin staan ook meetbestanden en lokaal gelicentieerde bronnen. Deze stap maakt nog geen exportpreset of distributiepakket.

## Gebruikersmap en eigen tactieken

`config/use_custom_user_dir=true` en `config/custom_user_dir_name="GaffersNotebook"` geven op Windows normaal `%APPDATA%/GaffersNotebook/` (`OS.GetUserDataDir()` is de werkelijke locatie). Hiermee hangt opslag niet af van de interne projectnaam. Bestaande opslag uit de oude Godot-projectmap verhuist niet automatisch.

Een export schrijft eigen tactieken uitsluitend in `user://tactieken/`; Nieuw, Plakken/importeren en Opslaan gebruiken dezelfde schrijfmap. De map ontstaat bij het opslaan. In de editor blijven bewerkingen in repo-`tactics/` toegestaan. Openen van een tactiek maakt eerst een bewerkbare kopie; ingebouwde exportbestanden worden nooit overschreven. Een nieuwe naam die al in de schrijfmap bestaat wordt geweigerd; opnieuw opslaan van de zojuist opgeslagen eigen kopie blijft mogelijk.

Menu, Editor Open, Challenges, Season, Meter en SquadScreen gebruiken dezelfde catalogus: ingebouwde tactieken plus eigen bestanden, met `(eigen)` achter eigen namen. Gelijke bestandsnamen blijven afzonderlijk selecteerbaar. De catalogus bewaart voor een eigen bestand de draagbare verwijzing `own@<bestandsnaam>.json`; een ingebouwd bestand houdt zijn gewone bestandsnaam. `@` ontstaat niet uit de naamopschoning in de editor. Season bewaart deze verwijzing en lost hem zowel live als bij simuleren op via de eigen map; absolute machinepaden komen niet in de carrière. Oude carrières met kale bestandsnamen blijven geldig. AI-tegenstanders en hun adaptieve pool gebruiken de ingebouwde tactieken.

De bestaande bestanden `zeus_carriere.json`, `zeus_progress.json`, `zeus_history.json` en `zeus_vertrouwdheid.json` blijven in `user://`. `SafeFile` schrijft instellingen atomair met `.tmp` en hernoemen; beschadigde instellingen worden eerst `instellingen.bad`, onleesbare bestanden worden niet overschreven.

## Taalbasis

`Loc.T("sleutel")` en `Loc.T("sleutel", args)` lezen platte JSON-tabellen. Standaard is Engels (`en`); een ontbrekende Nederlandse sleutel valt terug op Engels, daarna op de sleutel zelf. Argumenten gebruiken .NET-formaatvelden zoals `{0}`. Ontbrekende/beschadigde tabellen geven een waarschuwing en dezelfde terugval. De menu-keuze English / Nederlands bewaart `language` in `user://instellingen.json` via SafeFile en herbouwt het menu met behoud van wedstrijdkeuzes. Bij een schrijffout verandert de sessietaal wel en verschijnt een melding dat bewaren mislukte.

Alle eigen menuteksten en de zichtbare menukoppen/formatiebalken zijn omgezet. Tactieknamen, de gevraagde aanduiding `(eigen)` en doorgegeven foutdetails behouden hun oorspronkelijke tekst. Andere schermen volgen later; zie Demo WP2 in `hervatten.md`. `DemoWp2Tests` controleert dezelfde sleutels in beide tabellen, stringwaarden, opmaak en terugval.

## Debugbediening

Alle `ZEUS_*`-omgevingsvariabelen gaan door `GameSession.DebugEnvironment`, uitsluitend actief bij `OS.IsDebugBuild()` of `OS.HasFeature("editor")`. Dat omvat screenshots, automatische acties, replay, tactiek-/niveau-overrides en alternatieve opslagpaden. D en de afzonderlijke debuglagen F/T/P/O/E hebben dezelfde beperking. In een release-export doen deze schakelaars niets; een debug-export ondersteunt ze nog wel. De release-menutekst vermeldt D niet. Normale bediening (pauze, snelheid, doorspoelen, Esc en F11) blijft beschikbaar.

`scripts/smoke_godot.py` blijft ongewijzigd en wordt eenmaal aan het einde gedraaid. Het bouwt zelf en laadt alle zeven schermen headless met de editorbinary; dat controleert laden/crashes, geen muisbediening en geen echte Windows-release-export.

## Wedstrijdpopups

Tijdens de wedstrijd verschijnen popups boven het veld voor doelpunten (groot, met scorer, minuut en nieuwe stand), gele en rode kaarten, wissels, penalty's, rust, tweede helft en het eindsignaal, en kleine stroken onderin voor overtredingen, buitenspel en hoekschoppen. In het pauzemenu (B) rechtsboven staat 'Popups: Alle / Alleen belangrijke / Uit'; de keuze wordt bewaard. Bij 16x snelheid verschijnen alleen doelpunten, rode kaarten, rust en eind; doorspoelen (END) toont alleen de eindstand.

Debug (alleen met de debugschakelaars, zie 'Debugbediening'): `ZEUS_POPUPDEMO=1` toont elk popuptype na elkaar en slaat met `ZEUS_SHOT=<voorvoegsel>` per type een screenshot op (`<voorvoegsel>_popup_goal.png`, `_yellow`, `_red`, `_secondyellow`, `_sub`, `_foul`, `_offside`, `_penalty`, `_corner`, `_halftime`, `_secondhalf`, `_fulltime`, `_abandoned`) en sluit daarna af. Starten kan rechtstreeks met `Godot --path godot res://Main.tscn`. `ZEUS_FINISH=1` toont het eindsignaal over de eindstandkaart.

## Export en zip

WP3 levert `godot/export_presets.cfg` (preset 'Windows Desktop', x86_64, `embed_pck=true`, .NET-uitvoer als map `data_Zeus.Godot_windows_x86_64` naast de exe, `dotnet/embed_build_outputs=false`) en `scripts/build_demo.ps1`. Het script gebruikt de Godot 4.7.2 mono-console-exe (parameter `-GodotExe`) en schrijft alleen naar `build/` en `dist/` (beide in `.gitignore`).

Eenmalig: de officiële exporttemplates `Godot_v4.7.2-stable_mono_export_templates.tpz` (GitHub-release godotengine/godot, 1.202.598.411 bytes, SHA-512 gecontroleerd tegen `SHA512-SUMS.txt`) staan uitgepakt in `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\` (de inhoud van de map `templates` uit de .tpz, ca. 1,9 GB; `version.txt` = `4.7.2.stable.mono`). Het script weigert te draaien zonder die map.

Bouwen: `powershell -ExecutionPolicy Bypass -File scripts\build_demo.ps1` (wacht tot vrij geheugen > 3 GB, `-SkipMemoryCheck` slaat dat over). Stappen: templates- en geheugencontrole; `build/VERSIE.txt` (korte git-hash, `-dirty` bij niet-gecommitte wijzigingen in gevolgde bestanden, datum); `dotnet build`; tijdelijk `godot/Zeus.Godot.sln` aanmaken (Godot-mono eist die bij export, hij staat niet in git; het script verwijdert hem weer); `godot --headless --export-release` met absoluut exportpad (een relatief `..\build\...` gaf een keer 'export path doesn't exist'); de exportlog wordt op `ERROR:`-regels gecontroleerd omdat Godot dan toch exit 0 geeft; kopiëren van speldata; `build/LEESMIJ.txt` (EN + NL); `dist/GaffersNotebook-<hash>.zip` (met `/` in de entrynamen); `dotnet build-server shutdown`.

Meegenomen data (alleen bestanden uit git, alleen `*.json`): `data/tactics/` (6), `data/squads/` (3), `data/challenges/` (6), `data/competitions/` (2: schema en `profielen.json`, openfootball-uitslagen, nog gereserveerd) en `data/lang/en.json`, `nl.json`. Bewust niet: `calibration/` (StatsBomb-afgeleide modellen en `wyscout_models.json`: Wyscout pas in het product na bevestiging, zie `DATA_SOURCES.md`), ruwe/afgeleide referentiedata in lokaal `data/`, `.tmp_*`, `*.local.json`, `local/`, csv/jsonl. Het script controleert dit met een verbodspatroonlijst en breekt af als er toch iets verbodens in `build/` staat. Let op: StatsBomb-afgeleide getallen zitten nog in code (`Shooting.cs`, `DuelModel.cs`), zie `DATA_SOURCES.md`; dat valt buiten dit pakket maar is voor een verspreiding aan vrienden wel iets om te weten.

Grootte (2026-10-16): `build/` 181,5 MB (exe 110 MB met ingebed pck, runtime-map 77 MB, data < 0,2 MB); zip 69,9 MB. De exe is niet ondertekend (SmartScreen-melding, staat in LEESMIJ). Geen icoon (project heeft geen icon.svg), alleen productnaam/bedrijf/versie in de exe-resources.

Getest: build en zip op deze machine; release-exe gestart vanuit `build/`, vanuit een kopie in `%TEMP%\Gaffers Test\` (spatie in pad) met werkmap `C:\Windows`: proces blijft draaien en reageert, menu rendert (Vulkan Forward+), tactieken en Engelse taaltabel geladen. Negatieve controle: dezelfde kopie zonder `data/` toont ruwe sleutels (`menu.season`) en geen tactieken, dus `DataRoot()` leest echt `<exe-map>/data/`. Een debug-export is gemaakt om het log te lezen; hij toonde alleen de Godot/Vulkan-regels (geen extra gamelog). De zip is gecontroleerd op 209 entries zonder backslashes.

Bekende problemen / niet getest:
- Venstertitel blijft `Zeus online` (in de debug-export `Zeus online (DEBUG)`) in plaats van `Gaffer's Notebook — <versie>`: `DisplayServer.WindowSetTitle` in `Hotkeys._Ready` heeft in de export geen effect (waarschijnlijk wordt de titel van het hoofdvenster daarna weer overschreven; `GetWindow().Title` proberen). Daardoor staat de versie nergens zichtbaar. Voor Codex.
- Het project gebruikt 'Forward Plus' (Vulkan); op een pc zonder Vulkan start het spel niet. `gl_compatibility` is niet getest en niet ingesteld.
- Release-log: `--log-file` gaf een leeg bestand; `%APPDATA%\GaffersNotebook\logs\godot.log` wordt door een tweede gelijktijdige instantie vergrendeld.
- Niet getest: schone Windows-installatie zonder .NET of VC-runtime, andere pc/GPU, antivirus-reacties, spelen van een volledige wedstrijd/seizoen in de export, tactiek opslaan naar `user://tactieken`, taalwissel, opslaan/laden van carrière. Windows Sandbox: de status van `Containers-DisposableClientVM` kon zonder beheerdersrechten niet worden opgevraagd; er is niets ingeschakeld. De maker kan de zip daar of bij een vriend testen.
- Godot maakte tijdens de export `*.cs.uid`-bestanden in `godot/` aan (ongevolgd, niet gecommit); `Main.cs.uid` staat wel in git.
