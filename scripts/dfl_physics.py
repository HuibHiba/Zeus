"""Natuurkunde uit de DFL/Sportec 'integrated dataset' (7 wedstrijden Bundesliga 2022/23, 25 Hz, CC BY 4.0).

Leest data/external/dfl/*.xml (alleen lezen, streaming) en geeft:
- loopsnelheid en versnelling van spelers (door de DFL berekend: S in km/u, hier omgerekend naar m/s; A in m/s^2)
- beginsnelheid van de bal bij passes (per hoogte en afstand), schoten (per afloop) en standaardsituaties
- een schatting van de vertraging van een rollende bal (rolweerstand + luchtweerstand) uit platte passen, alleen in vensters zonder
  enige andere gebeurtenis (alle eventtypen), zonder snelheidssprong en zonder hoge bal; als die schatting fysiek onmogelijk blijft
  (> ROLL_MAX_PLAUSIBLE m/s^2) of te weinig vensters heeft, staat er "rolweerstand": "niet betrouwbaar meetbaar"
Eenheden van n: speler-snelheid/-versnelling = beelden; piek per speler = speler-wedstrijden (halves samengevoegd, minimaal 10 minuten
gedetecteerd), met ook n_speler_helften; passen/schoten = events; rollende bal = vensters.
Draai met `python -I scripts/dfl_physics.py` (externe data, onbetrouwbaar behandelen). Bron: DFL/Sportec, CC BY 4.0
(Bassek e.a., Scientific Data 2025); naamsvermelding verplicht.
"""
import glob
import json
import sys
from datetime import datetime
from pathlib import Path

import numpy as np
from defusedxml import ElementTree as ET  # externe XML: geen entity-/DTD-aanvallen

sys.path.insert(0, str(Path(__file__).resolve().parent))
from zeus_common import atomic_write_text  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "external" / "dfl"
OUT = ROOT / "data" / "dfl_summary.json"
DT_MS = 40.0  # 25 Hz
MAX_SPEED = 12.0
MAX_ACC = 10.0
G = 9.81
ROLL_MAX_PLAUSIBLE = 3.0  # m/s^2; een rollende voetbal vertraagt met ca. 0,5-1,5 m/s^2, meer duidt op contact of meetruis
ROLL_MIN_WINDOWS = 100
ROLL_MAX_JUMP = 0.5       # m/s per beeld (op de positiesnelheid); groter = snelheidssprong (aanraking, botsing, trap)
ROLL_MAX_Z = 0.3          # m: hoger is een hoge bal
MIN_PEAK_FRAMES = 25 * 60 * 10


def ts(s: str) -> float:
    return datetime.fromisoformat(s).timestamp() * 1000.0


def match_id(path: str) -> str:
    return Path(path).stem.rsplit("_", 1)[-1]


def parse_positions(path: str):
    """Geeft (ballen per helft, spelersnelheden, spelersversnellingen, snelheden per speler-id [lijst per helft])."""
    balls, speeds, accs = [], [], []
    per_player = {}
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
            xyz = np.array([[float(f.get(k, "nan")) for k in ("X", "Y", "Z")] for f in frames], dtype=float)
            # snelheid uit de positie (3D, +-2 beelden = 0,16 s): de door de DFL geleverde S van de bal springt per beeld met meters/s
            v = np.full(len(frames), np.nan)
            if len(frames) > 4:
                v[2:-2] = np.linalg.norm(xyz[4:] - xyz[:-4], axis=1) / (4 * DT_MS / 1000.0)
            balls.append({"t0": ts(frames[0].get("T")), "n0": n0, "S": s, "A": a, "Z": xyz[:, 2], "V": v})
        else:
            ok = s <= MAX_SPEED
            speeds.append(s[ok])
            accs.append(a[np.abs(a) <= MAX_ACC])
            per_player.setdefault(el.get("PersonId"), []).append(s[ok])
        el.clear()
    return balls, speeds, accs, per_player


