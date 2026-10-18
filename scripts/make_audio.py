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


# ---- music: lo-fi beat loops (drums, pluck bass, short Rhodes-like stabs, small riff), own chords and riffs ----
# Everything is circular: note tails wrap around, filters are warmed up on the loop's end, wow/flutter uses whole
# LFO cycles per loop, so the last sample flows into the first.
def kick(amp):
    n, ph, out = int(.32 * SR), 0.0, []
    for i in range(n):
        t = i / SR
        ph += TAU * (46 + 80 * math.exp(-t * 28)) / SR    # pitch drops 126 -> 46 Hz
        out.append(amp * math.sin(ph) * math.exp(-t * 11) * min(1, i / 20))
    return out


def snare(amp, rnd, brush=False):
    n = int((.24 if brush else .18) * SR)
    noise = lowpass(highpass([rnd.uniform(-1, 1) for _ in range(n)], 1100), 5200)
    dn = 14 if brush else 22                              # brush: longer, airier, no body
    return [amp * min(1, i / 30) * (noise[i] * math.exp(-i / SR * dn) +
            (0 if brush else .55 * math.sin(TAU * 185 * i / SR) * math.exp(-i / SR * 38))) for i in range(n)]


def hat(amp, rnd, open_=False):
    n = int((.22 if open_ else .05) * SR)
    noise = highpass(highpass([rnd.uniform(-1, 1) for _ in range(n)], 5000), 5000)
    d = 14 if open_ else 70
    return [amp * v * math.exp(-i / SR * d) for i, v in enumerate(noise)]


def bass(freq, seconds, amp):                             # plucked, round: fundamental + quickly fading 2nd/3rd
    n = int(seconds * SR)
    return [amp * min(1, i / 60) * math.exp(-i / SR * 4.5) * min(1, (n - i) / 300) *
            (math.sin(TAU * freq * i / SR) + (.45 * math.sin(TAU * 2 * freq * i / SR) + .15 * math.sin(TAU * 3 * freq * i / SR)) * math.exp(-i / SR * 14))
            for i in range(n)]


def keys(freq, seconds, amp, bright=1.0):                 # Rhodes-like: FM with decaying index, short gated stab
    n = int((seconds + .08) * SR)
    g0, g1 = int(seconds * SR), int(.08 * SR)
    out = []
    for i in range(n):
        t = i / SR
        ph = TAU * freq * t
        idx = bright * (.25 + 1.1 * math.exp(-t * 9))
        gate = 1.0 if i < g0 else max(0.0, 1 - (i - g0) / g1)
        out.append(amp * gate * min(1, i / 45) * math.exp(-t * 2.6) * math.sin(ph + idx * math.sin(ph)))
    return out


