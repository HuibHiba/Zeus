# DATA_SOURCES

Herkomst, licentie en gebruiksbeperking van alle externe data. Stand 2026-10-13. **Geen juridisch advies.** Ruwe data staat lokaal in `data/` en komt nooit in git of in een build. Alleen eigen afgeleide parameters (percentielen, gemiddelden) mogen in de repo, en alleen uit bronnen waarvan de voorwaarden dat toestaan. Meer uitleg in de Obsidian-vault: `Data-inventaris`, `Licenties en Steam`, `Namen en logo's (juridisch)`.

| Bron | Gebruikt voor | Licentie | Gecontroleerd | Afgeleid | Beperking |
|---|---|---|---|---|---|
| StatsBomb Open Data (`hudl/open-data`) | Benchmark Premier League 2015/16 en 383 recente wedstrijden | Eigen "Public Data User Agreement"; README: bronvermelding "StatsBomb" met logo bij publicatie. Commercieel gebruik **onduidelijk** (LICENSE.pdf nog niet gelezen) | 2026-10-13 (README) | `data/pl_1516_team_match.csv`, `data/team_match_all.csv` (lokaal) | Alleen intern onderzoek en kalibratie tot schriftelijke bevestiging; niet in de Steam-build |
| SkillCorner Open Data (`SkillCorner/opendata`) | Loopsnelheden, versnellingen, passsnelheid, linehoogte, fasesjablonen (20 wedstrijden A-League 2024/25) | MIT (repo); README vraagt credit aan SkillCorner. Of MIT ook de data dekt: niet vastgesteld | 2026-10-13 | `data/skillcorner_summary.json`, `data/skillcorner_templates.json` (lokaal) | Credit "SkillCorner"; afgeleide percentielen mogen in documentatie |
| DFL/Sportec "integrated dataset" (Bassek e.a., Scientific Data 2025, figshare 28196177) | Spelers- en balsnelheid, schotsnelheid (7 Bundesliga-wedstrijden 2022/23, 25 Hz) | CC BY 4.0 volgens figshare-metadata | 2026-10-13 | `data/dfl_summary.json` (lokaal) | Naamsvermelding verplicht in de credits van het spel |
| Wyscout public dataset (Pappalardo e.a. 2019, figshare collectie 4415000) | Gedownload (events, wedstrijden, spelers, teams); nog niet verwerkt: 1.941 wedstrijden, 5 topcompetities 2017/18, WK 2018, EK 2016 | **CC BY 4.0** op alle 12 records (figshare-API, door mij gecontroleerd 2026-10-13) | 2026-10-13 | nog niets (lokaal in `data/external/wyscout`) | Naamsvermelding verplicht in de credits (Pappalardo e.a., Scientific Data 2019) |
| openfootball (Football.TXT, alle repos) | Uitslagen en doelpuntenverdeling van 900+ competitie-seizoenen, thuisvoordeel | CC0 (licentiebestand van `europe` gecontroleerd) | 2026-10-13 | `data/leagues_results_summary.csv`, `competitions/profielen.json` | Geen |
| FotMob (gewone pagina's) | **Testdata**: 9 + 64 + steekproef Feyenoord- en Eredivisie-wedstrijden, formaties en teamstatistieken | Voorwaarden verbieden waarschijnlijk hergebruik; `robots.txt` verbiedt alleen `/api/*` | 2026-10-13 (robots.txt) | `data/testdata_fotmob_matches.csv` (lokaal); enkele waarden in `competitions/profielen.json`, gemarkeerd als zekerheid L | **Niet voor het product, een uit te brengen model of een deelbare dataset** (besluit maker, 2026-10-13) |
| Publieke rapporten (CIES Football Observatory, Sportradar, The Analyst, Premier League) | Speeltijd, overtredingen, doelpunten per seizoen per competitie | Eigen voorwaarden; alleen cijfers geciteerd met bron | 2026-10-13 | `competitions/profielen.json` met bron per waarde | Alleen losse cijfers met bron, geen tabellen overnemen |
| Wetenschappelijke publicaties (open access) | Natuurkunde en spelersmodel (zie vault, `Natuurkundewaarden`, `Tactische begrippen`) | Per publicatie | 2026-10-13 | Alleen getallen met bronvermelding in de vault | Citeren met bron |

## Niet gebruikt of uitgesloten
- **FBref, Sofascore** (403 bot-bescherming, niet omzeild), **Understat** (`robots.txt` verbiedt alles), **WhoScored, Transfermarkt** (niet gebruikt).
- **PFF FC WK 2022, Metrica sample data, SoccerNet, jalapic/engsoccerdata, Kaggle-sets zonder duidelijke licentie, football-data.co.uk en football-data.org** (voorwaarden onduidelijk of niet-commercieel; nog niet gebruikt).
- (Wyscout stond hier eerst vanwege een tegenstrijdige licentiemelding; de figshare-API bevestigt CC BY 4.0, zie de tabel hierboven.)

## Credits (voor het spel, bij gebruik)
- StatsBomb (bronvermelding met logo, zodra StatsBomb-afgeleide parameters in een publicatie komen)
- SkillCorner (credit)
- DFL Deutsche Fußball Liga / Sportec Solutions, "An integrated dataset of spatiotemporal and event data in elite soccer" (CC BY 4.0)
- openfootball (niet verplicht, wel netjes)

## Regel
Elke nieuwe bron: licentie lezen, hier invullen (herkomst, licentie, datum van controle, wat is afgeleid, beperking), pas daarna gebruiken.
