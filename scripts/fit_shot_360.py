"""Schot naar doelpunt met keeper en verdedigers (StatsBomb 360-freeze-frames).

Koppelt elk schot (zonder strafschoppen) aan zijn 360-frame en berekent: afstand en hoek, positie van de keeper (afstand tot de
doellijn en zijdelingse afwijking van de schotlijn), aantal verdedigers in de driehoek schutter-doelpalen, afstand van de dichtstbijzijnde
verdediger tot de schutter. Fit logistisch model met en zonder 360-kenmerken (training = even match_id, test = oneven) en vergelijkt
met het eigen xG van StatsBomb. Schrijft calibration/shot_360_model.json. 360 bevat alleen spelers in beeld.
Bron: StatsBomb open data (alleen intern onderzoek; commercieel gebruik onduidelijk).
"""
import gzip
import json
import pickle
import sys
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent))
from fit_event_models import auc, logistic_fit, predict  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
RAW, RAW360 = ROOT / "data" / "raw", ROOT / "data" / "raw360"
OUT = ROOT / "calibration" / "shot_360_model.json"
YARD = 0.9144
GX, GY, HALF = 120.0, 40.0, 4.0
POST_A, POST_B = np.array([GX, GY - HALF]), np.array([GX, GY + HALF])


def in_triangle(pts, s):
    """True voor punten binnen de driehoek schutter-paal-paal."""
    def sign(p1, p2, p3):
        return (p1[:, 0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[:, 1] - p3[1])
    d1, d2, d3 = sign(pts, s, POST_A), sign(pts, POST_A, POST_B), sign(pts, POST_B, s)
    neg = (d1 < 0) | (d2 < 0) | (d3 < 0)
    pos = (d1 > 0) | (d2 > 0) | (d3 > 0)
    return ~(neg & pos)


def build() -> pd.DataFrame:
    rows = []
    for fpath in sorted(RAW360.glob("frames_*.pkl.gz")):
        mid = int(fpath.stem.split("_")[1].split(".")[0])
        epath = RAW / f"events_{mid}.pkl.gz"
        if not epath.exists():
            continue
        with gzip.open(fpath, "rb") as f:
            fr = pickle.load(f)
        with gzip.open(epath, "rb") as f:
            ev = pickle.load(f)
        ev = ev[(ev["type"] == "Shot") & (ev["period"] <= 4) & ev["location"].notna()]
        ev = ev[ev.get("shot_type") != "Penalty"] if "shot_type" in ev.columns else ev
        if ev.empty:
            continue
        opp = fr[(~fr["teammate"].astype(bool)) & (~fr["actor"].astype(bool))]
        idx = opp.groupby("id").indices
        locs = np.array(opp["location"].tolist(), dtype=float) if len(opp) else np.empty((0, 2))
        keeper = opp["keeper"].to_numpy().astype(bool) if len(opp) else np.array([], dtype=bool)
        vis = set(fr["id"].unique())
        for _, e in ev.iterrows():
            eid = e["id"]
            if eid not in vis:
                continue
            s = np.array(e["location"][:2], dtype=float)
            ii = idx.get(eid, np.array([], dtype=int))
            pts, kp = locs[ii], keeper[ii]
            if len(pts):
                gk = pts[kp]
                defn = pts[~kp]
            else:
                gk, defn = np.empty((0, 2)), np.empty((0, 2))
            if len(gk):
                k = gk[0]
                line = (POST_A + POST_B) / 2 - s
                n = np.array([-line[1], line[0]]) / (np.hypot(*line) + 1e-9)
                k_off = abs(float((k - s) @ n)) * YARD
                k_goal = (GX - k[0]) * YARD
                k_present = 1.0
            else:
                k_off, k_goal, k_present = 0.0, 0.0, 0.0
            n_cone = int(in_triangle(defn, s).sum()) if len(defn) else 0
            nearest = float(np.hypot(*(defn - s).T).min() * YARD) if len(defn) else 15.0
            dx, dy = GX - s[0], s[1] - GY
            dist = float(np.hypot(max(dx, 0.1), dy) * YARD)
            ang = float(abs(np.arctan2(HALF - dy, max(dx, 0.1)) - np.arctan2(-HALF - dy, max(dx, 0.1))))
            sb = e.get("shot_statsbomb_xg")
            rows.append({"match": mid, "dist": dist, "ang": ang, "head": e.get("shot_body_part") == "Head",
                         "first": bool(e.get("shot_first_time") is True), "fk": e.get("shot_type") == "Free Kick",
                         "goal": e.get("shot_outcome") == "Goal", "k_present": k_present, "k_off": min(k_off, 10.0), "k_goal": min(k_goal, 15.0),
                         "n_cone": min(n_cone, 5), "nearest_def": min(nearest, 15.0), "sb_xg": float(sb) if pd.notna(sb) else 0.0})
        print(mid, len(rows), flush=True)
    return pd.DataFrame(rows)


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    df = build()
    y = df["goal"].to_numpy().astype(float)
    base = np.column_stack([np.ones(len(df)), df.dist, df.dist ** 2 / 100.0, df.ang, df["head"], df["first"], df["fk"]]).astype(float)
    ext = np.column_stack([base, df.k_present, df.k_off, df.k_goal, df.n_cone, df.nearest_def]).astype(float)
    train = (df["match"] % 2 == 0).to_numpy()
    wb, we = logistic_fit(base[train], y[train]), logistic_fit(ext[train], y[train])
    sb = df["sb_xg"].to_numpy()
    names = ["const", "dist_m", "dist_m_sq", "goal_angle_rad", "head", "first_time", "free_kick", "keeper_visible", "keeper_offset_from_shot_line_m", "keeper_distance_to_goal_line_m", "defenders_in_shot_cone", "nearest_defender_m"]

    def rate(mask):
        return round(float(y[mask].mean()), 3) if mask.sum() >= 100 else None

    res = {"bron": "StatsBomb open data met 360-freeze-frames (alleen intern onderzoek; commercieel gebruik onduidelijk)",
           "data": f"{df['match'].nunique()} wedstrijden met 360, {len(df)} schoten zonder strafschoppen; 360 bevat alleen spelers in beeld",
           "doelpuntkans_totaal": round(float(y.mean()), 4), "validatie": "training = even match_id, test = oneven",
           "auc_test_zonder_360": round(auc(y[~train], predict(base[~train], wb)), 3),
           "auc_test_met_360": round(auc(y[~train], predict(ext[~train], we)), 3),
           "auc_test_statsbomb_xg": round(auc(y[~train], sb[~train]), 3),
           "coefficienten_met_360": dict(zip(names, [round(float(v), 5) for v in we])),
           "doelpunt_per_verdedigers_in_schotlijn": [{"verdedigers": str(v), "n": int((df.n_cone == v).sum()), "doelpunt": rate((df.n_cone == v).to_numpy())} for v in range(0, 4)],
           "doelpunt_per_keeper_afwijking_m": [{"bin": lab, "n": int(m.sum()), "doelpunt": rate(m.to_numpy())} for lab, m in
                                               [("<0,5", (df.k_present == 1) & (df.k_off < 0.5)), ("0,5-1,5", (df.k_present == 1) & (df.k_off >= 0.5) & (df.k_off < 1.5)),
                                                ("1,5-3", (df.k_present == 1) & (df.k_off >= 1.5) & (df.k_off < 3)), (">=3", (df.k_present == 1) & (df.k_off >= 3))]]}
    OUT.write_text(json.dumps(res, indent=2, ensure_ascii=False), encoding="utf-8")
    print("klaar ->", OUT)
    print("schoten %d | AUC zonder 360 %.3f | met 360 %.3f | StatsBomb xG %.3f" % (len(df), res["auc_test_zonder_360"], res["auc_test_met_360"], res["auc_test_statsbomb_xg"]))


if __name__ == "__main__":
    sys.exit(main())
