# itch.io-paginatekst

Kant-en-klare tekst voor de itch.io-pagina. Engels staat bovenaan (publiek), de Nederlandse versie eronder. Plak de blokken in de velden van de pagina-editor (itch ondersteunt Markdown in het veld "Description"). Alles tussen `<...>` moet de maker invullen. Er staan bewust geen beweringen in over realisme, echte competities, clubs of spelers.

De Nederlandse versie hoort als tweede blok in dezelfde beschrijving (onder een kopje), niet als aparte pagina.

---

## Velden naast de beschrijving

| Veld | Waarde |
|---|---|
| Title | Gaffer's Notebook |
| Project URL | `trainertoto.itch.io/gaffers-notebook` (itch-gebruikersnaam aangenomen; controleer of die klopt, gebruik dezelfde slug in butler) |
| Short description / tagline (max. ca. 100 tekens) | EN: `A football tactics sandbox: draw your plan in a notebook, then coach the match.` / NL: `Een voetbaltactiek-sandbox: teken je plan in een notitieboek en coach de wedstrijd.` (itch heeft een enkele tagline; kies Engels en zet NL in de beschrijving) |
| Release status | In development (of Prototype) |
| Version label (userversion) | zie `instellingen.md`, bijvoorbeeld `0.1.0-beta.1` |

---

## Beschrijving, Engels

# Gaffer's Notebook (work-in-progress demo)

**A football tactics sandbox where your notebook is the game.** Draw how your team builds up, presses and defends, then watch a full simulated match play out and step in as the coach.

> **Early work-in-progress demo.** This is a preliminary build. Things are unfinished, will change, and old progress may stop working after an update. Please read "Known limitations" below before you play.

### What is it?

Gaffer's Notebook is a single-player football tactics game for Windows. You are the manager in the dugout. You set up formations, roles, pressing and team shape in a tactic editor, then play a match in a top-down 2D view. Every match is a complete 90-minute simulation. The same seed and the same tactics reproduce the same match on the same build and platform.

All clubs, players and competitions in the game are **fictional**.

### What can you do in the first 30 minutes?

On first start a short guided tour spotlights the main menu (pick a tactic, Tactic Maker, kick off, Tactic Meter). Skip it with Esc; restart it from Esc > Help and feedback. In your first match one card explains coaching with B, and the first full-time card explains the result, statistics and heatmaps. The main menu also has a checklist, "Your first half hour". You can do the steps in any order:

1. **Play a live challenge match.** Pick one of ten challenges with a concrete goal (for example score against a low block, or keep a clean sheet).
2. **Make a substitution (B).** Press B and the game offers coaching at the next stoppage: swap players or change the tactic.
3. **Create and save your own tactic** in the Tactic Maker. Drag players on the pitch per phase of play, set roles and instructions.
4. **Compare two tactics in the Tactic Meter.** It plays many simulated matches and shows how two tactics differ. It measures differences inside the simulation; it does not say anything about real football.
5. **Play a league match** in the small fictional league, live or simulated.

A standard match takes about 5 real minutes (4 to 5, plus any pauses you take). Esc > Game > Match length also has a slower "Relaxed" pace (about 8 minutes) and "Tactical study" (slower still). You can also share tactics with friends as a file (export and import in the Tactic Maker).

### New in this build

- **Guided tour** on first start, plus one card about coaching and one about the full-time screen (see above).
- **Music and sound effects, all made for this game.** Everything is generated for this game; no external samples or recordings. Music volume and effects volume are in Esc > Settings.
- **About** screen (About / Over) from the main menu, with the sources, fonts and licence notes.
- **Nine tactics: six from the start, three unlock with stars** (new: 4-4-2 long ball, 3-5-2 flank press, wide 4-3-3 wing play).
- **Improved Tactic Meter and screen layouts**, including the league screen, Settings and Squad on smaller windows.
- **Ten challenges with stars.** Stars unlock extra tactics for Challenges and for your home side choice in the menu: long ball at 3 stars, flank press at 7, wing play at 9.
- **Tactic Maker tools:** undo/redo, test a tactic against an opponent, explained example tactics and opponent scouting.
- **Match highlights**, a tactic library you can share from, a player card in the match, and a club of your own (name, two kit colours, crest) with a season goal set by the club.

### Controls

| Key | Action |
|---|---|
| Space | Pause / resume |
| B | Coaching: substitutions and tactic changes at the next stoppage |
| 1 / 4 / 8 | Match speed |
| End | Calculate the rest of the match quickly |
| Esc | Settings (match length, music and effects volume, language, window mode, vsync, frame rate, help, feedback) |
| F9 | Create a feedback file (see Privacy) |
| F1 or ? | Explain the current screen |
| F11 | Fullscreen |

