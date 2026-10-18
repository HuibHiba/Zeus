# Meerjarenplan Gaffer's Notebook (2026-2028)

> **Planning vervangen door [roadmap-naar-1.0.md](roadmap-naar-1.0.md)** (later op 2026-10-18, na de vragenronde met de maker). De uitgangspunten (§0), meetcriteria (§4), licentieregels (§5) en de release-checklist (§6.3) hieronder blijven gelden en worden daar aangehaald.

**Intern werkdocument, geen belofte aan spelers.** Opgesteld 2026-10-18 vanaf commit `8e3585c` (branch `demo/ui-polish`). Niets hieruit hoort op een itch- of Steampagina, in een devlog of in een bericht aan testers.

Bronnen: vault `04 Planning/Roadmap.md`, `01 Ontwerp/PRD.md`, `01 Ontwerp/Besluiten.md`, `02 Techniek/Licenties en Steam.md`, `02 Techniek/Namen en logo's (juridisch).md`, `04 Planning/Meetplan.md`, `05 Sessielogs/2026-10-17.md`; repo `docs/hervatten.md`, `docs/nachtplan.md`, `docs/roadmap-open.md`, `docs/kalibratie.md`, `docs/competitie.md`, `docs/open-beta.md`, `docs/itch/licenties-check.md` en `docs/ai-gap-analyse.md` (branch `demo/ai-gap`).

Rangorde bij tegenspraak: `docs/hervatten.md` voor de actuele bouwstand en testaantallen, `Licenties en Steam` voor licenties, `Besluiten` voor besluiten. Dit plan neemt geen testaantallen over die kunnen verouderen, behalve als momentopname met datum.

---

## 0. Uitgangspunten van dit plan

| Punt | Aanname |
|---|---|
| Tijd van de maker | 8-10 uur per week, ongeveer 110-130 uur per kwartaal. Rekening houden met vakanties, schoolpieken (toetsweken, rapportvergaderingen) en weken zonder tijd: reken op **90 uur per kwartaal**. |
| Bouwen | Claude (en eerder Codex) schrijft vrijwel alle code, steeds vaker met parallelle agents in eigen worktrees. De maker stuurt, beslist, speelt en test. |
| Snelheid code | Oktober 2026 liet zien dat een demo met fase 1-3 plus eerste versies van 5, 6, 7 en 10 in ongeveer een week kon. De vaultraming ("5-8 jaar voor alles", "fase 1-3 na 12-20 maanden") is voor **code** dus te pessimistisch: reken met een factor 4-8 sneller. |
| Snelheid van de rest | Speltests, oogtests, het oordeel van de maker, juridische vragen, antwoorden van data-auteurs en de wachttijden van Steam gaan **niet** sneller met agents. Die bepalen de echte tijdlijn. |
| Knelpunt | Niet hoeveel code er komt maar hoeveel de maker kan beoordelen: elk agentresultaat vraagt speel- of leestijd. Plan daarom maximaal 2-3 grote sporen tegelijk. |
| Wat "klaar" betekent | Gemeten tegen een vooraf vastgelegd criterium (zie §4), met bewaarde uitslagen. Grenzen nooit verruimen om groen te krijgen (bestaande regel). |
| Data en namen | Fictief als standaard. Geen StatsBomb-afgeleide getallen in een openbare of betaalde versie. Zie §5. |

---

## 1. Waar staan we (stand 2026-10-18)

### 1.1 De demo

**Gaffer's Notebook 0.1.0-beta.1**, Windows-zip, op itch.io, **Public sinds 2026-10-18** (de maker koos zelf voor publiceren en deelt de link; reacties via Comments, geen donaties, tips later). Wijzigingen aan de pagina alleen met de maker (besluit nachtplan D1 geldt nog voor alles behalve deze ene publicatie).

Inhoud van de demo:
- 2D-wedstrijd 11 tegen 11 in notitieboekstijl, ongeveer 4-5 minuten per wedstrijd (20x; 12x en 4x te kiezen), pauzeren, wisselen en bijsturen (B), eindkaart met statistieken, heatmaps en een samenvatting met doelpunten en grote kansen.
- Tactiekmaker met sjablonen, vrije plaatsing, instructies per speler en fase, looppaden, een tactiekbibliotheek (favorieten, sorteren, import/export) en tegenstanderscouting in de TEST-tab.
- Meter: batches met versnelde simulatie en het verschil tussen tactieken.
- 10 uitdagingen met sterren en tactieken die je kunt vrijspelen.
- Competitie met drie divisies (amateur, halfprof, prof) en fictieve clubs, promotie en degradatie, een eigen club (naam, tenue, embleem) en een seizoensdoel.
- Selectiescherm, vertrouwdheid en training (voorlopig), een tegenstander die zelf wisselt (standaard uit).
- Engels en Nederlands, rondleiding bij de eerste start, F9-feedbackpakket (lokaal, de speler deelt het zelf), muziek en geluidseffecten die voor het spel zijn gemaakt.

Bekende zwakke plekken: positiespel (zie §1.3), de balans tussen niveaus op grote reeksen (profwinst tegen amateur ongeveer 55-57%), aanvallers die de bal uit het oog verliezen, nog **geen menselijke speltest** en geen test op een schone pc.

### 1.2 Stand per roadmapfase

