# Teamspelfases — configureerbare v0

`TeamPhases` is een pure, deterministische toestandsmachine voor de zes bestaande `GamePhase`-waarden. De klasse gebruikt geen RNG en leest geen tactiekinstructies. De standaard overgangsduur van 3 seconden en grens van .66 zijn expliciete spelmodelaannames; ze zijn niet uit voetbalonderzoek afgeleid.

## API

```csharp
var phases = new TeamPhases(initialPossession: Side.Home,
    transitionSeconds: 3, attackThirdThreshold: .66);
IReadOnlyList<PhaseChange> changes = phases.Update(possession, stopped,
    ballHomeRelativeX, timeSeconds);
GamePhase homePhase = phases.PhaseOf(Side.Home);
```

`ballHomeRelativeX` loopt van 0 bij het eigen doel van Home tot 1 bij het doel dat Home aanvalt. De wedstrijd zet wereldcoördinaten om en spiegelt wanneer de speelrichting verandert. Away gebruikt `1 - ballHomeRelativeX`. De klok is verstreken simulatietijd, eindig en monotoon; gelijke tijden mogen. Invoer buiten het geldige domein wordt vóór mutatie afgewezen.

## Overgangen

- De constructor zet de initiële bezitter op `Opbouw`, de tegenstander op `Verdedigen`.
- Zonder overgangsvenster krijgt de bezitter `Aanval` als de bal in diens aanvalrichting minstens de ingestelde grens heeft bereikt, anders `Opbouw`. De tegenstander blijft `Verdedigen`.
- Een bezitwissel tijdens doorgaand spel geeft de nieuwe bezitter `Balwinst`, de oude `Balverlies`. Elke volgende wissel start een nieuw venster voor beide teams. Bij de exacte eindtijd wordt de baseline uit balplek/bezit gekozen. Een duur van 0 slaat het venster over.
- Bij stilgelegd spel krijgen beide teams `Stilstaand`. Een stop beëindigt het overgangsvenster. Bezitwijzigingen tijdens een stop worden bijgehouden zonder gefingeerde balwinst. Bij hervatting kiest de machine meteen de baseline uit actuele bezitter en balplek; ook een korte stop onderbreekt dus het venster.
- Alleen feitelijke faseveranderingen leveren events. Als beide teams veranderen, staat Home altijd eerst. `PhaseChange` bevat team, vorige fase, nieuwe fase en reden: `possession_changed`, `transition_expired`, `ball_zone`, `stopped` of `resumed`.

## Grenzen

De machine veronderstelt een bekende huidige/laatste bezitter; een losse bal is geen extra fase. De caller bepaalt wat een daadwerkelijke bezitwissel is. Geen pressing-, compactheids- of tactiekgedrag wordt door deze klasse verondersteld: zij selecteert uitsluitend fases. Tactiekposities en gedrag worden elders aan de geselecteerde fase gekoppeld.

Tests dekken beide aanvalrichtingen, grensgelijkheid, bezitwissels, herhaalde steals, exacte afloop, stoppen/hervatten, bezitwijziging tijdens stilstand, lange stops, nulduur, configureerbare grenzen, Home-eerst-volgorde en validatie zonder gedeeltelijke mutatie.
