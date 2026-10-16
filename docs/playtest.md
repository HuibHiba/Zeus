# Playtest v1 (voorlopig)

Voor de maker. Vier à vijf vrienden spelen de eerste versie van Gaffer's Notebook als zip (Windows, geen Steam, geen installatie). Bijbehorend: [playtest-formulier.md](playtest-formulier.md) (vragen aan de tester) en `docs/playtest-v1.md` (de verwerkte uitkomst, komt na de ronde).

## Doelen
Vijf dingen willen we weten:
1. Begrijpt een nieuwe speler wat hij moet doen?
2. Waar loopt hij vast?
3. Wat is leuk?
4. Is het verschil tussen tactieken te zien (in de wedstrijd en in de meter)?
5. Wil hij verder spelen?

## Wat we niet testen
- Realisme van de voetbalsimulatie of de kalibratie (alles is v0, werkhypothese).
- Balans en moeilijkheid van de niveaus; uitslagen op amateurniveau zijn nog onrealistisch groot.
- Prestaties op verschillende pc's, behalve dat we pc-type en vastlopers noteren.
- Geluid, animaties, grafische afwerking en de volledige vertaling.
- Cijfers of statistieken: met 4-5 testers is dit kwalitatief. We zoeken patronen ("drie van de vijf liepen hier vast"), geen percentages.

## Aanpak
**Volgorde.** Eerst één proeftester, daarna pas de rest. De proeftester vindt de blokkerende fouten (spel start niet, route onmogelijk). Los die op, lever een nieuwe zip aan de rest, en noteer welke versie (zie LEESMIJ/VERSIE.txt) elke tester had. Kies als proeftester iemand die het niet erg vindt dat het misgaat.

**Hoe.** Bij voorkeur op afstand of naast de tester met een scherm dat jij ziet (gedeeld scherm), of anders zelfstandig met het formulier. Meekijken levert veel meer op dan het formulier alleen.

**Wat je zegt.** Alleen:
- Hoe je het pakket start (uitpakken, SmartScreen, antivirus) en dat het een vroege versie is.
- "Doe wat je zou doen als je alleen was. Zeg hardop wat je denkt en wat je verwacht."
- Bij een stilte langer dan een halve minuut: "Wat denk je nu?" of "Wat zou je verwachten dat er gebeurt?"
- Bij een crash of echte blokkade: help, en noteer eerst wat er gebeurde.

**Wat je niet zegt.** Geen uitleg van knoppen, menu's, tactieken of wat een uitdaging van je vraagt. Niet "klik daar", "dat is een bug" of "dat komt later". Niet verdedigen of toelichten tijdens het spelen. Elke vraag van de tester ("wat moet ik hier?") is data: noteer hem, antwoord "wat denk je dat het is?". Pas als de tester werkelijk niet verder kan, geef je een zo klein mogelijke hint en noteer je dat.

**Hardop denken via een gesprek.** Praten tijdens het spelen lukt niet iedereen. Laat de tester spelen en praat door met korte, neutrale vragen: "Wat probeer je nu?", "Wat verwachtte je?", "Wat valt je op?". Zo nodig neem je het gesprek op (alleen met toestemming van de tester) en luister je later terug. Na afloop (5 minuten): wat was het leukste, wat het irritantste, wat zou je veranderen, zou je morgen verder spelen? Laat daarna het formulier invullen.

**Wat je noteert.** Per tester een kort blad of bestand:
- Naam of letter, datum, versie (hash uit `VERSIE.txt`), pc-type.
- Tijd per stap uit de speelroute (minuten:seconden, bij benadering).
- Scherm waarop iets gebeurde (Menu, Challenges, wedstrijd, Tactic maker, Tactic meter, League/Competitie).
- Waar vastgelopen: wat hij probeerde, wat hij verwachtte, wat er gebeurde, of hij zelf een uitweg vond of een hint kreeg.
- Letterlijke uitspraken ("ik snap niet wat dit getal is").
- Of hij na de route nog door wilde spelen en wat hij dan koos.

