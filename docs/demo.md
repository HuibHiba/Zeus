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
