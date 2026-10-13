"""Spelsjablonen per fase uit SkillCorner open data (A-League 2024/25, MIT).

Per spelfase (opbouw, creatie, afwerking, ... en blokhoogte van de verdedigende ploeg) de
gemiddelde positie per rol, genormaliseerd naar het tactiekmodel: x 0 = eigen doellijn, 1 = tegenstanders
doellijn (in aanvalsrichting), y 0 = links, 1 = rechts. Plus breedte en lengte van het team per fase.
Draai met `python -I scripts/skillcorner_templates.py` (externe data, alleen lezen).
"""
import json
import sys
from collections import defaultdict
from pathlib import Path

import numpy as np
import pandas as pd

ROOT = Path(__file__).resolve().parent.parent
MATCHES = ROOT / "data" / "external" / "skillcorner" / "data" / "matches"
OUT = ROOT / "data" / "skillcorner_templates.json"
L, W = 105.0, 68.0
STEP = 5  # elk 5e beeld = 0,5 s
LEFT = {"LB", "LWB", "LCB", "LW", "LM", "LCM", "LDM", "LAM"}


def tracking_ids(trk: Path) -> set:
    """Spelers-ids zoals de tracking ze gebruikt (eerste frame na beeld 3000 met spelers)."""
    with open(trk, encoding="utf-8") as f:
        for i, line in enumerate(f):
            if i < 3000:
                continue
            d = json.loads(line)
            if d["player_data"]:
                return {p["player_id"] for p in d["player_data"]}
    return set()


def main() -> None:
    acc = defaultdict(list)  # (fasetype, 'in'|'uit', rol) -> lijst van (x, y)
    dims = defaultdict(lambda: defaultdict(list))
    n_matches = 0
    for mdir in sorted(MATCHES.iterdir()):
        mid = mdir.name
        mj = mdir / f"{mid}_match.json"
        trk = mdir / f"{mid}_tracking_extrapolated.jsonl"
        php = mdir / f"{mid}_phases_of_play.csv"
        if not (mj.exists() and trk.exists() and php.exists()):
            continue
        n_matches += 1
        print(mid, flush=True)
        match = json.loads(mj.read_text(encoding="utf-8"))
        ph = pd.read_csv(php)
        players = match["players"]
        ids_trk = tracking_ids(trk)
        key = max(("id", "trackable_object"), key=lambda k: len(ids_trk & {p.get(k) for p in players}))
        info = {p[key]: (p["team_id"], p["player_role"]["acronym"]) for p in players if p.get(key) is not None}

        starts = ph["frame_start"].to_numpy()
        ends = ph["frame_end"].to_numpy()
        with open(trk, encoding="utf-8") as f:
            for i, line in enumerate(f):
                if i % STEP:
                    continue
                d = json.loads(line)
                if d["period"] is None or not d["player_data"]:
                    continue
                fr = d["frame"]
                idx = np.searchsorted(starts, fr, side="right") - 1
                if idx < 0 or fr >= ends[idx]:
                    continue
                row = ph.iloc[idx]
                in_team = int(row["team_in_possession_id"])
                sgn = 1 if row["attacking_side"] == "left_to_right" else -1
                for p in d["player_data"]:
                    if not p["is_detected"] or p["player_id"] not in info:
                        continue
                    team, role = info[p["player_id"]]
                    if team == in_team:
                        s, phase, side = sgn, row["team_in_possession_phase_type"], "in"
                    else:
                        s, phase, side = -sgn, row["team_out_of_possession_phase_type"], "uit"
                    acc[(phase, side, role)].append(((p["x"] * s) / L + 0.5, (p["y"] * s) / W + 0.5))
        for _, r in ph.iterrows():
            for side, tip in (("in", "team_in_possession"), ("uit", "team_out_of_possession")):
                label = r[f"{tip}_phase_type"]
                dims[(side, label)]["breedte"].append((r[f"{tip}_width_start"] + r[f"{tip}_width_end"]) / 2)
                dims[(side, label)]["lengte"].append((r[f"{tip}_length_start"] + r[f"{tip}_length_end"]) / 2)

    # links/rechts controleren: linkerrollen horen y < 0,5 te hebben, anders spiegelen
    ly = [np.mean([p[1] for p in v]) for (_, _, role), v in acc.items() if role in LEFT and len(v) > 200]
    flip = bool(np.mean(ly) > 0.5)
    result = {"bron": "SkillCorner open data, A-League 2024/25, MIT", "wedstrijden": n_matches,
              "y_gespiegeld": flip, "posities": {}, "afmetingen_m": {}}
    for (phase, side, role), v in acc.items():
        if len(v) < 400:
            continue
        a = np.array(v)
        y = 1 - a[:, 1] if flip else a[:, 1]
        result["posities"].setdefault(f"{side}:{phase}", {})[role] = {
            "x": round(float(a[:, 0].mean()), 3), "y": round(float(y.mean()), 3), "n": int(len(a))}
    for (side, phase), dd in dims.items():
        result["afmetingen_m"][f"{side}:{phase}"] = {
            k: {"p10": round(float(np.percentile(v, 10)), 1), "p50": round(float(np.percentile(v, 50)), 1),
                "p90": round(float(np.percentile(v, 90)), 1), "n": len(v)} for k, v in dd.items()}
    OUT.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    print("klaar ->", OUT)


if __name__ == "__main__":
    sys.exit(main())