| Fase | Onderwerp | Stand | Inschatting |
|---|---|---|---|
| 0 | Fundament, ontwerp, engine | Af. C#/.NET 8 + Godot 4.7, Python voor data. De v0-ontwerpen zijn nog niet door de maker beoordeeld. | 100% (beoordeling open) |
| 1a-c | Kijkbare wedstrijd, regels, spelfases | Gebouwd: beweging, passen, duels, overtredingen en kaarten, buitenspel, spelhervattingen, spelfases, minimale vermoeidheid, determinisme en replay. | 85%: werkt, nog niet echt realistisch |
| 2 | Tactiekeditor v1 | Gebouwd en uitgebreid tot een volledige tactiekmaker. De invoertijd van de Feyenoordtactiek (≤30-45 min) is nog niet gemeten. | 85% |
| 3 | Inzicht en snelheid | Meter, batches, annuleren en een headless runner (3-4 s per wedstrijd). De klikbare "waarom"-lijst en het rapport over verdiende winst zijn nog dun. | 70% |
| 3b | Asynchrone vriendenmodus | Tactiek als bestand of via het klembord, plus de bibliotheek. Nog geen "speel tegen de tactiek van je vriend" met uitslag terug. | 50% |
| 4 | Voetbal-AI op echte data | Deels gekalibreerd (Wyscout-schot- en duelmodellen, SkillCorner/DFL-beweging). De kloofanalyse toont 5 grote verschillen in positiespel. | 35% |
| 5 | Niveaus en conditie | Drie niveaus met een niveaucontrole in de tests. Vermoeidheid v0, niet empirisch gekalibreerd. | 40% |
| 6 | Vertrouwdheid en training | Voorlopige v0, standaard uit, gekoppeld aan de competitie. | 30% |
| 7 | Live wedstrijd en slimme tegenstander | Wissels en bijsturen af. De tegenstander wisselt zelf (uit). Voorbereiding op jouw stijl alleen in uitdaging 6. Scouting met onzekerheid ontbreekt. | 40% |
| 8 | Tekenen en triggers | Looppaden per fase af. Triggers zitten in het datamodel, niet in de bediening. | 25% |
| 9 | Geleerde AI | Niet gestart. | 0% |
| 10 | Mini-manager en competitie | Dunne competitie en carrière v0, 10 uitdagingen. Geen selectiebeheer, blessures, schorsingen of bekers. | 30% |
| 11 | 3D-weergave | Niet gestart (een lege verkenningsbranch `demo/proto-3d` bestaat). | 0% |
| 12 | Live samen spelen | Niet gestart, optioneel. | 0% |
| 13 | Steam-release | Niet gestart. Spelnaam voorlopig, merkcontrole open, data-vrijgave open. | 5% |

### 1.3 Kloofanalyse positiespel (samenvatting van `docs/ai-gap-analyse.md`)

Sim (60 wedstrijden, profniveau) tegenover DFL-tracking (7 Bundesligawedstrijden):

| # | Kloof | Sim | Echt |
|---|---|---|---|
| 1 | Het blok schuift zijwaarts mee met de bal (factor) | 0,11-0,17 | 0,37-0,41 |
| 2 | Stilstand van veldspelers zonder bal | 42% | 3% |
| 3 | Passes die vooruit gaan / gemiddelde passlengte | 51% / 15,0 m | 60% / 19,5 m |
| 4 | Afstand verdediging-middenveld in bezit | 11,3 m | 16,5 m |
| 5 | Vrije passopties rond de baldrager | 3,15 | 4,20 |

Dit is de belangrijkste inhoudelijke werklijst voor spoor B in 2026-Q4 en 2027-Q1.

---

## 2. Strategie: drie sporen

Elk kwartaal krijgt één **hoofdspoor** (ongeveer 60% van de beoordelingstijd van de maker) en hooguit één **bijspoor**. Agents mogen meer sporen tegelijk bouwen, maar de maker beoordeelt er niet meer dan twee.

### Spoor A: speelbaarheid en speelgevoel ("feel")
Doel: iemand zonder voorkennis begrijpt binnen 10 minuten wat hij doet, en wil nog een wedstrijd spelen.
- Eerste sessie en rondleiding, leesbaarheid op 20x, een hoogtepuntmodus als continu versnellen niet werkt (zie `open-beta.md`).
- Coachen tijdens de wedstrijd: snelle bijsturing, duidelijke feedback over wat een wijziging deed.
- Eindkaart en meter die het **waarom** tonen (de 3 grootste oorzaken; meetplan §7).
- Afwerking: animaties, geluid, overgangen, prestaties op een zwakke laptop.
- **Klaar als:** 4 van de 5 testers zonder voorkennis spelen de eerste wedstrijd zonder hulp uit, en 3 van de 5 spelen uit zichzelf een tweede sessie (§4).

### Spoor B: simulatie-realisme
Doel: zelfbedachte tactieken spelen herkenbaar anders, en het lijkt op voetbal (de PRD-hypothese).

| Onderdeel | Inhoud | Afhankelijk van |
|---|---|---|
| B1 Positiespel | De 5 kloven uit §1.3: meeschuiven van het blok, joggen in plaats van aan/uit, passkeuze vooruit, afstand tussen linies, aanspeelbaarheid | Kloofmeting (bestaat), DFL/SkillCorner-referentie |
| B2 AI-training op data | Parameters fitten op eigen aggregaten uit CC BY/MIT-data (passkeuze, looplijnen, drukzetten), steeds met een vaste toetsset die nooit voor het fitten wordt gebruikt | B1, bronvrijgave (§5) |
| B3 Geleerde AI (fase 9) | Eerst één klein, uitlegbaar onderdeel (bijvoorbeeld de keuze van de passontvanger, of de positie zonder bal) als geleerd model naast de regels, met een harde terugval op de regels | B2, go/no-go G4 |
| B4 Slimme tegenstander (fase 7) | Voorbereiding op jouw vaak gebruikte stijl, met vertraging en een zichtbare reden; scouting met onzekerheid | Competitie, carrière v1 |
| B5 Vermoeidheid en conditie | Kalibratie op afstanden en sprints per 90 minuten (SkillCorner/DFL), herstel tussen wedstrijden, rotatie in de carrière | B1 (realistische loopsnelheden) |
| B6 Spelhervattingen | Corners, vrije trappen, inworpen: eigen routines in de tactiekmaker, verdedigen zonal of man-om-man, doelen voor doelpunten uit standaardsituaties | Tactiekmaker, fase 8-triggers |
| B7 Niveaus | Systematische fouten per niveau (late reactie, verkeerde afstand, minder overzicht) in plaats van alleen lagere getallen; kalibratiedoelen per niveau (§4.4) | B1 |

