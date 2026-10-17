"""Positional-play metrics from the sim, same definitions as ai_gap_real.py (scripts/ai_gap_metrics.py).

Input: the frames_*.tsv / events_*.tsv dump of `Zeus.Runner --positional-probe --out-dir DIR` (1 Hz, running play).
Writes docs/ai-gap/sim_metrics.json (all matches pooled + per tactic). The dump itself stays out of git.
Run: C:/dev/zeus/.venv/Scripts/python.exe -I scripts/ai_gap_sim.py DIR
"""
import glob
import json
import sys
from collections import defaultdict
from multiprocessing import Pool
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from ai_gap_metrics import carrier_frame, passes_summary, pct, runs_from_sequence, team_frame  # noqa: E402

OUT = HERE.parent / "docs" / "ai-gap" / "sim_metrics.json"


def process(frames_path):
    tag = Path(frames_path).stem[len("frames_"):]
    home, away, seed = tag.split("__")
    tactic = {"H": home, "A": away}
    rows = []
    for line in open(frames_path, encoding="utf-8").read().splitlines()[1:]:
        t, period, _, poss, _, bx, by, pl = line.split("\t")
        players = [p.split("|") for p in pl.split(";")]
        rows.append((int(period), poss, float(bx), float(by),
                     [(p[0], float(p[2]), float(p[3]), float(p[4]), p[5] == "1") for p in players]))
    gk = defaultdict(list)
    for period, _, _, _, players in rows:
        for team, x, y, s, k in players:
            if k:
                gk[(team, period)].append(x)
    direction = {k: (1 if np.mean(v) < 0 else -1) for k, v in gk.items()}
    team_rows, carrier_rows = defaultdict(list), []
    for period, poss, bx, by, players in rows:
        rot = {}
        for team in ("H", "A"):
            d = direction.get((team, period))
            a = np.array([(x, y, s, k) for tm, x, y, s, k in players if tm == team], float)
            if d is None or len(a) == 0:
                continue
            rot[team] = (a[:, :2] * d, a[:, 2], a[:, 3].astype(bool), d)
        for team, (xy, sp, isgk, d) in rot.items():
            m = team_frame(xy[~isgk], sp[~isgk], bx * d)
            if m:
                team_rows[(tactic[team], "in_bezit" if team == poss else "zonder_bal")].append(m)
        if poss in rot and len(rot) == 2:
            xy, _, _, d = rot[poss]
            opp = rot["A" if poss == "H" else "H"]
            c = carrier_frame(xy, opp[0], np.array([bx, by]) * d)
            if c:
                carrier_rows.append((tactic[poss], c))

    ev = [l.split("\t") for l in open(frames_path.replace("frames_", "events_"), encoding="utf-8").read().splitlines()[1:]]
    ball = [e for e in ev if e[0] in ("Pass", "Shot", "BallControl", "Interception", "PossessionChange", "Out", "Save")]
    passes, seq = [], []
    for i, e in enumerate(ball):
        typ, team, _, _, period, x, y, outcome = e
        if typ == "Shot":
            seq.append((team, False))
        if typ != "Pass" or outcome == "clear" or not outcome.startswith("to "):
            continue
        seq.append((team, True))
        nxt = ball[i + 1] if i + 1 < len(ball) else None
        d = direction.get((team, int(period)))
        if nxt and d and nxt[0] == "BallControl" and nxt[1] == team:
            dx, dy = float(nxt[5]) - float(x), float(nxt[6]) - float(y)
            passes.append((tactic[team], 0, dx * d, float(np.hypot(dx, dy))))
    runs = [(tactic[t], n) for t, n in zip(_run_teams(seq), runs_from_sequence(seq))]
    return tag, dict(team_rows), carrier_rows, passes, runs


def _run_teams(seq):
    """Team of each possession run, aligned with runs_from_sequence."""
    out, cur = [], None
    for team, _ in seq:
        if team != cur:
            out.append(team)
            cur = team
    return out


def summarize(team_rows, carrier_rows, passes, runs):
    out = {"team": {}, "baldrager": {}, "passes": passes_summary(passes), "passes_per_balbezit": pct(runs),
           "passes_per_balbezit_gem": float(np.mean(runs)) if runs else None}
    for ph, rows in team_rows.items():
        out["team"][ph] = {k: pct([r[k] for r in rows]) for k in rows[0]}
    if carrier_rows:
        out["baldrager"] = {k: pct([r[k] for r in carrier_rows]) for k in carrier_rows[0]}
    return out


def main():
    files = sorted(glob.glob(str(Path(sys.argv[1]) / "frames_*.tsv")))
    by_tac = defaultdict(lambda: (defaultdict(list), [], [], []))
    allr = (defaultdict(list), [], [], [])
    with Pool(4) as pool:
        for tag, tr, cr, ps, rn in pool.imap_unordered(process, files):
            for (tac, ph), rows in tr.items():
                by_tac[tac][0][ph] += rows
                allr[0][ph] += rows
            for tac, c in cr:
                by_tac[tac][1].append(c)
                allr[1].append(c)
            for p in ps:
                by_tac[p[0]][2].append(p)
                allr[2].append(p)
            for tac, n in rn:
                by_tac[tac][3].append(n)
                allr[3].append(n)
    out = {"bron": f"Zeus.Runner --positional-probe, {len(files)} wedstrijden, prof-niveau, 1 Hz lopend spel",
           "wedstrijden": [Path(f).stem[7:] for f in files], "alle": summarize(*allr),
           "per_tactiek": {t: summarize(*v) for t, v in sorted(by_tac.items())}}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(out, indent=1, ensure_ascii=False), encoding="utf-8")
    print("->", OUT, len(files), "wedstrijden")


if __name__ == "__main__":
    main()
