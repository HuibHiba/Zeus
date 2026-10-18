#!/usr/bin/env python3
"""Technical check of the game audio (stdlib only): peak, RMS, DC offset, clipping and loop seam.

Usage: python scripts/check_audio.py [--write]   (--write also writes docs/audio-analyse.md)
Exit code 1 when a file has peak > -1 dBFS, clipping, or a bad loop seam.
"""
import math, os, struct, sys, wave

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
DIR = os.path.join(ROOT, "godot", "assets", "audio")
DOC = os.path.join(ROOT, "docs", "audio-analyse.md")
PEAK_LIMIT = -1.0      # dBFS
SEAM_JUMP_MAX = 0.02   # of full scale: jump between last and first sample of a loop
SEAM_RMS_MAX = 3.0     # dB difference between last and first 50 ms


def db(v): return 20 * math.log10(v) if v > 0 else -120.0


def rms(x): return math.sqrt(sum(v * v for v in x) / len(x)) if x else 0.0


def analyze(path):
    with wave.open(path) as w:
        assert w.getsampwidth() == 2 and w.getnchannels() == 1, path
        sr, raw = w.getframerate(), w.readframes(w.getnframes())
    x = [s / 32768 for s in struct.unpack(f"<{len(raw) // 2}h", raw)]
    r = {"sec": len(x) / sr, "peak": db(max(abs(v) for v in x)), "rms": db(rms(x)), "dc": sum(x) / len(x),
         "clip": sum(1 for s in struct.unpack(f"<{len(raw) // 2}h", raw) if abs(s) >= 32767)}
    if os.path.basename(path).startswith(("music_", "crowd_low", "crowd_mid", "crowd_high")):     # loops
        n = int(0.05 * sr)
        r["jump"] = abs(x[0] - x[-1])
        r["seam_db"] = db(rms(x[-n:])) - db(rms(x[:n]))
        r["edge"] = f"{x[-1]:+.4f}/{x[0]:+.4f}"
    return r


def main():
    rows, bad = [], []
    for f in sorted(os.listdir(DIR)):
        if not f.endswith(".wav"): continue
        r = analyze(os.path.join(DIR, f))
        flags = []
        if r["peak"] > PEAK_LIMIT: flags.append("piek")
        if r["clip"]: flags.append("clipping")
        noise = f.startswith("crowd_")      # shaped noise: neighbouring samples always differ, only the level across the seam counts
        if "jump" in r and ((r["jump"] > SEAM_JUMP_MAX and not noise) or abs(r["seam_db"]) > SEAM_RMS_MAX): flags.append("loopnaad")
        if flags: bad.append(f)
        loop = f"{r['jump']:.4f} / {r['seam_db']:+.1f} dB" if "jump" in r else "-"
        rows.append(f"| {f} | {r['sec']:.2f} | {r['peak']:.1f} | {r['rms']:.1f} | {r['dc']:+.5f} | {r['clip']} | {loop} | {', '.join(flags) or 'ok'} |")
    size = sum(os.path.getsize(os.path.join(DIR, f)) for f in os.listdir(DIR))
    table = ("| Bestand | Duur (s) | Piek (dBFS) | RMS (dBFS) | DC-offset | Clip | Loopnaad (sprong / RMS laatste-eerste 50 ms) | Status |\n"
             "|---|---|---|---|---|---|---|---|\n" + "\n".join(rows))
    print(table); print(f"audio-map: {size / 1024:.0f} KiB")
    if "--write" in sys.argv:
        with open(DOC, "w", encoding="utf-8", newline="\n") as o:
            o.write(f"# Audio-analyse\n\nGegenereerd met `python scripts/check_audio.py --write`. Doelen: piek <= {PEAK_LIMIT:.0f} dBFS, "
                    f"geen clipping, loopnaad: sprong <= {SEAM_JUMP_MAX}, RMS-verschil <= {SEAM_RMS_MAX:.0f} dB.\n\n{table}\n\n"
                    f"Grootte `godot/assets/audio`: {size / 1024:.0f} KiB (WAV, 16-bit mono 22,05 kHz; in Godot als PCM ingelezen, `compress/mode=0`).\n")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
