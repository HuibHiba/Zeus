# Regels voor de nachtwerker (Zeus online)

Je werkt zelfstandig, 's nachts, zonder dat iemand je vragen kan beantwoorden. Los dingen zelf op, kies de eenvoudigste
redelijke oplossing en schrijf je keuzes op. Stel nooit een vraag aan de gebruiker.

## Context
- Repo `C:\dev\zeus` (branch `main`). Lees eerst `CLAUDE.md`, `docs/hervatten.md`, `docs/kalibratie.md`, `docs/waarneming.md`
  en `docs/blokwissels.md`. De simulatiekern is C# (.NET 8) in `sim/Zeus.Sim`, tests in `sim/Zeus.Sim.Tests`, runner in
  `sim/Zeus.Runner`, Godot-weergave in `godot/`. Documentatie en commitberichten zijn Nederlands, code en namen Engels.
- Schrijf code die past bij de omringende code: zelfde stijl, dezelfde hoeveelheid commentaar, zelfde naamgeving.

## Harde regels
1. **Determinisme.** Alleen de geseede `Rng` van de wedstrijd. Geen `System.Random`, geen `DateTime`/klok, geen parallel rekenen,
   geen iteratie over `HashSet`/`Dictionary` die de uitkomst bepaalt. Trek `Rng`-waarden in een vaste volgorde.
2. **Een brein ziet alleen zijn `Perception`.** Geen toegang tot `Match` of de volledige wereld. Uitkomsten volgen uit de fysica
   en contactdetectie; geen tweede kansworp bovenop een uitkomst.
3. **Nieuwe instellingen** komen in een config-record met een default en een `Validate()`-controle, en worden in tests gedekt.
4. **Tests.** Draai `dotnet test Zeus.sln` en zorg dat alles slaagt. Verwijder of verzwak nooit een bestaande test om iets groen
   te krijgen. Voeg tests toe voor nieuw gedrag (xUnit, in de stijl van de bestaande tests).
5. **Golden-hashes.** `sim/Zeus.Sim.Tests/GoldenMatchTests.cs` bevat de hashes van twee volledige wedstrijden. Verandert je werk het
   wedstrijdgedrag bewust, werk ze dan bij: draai
   `dotnet run --no-build --project sim/Zeus.Runner -c Release -- --home tactics/laag_blok_442.json --away tactics/man_om_man_433.json --seed 42 --until-finished --out nacht/tmp/o.jsonl`
   (bouw eerst: `dotnet build sim/Zeus.Runner -c Release`) en daarna hetzelfde met `--away tactics/laag_blok_442.json --seed 7`; neem de regels `event hash` en `snapshot hash` over in de test. Zeg in je eindverslag dat je dit deed.
6. **Blijf binnen je scope** (staat bij de taak). Wijzig geen bestanden daarbuiten. Een andere sessie werkt tegelijk in `scripts/`:
   raak daar alleen aan wat in je scope staat.
7. **Git.** Commit en push NIET: het nachtscript doet dat nadat het jouw werk heeft gecontroleerd. Gebruik nooit `git add -A`,
   `git stash`, `git reset`, `git checkout` of `git clean`.
8. **Eerlijk meten.** Rapporteer gemeten cijfers zoals ze zijn. Haal een kalibratiedoel nooit met een truc (andere telling,
   gemanipuleerde meting). Lukt een doel niet, zeg dat dan en beschrijf wat je wel zag.
9. **Documenteren.** Leg elke aanname als v0-aanname vast in de juiste doc (Nederlands), met de parameternamen. Neem geen
   testaantallen of commit-hashes over in docs; `docs/hervatten.md` wordt aan het eind door een aparte taak bijgewerkt.

## Meten (alleen als je taak dat vraagt)
- `.venv\Scripts\python.exe scripts\calibration_run.py --seeds 8 --label naam --home tactics/laag_blok_442.json --away tactics/laag_blok_442.json`
  is het symmetrische referentiepaar; het ongelijke paar is `--away tactics/man_om_man_433.json`.
  Met `--config bestand.json` kun je gedeeltelijke `MatchConfig`-overrides meegeven (zie `docs/kalibratie.md`). Bouw eerst:
  `dotnet build sim/Zeus.Runner -c Release`.
- Doelen per team per wedstrijd (PL 2015/16): passes 485, pass geslaagd 79%, onderschept 19,5%, uit 2%, duelpogingen 33,
  overtredingen 12,5, geel 1,58, schoten 13, doelpunten 1,3, buitenspel 1,8, inworpen 23, corners 5,4.

## Eindverslag
Sluit af met een kort verslag in het Nederlands (max. 25 regels): wat je veranderde, welke parameters en defaults, wat je mat
(voor en na), wat niet lukte of open blijft, en welke golden-hashes je bijwerkte.
