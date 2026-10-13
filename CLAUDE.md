# Zeus online – code-afspraken

Tactisch voetbalspel met een vrije tactiekeditor en een simulatie. Het ontwerp, de besluiten en de roadmap staan in de Obsidian-vault, niet hier:
`C:\Users\lth\OneDrive - Christelijk College Groevenbeek\Documenten\Zeus online` (lees daar `01 Ontwerp/PRD.md`, `02 Techniek/Techniek overzicht.md` en `04 Planning/Roadmap.md`).

## Status
Fase 0 (fundament, ontwerp, wegwerpproef). Geen spelcode vóór de ontwerpnotities af zijn, behalve de wegwerpproef (WP8). Engine is nog **niet** gekozen: voorstel is C#/.NET-kern + Godot 4 + Python voor data.

## Afspraken
- Taal: Nederlands in uitleg, commits en notities; code en identifiers in het Engels.
- Taken zijn klein (1–3 uur) en komen met een test of een zichtbaar resultaat.
- Simulatie is los van weergave; geen spellogica in de engine. Determinisme: zelfde seed + build = zelfde wedstrijd.
- Coördinaten in meters, oorsprong in het middelpunt, x in aanvalsrichting.
- Download geen databronnen zonder licentie te noteren in de vault (`Licenties en Steam`).
- Zet geen grote data of secrets in git (`data/` is genegeerd).
- Leg besluiten vast in de vault (`01 Ontwerp/Besluiten.md`), sluit sessies af met een sessielog in `05 Sessielogs/`.
- Dit is een werklaptop die privé gebruikt mag worden: werk alleen buiten werktijd en buiten OneDrive.

## Mappen
- `notebooks/` – Python-verkenning van data (fase 0, WP2)
- `data/` – lokale data, niet in git
- `docs/` – korte technische uitleg per module
