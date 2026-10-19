# Speltest ronde 2 (2027-Q1)

Voor de maker. Uitwerking van meerjarenplan 2027-Q1 "Speltest ronde 2 (dezelfde testers + 2 nieuwe)" en §4.1 (focus: is realisme zichtbaar, niveaus, vergelijking met ronde 1). De werkwijze van ronde 1 ([playtest.md](playtest.md)) blijft gelden: meekijken, niets uitleggen, vragen zijn data, patronen zoeken en geen percentages (minder dan 10 testers). Hieronder staat alleen wat anders of extra is.

Bijbehorend: [playtest-formulier-ronde2.md](playtest-formulier-ronde2.md) (vragen aan de tester), `docs/playtest-v2.md` (de verwerkte uitkomst, komt na de ronde) en, optioneel, [amateur-telblad.md](amateur-telblad.md).

Let op: `docs/playtest-v1.md` bestaat nog niet (stand 2026-10-19). De vergelijking in §7 kan pas als ronde 1 verwerkt is.

## 1. Doel

Vijf vragen uit ronde 1 opnieuw (zodat vergelijken kan), plus drie nieuwe:

| # | Vraag | Uit |
|---|---|---|
| 1 | Begrijpt een nieuwe speler wat hij moet doen? | Ronde 1 |
| 2 | Waar loopt hij vast? | Ronde 1 |
| 3 | Wat is leuk? | Ronde 1 |
| 4 | Is het verschil tussen tactieken te zien? | Ronde 1 |
| 5 | Wil hij verder spelen? | Ronde 1 |
| 6 | **Lijkt het op voetbal?** Is het realisme van spoor B1/B5 zichtbaar voor spelers en voor kenners (oogtest)? | Nieuw |
| 7 | **Voelen de niveaus anders?** Ziet een speler het verschil tussen prof, halfprof en amateur aan het spel en niet alleen aan de uitslag (B7)? | Nieuw |
| 8 | **Zijn triggers te maken en te zien?** Lukt een trigger in ≤ 2 minuten en is het effect te zien (C: triggers v1)? | Nieuw, alleen als triggers in de build zitten |

Extra voor de terugkerende testers: zijn de ronde-1-knelpunten (prioriteit 1) echt weg?

## 2. Wie

- **Terugkerend:** dezelfde testers als ronde 1 (letters A-E uit `playtest-v1.md` houden). Zij beantwoorden vooral vraag 5-8 en "is het beter dan vorige keer".
- **Nieuw: 2 testers** zonder ervaring met het spel. Alleen zij meten vraag 1-2 eerlijk (de anderen kennen de route al). Liefst één die voetbalmanagementgames speelt en één die dat niet doet.
- **Kenners voor de oogtest: 3**, los van de testers (mag overlappen met een tester, maar dan de oogtest vóór het spelen doen). Een kenner kijkt veel voetbal of heeft gespeeld of getraind op enig niveau.

### Werven zonder persoonsgegevens te bewaren

- Vraag via je eigen kanalen (persoonlijk, bestaande chat). Geen aanmeldformulier, geen lijst met namen of adressen in de repo of in een gedeeld bestand.
- Geef elke nieuwe tester en elke kenner meteen een letter (F, G voor nieuwe testers; K1-K3 voor kenners). Notities, formulieren en `playtest-v2.md` gebruiken alleen die letters. De koppeling letter ↔ persoon onthoud je zelf of zet je in je eigen, lokale notities buiten git.
- Vraag geen leeftijd, woonplaats of e-mailadres als dat niet nodig is; de enige vragen over de persoon zijn ervaring met managementgames en kennis van voetbal (antwoord in een paar woorden).
- Feedbackbestanden en formulieren lokaal in `playtest/v2/<letter>/` (buiten git) en verwijderen na verwerking, zoals in ronde 1 ([playtest.md](playtest.md) "Privacy").

## 3. Wat testers krijgen

