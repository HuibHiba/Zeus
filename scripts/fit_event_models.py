"""Kansmodellen uit StatsBomb-events: passuitkomst, schot naar doelpunt en locatie van overtredingen en kaarten.

Leest data/raw/events_*.pkl.gz (eigen eerdere download, niet extern) en schrijft calibration/event_models.json met
logistische-regressiecoefficienten (eigen Newton-methode in numpy), gemeten kalibratiecurves en AUC op een aparte testset
(wedstrijden met oneven match_id; training = even). Coordinaten in StatsBomb-yards worden omgerekend naar meters.
Bron: StatsBomb open data (bronvermelding met logo; commercieel gebruik onduidelijk: alleen intern onderzoek en kalibratie).
"""
import gzip
import json
import pickle
import sys
from pathlib import Path

import numpy as np
import pandas as pd

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "data" / "raw"
OUT = ROOT / "calibration" / "event_models.json"
YARD = 0.9144
GOAL_X, GOAL_Y, HALF_GOAL = 120.0, 40.0, 4.0  # yards


def logistic_fit(X: np.ndarray, y: np.ndarray, ridge: float = 1e-4, iters: int = 60) -> np.ndarray:
    w = np.zeros(X.shape[1])
    for _ in range(iters):
        z = np.clip(X @ w, -30, 30)
        p = 1 / (1 + np.exp(-z))
        g = X.T @ (p - y) + ridge * w
        h = (X * (p * (1 - p))[:, None]).T @ X + ridge * np.eye(X.shape[1])
        step = np.linalg.solve(h, g)
        w -= step
        if np.max(np.abs(step)) < 1e-8:
            break
    return w


def predict(X: np.ndarray, w: np.ndarray) -> np.ndarray:
    return 1 / (1 + np.exp(-np.clip(X @ w, -30, 30)))


def auc(y: np.ndarray, p: np.ndarray) -> float:
    order = np.argsort(p)
    ranks = np.empty(len(p))
    ranks[order] = np.arange(1, len(p) + 1)
    n1 = y.sum()
    n0 = len(y) - n1
    return float((ranks[y == 1].sum() - n1 * (n1 + 1) / 2) / (n1 * n0))


def loc(series, i):
    return series.map(lambda v: v[i] if isinstance(v, (list, tuple)) and len(v) > i else np.nan)


def load_all():
    passes, shots, fouls = [], [], []
    for path in sorted(RAW.glob("events_*.pkl.gz")):
        match_id = int(path.stem.split("_")[1].split(".")[0])
        with gzip.open(path, "rb") as f:
            ev = pickle.load(f)
        ev = ev[ev["period"] <= 4].copy()
        ev["match_id"] = match_id
        ev["x"] = loc(ev["location"], 0)
        ev["y"] = loc(ev["location"], 1)
        ty = ev["type"]
        passes.append(ev[ty == "Pass"])
        shots.append(ev[ty == "Shot"])
        fouls.append(ev[ty == "Foul Committed"])
    return pd.concat(passes), pd.concat(shots), pd.concat(fouls)


def get(df, name):
    return df[name] if name in df.columns else pd.Series([np.nan] * len(df), index=df.index)


def pass_model(p: pd.DataFrame) -> dict:
    p = p[get(p, "pass_type").isna() | ~get(p, "pass_type").isin(["Kick Off"])].copy()
    p = p[p["pass_length"].notna() & p["x"].notna()]
    length_m = p["pass_length"] * YARD
    forward = np.cos(p["pass_angle"].fillna(0))
    pressure = get(p, "under_pressure").fillna(False).astype(bool).astype(float)
    height = get(p, "pass_height").fillna("Ground Pass")
    head = (get(p, "pass_body_part") == "Head").astype(float)
    cross = get(p, "pass_cross").fillna(False).astype(bool).astype(float)
    ptype = get(p, "pass_type")
    setpiece = ptype.isin(["Corner", "Free Kick", "Goal Kick", "Throw-in"]).astype(float)
    attack_third = (p["x"] / 120.0).clip(0, 1)
    names = ["const", "length_m", "length_m_sq", "forward_cos", "under_pressure", "high_pass", "low_pass", "head", "cross", "set_piece", "x_rel"]
    X = np.column_stack([np.ones(len(p)), length_m, length_m ** 2 / 100.0, forward, pressure, (height == "High Pass").astype(float),
                         (height == "Low Pass").astype(float), head, cross, setpiece, attack_third])
    y = get(p, "pass_outcome").isna().astype(float).to_numpy()
    train = (p["match_id"] % 2 == 0).to_numpy()
    w = logistic_fit(X[train], y[train])
    a = auc(y[~train], predict(X[~train], w))
    bins = [0, 5, 10, 15, 20, 30, 40, 100]
    curve = []
    lm = length_m.to_numpy()
    pr = pressure.to_numpy()
    for lo, hi in zip(bins[:-1], bins[1:]):
        m = (lm >= lo) & (lm < hi)
        if m.sum() < 200:
            continue
        curve.append({"lengte_m": f"{lo}-{hi}", "n": int(m.sum()), "geslaagd": round(float(y[m].mean()), 3),
                      "onder_druk": round(float(y[m & (pr == 1)].mean()), 3) if (m & (pr == 1)).sum() > 100 else None,
                      "zonder_druk": round(float(y[m & (pr == 0)].mean()), 3)})
    by_height = {h: round(float(y[(height == h).to_numpy()].mean()), 3) for h in ("Ground Pass", "Low Pass", "High Pass")}
    return {"n_passes": int(len(p)), "geslaagd_totaal": round(float(y.mean()), 4), "auc_test": round(a, 3),
            "coefficienten": dict(zip(names, [round(float(v), 5) for v in w])), "kalibratie_per_lengte": curve,
            "geslaagd_per_hoogte": by_height,
            "opmerking": "logit(p geslaagd) = som(coefficient x kenmerk); length_m_sq is lengte in m kwadraat gedeeld door 100; x_rel = 0..1 in aanvalsrichting"}