### Spoor C: inhoud en meta
Doel: een reden om terug te komen, zonder een volledige managementsimulatie te worden (PRD: "niet voor" transfers, financiën en media).
- **Carrière v1:** meerdere seizoenen, een selectie met ontwikkeling en veroudering, blessures en schorsingen, rotatie, een beker, het niveau van de club stijgt en daalt. Geen transfermarkt met geld; hooguit een eenvoudige "nieuwe speler kiezen" aan het eind van een seizoen.
- **Uitdagingen en scenario's:** van 10 naar ongeveer 30, in thema's (tegen tien man, een voorsprong verdedigen, met amateurs pressen, beroemde tactische vraagstukken met fictieve teams).
- **Tactieken delen en vriendenmodus (3b):** een korte deelcode of bestand, "speel tegen de tactiek van je vriend", een ladder tussen vrienden **zonder server** (uitslagen deterministisch na te rekenen op seed en tactiek).
- **Editor: tekenen en triggers (fase 8):** regels als "als de bal bij hun linksback is, zet de rechtsbuiten druk" in de bediening, plus het tekenen van loopacties binnen een fase.

### Volgorde tussen de sporen
1. Eerst **A + B1** (Q4 2026 - Q1 2027): een demo die vrienden begrijpen en die op voetbal lijkt. Zonder dat zeggen alle andere functies niets.
2. Dan **B2/B4/B6 + C** (2027): diepte en een reden om terug te komen.
3. Pas daarna **3D, B3 en Steam** (eind 2027 - 2028), elk achter een go/no-go.

---

## 3. Fasering per kwartaal (2026-Q4 tot 2028-Q4)

Legenda: **M** = mijlpaal, **G** = go/no-go-moment, **R** = interne release. Versienummers zijn intern.

### Overzicht

| Kwartaal | Hoofdspoor | Bijspoor | Mijlpaal / release | Go/no-go |
|---|---|---|---|---|
| 2026-Q4 | A: eerste speltest (demo is Public, feedback via comments en DM) | B1 positiespel, herkalibratie met buitenspelfix | R 0.1.x en 0.2 | **G1** eind december |
| 2027-Q1 | B1/B7 realisme en niveaus | C: triggers v1 | R 0.3 | - |
| 2027-Q2 | B4/B6 tegenstander en spelhervattingen | A: hoogtepunten en coachen | R 0.4, **M2 open bèta-kandidaat** | **G2** open bèta |
| 2027-Q3 | C: carrière v1, uitdagingen | B2 training op data, B3-verkenning | R 0.5 (eventueel Public op itch) | **G3** 3D-haalbaarheid |
| 2027-Q4 | B3 of 3D (volgens G3/G4) | Steam-voorbereiding juridisch | R 0.6 | **G4** geleerde AI, **G5** Steam ja/nee |
| 2028-Q1 | Steam: pagina, inhoud compleet | A: afwerking | **M4 Coming Soon** | - |
| 2028-Q2 | Steam Early Access | Spelhervattingen en carrière bijwerken | **M5 EA-lancering** (vroegst) | **G6** lanceren of uitstellen |
| 2028-Q3 | Onderhoud en EA-updates | Live samen spelen: verkenning | R EA-updates | **G7** live samen spelen |
| 2028-Q4 | Richting 1.0 of verlengde EA | - | evaluatie | **G8** 1.0 of doorgaan |

### 2026-Q4 (rest van oktober - december): eerste mensen, eerste realisme

| Werk | Spoor | Klaar als |
|---|---|---|
| Speltest ronde 1: eerst 1 proeftester, daarna 4-5 vrienden (`docs/playtest.md`) | A | Formulieren verwerkt in `docs/playtest-v1.md`; top-10 knelpunten op volgorde |
| Schone-pc-test (andere Windows-laptop, ook een zwakke) | A | Start, eerste wedstrijd en opslaan werken; minimale systeemeisen genoteerd |
| Knelpunten uit ronde 1 oplossen (alleen blokkerende fouten en verwarring) | A | Ronde-1-knelpunten met prioriteit 1 dicht |
| B1: kloof 1-2 (meeschuiven en joggen), daarna 3 (passkeuze) | B | Kloof 1 en 2 binnen 1 IQR van echt; kloof 3 gehalveerd; niveaucontrole en goldens groen zonder verruimde grenzen |
| Regressiesuite op orde: sim-tests, UI-autotests (`run_autotest.py`), smoke van alle scènes, test van de exe (`playtest_exe.py`) in één script | Tools | Eén opdracht geeft groen of rood voor een release-kandidaat |
| Licenties: conceptmail aan de Wyscout- en DFL/SkillCorner-auteurs afmaken; de maker beslist over versturen | Data | Verstuurd of bewust uitgesteld, met reden in Besluiten |
| Merkcontrole "Gaffer's Notebook" (EUIPO, BBIE, USPTO), zelf of met een jurist | Juridisch | Uitkomst in Besluiten; bij een botsing start de zoektocht naar een nieuwe naam |

**R 0.2 (Restricted):** de ronde-1-fixes plus B1 deel 1.
**G1 (eind december 2026): doorgaan, bijsturen of pauzeren.**
- Doorgaan als: de oogtest (3 kenners, 5 replays) geeft ≥3,0 op 5, **en** ten minste 3 van de 5 testers willen een volgende versie spelen.
- Bijsturen als: de oogtest is <3,0 → Q1 2027 volledig spoor B1, geen nieuwe functies.
- Pauzeren of het concept heroverwegen als: testers begrijpen de kern (tactiek maken, verschil zien) niet, zelfs na de fixes.

### 2027-Q1: lijkt op voetbal, ook per niveau

