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

### Voorbeeldtactieken gerepareerd (alleen tactiek-JSON, geen simwijziging)

Gemeten zoals de meter rekent (seeds 1-10, beide kanten, 2×45 min, A tegen A zonder regels, n = 20). t = gemiddeld verschil A−B per wedstrijd gedeeld door de standaardfout; |t| ≥ 3 telt als "buiten de ruis".

| Tactiek | Regel vóór | Regelacties vóór | Regel na | Regelacties na | Meter na (A / B) |
| --- | --- | --- | --- | --- | --- |
| Vleugelspel 4-3-3 | balwinst hoog → RW/LW `loop_diep` (niet uitvoerbaar) | 0 | zij hebben de bal in zone M-C → RCM/LCM `start_pressing` | 181,8 | balcontacten 407,5 / 463,6 (t −3,1); passes 497,7 / 552,2 (t −3,0); onderscheppingen 68,5 / 73,6 (t −2,1) |
| Lange bal 4-4-2 | `balverlies_in_eigen_helft` in fase balwinst, RS `loop_diep` | 0 | zone M-C → RCM/LCM/RM/LM `start_pressing` | 375,2 | schoten 24,8 / 21,1 (t 3,2); onderscheppingen 73,9 / 79,6 (t −3,5) |
| Flankdruk 3-5-2 | flank → RWB/LWB `start_pressing` | 55,3 (alles gelijk) | zelfde regel + RCM/LCM | 107,3 | balcontacten 402,2 / 375,5 (t 3,8); passes 525,8 / 501,3 (t 3,5) |
| Hoge druk 4-3-3 | keeper krijgt bal → ST `start_pressing` | 16,4 (alles gelijk) | **ongewijzigd** | 16,4 | geen verschil |

Vóór waren alle cijfers van A en B bij deze vier gelijk. Controle op andere seeds: Vleugelspel onderscheppingen t −3,5 op seeds 1-20 én op 11-30 (balcontacten alleen −1,2 op 11-30: bij n = 20 deels ruis); Lange bal op seeds 1-20 schoten t 5,6; Flankdruk op seeds 11-30 balcontacten t 2,7 (zelfde richting). Niet elk verschil bij n = 20 houdt stand: een eerdere kandidaat (Vleugelspel, flankdruk door vleugels en backs) gaf op seeds 1-10 schoten t −3,0 en op seeds 11-30 +0,4. Daarom staat in de test alleen een verschil dat ook op een tweede seedset overeind bleef; voor Vleugelspel met 20 seeds (n = 40), omdat het robuuste verschil (onderscheppingen) bij de meter-n = 20 maar t −2,1 haalt.

Eerst kreeg Vleugelspel RCM/CM/LCM (onderscheppingen t −3,2 bij n = 20), maar dan won *Positiespel* in `StartMatchTests.DefaultMenuMatchIsNotLopsided` seed 13 met 6-1 (grens ≤ 6 doelpunten); niet verruimd, regel teruggebracht naar RCM/LCM.

*Hoge druk 4-3-3* niet aangepast: 17 kandidaatregels (druk door middenveld/aanval op flank, zone A-C/M-C/V-C, terugspeelbal, slechte aanname; `dek_ruimte_achter_lijn` voor achterlijn of middenveld; `kom_op`/`blijf_breed`/`dribbel_in` na balwinst hoog; `kom_op` bij achterstand na minuut 70) gaven bij n = 40 geen |t| boven 2. Waarschijnlijke reden: met pressing-intensiteit 0,95 drukt het team al, dus `start_pressing` voegt niets toe, en de aanvallende instructies werken maar 4 s. Een merkbare regel vraagt hier simwerk. Besluit (maker, 2026-10-19): zo laten; de regel blijft staan als voorbeeld, het effect in de meter is hier bewust nul. Laag blok en Man om man blijven ook ongewijzigd.

*Flankdruk 3-5-2* opnieuw gemeten op 24f2f48 (2026-10-27), met jockeying (`PressJockeyMeters` 4) en `CautionedAvoidsBehind` aan, seeds 1-60 beide benen (n = 120, regel vuurt 103,7 keer per wedstrijd), verschil A min B: balcontacten +7,74 (se 5,00, t 1,55), passes +6,43 (se 4,94, t 1,30), onderscheppingen -0,47 (t -0,67), overtredingen -0,71 (t -1,44), gele kaarten +0,21 (t 1,65), schoten +0,38 (t 0,62), doelpunten -0,22 (t -1,19). Geen enkele maat haalt |t| 3: het eerdere effect (t 3,8) is weg. Besluit: regel en tactiek ongewijzigd, geen test (zoals Hoge druk); het effect in de meter is hier nu nul. Niet verder getuned.

