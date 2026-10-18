# Licentiecontrole voor verspreiding via itch.io

Stand: 2026-10-17, gebaseerd op `DATA_SOURCES.md`, `LICENSES/` (README, THIRD_PARTY_NOTICES, WYSCOuT_NOTICE), `docs/bronvervanging-beta.md`, `godot/assets/fonts/OFL.txt`, `scripts/build_demo.ps1`, de broncode (alleen gelezen) en de inhoud van de nieuwste lokale zip (`dist/GaffersNotebook-357267d-dirty-20261017-173803.zip`: 214 bestanden). **Geen juridisch advies.** Ik heb de oorspronkelijke licentieteksten van StatsBomb, Wyscout/Figshare, SkillCorner en DFL niet zelf opnieuw gelezen of online gecontroleerd; ik citeer wat het project daarover heeft vastgelegd. De vaultnotitie `Licenties en Steam` (leidend volgens `DATA_SOURCES.md`) staat niet in de repo en is hier dus niet gecontroleerd.

## MAKER MOET BESLISSEN

Volgorde: zwaarste eerst. Zolang 1 tot 4 openstaan: publiceer niet als Public; Draft of Restricted voor bekende testers is een kleiner risico.

1. **Wyscout-afgeleide getallen in een publiek product: het "publicatievoorbehoud".**
   - Wat er speelt: de schotkans (`Shooting.cs`, vier coëfficiënten) en de duel-/foul-/kaartparameters (`DuelModel.cs`) zijn nu afgeleid van de publieke Wyscout-dataset (Pappalardo en Massucco, Events + Matches). Beide Figshare-records vermelden CC BY 4.0 (gecontroleerd 2026-10-17: <https://api.figshare.com/v2/articles/7770599> en <https://api.figshare.com/v2/articles/7770422>; lokale archieven met gelijke MD5). CC BY 4.0 staat bewerken, herverspreiden en commercieel gebruik toe, mits naamsvermelding, vermelding van wijzigingen, licentielink en zonder suggestie van goedkeuring. `WYSCOuT_NOTICE.txt` doet dat en zit in de zip.
   - Open: `DATA_SOURCES.md` en `LICENSES/README.md` zeggen nog steeds "product-mogelijk na afronding van het publicatievoorbehoud uit `Licenties en Steam`" en eerder "tegenstrijdige meldingen elders" over de voorwaarden van Wyscout zelf. Die voorwaarden zijn niet in de repo vastgelegd. De Figshare-licentie is door de dataset-auteurs gezet; of Wyscout (de eigenaar van de ruwe data) daarmee instemt, is niet bevestigd.
   - Keuze: (a) accepteren op basis van de CC BY 4.0-metadata en de notice, (b) de auteurs (Pappalardo) per mail laten bevestigen dat afgeleide modelparameters in een gratis spel mogen en dat bewaren, (c) tot bevestiging alleen Restricted/Draft verspreiden.
   - Bron: `DATA_SOURCES.md` regel Wyscout; `LICENSES/README.md`; `docs/bronvervanging-beta.md`.

2. **StatsBomb: zit er nog iets afgeleids in het product, en in de repo-historie?**
   - Wat er speelt: de StatsBomb Public Data User Agreement (art. 1.2.2, zoals samengevat in `DATA_SOURCES.md`) verbiedt commerciële exploitatie van de data of "any analysis derived"; art. 1.2.1 verbiedt doorgeven aan derden. Status: lokaal, niet in het product.
   - Gedaan: de oude StatsBomb-modellen staan niet meer in git (`.gitignore`) en niet in de zip (de zip bevat geen `calibration/`). De schotfit en kaartdrempels zijn vervangen door Wyscout-versies.
   - Nog open: (a) `docs/bronvervanging-beta.md` zegt dat keeperbereik, schotruis/-snelheid, `PassConfig`, `InterceptSettings`, `KickShieldMeters` e.d. "aan oude schotconversie/oude tellingen zijn getuned" en dat de gecombineerde eindvalidatie tegen Wyscout nog niet is gedaan (`docs/hervatten.md` noemt kalibratie en balans nog open). Die getallen zijn eigen uitvoeringsparameters, maar hun herkomst is deels de StatsBomb-meetlat. (b) `docs/kalibratie.md` verwijst nog naar StatsBomb. (c) Git-historie bevat de oude modellen (commits `1b8e7a2`, `f1a0e0f`, verwijderd in `04cfd61`) en de remote is `https://github.com/HuibHiba/Zeus.git`; ik kan niet zien of die repo privé is. Is hij publiek, dan staat afgeleid materiaal publiek.
   - Keuze: repo privé houden of historie opschonen vóór publicatie; beslissen of de resterende getunede parameters volstaan als "eigen parameters" of eerst tegen Wyscout moeten worden herijkt; geen StatsBomb-vermelding hoeft in het spel zolang er niets afgeleids in zit. Als je het toch gebruikt, eist StatsBomb naamsvermelding met logo.
   - Bron: `DATA_SOURCES.md` (StatsBomb-rij en "Open"), `docs/bronvervanging-beta.md` (sectie "Overige eigen parameters"), `.gitignore`.

3. **SkillCorner en DFL: bronvermelding ontbreekt in de zip.**
   - Wat er speelt: `sim/Zeus.Sim/TacticTemplates.cs` noemt in de commentaar dat de standaardposities per fase zijn afgeleid van SkillCorner (A-League) en DFL (Bundesliga) trackingdata ("indicative"). `DATA_SOURCES.md` vereist credit "SkillCorner" en de DFL/Sportec-vermelding (CC BY 4.0 volgens Scientific Data/Figshare).
   - Probleem: `LICENSES/THIRD_PARTY_NOTICES.md` heeft hiervoor alleen placeholders (`[Licentietekst en copyright invoegen.]`), `build_demo.ps1` kopieert geen SkillCorner/DFL-notice, en `LEESMIJ.txt` noemt ze niet. Het zip bevat dus nu geen vermelding, terwijl CC BY 4.0 (DFL) die vereist en MIT (SkillCorner) het bewaren van de licentietekst. Of de MIT-licentie van SkillCorner ook de data dekt, is volgens `DATA_SOURCES.md` niet vastgesteld.
   - Keuze: (a) notices toevoegen (licentietekst plus bronnaam) aan het exportscript en de LEESMIJ vóór publicatie, of (b) vaststellen dat geen afgeleide getallen in de build zitten en de commentaar aanpassen. Dit vraagt een scriptwijziging na afronding van de sim; dit document wijzigt niets daaraan.
   - Bron: `LICENSES/THIRD_PARTY_NOTICES.md`, `DATA_SOURCES.md`.
   - TODO (open, 2026-10-17): de LEESMIJ in `scripts/build_demo.ps1` noemt SkillCorner en DFL bewust nog niet. De zin "tactic templates are informed by publicly described formations; no third-party data is redistributed" is pas waar te maken als vaststaat dat `TacticTemplates.cs` geen getallen uit SkillCorner/DFL-trackingdata bevat (nu zegt de commentaar daar van wel). Beslis (a) bronvermelding + licentietekst toevoegen, of (b) de commentaar/getallen aanpassen en daarna die zin opnemen.

4. **Eigen licentie, rechthebbende en juridische context van de pagina.**
   - Er is geen `LICENSE`/EULA in de repo en geen copyrightregel in de zip. Beslis: "Alle rechten voorbehouden" (aanbevolen voor een gesloten gratis demo) en wiens naam er staat (jouw naam of een bedrijf). Vul dat in `pagina.md` in en overweeg dezelfde regel in `LEESMIJ.txt` bij een volgende build.
   - Beslis of je in de pagina "Wyscout" of "StatsBomb" noemt. Voorstel: nee, alleen de algemene zin over openbare datasets plus de notices in de download.
   - Check de naam: zoek of "Gaffer's Notebook" al als spel of merk bestaat (niet gedaan; het is een korte zoekactie op itch.io, Steam en de merkenregisters).

5. **Is de build rijp voor de term "demo"?** `docs/hervatten.md` (17 oktober) zegt: vrijgave geblokkeerd door balans (prof/prof gemiddeld 4 goals en prof/amateur 3,5 tegenover 2,2 tot 3,2), ontbrekende schone-pc- en menselijke eindtest, en het bronvoorbehoud. `docs/open-beta.md` criterium 7: pas na afhandeling van de bronvoorwaarden een distributiepagina inrichten. De pagina-tekst is daarom eerlijk "voorlopig". Beslis of je dat zo publiek wilt, of eerst alleen Restricted.

6. **AI-disclosure op itch.io.** Het project is met AI-hulp ontwikkeld (sessie- en agentnotities in de repo). itch.io heeft een veld voor het melden van generatieve-AI-gebruik; ik kan niet vaststellen wat daar voor jou van toepassing is. Beslis wat je invult (code, tekst, geen beeld/geluid van een model?) en wees consistent met de pagina.

7. **Feedbackkanaal.** Het spel verstuurt niets; spelers sturen een zip. Besluit maker: itch-reacties op de pagina (geen Discord). Een e-mailadres op een publieke pagina geeft ruis; reacties onder de pagina zijn publiek zichtbaar (feedbackzips bevatten screenshots; zeg erbij dat men ze niet publiek plaatst). Dit staat open in `docs/playtest.md` ("Kiezen hoe feedback binnenkomt").

## Per onderdeel: wat zit in de zip en mag het mee?

Inhoud van de nieuwste zip (214 bestanden): `GaffersNotebook.exe` (Godot, ingebed pck), map `data_Zeus.Godot_windows_x86_64/` (.NET-runtime en assemblies), `data/` (tactieken 6, squads 3, uitdagingen 6, competitions 2, lang 2), `VERSIE.txt`, `LEESMIJ.txt`, `GODOT_LICENSES.txt`, `DOTNET_LICENSE.txt`, `DOTNET_THIRD_PARTY_NOTICES.txt`, `WYSCOuT_NOTICE.txt`, `FONT_OFL.txt`. Geen `calibration/`, geen csv/jsonl, geen `.tmp_`, geen `*.local.json`, geen ruwe data (zelf gecontroleerd op bestandsnaam).

| Onderdeel | Licentie | Mag verspreid? | Vermelding in zip | Opmerking |
|---|---|---|---|---|
| Godot Engine 4.7.2 (mono) en exporttemplates | MIT, plus ingebouwde componenten (o.a. FreeType, ICU, etc.) | Ja | `GODOT_LICENSES.txt`, gegenereerd met `scripts/export_notices.gd` uit dezelfde binary (volledige licentietekst en copyrightregels) | Geen actie. Het script faalt als het bestand ontbreekt. |
| .NET-runtime (zelfstandig meegeleverd) | MIT, met componentenlijst | Ja | `DOTNET_LICENSE.txt`, `DOTNET_THIRD_PARTY_NOTICES.txt` van exact de ingebedde runtimeversie | Geen actie. |
| Barlow en Barlow Condensed (Barlow Project Authors) | SIL OFL 1.1 | Ja, ongewijzigd meegeleverd | `FONT_OFL.txt` met copyright en volledige licentie | OFL verbiedt alleen het los verkopen van het font en het gebruik van een gereserveerde naam bij wijziging. |
| Big Shoulders Display ExtraBold | SIL OFL 1.1 | Ja. Het bestand is een statische instantie (gewicht 800) uit de variabele font met aangepaste metadata | Idem; de wijziging staat in `FONT_OFL.txt` genoemd | OFL staat gewijzigde versies toe; in het licentiebestand staat geen gereserveerde fontnaam, dus de naamswijziging in de metadata is geen probleem. Laat deze notitie staan. |
| IBM Plex Sans Medium/SemiBold | SIL OFL 1.1, gereserveerde naam "Plex" | Ja, ongewijzigd | Idem (copyright IBM Corp.) | Ongewijzigd meeleveren is correct; wijzig het font niet zonder het te hernoemen. |
| Kalam Regular (Indian Type Foundry) | SIL OFL 1.1 | Ja, ongewijzigd | Idem (copyright 2014 Indian Type Foundry) | Geen actie. |
| Wyscout public dataset (Events, Matches; Pappalardo en Massucco) | CC BY 4.0 (Figshare-metadata, 2026-10-17) | Ja onder voorwaarden, maar zie punt 1 | `WYSCOuT_NOTICE.txt`: beide DOI's, artikel, licentielink, vermelding eigen bewerking, geen goedkeuring, geen ruwe data | Ruwe events/matches zitten niet in de zip. Afgeleide getallen zitten in gecompileerde code (`Shooting.cs`, `DuelModel.cs`). |
| StatsBomb Open Data | Public Data User Agreement | Niet voor het product | Geen | Zie punt 2. Niets afgeleids in data of modelbestanden in de zip; getunede parameters nog te beoordelen. |
| SkillCorner Open Data | MIT (data onzeker) | Ja met credit en licentietekst | Ontbreekt | Zie punt 3. |
| DFL/Sportec integrated dataset (Bassek e.a.) | CC BY 4.0 | Ja met naamsvermelding | Ontbreekt | Zie punt 3. |
| openfootball | CC0 | Ja | Niet verplicht | `competitions/profielen.json` en een schema-bestand zitten in de zip. |
| Publieke rapporten (CIES, Sportradar, e.d.) | Eigen voorwaarden; losse cijfers met bron | Losse cijfers, geen tabellen | Bron per waarde in `profielen.json` | Controleer vóór publicatie dat in dat bestand geen tabellen staan die je niet mag hergebruiken. |
| FotMob | Voorwaarden verbieden hergebruik | Nee | Geen | De zestien waarden zijn uit `profielen.json` gehaald (`DATA_SOURCES.md`). Controleer dit eenmalig in de definitieve zip. |
| Audio (fluitsignalen, juichen, klik; `godot/assets/audio/*.wav`) | Eigen werk | Ja | Geen | Zelf gegenereerd met `scripts/make_audio.py` (alleen Python-stdlib, vaste seeds). |
| Muziek (`godot/assets/audio/music_*.mp3/.ogg`): Windswept, Clear Air, Summer Day (Kevin MacLeod, incompetech.com) | CC BY 4.0 | Ja, met naamsvermelding | `MUSIC_CREDITS.txt`, About, LEESMIJ, itch-pagina | Credit-regel is het standaardformaat van incompetech; de exacte tekst per trackpagina kon niet gelezen worden (JavaScript). Nummers niet bewerkt, alleen volume per nummer. Toegevoegd 2026-10-18 (besluit maker: de nummers in `muziek-voorstel3`). |
| Muziek: Gymnopedie No. 1 (Satie, 1888), opname Robin Alciatore via Musopen | Publiek domein (opname vrijgegeven door de maker van de opname; Wikimedia Commons, categorie PD-author) | Ja | Musopen vraagt om vermelding; staat in `MUSIC_CREDITS.txt` | Bron: commons.wikimedia.org File:Erik_Satie_-_gymnopedies_-_la_1_ere._lent_et_douloureux.ogg. Licentie van de opname is door de bouwer van de bronpagina gelezen, niet juridisch getoetst. |
| Eigen app-iconen (`icon.svg`, `icon.ico`), tactieken, uitdagingen, vertalingen | Eigen werk | Ja | Copyrightregel nog in te vullen (punt 4) | Geen externe merken of beeldbronnen volgens `docs/demo.md`. Club-, speler- en tactieknamen zijn fictief (`season.provisional_help`: "All clubs are fictional"). |

## Gebruikersdata en feedback-privacy

Goed beschreven en consistent over vijf plekken: `docs/playtest.md` (sectie Privacy), `docs/demo.md` (Feedback en foutafhandeling), `LEESMIJ.txt` (EN en NL, in de zip), `docs/open-beta.md` en nu `pagina.md`.

- Het spel heeft geen telemetrie, account of netwerkverkeer; niets wordt automatisch verzonden. De F9-zip blijft lokaal (bureaublad, anders `user://feedback/`); de speler ziet het pad en krijgt "Map openen".
- Inhoud van de zip is opgesomd (screenshot van het spelvenster, `info.txt` met versie/taal/OS/CPU/RAM/renderer en videokaartnaam, `godot.log`, `gamelog.txt` van maximaal 500 regels, voortgangsbestanden, `last_match.json`, opmerking, eventueel `vorige_crash.txt`). Eigen tactieken zitten er bewust niet in. De gebruikersnaam wordt uit paden gehaald (`%USERPROFILE%`, `<user>`).
- Zwakke plekken, geen blokkade maar wel benoemen: (1) het screenshot kan alles tonen wat in het spelvenster staat (de tekst waarschuwt hiervoor); (2) de naam van de videokaart en de hardwaregegevens zijn technische gegevens; (3) als testers de zip als bijlage in publieke itch-reacties plaatsen, is dat openbaar. Zeg op de pagina dat ze het bestand niet publiek moeten plaatsen en eerst nakijken. De pagina-tekst zegt het eerste en laatste punt al deels; voeg de zin over niet publiek plaatsen toe als je reacties aanzet.
- Als je zelf feedback ontvangt en bewaart, ben je verantwoordelijk voor die gegevens (AVG: schermafbeeldingen kunnen persoonsgegevens bevatten). Het advies in `playtest.md` (lokaal bewaren, niet doorsturen, na verwerking verwijderen, testers als letters noemen) is bruikbaar.
- Niet getest door mij: de werking van de zip op een bureaublad met OneDrive en een vergrendeld log (`playtest.md` houdt dat als open punt aan).

## Minimale acties voordat je Public zet

- [ ] Punt 1 en 2 beslist en zo nodig bevestigd/opgeschoond.
- [ ] SkillCorner- en DFL-vermeldingen in de export (punt 3), of vastgesteld dat ze niet nodig zijn.
- [ ] Copyrightregel en rechthebbende ingevuld (punt 4).
- [ ] Zip gebouwd uit schone commit (`build-notes.md`) en de lijst "Per onderdeel" opnieuw gecontroleerd tegen die zip (bestandenlijst en `VERSIE.txt`).
- [ ] Testchecklist in `instellingen.md` doorlopen op een tweede pc.