## Speelroute (20-30 minuten)
Doel: een nieuwe speler komt langs de kern van het spel. Je legt de route niet uit; je gebruikt hem om te zien of de speler de stappen zelf vindt. De route staat voor jou, niet voor de tester; geef de tester alleen de zin "probeer het spel te leren kennen, je mag zelf kiezen". Als hij na een paar minuten niets vindt, mag je zeggen: "Er is een onderdeel Challenges."

| # | Stap | Verwachte uitkomst | Tijd |
|---|---|---|---|
| 1 | Zip uitpakken, `GaffersNotebook.exe` starten (SmartScreen/antivirus doorlopen). | Menu verschijnt. Eventuele waarschuwing is begrepen met de LEESMIJ. | 2-3 min |
| 2 | Menu verkennen; taal kiezen (English / Nederlands). | Tester vindt de taalkeuze en ziet de belangrijkste onderdelen. | 1-2 min |
| 3 | Challenges openen; uitdaging 'Scoor tegen het blok' kiezen; tactiek `positiespel_4231` en niveau instellen; **Speel live**. | Tester begrijpt het doel (scoor tegen een laag blok) en start de eerste wedstrijd. | 2-3 min |
| 4 | Wedstrijd kijken; pauzemenu met **B** openen en een wissel of aanpassing doen. | Tester vindt B zonder hulp, ziet de wijziging terug in het spel. Popups voor doelpunten begrepen. | 4-6 min |
| 5 | De wedstrijden van de uitdaging uitspelen (of doorspoelen) en de tussenstand lezen. | Tester snapt 'op koers / nog haalbaar / niet meer haalbaar'. Uitdaging gehaald of bewust mislukt. | 4-6 min |
| 6 | Tactic maker: open `positiespel_4231`, verander iets (rol, instructie of regel) en sla op onder een nieuwe naam. | Tactiek wordt opgeslagen en staat als `(eigen)` in de lijst. Tester snapt de tabbladen. | 4-6 min |
| 7 | Tactic meter: eigen tactiek tegen het origineel vergelijken. | Tester leest de uitslagen en de 'waarom'-redenen en kan zeggen welke tactiek beter is en waarom. | 3-4 min |
| 8 | Challenges: uitdaging 'Houd de nul' met de eigen of een andere tactiek (bijvoorbeeld `laag_blok_442`). | Tester merkt dat een andere tactiek bij een ander doel past. | 3-5 min |
| 9 | League/Competitie: begin een seizoen, speel of simuleer een speelronde. | Tester vindt stand en schema, snapt promotie/degradatie. Seizoen wordt voortgezet of bewust gestopt. | 3-4 min |

Na stap 9: gesprek, daarna het formulier. Wil de tester doorspelen, laat hem dat doen en noteer wat hij kiest; dat is het sterkste teken voor vraag 5. Is stap 9 niet haalbaar binnen de tijd, sla hem dan liever over dan dat je stap 4-7 jaagt.

Controleer vooraf of de namen in de lijst nog kloppen (de uitdagingen heten in het bestand `Houd de nul` en `Scoor tegen het blok`; menu-items zijn in het Engels `League`, `Challenges`, `Squad`, `Tactic maker`, `Tactic meter`).

## Feedbackbestanden verzamelen en verwerken
1. **Bron.** De tester maakt het feedbackbestand met knop Feedback / F9 (nog te bouwen) en stuurt het per mail of chat naar jou. Het formulier vult hij apart in. Zonder F9 zijn jouw notities en het formulier de bron.
2. **Opslaan.** Zet alles in een lokale map `playtest/v1/<tester>/` (niet in git: bevat screenshots en logs van anderen) met je notities, het formulier en het feedbackbestand. Hernoem niets aan de inhoud.
3. **Verwerken.** Geef de map aan Claude met de opdracht om `docs/playtest-v1.md` te maken. Claude leest notities, formulieren, logs en screenshots en groepeert alles per probleem, met aantallen ("3 van 5") en per tester een citaat. Elk punt krijgt een label:
   - **blokkerend**: tester kon niet verder, spel crasht of start niet, of de route wordt onmogelijk.
   - **storend**: tester ging verder maar raakte in de war, verloor tijd of ergerde zich; meerdere testers hadden het of het raakt de kern.
   - **wens**: voorstel, ontbrekende functie of detail dat het spel mooier maakt maar niets in de weg staat.
