# Windows-demo WP2 (voorlopig)

De zichtbare naam en versie komen uit `godot/GameInfo.cs`. De titel is Gaffer's Notebook; `ShortTitle` verwijst voorlopig naar dezelfde constante. `VERSIE.txt` naast de exe heeft voorrang, daarna (alleen in de editor) `VERSIE.txt` in de repo-root. Een ontbrekend, leeg of onleesbaar bestand geeft `dev`. De autoload `Hotkeys` zet de venstertitel voor alle startscènes; Menu geeft de titel door aan zijn achtergrond. Ook de projectnaam is Gaffer's Notebook; de gebruikersmap blijft expliciet GaffersNotebook.

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

## Taal en vertalen

Engels is de brontaal en de standaard (`en`), Nederlands de tweede taal (`nl`). Alle schermteksten lopen via `Loc.T("sleutel", args)` in de platte UTF-8-tabellen `data/lang/en.json` en `nl.json`. Een ontbrekende Nederlandse sleutel valt terug op Engels, daarna op de sleutel zelf. Ontbrekende of beschadigde tabellen geven een waarschuwing. De menukeuze English / Nederlands bewaart `language` in `user://instellingen.json`, met behoud van andere instellingen en wedstrijdkeuzes. Bij een schrijffout verandert de sessietaal wel en verschijnt een melding. `ZEUS_LANG=en|nl` overschrijft alleen in een debugbuild/editor de begintaal en schrijft niets naar de instellingen.

Gebruik per scherm `menu.*`, `match.*`, `pause.*`, `meter.*`, `editor.*`, `season.*`, `squad.*`, `challenge.*` of `popup.*`; gedeelde tekst krijgt `common.*`. Een sleutel beschrijft de betekenis, bijvoorbeeld `editor.save_failed`. Voeg eerst de natuurlijke Engelse tekst toe, daarna de Nederlandse vertaling met exact dezelfde argumentnummers. Zet een hele zin in de tabel als de woordvolgorde kan verschillen: `Loc.T("match.skipping", percent)` gebruikt `Skipping... {0:0}%` / `Doorspoelen... {0:0}%`. .NET-opmaak, inclusief `{0:F2}`, is toegestaan. Loc formatteert argumenten met en-GB of nl-NL; technische id's en bestandsnamen blijven gelijk.

`Loc.Name` gebruikt intern `Loc.T("common.name." + id)` met kleine letters en spaties als underscores. Daar staan de ingebouwde tactiek-, rol-, instructie-, fase-, blok-, zone-, regel- en conceptlabels, fictieve club- en spelersnamen. Onbekende namen van de gebruiker blijven letterlijk bewaard. Een eigen tactiek krijgt `(own)` / `(eigen)` via `common.own`. Keuzelijsten bewaren het oorspronkelijke id als metadata; vertaalde labels worden nooit teruggeschreven als sim-invoer. Fasen/tabs krijgen een aparte zichtbare titel, geen vertaalde Node-naam. Voor getekende tabellen zijn er korte labels; lange uitleg mag afbreken. Brede opties mogen het scherm niet breder maken (`FitToLongestItem=false`).

Uitdagingen houden hun bestaande `naam`, `uitleg` en `tip` en voegen per veld `_en` en `_nl` toe. `ChallengeEvaluator.Parse(json, language)` en `LoadAll(directory, out errors, language)` kiezen dat veld, met terugval op het oorspronkelijke veld. Oude bestanden en bestaande sim-aanroepen blijven werken (zonder taalargument blijven ze Nederlands). Alle zes meegeleverde uitdagingen bevatten beide talen. Criteria worden in de UI opgebouwd uit maat-id, vergelijking en waarde via `Loc.Criterion`; de Nederlandse sim-diagnosetekst wordt niet getoond. Ook de uitleg bij een voorbereide tegenstander komt uit de taaltabel. Spelerfouten gaan via `Loc.Error`: een vertaalde melding per foutcategorie, technische details in het log. Developer-events, id's, getallen, bestandsnamen en de productnaam zijn geen vertaalbare spelzinnen.