Keyboard and mouse only. There is no gamepad support yet.

### System requirements

- Windows 10 or 11, 64-bit
- A graphics card with OpenGL 3.3 support (Vulkan is not needed)
- Screen of at least 1024×768 (minimum supported; text is small there and the game shows bars above and below). Best on a 16:9 screen of 1280×720 or larger; 1920×1080, 2560×1440 and 4K fill the screen
- Around 200 MB free disk space; the download is about 73 MB
- No installation, no Godot, no .NET and no Python needed: unzip and run `GaffersNotebook.exe`

### Known limitations (please read)

- **This is a preliminary demo.** It is a work in progress; features, menus and balance will change.
- **The player AI is still rough.** Players can stand still, bunch up or run in odd ways, and there is little combination play. Matches can show strange behaviour.
- **Calibration is provisional.** Numbers such as shots, fouls, cards and scorelines are only roughly tuned and not balanced yet, especially at amateur level. Do not read the results as predictions or as a statement about real football.
- **Names are fictional.** Clubs, players and seasons are made up; there are no real licences, clubs or players.
- **Sound is basic.** The music and effects are simple, generated sounds, not recordings, and are not tuned for every setup. Use the volume sliders in Settings.
- Old progress and saved files may stop working after an update.
- Not tested on every PC, and not on a clean Windows install without development tools.
- English and Dutch only. Some technical logs are in English.

### Windows warning ("Windows protected your PC")

The game is not code-signed, so Windows SmartScreen may show a blue "Windows protected your PC" window with "Unknown publisher". That is expected for a small indie build. If you trust the source, click **More info**, then **Run anyway**. Some antivirus programs may also warn about an unknown program. You are free not to run it. The game does not use the internet and sends nothing.

### Privacy and feedback

- No account, no telemetry, no network traffic. The game never sends anything.
- If something goes wrong, press **F9** (or Esc > Help and feedback). The game creates one zip file on your desktop with a screenshot of the game window, system details (Windows version, processor, memory, graphics card name), logs and your saved progress. Your Windows user name is removed from file paths.
- **The file stays on your computer.** You decide whether to send it. Check the contents first and delete what you do not want to share.
- Where to send feedback: one place only, the comments on this itch page. Describe the problem there; the maker asks for the zip privately if needed. Do not post feedback zips publicly: they contain logs, system details and your progress.

### Credits and licences

Made with the Godot Engine (MIT licence). All music and sound effects were made for this game and were generated for this game; no external samples are used. The same credits are in the About screen in the main menu. Fonts: Barlow, Big Shoulders Display, IBM Plex Sans and Kalam (SIL Open Font License 1.1). Some game numbers were derived from public data sets; the required attributions and licence texts are in the download (`LEESMIJ.txt`, `GODOT_LICENSES.txt`, `DOTNET_*.txt`, `WYSCOuT_NOTICE.txt`, `BRONNEN_NOTICE.txt`, `FONT_OFL.txt`). The game is not endorsed by any data provider.

(c) 2026 TrainerToto. All rights reserved.

---

## Beschrijving, Nederlands

# Gaffer's Notebook (voorlopige demo, work in progress)

**Een voetbaltactiek-sandbox waarin je notitieboek het spel is.** Teken hoe je team opbouwt, druk zet en verdedigt, kijk naar een volledige gesimuleerde wedstrijd en grijp in als coach.

> **Voorlopige demo van een onaf spel.** Dit is een eerste testversie. Er is veel nog niet af, dingen veranderen en oude voortgang kan na een update onbruikbaar worden. Lees "Bekende beperkingen" hieronder voordat je speelt.

### Wat is het?

Gaffer's Notebook is een singleplayer-voetbaltactiekspel voor Windows. Jij bent de trainer in de dugout. In de Tactiekmaker stel je opstelling, rollen, druk en teamvorm in, daarna speel je de wedstrijd in een 2D-bovenaanzicht. Elke wedstrijd is een complete simulatie van 90 minuten. Dezelfde seed en dezelfde tactiek geven op dezelfde build en hetzelfde platform dezelfde wedstrijd.

Alle clubs, spelers en competities in het spel zijn **verzonnen**.

### Wat kun je doen in de eerste 30 minuten?