def shot_model(s: pd.DataFrame) -> dict:
    s = s[(get(s, "shot_type") != "Penalty") & s["x"].notna()].copy()
    dx = (GOAL_X - s["x"]).clip(lower=0.1)
    dy = s["y"] - GOAL_Y
    dist_m = np.hypot(dx, dy) * YARD
    a1, a2 = np.arctan2(HALF_GOAL - dy, dx), np.arctan2(-HALF_GOAL - dy, dx)
    angle = np.abs(a1 - a2)  # hoek waaronder het doel zichtbaar is
    head = (get(s, "shot_body_part") == "Head").astype(float)
    first = get(s, "shot_first_time").fillna(False).astype(bool).astype(float)
    pressure = get(s, "under_pressure").fillna(False).astype(bool).astype(float)
    fk = (get(s, "shot_type") == "Free Kick").astype(float)
    names = ["const", "dist_m", "dist_m_sq", "goal_angle_rad", "head", "first_time", "under_pressure", "free_kick"]
    X = np.column_stack([np.ones(len(s)), dist_m, dist_m ** 2 / 100.0, angle, head, first, pressure, fk])
    y = (get(s, "shot_outcome") == "Goal").astype(float).to_numpy()
    train = (s["match_id"] % 2 == 0).to_numpy()
    w = logistic_fit(X[train], y[train])
    p_test = predict(X[~train], w)
    sb = get(s, "shot_statsbomb_xg").to_numpy()
    res = {"n_schoten_zonder_strafschop": int(len(s)), "doelpuntkans_totaal": round(float(y.mean()), 4), "auc_test": round(auc(y[~train], p_test), 3),
           "auc_statsbomb_xg_zelfde_testset": round(auc(y[~train], np.nan_to_num(sb[~train])), 3),
           "correlatie_met_statsbomb_xg_test": round(float(np.corrcoef(p_test, np.nan_to_num(sb[~train]))[0, 1]), 3),
           "coefficienten": dict(zip(names, [round(float(v), 5) for v in w])),
           "opmerking": "logit(p doelpunt) = som(coefficient x kenmerk); afstand in meters, goal_angle_rad is de hoek waaronder het doel zichtbaar is"}
    curve = []
    d = dist_m.to_numpy()
    for lo, hi in [(0, 6), (6, 10), (10, 16), (16, 22), (22, 30), (30, 60)]:
        m = (d >= lo) & (d < hi)
        if m.sum() >= 100:
            curve.append({"afstand_m": f"{lo}-{hi}", "n": int(m.sum()), "doelpunt": round(float(y[m].mean()), 3),
                          "gem_statsbomb_xg": round(float(np.nanmean(sb[m])), 3)})
    res["kalibratie_per_afstand"] = curve
    return res


def foul_model(f: pd.DataFrame) -> dict:
    f = f[f["x"].notna()].copy()
    card = get(f, "foul_committed_card")
    got_card = card.isin(["Yellow Card", "Second Yellow", "Red Card"])
    rel_x = (f["x"] / 120.0).clip(0, 1)
    out = {"n_overtredingen": int(len(f)), "kaart_per_overtreding": round(float(got_card.mean()), 4), "per_derde": []}
    for name, lo, hi in [("eigen derde (x 0-0,33)", 0, 1 / 3), ("middenderde", 1 / 3, 2 / 3), ("aanvallend derde", 2 / 3, 1.0001)]:
        m = (rel_x >= lo) & (rel_x < hi)
        out["per_derde"].append({"zone": name, "aandeel_overtredingen": round(float(m.mean()), 3), "kaart_per_overtreding": round(float(got_card[m].mean()), 3)})
    out["strafschop_per_overtreding"] = round(float(get(f, "foul_committed_penalty").fillna(False).astype(bool).mean()), 4)
    out["opmerking"] = "x in aanvalsrichting van het team dat de overtreding beging (0 = eigen doellijn)"
    return out


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    p, s, f = load_all()
    res = {"bron": "StatsBomb open data (alleen intern onderzoek en kalibratie; niet in de Steam-build tot de licentie is bevestigd)",
           "data": "763 wedstrijden: Premier League 2015/16 + recente competities en toernooien (events met period <= 4)",
           "validatie": "training = wedstrijden met even match_id, test = oneven",
           "pass_geslaagd": pass_model(p), "schot_doelpunt": shot_model(s), "overtreding_locatie_kaart": foul_model(f)}
    OUT.write_text(json.dumps(res, indent=2, ensure_ascii=False), encoding="utf-8")
    print("klaar ->", OUT)
    print("pass: n=%d, geslaagd %.3f, AUC test %.3f" % (res["pass_geslaagd"]["n_passes"], res["pass_geslaagd"]["geslaagd_totaal"], res["pass_geslaagd"]["auc_test"]))
    print("schot: n=%d, doelpunt %.3f, AUC test %.3f (StatsBomb xG %.3f)" % (res["schot_doelpunt"]["n_schoten_zonder_strafschop"], res["schot_doelpunt"]["doelpuntkans_totaal"], res["schot_doelpunt"]["auc_test"], res["schot_doelpunt"]["auc_statsbomb_xg_zelfde_testset"]))


if __name__ == "__main__":
    sys.exit(main())