| Werk | Spoor | Klaar als |
|---|---|---|
| B1 afronden: kloven 3-5 | B | Alle 5 kloven binnen 1 IQR, of gedocumenteerd waarom niet |
| B7: niveaus via systematische fouten | B | Kalibratiedoelen per niveau 2027-Q1 gehaald (§4.4) |
| B5: vermoeidheid kalibreren op afstand en sprints per 90 min | B | Afstand per veldspeler en aandeel sprints binnen de band van de referentie (§4.4); "90 minuten druk" kost zichtbaar |
| Meetplan §1: realisme 8 van 10 maatstaven op profniveau | B | 8/10 binnen de marge van het referentieprofiel (gekozen door de maker, zie §7) |
| C: triggers v1 in de tactiekmaker (3-5 triggertypes) | C | Een trigger is in ≤2 minuten te maken en het effect is te zien in de meter |
| Speltest ronde 2 (dezelfde testers + 2 nieuwe) | A | Verwerkt; vergelijking met ronde 1 |

**R 0.3 (Restricted).** Risico: een kloof sluiten maakt een andere groter (bijvoorbeeld meer schoten). Maatregel: elke wijziging op dezelfde vaste invoer meten, alle uitslagen bewaren (bestaande werkwijze).

### 2027-Q2: tegenstander en spelhervattingen; open bèta-kandidaat

| Werk | Spoor | Klaar als |
|---|---|---|
| B4: slimme tegenstander v1 in de competitie (standaard aan, uit te zetten) | B | Speel je 3 keer dezelfde stijl, dan past een tegenstander zich zichtbaar aan, met een reden in de scouting; winst daalt meetbaar maar niet naar nul |
| B6: spelhervattingen v1 (corner- en vrije-trapsroutines, zonal/man) | B | Aandeel doelpunten uit standaardsituaties binnen de referentieband; 2+ routines zijn in de editor te kiezen |
| A: hoogtepuntmodus (als speltest 2 zegt dat 20x onleesbaar is) en het waarom-rapport | A | Meetplan §7: ≥8 van 10 oorzaken kloppen volgens de maker |
| Bronvrijgave afronden (§5): StatsBomb-getunede parameters hergekalibreerd op Wyscout/DFL/SkillCorner | Data | Geen parameter meer met een StatsBomb-herkomst in `sim/`; `docs/itch/statsbomb-herkomst.md` bijgewerkt |
| Repo-historie: beslissen of opschonen nodig is (alleen bij delen of openbare broncode) | Data | Besluit vastgelegd |

**M2 open bèta-kandidaat (R 0.4).**
**G2 (eind juni 2027): open bèta op itch (Public, gratis)?** Voorwaarden, allemaal nodig:
1. De vrijgavecriteria 1-7 uit `docs/open-beta.md` zijn gehaald.
2. Wyscout en DFL zijn bevestigd **of** de maker accepteert schriftelijk (in Besluiten) het risico op basis van CC BY 4.0 en de notices.
3. De merkcontrole is zonder botsing, of er is een nieuwe naam.
4. Er staat geen StatsBomb-afgeleide parameter in het pakket.
5. Er is een feedbackbestemming (itch-reacties en/of e-mail), zonder telemetrie.
Haalt het dit niet: Restricted blijven, de open bèta schuift een kwartaal op.

### 2027-Q3: reden om terug te komen

| Werk | Spoor | Klaar als |
|---|---|---|
| C: carrière v1 (meerdere seizoenen, selectie-ontwikkeling, blessures, schorsingen, beker) | C | De maker speelt 3 seizoenen achter elkaar zonder blokkerende fout; meetplan §9 (4 weken 2 sessies per week) begint te tellen |
| C: uitdagingen 11-25 in thema's | C | Elke uitdaging geijkt met de runner op verse seeds (bestaande methode) |
| C: vriendenmodus 3b compleet (deelcode, uitdaging aan een vriend, ladder zonder server) | C | Twee vrienden spelen een ladder van 5 rondes; uitslagen zijn bij beide gelijk (determinisme) |
| B2: training op data, eerste onderdeel (bijvoorbeeld de keuze van de passontvanger) | B | Op de toetsset beter dan de regels op ≥2 kloofmaten, zonder dat de niveaucontrole verslechtert |
| **3D-haalbaarheidscheck** (timebox: 2 weken agentwerk + 4 uur beoordeling) | 3D | Zie G3 |

**R 0.5** (Public op itch als G2 groen was).
**G3 (eind september 2027): 3D-weergave als fase?** Een proef in Godot 4 met dezelfde simulatie en replay (de simulatie blijft los van het beeld):
- Go als: (a) 22 spelers en bal draaien vloeiend (≥60 fps op de ontwikkel-pc, ≥30 fps op de zwakke testlaptop); (b) de camera vanuit het managerperspectief is beter leesbaar dan 2D volgens 3 van de 5 testers; (c) animaties met vrije of zelfgemaakte assets, zonder licentierisico; (d) een schatting voor de volledige fase van ≤1 kwartaal agentwerk plus ≤40 uur van de maker.
- No-go: 2D blijft het product. 3D gaat naar de parkeerlijst; mogelijk wel een "broadcastcamera" in 2D-plus (schaduw, perspectief).
- Let op: 3D maakt de pakketgrootte, systeemeisen en schone-pc-tests zwaarder, en geeft een nieuw risico voor de AI-disclosure als er gegenereerde assets in komen.

### 2027-Q4: kiezen tussen diepte en weergave; Steam-besluit

| Werk | Spoor | Klaar als |
|---|---|---|
| Of 3D-fase deel 1 (bij G3 go), of B3 geleerde AI (bij G4 go), **niet allebei** als hoofdspoor | B/3D | Volgens het eigen klaar-als |
| Steam juridisch spoor (§3.1): jurist, ondernemingsvorm, werkgever, naam | Juridisch | Alle Steam-voorwaarden hebben een eigenaar en een datum |
| Prijs en model bepalen (§3.1) | Meta | Besluit in Besluiten |
| Speltest ronde 3 (breed: 8-10 mensen, deels onbekenden via de open bèta) | A | Verwerkt |