def ball_window(balls, t_ms: float, frames: int, key: str = "S"):
    for b in balls:
        i = int(round((t_ms - b["t0"]) / DT_MS))
        if 0 <= i < len(b["S"]) - frames - 1:
            return b[key][i:i + frames + 1]
    return None


def rolling_decel(balls, t_ms: float):
    """Vertraging (m/s^2, positief) van een rollende bal 0,32-1,28 s na een pas, of None als het venster niet schoon is:
    positiesnelheid V >= 8 m/s bij start en >= 4 m/s overal, geen snelheidssprong tussen opeenvolgende beelden (ROLL_MAX_JUMP),
    bal laag (z <= ROLL_MAX_Z; ontbrekende z = venster niet gebruikt), dalend en een lineaire fit met kleine restfout."""
    v = ball_window(balls, t_ms, 34, "V")
    z = ball_window(balls, t_ms, 34, "Z")
    if v is None or z is None:
        return None
    seg = v[8:33]
    if np.any(np.isnan(v[6:35])) or np.any(np.isnan(z)) or seg.min() < 4.0 or seg[0] < 8.0:
        return None
    if z.max() > ROLL_MAX_Z:
        return None
    if np.max(np.abs(np.diff(v[6:35]))) > ROLL_MAX_JUMP:
        return None
    t = np.arange(len(seg)) * DT_MS / 1000.0
    slope, icpt = np.polyfit(t, seg, 1)
    if slope >= 0 or float(np.std(seg - (slope * t + icpt))) > 0.4:
        return None
    return float(-slope)


def parse_events(path: str):
    """Geeft (pass-/schotevents gesorteerd, gesorteerde tijden van ALLE events)."""
    out = []
    all_times = []
    for _, el in ET.iterparse(path, events=("end",)):
        if el.tag != "Event":
            continue
        t = ts(el.get("EventTime"))
        all_times.append(t)  # elk eventtype onderbreekt een rollende bal
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
    return out, np.array(sorted(all_times))


def pct(vals, qs=(10, 50, 90)):
    v = np.asarray(vals, dtype=float)
    v = v[~np.isnan(v)]
    if v.size == 0:
        return {"n": 0}
    return {f"p{q}": round(float(np.percentile(v, q)), 2) for q in qs} | {"max": round(float(v.max()), 2), "n": int(v.size)}


