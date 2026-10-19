# Publiek (E1)

Dynamisch publiek, ook in 2D (roadmap E1). `godot/CrowdModel.cs` (puur C#, sim-tijd, deterministisch) levert per sim-tick een intensiteit en one-shots; `AudioManager` mengt de bedden en speelt de one-shots via de bestaande `Sfx`-pool. Het thuispubliek hoort bij `Side.Home`.

## Model

- `Intensity = clamp(Base + 0.3 * Danger + Excite, 0, 1)`
- `Base(stand, minuut) = 0,2 + 0,1 * min(minuut/90, 1)`, plus `0,2 * clamp((minuut - 75)/15, 0, 1)` als het verschil <= 1 doelpunt is. 0-0 in minuut 10: 0,21; 2-2 in minuut 88: 0,49.
- `Excite`: prikkel `max(Excite, impuls)`, daarna `Excite *= exp(-dt/1,3)` (onder 5% na 4 s simtijd). Impuls: goal 1,0; penalty 0,6; schot en redding 0,5; gemist schot 0,4; kaart 0,4; einde helft of wedstrijd 0,4; corner 0,35; foul 0,2; wissel en aftrap 0,2; buitenspel 0,15.
- `Danger`: bal in het laatste derde (|x| >= 17,5 m); `1 - afstand tot het doelmidden/35`, geklemd. Aanvallend = `Ball.Pos.X * (thuis ? 1 : -1) * (EndsSwapped ? -1 : 1) > 0`; een uitaanval telt voor 0,6. Gladgestreken met tijdconstante 0,8 s.

| Reactie | Trigger | Geluid | Min. tussenpoos (simtijd) |
|---|---|---|---|
| Ooh | Save (elk team) | crowd_ooh | 1,5 s |
| Sigh | thuisschot, daarna GoalKick binnen 5 s | crowd_sigh | 3 s |
| Cheer | Goal thuis | goal_cheer | 5 s |
| Groan | Goal uit | crowd_groan | 5 s |
| Whistles | Card tegen/door thuis, foul op een thuisspeler, buitenspel thuis | crowd_whistles | 3 s |
| Applause | Substitution, KickOff, HalfTime, FullTime | crowd_applause | 4 s |
| Anticipation | Corner, penalty | crowd_anticipation | 4 s |

De goal-cheer komt nu van het model (niet meer van `AudioManager.Match`). Boven 8x speelt `AudioManager.CrowdReaction` niets; de bedden blijven. Bij END (overslaan) en in de hoogtepuntenreeks wordt het model niet gevoed.

## Bedden en instelling

`crowd_low` (gemompel, intensiteit 0), `crowd_mid` (0,5) en `crowd_high` (brul, 1): driehoekige crossfade, 10 s naadloze lussen. Volume `crowd_volume` (0-100, standaard 50) in Instellingen > Audio (`settings.crowd_volume` in en.json en nl.json); geen aparte Godot-bus, het volume wordt in `AudioManager` toegepast zoals bij muziek en effecten (de echte Crowd-bus komt in E4).

## Herkomst

Alle `crowd_*.wav` (behalve het bestaande `crowd_ooh.wav`) zijn voor dit spel gegenereerd door `make_crowd()` in `scripts/make_audio.py` uit gevormde ruis (eigen code, vaste seeds, geen samples, geen derde partij). Placeholder tot werkstroom E ze door opnames vervangt. Niveau -24 dBFS RMS. Meetwaarden: `docs/audio-analyse.md`.

## Verifiëren (hoofdsessie)

1. Bouwen en `dotnet test godot/tests` (CrowdModelTests: stijging bij goal en terugval binnen 4 s, basisniveau 0-0/min 10 tegen 2-2/min 88, reactie in dezelfde tick, determinisme, balgevaar).
2. `python scripts/smoke_godot.py` en autotest `4_match` (`python scripts/run_autotest.py`): geen scriptfouten bij het laden van de nieuwe bedden; slider `crowd_volume` aanwezig in Instellingen > Audio.
3. Handmatig: wedstrijd op 1x; bij een goal, redding en kaart de reactie horen (<= 0,5 s); op 16x alleen bedden; `crowd_volume` op 0 is stil.
4. Blindtest met 5 testers uit de roadmap: ledig 0-0 in minuut 10 en 2-2 in minuut 88 (zelfde audio-instellingen) en vraag welke gespannener klinkt; slaagt bij >= 4 van 5.
