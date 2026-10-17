# itch.io: pagina aanmaken en uploaden (checklist voor de maker)

Jij maakt het itch.io-account en de pagina zelf aan; deze lijst is de volgorde. Namen van schermen en velden kunnen op itch.io iets afwijken van deze beschrijving (het dashboard verandert af en toe); de keuzes zelf blijven gelijk. Vooraf: werk eerst `licenties-check.md` af, vooral de punten onder "MAKER MOET BESLISSEN", en bouw de zip volgens `build-notes.md` vanuit een schone commit.

Vervang overal `trainertoto` door je itch-gebruikersnaam en `gaffers-notebook` door de projectslug (voorstel: `gaffers-notebook`).

## 1. Account en pagina aanmaken

1. [ ] Maak een account op <https://itch.io/register> (of log in). Kies een gebruikersnaam die je als maker wilt tonen; deze komt in de URL (`trainertoto.itch.io`).
2. [ ] Dashboard > **Create new project** (<https://itch.io/game/new>).
3. [ ] **Title**: `Gaffer's Notebook`. **Project URL**: `gaffers-notebook` (de slug is later lastig te wijzigen zonder links te breken).
4. [ ] **Short description / tagline**: zie `pagina.md`.
5. [ ] **Classification**: Games.
6. [ ] **Kind of project**: **Downloadable** (geen HTML5; het spel is een Windows-exe).
7. [ ] **Release status**: In development (of Prototype), passend bij "voorlopige demo".
8. [ ] **Pricing**: **No payments** (gratis; geen donatieknop nodig). Laat "$0 or donate" uit tenzij je dat bewust wilt.
9. [ ] **Description**: plak de Engelse en daaronder de Nederlandse tekst uit `pagina.md`. Vul de `<...>`-velden in (feedbackkanaal, copyright).
10. [ ] **Genre**: Simulation (secundair: Strategy, Sports als het veld meerdere keuzes toestaat).
11. [ ] **Tags** (maximaal 10; kies uit wat itch voorstelt): `football`, `soccer`, `sports`, `management`, `tactics`, `simulation`, `singleplayer`, `2D`, `Godot`, `Dutch` / `English` (talen staan ook onder "Languages"; zet **English** en **Dutch** daar aan). Gebruik geen merk-, club- of competitienamen als tag.
12. [ ] **Custom noun** (optioneel): laat staan op "game" of kies "demo".
13. [ ] **Metadata**: Platforms = **Windows**; Languages = English, Dutch; Inputs = Keyboard, Mouse; Accessibility: laat leeg tenzij je iets kunt onderbouwen.
14. [ ] **AI-disclosure**: besluit maker: geen AI-melding op de pagina. Het itch-formulier heeft een eigen verklaringsveld; dat vul jij zelf in volgens jouw besluit.
15. [ ] **Community**: kies **Comments** (reacties onder de pagina) als je feedback via itch wilt; kies **Disabled** als feedback alleen via F9-bestand plus je eigen kanaal loopt. Voorstel voor een eerste ronde: comments **aan**, maar de pagina nog niet publiek (zie punt 18). Zet moderatie in je gedachten: je moet reacties kunnen weghalen.
16. [ ] **Visuals**: cover (630x500), screenshots (1280x720) en eventueel banner: zie `screenshots.md`.
17. [ ] **Embed/Download-opties**: geen embed; laat "Downloads" als normale bestanden staan. Zet "Enable download page / direct download" op de standaard.
18. [ ] **Visibility & access**: begin op **Draft** (alleen jij ziet de pagina) of **Restricted** met een wachtwoord (testers krijgen link plus wachtwoord). Zet het pas op **Public** nadat de testchecklist hieronder is doorlopen en je de punten in `licenties-check.md` hebt beslist. Wachtwoord niet hergebruiken en niet in git zetten.
19. [ ] **Save** de pagina (je kunt opslaan zonder publiek te maken).

## 2. butler installeren (Windows)

butler is het officiële uploadprogramma van itch.io (uploadt alleen wat veranderd is, geeft versies en kanalen).

1. [ ] Download butler voor Windows: <https://itchio.itch.io/butler> (kies `windows-amd64`). Directe link: <https://broth.itch.zone/butler/windows-amd64/LATEST/archive/default>.
2. [ ] Pak de zip uit in een vaste map, bijvoorbeeld `C:\Tools\butler\` (daarin staat `butler.exe`).
3. [ ] Zet die map in je PATH (PowerShell, eenmalig, daarna nieuw terminalvenster openen):

   ```powershell
   [Environment]::SetEnvironmentVariable('Path', [Environment]::GetEnvironmentVariable('Path','User') + ';C:\Tools\butler', 'User')
   ```

   Zonder PATH-aanpassing kun je ook `C:\Tools\butler\butler.exe` voluit aanroepen.
4. [ ] Controleer: `butler -V` (toont de versie). Bijwerken kan later met `butler upgrade`.
5. [ ] Eenmalig inloggen (opent de browser; keur de koppeling goed):

   ```powershell
   butler login
   ```

   De sleutel wordt lokaal bewaard (`%APPDATA%\itch\butler_creds`). Deel dat bestand niet en zet het niet in git. Voor geautomatiseerd gebruik kan de omgevingsvariabele `BUTLER_API_KEY` dienen; die gebruiken we hier niet.

## 3. Zip uploaden

Alleen een zip uit `dist\` die uit een **schone** commit is gebouwd (bestandsnaam zonder `-dirty`; zie `build-notes.md`). De map `dist\` bevat nu uitsluitend `-dirty`-zips en testbestanden; upload daarvan niets.

1. [ ] Kies de zip en een versielabel. Het label is een leesbare versie die jij kiest; zet de korte commit-hash erin zodat feedback te herleiden is (de hash staat ook in `VERSIE.txt` in de zip). Voorbeeld: `0.1.0-demo.1+<hash>`.
2. [ ] Controleer eerst of butler de pagina kent (de pagina moet bestaan; Draft is genoeg):

   ```powershell
   butler status trainertoto/gaffers-notebook
   ```

3. [ ] Upload:

   ```powershell
   butler push dist\GaffersNotebook-<hash>-<yyyymmdd-hhmmss>.zip trainertoto/gaffers-notebook:windows --userversion 0.1.0-demo.1
   ```

   Het kanaal `windows` zorgt dat itch het bestand automatisch als Windows-download markeert. Een volgende versie pushen is hetzelfde commando met een nieuwe zip en een nieuw `--userversion`; butler stuurt alleen verschillen. Een zip wordt door butler als archief behandeld: controleer na de upload op de pagina wat spelers precies krijgen (zie het testlijstje) en of de bestandsnaam en grootte kloppen (ongeveer 70 MB).
4. [ ] Wacht tot de verwerking klaar is: `butler status trainertoto/gaffers-notebook` toont het kanaal `windows` met de build en de versie. Controleer op de pagina (Edit game > Uploads) dat de upload het label **Windows** heeft.
5. [ ] Maak nooit een push vanuit een map waar je niet zeker van weet wat erin zit; push uitsluitend de zip die je zelf hebt gecontroleerd (zie hieronder).

## 4. Testen na upload

Test eerst in de Draft/Restricted-stand.

**Zip controleren (op de bouwmachine, voor het uploaden)**
- [ ] Naam bevat geen `-dirty`; `VERSIE.txt` in de zip bevat dezelfde hash als `git rev-parse --short HEAD`.
- [ ] Aanwezig in de zip: `GaffersNotebook.exe`, `VERSIE.txt`, `LEESMIJ.txt`, `GODOT_LICENSES.txt`, `DOTNET_LICENSE.txt`, `DOTNET_THIRD_PARTY_NOTICES.txt`, `WYSCOuT_NOTICE.txt`, `FONT_OFL.txt`, map `data/` met `tactics`, `squads`, `challenges`, `competitions`, `lang`.
- [ ] Geen `.csv`, `.jsonl`, `.local.json`, `calibration/` of `.tmp_`-bestanden erin (het script weigert dat al, maar kijk zelf even).

**Downloaden en starten op een andere pc (bij voorkeur zonder ontwikkeltools of Godot/.NET, schone Windows 10 of 11)**
- [ ] Open de pagina in een browser met het wachtwoord/de link zoals een speler dat zou doen. Download het bestand via de knop op de pagina (niet via butler).
- [ ] Download klopt in grootte (ongeveer 70 MB) en uitpakken lukt in een gewone map (bijvoorbeeld `Downloads\GaffersNotebook`). Niet starten vanuit de zip.
- [ ] Start `GaffersNotebook.exe`. Noteer of SmartScreen verschijnt, welk antivirus actief is en wat het doet.
- [ ] Menu verschijnt, versie rechtsonder komt overeen met `VERSIE.txt`, taal wisselen werkt (English / Nederlands).
- [ ] Een volledige wedstrijd zonder doorspoelen: duurt ongeveer 4 tot 5 minuten, eindkaart verschijnt, terug naar menu.
- [ ] Een eigen tactiek opslaan, het spel sluiten, opnieuw starten: tactiek staat er nog (`%APPDATA%\GaffersNotebook\`).
- [ ] Esc opent instellingen; F9 maakt een feedbackzip op het bureaublad; open de zip en controleer dat er geen gebruikersnaam of andere persoonlijke gegevens in staan.
- [ ] Er verschijnt geen netwerkverkeer / firewallvraag (het spel gebruikt geen internet).
- [ ] Een tweede pc met een oudere of simpele videokaart (OpenGL 3.3, zonder Vulkan) start ook.
- [ ] De pagina zelf: tekst, cover, screenshots, tags, taalvelden en de eerlijke beperkingen zijn zichtbaar; links werken; mobiel/telefoonweergave leest redelijk.
- [ ] Alleen na een geslaagde test en ingevulde licentiepunten: visibility op **Public**.

## 5. SmartScreen en "onbekende uitgever": tekst voor spelers

De exe is niet code-signed (certificaat kost geld en een nieuwe uitgever bouwt eerst reputatie op). Windows kan daarom bij het eerste starten "Windows protected your PC / Windows heeft uw pc beveiligd" tonen, met "Unknown publisher / Onbekende uitgever". Dit zegt dat het programma niet bekend of niet ondertekend is, niet dat het schadelijk is. Het is ook een mogelijke bijwerking van het feit dat het bestand uit het internet komt (Mark of the Web).

Tekst om op de pagina of in een bericht te zetten (de pagina bevat dit al in `pagina.md`):

**EN:** The game is not code-signed, so Windows may show "Windows protected your PC" with "Unknown publisher". If you trust this page, click **More info**, then **Run anyway**. You can also decline to run it. Some antivirus programs may warn about unknown programs as well. The game does not connect to the internet.

**NL:** Het spel is niet digitaal ondertekend, dus Windows kan "Windows heeft uw pc beveiligd" tonen met "Onbekende uitgever". Vertrouw je deze pagina, klik dan op **Meer info** en daarna op **Toch uitvoeren**. Je mag ook besluiten het niet te starten. Sommige antivirusprogramma's waarschuwen ook voor onbekende programma's. Het spel maakt geen verbinding met internet.

Extra tips voor wie liever zeker weet:
- Pak het bestand uit in een eigen map; rechtermuisknop op de zip > Eigenschappen > "Blokkering opheffen" (Unblock) vóór het uitpakken voorkomt de melding soms.
- Controleer desgewenst het bestand met een virusscanner naar keuze.
- Geef de maker liever geen persoonlijke gegevens in feedback; het F9-bestand blijft lokaal tot je het zelf verstuurt.

Overweeg later (kost geld, niet nodig voor deze demo): code-signing via Azure Trusted Signing of een gewoon code-signingcertificaat, of publicatie via de itch-app, waar spelers de installatie via de app doen.

## Paginastijl (Edit game > Theme)
Kleuren uit het spel: achtergrond `#2f4a3e` (donkergroen), tekstvlak `#ebdfc8` (crème), tekst `#231f1c`, accent/knoppen `#f2c744` (geel). Lettertype Kalam of Big Shoulders Display voor de titel als itch het aanbiedt. Cover: `screenshots/cover-630x500.png`; screenshots: `screenshots/01..06`. Bekijk `docs/itch/preview/index.html` in een browser voor het beoogde uiterlijk (lokale voorvertoning, geen itch-pagina).
