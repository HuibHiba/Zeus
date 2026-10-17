"""Positional-play metrics from real tracking: DFL/Sportec open dataset (7 Bundesliga matches 2022/23, 25 Hz, CC BY 4.0).

Reads C:/dev/zeus/data/external/dfl (read-only, streaming XML) and writes ONLY derived statistics to
docs/ai-gap/real_metrics.json. Possession per moment as in dfl_templates.py (team of the last ball-contact event at most
6 s old, ball in play). Team metrics at 1 Hz, passes from the events with positions from the tracking (5 Hz).
Run: C:/dev/zeus/.venv/Scripts/python.exe -I scripts/ai_gap_real.py [--data C:/dev/zeus/data]
"""
import bisect
import glob
import json
import sys
from collections import defaultdict
from multiprocessing import Pool
from pathlib import Path

import numpy as np
from defusedxml import ElementTree as ET  # external XML

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from ai_gap_metrics import carrier_frame, passes_summary, pct, runs_from_sequence, team_frame  # noqa: E402
from dfl_templates import MAX_GAP_S, parse_events, parse_info, ts_ms  # noqa: E402

DATA = Path(sys.argv[sys.argv.index("--data") + 1]) if "--data" in sys.argv else Path("C:/dev/zeus/data")
OUT = HERE.parent / "docs" / "ai-gap" / "real_metrics.json"
STEP = 5          # keep every 5th frame (5 Hz) for pass positions; team metrics use every 25th (1 Hz)


def parse_plays(path):
    """Ball actions for passes and possession runs: (t_ms, team, player, recipient or None, ok, is_pass)."""
    out = []
    for _, el in ET.iterparse(path, events=("end",)):
        if el.tag != "Event":
            continue
        t = ts_ms(el.get("EventTime"))
        for c in el.iter():
            if c.tag == "Play" and c.get("Team"):
                out.append((t, c.get("Team"), c.get("Player"), c.get("Recipient"),
                            c.get("Evaluation") == "successfullyCompleted", True))
            elif c.tag == "ShotAtGoal" and c.get("Team"):
                out.append((t, c.get("Team"), c.get("Player"), None, False, False))
        el.clear()
    out.sort(key=lambda e: e[0])
    return out


def parse_tracking(path, players):
    """{(half, n): {'t', 'bx', 'by', 'st', 'pl': {pid: (x, y, speed)}}} at 5 Hz."""
    frames = defaultdict(lambda: {"pl": {}})
    for _, el in ET.iterparse(path, events=("end",)):
        if el.tag != "FrameSet":
            continue
        team, half, pid = el.get("TeamId"), el.get("GameSection"), el.get("PersonId")
        if team == "referee" or (team != "BALL" and pid not in players):
            el.clear()
            continue
        for f in el.iter("Frame"):
            n = int(f.get("N"))
            if n % STEP:
                continue
            try:
                x, y = float(f.get("X")), float(f.get("Y"))
            except (TypeError, ValueError):
                continue
            if not (np.isfinite(x) and np.isfinite(y)) or abs(x) > 60 or abs(y) > 40:
                continue
            fr = frames[(half, n)]
            if team == "BALL":
                fr.update(t=ts_ms(f.get("T")), bx=x, by=y, st=f.get("BallStatus"))
            else:
                fr["pl"][pid] = (x, y, float(f.get("S") or 0) / 3.6)  # S in km/h
        el.clear()
    return frames