Bij de eerste start laat een korte rondleiding de onderdelen van het hoofdmenu zien (tactiek kiezen, Tactiekmaker, aftrappen, Tactiekmeter). Sla hem over met Esc; start hem opnieuw via Esc > Hulp en feedback. In je eerste wedstrijd legt één kaart het coachen met B uit, en de eerste eindkaart legt uitslag, statistieken en heatmaps uit. Het hoofdmenu bevat ook de checklist "Jouw eerste half uur". De volgorde is vrij:

1. **Speel een live uitdagingswedstrijd.** Kies een van de tien uitdagingen met een concreet doel (bijvoorbeeld scoren tegen een laag blok of de nul houden).
2. **Doe een wissel (B).** Druk op B; bij de volgende onderbreking kun je spelers wisselen of de tactiek aanpassen.
3. **Maak en bewaar je eigen tactiek** in de Tactiekmaker. Sleep spelers over het veld per spelfase, stel rollen en instructies in.
4. **Vergelijk twee tactieken in de Tactiekmeter.** Die speelt veel gesimuleerde wedstrijden en laat zien waarin twee tactieken verschillen. Dit meet verschillen binnen de simulatie; het zegt niets over echt voetbal.
5. **Speel een competitiewedstrijd** in de kleine fictieve competitie, live of gesimuleerd.

Een standaardwedstrijd duurt ongeveer 5 echte minuten (4 tot 5, plus eigen pauzes). Via Esc > Spel > Wedstrijdduur kies je ook "Rustig" (circa 8 minuten) of "Tactisch analyseren" (nog langzamer). Je kunt tactieken als bestand delen met vrienden (exporteren en importeren in de Tactiekmaker).

### Nieuw in deze build

- **Rondleiding** bij de eerste start, plus een kaart over coachen en een over het eindscherm (zie hierboven).
- **Muziek en geluidseffecten, allemaal voor dit spel gemaakt.** Alles is voor dit spel gegenereerd; geen externe samples of opnames. Muziek- en effectvolume staan in Esc > Instellingen.
- **About / Over**-scherm vanuit het hoofdmenu, met bronnen, lettertypen en licentie-opmerkingen.
- **Negen tactieken: zes vanaf de start, drie speel je vrij met sterren** (nieuw: 4-4-2 lange bal, 3-5-2 flankdruk, breed 4-3-3 vleugelspel).
- **Verbeterde Tactiekmeter en schermindeling**, onder meer het competitiescherm, Instellingen en Selectie op kleinere vensters.
- **Tien uitdagingen met sterren.** Sterren spelen extra tactieken vrij voor Uitdagingen en voor je thuisploeg-keuze in het menu: lange bal bij 3 sterren, flankdruk bij 7, vleugelspel bij 9.
- **Tactiekmaker-hulpmiddelen:** ongedaan maken/opnieuw, een tactiek testen tegen een tegenstander, uitleg bij voorbeeldtactieken en de tegenstander verkennen.
- **Wedstrijdhoogtepunten**, een tactiekbibliotheek om uit te delen, een spelerskaart in de wedstrijd en een eigen club (naam, twee shirtkleuren, embleem) met een seizoensdoel dat de club bepaalt.

### Bediening

| Toets | Actie |
|---|---|
| Spatie | Pauze / hervatten |
| B | Coachen: wissels en tactiekwijzigingen bij de volgende onderbreking |
| 1 / 4 / 8 | Wedstrijdsnelheid |
| End | Rest van de wedstrijd snel doorrekenen |
| Esc | Instellingen (wedstrijdduur, muziek- en effectvolume, taal, venstermodus, vsync, beeldsnelheid, hulp, feedback) |
| F9 | Feedbackbestand maken (zie Privacy) |
| F1 of ? | Uitleg bij het huidige scherm |
| F11 | Volledig scherm |

Alleen toetsenbord en muis. Nog geen gamepad.

### Systeemeisen

- Windows 10 of 11, 64-bit
- Videokaart met OpenGL 3.3 (Vulkan is niet nodig)
- Scherm van minstens 1024×768 (minimaal ondersteund; tekst is daar klein en het spel toont balken boven en onder). Het best op een 16:9-scherm vanaf 1280×720; 1920×1080, 2560×1440 en 4K vullen het scherm
- Circa 200 MB vrije schijfruimte; de download is ongeveer 73 MB
- Geen installatie, geen Godot, .NET of Python nodig: uitpakken en `GaffersNotebook.exe` starten

### Bekende beperkingen (graag lezen)