4. **Beslissen.** Jij kiest wat in de volgende versie komt. Blokkerende punten gaan voor; storende punten die meerdere testers raken daarna; wensen alleen als ze de vijf doelen helpen. `docs/playtest-v1.md` bevat een korte conclusie per doel, de lijst per label en de beslissing.

## Versies uitbrengen
- **Versienummer.** Het script `scripts/build_demo.ps1` schrijft `VERSIE.txt` met de korte git-hash en datum (`-dirty` bij niet-gecommitte wijzigingen) en noemt de zip `GaffersNotebook-<hash>.zip`. Geef alleen zips uit van een schone commit (zonder `-dirty`). Houd per tester bij welke hash hij had. Een leesbaar nummer (bijvoorbeeld `playtest-1`, `playtest-2`) ernaast in je notities kan, maar de hash is de bron.
- **Nieuwe zip.** Stuur de zip met de LEESMIJ.txt erin; vertel kort wat er anders is. Bouwen gebeurt met `powershell -ExecutionPolicy Bypass -File scripts\build_demo.ps1` (zie [demo.md](demo.md)).
- **Update met voortgang.** Voortgang, eigen tactieken, carrière en instellingen staan niet in de spelmap maar in `%APPDATA%\GaffersNotebook\`. Een nieuwe zip raakt die map niet. De tester pakt de nieuwe zip uit in een nieuwe map (de oude mag hij verwijderen) en start hem; zijn voortgang is er dan nog. Verwijderen van `%APPDATA%\GaffersNotebook\` zet alles terug (een schone start). Let op: een opslagformaat dat tussen versies wijzigt kan oude voortgang onbruikbaar maken. Noem dat in de update als je het weet, en test een oude voortgang op de nieuwe versie voordat je hem uitgeeft. Verhuis nooit stilletjes data tussen versies.

## Privacy
- **Alleen fictieve data.** Clubs, spelers en seizoenen in het spel zijn verzonnen. Er worden geen persoonsgegevens van de tester gevraagd door het spel.
- **Niets wordt automatisch verzonden.** Er is geen telemetrie, geen account en geen netwerkverkeer voor analyse. Het spel verstuurt zelf niets.
- **Feedbackbestand (F9, volgt).** Het bevat: een screenshot van het spelvenster op het moment van aanmaken, het spellog (fouten en meldingen), de versie en het systeemtype (besturingssysteem, processor, geheugen, schermformaat) en de tekst die de tester zelf typt. Een screenshot kan alles tonen wat op het spelvenster staat; andere vensters horen er niet in, maar sluit chat of mail liever tijdens het spelen. Het log bevat geen wachtwoorden of accounts. Paden kunnen de Windows-gebruikersnaam bevatten (bijvoorbeeld `C:\Users\<naam>\...`).
- **Nakijken voor het versturen.** De tester krijgt de bestandslocatie te zien en kan het bestand (zip of map) openen: de screenshot is een gewoon plaatje, het log een tekstbestand. Zie je iets wat je niet wilt delen, verwijder het bestand of vervang de regel. Pas dan sturen. Dit moet de LEESMIJ en de melding in het spel ook zeggen. Controleer dit zodra F9 gebouwd is: dat het bestand echt alleen dit bevat, en dat een lege of onleesbare screenshot niet blokkeert.
- **Bewaren door jou.** Bewaar de bestanden lokaal, deel ze niet verder, en verwijder ze na de verwerking in `playtest-v1.md`. In `playtest-v1.md` staan geen namen, alleen letters (tester A, B).

## Nog te beslissen of te doen
- F9 / knop Feedback bouwen (Codex) en de privacytekst in het spel controleren.
- Kiezen hoe feedback binnenkomt (mail of chat) en waar het formulier wordt ingevuld.
- Testers kiezen en een proeftester aanwijzen.
- Controleren of de speelroute klopt met de huidige build (menunamen, de uitdagingen en de pauzetoets B).
- Op een schone pc of in Windows Sandbox testen of het spel start (nog niet gedaan, zie `demo.md`).