- De zip van **R 0.3** (of de dan geldende build) van een schone commit, met LEESMIJ en `VERSIE.txt`. Noteer per tester de hash.
- **Terugkerende testers:** een kort bericht wat er sinds ronde 1 is veranderd (maximaal 5 regels, alleen wat ze kunnen zien), en of hun voortgang uit ronde 1 nog werkt (zie [playtest.md](playtest.md) "Update met voortgang"; test dit vooraf zelf). Vertel niet waar de verbeteringen zitten in de wedstrijd; dat is wat we meten.
- **Nieuwe testers:** dezelfde introductie als ronde 1 (alleen starten en "het is een vroege versie").
- Het formulier [playtest-formulier-ronde2.md](playtest-formulier-ronde2.md) na afloop.
- **Kenners:** geen zip; ze kijken mee via gedeeld scherm of krijgen 5 korte schermopnamen (zie §5.2).

## 4. Taken per sessie (35-45 minuten)

### Nieuwe testers (F, G)
De volledige speelroute van ronde 1 ([playtest.md](playtest.md), stap 1-9), daarna stap 10-12 hieronder. Dezelfde regel: sla stap 9 liever over dan stap 4-7 te jagen.

### Terugkerende testers (A-E)
Stap 1 alleen als ze een nieuwe zip uitpakken; daarna zelf kiezen ("speel zoals je thuis zou spelen") gedurende 10 minuten, dan stap 10-12. Noteer wat ze uit zichzelf kiezen.

| # | Stap | Verwachte uitkomst | Tijd |
|---|---|---|---|
| 10 | **Realisme kijken.** Eén wedstrijd op profniveau op normale snelheid kijken (minstens 10 minuten speeltijd of één helft), daarna vraag: "Wat leek op echt voetbal en wat niet?" | Tester noemt zelf concrete momenten (meeschuiven, opbouw, pressing, stilstaande spelers). Noteer letterlijk. | 6-8 min |
| 11 | **Niveaus vergelijken.** Dezelfde tactiek, dezelfde tegenstander, eerst op amateur, dan op prof (doorspoelen mag na de eerste minuten). Vraag: "Zag je verschil, en waaraan?" | Tester noemt verschillen in het spel (fouten, afstanden, passen), niet alleen de uitslag. | 6-8 min |
| 12 | **Trigger maken** (alleen als triggers in de build zitten). Opdracht in één zin: "Maak een regel: als de bal bij hun linksachter is, gaat onze rechtsbuiten druk zetten." Tijd meten vanaf openen van de Tactic maker tot opslaan; daarna in de meter of een wedstrijd kijken of het effect zichtbaar is. | Gelukt in ≤ 2 minuten zonder hint; tester ziet het effect. | 4-6 min |

Na stap 12: gesprek (zoals ronde 1, plus "is het beter of slechter dan vorige keer?" voor terugkerende testers), dan het formulier.

## 5. Wat gemeten wordt

### 5.1 Zelfde maatstaven als ronde 1
Formuliervragen 1-10 ongewijzigd (zelfde tekst, zelfde schaal 1-5), plus de notities per tester zoals in [playtest.md](playtest.md) "Wat je noteert" (tijd per stap, scherm, vastlopers, citaten, doorspelen). Zo kun je de cijfers per vraag naast ronde 1 leggen.

Daarnaast, zoals bij G1 (meerjarenplan 2026-Q4): **hoeveel testers willen een volgende versie spelen** (formulier vraag 11, ja/nee/weet niet). Doorgaan-grens bij G1 was ≥ 3 van 5.

### 5.2 Realisme-oogtest (3 kenners, 5 replays)
Volgens meerjarenplan §4.1 en G1/M2: 3 kenners beoordelen 5 replays op een schaal van 1-5; doel ≥ 3,0 gemiddeld. Het volledige protocol staat in het meetplan in de vault (§2); volg dat als het afwijkt van dit stappenplan.

1. **Replays kiezen (maker, vooraf).** 5 wedstrijden op profniveau met vaste seeds en verschillende tactiekparen (bijvoorbeeld positiespel tegen laag blok, pressing tegen pressing). Opnemen met `--replay-out` (zie `hervatten.md`) zodat ze met dezelfde build exact terug te spelen zijn. Per replay een fragment van 3-5 minuten speeltijd op 1x, zonder debuglagen, zonder uitslag in beeld als dat kan. Noteer seed, tactieken en hash; gebruik **dezelfde 5 seeds** bij elke volgende oogtest.
2. **Afspelen.** Live via gedeeld scherm, of als schermopname die je alleen aan de kenner stuurt en na afloop verwijdert. De kenner kijkt alle 5 in willekeurige volgorde, zonder uitleg vooraf behalve: "Dit is een voetbalsimulatie van bovenaf. Beoordeel of het eruitziet als echt voetbal."
3. **Score per replay (1-5):** "Dit lijkt op echt voetbal." 1 = helemaal niet, 3 = deels, 5 = helemaal wel. Plus één zin: wat viel het meest op (goed en fout).
4. **Uitkomst:** gemiddelde per kenner en over alle 15 scores; laagste replay en waarom. Grens ≥ 3,0.
5. **Volgende versie:** vraag elke kenner aan het eind ook: "Zou je een volgende versie willen spelen of zien?" (ja/nee/weet niet).