Een sleutel toevoegen:

1. Voeg dezelfde sleutel toe in beide tabellen; vertaal vanuit het Engels en houd alle `{0}`..`{n}`-argumenten gelijk.
2. Gebruik `Loc.T` bij het maken of bijwerken van de UI. Voeg een ingebouwd datalabel ook aan `common.name.*` toe en gebruik `Loc.Name`; bewaar bij opties het id als metadata.
3. Draai onderstaande controles en bekijk het betreffende scherm met `ZEUS_LANG=en` en `ZEUS_LANG=nl`. Controleer ook de langste optie en meldingen, niet alleen het beginscherm.

```powershell
dotnet build godot/Zeus.Godot.csproj -m:1 --nologo
dotnet build-server shutdown
dotnet test sim/Zeus.Sim.Tests -m:1 --nologo -o C:/dev/zeus-i18n/.tmp_t --filter "DemoWp2Tests|LocCoverageTests|ChallengeTests"
dotnet build-server shutdown
C:/dev/zeus/.venv/Scripts/python.exe scripts/smoke_godot.py --root C:/dev/zeus-i18n
dotnet build-server shutdown
```

Maximaal één eigen dotnet-opdracht tegelijk. `DemoWp2Tests` vergelijkt sleutels, argumentnummers (ook met opmaakspecificaties), geldige opmaak en terugval. `LocCoverageTests` bewaakt directe tekstliterals bij Text/Tooltip/Dialog, UI-helpers en DrawString, controleert ingebouwde datalabels en test de tweetalige uitdagingen plus oude bestanden. Het is een statische vangrail: tekst uit variabelen vraagt nog steeds een inventarisatie en screenshotcontrole. Alleen de productnaam en de technische developerlabels PASS/TRIGGER staan in de kleine allowlist.

Screenshots: zet `ZEUS_SHOT` op een absoluut voorvoegsel en start een scène met Godot (`--path godot res://Editor.tscn`). `ZEUS_FITCHECK=9` meldt bediening buiten 1520×780; scrollinhoud mag groter zijn. `ZEUS_TAB=0..3`, `ZEUS_PAUSETAB=0|1`, `ZEUS_PAUSEMENU=1`, `ZEUS_FINISH=1` en `ZEUS_LIVEFAKE=2-0,1-1` helpen bij deelweergaven. `ZEUS_METER_SECONDS=10` verkort uitsluitend in debug/editor de speelhelften voor een snelle layoutcontrole; dit is geen volledige meting. Gebruik een apart carrière-/vertrouwdheidsbestand en `ZEUS_NOSAVE=1`. Binnen een sandbox kan een tijdelijke `APPDATA` onder de werkmap nodig zijn voor Godots logs en shadercache; de echte gebruikersbestanden blijven dan ongemoeid.

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

Grootte (2026-10-16): `build/` 181,5 MB (exe 110 MB met ingebed pck, runtime-map 77 MB, data < 0,2 MB); zip 69,9 MB. De exe is niet ondertekend (SmartScreen-melding, staat in LEESMIJ). Deze oorspronkelijke export had nog geen icoon; de huidige preset gebruikt het eigen notitieboekicoon (zie hieronder).

