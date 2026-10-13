"""Passuitkomst met tegenstanders in de buurt (StatsBomb 360-freeze-frames), zonder uitkomst-lekkage.

Koppelt elk pass-event (data/raw/events_*.pkl.gz) aan zijn 360-frame (data/raw360/frames_*.pkl.gz, via event-id).
Het frame bevat de posities op het moment van de trap. Het doel van de pass is de BEOOGDE ontvanger (positie uit zijn
Ball Receipt*-event, zie zeus_common.pass_targets), niet het eindpunt van de bal: bij mislukte passes is pass_length/pass_angle/
pass_end_location de plek waar de bal eindigde (onderschept/uit) en lekt de uitkomst. De drukkenmerken worden daarom
berekend ten opzichte van de beoogde ontvanger. Alleen passes met bekende beoogde ontvanger doen mee (bij ca. 26% van de
mislukte passes ontbreekt hij; het model is daardoor licht optimistisch over het slagingspercentage).

Schrijft calibration/pass_360_model_v2.json met AUC (bootstrap-interval per wedstrijd) voor: zonder doel, met doel, met 360,
en het oude lekkende model op exact dezelfde passes. Testset = hele competitie-seizoenen (SB_TEST_GROUPS).
Let op: 360 bevat alleen spelers die in beeld waren.
Bron: StatsBomb open data (alleen intern onderzoek; commercieel gebruik onduidelijk).
"""
import gzip
import pickle
import sys
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent))
from zeus_common import (LICENSE_STATSBOMB, SB_TEST_GROUPS, YARD, SkipLog, auc_diff_report, auc_report, loc,  # noqa: E402
                         logistic_fit, match_groups, num, pass_targets, predict, write_json)

ROOT = Path(__file__).resolve().parent.parent
RAW, RAW360 = ROOT / "data" / "raw", ROOT / "data" / "raw360"
OUT = ROOT / "calibration" / "pass_360_model_v2.json"
CAP = 15.0  # meter: grens voor "geen tegenstander dichtbij"
SETPIECE = ("Corner", "Free Kick", "Goal Kick", "Throw-in")


def seg_dist(p, a, b):
    """Afstand van punten p (N x 2) tot het lijnstuk a-b."""
    ab = b - a
    denom = float(ab @ ab)
    if denom < 1e-9:
        return np.hypot(*(p - a).T)
    t = np.clip(((p - a) @ ab) / denom, 0, 1)
    proj = a + t[:, None] * ab
    return np.hypot(*(p - proj).T)


