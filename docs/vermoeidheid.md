# Vermoeidheid v0

Dit pure model volgt het voorstel `Spelersmodel v0` in de vault: twee energiereservoirs tussen 0 en 1, met 1 = fris en `ShortEnergy <= LongEnergy`. Alle coëfficiënten zijn expliciete spelaannames; het model is nog niet empirisch gekalibreerd. De kern (`Fatigue.cs`: `Step`, `Apply`, `ReactionExtra`) is een pure functie: ze verandert zelf geen wedstrijdtoestand en gebruikt geen toeval. De wedstrijdadapter (`Match.Fatigue.cs`, zie Wedstrijdadapter hieronder) koppelt het model wel aan `Match` (standaard aan via `EnableFatigue`): hij bewaart de reservoirs per speler, past bewegingsprofiel en beslisvertraging aan en voedt de duelvermoeidheid.

`Fatigue.Step(state, speed, pressing, endurance, dt, config)` rekent met simulatieseconden. Snelheid is een magnitude in m/s; negatieve waarden worden nul. Uithoudingsvermogen wordt begrensd tot 0–1. Ongeldige reservoirs worden begrensd; niet-eindige invoer en negatieve duur worden geweigerd.

## Voorlopige rates

De verbruiksfactor is `u = 0.7 + 0.6 * endurance`. Standaardwaarden:

- Kort sprintverbruik: `0.008 * max(0, speed - 5.5) / u` energie per seconde.
- Kort drukzetverbruik: `0.002 / u` energie per seconde zolang `pressing` waar is.
- Kort herstel: `0.012 * u` energie per seconde bij snelheid < 2 m/s en geen actieve pressing; herstel stopt bij de langetermijnenergie.
- Lang verbruik: `(0.000017 * speed + 0.000025 * pressing) / u` energie per seconde. Dit reservoir herstelt tijdens de wedstrijd niet.

Bij gemiddeld 2,2 m/s over 90 minuten en endurance 0,5 blijft ongeveer 0,798 langetermijnenergie over. Continu 7 m/s zonder pressing kost in 45 seconden ongeveer 0,54 kortetermijnenergie. Dit zijn gewenste modeluitkomsten voor playtests, geen claims over gemeten voetbalprestaties. De extra kortetermijnkosten van pressing en het onderdrukken van herstel tijdens pressing zijn uitbreidingen op het voorlopige vaultvoorstel.

Alle rates/drempels/effecten staan in `FatigueConfig`. Een stap veronderstelt constante belasting; wisselende snelheid/pressing wordt per simulatiestap aangeboden. Afkappen op nul en op het langetermijnreservoir voorkomt ongeldige energie.

## Effecten

`Fatigue.Apply(profile, state, config)` maakt een nieuw bewegingsprofiel. MaxSpeed wordt standaard vermenigvuldigd met `0.85 + 0.15 * ShortEnergy`. Acceleratie blijft standaard gelijk; een configureerbare ondergrens kan later een acceleratie-effect toevoegen. Remvermogen en draaisnelheid blijven gelijk.

`Fatigue.ReactionExtra(state, config)` levert standaard `0.15 * (1 - LongEnergy)` seconden extra reactietijd. Het pure model schrijft zelf geen beslisklok of intentie. Spreiding en positiediscipline uit het vaultvoorstel zijn nog niet geïntegreerd.

## Wedstrijdadapter

`Match.Fatigue.cs` biedt `FatigueOf(team, id)` en interne helpers voor de vaste stap. Energieverbruik gebruikt de actuele snelheid en `Press`-intentie. Tijdens de pauze vóór een spelhervatting wordt de belasting als rustig staan behandeld. Uitgesloten spelers worden overgeslagen. In de rust wordt kortetermijnenergie gelijk aan de bestaande langetermijnenergie; lange energie wordt niet bijgevuld.

Het bewegingsprofiel wordt per speler afgeleid en de extra reactietijd wordt omgezet naar gehele simulatieticks met afronding naar de dichtstbijzijnde tick (halve ticks van nul af). `EnableFatigue=false` bewaart frisse reservoirs en het basisprofiel zonder extra beslisvertraging; `Endurance` en `FatigueSettings` komen uit MatchConfig. Match roept de helpers aan, snapshots bevatten `ShortEnergy`/`LongEnergy` en duels gebruiken `1 - ShortEnergy` als vermoeidheid van de verdediger. Het halve-tijdherstel vult geen lange energie bij.

De Match-tests controleren verbruik, uitschakelen, herstel bij een hervattingspauze, rustherstel, uithoudingsvermogen, determinisme, extra beslisvertraging en de daadwerkelijke lagere bewegingssnelheid plus energie in snapshots.