Getest: build en zip op deze machine; release-exe gestart vanuit `build/`, vanuit een kopie in `%TEMP%\Gaffers Test\` (spatie in pad) met werkmap `C:\Windows`: proces blijft draaien en reageert, menu rendert (Vulkan Forward+), tactieken en Engelse taaltabel geladen. Negatieve controle: dezelfde kopie zonder `data/` toont ruwe sleutels (`menu.season`) en geen tactieken, dus `DataRoot()` leest echt `<exe-map>/data/`. Een debug-export is gemaakt om het log te lezen; hij toonde alleen de Godot/Vulkan-regels (geen extra gamelog). De zip is gecontroleerd op 209 entries zonder backslashes.

Bekende problemen / niet getest:
- De i18n-wijziging zet nu `GetWindow().Title = GameInfo.Title`, plus de projectnaam, en toont de versie rechtsonder in Menu. De oude exportzip moet opnieuw gebouwd worden om dit mee te nemen.
- De huidige projectinstellingen gebruiken `gl_compatibility` voor desktop, mobile en web. Alle schermen zijn met echte OpenGL-screenshots bekeken. Er waren geen Forward+-specifieke effecten om te verwijderen. Een aparte pc zonder Vulkan en een nieuwe Windows-export zijn nog niet getest.
- Release-log: `--log-file` gaf een leeg bestand; `%APPDATA%\GaffersNotebook\logs\godot.log` wordt door een tweede gelijktijdige instantie vergrendeld.
- Niet getest: schone Windows-installatie zonder .NET of VC-runtime, andere pc/GPU, antivirus-reacties, spelen van een volledige wedstrijd/seizoen in de export, tactiek opslaan naar `user://tactieken`, taalwissel, opslaan/laden van carrière. Windows Sandbox: de status van `Containers-DisposableClientVM` kon zonder beheerdersrechten niet worden opgevraagd; er is niets ingeschakeld. De maker kan de zip daar of bij een vriend testen.
- Nieuwe `*.cs.uid`-bestanden worden genegeerd; de al gevolgde `Main.cs.uid` blijft staan. `godot/icon.svg` is een eigen notitieboek met G, `icon.ico` bevat Windows-formaten van 16 tot 256 pixels. Project en exe-preset verwijzen naar deze iconen; geen externe merken of beeldbronnen.


## Onboarding

Het welkom (`godot/Welcome.cs`) verschijnt bij de eerste menustart en blijft daarna bereikbaar via Help / Hulp. Het legt de rol van coach in de dugout uit, de vijf activiteiten en de toetsen Spatie, B, 1/4/8, F11 en F1, met de melding dat dit een vroege versie is. Sluiten met Verder, Esc of het kruisje bewaart dat het welkom is gezien. Er is uitsluitend statische uitleg, geen tutorialsysteem of wijziging van de wedstrijdlogica.

Menu toont de checklist **Your first half hour / Jouw eerste half uur**. De menu-acties staan in twee kolommen zodat alles binnen 1520x780 past zonder scrollen. Elke stap heeft een knop Go there / Ga erheen: uitdaging, gewone wedstrijd voor een wissel met B, Tactiekmaker, Tactiekmeter en Competitie. De volgorde is vrij. De vijf vinkjes komen uitsluitend van uitgevoerde acties:

- Challenges: terugkeer na minstens een afgeronde live wedstrijd; simuleren en een afgebroken wedstrijd tellen niet. Een debugtussenstand met ZEUS_LIVEFAKE telt niet.
- PauseMenu: een daadwerkelijk uitgevoerde wissel.
- Editor: een geslaagde opslag van de eigen tactiek. De wijziging in Editor.cs beperkt zich tot twee aanroepen (hulp installeren en voortgang markeren).
- Meter: een volledig geslaagde vergelijking met twee verschillende tactiekbestanden; annuleren en fouten tellen niet.
- Season: de teruggekeerde uitslag van een live competitiewedstrijd of een geslaagde gesimuleerde speelronde. Alleen een bestaande competitie openen telt niet.

`OnboardingProgress.Mark("challenge" | "substitution" | "tactic" | "meter" | "season")` bewaart via SafeFile naar `user://onboarding.json`. Formaat: `version: 1`, `welcomeSeen: bool`, `completed: string[]`. De sim-helper OnboardingState bewaakt bekende stappen, dubbel afvinken, tellen en JSON. Onbekende stappen worden genegeerd. Beschadigde of ongeldige bestanden worden apart gezet als `onboarding.bad` en geven een lege voortgang; bij onleesbare bestanden of mislukte veiligstelling blijft schrijven geblokkeerd. Schrijffouten blijven uit de schermstroom. ZEUS_NOSAVE onderdrukt schrijven van voortgang.