def circular_lowpass(x, cutoff):
    a, y = 1 - math.exp(-TAU * cutoff / SR), 0.0
    for v in x[-SR // 2:]: y += a * (v - y)                # warm-up on the loop's end
    out = []
    for v in x:
        y += a * (v - y); out.append(y)
    return out


def wow(x, total_sec):                                    # tape wow (~0.5 Hz) + flutter (~6 Hz), whole cycles per loop
    n = len(x)
    kw, kf = max(1, round(total_sec * .5)), max(1, round(total_sec * 6))
    out = []
    for i in range(n):
        p = i + 18 * math.sin(TAU * kw * i / n) + 1.2 * math.sin(TAU * kf * i / n)
        j = math.floor(p); f = p - j
        out.append(x[j % n] * (1 - f) + x[(j + 1) % n] * f)
    return out


def music(bpm, chords, bars_per_chord, kit, bass_line, stabs, riff, swing, cutoff, seed):
    rnd = random.Random(seed)
    beat = 60 / bpm
    bars = len(chords) * bars_per_chord
    total = bars * 4 * beat
    mix = buf(total)
    n = len(mix)

    def at(bar, slot16, jitter=.006):                     # 16th grid with swing on the off-16ths/8ths, small humanize
        pos = slot16 // 2 * 2 + (swing * 2 if slot16 % 2 else 0)
        return int(((bar * 16 + pos) * beat / 4 + rnd.uniform(-jitter, jitter)) * SR) % n

    for bar in range(bars):
        root, voicing = chords[bar // bars_per_chord]
        fill = bar % 4 == 3
        for slot in kit["kick"][bar % 2]: add(mix, kick(.9 * rnd.uniform(.9, 1)), at(bar, slot, .003), wrap=True)
        for slot in kit["snare"]: add(mix, snare(.42 * rnd.uniform(.9, 1), rnd, kit["brush"]), at(bar, slot), wrap=True)
        for slot in kit["ghost"] + (kit["fill"] if fill else []): add(mix, snare(.12, rnd, True), at(bar, slot), wrap=True)
        for slot in range(0, 16, kit["hat_step"]):
            open_ = fill and slot == 14
            add(mix, hat((.16 if slot % 4 == 0 else .10) * rnd.uniform(.75, 1), rnd, open_), at(bar, slot), wrap=True)
        for slot, interval, sec in bass_line:
            add(mix, bass(hz(root + interval), sec, .5 * rnd.uniform(.9, 1)), at(bar, slot, .004), wrap=True)
        for slot, sec, vel in stabs:
            for k, m in enumerate(voicing):               # slight strum, low note first
                add(mix, keys(hz(m), sec, .085 * vel * rnd.uniform(.85, 1)), (at(bar, slot) + k * int(.009 * SR)) % n, wrap=True)
        for b, slot, m in riff:
            if b == bar: add(mix, keys(hz(m), .28, .11, .6), at(bar, slot), wrap=True)
    crackle = [0.0] * n                                   # vinyl bed: soft hiss + sparse clicks
    for i in range(n): crackle[i] = rnd.uniform(-1, 1) * .006
    for _ in range(int(total * 6)):
        i = rnd.randrange(n); crackle[i] += rnd.choice((-1, 1)) * rnd.uniform(.03, .08)
    mix = [v + c for v, c in zip(mix, circular_lowpass(crackle, 3000))]
    mix = circular_lowpass(circular_lowpass(mix, cutoff), cutoff * 1.6)   # warm, dulled top
    m = max(abs(v) for v in mix)
    mix = [math.tanh(1.4 * v / m) for v in mix]           # soft tape saturation, gentle so the beat keeps its punch
    mix = wow(mix, total)
    # The file starts 30-250 ms before the downbeat (a pickup), at the point where the 50 ms on either side of the
    # seam are equally loud, so the seam never sits right on a kick (the loop itself is circular either way).
    w = int(.05 * SR)
    lvl = lambda a, b: math.log10(sum(v * v for v in mix[a:b]) / w + 1e-12)
    r = min(range(int(.03 * SR), int(.25 * SR), int(.005 * SR)), key=lambda r: abs(lvl(n - r - w, n - r) - lvl(n - r, n - r + w)))
    return mix[-r:] + mix[:-r], total


def make_music():
    # Menu, 86 bpm, swung 8ths, brushes: Abmaj9 - Gm9 - Cm9 - Bb13sus, 2 bars each (8 bars). (root, rootless voicing)
    menu_chords = [(44, [60, 63, 67, 70]), (43, [58, 62, 65, 69]), (48, [58, 62, 63, 67]), (46, [56, 60, 62, 67])]
    menu_kit = {"kick": ([0, 10], [0, 6, 10]), "snare": [4, 12], "ghost": [15], "fill": [7, 14], "hat_step": 2, "brush": True}
    menu_bass = [(0, 0, .55), (6, 0, .25), (10, 7, .45), (14, 12, .2)]
    menu_stabs = [(0, .55, 1.0), (6, .28, .7), (11, .3, .6)]
    menu_riff = [(0, 4, 79), (0, 6, 77), (0, 8, 75), (1, 2, 72), (1, 8, 75), (2, 4, 77), (2, 6, 79), (2, 10, 82),
                 (3, 4, 79), (3, 12, 77), (4, 4, 75), (4, 6, 77), (4, 8, 79), (5, 0, 82), (5, 6, 79),
                 (6, 4, 77), (6, 6, 75), (6, 10, 72), (7, 4, 74), (7, 12, 77)]
    x, _ = music(86, menu_chords, 2, menu_kit, menu_bass, menu_stabs, menu_riff, .60, 2600, 1)
    save("music_menu.wav", x, 0.5)
    # Match, 104 bpm, 16th hats, punchier snare, moving bass: Em9 - A13 - Cmaj9 - B7b13, 1 bar each, twice (8 bars)
    match_chords = [(40, [55, 59, 62, 66]), (45, [55, 61, 66, 71]), (48, [55, 59, 62, 64]), (47, [57, 63, 67, 74])] * 2
    match_kit = {"kick": ([0, 7, 10], [0, 10, 13]), "snare": [4, 12], "ghost": [7], "fill": [14, 15], "hat_step": 1, "brush": False}
    match_bass = [(0, 0, .35), (3, 12, .15), (6, 0, .2), (8, 7, .3), (10, 10, .2), (14, 12, .15)]
    match_stabs = [(0, .3, 1.0), (6, .22, .75), (10, .22, .65)]
    match_riff = [(0, 0, 71), (0, 6, 74), (0, 8, 76), (0, 12, 79), (1, 4, 78), (1, 6, 76), (1, 10, 73),
                  (2, 4, 79), (2, 6, 76), (2, 8, 74), (3, 2, 75), (3, 6, 78), (3, 12, 81),
                  (4, 0, 83), (4, 4, 81), (4, 6, 79), (4, 12, 76), (5, 4, 78), (5, 8, 76), (5, 10, 73),
                  (6, 4, 74), (6, 6, 76), (6, 10, 79), (7, 0, 78), (7, 6, 75), (7, 10, 71)]
    x, _ = music(104, match_chords, 1, match_kit, match_bass, match_stabs, match_riff, .56, 3400, 2)
    save("music_match.wav", x, 0.5)


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


def make_ball():
    """Soft ball sounds: three pass thumps (different pitch), a harder kick, a foul thud, a keeper clap, a card whistle and a crowd 'ooh'."""
    rnd = random.Random(21)

    def thump(freq, seconds, decay, click):
        n = int(seconds * SR)
        noise = lowpass([rnd.uniform(-1, 1) for _ in range(n)], 1800)
        return [min(1, i / 12) * (math.sin(TAU * freq * (1 + .6 * math.exp(-i / SR * 40)) * i / SR) * math.exp(-i / SR * decay)
                                  + click * noise[i] * math.exp(-i / SR * 150)) for i in range(n)]
    for k, f in enumerate((120, 150, 185), 1):
        save(f"ball_pass_{k}.wav", fade_edges(thump(f, .12, 38, .5)), 0.6)
    save("ball_kick.wav", fade_edges(thump(95, .2, 24, .9)), 0.7)
    n = int(.3 * SR)
    body = lowpass([rnd.uniform(-1, 1) for _ in range(n)], 500)
    save("foul_thud.wav", fade_edges([(body[i] * 1.4 + math.sin(TAU * 62 * i / SR)) * math.exp(-i / SR * 16) * min(1, i / 20) for i in range(n)]), 0.7)
    clap = buf(.35)                                       # two slightly offset slaps: reads as a glove clap
    for start, amp in ((0, 1.0), (int(.035 * SR), .6)):
        m = int(.2 * SR)
        add(clap, [amp * v * math.exp(-i / SR * 45) for i, v in enumerate(lowpass(highpass([rnd.uniform(-1, 1) for _ in range(m)], 1200), 5000))], start)
    add(clap, [.8 * math.sin(TAU * 140 * i / SR) * math.exp(-i / SR * 30) for i in range(int(.2 * SR))])
    save("keeper_save.wav", fade_edges(clap), 0.7)
    save("whistle_card.wav", fade_edges(whistle_blast(.3)), 0.8)
    n = int(1.6 * SR)                                      # swells up, breaks off: a soft 'ooh' of a crowd
    air = lowpass(highpass([rnd.uniform(-1, 1) for _ in range(n)], 350), 1500)
    save("crowd_ooh.wav", fade_edges([v * min(1, (i / SR) / .7) ** 2 * math.exp(-max(0, i / SR - .8) / .25) for i, v in enumerate(air)], 20), 0.5)


if __name__ == "__main__":
    make_whistles(); make_goal(); make_ui(); make_ball(); make_music()
