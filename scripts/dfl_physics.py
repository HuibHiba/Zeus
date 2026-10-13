"""Natuurkunde uit de DFL/Sportec 'integrated dataset' (7 wedstrijden Bundesliga 2022/23, 25 Hz, CC BY 4.0).

Leest data/external/dfl/*.xml (alleen lezen, streaming) en geeft:
- loopsnelheid en versnelling van spelers (door de DFL berekend: S in km/u, hier omgerekend naar m/s; A in m/s^2)
- beginsnelheid van de bal bij passes (per hoogte en afstand), schoten (per afloop) en standaardsituaties
- een schatting van de vertraging van een rollende bal (rolweerstand + luchtweerstand) uit platte passen
Draai met `python -I scripts/dfl_physics.py` (externe data, onbetrouwbaar behandelen). Bron: DFL/Sportec, CC BY 4.0
(Bassek e.a., Scientific Data 2025); naamsvermelding verplicht.
"""
import glob
import json
import sys
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "external" / "dfl"
OUT = ROOT / "data" / "dfl_summary.json"
DT_MS = 40.0  # 25 Hz
MAX_SPEED = 12.0
MAX_ACC = 10.0
G = 9.81


def ts(s: str) -> float:
    return datetime.fromisoformat(s).timestamp() * 1000.0


def match_id(path: str) -> str:
    return Path(path).stem.rsplit("_", 1)[-1]


def parse_positions(path: str):
    """Geeft (ballen per helft, spelersnelheden, spelersversnellingen, pieksnelheid per speler)."""
    balls, speeds, accs, peaks = [], [], [], []
    for _, el in ET.iterparse(path, events=("end",)):
        if el.tag != "FrameSet":
            continue
        team = el.get("TeamId")
        if team == "referee":
            el.clear()
            continue
        frames = el.findall("Frame")
        if not frames:
            el.clear()
            continue
        # DFL: S is in km/u (gecontroleerd: S stijgt 0,21 per beeld terwijl A = 1,48 m/s^2), A in m/s^2
        s = np.array([float(f.get("S")) for f in frames], dtype=np.float32) / 3.6
        a = np.array([float(f.get("A")) for f in frames], dtype=np.float32)
        if team == "BALL":
            n0 = int(frames[0].get("N"))
            balls.append({"t0": ts(frames[0].get("T")), "n0": n0, "S": s, "A": a})
        else:
            ok = s <= MAX_SPEED
            speeds.append(s[ok])
            accs.append(a[np.abs(a) <= MAX_ACC])
            if ok.sum() > 25 * 60 * 10:
                peaks.append(float(np.percentile(s[ok], 99.9)))
        el.clear()
    return balls, speeds, accs, peaks


def ball_window(balls, t_ms: float, frames: int):
    for b in balls:
        i = int(round((t_ms - b["t0"]) / DT_MS))
        if 0 <= i < len(b["S"]) - frames:
            return b["S"][i:i + frames + 1]
    return None


def parse_events(path: str):
    out = []
    for _, el in ET.iterparse(path, events=("end",)):
        if el.tag != "Event":
            continue
        t = ts(el.get("EventTime"))
        children = list(el)
        top = children[0].tag if children else ""
        for play in el.iter("Play"):
            kids = [k.tag for k in play]
            if kids and kids[0] in ("Pass", "Cross"):
                out.append({"t": t, "kind": "pass", "height": play.get("Height"), "dist": play.get("Distance"),
                            "ok": play.get("Evaluation"), "set": top if top in ("CornerKick", "FreeKick", "GoalKick", "KickOff", "ThrowIn") else ""})
        shot = el.find("ShotAtGoal")
        if shot is not None:
            kids = [k.tag for k in shot]
            out.append({"t": t, "kind": "shot", "result": kids[0] if kids else "unknown"})
        el.clear()
    out.sort(key=lambda e: e["t"])
    return out


