# Bronvervanging voor de eerste vriendenbeta

Stand: 2026-10-16. Dit document legt herkomst en reproduceerbare technische metingen vast. Het geeft geen juridisch oordeel. De concrete bronvoorwaarden en eerdere besluiten staan in `DATA_SOURCES.md` en `LICENSES/README.md`.

## Bronnen en status

Aanvulling 2026-10-17: ook de afzonderlijke bron [Matches](https://api.figshare.com/v2/articles/7770422), DOI <https://doi.org/10.6084/m9.figshare.7770422.v1>, is gecontroleerd. Events en Matches vermelden beide CC BY 4.0; lokale events.zip en matches.zip zijn gelijk aan de officiële MD5-waarden uit hun records. Matches wordt gebruikt voor het aantal teamwedstrijden, dus beide datasets en het artikel staan nu in de bronvermelding. De volledige API-antwoorden staan lokaal onder `.tmp_demo_check/figshare-{events,matches}-metadata-20261017.json`. Het publicatievoorbehoud uit de leidende vaultnotitie `Licenties en Steam` blijft gelden; deze controle bevestigt bronidentiteit en metadata.

De oorspronkelijke schotbeslissingsfit in `Shooting.cs` bevatte vier rechtstreeks uit StatsBomb geschatte coëfficiënten. De kaartdrempels en penaltygebiedfactor in `DuelModel.cs` waren afgestemd op StatsBomb-doelaantallen; ook verschillende eigen uitvoeringsparameters zijn daarbij getuned. Het uitsluiten van losse databestanden uit een zip verwijdert meegecompileerde parameters niet.

De vervangende bron is Pappalardo en Massucco, *Events*, onderdeel van *Soccer match event dataset*: <https://doi.org/10.6084/m9.figshare.7770599>, bijbehorend artikel Pappalardo e.a., Scientific Data 6:236 (2019), <https://doi.org/10.1038/s41597-019-0247-7>. Het primaire API-record <https://api.figshare.com/v2/articles/7770599> vermeldde op 2026-10-16 expliciet **CC BY 4.0**, auteurs Luca Pappalardo en Emanuele Massucco, en <https://creativecommons.org/licenses/by/4.0/>. Dit document legt die waarneming vast en veronderstelt geen toestemming voor andere Wyscout-producten. Naamsvermelding en aanduiding van de eigen afleiding horen bij het pakket wanneer deze afleiding erin wordt gebruikt.

`calibration/wyscout_duel_targets.json` bevat uitsluitend eigen aggregaten, bronlink, definities en SHA256 van de oorspronkelijke events.zip/matches.zip. Geen ruwe events, namen of spelers-/teamrecords worden toegevoegd. De lokale bronbestanden blijven buiten distributie.

## Reproductie van de doelen

```powershell
python scripts/derive_wyscout_duel_targets.py --source-dir <lokale-wyscout-map> --out <aggregaten.json>
python scripts/propose_duel_calibration.py --targets <aggregaten.json> --scores <lab-scores.jsonl> --baseline-attempt-rate 0.065 --out <voorstel.json>
```

De eerste opdracht leest events.zip en matches.zip. Zeven competities, 3882 teamwedstrijden; gelijke weging per teamwedstrijd, niet per competitie. Periode P (strafschoppenreeks) is uitgesloten; verlenging blijft inbegrepen. Percentages per derde gebruiken de positie van de overtreder in zijn aanvalsrichting. Kaarttags: 1701 rood, 1702 geel, 1703 tweede geel. Direct rood sluit tweede geel uit.

De synthetische Charge-route van de simulator past het best bij Wyscout gewone overtreding sub20. Daarom zijn de kaartdoelen apart uit deze contactfouls geteld. Handbal, protest, tijdrekken, simulatie, late-kaart-, out-of-game- en violent-foul-categorieën zitten wel in de all-foul-auditcijfers, maar niet in deze contactkaartfit. Deze afbakening is een modelkeuze, geen bewezen identieke eventdefinitie.

| Doel | Bronafleiding |
|---|---:|
| Gewone contactfouls/team | 47143 / 3882 = 12,143998 |
| Alle foultypen/team | 51049 / 3882 = 13,150180 |
| Contactkaart/foul eigen derde | 19,22677% |
| Contactkaart/foul midden | 12,23502% |
| Contactkaart/foul aanvallende derde | 7,39111% |
| Contact direct-rood/foul | 98 / 47143 = 0,207878% |
| Alle foultypen: direct rood/team | 174 / 3882 = 0,044822 |
| Alle foultypen: tweede geel/team | 164 / 3882 = 0,042246 |
| Genomen penalties/team | 582 / 3882 = 0,149923 |
| Penalties/contactfoul | 582 / 47143 = 1,234542% |

Wyscout grondduels verdediging plus gewone contactfouls geven 83,937661/team. Dit is uitsluitend een diagnostische proxy: Wyscout verdedigende duels zijn niet dezelfde gebeurtenissen als onze getrokken `Duel`-pogingen. Blind op dit getal afstemmen is niet verantwoord. De voorgestelde AttemptRate wordt afgeleid uit contactfouls/team en de verwachte foulmassa per simulatiepoging.

De penaltyteller betreft genomen strafschoppen, niet de beslissing van de scheidsrechter; hernemingen kunnen verschillen en sommige penalties volgen uit handbal. Daarom is penalties/contactfoul een voorlopig meetdoel. De bron ondersteunt geen claim dat iedere getelde penalty uit de gekozen contactfoulcategorie voortkwam.

## Privélab en voorstel

Een zelfstandige simulatorkopie onder `.tmp_takeover/source_duels/` heeft 90 volledige neutrale wedstrijden gedraaid: `laag_blok_442` tegen zichzelf, `hoge_druk_433` tegen zichzelf, `positiespel_4231` tegen `counter_532`, elk seeds 1..30. Alle wedstrijden eindigden Finished. Geen squadattributen. Deze kalibratiesteekproef bevatte 1777 echte fouls, verdeeld 500/1137/140 over de derden. Er zijn geen productie-edits gedaan voor deze labmeting.

De laboratoriuminstrumentatie voegt geen RNG-trekkingen toe en verandert geen beslissingen. In `Match.ResolveDuel`, na berekening van closing/angle maar vóór `DuelModel.Resolve`, wordt voor iedere getrokken poging de foulkans zonder gebiedsonderdrukking opgeteld, apart voor binnen/buiten het eigen strafschopgebied. Gebruik exact dezelfde defending/discipline/fatiguewaarden als de echte aanroep. Binnen de foulroute wordt vóór `ClassifyConduct` de score gelogd:

```text
score = ConductSpeedWeight * clamp(closing / SpeedScale, 0, 1)
      + (1-ConductSpeedWeight) * abs(normalize(angle)) / pi
zone = relX < 1/3 ? 0 : relX < 2/3 ? 1 : 2
```

Het tweede script leest deze JSONL-records:

```json
{"kind":"foul","zone":0,"score":0.5}
{"kind":"match","status":"Finished","Attempts":50,"InsideUnscaled":1.0,"OutsideUnscaled":20.0}
```

Extra recordvelden zijn toegestaan. Iedere foulrecord moet bij een meegetelde match horen. Instrumentatie moet op een aparte kopie/labbranch blijven. De gebruikte kopie en complete logs staan lokaal in bovengenoemde labmap; dit document maakt daarvan geen distributie-artifact.

| Parameter | Kwantielfit / begrensd voorstel |
|---|---:|
| RecklessOwnThird | 0.5051685694376448 |
| RecklessMiddle | 0.5556158641537382 |
| RecklessAttacking | 0.7289114591284318 |
| ExcessiveForceScore | 0.8759214464741588 |
| PenaltyAreaFoulFactor | 1 |
| AttemptRate | 0.08005129285133622 |

Dit zijn voorlopige voorstellen, geen bevestiging dat productiedefaults al zijn vervangen of gevalideerd. Kaartdrempels minimaliseren de afwijking tussen empirische scoreoverschrijding en het bronpercentage. Direct rood levert 4/1777 (0,2251%) tegen doel 0,2079%; minder dan vijf verwachte zeldzame gevallen, dus weinig precisie. De kaart/ratewijziging verandert later gedrag en daarmee de scoreverdeling. De gewone win/losse-bal/geen-uitkomstformule blijft een interne modelaanname; deze bronfit identificeert die verdeling niet.

### Penaltylimiet

Verwachte ongeschaalde foulmassa in het lab: binnen 19,64446, buiten 1755,27723. Voor bronratio r wordt de onbegrensde factor `r * buiten / (binnen * (1-r))` = 1,11688. Dat overschrijdt het toegestane maximum 1. De begrensde beste factor is dus 1: geen extra onderdrukking in het penaltygebied. De verwachte penalty/foulratio blijft dan 1,10678%, onder het voorlopige doel 1,23454%. Exacte aansluiting is met alleen deze factor in deze steekproef niet mogelijk. Forceer geen nieuwe gebiedsmechaniek om het gemiddelde passend te maken; rapporteer de afwijking voor de beta. De oude factor 0,2 is hiermee niet onafhankelijk herleid.

AttemptRate schaalt de verwachte foulmassa bij de begrensde factor naar 12,143998 contactfouls/team. Een afgeronde productiekandidaat 0,08 moet opnieuw worden gemeten; dit document claimt daarvoor geen uitslag.

## Overige eigen parameters en gezamenlijke eindtoets

`docs/kalibratie.md` toont dat eigen uitvoeringsparameters op de oude referentie zijn afgestemd. Dat maakt ze geen rechtstreeks overgenomen regressiecoëfficiënten, maar hun geschiedenis mag niet worden weggepoetst:

- `ShotModel` bevat vier directe fitcoëfficiënten. Vervang deze door de Wyscout-fit uit `calibration/wyscout_models.json` met dezelfde features/scaling: const -0,22363, afstand -0,18228, afstand²/100 0,16676, hoek 1,19441. Dit beïnvloedt beslissingen; de balfysica bepaalt nog steeds de werkelijke uitkomst.
- Keeperbereik `(2.5 m, 2.2 m, 35 m/s)` in `MatchConfig.KeeperSettings` en schotruis/snelheid in `ShotConfig` zijn eigen uitvoeringskeuzen die samen aan oude schotconversie zijn getuned. Toets de definitieve combinatie opnieuw tegen Wyscout conversie per afstand en totaal, met genoeg schoten per vak; documenteer lege/kleine vakken. De bron verschilt in shotdefinitie en bevat onder meer niet-doelgerichte pogingen. Vergelijk overeenkomstige voet-/open-playcategorieën waar mogelijk, en benoem ieder definitieverschil.
- `PassConfig` houdtijd, hoek-/snelheidsruis, baanrisico/onderschepinstellingen, `KickShieldMeters` en `InterceptSettings` zijn eigen uitvoeringsparameters met oude tellingen als meetlat. Toets dezelfde uiteindelijke code opnieuw op passes/team en passsucces, per tactiekpaar en niveau, tegen Wyscout-aggregaten. Gebruik Wyscout werkelijke passlengte/eindpunt niet als kanskenmerk vóór de trap: dat lekt uitkomsten. De lokale nieuwe Wyscout-passfit vermijdt juist lengte/eindpunt.
- Duelpogingen, kaartkwantielen, keeperbereik, passruis en schotruis zijn onderling afhankelijk. Gelijke getallen mogen alleen blijven met een nieuwe expliciete meting, geen louter gewijzigde broncomment. Een andere dataset bevestigt ook geen fysische realiteit.

Minimale vervolgstap: private kandidaat met nieuwe schotfit + bovenstaande duelwaarden + definitieve niveaucorrectie op held-out seeds 31+ draaien. Meet actual fouls, geel, tweede geel, direct rood, penalties, schoten/doelpunten en passes, per tactiekpaar en zowel zonder squads als amateur/halfprof/prof; controleer Finished/Abandoned. Vergelijk met de gekozen Wyscout-definities en leg afwijkingen vast. Daarna pas promotie naar productiedefaults en nieuwe deterministische verwachtingen. Dit rapport bewijst de bronhertelling en het kwantielvoorstel; het bewijst nog niet de gecombineerde eindvalidatie of volledige vrijgave van alle productherkomst.
