# Herkomst van bronnen in de demo (itch.io, gratis)

Stand: 2026-10-17, branch `demo/claude-afronding`. **Geen juridisch advies**; dit is een technische herkomstcontrole plus een leesverslag van publieke licentiepagina's. Aanvulling op `licenties-check.md`.

## Wat zit er in de zip

`scripts/build_demo.ps1` kopieert alleen git-bestanden uit `tactics/`, `squads/`, `challenges/`, `competitions/` (schema en `profielen.json`) en `data/lang/`, en weigert bestanden met `calibration/`, `wyscout`, `statsbomb`, `skillcorner`, `fotmob`, csv, jsonl enz. Er gaat dus geen `calibration/*.json` en geen ruwe data mee. Maar: getallen die in de C#-code zijn overgenomen zitten wel in de exe (gecompileerd), en `tactics/testcase_a_433.json` gaat wel mee.

| Bron | Afgeleid materiaal in de distributie | Waar |
|---|---|---|
| SkillCorner | Ja, indirect: standaard linehoogtes per fase en rolgroep | `sim/Zeus.Sim/TacticTemplates.cs` (`Lines`; commentaar: "measured templates ... SkillCorner A-League / DFL Bundesliga, indicative"); `tactics/testcase_a_433.json` (opmerking: voor-blok posities "gekalibreerd op gemeten sjablonen (SkillCorner)") |
| DFL/Sportec | Idem, gemengd met SkillCorner (zelfde `Lines`-tabel); of ook loop-/balsnelheden uit `dfl_physics.py` in code zijn beland: niet vastgesteld | `TacticTemplates.cs` |
| Wyscout (Pappalardo) | Ja: schotkanscoefficienten en duel-/foul-/kaartparameters | `sim/Zeus.Sim/Shooting.cs`, `sim/Zeus.Sim/DuelModel.cs` (uit `calibration/wyscout_models.json`, `wyscout_duel_targets.json`, niet in zip) |
| StatsBomb | Geen herkenbare data of modelbestanden. Wel: eigen parameters die op de oude StatsBomb-meetlat zijn getuned (keeperbereik, schot-/passruis, intercept, `KickShieldMeters`) | `docs/bronvervanging-beta.md` sectie "Overige eigen parameters". `Events.cs` noemt "StatsBomb-like" alleen als formaatnaam |

## Per bron

### 1. SkillCorner Open Data
- **Conclusie: vermelding nodig (laag risico).**
- Licentie: de repo `SkillCorner/opendata` heeft een MIT-licentie, "Copyright (c) 2020 SkillCorner" (LICENSE gelezen via GitHub raw, 2026-10-17). De README noemt geen licentie voor de data en vraagt alleen: "if you use the data, we kindly ask that you credit SkillCorner". Over commercieel gebruik of herverspreiding van de data zegt de README niets. Of de MIT-tekst ook de data dekt, is dus niet vast te stellen uit de bron.
- Eisen: MIT eist dat copyright- en licentietekst worden meegeleverd bij "copies or substantial portions" van de software/bestanden; credit is een verzoek. Afgeleide gemiddelde positielijnen zijn geen kopie van bestanden, maar de credit is gratis en afdoende voorzichtig.
- Gratis itch-demo: de MIT-tekst laat gebruik en wijziging toe en verbiedt commercieel gebruik niet; een gratis demo valt er ruim onder. Resterende onzekerheid: de datadekking.
- Voorgestelde tekst (LEESMIJ, kort): `Default tactic line heights are informed by averages computed from SkillCorner Open Data (https://github.com/SkillCorner/opendata, A-League 2024/25). Credit: SkillCorner.`
- Voorgestelde tekst (THIRD_PARTY_NOTICES): 
  ```
  SkillCorner Open Data (https://github.com/SkillCorner/opendata), A-League 2024/25.
  Average formation line heights per game phase were computed from this data; no raw
  data is included. MIT License, Copyright (c) 2020 SkillCorner. https://skillcorner.com
  [volledige MIT-tekst uit de LICENSE van die repo hier invoegen]
  ```

### 2. DFL / Sportec "integrated dataset"
- **Conclusie: vermelding nodig (laag risico, licentie duidelijk).**
- Licentie: Figshare-record 28196177 (API gelezen 2026-10-17): CC BY 4.0, https://creativecommons.org/licenses/by/4.0/. Titel "An integrated dataset of spatiotemporal and event data in elite soccer", auteurs Manuel Bassek, Robert Rein, Hendrik Weber, Daniel Memmert; dataset-DOI 10.6084/m9.figshare.28196177.v1; artikel 10.1038/s41597-025-04505-y (Scientific Data 2025).
- Eisen CC BY 4.0: naamsvermelding (auteurs, titel, bron, licentie), link naar de licentie, vermelden dat er wijzigingen zijn, geen suggestie van goedkeuring. Commercieel gebruik en doorgeven van bewerkingen zijn toegestaan; bewerkte data hoeft niet onder dezelfde licentie (geen ShareAlike). Ruwe data wordt niet doorgegeven.
- Gratis itch-demo: ja, mits vermelding.
- Voorgestelde tekst (LEESMIJ): `Default tactic line heights are partly derived (averaged, changed by us) from: Bassek, Rein, Weber, Memmert, "An integrated dataset of spatiotemporal and event data in elite soccer" (DFL/Sportec Solutions), CC BY 4.0. Not endorsed by the authors or the DFL.`
- THIRD_PARTY_NOTICES:
  ```
  Bassek, M., Rein, R., Weber, H., Memmert, D. (2025). An integrated dataset of spatiotemporal
  and event data in elite soccer. Scientific Data. https://doi.org/10.1038/s41597-025-04505-y
  Dataset: https://doi.org/10.6084/m9.figshare.28196177.v1 . License: CC BY 4.0,
  https://creativecommons.org/licenses/by/4.0/ . Changes: we computed averages (formation line
  heights per phase) from 7 Bundesliga 2022/23 matches; no raw data is included. Not endorsed
  by the authors, DFL or Sportec Solutions.
  ```