def process_match(args):
    mid, info_f, ev_f, pos_f = args
    teams, players = parse_info(info_f)
    ev = parse_events(ev_f, players)  # (t, team) ball contacts, possession source
    ev_t = [e[0] for e in ev]
    plays = parse_plays(ev_f)
    frames = parse_tracking(pos_f, players)
    keys = sorted((k for k, f in frames.items() if "t" in f), key=lambda k: frames[k]["t"])
    times = [frames[k]["t"] for k in keys]
    gk = defaultdict(list)
    for (half, n), f in frames.items():
        for pid, (x, y, s) in f["pl"].items():
            if players[pid][1] == "TW":
                gk[(players[pid][0], half)].append(x)
    direction = {k: (1 if np.mean(v) < 0 else -1) for k, v in gk.items()}

    team_rows = defaultdict(list)    # (phase) -> list of team metric dicts
    carrier_rows = []
    for k in keys:
        half, n = k
        f = frames[k]
        if n % 25 or f.get("st") != "1":
            continue
        i = bisect.bisect_right(ev_t, f["t"]) - 1
        if i < 0 or (f["t"] - ev_t[i]) / 1000.0 > MAX_GAP_S:
            continue
        poss = ev[i][1]
        by_team = defaultdict(list)
        for pid, (x, y, s) in f["pl"].items():
            by_team[players[pid][0]].append((x, y, s, players[pid][1] == "TW"))
        if len(by_team) != 2 or any((t, half) not in direction for t in by_team):
            continue
        rot = {}
        for team, rows in by_team.items():
            d = direction[(team, half)]
            a = np.array(rows, float)
            rot[team] = (a[:, :2] * d, a[:, 2], a[:, 3].astype(bool), d)
        for team, (xy, sp, isgk, d) in rot.items():
            m = team_frame(xy[~isgk], sp[~isgk], f["bx"] * d)
            if m:
                team_rows["in_bezit" if team == poss else "zonder_bal"].append(m)
        if poss in rot:
            xy, _, _, d = rot[poss]
            opp = next(v for t, v in rot.items() if t != poss)
            c = carrier_frame(xy, opp[0], np.array([f["bx"], f["by"]]) * d)
            if c:
                carrier_rows.append(c)

    def pos_at(pid, t):
        j = bisect.bisect_left(times, t)
        for jj in (j, j - 1):
            if 0 <= jj < len(keys) and abs(times[jj] - t) < 150 and pid in frames[keys[jj]]["pl"]:
                k = keys[jj]
                return k[0], np.array(frames[k]["pl"][pid][:2])
        return None

    passes, seq = [], []
    for t, team, player, rec, ok, is_pass in plays:
        seq.append((team, is_pass))
        if not (is_pass and ok and rec):
            continue
        j = bisect.bisect_right(ev_t, t + 1)  # next ball contact after the pass
        if j >= len(ev_t) or (ev_t[j] - t) / 1000.0 > MAX_GAP_S:
            continue
        a, b = pos_at(player, t), pos_at(rec, ev_t[j])
        if not a or not b or (team, a[0]) not in direction:
            continue
        d = direction[(team, a[0])]
        passes.append((team, 0, float((b[1][0] - a[1][0]) * d), float(np.linalg.norm(b[1] - a[1]))))
    runs = runs_from_sequence(seq)
    return mid, dict(team_rows), carrier_rows, passes, runs


def main():
    src = DATA / "external" / "dfl"
    jobs = []
    for info in sorted(glob.glob(str(src / "DFL_02_01_matchinformation_*.xml"))):
        mid = info.split("_")[-1][:-4]
        ev = glob.glob(str(src / f"DFL_03_02_events_raw_*{mid}.xml"))
        pos = glob.glob(str(src / f"DFL_04_03_positions_raw_observed_*{mid}.xml"))
        if ev and pos:
            jobs.append((mid, info, ev[0], pos[0]))
    team_rows, carrier_rows, passes, runs, per_match = defaultdict(list), [], [], [], {}
    with Pool(2) as pool:  # each worker holds one match (~1-2 GB); other agents share this machine
        for mid, tr, cr, ps, rn in pool.imap_unordered(process_match, jobs):
            for ph, rows in tr.items():
                team_rows[ph] += rows
            carrier_rows += cr
            passes += ps
            runs += rn
            per_match[mid] = {"team_frames": {ph: len(r) for ph, r in tr.items()}, "carrier_frames": len(cr),
                              "passes_completed": len(ps), "pass_runs": len(rn)}
            print(mid, per_match[mid], file=sys.stderr)
    out = {"bron": "DFL/Sportec open data, 7 Bundesliga-wedstrijden 2022/23 (CC BY 4.0), 1 Hz, bal in spel",
           "wedstrijden": per_match, "team": {}, "baldrager": {}, "passes": passes_summary(passes),
           "passes_per_balbezit": pct(runs), "passes_per_balbezit_gem": float(np.mean(runs))}
    for ph, rows in team_rows.items():
        out["team"][ph] = {k: pct([r[k] for r in rows]) for k in rows[0]}
    out["baldrager"] = {k: pct([r[k] for r in carrier_rows]) for k in carrier_rows[0]}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(out, indent=1, ensure_ascii=False), encoding="utf-8")
    print("->", OUT)


if __name__ == "__main__":
    main()
