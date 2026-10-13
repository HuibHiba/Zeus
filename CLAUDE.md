# Zeus online – code-afspraken

Tactisch voetbalspel met een vrije tactiekeditor en een simulatie. Het ontwerp, de besluiten en de roadmap staan in de Obsidian-vault, niet hier:
`C:\Users\lth\OneDrive - Christelijk College Groevenbeek\Documenten\Zeus online` (lees daar `01 Ontwerp/PRD.md`, `02 Techniek/Techniek overzicht.md` en `04 Planning/Roadmap.md`).

## Status
Stand, testaantal en laatste commit staan alleen in `docs/hervatten.md` (enige bron van waarheid; neem hier geen aantallen over). Kort: fase 1a, regelbasis 1b en de koppeling van fase 1c (vermoeidheid, teamfases, blokwissels) zijn gebouwd; de zes triggervoorwaarden worden uitgevoerd (v0-aannames, `docs/blokwissels.md`); aantallen zijn gekalibreerd op een symmetrisch en een ongelijk tactiekpaar (`docs/kalibratie.md`), realisme en bijzondere regels zijn open. Engine besloten: C#/.NET 8 voor de simulatiekern, Godot 4.7 (.NET) voor weergave en editor, Python voor data. De code in `proto/` is wegwerp. Zie ook `docs/waarneming.md` en `Fase 1a taken` in de vault.

## Afspraken
- Taal: Nederlands in uitleg, commits en notities; code en identifiers in het Engels.
- Taken zijn klein (1–3 uur) en komen met een test of een zichtbaar resultaat.
- Simulatie is los van weergave; geen spellogica in de engine. Determinisme: zelfde seed + zelfde build + zelfde platform/runtime = zelfde wedstrijd (niet gegarandeerd op een andere CPU/OS/runtime; zie `docs/hervatten.md`).
- Coördinaten in meters, oorsprong in het middelpunt, x in aanvalsrichting.
- Download geen databronnen zonder licentie te noteren in de vault (`Licenties en Steam`).
- Zet geen grote data of secrets in git (`data/` is genegeerd).
- Leg besluiten vast in de vault (`01 Ontwerp/Besluiten.md`), sluit sessies af met een sessielog in `05 Sessielogs/`.
- Dit is een werklaptop die privé gebruikt mag worden: werk alleen buiten werktijd en buiten OneDrive.

## Mappen
- `notebooks/` – Python-verkenning van data (fase 0, WP2)
- `data/` – lokale data, niet in git
- `docs/` – korte technische uitleg per module
