#!/usr/bin/env python3
"""Generates all game audio from scratch (stdlib only, no samples, no third-party material).

Writes 16-bit mono 22.05 kHz WAV files to godot/assets/audio/. Deterministic (fixed seeds).
Usage: python scripts/make_audio.py
"""
import math, os, random, struct, wave

SR = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "godot", "assets", "audio")
TAU = 2 * math.pi


def hz(midi): return 440.0 * 2 ** ((midi - 69) / 12)


def buf(seconds): return [0.0] * int(seconds * SR)


def add(dst, src, start=0, wrap=False):
    """Mix src into dst at start; wrap=True lets the tail wrap around (seamless loops)."""
    n = len(dst)
    for i, v in enumerate(src):
        j = start + i
        if j >= n:
            if not wrap: break
            j %= n
        dst[j] += v


def env(i, n, a, r):  # linear attack / release in samples
    return min(1.0, i / max(a, 1), (n - i) / max(r, 1))


def tone(freq, seconds, amp, attack=0.05, release=0.5, harm=((1, 1.0),), detune=0.0):
    n = int(seconds * SR)
    a, r = int(attack * SR), int(release * SR)
    out = []
    for i in range(n):
        t = i / SR
        s = 0.0
        for k, w in harm:
            s += w * math.sin(TAU * freq * k * t)
            if detune: s += w * math.sin(TAU * freq * k * (1 + detune) * t)
        out.append(s * amp * max(0.0, env(i, n, a, r)))
    return out


def pluck(freq, seconds, amp):
    n = int(seconds * SR)
    return [amp * math.exp(-i / SR * 3.2) * min(1.0, i / 40) *
            (math.sin(TAU * freq * i / SR) + .35 * math.sin(TAU * 2 * freq * i / SR) * math.exp(-i / SR * 6))
            for i in range(n)]


def lowpass(x, cutoff):
    a = 1 - math.exp(-TAU * cutoff / SR)
    y, out = 0.0, []
    for v in x:
        y += a * (v - y); out.append(y)
    return out


def highpass(x, cutoff): return [v - l for v, l in zip(x, lowpass(x, cutoff))]


def normalize(x, peak):
    m = max(abs(v) for v in x) or 1.0
    return [v * peak / m for v in x]


def fade_edges(x, ms=4):
    n = int(ms / 1000 * SR)
    for i in range(min(n, len(x))):
        x[i] *= i / n; x[-1 - i] *= i / n
    return x


def save(name, x, peak):
    x = normalize(x, peak)
    assert max(abs(v) for v in x) < 1.0, name          # no clipping: peak is below 0 dBFS
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(v * 32767)) for v in x))
    print(f"{name}: {len(x) / SR:.2f}s peak {20 * math.log10(max(abs(v) for v in x)):.1f} dBFS")


# ---- music: calm loops, own chord progressions, rendered with wrap-around tails so the loop point is seamless ----
def music(bpm, chords, bass_oct_shift, arp_pattern, pulse, seed):
    rnd = random.Random(seed)
    beat = 60 / bpm
    bars_per_chord = 2
    chord_len = 4 * beat * bars_per_chord
    total = chord_len * len(chords)
    mix = buf(total)
    for ci, notes in enumerate(chords):
        start = int(ci * chord_len * SR)
        for m in notes:                                   # soft pad, slightly detuned
            add(mix, tone(hz(m), chord_len + 1.5, 0.10, 1.2, 1.5, ((1, 1), (2, .25), (3, .08)), 0.003), start, wrap=True)
        add(mix, tone(hz(notes[0] - 12 * bass_oct_shift), chord_len + .5, 0.22, .08, .6, ((1, 1), (2, .3))), start, wrap=True)
        step = beat / 2
        for k in range(bars_per_chord * 8):               # eighth-note arpeggio, gentle accents
            idx = arp_pattern[k % len(arp_pattern)]
            if idx is None: continue
            m = notes[idx % len(notes)] + 12 * (1 + idx // len(notes))
            s = start + int(k * step * SR)
            add(mix, pluck(hz(m), 1.6, 0.11 * (1.0 if k % 4 == 0 else .7) * rnd.uniform(.9, 1.0)), s, wrap=True)
        if pulse:                                         # soft low pulse on every beat for the match loop
            for k in range(bars_per_chord * 4):
                add(mix, tone(hz(notes[0] - 24), .35, 0.16, .005, .3), start + int(k * beat * SR), wrap=True)
    return mix, total


def make_music():
    # Menu: Dm9 - Bbmaj7 - Gm9 - Asus4/A7 (MIDI notes root-first), 68 bpm
    menu_chords = [[50, 57, 60, 64, 65], [46, 57, 62, 65, 69], [43, 58, 62, 65, 69], [45, 57, 62, 64, 67]]
    x, _ = music(68, menu_chords, 1, [0, 2, 4, 2, 1, 3, 4, None], False, 1)
    save("music_menu.wav", x, 0.6)
    # Match: Em7 - Cmaj7 - Am7 - B7sus4, 92 bpm, a little more motion and a quiet pulse
    match_chords = [[52, 59, 62, 67, 71], [48, 59, 64, 67, 71], [45, 57, 60, 64, 67], [47, 57, 62, 66, 69]]
    x, _ = music(92, match_chords, 1, [0, 3, 2, 4, 1, 3, 2, None], True, 2)
    save("music_match.wav", x, 0.6)


# ---- effects ----
def whistle_blast(seconds, f0=2850):
    n = int(seconds * SR)
    rnd = random.Random(7)
    out, ph = [], 0.0
    for i in range(n):
        t = i / SR
        trill = 1 + 0.035 * math.sin(TAU * 38 * t)        # pea rattle
        ph += TAU * (f0 * (1 + .02 * math.sin(TAU * 5 * t))) / SR
        s = math.sin(ph) * (.75 + .25 * math.sin(TAU * 38 * t)) + .35 * math.sin(2 * ph) + rnd.uniform(-.12, .12)
        out.append(s * trill * min(1, i / (.02 * SR), (n - i) / (.05 * SR)))
    return highpass(out, 1200)


def make_whistles():
    def seq(blasts, gap):
        x = buf(sum(b for b in blasts) + gap * (len(blasts) - 1) + .2)
        t = 0.0
        for b in blasts:
            add(x, whistle_blast(b), int(t * SR)); t += b + gap
        return x
    save("whistle_start.wav", seq([.55], 0), 0.8)
    save("whistle_half.wav", seq([.32, .32], .12), 0.8)
    save("whistle_end.wav", seq([.28, .28, .75], .1), 0.8)


def make_goal():
    rnd = random.Random(11)
    n = int(4.5 * SR)
    noise = [rnd.uniform(-1, 1) for _ in range(n)]
    roar = lowpass(highpass(noise, 300), 2400)
    out = []
    for i, v in enumerate(roar):
        t = i / SR
        swell = min(1, t / .6) * math.exp(-max(0, t - 1.2) / 1.1)
        out.append(v * swell * (.75 + .25 * math.sin(TAU * 4.5 * t)))   # rippling crowd
    save("goal_cheer.wav", fade_edges(out), 0.8)


def make_ui():
    n = int(.05 * SR)
    click = [math.sin(TAU * 1500 * i / SR) * math.exp(-i / SR * 90) * min(1, i / 8) for i in range(n)]
    save("ui_click.wav", click, 0.5)


if __name__ == "__main__":
    make_whistles(); make_goal(); make_ui(); make_music()