Menu, Challenges, Meter, Editor, Season en Squad hebben rechtsboven een ?-knop en F1 voor een modaal hulpvenster met vier korte alinea's: doel van het scherm en een eerste handeling. Sluiten kan met Verder, Esc, kruisje of F1. Teksten gebruiken Loc.T met sleutels welcome.*, onboarding.* en help.* in beide taaltabellen. OnboardingTests bewaakt ook de dynamisch opgebouwde stap- en hulpsleutels; DemoWp2Tests en LocCoverageTests blijven gelden.

Controle (2026-10-16, feat/demo-onboarding): 32 gefilterde tests geslaagd met `dotnet test sim/Zeus.Sim.Tests -m:1 --nologo -o C:/dev/zeus-onboarding/.tmp_t --filter "OnboardingTests|DemoWp2Tests|LocCoverageTests"`; Godot-build geslaagd (bestaande waarschuwing HeatmapView.Scale). Maximaal een dotnet-opdracht tegelijk; buildservers na afloop afgesloten. De eenmalige `scripts/smoke_godot.py --root C:/dev/zeus-onboarding` meldde zeven schermen met fouten doordat de sandbox user://logs buiten de werkmap niet laat schrijven. Alle schermprocessen eindigden met exitcode 0; geen schone rooktestclaim en niet herhaald.

Echte ZEUS_SHOT-PNG's zijn gelezen en visueel gecontroleerd onder `.tmp_shots/onboarding/` (ongevolgd): `en_menu_menu.png`, `nl_menu_menu.png`, `en_help_menu.png`, `nl_help_menu.png`, beide welkomvensters, Editor-hulp (EN) en Challenges-hulp (NL). ZEUS_FITCHECK=9 meldde geen overflow. Een echte beschadigde onboarding.json gaf exitcode 0 en onboarding.bad; `damaged_menu.png` toont 0/5 en `completed_menu.png` toont na laden van een volledig voortgangsbestand 5/5 met vinkjes. Voor deze controles stond APPDATA in `.tmp_appdata` onder de werkmap; echte gebruikersopslag bleef ongemoeid. Alleen de bekende rootcertificaatmelding staat in de screenshotlogs.

Debug/editor: `ZEUS_HELP=1` opent de schermhulp bij laden; `ZEUS_WELCOME=1` forceert het welkom op Menu. ZEUS_SHOT onderdrukt het automatische eerste welkom zodat de checklist kan worden opgenomen; ZEUS_WELCOME kan dat overschrijven. Niet getest: de volledige speelroute van 20-30 minuten, handmatige toetsenbord/muisbediening en de opnieuw gebouwde Windows-export. Committen werd geblokkeerd door de sandbox: gericht `git add -- sim/Zeus.Sim/OnboardingState.cs` kon `C:/dev/zeus/.git/worktrees/zeus-onboarding/index.lock` niet maken (Permission denied). Alles ongecommit; niets gepusht of gemerged.
## Feedback en foutafhandeling

**Feedbackbestand.** F9 (op elk scherm, ook in de wedstrijd; de wedstrijd staat stil zolang het venster open is) en de knop 'Feedback' in het hoofdmenu (naast de taalkeuze) en in het pauzemenu openen een klein venster met een tekstveld voor een opmerking, 'Feedback maken' (Enter) en 'Annuleren' (Esc). Het screenshot wordt genomen op het moment van F9, dus zonder het venster. Daarna volgt 'Opgeslagen op je bureaublad: <pad>' met 'Map openen' (zonder bureaublad: `user://feedback/`). Bestandsnaam `GaffersNotebook-feedback-<jjjjmmdd-uumm>.zip` (bij dezelfde minuut `-2`, `-3`). Niets wordt verzonden. Inhoud en privacy: [playtest.md](playtest.md), sectie Privacy. Een fout bij screenshot, log of een bestand blokkeert de zip niet; het komt als `note:` in `info.txt`. Het spec-onderdeel 'het laatste Godot-log, anders de ringlog' is ruimer uitgevoerd: de ringlog (`gamelog.txt`) zit er altijd in, ook als `godot.log` er is, want alleen die bevat de spelgebeurtenissen.

