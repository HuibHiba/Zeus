# Triggers v1 in de tactiekmaker (2027-Q1, spoor C)

Maatstaf (meerjarenplan): een trigger is in ≤2 minuten te maken en het effect is te zien in de meter. Werking per voorwaarde en sim-details: [triggers.md](triggers.md) en [blokwissels.md](blokwissels.md). Dit document beschrijft alleen de bediening.

## Wat er al was (branch `feat/triggers-v1`, zit in deze tak)

- Datamodel en sim: acht voorwaarden, actie = instructie of "getekend pad", zones, toestandsregels. Standaard gedrag en hashes ongewijzigd.
- Tactiekmaker, tab REGELS: formulier (naam, fase, voorwaarde met uitleg, zone, speler, instructie) en een lijst met ✕ om te verwijderen. Opslaan gaat mee met de tactiek (zelfde JSON-veld `triggers`).

## Wat v1 toevoegt

1. **Bewerken**: elke regel in de lijst heeft een ✎-knop. Die zet de regel in het formulier; de knop eronder heet dan "✓ Wijziging regel opslaan" en vervangt de regel op dezelfde plek (volgorde blijft). Extra acties die alleen in JSON staan blijven behouden; het formulier toont de eerste actie.
2. **Meter met/zonder regels**: vinkje "B = A zonder regels" onder de teamkeuze. Team B is dan tactiek A met alle regels weggehaald (`Tactic.WithoutTriggers()`), dus je hoeft geen tweede bestand op te slaan. De uitslag toont onder de kaarten "Regelacties per wedstrijd: A x / B y" (aantal `TriggerFired`-events), zodat je ziet óf een regel afgaat, naast de gewone verschiltabel.

Geen nieuwe simlogica, geen nieuw bestandsformaat: oude tactieken zonder `triggers` laden zoals voorheen, en wie alle regels verwijdert krijgt weer een bestand zonder dat veld. Delen/import/export gebruikt dezelfde JSON en verandert niet.

## Triggertypes voor v1

De editor biedt alle acht voorwaarden (minder aanbieden zou bestaande tactieken en de autotest breken). Vijf ervan raden we aan voor v1; per stuk bewijst `TriggersV1Tests.EditorMadeRuleFiresAndChangesAFixedMatch` dat een in de editor gemaakte regel in een vaste wedstrijd afgaat **en het spel verandert** (snapshothash, niet alleen extra `TriggerFired`-events):

| Voorwaarde (fase) | Typisch gebruik |
| --- | --- |
| `balverlies_in_eigen_helft` (balverlies) | tegenpressing na balverlies |
| `tegenstander_heeft_bal_in_zone` (verdedigen, zone) | "bal bij hun linksback → rechtsbuiten zet druk" |
| `tegenstander_ontvangt_op_flank` (verdedigen) | druk op de flank |
| `bal_terug_naar_middenvelder` (verdedigen) | doorschuiven op een terugspeelbal |
| `slechte_aanname` (verdedigen) | druk zetten op een slechte aanname |

Niet bij de vijf: `balwinst_in_hoge_zone` gaat af, maar veranderde het spel niet met `start_pressing` of `kom_op` (pressing stopt bij eigen bezit); `bal_naar_keeper` ging in vier wedstrijden van 50 min tegen het laag blok niet af; `achterstand_na_minuut_70` vraagt > 70 min en heeft eenheidstests. "Bij voorsprong lager blok" is géén trigger maar een **blokwissel** (zelfde tab, onder de regels).

**Instructielijst ingeperkt.** De sim voert een regel alleen uit met de instructies in `TriggerRules.SupportedInstructions`; met een andere (bv. `loop_diep`) blijft de hele regel stil. De editor bood die toch aan. Nu toont de lijst alleen uitvoerbare instructies plus "getekend pad" (zelfde set, nu één bron; geen gedragswijziging).

Gevonden, niet aangepast (simgedrag/hashes): de voorbeeldtactieken *Vleugelspel 4-3-3* en *Lange bal 4-4-2* hebben een regel met `loop_diep` (en Lange bal ook een voorwaarde in de verkeerde fase) die dus nooit afgaat. Bij *Flankdruk 3-5-2* en *Hoge druk 4-3-3* gaat de regel wel af, maar in drie wedstrijden van 2×15 min was het spel identiek met en zonder regel. De meter laat dat eerlijk zien ("Regelacties per wedstrijd: A 19,4 / B 0,0" met gelijke cijfers).

## Zo maak je een regel (stappen)

Tactiekmaker open → tab REGELS → (naam optioneel) → voorwaarde kiezen (fase springt mee) → eventueel zone → speler → instructie → "+ Regel toevoegen" → Opslaan.
Dat is 1 tab-klik + 3 keuzelijsten (2 klikken elk) + 1 klik toevoegen + 1 klik opslaan = **9 klikken**, met zone 11. Bij 1100×620 vallen speler, instructie en de toevoegknop onder de rand: één keer scrollen (of Tab vanaf het naamveld). Bewerken: ✎ (het naamveld krijgt focus) → keuzes aanpassen → "✓ Wijziging regel opslaan".
Effect zien: Menu → Meter → A = deze tactiek, vinkje "B = A zonder regels" → Vergelijk = 4-5 klikken, daarna wachten op 20 wedstrijden.

Schatting: het maken zelf kost een geoefende maker ~30-60 s, een nieuwe maker die de voorwaarden leest waarschijnlijk 1-2 min. Niet gemeten met een echte gebruiker; de echte test doet de maker. De meterrun zelf (20 volledige wedstrijden) telt niet mee in de 2 minuten en duurt langer.

## Beperkingen

- Eén actie per regel in het formulier (JSON mag meer).
- De meter laat zien dát en hoe vaak een regel afgaat en wat er verschuift; of dat verschil buiten de ruis valt (n = 20) wordt niet getoetst.

## Controle

- Autotests `scripts/autotest/18b_triggers_v1_en.json` en `_nl.json` (venster 1100×620): regel toevoegen, ✎, naam aanpassen, opslaan op dezelfde plek, meter met "B = A zonder regels" en de regelactieregel. Screenshots lokaal in `.tmp_autotest/18b_triggers_v1_*/` (niet gecommit). NL draait de meter met helften van 15 min, EN met 90 s.
- Uitslag (2026-10-19, deze tak): sim-suite 892/893, de ene mislukte is de bekende timingtest `NinetyMinutesRunInUnderTenSeconds` (los gedraaid groen); Godot-tests 77/77; `smoke_godot.py` ALLES SCHOON; `run_autotest.py` alles OK behalve `10a` (bewust rood) en bij de volledige run `12b`, `12c`, `13a`, `2_library` (los herhaald alle vier OK; 12c/13a/2_library alleen 'resources still in use at exit').
