# Benchmarkvergelijking (taak 25)

`scripts/compare_simulation.py` vergelijkt runner-eventtotalen met de lokale StatsBomb-benchmarktabel `data/pl_1516_team_match.csv`. Het gemiddelde wordt bij iedere run uit de CSV berekend. De huidige tabel heeft **760 teamwedstrijdregels** en **485,025 passes per teamwedstrijd** (Premier League 2015/16). Dit is een beschrijvende referentie, geen bewijs dat de simulatie gekalibreerd is.

Gebruik vanuit de repository:

```powershell
python scripts/compare_simulation.py data/verification/summary.json --out data/verification/comparison.json
python scripts/compare_simulation.py data/verification/summary.json --out data/verification/comparison.md
```

Zonder `--out` komt JSON op stdout. `--format markdown` of `--format json` overschrijft de automatische keuze op basis van de uitvoerextensie. Met `--benchmark <csv>` kan een andere tabel worden gekozen; die moet een numerieke `passes`-kolom met teamwedstrijdregels bevatten.

De runner levert `timeSeconds` en objecten `home` en `away` met gehele niet-negatieve tellers `passes`, `ballControls`, `interceptions`, `fouls`, `yellowCards`, `redCards`. Het script vereist een positieve eindtijd. Per team toont het eventtotalen, passes omgerekend naar 90 minuten en procentuele afwijking: `(passes * 5400 / timeSeconds / referentiegemiddelde - 1) * 100`. De CSV telt complete wedstrijden inclusief blessuretijd; deze referentie is zelf niet op exacte speelduur genormaliseerd. Een korte simulatie extrapoleren levert een tempo-indicatie, geen volledige wedstrijdvalidatie.

## Actuele proef: mechanische fase 1a (13 oktober 2026)

De nieuwste runnerproef met seed 42, de huidige `laag_blok_442` en `man_om_man_433` en 54.000 stappen van 0,1 seconde geeft onderstaande 90 minuten. Deze proef vervangt de eerdere proef met het willekeurige kaartmodel. De gegenereerde vergelijking staat in `data/verification/benchmark.md`; de bron is `data/verification/summary.json`.

| Team | Passes | Afwijking t.o.v. 485,025 | Overtredingen | Geel | Rood |
| --- | ---: | ---: | ---: | ---: | ---: |
| Home | 607 | +25,15% | 73 | 0 | 0 |
| Away | 836 | +72,36% | 53 | 0 | 0 |

Dit is één testcase en **geen geslaagde kalibratie**. Het passtempo ligt duidelijk boven het seizoengemiddelde; de grote hoeveelheid duels en overtredingen vraagt nader onderzoek. Er is voor die laatste tellers nog geen gecontroleerde referentietabel of berekende kalibratiemarge. De actieve scheidsrechterroute genereert uitsluitend expliciet synthetische `Careless`-incidenten: zij stelt niet automatisch werkelijk slachtoffercontact, onbesuisd (`Reckless`) of buitensporig (`ExcessiveForce`) gedrag vast. Het oude willekeurige kaartmodel is niet meer actief. **Nul kaarten bewijst daarom geen realistische discipline**; deze proef toetst mechanische fase 1a, niet de ontbrekende incidentwaarneming en context van fase 1b. Zie `docs/scheidsrechter-regels.md` voor de afgebakende regelset.

Mechanische correcties aan boundary-hervattingen, een tijdgebaseerde grace van 0,3 seconde en expliciete afbreking wanneer geen actieve restartnemer overblijft maken de run uitvoerbaar, maar onderbouwen geen claim van voetbalrealisme. De vervolgstap is een vaste reeks tactieken en seeds vergelijken en gecontroleerde referenties voor overtredingen en kaarten extraheren.

## Definities en grenzen

- Een gesimuleerde pass is een `Pass`-event: een daadwerkelijk uitgevoerde passpoging. Het totale aantal balcontacten is geen succespercentage.
- `ballControls` bevat ook contacten met losse ballen. Er ontbreekt nog een expliciete koppeling tussen pass, bedoelde ontvanger en einduitkomst; daarom rapporteert dit script **geen passnauwkeurigheid** en deelt het nooit `ballControls` door `passes`.
- `interceptions`, overtredingen en kaarten zijn voorlopig diagnostische totalen. De CSV heeft geen referentiekolommen voor overtredingen of kaarten.
- Onder `data/raw` staan lokale StatsBomb-eventcaches (`events_*.pkl.gz`; bij inventarisatie 763 bestanden). `scripts/benchmark_pl.py` toont dat het om eerder opgeslagen pandas-events gaat. Het vergelijkingsscript deserialiseert geen picklebestanden en downloadt niets. Een betrouwbare referentie voor overtredingen/kaarten vraagt een aparte gecontroleerde extractiestap, dezelfde wedstrijdselectie en expliciete definities (o.a. tweede geel versus direct rood).
- Er zijn geen willekeurige tolerantiegrenzen of kalibratie-PASS-claims. Een verschil vereist nader onderzoek naar tactieken, looptijd, seeds, wedstrijdregels en eventdefinities.

## Mechanica en vergelijking apart beoordelen

Passkeuze gebruikt waarneembare medespelers, afstanden, vooruitgang en druk op de passlijn. Passuitvoering brengt een door seed reproduceerbare richting- en snelheidsfout aan. De fysieke balbaan en contacten bepalen vervolgens de uitkomst; er is geen extra succesworp. Gerichte tests van keuze, uitvoering, contact en reproduceerbaarheid bewijzen deze mechanica, niet de overeenkomst met een seizoenbenchmark.

Fysische substeps beperken overslaan van contacten en beïnvloeden daardoor eventaantallen. Bij een gewijzigde substepgrootte of contactlogica moet opnieuw worden vergeleken met dezelfde seed, tactieken en eindtijd; hashes tussen verschillende modellen hoeven niet identiek te zijn. Een benchmarkafwijking vervangt de tests van deze mechanica niet. Voor kalibratie zijn meerdere volledige wedstrijden en seeds nodig, gevolgd door definities voor passuitkomsten en gecontroleerde referenties voor de overige events.

Bronnen: lokale `data/pl_1516_team_match.csv`, `scripts/benchmark_pl.py`, `sim/Zeus.Sim/Passing.cs` en `sim/Zeus.Sim.Tests/PassingTests.cs`. De herkomst en publicatievereisten van StatsBomb-data staan in het bestaande extractiescript.