def pct(vals, qs=(10, 50, 90)):
    v = np.asarray(vals, dtype=float)
    v = v[~np.isnan(v)]
    if v.size == 0:
        return {"n": 0}
    return {f"p{q}": round(float(np.percentile(v, q)), 2) for q in qs} | {"max": round(float(v.max()), 2), "n": int(v.size)}


def main() -> None:
    pos_files = sorted(glob.glob(str(SRC / "DFL_04_03_positions_raw_observed_*.xml")))
    all_speed, all_acc, all_peaks = [], [], []
    ball_all = []
    pass_v = {}      # (hoogte, afstand) -> beginsnelheden
    shot_v = {}      # afloop -> beginsnelheden
    setp_v = {}      # standaardsituatie -> beginsnelheden
    decel = []       # m/s^2 voor platte passes
    n_matches = 0
    for pf in pos_files:
        mid = match_id(pf)
        ef = SRC / f"DFL_03_02_events_raw_{Path(pf).stem.split('observed_')[1]}.xml"
        if not ef.exists():
            continue
        n_matches += 1
        print(mid, "positie", flush=True)
        balls, speeds, accs, peaks = parse_positions(pf)
        all_speed += speeds
        all_acc += accs
        all_peaks += peaks
        for b in balls:
            ball_all.append(b["S"][b["S"] <= 45])
        evs = parse_events(str(ef))
        times = [e["t"] for e in evs]
        for k, e in enumerate(evs):
            nxt = times[k + 1] if k + 1 < len(times) else e["t"] + 10_000
            if e["kind"] == "pass":
                w = ball_window(balls, e["t"], 15)
                if w is None:
                    continue
                v0 = float(np.max(w))
                pass_v.setdefault((e["height"], e["dist"]), []).append(v0)
                if e["set"]:
                    setp_v.setdefault(e["set"], []).append(v0)
                if e["height"] == "flat" and nxt - e["t"] > 1600:
                    w2 = ball_window(balls, e["t"], 32)
                    if w2 is not None and w2[8] >= 8.0 and w2[8] <= w2[3] + 0.5:
                        decel.append(float((w2[8] - w2[32]) / ((32 - 8) * DT_MS / 1000.0)))
            else:
                w = ball_window(balls, e["t"], 25)
                if w is not None:
                    shot_v.setdefault(e["result"], []).append(float(np.max(w)))
    speeds = np.concatenate(all_speed)
    accs = np.concatenate(all_acc)
    ball_s = np.concatenate(ball_all)
    dec = np.array(decel)
    summary = {
        "bron": "DFL/Sportec integrated dataset, Bundesliga 2022/23, 25 Hz, CC BY 4.0",
        "wedstrijden": n_matches,
        "speler_snelheid_m_s": pct(speeds, (50, 90, 99, 99.9)),
        "piek_per_speler_p99.9_m_s": pct(all_peaks, (10, 50, 90)),
        "versnelling_positief_m_s2": pct(accs[accs > 0], (50, 90, 99)),
        "vertraging_absoluut_m_s2": pct(-accs[accs < 0], (50, 90, 99)),
        "bal_snelheid_alle_frames_m_s": pct(ball_s, (50, 90, 99, 99.9)),
        "pass_beginsnelheid_m_s": {f"{h}/{d}": pct(v) for (h, d), v in sorted(pass_v.items(), key=lambda kv: (str(kv[0][0]), str(kv[0][1]))) if len(v) >= 30},
        "schot_beginsnelheid_per_afloop_m_s": {r: pct(v) for r, v in sorted(shot_v.items()) if len(v) >= 10},
        "schot_beginsnelheid_alle_m_s": pct([x for v in shot_v.values() for x in v]),
        "standaardsituatie_beginsnelheid_m_s": {s: pct(v) for s, v in sorted(setp_v.items()) if len(v) >= 10},
        "vertraging_rollende_bal_m_s2": pct(dec, (25, 50, 75)),
        "vertraging_rollende_bal_g_equivalent_mediaan": round(float(np.median(dec)) / G, 3) if dec.size else None,
    }
    OUT.write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(summary, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    sys.exit(main())
