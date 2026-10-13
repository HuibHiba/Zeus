# Fase 1b: één mechanische wedstrijdproef

Proef op 13 oktober 2026, seed **42**, `laag_blok_442` thuis en `man_om_man_433` uit. Deze registratie betreft uitsluitend één fixture van de actuele simulator. Zij vervangt niet de historische fase-1a-proef in `docs/benchmarkvergelijking.md` en is **geen geslaagde kalibratie**.

De wedstrijd eindigde met `FullTime` op tick **59.730**, bij stappen van 0,1 seconde: **5.973 seconden** verstreken wedstrijdtijd, inclusief de prototypecompensatie voor stilstaande hervattingen. Dat is 99 minuten en 33 seconden. `HalfTime` staat op tick 29.790 (2.979 seconden). De eindstand was **0–9**.

| Teller | Home | Away |
| --- | ---: | ---: |
| Passpogingen | 678 | 864 |
| BallControl-events | 233 | 540 |
| Intercepties | 403 | 450 |
| Overtredingen | 54 | 48 |
| Gele kaarten | 0 | 0 |
| Rode kaarten | 0 | 0 |
| Schoten | 0 | 31 |
| Doelpunten | 0 | 9 |

Het eventbestand bevat daarnaast **22 Save-events**, 1.538 duels, 28 buitenspelbesluiten, vier double-touchbesluiten en 191 toegekende/genomen hervattingen. Deze getallen komen uit de opgeslagen events; een Save-event is een keepercontrole na een vijandelijk schot, geen gemeten savekwaliteit of volledige interpretatie van Regel 11/12.

## Vergelijking en betekenis

De lokale Premier League 2015/16-tabel bevat 760 teamwedstrijdregels met gemiddeld **485,025 passes per teamwedstrijd**. De ruwe totalen van deze proef bevatten extra speeltijd. Omgerekend naar precies 5.400 verstreken seconden komen de passpogingen op **612,96 home (+26,38%)** en **781,12 away (+61,05%)**. De referentietabel zelf bevat volledige wedstrijden inclusief hun blessuretijd, zonder genormaliseerde looptijd; dit is dus slechts een tempo-indicatie, geen zuivere vergelijking op effectieve speeltijd.

Negen doelpunten uit 31 schoten toont dat de mechanische keeperroute nu verschil maakt tussen schot en doelpunt. De 0–9-verhouding, het hoge passtempo, de grote hoeveelheid duels/overtredingen en deze ene seed onderbouwen geen realismeclaim. Een seizoengemiddelde is ook geen verwachting voor iedere individuele fixture. Er zijn nog geen vastgelegde, gevalideerde kalibratiemarges gehaald.

De duelroute levert uitsluitend expliciet synthetische `Careless`-incidenten. Zij stelt niet automatisch slachtoffercontact, onbesuisd (`Reckless`) of buitensporig (`ExcessiveForce`) gedrag vast; SPA/DOGSO en voordeel worden niet automatisch waargenomen. **Nul kaarten bewijst daarom geen realistische discipline.** BallControl-totalen zijn geen passnauwkeurigheid: zij bevatten lossebalcontacten en ontbreken koppelingen tussen afzonderlijke passes en einduitkomsten.

Deze fase controleert uitvoering van regels en wedstrijdmechanica: klok/helftwisseling, aanvalrichting, doelen en hervattingen, strafschopplaats, keepercontrole, directe-doeluitzonderingen en dubbele aanraking. Werkelijke contactclassificatie, volledige buitenspelbetrokkenheid en keeper-/spelersvaardigheden blijven begrensde modelaannames. De vervolgcontrole moet meerdere seeds en tactiekparen gebruiken en concrete regelgevallen afzonderlijk toetsen.

## Reproduceerbare artefacten

- Samenvatting: `data/verification/phase1b-summary.json`.
- Events: `data/verification/phase1b-events.jsonl`.
- Snapshot-uitvoer: `data/verification/phase1b.jsonl`.
- Replay: `data/verification/phase1b.replay.json`, met 59.730 stappen en seed 42; dezelfde simulatorbuild is vereist.
- Eventhash uit replay: `23c05ed1719e63043110bacd5a136524d1d606d1b945e06faaf3037ea900a5cb`.
- Simulator-buildhash uit replay: `bd2aae13af22fba084e7ea48d265e5074ef95492d48361c81f1312571be18d3e`.

Regelbasis en grenzen: `docs/scheidsrechter-regels.md`. Passreferentie: `data/pl_1516_team_match.csv` en het bestaande extractiescript `scripts/benchmark_pl.py`.