**G4 (geleerde AI):** go als het eerste geleerde onderdeel uit Q3 op de toetsset beter is, deterministisch blijft (vaste gewichten, geen GPU nodig tijdens het spel), en aantoonbaar alleen op toegestane data is getraind. Anders blijft de gekalibreerde AI de basis (de vaultregel blijft gelden).
**G5 (eind december 2027): Steam ja of nee, en wanneer?** Go alleen als alle voorwaarden uit §3.1 "groen" of "met een datum" zijn **en** meetplan §9 (blijven spelen) is ten minste bij de maker en 2 testers gehaald.

### 2028-Q1: Coming Soon

- Steamworks: aanmelden, $100 betalen, belasting- en bankverificatie (10-15 werkdagen), 21 dagen wachttijd na betaling, storepagina met fictieve teams en zonder echte namen, trailer.
- **M4: Coming Soon-pagina publiek** (minimaal 2 weken voor de lancering verplicht; praktisch 3-6 maanden om wishlists op te bouwen).
- Inhoud compleet voor Early Access: feature freeze 6 weken voor de lancering; daarna alleen fixes.
- Steam Playtest (eigen omgeving) als vervolg op de itch-bèta.

### 2028-Q2: Early Access (vroegste moment)

**G6 (4 weken voor de lancering):** lanceren als (a) alle §3.1-voorwaarden groen zijn, (b) de release-checklist (§6.3) volledig groen is op een release-kandidaat, (c) in de Steam Playtest geen blokkerende fout open staat, en (d) de maker het spel zelf nog graag speelt. Anders: uitstellen per maand; een Coming Soon-pagina mag blijven staan.

**M5: Early Access-lancering.** Na de lancering: eerste 4 weken alleen fixes en kleine verbeteringen; een vast, haalbaar updateritme (bijvoorbeeld eens per 6-8 weken), intern bepaald.

### 2028-Q3 en Q4: onderhoud, eventueel live samen spelen, richting 1.0

- EA-updates volgens de feedback; balans en carrière.
- **G7 live samen spelen (fase 12, optioneel):** alleen als de maker het echt wil **en** de vriendenmodus zonder server (3b) gebruikt wordt. Verkenning: twee spelers coachen tegelijk via een relais of P2P (Steam Networking), met de simulatie bij één host. Risico: determinisme over machines (nu niet gegarandeerd), cheaten, kosten van een server. Standaard: no-go.
- **G8 (eind 2028):** 1.0 als de EA-feedback stabiel is en de inhoud af; anders de EA verlengen met een nieuw plan.

### 3.1 Voorwaarden voor een Steam-release

| Voorwaarde | Wat nodig is | Uiterlijk | Status 2026-10 |
|---|---|---|---|
| Datalicenties | Wyscout en DFL bevestigd of het risico schriftelijk geaccepteerd; SkillCorner-MIT voor data bevestigd of vervangen; jurist kijkt naar "model getraind op data van derden" | 2027-Q4 | Open |
| Geen StatsBomb | Geen afgeleide parameter in het product; historie opgeschoond als de broncode ooit openbaar wordt | 2027-Q2 | Deels (schot en kaarten vervangen; overige uitvoeringsparameters open) |
| Namen en logo's | Alleen fictieve clubs, spelers en competities; geen lookalikes; **geen** officiële importknop in de eerste release; moddingbeleid opgesteld | 2027-Q4 | Fictief: ja; beleid: open |
| Merkcontrole | "Gaffer's Notebook" in EUIPO/BBIE/USPTO klasse 9, 28 en 41; eventueel zelf deponeren (Benelux/EU) | 2027-Q1 | Open |
| Eigen data | Amateurniveau en natuurkunde deels op eigen metingen (§5.2) | 2028-Q1 | Niet gestart |
| AI-disclosure | Logboek van wat met AI is gemaakt (code, teksten, audio, eventuele beelden); de actuele Steam-regel bij de aanmelding lezen; nagaan of de simulatie-AI als "live-generated" geldt (waarschijnlijk niet: geen generatieve AI, maar wel controleren) | Bij aanmelding | Logboek: deels via git en de sessielogs |
| Ondernemingsvorm | Eenmanszaak (KvK, btw, eventueel KOR), boekhouder kort voor de aanmelding; BV alleen bij een hoog aansprakelijkheidsrisico (jurist) | 2027-Q4 | Open |
| Werkgever | Nagaan of de cao/het contract nevenwerkzaamheden of intellectueel eigendom regelt (deels gebouwd op de werklaptop, wel buiten werktijd); zo nodig melden | 2027-Q3 | Open |
| Prijs en model | Voorstel om te toetsen: Early Access €9,99-14,99, daarna 1.0 iets hoger; geen microtransacties; de demo blijft gratis | 2027-Q4 | Open |
| Steam-wachttijden | Fee, 21 dagen, verificatie 10-15 werkdagen, Coming Soon ≥2 weken, review van pagina en build | Start ≥4 maanden voor de lancering | - |
| Credits en notices | Godot (MIT), lettertypen (OFL), Wyscout/DFL/SkillCorner-vermelding, overige third party | Bij elke release | In de zip: ja |

---

## 4. Testen en kwaliteit

### 4.1 Speltests met vrienden

| Ronde | Wanneer | Wie | Focus |
|---|---|---|---|
| 1 | 2026-Q4 | 1 proeftester, daarna 4-5 vrienden | Begrijpen, vastlopen, leuk, tactiekverschil zien, verder willen (`playtest.md`) |
| 2 | 2027-Q1 | Ronde 1 + 2 nieuwe | Is realisme zichtbaar; niveaus; vergelijking met ronde 1 |
| 3 | 2027-Q4 | 8-10, deels open bèta | Carrière, uitdagingen, vriendenmodus, blijven spelen |
| Steam Playtest | 2028-Q1 | Breder, met beheerde toegang | Stabiliteit, eerste sessie, systeemeisen |

Regels: steeds meekijken via een gedeeld scherm waar het kan; per tester het versielabel (`VERSIE.txt`) noteren; patronen zoeken, geen percentages bij <10 testers; een oogtest na elke grote wijziging van de simulatie (meetplan §2).