def main() -> int:
    pos_files = sorted(glob.glob(str(SRC / "DFL_04_03_positions_raw_observed_*.xml")))
    all_speed, all_acc = [], []
    ball_all = []
    pass_v = {}      # (hoogte, afstand) -> beginsnelheden
    shot_v = {}      # afloop -> beginsnelheden
    setp_v = {}      # standaardsituatie -> beginsnelheden
    decel = []       # m/s^2 voor platte passes met schoon venster
    n_flat_candidates = 0
    peaks = []       # p99.9 per speler-wedstrijd
    n_player_halves = 0
    n_matches = 0
    for pf in pos_files:
        mid = match_id(pf)
        ef = SRC / f"DFL_03_02_events_raw_{Path(pf).stem.split('observed_')[1]}.xml"
        if not ef.exists():
            continue
        n_matches += 1
        print(mid, "positie", flush=True)
        balls, speeds, accs, per_player = parse_positions(pf)
        all_speed += speeds
        all_acc += accs
        for halves in per_player.values():
            n_player_halves += len(halves)
            v = np.concatenate(halves)
            if v.size > MIN_PEAK_FRAMES:
                peaks.append(float(np.percentile(v, 99.9)))
        for b in balls:
            ball_all.append(b["S"][b["S"] <= 45])
        evs, all_times = parse_events(str(ef))
        for e in evs:
            j = int(np.searchsorted(all_times, e["t"], side="right"))
            nxt = float(all_times[j]) if j < len(all_times) else e["t"] + 10_000
            if e["kind"] == "pass":
                w = ball_window(balls, e["t"], 15)
                if w is None:
                    continue
                v0 = float(np.max(w))
                pass_v.setdefault((e["height"], e["dist"]), []).append(v0)
                if e["set"]:
                    setp_v.setdefault(e["set"], []).append(v0)
                if e["height"] == "flat" and nxt - e["t"] > 1600:
                    n_flat_candidates += 1
                    d = rolling_decel(balls, e["t"])
                    if d is not None:
                        decel.append(d)
            else:
                w = ball_window(balls, e["t"], 25)
                if w is not None:
                    shot_v.setdefault(e["result"], []).append(float(np.max(w)))
    speeds = np.concatenate(all_speed)
    accs = np.concatenate(all_acc)
    ball_s = np.concatenate(ball_all)
    dec = np.array(decel)
    roll = pct(dec, (25, 50, 75))
    roll_ok = dec.size >= ROLL_MIN_WINDOWS and float(np.median(dec)) <= ROLL_MAX_PLAUSIBLE
    print(f"rollende bal: {n_flat_candidates} platte passen zonder eventonderbreking, {dec.size} schone vensters, mediaan "
          f"{float(np.median(dec)) if dec.size else float('nan'):.2f} m/s^2 -> {'betrouwbaar' if roll_ok else 'niet betrouwbaar'}")
    summary = {
        "bron": "DFL/Sportec integrated dataset, Bundesliga 2022/23, 25 Hz, CC BY 4.0",
        "wedstrijden": n_matches,
        "speler_snelheid_m_s": pct(speeds, (50, 90, 99, 99.9)),
        "piek_per_speler_p99.9_m_s": pct(peaks, (10, 50, 90)) | {"eenheid_n": "speler-wedstrijden (halves samengevoegd, >10 min gedetecteerd)",
                                                                 "n_speler_helften": n_player_halves},
        "versnelling_positief_m_s2": pct(accs[accs > 0], (50, 90, 99)),
        "vertraging_absoluut_m_s2": pct(-accs[accs < 0], (50, 90, 99)),
        "bal_snelheid_alle_frames_m_s": pct(ball_s, (50, 90, 99, 99.9)),
        "pass_beginsnelheid_m_s": {f"{h}/{d}": pct(v) for (h, d), v in sorted(pass_v.items(), key=lambda kv: (str(kv[0][0]), str(kv[0][1]))) if len(v) >= 30},
        "schot_beginsnelheid_per_afloop_m_s": {r: pct(v) for r, v in sorted(shot_v.items()) if len(v) >= 10},
        "schot_beginsnelheid_alle_m_s": pct([x for v in shot_v.values() for x in v]),
        "standaardsituatie_beginsnelheid_m_s": {s: pct(v) for s, v in sorted(setp_v.items()) if len(v) >= 10},
    }
    if roll_ok:
        summary["vertraging_rollende_bal_m_s2"] = roll | {"eenheid_n": "vensters (platte passen, schoon venster)", "kandidaten": n_flat_candidates}
        summary["vertraging_rollende_bal_g_equivalent_mediaan"] = round(float(np.median(dec)) / G, 3)
    else:
        summary["rolweerstand"] = "niet betrouwbaar meetbaar"
        summary["rolweerstand_toelichting"] = (f"{n_flat_candidates} kandidaten, {dec.size} schone vensters, mediaan "
                                               f"{round(float(np.median(dec)), 2) if dec.size else None} m/s^2 > {ROLL_MAX_PLAUSIBLE} of minder dan "
                                               f"{ROLL_MIN_WINDOWS} vensters; de balpositie in de 25 Hz-data is te ruw (snelheidssprongen van meerdere m/s per beeld, ook zonder event) voor een schone vertragingsfit.")
    atomic_write_text(OUT, json.dumps(summary, indent=2, ensure_ascii=False, allow_nan=False))
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