- Let op: de DFL-bijdrage en SkillCorner-bijdrage aan `Lines` zijn niet te scheiden; vermeld beide.

### 3. StatsBomb Open Data
- **Conclusie: risico (geen vermelding oplost het, tenzij niets afgeleids meer zit).**
- Licentie: de README (hudl/statsbomb open-data, gelezen 2026-10-17) vraagt bronvermelding "StatsBomb" met logo bij publicatie van onderzoek, analyse of inzichten op basis van de data en noemt geen formele licentie. De projectnotities zeggen dat LICENSE.pdf (Public Data User Agreement) is gelezen: art. 1.2.2 verbiedt commerciele exploitatie van de data of "any analysis derived", art. 1.2.1 doorgeven aan derden. Die PDF heb ik nu niet opnieuw gelezen; ik vertrouw op `DATA_SOURCES.md`.
- In de distributie: geen data, geen modelbestand. Onzeker: de eigen uitvoeringsparameters die op StatsBomb-tellingen zijn getuned en niet herijkt. Of dat "any analysis derived" is, is een juridische vraag die ik niet kan beantwoorden.
- Gratis itch-demo: "gratis" is geen vrijstelling als de clausule exploitatie breder leest dan geld verdienen; bevestiging van de bron is nodig als er afgeleide getallen blijven. Advies: eerst herijken tegen Wyscout (zoals `bronvervanging-beta.md` beschrijft) en historie niet publiek maken.
- Tekst: nu **geen** vermelding in LEESMIJ (er is niets aanwijsbaars afgeleids). Als je tot het tegendeel besluit, is de eis "StatsBomb" plus logo. Voor THIRD_PARTY_NOTICES: laat "Niet opnemen" staan.
- Git-historie bevat de oude StatsBomb-modellen (commits `1b8e7a2`, `f1a0e0f`, verwijderd in `04cfd61`); remote `github.com/HuibHiba/Zeus`. Publiek maken van die repo valt buiten de zip, maar is wel doorgeven.

### 4. Wyscout public dataset (Pappalardo en Massucco)
- **Conclusie: vermelding nodig (middelhoog risico: licentie op papier duidelijk, instemming van Wyscout zelf niet bevestigd).**
- Licentie: de projectnotities melden CC BY 4.0 op Figshare-records 7770599 (Events) en 7770422 (Matches), gecontroleerd 2026-10-17 met gelijke MD5. Ik heb die records nu niet opnieuw opgehaald.
- Eisen CC BY 4.0: zoals bij DFL; commercieel gebruik en bewerking toegestaan. `LICENSES/WYSCOuT_NOTICE.txt` doet dit al (beide DOI's, artikel, licentie, wijzigingen, geen goedkeuring) en gaat in de zip.
- Gratis itch-demo: licentietechnisch ja. Open punt: de eerder gemelde "tegenstrijdige meldingen" over Wyscout-voorwaarden en het "publicatievoorbehoud" uit de vaultnotitie `Licenties en Steam` (niet in de repo). Een e-mail aan de auteurs is de goedkoopste zekerheid.
- Tekst LEESMIJ: staat al in `build_demo.ps1` (EN en NL); die is voldoende. THIRD_PARTY_NOTICES staat al op orde (niet aanpassen), maar de "Associated paper"-regel in `WYSCOuT_NOTICE.txt` hoort erbij te blijven.

## Aanbevolen aanpassingen (niet gedaan, buiten opdracht)
1. Voeg SkillCorner- en DFL-blokken hierboven toe aan `LICENSES/THIRD_PARTY_NOTICES.md`, een gecombineerde notice aan de export (zoals `WYSCOuT_NOTICE.txt`) en twee regels aan beide LEESMIJ-teksten in `build_demo.ps1`.
2. Vervang de LEESMIJ-zin "no third-party data is redistributed" niet door iets sterkers zolang `Lines` op SkillCorner/DFL gebaseerd is; "no raw data is redistributed" is juist.
3. Herijk of beoordeel de op StatsBomb getunede parameters voor je Public zet.

## Niet geverifieerd
- StatsBomb LICENSE.pdf en de Wyscout/Figshare-records zelf: niet opnieuw geopend; alleen README en projectnotities.
- Of SkillCorners MIT de data dekt (README zwijgt; schrijf SkillCorner of vraag het na).
- Of snelheden/versnellingen uit `skillcorner_physics.py` of `dfl_physics.py` als constanten in de sim staan (niet gevonden in een trefwoordzoek; herleiding via code-commentaar is niet uitputtend).
- Of `competitions/profielen.json` (in de zip) waarden uit rapporten bevat die niet herverspreid mogen worden; alleen het schema-commentaar gelezen.
- De exacte inhoud van de nieuwste zip is hier niet opnieuw gecontroleerd (zie `licenties-check.md`).