### 5.3 Niveaus en triggers
- Formuliervraag 13 (niveauverschil 1-5) en de notities van stap 11 (waaraan zag de tester het verschil).
- Stap 12: tijd tot opslaan (mm:ss), gelukt ja/nee, hint ja/nee, effect gezien ja/nee; formuliervraag 14.

## 6. Verwerken
Zoals ronde 1 ([playtest.md](playtest.md) "Feedbackbestanden verzamelen en verwerken"), maar in `playtest/v2/` en met als uitkomst `docs/playtest-v2.md`: top-10 knelpunten met oorzaak (sim, UI, inhoud) en besluit (fixen, parkeren, afwijzen), plus de ingevulde tabel hieronder. Meerjarenplan 2027-Q2 noemt een hoogtepuntmodus "als speltest 2 zegt dat 20x onleesbaar is": noteer daarom expliciet of testers bij doorspoelen nog konden volgen wat er gebeurde.

## 7. Vergelijking ronde 1 en ronde 2 (in te vullen)

Schaalvragen: noteer het aantal testers per antwoord (bijvoorbeeld "4,4,3,5,2"), geen percentages. "Nieuw" = alleen de 2 nieuwe testers (voor begrip en vastlopen is dat de eerlijke vergelijking met ronde 1).

| Maatstaf | Ronde 1 (build: ____) | Ronde 2 alle testers (build: ____) | Ronde 2 alleen nieuw | Beter / gelijk / slechter |
|---|---|---|---|---|
| Aantal testers | | | 2 | |
| V1 Begrip (1-5) | | | | |
| V2 Vastgelopen: aantal plekken, top-3 | | | | |
| V3 Leukste moment: meest genoemd | | | | |
| V4 Verschil tussen tactieken (1-5) | | | | |
| V5 Verder spelen (1-5) | | | | |
| V6 Uitdagingen gehaald / geprobeerd | | | | |
| V7 Bugs: aantal blokkerend / storend | | | | |
| V8 Eén ding veranderen: meest genoemd | | | | |
| V10 Speelduur (min) | | | | |
| Tijd speelroute stap 1-9 (min, mediaan) | | | | |
| Hints gegeven (aantal) | | | | |
| Doorgespeeld na de route (aantal testers) | | | | |
| Wil volgende versie spelen (ja / totaal) | | | | |
| Ronde-1-knelpunten prio 1 nog gezien | n.v.t. | | | |
| V12 Lijkt op voetbal, testers (1-5) | n.v.t. | | | |
| V13 Niveauverschil zichtbaar (1-5) | n.v.t. | | | |
| V14 Trigger gelukt ≤ 2 min (aantal) | n.v.t. | | | |
| Oogtest kenners, gemiddelde (doel ≥ 3,0) | (G1-oogtest: ____) | | n.v.t. | |
| Oogtest, laagste replay en reden | | | n.v.t. | |
| Kenners: wil volgende versie (ja / 3) | | | n.v.t. | |

## 8. Voorbereiding (checklist)
- [ ] `playtest-v1.md` af en de prio-1-knelpunten dicht (anders meet ronde 2 dezelfde fouten).
- [ ] Build van een schone commit; hash genoteerd; schone-pc-test gedaan.
- [ ] Voortgang uit ronde 1 getest op de nieuwe build.
- [ ] 5 replays gekozen en opgenomen; seeds vastgelegd voor latere oogtests.
- [ ] Namen in de speelroute nagelopen (menu-items, uitdagingen, B-toets, niveaukeuze, triggers).
- [ ] Letters toegewezen (A-E, F-G, K1-K3); map `playtest/v2/` buiten git.
