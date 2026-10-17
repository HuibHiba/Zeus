# Audio-analyse

Gegenereerd met `python scripts/check_audio.py --write`. Doelen: piek <= -1 dBFS, geen clipping, loopnaad: sprong <= 0.02, RMS-verschil <= 3 dB.

| Bestand | Duur (s) | Piek (dBFS) | RMS (dBFS) | DC-offset | Clip | Loopnaad (sprong / RMS laatste-eerste 50 ms) | Status |
|---|---|---|---|---|---|---|---|
| ball_kick.wav | 0.20 | -3.1 | -17.2 | -0.00003 | 0 | - | ok |
| ball_pass_1.wav | 0.12 | -4.4 | -17.2 | -0.00089 | 0 | - | ok |
| ball_pass_2.wav | 0.12 | -4.4 | -17.0 | -0.00075 | 0 | - | ok |
| ball_pass_3.wav | 0.12 | -4.4 | -17.1 | +0.00020 | 0 | - | ok |
| crowd_ooh.wav | 1.60 | -6.0 | -21.2 | -0.00000 | 0 | - | ok |
| foul_thud.wav | 0.30 | -3.1 | -18.8 | +0.00351 | 0 | - | ok |
| goal_cheer.wav | 4.50 | -1.9 | -17.7 | +0.00000 | 0 | - | ok |
| keeper_save.wav | 0.35 | -3.1 | -22.1 | -0.00010 | 0 | - | ok |
| music_match.wav | 20.87 | -4.4 | -16.0 | +0.00053 | 0 | 0.0121 / -0.8 dB | ok |
| music_menu.wav | 28.24 | -4.4 | -15.6 | +0.00000 | 0 | 0.0097 / -0.4 dB | ok |
| ui_click.wav | 0.05 | -6.0 | -18.4 | -0.00009 | 0 | - | ok |
| whistle_card.wav | 0.30 | -1.9 | -10.3 | +0.00000 | 0 | - | ok |
| whistle_end.wav | 1.71 | -1.9 | -11.2 | +0.00000 | 0 | - | ok |
| whistle_half.wav | 0.96 | -1.9 | -12.0 | +0.00000 | 0 | - | ok |
| whistle_start.wav | 0.75 | -1.9 | -11.3 | +0.00000 | 0 | - | ok |

Grootte `godot/assets/audio`: 2593 KiB (WAV, 16-bit mono 22,05 kHz; in Godot als PCM ingelezen, `compress/mode=0`).
