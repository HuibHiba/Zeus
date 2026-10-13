"""Passuitkomst met tegenstanders in de buurt (StatsBomb 360-freeze-frames).

Koppelt elk pass-event (data/raw/events_*.pkl.gz) aan zijn 360-frame (data/raw360/frames_*.pkl.gz, via event-id) en berekent
druk-kenmerken: dichtstbijzijnde tegenstander bij de passer en bij de ontvanger, tegenstanders in de passlijn, aantal
tegenstanders dichtbij. Fit een logistisch model met en zonder die kenmerken op dezelfde wedstrijden (training = even match_id,
test = oneven) en schrijft calibration/pass_360_model.json. Let op: 360 bevat alleen spelers die in beeld waren.
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
OUT = ROOT / "calibration" / "pass_360_model.json"
YARD = 0.9144
CAP = 15.0  # meter: grens voor "geen tegenstander dichtbij"


def seg_dist(p, a, b):
    """Afstand van punten p (N x 2) tot het lijnstuk a-b."""
    ab = b - a
    denom = float(ab @ ab)
    if denom < 1e-9:
        return np.hypot(*(p - a).T)
    t = np.clip(((p - a) @ ab) / denom, 0, 1)
    proj = a + t[:, None] * ab
    return np.hypot(*(p - proj).T)


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
        ev = ev[(ev["type"] == "Pass") & (ev["period"] <= 4) & ev["pass_length"].notna() & ev["pass_end_location"].notna()]
        if ev.empty:
            continue
        opp = fr[(~fr["teammate"].astype(bool)) & (~fr["actor"].astype(bool))]
        idx = opp.groupby("id").indices
        locs = np.array(opp["location"].tolist(), dtype=float) if len(opp) else np.empty((0, 2))
        nvis = fr.groupby("id").size()
        for _, e in ev.iterrows():
            eid = e["id"]
            if eid not in nvis.index:
                continue
            a = np.array(e["location"][:2], dtype=float)
            b = np.array(e["pass_end_location"][:2], dtype=float)
            pts = locs[idx[eid]] if eid in idx else np.empty((0, 2))
            if len(pts):
                d_pass = np.hypot(*(pts - a).T) * YARD
                d_recv = np.hypot(*(pts - b).T) * YARD
                d_line = seg_dist(pts, a, b) * YARD
                f = (d_pass.min(), d_recv.min(), int((d_pass < 3).sum()), int((d_recv < 5).sum()), int((d_line < 2).sum()))
            else:
                f = (CAP, CAP, 0, 0, 0)
            ptype = e.get("pass_type")
            rows.append({
                "match": mid, "len": float(e["pass_length"]) * YARD, "fwd": float(np.cos(e["pass_angle"])) if pd.notna(e["pass_angle"]) else 0.0,
                "x": a[0] / 120.0, "high": e.get("pass_height") == "High Pass", "low": e.get("pass_height") == "Low Pass",
                "head": e.get("pass_body_part") == "Head", "cross": bool(e.get("pass_cross") is True),
                "setpiece": ptype in ("Corner", "Free Kick", "Goal Kick", "Throw-in"), "ok": pd.isna(e.get("pass_outcome")),
                "nearest_passer": min(f[0], CAP), "nearest_recv": min(f[1], CAP), "n_passer3": f[2], "n_recv5": f[3], "n_line2": f[4],
                "visible": int(nvis[eid])})
        print(mid, len(rows), flush=True)
    return pd.DataFrame(rows)


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    df = build()
    df = df[df["len"] > 0.5]
    y = df["ok"].to_numpy().astype(float)
    base = np.column_stack([np.ones(len(df)), df["len"], df["len"] ** 2 / 100.0, df["fwd"], df["high"], df["low"], df["head"], df["cross"], df["setpiece"], df["x"]]).astype(float)
    ext = np.column_stack([base, df["nearest_passer"], df["nearest_recv"], df["n_passer3"], df["n_recv5"], df["n_line2"]]).astype(float)
    train = (df["match"] % 2 == 0).to_numpy()
    wb, we = logistic_fit(base[train], y[train]), logistic_fit(ext[train], y[train])
    auc_b, auc_e = auc(y[~train], predict(base[~train], wb)), auc(y[~train], predict(ext[~train], we))
    names = ["const", "length_m", "length_m_sq", "forward_cos", "high_pass", "low_pass", "head", "cross", "set_piece", "x_rel",
             "nearest_opp_passer_m", "nearest_opp_receiver_m", "n_opp_within3m_passer", "n_opp_within5m_receiver", "n_opp_within2m_passline"]

    def rate(mask):
        return round(float(y[mask].mean()), 3) if mask.sum() > 300 else None

    tables = {
        "nearest_opp_passer_m": [{"bin": lab, "n": int(m.sum()), "geslaagd": rate(m)} for lab, m in
                                 [("<2", df.nearest_passer < 2), ("2-5", (df.nearest_passer >= 2) & (df.nearest_passer < 5)), ("5-10", (df.nearest_passer >= 5) & (df.nearest_passer < 10)), (">=10", df.nearest_passer >= 10)]],
        "nearest_opp_receiver_m": [{"bin": lab, "n": int(m.sum()), "geslaagd": rate(m)} for lab, m in
                                   [("<2", df.nearest_recv < 2), ("2-5", (df.nearest_recv >= 2) & (df.nearest_recv < 5)), ("5-10", (df.nearest_recv >= 5) & (df.nearest_recv < 10)), (">=10", df.nearest_recv >= 10)]],
        "opponents_in_passline_2m": [{"bin": lab, "n": int(m.sum()), "geslaagd": rate(m)} for lab, m in
                                     [("0", df.n_line2 == 0), ("1", df.n_line2 == 1), ("2", df.n_line2 == 2), (">=3", df.n_line2 >= 3)]],
    }
    res = {"bron": "StatsBomb open data met 360-freeze-frames (alleen intern onderzoek; commercieel gebruik onduidelijk)",
           "data": f"{df['match'].nunique()} wedstrijden met 360, {len(df)} passes; 360 bevat alleen spelers in beeld",
           "validatie": "training = even match_id, test = oneven", "geslaagd_totaal": round(float(y.mean()), 4),
           "auc_test_zonder_360": round(auc_b, 3), "auc_test_met_360": round(auc_e, 3),
           "coefficienten_met_360": dict(zip(names, [round(float(v), 5) for v in we])),
           "geslaagd_per_kenmerk": tables}
    OUT.write_text(json.dumps(res, indent=2, ensure_ascii=False), encoding="utf-8")
    print("klaar ->", OUT)
    print("passes %d | AUC zonder 360 %.3f | met 360 %.3f" % (len(df), auc_b, auc_e))


if __name__ == "__main__":
    sys.exit(main())