def build(skip: SkipLog) -> pd.DataFrame:
    groups = match_groups()
    rows = []
    for fpath in sorted(RAW360.glob("frames_*.pkl.gz")):
        try:
            mid = int(fpath.stem.split("_")[1].split(".")[0])
            epath = RAW / f"events_{mid}.pkl.gz"
            if not epath.exists():
                skip.skip(fpath.name, "geen events-bestand")
                continue
            with gzip.open(fpath, "rb") as f:
                fr = pickle.load(f)
            with gzip.open(epath, "rb") as f:
                ev = pickle.load(f)
            ev = ev[ev["period"] <= 4].copy()
            ev["x"], ev["y"] = loc(ev["location"], 0), loc(ev["location"], 1)
            p = ev[(ev["type"] == "Pass") & ev["pass_length"].notna() & ev["x"].notna() & ev["y"].notna()].copy()
            if "pass_type" in p.columns:
                p = p[p["pass_type"] != "Kick Off"]
            tg = pass_targets(ev, p)
            p["tx"], p["ty"] = tg["tx"], tg["ty"]
            p = p[p["tx"].notna()]
            if p.empty:
                skip.skip(fpath.name, "geen passes met bekende beoogde ontvanger")
                continue
            opp = fr[(~fr["teammate"].astype(bool)) & (~fr["actor"].astype(bool))]
            idx = opp.groupby("id").indices
            locs = np.array(opp["location"].tolist(), dtype=float) if len(opp) else np.empty((0, 2))
            nvis = fr.groupby("id").size()
        except Exception as exc:
            skip.skip(fpath.name, f"leesfout: {type(exc).__name__}")
            continue
        group = groups.get(mid, "onbekend")
        for _, e in p.iterrows():
            eid = e["id"]
            if eid not in nvis.index:
                continue
            a = np.array([e["x"], e["y"]], dtype=float)
            b = np.array([e["tx"], e["ty"]], dtype=float)
            dist = float(np.hypot(*(b - a))) * YARD
            if dist <= 0.5 or e["pass_length"] * YARD <= 0.5:
                continue
            pts = locs[idx[eid]] if eid in idx else np.empty((0, 2))
            if len(pts):
                d_pass = np.hypot(*(pts - a).T) * YARD
                d_recv = np.hypot(*(pts - b).T) * YARD
                d_line = seg_dist(pts, a, b) * YARD
                f = (d_pass.min(), d_recv.min(), int((d_pass < 3).sum()), int((d_recv < 5).sum()), int((d_line < 2).sum()))
            else:
                f = (CAP, CAP, 0, 0, 0)
            height = e.get("pass_height")
            ptype = e.get("pass_type")
            ang = e.get("pass_angle")
            rows.append({
                "match": mid, "group": group, "ok": float(pd.isna(e.get("pass_outcome"))),
                "len_tgt": dist, "fwd_tgt": float((b[0] - a[0]) / (dist / YARD)),
                "len_act": float(e["pass_length"]) * YARD, "cos_act": float(np.cos(ang)) if pd.notna(ang) else np.nan,
                "x": a[0] / 120.0, "y_cent": abs(a[1] - 40.0) / 40.0,
                "pressure": float(bool(e.get("under_pressure") is True)),
                "high": float(height == "High Pass"), "low": float(height == "Low Pass"),
                "head": float(e.get("pass_body_part") == "Head"), "cross": float(bool(e.get("pass_cross") is True)),
                "setpiece": float(ptype in SETPIECE),
                "nearest_passer": min(f[0], CAP), "nearest_recv": min(f[1], CAP), "n_passer3": f[2], "n_recv5": f[3], "n_line2": f[4],
                "visible": int(nvis[eid])})
        skip.ok()
        print(mid, len(rows), flush=True)
    return pd.DataFrame(rows)