- **Dit is een voorlopige demo.** Werk in uitvoering; functies, menu's en balans veranderen nog.
- **De spelers-AI is nog ruw.** Spelers kunnen stilstaan, in een groep klonteren of vreemd lopen, en er is weinig onderling samenspel. Wedstrijden kunnen raar gedrag tonen.
- **De kalibratie is voorlopig.** Aantallen schoten, overtredingen, kaarten en uitslagen zijn grof afgesteld en nog niet in balans, vooral op amateurniveau. Lees uitslagen niet als voorspelling of als uitspraak over echt voetbal.
- **Namen zijn verzonnen.** Clubs, spelers en seizoenen zijn fictief; er zijn geen echte licenties, clubs of spelers.
- **Het geluid is eenvoudig.** Muziek en effecten zijn simpele, gegenereerde geluiden en geen opnames, en niet voor elke opstelling afgesteld. Gebruik de volumeschuifjes in Instellingen.
- Oude voortgang en opgeslagen bestanden kunnen na een update onbruikbaar worden.
- Niet op elke pc getest, en niet op een schone Windows-installatie zonder ontwikkeltools.
- Alleen Engels en Nederlands. Sommige technische logs zijn Engels.

### Windows-waarschuwing ("Windows heeft uw pc beveiligd")

Het spel is niet digitaal ondertekend, dus Windows SmartScreen kan een blauw venster "Windows heeft uw pc beveiligd" tonen met "Onbekende uitgever". Dat is normaal bij een kleine indiebuild. Vertrouw je de bron, klik dan op **Meer info** en daarna op **Toch uitvoeren**. Sommige antivirusprogramma's waarschuwen ook voor een onbekend programma. Je mag er ook voor kiezen niet te spelen. Het spel gebruikt geen internet en verstuurt niets.

### Privacy en feedback

- Geen account, geen telemetrie, geen netwerkverkeer. Het spel verstuurt nooit iets.
- Gaat er iets mis, druk dan op **F9** (of Esc > Hulp en feedback). Het spel maakt een zipbestand op je bureaublad met een screenshot van het spelvenster, systeemgegevens (Windows-versie, processor, geheugen, naam van de videokaart), logs en je opgeslagen voortgang. Je Windows-gebruikersnaam wordt uit paden gehaald.
- **Het bestand blijft op je eigen pc.** Jij beslist of je het doorstuurt. Kijk de inhoud eerst na en verwijder wat je niet wilt delen.
- Feedback sturen: alleen via de reacties op deze itch-pagina. Beschrijf daar het probleem; de maker vraagt de zip zo nodig apart op. Plaats feedbackzips niet openbaar: die bevatten logs, systeemgegevens en je voortgang.

### Credits en licenties

Gemaakt met de Godot Engine (MIT-licentie). Alle muziek en geluidseffecten zijn voor dit spel gemaakt en zijn voor dit spel gegenereerd; er zijn geen externe samples. Dezelfde credits staan in het Over-scherm in het hoofdmenu. Lettertypen: Barlow, Big Shoulders Display, IBM Plex Sans en Kalam (SIL Open Font License 1.1). Sommige spelgetallen zijn afgeleid van openbare datasets; de verplichte bronvermeldingen en licentieteksten zitten in de download (`LEESMIJ.txt`, `GODOT_LICENSES.txt`, `DOTNET_*.txt`, `WYSCOuT_NOTICE.txt`, `BRONNEN_NOTICE.txt`, `FONT_OFL.txt`). Het spel wordt door geen enkele databron onderschreven.

(c) 2026 TrainerToto. Alle rechten voorbehouden.

---

## Opmerkingen voor de maker (niet publiceren)

- Controleer alle bewoordingen tegen de uiteindelijke build voordat je publiceert: het aantal uitdagingen (10), de menunamen en "4 tot 5 minuten" zijn gebaseerd op de stand van 2026-10-17 (native proef 287 s zonder doorspoelen, zie `docs/hervatten.md`).
- De toets D werkt in een release-export niet (`GameSession.DebugEnabled`); daarom staat hij niet meer in de toetsentabel.
- Versielabel: `0.1.0-beta.1` (kaft en Instellingen tonen alleen dit label; About en `VERSIE.txt` tonen ook hash en bouwtijd). Screenshots: `screenshots/01..11` (zie `screenshots.md`).
- De zinnen over data zijn bewust algemeen ("afgeleid van openbare datasets"). Pas ze pas aan nadat de punten in `licenties-check.md` zijn beslist.
- Gebruik geen schermafbeeldingen met "D debug" in de voettekst: zie `screenshots.md`.