### 4.2 Feedback zonder telemetrie

1. In het spel: F9 maakt een lokaal feedbackpakket (versie, seed, tactiek, log). **Er wordt niets automatisch verstuurd**; de speler deelt het zelf.
2. Kanalen: itch-reacties of een e-mailadres van het project (geen privéadres); later de Steam-discussies. Geen Discord tot de maker anders beslist (C4).
3. Verwerking: elke ronde in `docs/playtest-vN.md` met een top-10, oorzaak (sim, UI, inhoud) en een besluit (fixen, parkeren, afwijzen).
4. Reproduceren: met seed + tactiek + versie is elke wedstrijd na te spelen (determinisme op dezelfde build).
5. Privacy: het pakket bevat geen namen of machinegegevens buiten de versie; vóór de open bèta controleren (bestaande feedbackprivacycontrole).

### 4.3 Regressiesuite

| Laag | Wat | Wanneer |
|---|---|---|
| Sim-unit en integratie | xUnit, Release, `-m:1`, één dotnet tegelijk | Elke merge |
| Determinisme | Goldens en event-/snapshothashes; vernieuwen alleen na een verklaarde wijziging en twee gelijke herhalingen | Elke merge |
| Balans | `LevelCalibrationTests` op vaste seeds + een grote verse reeks (≥240 wedstrijden) vóór elke release | Release-kandidaat |
| Realisme | Kloofmeting (`--positional-probe` + `ai_gap_compare.py`) en meetplan §1 | Elke sim-release, trend bijhouden |
| UI | `run_autotest.py` (echte invoer), smoke van alle hoofdscènes, renderproeven EN/NL, klein venster | Elke merge in de integratie |
| Exe | `playtest_exe.py` op de echte export; wedstrijd op echte tijd zonder doorspoelen | Release-kandidaat |
| Opslag | Oude opslagbestanden laden (versie 1, 2, ...), beschadigd bestand, nieuwer formaat | Bij elke wijziging van een opslagformaat |
| Schone pc | Andere Windows-pc, nieuwe gebruiker | Elke release naar buiten |

Doel 2026-Q4: de lagen sim, determinisme, UI en exe in één script, met een samenvatting groen/rood.

### 4.4 Kalibratiedoelen per niveau

Huidige grenzen (`docs/kalibratie.md`, besluit C1): doelpunten per wedstrijd 2,2-3,2 per niveaupaar; prof wint ≥55% van amateur. Voorstel om per kwartaal aan te scherpen (de maker keurt het goed, §7):

| Maatstaf | Prof (referentie DFL/Wyscout) | Halfprof | Amateur | Doel vanaf |
|---|---|---|---|---|
| Doelpunten per wedstrijd | 2,5-3,0 | 2,5-3,2 | 2,8-3,6 | 2027-Q1 |
| Prof wint van amateur | ≥65% (nu 55%) | - | - | 55% nu, 60% 2027-Q1, 65% 2027-Q2 |
| Passnauwkeurigheid | 78-85% | 70-78% | 62-72% (op gevoel) | 2027-Q1 |
| Schoten per team | 11-15 | 10-15 | 9-16 | 2027-Q1 |
| Stilstand zonder bal | ≤10% (echt 3%) | ≤12% | ≤15% | 10% in 2027-Q1, 5% in 2027-Q2 |
| Meeschuiven zijwaarts (factor) | 0,30-0,45 | 0,25-0,40 | 0,15-0,35 | 2027-Q1 |
| Niveauverschil bij een complexe tactiek (meetplan §4) | ≤3 pp passnauwkeurigheid verlies | - | ≥10 pp verlies | 2027-Q2 |
| Afstand per veldspeler per 90 min | 9,5-11,5 km | 9-11 km | 8-10,5 km (op gevoel) | 2027-Q1 |

Amateur- en halfprofwaarden zijn **aannames zonder data** tot er eigen metingen zijn (§5.2). Zet ze als zodanig in `kalibratie.md`.

---

## 5. Data en licenties

### 5.1 Bronnen

| Bron | Licentie (vastgelegd) | Gebruik nu | Plan |
|---|---|---|---|
| StatsBomb Open Data | Geen commerciële exploitatie, ook niet van "any analysis derived" | Alleen een lokale meetlat; een deel van de uitvoeringsparameters is er historisch op getuned | **Eruit** uit alle parameters vóór de open bèta (2027-Q2); blijft alleen een lokale vergelijking |
| Wyscout (Pappalardo e.a.) | CC BY 4.0 volgens figshare; tegenstrijdige melding elders | Schot- en duel-/kaartmodellen, met een notice | **Blijft**, na bevestiging of een geaccepteerd risico (G2) |
| DFL/Sportec | CC BY 4.0 | Beweging, kloofanalyse | **Blijft**, met vermelding; klein (7 wedstrijden) |
| SkillCorner Open Data | MIT (repo); dekking van de data onzeker | Natuurkunde, beweging | **Blijft voorlopig**; navragen of vervangen door DFL en eigen metingen vóór Steam |
| openfootball | CC0 (repo, niet zelf geverifieerd) | Eredivisie-uitslagen als profiel | **Blijft**; LICENSE nalezen |
| Metrica, PFF, FotMob | Onduidelijk, niet-commercieel of alleen test | Niet in het product | **Nooit in het product** |

### 5.2 Vervangen door eigen metingen

| Wat | Waarom | Hoe (voorstel) | Wanneer |
|---|---|---|---|
| Amateur- en halfprofgedrag | Er is geen open data; nu op gevoel | De maker of vrienden filmen met toestemming eigen amateurwedstrijden (vaste camera, hele veld); semi-automatisch tellen met een eigen script: passen, afstanden tussen linies, reactietijd bij balverlies. Geen gezichten of namen bewaren | Verkenning 2027-Q2, meting 2027-Q3 |
| Natuurkunde (bal) | Deels aannames | Eigen proeven met een telefoon op 240 fps: rolweerstand op gras, passsnelheid, stuit | 2027-Q1 (klein, 4-6 uur) |
| Oordeel "lijkt op voetbal" | Data meet niet alles | Oogtest met een vast protocol (meetplan §2) | Elke grote sim-wijziging |
| Uitkomstdoelen (goals, kaarten) | Afhankelijk van Wyscout | Kruiscontrole met openfootball (CC0) en DFL | Doorlopend |

