# Audio-analyse

Gegenereerd met `python scripts/check_audio.py --write`. Doelen: piek <= -1 dBFS, geen clipping, loopnaad: sprong <= 0.02, RMS-verschil <= 3 dB.

| Bestand | Duur (s) | Piek (dBFS) | RMS (dBFS) | DC-offset | Clip | Loopnaad (sprong / RMS laatste-eerste 50 ms) | Status |
|---|---|---|---|---|---|---|---|
| ball_kick.wav | 0.20 | -3.1 | -17.2 | -0.00003 | 0 | - | ok |
| ball_pass_1.wav | 0.12 | -4.4 | -17.2 | -0.00089 | 0 | - | ok |
| ball_pass_2.wav | 0.12 | -4.4 | -17.0 | -0.00075 | 0 | - | ok |
| ball_pass_3.wav | 0.12 | -4.4 | -17.1 | +0.00020 | 0 | - | ok |
| crowd_anticipation.wav | 3.00 | -10.4 | -24.0 | +0.00000 | 0 | - | ok |
| crowd_applause.wav | 2.60 | -5.9 | -24.0 | +0.00185 | 0 | - | ok |
| crowd_groan.wav | 2.20 | -7.4 | -24.0 | +0.00000 | 0 | - | ok |
| crowd_high.wav | 10.00 | -12.1 | -24.0 | -0.00000 | 0 | 0.1772 / +2.1 dB | ok |
| crowd_low.wav | 10.00 | -10.7 | -24.0 | +0.00000 | 0 | 0.0755 / -0.3 dB | ok |
| crowd_mid.wav | 10.00 | -12.2 | -24.0 | +0.00000 | 0 | 0.0622 / +1.6 dB | ok |
| crowd_ooh.wav | 1.60 | -6.0 | -21.2 | -0.00000 | 0 | - | ok |
| crowd_sigh.wav | 1.80 | -7.5 | -24.0 | -0.00000 | 0 | - | ok |
| crowd_whistles.wav | 2.60 | -8.8 | -24.0 | -0.00000 | 0 | - | ok |
| foul_thud.wav | 0.30 | -3.1 | -18.8 | +0.00351 | 0 | - | ok |
| goal_cheer.wav | 4.50 | -1.9 | -17.7 | +0.00000 | 0 | - | ok |
| keeper_save.wav | 0.35 | -3.1 | -22.1 | -0.00010 | 0 | - | ok |
| ui_click.wav | 0.05 | -6.0 | -18.4 | -0.00009 | 0 | - | ok |
| whistle_card.wav | 0.30 | -1.9 | -10.3 | +0.00000 | 0 | - | ok |
| whistle_end.wav | 1.71 | -1.9 | -11.2 | +0.00000 | 0 | - | ok |
| whistle_half.wav | 0.96 | -1.9 | -12.0 | +0.00000 | 0 | - | ok |
| whistle_start.wav | 0.75 | -1.9 | -11.3 | +0.00000 | 0 | - | ok |

Grootte `godot/assets/audio`: 28968 KiB (WAV, 16-bit mono 22,05 kHz; in Godot als PCM ingelezen, `compress/mode=0`).