**Foutmelding.** `FeedbackLog.Install()` (aangeroepen door de autoload `Hotkeys`) koppelt `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` en een Godot-`Logger` (`OS.AddLogger`). Een onverwachte uitzondering gaat met type, bericht en stack (zonder gebruikersnaam) naar de ringlog en toont 'Er ging iets mis / Something went wrong' met 'Feedbackbestand maken' en 'Sluiten' (hoogstens eens per 3 s). Uitzonderingen in Godot-callbacks (`_Process`, signalen) vangt Godot zelf op en logt het als `ERROR: <Type>Exception: ...` zonder het spel te stoppen; de Logger herkent dat patroon en toont dezelfde melding. Een uitzondering die het proces toch beeindigt (bijvoorbeeld in een eigen thread) kan geen venster meer tonen: de ringlog wordt dan naar `user://feedback_vorige_crash.txt` geschreven en de volgende F9 neemt die op als `vorige_crash.txt`. Een aparte vanger rond elke sceneovergang is er niet: Godot-C# biedt geen hook voor `ChangeSceneToFile`; fouten bij het laden van een scene (`_Ready`) komen via dezelfde Godot-logmelding binnen en de bestaande `catch` in `Main._Ready` blijft staan.

**Ringlog.** `FeedbackLog` (godot/FeedbackLog.cs) gebruikt `Zeus.Sim.RingLog` (sim/Zeus.Sim/FeedbackPack.cs): thread-safe, maximaal 500 regels, elke regel wordt bij het toevoegen geanonimiseerd. Gelogd worden: spel gestart (versie, taal), scherm geopend, wedstrijd gestart en afgelopen (seed, tactieknamen, niveaus, uitslag, aantal stappen), feedbackvenster, Godot-fouten en onverwachte uitzonderingen. Geen persoonsgegevens. Logica zonder Godot (padschoonmaak `Anonymize`, bestandslijst `ExistingProgressFiles`, `WriteZip`) is getest in `FeedbackPackTests`.

**Debug (alleen met de debugschakelaars).** `ZEUS_THROW=1` laat een taak met een niet-opgevangen uitzondering falen (`UnobservedTaskException`), `ZEUS_THROW=2` gooit een uitzondering in een Godot-callback; in beide gevallen verschijnt de foutmelding. `ZEUS_FEEDBACK=ask|error|done` opent het venster, de foutmelding of maakt direct een zip; samen met `ZEUS_SHOT=<voorvoegsel>` komt `<voorvoegsel>_feedback_<ask|error|done|throw>.png` en sluit het spel af. `ZEUS_FEEDBACK_DIR=<map>` schrijft de zip daar in plaats van op het bureaublad, `ZEUS_FEEDBACK_TEXT=<tekst>` vult de opmerking vooraf.

**Versie in opslagbestanden (gecontroleerd, niets aangepast).**
- `zeus_carriere.json`: heeft `Version` (nu 1); onbekende velden worden genegeerd (System.Text.Json); een andere versie wordt geweigerd en het bestand wordt als `.bad` opzij gezet (er is nog maar een versie).
- `zeus_progress.json` (`gehaald`) en `zeus_history.json` (`geschiedenis`): geen versienummer. Godot-JSON leest alleen de bekende sleutels (onbekende velden genegeerd) en slaat kapotte items over; een andere vorm geeft `.bad`. Een toekomstige formaatwijziging moet dus een nieuw veld toevoegen of zelf een `versie` introduceren.
- `zeus_vertrouwdheid.json`: sleutel naar JSON van `FamiliarityState`, zonder versie; onbekende velden genegeerd, ongeldige waarden worden door `Validate` geweigerd.
- `instellingen.json`: platte tekst-tekst-map zonder versie; sleutels van andere versies blijven behouden (`Loc` bewaart alle sleutels), een niet-tekstwaarde maakt het bestand 'kapot' (`.bad`).
- `onboarding.json` bestaat in deze branch nog niet (andere stroom); F9 neemt het mee zodra het er is.