def main() -> int:
    skip = SkipLog("fit_pass_360")
    df = build(skip)
    code = skip.finish()
    if df.empty:
        return 1
    df = df[df["cos_act"].notna()]
    y = df["ok"].to_numpy()
    test = df["group"].isin(SB_TEST_GROUPS).to_numpy()
    if test.sum() == 0 or (~test).sum() == 0:
        print("FOUT: geen train/test-splitsing mogelijk (SB_TEST_GROUPS komt niet voor in de 360-wedstrijden)")
        return 1
    ones = np.ones(len(df))
    common = [df.pressure, df.high, df.low, df["head"], df.cross, df.setpiece, df.x, df.y_cent]
    X0 = np.column_stack([ones] + common)                                              # niets over het doel
    Xt = np.column_stack([ones, df.len_tgt, df.len_tgt ** 2 / 100.0, df.fwd_tgt] + common)  # + beoogde ontvanger
    X3 = np.column_stack([Xt, df.nearest_passer, df.nearest_recv, df.n_passer3, df.n_recv5, df.n_line2])  # + 360-druk
    XL = np.column_stack([ones, df.len_act, df.len_act ** 2 / 100.0, df.cos_act] + common)  # OUD, lekkend, ter vergelijking
    m = df["match"].to_numpy()
    aucs, preds, coefs = {}, {}, {}
    for name, X in (("v2_zonder_doel", X0), ("v2_met_beoogde_ontvanger", Xt), ("v2_met_360", X3), ("oud_lekkend_zelfde_passes", XL)):
        w, ok = logistic_fit(X[~test], y[~test], label=name)
        pt = predict(X[test], w)
        aucs[name] = {**auc_report(y[test], pt, m[test]), "geconvergeerd": bool(ok)}
        preds[name], coefs[name] = pt, w
    names = ["const", "length_m_beoogd", "length_m_beoogd_sq", "forward_cos_beoogd", "under_pressure", "high_pass", "low_pass", "head",
             "cross", "set_piece", "x_rel", "y_centraliteit", "nearest_opp_passer_m", "nearest_opp_receiver_m",
             "n_opp_within3m_passer", "n_opp_within5m_receiver", "n_opp_within2m_passline"]

    def rate(mask):
        return num(y[mask].mean(), 3) if mask.sum() > 300 else None

    def table(col, bins):
        v = df[col].to_numpy()
        return [{"bin": lab, "n": int(mk.sum()), "geslaagd": rate(mk)} for lab, mk in
                [(lab, (v >= lo) & (v < hi)) for lab, lo, hi in bins]]

    tables = {
        "nearest_opp_passer_m": table("nearest_passer", [("<2", -1, 2), ("2-5", 2, 5), ("5-10", 5, 10), (">=10", 10, 1e9)]),
        "nearest_opp_receiver_m": table("nearest_recv", [("<2", -1, 2), ("2-5", 2, 5), ("5-10", 5, 10), (">=10", 10, 1e9)]),
        "opponents_in_passline_2m": table("n_line2", [("0", -1, 1), ("1", 1, 2), ("2", 2, 3), (">=3", 3, 1e9)]),
    }
    res = {"bron": "StatsBomb open data met 360-freeze-frames (alleen intern onderzoek)", "licentie_status": LICENSE_STATSBOMB,
           "doel": "Kans dat een pass geslaagd is VOOR de trap: kenmerken van voor de trap, de BEOOGDE ontvanger (positie uit zijn "
                   "Ball Receipt*-event) en de 360-druk daarop. Geen werkelijke eindpositie, lengte of hoek van de bal.",
           "data": f"{df['match'].nunique()} wedstrijden met 360, {len(df)} passes met bekende beoogde ontvanger; 360 bevat alleen spelers in beeld",
           "validatie": "training = competitie-seizoenen buiten SB_TEST_GROUPS, test = " + ", ".join(sorted(SB_TEST_GROUPS))
                        + "; AUC-interval = bootstrap 200x per wedstrijd, seed 20261013",
           "voorbehoud": "Alleen passes met bekende beoogde ontvanger (ontbreekt bij ca. 26% van de mislukte passes): licht optimistisch over het "
                         "slagingspercentage. 360-kenmerken zijn zwakker dan bij een volledig veld."
                         " De positie van de beoogde ontvanger komt uit zijn Ball Receipt*-event (op het moment van ontvangst, dus iets na de trap): de afstand tot tegenstanders kan daardoor licht optimistisch zijn.",
           "geslaagd_totaal": num(y.mean(), 4), "auc_test": aucs,
           "auc_verschil_lekkend_naar_met_360": auc_diff_report(y[test], preds["oud_lekkend_zelfde_passes"], preds["v2_met_360"], m[test]),
           "auc_verschil_zonder_naar_met_360": auc_diff_report(y[test], preds["v2_met_beoogde_ontvanger"], preds["v2_met_360"], m[test]),
           "coefficienten_met_360": dict(zip(names, [num(v, 5) for v in coefs["v2_met_360"]])),
           "geslaagd_per_kenmerk": tables}
    write_json(OUT, res)
    print("klaar ->", OUT)
    print("passes %d | AUC testset: " % len(df) + " | ".join(f"{k} {v['auc']}" for k, v in aucs.items()))
    return code


if __name__ == "__main__":
    sys.exit(main())
