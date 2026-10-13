# Vermoeidheid v0

Dit pure model volgt het voorstel `Spelersmodel v0` in de vault: twee energiereservoirs tussen 0 en 1, met 1 = fris en `ShortEnergy <= LongEnergy`. Alle coëfficiënten zijn expliciete spelaannames; het model is nog niet empirisch gekalibreerd. Het verandert geen wedstrijdtoestand en gebruikt geen toeval.

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

`Fatigue.ReactionExtra(state, config)` levert standaard `0.15 * (1 - LongEnergy)` seconden extra reactietijd. Het model schrijft zelf geen beslisklok of intentie. Spreiding en positiediscipline uit het vaultvoorstel zijn nog niet geïntegreerd. Match/snapshots passen deze pure voorbereiding nog niet automatisch toe.
