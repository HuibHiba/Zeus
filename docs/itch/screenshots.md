# Screenshots en cover voor de itch.io-pagina

Formaten: **cover 630x500** (itch toont dit op kaartjes; minimum 315x250, houd de verhouding 315:250) en **screenshots 1280x720** (16:9; itch accepteert PNG/JPG/GIF, zorg dat de belangrijkste inhoud niet bij de randen staat). Optioneel een banner (pagina-achtergrond) is niet nodig.

Er is niets gekopieerd naar git. Hieronder staat waar bestaand materiaal staat (alle paden relatief aan `C:\dev\zeus-codex-integratie\`, alle ongevolgde `.tmp_*`-mappen).

## Eerst: het formaat past niet

Alle bestaande opnamen zijn **1520x780** (venstergrootte van het spel, verhouding 1,95:1), dus niet 16:9. Voor de publicatie:

- Beste route: maak de definitieve opnamen opnieuw met de **echte release-export** (de nieuwe schone zip uitgepakt), venster op 1280x720 (of 1920x1080 en daarna verkleind naar 1280x720). Het venster is verstelbaar (`window/size/resizable=true`) en het spel schaalt mee. Controleer of niets wordt afgekapt.
- Noodroute: de 1520x780-opname bijsnijden naar 1387x780 (16:9) en verkleinen naar 1280x720. Het zijpaneel rechts valt dan deels af; kijk goed wat er verloren gaat.
- Voor de cover 630x500 (bijna vierkant) is altijd een uitsnede of een aparte compositie nodig. Aanbevolen: de titelkaart "GAFFER'S NOTEBOOK" uit het menu (donkergroen paneel met crème titelblok) als basis, eventueel met een stukje veld ernaast. Gebruik alleen eigen beeld en de meegeleverde lettertypen.
- **Niet gebruiken**: opnamen uit debugbuilds. Die tonen "D debug" in de voettekst en "Version dev" rechtsonder (zichtbaar in `.tmp_demo_check/notebook-visual/Menu` en `Main`). Een release-export toont geen D-tekst en de echte versie. Maak daarom voor de definitieve set nieuwe opnamen met de release-exe, zonder debugomgevingsvariabelen.
- Kies Engels als taal (publiek) en zet de taal van een tweede set opnamen op Nederlands als je die ook wilt tonen (`.tmp_demo_check/notebook-visual-nl/` heeft hiervoor voorbeelden).
- Geen persoonsgegevens in beeld: geen bureaublad, geen eigen gebruikersnaam, geen feedbackzip met pad.

## De vijf screenshots en de cover

| # | Bestandsnaam voorstel | Scherm | Wat zichtbaar moet zijn | Waarom |
|---|---|---|---|---|
| Cover | `cover-630x500.png` | Menu (titelblok) | "GAFFER'S NOTEBOOK" in Big Shoulders, notitieboekpapier, gele accentlijn; eventueel veldlijnen | Herkenbare stijl op het kaartje |
| 1 | `01-match-live-1280x720.png` | Live wedstrijd (`Main`) | Veld met spelers, scorebord met minuut ver in de wedstrijd (niet 00:05), statistieken rechts, voettekst met toetsen | Toont de kern: de wedstrijd |
| 2 | `02-tactic-maker-1280x720.png` | Tactiekmaker (`Editor`) | Spelers op het veld, fase-knoppen (Opbouw, Aanval, ...), zijpaneel met rol en instructies, SAVE | Toont het notitieboek-idee |
| 3 | `03-menu-first-half-hour-1280x720.png` | Hoofdmenu | Menu, wedstrijdopzet (tactiek, niveau, tempo/druk-sliders), checklist "Your first half hour" | Toont wat een nieuwe speler kan doen |
| 4 | `04-full-time-1280x720.png` | Eindkaart (`End`) | Eindstand, statistieken, twee heatmaps ("Where they played"), knoppen play again/menu | Toont de analyse na afloop |
| 5 | `05-challenges-or-meter-1280x720.png` | Uitdagingen of Tactiekmeter | Lijst met uitdagingen en hun doelen, of de meter die twee tactieken vergelijkt | Toont de extra spelmodi |
| (optioneel 6) | `06-season-1280x720.png` | Competitie (`Season`) | Tabel en speelronde van de fictieve competitie | Alleen als de tabel er goed uitziet |

Bijschriften (itch toont ze niet; gebruik ze in de beschrijving of de bestandsnaam): hou ze feitelijk ("The tactic maker", "The end-of-match card with heatmaps"). Geen claim over realisme.

## Bestaande kandidaten (beste eerst)

Alle: PNG, 1520x780, visueel bekeken tenzij anders vermeld.

**Menu (screenshot 3, cover-basis)**
- `.tmp_demo_check/notebook-visual/Menu/Menu_menu.png` (EN, notebookstijl, gecontroleerd; toont wel "D debug" en "Version dev": alleen als referentie, opnieuw opnemen)
- `.tmp_demo_check/notebook-visual-nl/Menu/Menu_menu.png` (NL, niet bekeken)
- `.tmp_takeover/automation/run-20261017-123212/beta-menu-reviewed.png` (release-export, 17 oktober; niet bekeken; waarschijnlijk geschikter omdat het een echte export betreft, controleer de voettekst)

**Live wedstrijd (screenshot 1)**
- `.tmp_demo_check/notebook-visual/Main/Main_match.png` (EN, notebookstijl, gecontroleerd, maar alleen minuut 0:05 en 0-0; opnieuw opnemen halverwege een wedstrijd)
- `.tmp_takeover/automation/run-20261017-123212/beta-standard-speed.png` (release-export; niet bekeken; waarschijnlijk een wedstrijd onderweg)
- Niet gebruiken: `.tmp_takeover/automation/run-20261017-112221/hud-live-current.png` (oudere, blauwe stijl, vóór het notitieboek-thema).
- Zoek anders in `.tmp_takeover/automation/run-20261017-111450/` (`recovery-live-*.png`) en `run-20261017-005500/` (`fixed-live-*.png`, `fixed-competition-live.png`); dit zijn runs vóór de definitieve vormgeving, controleer de stijl per bestand.

**Tactiekmaker (screenshot 2)**
- `.tmp_demo_check/notebook-visual/Editor/Editor_editor.png` (EN, notebookstijl, gecontroleerd: veld met rode spelers, fasebalk, rolpaneel)
- `.tmp_takeover/automation/run-20261017-115823/editor-review.png` en `editor-team.png`, `editor-phase.png`, `editor-rules.png` (verschillende tabbladen; niet bekeken)
- `.tmp_demo_check/notebook-visual-nl/Editor/Editor_editor.png` (NL)

**Eindkaart (screenshot 4)**
- `.tmp_takeover/automation/run-20261017-115823/full-time-review.png` (EN, gecontroleerd: stand 1-4, statistieken, twee heatmaps; goed leesbaar. Let op: de score is zeer scheef (1-4, 22 tegen 28 schoten); neem liever een opname van een gewone wedstrijduitslag nadat de balans is verbeterd)
- `.tmp_demo_check/notebook-visual/End/End_match.png` (standaardopname; niet bekeken)

**Uitdagingen, meter, competitie (screenshot 5 en 6)**
- `.tmp_demo_check/notebook-visual/Challenges/Challenges_challenges.png`, `.../Meter/Meter_meter.png`, `.../Season/Season_season.png`, `.../Squad/Squad_squad.png`, `.../Pause/Pause_match.png` (alle EN, notebookstijl; niet bekeken)
- Meter in actie: `.tmp_takeover/automation/run-20261017-121718/ecc-meter-start.png` (niet bekeken)
- Competitie: `.tmp_takeover/automation/run-20261017-005500/fixed-season-current.png` (vóór de laatste vormgeving; niet bekeken)

**Instellingen (optioneel)**
- `.tmp_demo_check/settings-visual/settings-game-en.png`, `settings-display-en.png`, `settings-help-en.png` (EN; NL-varianten staan ernaast)

Meer dan 290 PNG's staan in `.tmp_takeover/automation/run-*/` (testruns met UI-automatisering, veel tussenstappen). Kijk bij de keuze altijd naar het bestand zelf: de stijl is tijdens het project veranderd (donkerblauw HUD eerst, daarna het notitieboek-thema met crème zijpaneel).

## Hoe maak je de definitieve opnamen (zonder debugschakelaars)

1. Bouw eerst de schone zip (`build-notes.md`) en pak hem uit in een lege map; start `GaffersNotebook.exe` met een eigen profiel (bijv. een schone `%APPDATA%\GaffersNotebook`, hernoem je eigen map tijdelijk; zet hem daarna terug).
2. Zet het venster op 1280x720 (Esc > Instellingen > venstermodus, of sleep het venster) en neem op met Windows Win+Shift+S of de Xbox Game Bar (Win+Alt+PrtScn).
3. Wedstrijd: speel op snelheid 1 of 4 tot minuut 30 tot 60 en pauzeer met Spatie op een mooi moment (niet bij de aftrap).
4. Controleer elke opname: geen "D debug", geen "Version dev", taal Engels, geen persoonlijke gegevens.
5. Sla op als PNG; upload de screenshots op de pagina (Edit game > Screenshots) en de cover bij "Cover image".