Ook gemeten, niet aangepast (niet gevraagd, en ze zitten in de goldens en tientallen tests): *Laag blok 4-4-2* (66,8 regelacties, geen |t| boven 1) en *Man om man 4-3-3* (0,3 regelacties: de keeper krijgt de bal zelden). *Counter 5-3-2* gaf schoten t −2,5.

Test: `TriggersV1Tests.ExampleTacticRuleFiresAndMovesTheMeter` speelt precies de meterrun (20 volledige wedstrijden per tactiek) en eist ≥ 10 regelacties per wedstrijd en t > 3 in de verwachte richting (Vleugelspel met 20 seeds). Duurt in Debug ~4,5 min voor de drie. Golden-hashes veranderen niet (die gebruiken alleen Laag blok en Man om man).

## Zo maak je een regel (stappen)

Tactiekmaker open → tab REGELS → voorwaarde kiezen (fase springt mee) → speler → instructie → "+ Regel toevoegen" → Opslaan. Naam, fase en zone staan onder de knop (optioneel; vóór toevoegen instellen), daaronder de lijst met regels.
Dat is 1 tab-klik + 3 keuzelijsten (2 klikken elk) + 1 klik toevoegen + 1 klik opslaan = **9 klikken**, met zone 11 (zone ligt onder de knop: bij 1100×620 één keer scrollen). Zonder zone is bij 1100×620 alles **zonder scrollen** bereikbaar, ook met een paar regels in de lijst (de lijst staat nu onder de knop). Bewerken: ✎ (het naamveld krijgt focus; het ligt onder de lijst en valt bij 1100×620 soms net onder de rand, typen werkt wel) → keuzes aanpassen → "✓ Wijziging regel opslaan" (staat boven de lijst, blijft in beeld).
Effect zien: Menu → Meter → A = deze tactiek, vinkje "B = A zonder regels" → Vergelijk = 4-5 klikken, daarna wachten op 20 wedstrijden.

Schatting: het maken zelf kost een geoefende maker ~30-60 s, een nieuwe maker die de voorwaarden leest waarschijnlijk 1-2 min. Niet gemeten met een echte gebruiker; de echte test doet de maker. De meterrun zelf (20 volledige wedstrijden) telt niet mee in de 2 minuten en duurt langer.

## Beperkingen

- Eén actie per regel in het formulier (JSON mag meer).
- De meter laat zien dát en hoe vaak een regel afgaat en wat er verschuift; of dat verschil buiten de ruis valt (n = 20) wordt in het spel niet getoetst. Kleine verschillen (2-5 %) zijn bij n = 20 vaak ruis, zie de voorbeeldtactieken hierboven.

## Controle

- Autotests `scripts/autotest/18b_triggers_v1_en.json` en `_nl.json` (venster 1100×620): regel toevoegen en de wijziging opslaan met een muisklik op de knop (de klik faalt als de knop buiten beeld valt, dus dit toetst 'zonder scrollen'), ✎, naam aanpassen, opslaan op dezelfde plek, meter met "B = A zonder regels" en de regelactieregel. Screenshots lokaal in `.tmp_autotest/18b_triggers_v1_*/` (niet gecommit). NL draait de meter met helften van 15 min, EN met 90 s.
- Uitslag (2026-10-19, deze tak): sim-suite 892/893, de ene mislukte is de bekende timingtest `NinetyMinutesRunInUnderTenSeconds` (los gedraaid groen); Godot-tests 77/77; `smoke_godot.py` ALLES SCHOON; `run_autotest.py` alles OK behalve `10a` (bewust rood) en bij de volledige run `12b`, `12c`, `13a`, `2_library` (los herhaald alle vier OK; 12c/13a/2_library alleen 'resources still in use at exit').
- Uitslag na voorbeeldregels en compacter formulier (2026-10-19): sim-suite 895/896 met de eerste Vleugelspel-regel; de mislukte was `StartMatchTests.DefaultMenuMatchIsNotLopsided` (zie boven, regel aangepast). Daarna opnieuw groen: de drie `ExampleTacticRuleFiresAndMovesTheMeter`, `ChallengeTests`, `SmartOpponentTests`, `StartMatchTests`, `TriggersV1Tests` (42/42), `LocCoverageTests`. De volledige suite is na die laatste regelwijziging niet opnieuw gedraaid (35 min). Godot-tests 77/77; `smoke_godot.py` ALLES SCHOON; autotests `12a`, `18b_triggers_v1_nl`, `18b_triggers_v1_en` OK (screenshots NL en EN bekeken).