Regels die blijven: nooit ruwe data meeleveren; per parameter de herkomst noteren (bron, datum, script); de pijplijn per dataset te vervangen; toetsset gescheiden van de fitset. Toestemming bij eigen opnamen schriftelijk, ook van de tegenstander en de scheidsrechter, en geen herkenbare beelden bewaren na het tellen (AVG).

---

## 6. Team, tools en werkwijze

### 6.1 Agents-workflow

| Regel | Waarom |
|---|---|
| Elke taak in een eigen git-worktree en branch (`demo/<onderwerp>`, later `<spoor>/<onderwerp>`) | Parallel zonder conflicten |
| Eén integratiebranch per periode; mergen alleen na een groene build, smoke en tests; stoppen bij een conflict in `sim/` | `sim/` is het hart en is deterministisch |
| Max. 1 zware dotnet-opdracht tegelijk; Runner-reeksen niet parallel | Eerdere fouten met de compiler-server en geheugen |
| `docs/hervatten.md` is de enige bron van waarheid voor de stand; elke agent schrijft daar een korte sectie | Hervatbaar na pauzes |
| Grenzen en seeds niet aanpassen om groen te krijgen; goldens alleen na verklaarde wijziging | Eerlijke metingen |
| Elk agentresultaat eindigt met "wat moet de maker zien of beslissen" | Het knelpunt is de maker, niet de code |
| Een review-agent per merge in `sim/` (correctheid, determinisme) | Fouten van parallelle agents vroeg vangen |
| De maker plant per kwartaal 2 vaste speelavonden zonder te bouwen | Meetplan §9 en een echt oordeel |
| AI-logboek: per release opsommen wat met AI is gemaakt (komt grotendeels uit git en `hervatten.md`) | Steam-disclosure |

### 6.2 Buildpijplijn

- `scripts/build_demo.ps1`: versie uit een **schone** commit (`<hash>`, nooit `-dirty` voor buiten), `VERSIE.txt`, zip `GaffersNotebook-<releaselabel>.zip`; `prepare_playable.ps1` voor de lokale speelversie.
- Doel 2027-Q1: één opdracht `release.ps1` = schone controle → volledige suite → export zonder testklassen → zip → archiefcontrole (paden, CRC, notices, geen `calibration/`) → exetest → SHA256 → lokale tag.
- Upload naar itch via butler pas na een handmatig akkoord van de maker; later SteamPipe met hetzelfde pakket.

### 6.3 Release-checklist (elke release naar buiten)

1. Schone commit, tag `vX.Y.Z[-beta.N]`, release-label in de zip.
2. Volledige sim-suite groen, of elke rode test benoemd en geaccepteerd door de maker.
3. Grote balansreeks en kloofmeting gedraaid en in `kalibratie.md`.
4. UI-autotests, smoke en exetest groen; wedstrijd op echte tijd gecontroleerd.
5. Oude opslag laadt; nieuwe opslag geeft een reservekopie.
6. Schone-pc-test (bij een release naar buiten).
7. Zip: alleen spelbestanden, notices en credits, geen ruwe data of testklassen.
8. Licentiestand gelijk aan `Licenties en Steam`; geen echte namen in de pagina of de screenshots.
9. Pagina- en releasetekst zonder beloftes over toekomstige functies.
10. Back-up gemaakt (§6.4) vóór de upload.

### 6.4 Back-ups

| Wat | Hoe | Hoe vaak |
|---|---|---|
| Repo | Privé-GitHub (push alleen na akkoord van de maker) + `git bundle` naar een lokale back-upmap | Na elke werksessie / elke release |
| Vault | OneDrive (zoals nu) + een kopie op de eigen pc (rest van WP1) | Doorlopend / maandelijks |
| Releases | Elke uitgebrachte zip + SHA256 op een externe schijf | Elke release |
| Lokale data en modellen | `data/` en `calibration/`-bronnen buiten git op een externe schijf (niet in de cloud, vanwege de licenties) | Per kwartaal |
| Terugzettest | Een verse clone uit de bundel bouwen | Per kwartaal |

---

## 7. Openstaande beslissingen van de maker, per kwartaal

| Kwartaal | Beslissing | Opties / advies |
|---|---|---|
| 2026-Q4 | Conceptmail aan de data-auteurs versturen? | Advies: ja, vroeg; antwoorden duren maanden |
| 2026-Q4 | Merkcontrole zelf of via een jurist | Advies: eerst zelf zoeken (gratis), een jurist bij twijfel |
| 2026-Q4 | De v0-ontwerpen beoordelen (simulatie, spelersmodel, datamodel, meetplan) | Minimaal: het meetplan en de kalibratiedoelen van §4.4 |
| 2026-Q4 | Referentieprofiel voor realisme (PL 2015/16, toernooien, Bundesliga/DFL) | Advies: DFL + Wyscout als productprofiel; StatsBomb alleen lokaal |
| 2026-Q4 | Uitdagingen alleen haalbaar met een laag blok: gewenst? | Open sinds 2026-10-16 |
| 2027-Q1 | Kalibratiedoelen per niveau goedkeuren (§4.4) | Inclusief profwinst 60% en daarna 65% |
| 2027-Q1 | Vermoeidheid en vertrouwdheid standaard aan? | Na speltest 2 |
| 2027-Q1 | Eigen opnamen van amateurwedstrijden: ja/nee, welke club, toestemming | Zie §5.2 |
| 2027-Q2 | G2: open bèta Public op itch | Voorwaarden §3 |
| 2027-Q2 | Wyscout-risico accepteren zonder antwoord van de auteurs? | Alleen schriftelijk in Besluiten |
| 2027-Q2 | Repo-historie opschonen? | Alleen nodig bij openbare broncode of het delen van de repo |
| 2027-Q3 | G3: 3D-fase | Criteria §3 |
| 2027-Q3 | Werkgever: nevenwerkzaamheden/IP nagaan | Vóór Steam-uitgaven |
| 2027-Q4 | G4 geleerde AI, G5 Steam | Criteria §3 |
| 2027-Q4 | Ondernemingsvorm, boekhouder, prijs | Eenmanszaak; prijs €9,99-14,99 EA als startpunt |
| 2027-Q4 | Definitieve spelnaam, eventueel deponeren | Na de merkcontrole |
| 2028-Q1 | Early Access of meteen 1.0 | Advies: EA, het past bij een simulatie die blijft groeien |
| 2028-Q1 | Moddingbeleid en importfunctie voor echte namen | Advies: geen officiële import in de eerste release |
| 2028-Q2 | G6: lanceren of uitstellen | Criteria §3 |
| 2028-Q3 | G7: live samen spelen | Standaard nee |
| 2028-Q4 | G8: 1.0 of de EA verlengen | Op basis van de feedback |

