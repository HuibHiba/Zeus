# Blokwissels: pure v0-selectie

`BlockSelection.Select(Tactic tactic, ScoreState score, int minute, double fatigue)` kiest alleen een `BlockHeight?`. De functie verandert geen wedstrijd, spelersposities, tactiek of RNG-state. De integratie moet voor het team de juiste scoretoestand, wedstrijdminuut en vermoeidheid leveren, en daarna de gekozen verdedigingsvariant gebruiken.

Begin bij `tactic.Phase(GamePhase.Verdedigen).Block`. Alle aanwezige voorwaarden binnen één blokwissel zijn conjunctief:

- `Score` moet gelijk zijn aan de teamrelatieve stand (`Voor`, `Gelijk`, `Achter`).
- `FromMinute` geldt vanaf de opgegeven minuut, inclusief die grens.
- `FatigueAbove` geldt strikt boven de grens; gelijk aan de grens voldoet niet.

Doorloop de regels in de opgegeven volgorde: **de laatste passende regel wint**. Dit is een expliciete gekozen v0-aanname, geen bewezen trainersvoorkeur. Iedere aanroep begint opnieuw bij het basisblok: er is geen geheugen of blijvende omschakeling wanneer een voorwaarde later vervalt. Als geen regel past, blijft het basisblok behouden, ook wanneer dat `null` is.

`fatigue` is een eindige waarde binnen [0,1], bedoeld als teamgemiddelde vermoeidheid uit langdurig energieverlies. De selector bepaalt die waarde niet. `minute` is niet-negatief; de integratie bepaalt hoe speeltijd en blessuretijd op de wedstrijdminuut worden afgebeeld. Er wordt niet automatisch op 90 minuten afgekapt.

Een doelblok van een blokwissel moet aanwezig zijn in `DefendVariants`. Een onbekend/ontbrekend doel wordt een expliciete fout, geen stilzwijgend genegeerde trainersinstructie. De loader controleert dit al voor JSON; de selector beschermt ook rechtstreeks gemaakte `Tactic`-records. Ongeldige enums, negatieve minuutgrenzen en niet-eindige of buiten [0,1] liggende vermoeidheidsgrenzen worden eveneens geweigerd. De bestaande loader controleert nog niet al die conditionele grenzen.

## Triggers nog niet uitvoeren

In de aanwezige tactieken staan drie conditieteksten. Dit zijn momenteel strings; hun fysieke waarneming en uitvoeringssemantiek zijn nog niet vastgelegd:

| Conditie | Benodigde semantiek vóór uitvoering |
| --- | --- |
| `bal_naar_keeper` | Een waargenomen pass naar de keeper van de tegenstander onderscheiden van algemeen balbezit bij een keeper. Vastleggen of de trigger bij kick, aankomst of ontvangst geldt; alleen kennis gebruiken die de aangewezen presser kan zien/herinneren. |
| `slechte_aanname` | Een concrete slechte balcontrole detecteren met aparte uitvoeringsfeiten, bijvoorbeeld een te grote eerste-aannameafstand of verlies van controle. Een gewone BallControl is geen bewijs van een slechte aanname; de huidige perfecte contactcontrole levert dit feit nog niet. |
| `tegenstander_ontvangt_op_flank` | Ontvangst door de tegenstander en een expliciete, teamrelatieve flankzone definiëren. Bepalen of links/rechts afzonderlijk geldt en welke breedtegrens meetelt, ook na helftwisseling. Geen ontvangst afleiden uit alleen een voorspelde passbestemming. |

Alle aanwezige acties zijn `start_pressing`, gericht op één of meerdere spelers. Nog nodig zijn prioriteit tegenover normale pressingselectie, duur/eindvoorwaarde, herhaling/cooldown, gelijktijdige triggers en de vereiste fase (`Verdedigen` in de huidige bestanden). Meerdere aangewezen pressers kunnen botsen met de huidige één-presserselectie. Optioneel `Trigger.Zone` wordt door de loader opgeslagen, maar zonebetekenis is niet door de loader gevalideerd. Onbekende conditie-/instructiestrings mogen daarom niet stilzwijgend een willekeurige interpretatie krijgen. Deze wijziging implementeert uitsluitend blokselectie, geen triggers.
