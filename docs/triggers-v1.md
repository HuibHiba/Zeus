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

De editor biedt alle acht voorwaarden (minder aanbieden zou bestaande tactieken en de autotest breken). Voor v1 zijn dit de vijf die we aanraden en per stuk testen in een echte wedstrijd (`TriggersV1Tests`):

| Voorwaarde | Typisch gebruik |
| --- | --- |
| `balverlies_in_eigen_helft` (fase balverlies) | tegenpressing na balverlies |
| `balwinst_in_hoge_zone` (fase balwinst) | na hoge balwinst een loper het pad laten lopen |
| `tegenstander_heeft_bal_in_zone` (verdedigen, zone) | "bal bij hun linksback → rechtsbuiten zet druk" |
| `tegenstander_ontvangt_op_flank` (verdedigen) | druk op de flank |
| `bal_terug_naar_middenvelder` (verdedigen) | doorschuiven op een terugspeelbal |

`bal_naar_keeper`, `slechte_aanname` en `achterstand_na_minuut_70` blijven beschikbaar en hebben hun eigen eenheidstests in `TriggerTests`. "Bij voorsprong lager blok" is géén trigger maar een **blokwissel** (zelfde tab, onder de regels).

## Zo maak je een regel (stappen)

Tactiekmaker open → tab REGELS → (naam optioneel) → voorwaarde kiezen (fase springt mee) → eventueel zone → speler → instructie → "+ Regel toevoegen" → Opslaan.
Dat is 1 tab-klik + 3 keuzelijsten (2 klikken elk) + 1 klik toevoegen + 1 klik opslaan = **9 klikken**, met zone 11. Het formulier staat onder de regellijst en kan bij 1100×620 onder de rand vallen; dan is één keer scrollen nodig.
Effect zien: Menu → Meter → A = deze tactiek, vinkje "B = A zonder regels" → Vergelijk = 4-5 klikken, daarna wachten op 20 wedstrijden.

Schatting: het maken zelf kost een geoefende maker ~30-60 s, een nieuwe maker die de voorwaarden leest waarschijnlijk 1-2 min. Niet gemeten met een echte gebruiker; de echte test doet de maker. De meterrun zelf (20 volledige wedstrijden) telt niet mee in de 2 minuten en duurt langer.

## Beperkingen

- Eén actie per regel in het formulier (JSON mag meer).
- De meter laat zien dát en hoe vaak een regel afgaat en wat er verschuift; of dat verschil buiten de ruis valt (n = 20) wordt niet getoetst.