---

## 8. Eerlijke tijdlijn met onzekerheidsbanden

P50 = even grote kans op eerder of later; P90 = 9 van de 10 keer niet later dan dit. De banden zijn breed omdat het knelpunt mensen en derden zijn, niet de code.

| Mijlpaal | P50 | P90 | Grootste onzekerheid |
|---|---|---|---|
| M1 Speltest ronde 1 verwerkt, R 0.2 | 2026-12 | 2027-02 | Beschikbaarheid van testers rond de feestdagen |
| G1 "lijkt op voetbal" (oogtest ≥3,0) | 2026-12 | 2027-04 | B1 lost kloven op zonder nieuwe balansproblemen |
| Realisme 8/10 en de niveaudoelen voor 2027-Q1 | 2027-03 | 2027-09 | Afwegingen tussen maatstaven; kleine referentiedataset |
| M2 Open bèta Public op itch | 2027-06 | 2027-12 | Antwoord over de datalicentie, merkcontrole |
| M3 Carrière v1 + vriendenmodus compleet | 2027-09 | 2028-03 | Speeltijd van de maker voor beoordeling |
| 3D-besluit (G3) | 2027-09 | 2027-12 | Leesbaarheid en assets |
| 3D-weergave speelbaar (alleen bij go) | 2028-03 | 2028-12 | Animatie en camera; vaak onderschat |
| Geleerd AI-onderdeel in het product (alleen bij go) | 2027-12 | 2028-09 of nooit | Hoeveel data er is en mag; determinisme |
| M4 Steam Coming Soon | 2028-02 | 2028-09 | Juridisch spoor, ondernemingsvorm, werkgever |
| M5 Steam Early Access | 2028-05 | 2029-03 | Alles hierboven + de Steam-review |
| 1.0 | 2029 | 2030 of later | Afhankelijk van de EA-feedback en de motivatie |
| Live samen spelen | niet gepland | - | Optioneel; standaard no-go |

Vergelijking met de vault: de roadmap van 2026-10-12 schatte "eerste bruikbare versie na 12-20 maanden" en "5-8 jaar voor alles". Voor code is dat ingehaald (de demo staat er na een week). Voor uitbrengen blijft een termijn van **anderhalf tot twee en een half jaar** realistisch, omdat speltests, kalibratie die mensen moeten beoordelen, licenties en Steam hun eigen tempo hebben.

### Top-5 mijlpalen
1. **G1, eind 2026:** vrienden begrijpen het en het lijkt op voetbal (oogtest ≥3,0).
2. **Realisme 8/10 + niveaudoelen, 2027-Q1:** de PRD-hypothese is meetbaar overeind.
3. **M2 open bèta Public, 2027-Q2:** licenties, merk en StatsBomb-vrij afgehandeld.
4. **M3 carrière v1 + vriendenmodus, 2027-Q3:** een reden om terug te komen.
5. **M5 Steam Early Access, vroegst 2028-Q2:** alleen na G5 en G6.

### Grootste risico's

| Risico | Kans | Impact | Maatregel |
|---|---|---|---|
| Datalicentie: geen bevestiging van Wyscout/DFL, of SkillCorner-MIT dekt de data niet | Middel | Hoog (blokkeert Public en Steam) | Vroeg mailen; de pijplijn per bron vervangbaar; eigen metingen; risico alleen schriftelijk accepteren |
| De simulatie blijft "bijna voetbal": kloven sluiten maar maken elders iets slechter | Hoog | Hoog | Vaste toetsset, alle uitslagen bewaren, oogtest als scheidsrechter, G1 serieus nemen |
| De maker als knelpunt: agents leveren meer dan hij kan spelen en beoordelen | Hoog | Middel | Max. 2 sporen per kwartaal, vaste speelavonden, "wat moet de maker zien" bij elk resultaat |
| Merk- of naamconflict ("Gaffer's Notebook") | Laag-middel | Middel | Merkcontrole in 2026-Q4, vóór marketing |
| Werkgever of IP (gebouwd op de werklaptop) | Laag | Hoog | Contract en cao nagaan vóór Steam; zo nodig melden |
| Scope-creep (3D, live, managerlaag) | Hoog | Middel | Go/no-go met harde criteria; parkeerlijst; PRD "niet voor" |
| Motivatie of onderbreking over jaren | Middel | Hoog | Hervatkaart, kleine releases, pauzes zonder schuldgevoel (vaultregel) |
| Determinisme over machines (nodig voor de vriendenladder en live) | Middel | Middel | Test op 2 pc's in 2027-Q2; anders resultaten via een gedeeld replaybestand in plaats van narekenen |

---

*Volgende herziening: na G1 (januari 2027) en daarna elk kwartaal. Wijzigingen in mijlpalen en besluiten gaan naar de vault (`Besluiten`, `Roadmap`) door de maker of met zijn akkoord.*
