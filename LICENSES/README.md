# Licenties van bronnen

Stand 2026-10-13. **Geen juridisch advies.** Details per bron: `DATA_SOURCES.md`. Credits-skelet: `THIRD_PARTY_NOTICES.md`.

Statussen:
- **lokaal**: alleen op de machine van de maker, niet in git, niet in een build.
- **repo-intern**: mag in de repo (met naamsvermelding), maar niet in het product.
- **product-mogelijk**: kan in het product, mits de voorwaarden bevestigd zijn en de credits zijn opgenomen.

| Bron | Licentie (kernzin) | Gecontroleerd op | Status | Credits / naamsvermelding |
|---|---|---|---|---|
| StatsBomb Open Data | Public Data User Agreement, art. 1.2.2: verbiedt commerciele exploitatie van de data of van "any analysis derived" | 2026-10-13 (README, LICENSE.pdf) | lokaal (afgeleide modellen staan in `.gitignore`) | "StatsBomb" met logo bij elke publicatie |
| Wyscout public dataset (Pappalardo en Massucco, Events en Matches) | CC BY 4.0 op beide primaire records: https://api.figshare.com/v2/articles/7770599 en https://api.figshare.com/v2/articles/7770422 | 2026-10-17 (license.name en license.url; lokale archieven gelijk aan officiële MD5) | repo-intern; product-mogelijk na afronding van het publicatievoorbehoud uit `Licenties en Steam` | `WYSCOuT_NOTICE.txt` in de export, met beide dataset-DOI's, artikel en eigen bewerkingen |
| DFL/Sportec integrated dataset (Bassek e.a., Sci Data 12:195) | CC BY 4.0 | 2026-10-13 | repo-intern; product-mogelijk na bevestiging | DFL Deutsche Fussball Liga / Sportec Solutions, "An integrated dataset of spatiotemporal and event data in elite soccer" |
| SkillCorner Open Data | MIT (repo); of MIT ook de data dekt is niet vastgesteld | 2026-10-13 | repo-intern / product-mogelijk | Credit "SkillCorner" en de MIT-licentietekst bewaren |
| openfootball | CC0 / public domain | 2026-10-13 | product-mogelijk | Niet verplicht, wel netjes |
| FotMob (gewone pagina's) | Voorwaarden verbieden waarschijnlijk hergebruik | 2026-10-13 (robots.txt) | lokaal (testdata) | Geen: niet gebruiken buiten testdata |
| Godot Engine | MIT, met notices voor ingebouwde componenten | 2026-10-16 (ingebouwde Engine-API en officiële documentatie) | product-mogelijk (als in build) | `build_demo.ps1` schrijft `GODOT_LICENSES.txt` uit exact de gebruikte enginebinary via `export_notices.gd` |
| .NET runtime | MIT, met notices voor ingebouwde componenten | 2026-10-16 (runtime-NuGetpakket) | product-mogelijk | Exportscript kopieert `LICENSE.TXT` en `THIRD-PARTY-NOTICES.TXT` van exact de meegeleverde runtimeversie |
| Publiekgeluiden `crowd_*.wav` (placeholder) | Eigen werk: gegenereerd door `scripts/make_audio.py` uit ruis, geen samples, geen AI-geluid | 2026-10-19 | product-mogelijk (placeholder tot werkstroom E) | Geen |
