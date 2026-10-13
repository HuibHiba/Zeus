"""Kansmodellen uit StatsBomb-events: passuitkomst, schot naar doelpunt en locatie van overtredingen en kaarten.

Leest data/raw/events_*.pkl.gz (eigen eerdere download, niet extern) en schrijft
- calibration/event_models.json: schot- en overtredingsmodel + het OUDE passmodel (met waarschuwing: uitkomst-lekkage)
- calibration/event_models_v2.json: passmodel zonder uitkomst-lekkage (alleen kenmerken van voor de trap, plus richting/afstand
  naar de BEOOGDE ontvanger) en de tabel 'geslaagd per beoogde lengte'.
Uitkomst-lekkage: bij mislukte passes zijn pass_length, pass_angle en pass_end_location de plek waar de bal eindigde (onderschept/uit),
niet het doel. De beoogde ontvanger (pass_recipient) staat bij ~74% van de mislukte passes en 100% van de geslaagde in de data; zijn positie komt
uit zijn Ball Receipt*-event. Het v2-model met doel-kenmerken wordt daarom alleen op passes met bekende beoogde ontvanger geschat en
vergeleken (op exact dezelfde passes) met het oude lekkende model; het model zonder doel-kenmerken gebruikt alle passes.
Validatie: testset = hele competitie-seizoenen (SB_TEST_GROUPS in zeus_common), niet gesplitst op match_id-pariteit.
Coordinaten in StatsBomb-yards worden omgerekend naar meters. Kick Off is overal uitgesloten; lengte > 0,5 m.
Bron: StatsBomb open data (bronvermelding met logo; commercieel gebruik onduidelijk: alleen intern onderzoek en kalibratie).
"""
import gzip
import pickle
import sys
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent))
from zeus_common import (LEAK_WARNING, LICENSE_STATSBOMB, SB_TEST_GROUPS, YARD, SkipLog, auc, auc_diff_report, auc_report,  # noqa: E402,F401
                         loc, logistic_fit, match_groups, pass_targets, predict, write_json)

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "data" / "raw"
OUT = ROOT / "calibration" / "event_models.json"
OUT_V2 = ROOT / "calibration" / "event_models_v2.json"
GOAL_X, GOAL_Y, HALF_GOAL = 120.0, 40.0, 4.0  # yards
SETPIECE = ["Corner", "Free Kick", "Goal Kick", "Throw-in"]
BINS = list(zip([0, 5, 10, 15, 20, 30, 40], [5, 10, 15, 20, 30, 40, 100]))


def get(df, name):
    return df[name] if name in df.columns else pd.Series([np.nan] * len(df), index=df.index)


def load_all(skip: SkipLog):
    passes, shots, fouls = [], [], []
    groups = match_groups()
    for path in sorted(RAW.glob("events_*.pkl.gz")):
        try:
            match_id = int(path.stem.split("_")[1].split(".")[0])
            with gzip.open(path, "rb") as f:
                ev = pickle.load(f)
            ev = ev[ev["period"] <= 4].copy()
        except Exception as exc:
            skip.skip(path.name, f"leesfout: {type(exc).__name__}")
            continue
        ev["match_id"] = match_id
        ev["group"] = groups.get(match_id, "onbekend")
        ev["x"] = loc(ev["location"], 0)
        ev["y"] = loc(ev["location"], 1)
        ty = ev["type"]
        p = ev[ty == "Pass"].copy()
        tg = pass_targets(ev, p)
        p["tx"], p["ty"] = tg["tx"], tg["ty"]
        passes.append(p)
        shots.append(ev[ty == "Shot"])
        fouls.append(ev[ty == "Foul Committed"])
        skip.ok()
    if not passes:
        return None, None, None
    return pd.concat(passes), pd.concat(shots), pd.concat(fouls)


def pass_frame(p: pd.DataFrame) -> pd.DataFrame:
    """Een rij per pass met lekkende (werkelijke) en lekvrije (voor de trap bekende / beoogde) kenmerken."""
    p = p[get(p, "pass_type").isna() | (get(p, "pass_type") != "Kick Off")]
    p = p[p["pass_length"].notna() & p["x"].notna() & p["y"].notna()].copy()
    d = pd.DataFrame(index=p.index)
    d["match"], d["group"] = p["match_id"], p["group"]
    d["ok"] = get(p, "pass_outcome").isna().astype(float)
    d["len_act"] = p["pass_length"] * YARD
    d["ang_act"] = get(p, "pass_angle")
    d["pressure"] = get(p, "under_pressure").fillna(False).astype(bool).astype(float)
    h = get(p, "pass_height").fillna("Ground Pass")
    d["high"], d["low"] = (h == "High Pass").astype(float), (h == "Low Pass").astype(float)
    d["height"] = h
    d["head"] = (get(p, "pass_body_part") == "Head").astype(float)
    d["cross"] = get(p, "pass_cross").fillna(False).astype(bool).astype(float)
    d["setpiece"] = get(p, "pass_type").isin(SETPIECE).astype(float)
    d["x_rel"] = (p["x"] / 120.0).clip(0, 1)
    d["y_cent"] = (p["y"] - 40.0).abs() / 40.0
    dx, dy = p["tx"] - p["x"], p["ty"] - p["y"]
    dist = np.hypot(dx, dy)
    d["len_tgt"] = dist * YARD
    d["fwd_tgt"] = (dx / dist.where(dist > 1e-6)).fillna(0.0)
    d["has_target"] = p["tx"].notna().to_numpy() & (d["len_tgt"] > 0.5).to_numpy()
    return d[d["len_act"] > 0.5]


def _fit_eval(name, X, y, train, groups, out):
    w, conv = logistic_fit(X[train], y[train], label=name)
    pt = predict(X[~train], w)
    out[name] = {**auc_report(y[~train], pt, groups[~train]), "geconvergeerd": bool(conv), "n_train": int(train.sum())}
    return w, pt


def _table(vals, y, pr):
    rows = []
    for lo, hi in BINS:
        m = (vals >= lo) & (vals < hi)
        if m.sum() < 200:
            continue
        rows.append({"lengte_m": f"{lo}-{hi}", "n": int(m.sum()), "geslaagd": round(float(y[m].mean()), 3),
                     "onder_druk": round(float(y[m & (pr == 1)].mean()), 3) if (m & (pr == 1)).sum() > 100 else None,
                     "zonder_druk": round(float(y[m & (pr == 0)].mean()), 3)})
    return rows


def pass_models(p: pd.DataFrame) -> tuple[dict, dict]:
    d = pass_frame(p)
    n_all = len(d)
    ang_ok = d["ang_act"].notna()
    test_all = d["group"].isin(SB_TEST_GROUPS).to_numpy()
    y_all = d["ok"].to_numpy()

    # --- oud (lekkend) model, alle passes met bekende hoek (geen fillna(0))
    dl = d[ang_ok]
    test_l = dl["group"].isin(SB_TEST_GROUPS).to_numpy()
    Xl = np.column_stack([np.ones(len(dl)), dl.len_act, dl.len_act ** 2 / 100.0, np.cos(dl.ang_act), dl.pressure, dl.high, dl.low, dl["head"], dl.cross, dl.setpiece, dl.x_rel])
    yl = dl["ok"].to_numpy()
    names_l = ["const", "length_m", "length_m_sq", "forward_cos", "under_pressure", "high_pass", "low_pass", "head", "cross", "set_piece", "x_rel"]
    aucs_l = {}
    w_l, _ = _fit_eval("oud_lekkend", Xl, yl, ~test_l, dl["match"].to_numpy(), aucs_l)
    legacy = {"waarschuwing": LEAK_WARNING, "n_passes": int(len(dl)), "geslaagd_totaal": round(float(yl.mean()), 4),
              "auc_test": aucs_l["oud_lekkend"]["auc"], "auc_test_ci95": aucs_l["oud_lekkend"]["ci95"],
              "coefficienten": dict(zip(names_l, [round(float(v), 5) for v in w_l])),
              "kalibratie_per_lengte": _table(dl["len_act"].to_numpy(), yl, dl["pressure"].to_numpy()),
              "geslaagd_per_hoogte": {h: round(float(yl[(dl.height == h).to_numpy()].mean()), 3) for h in ("Ground Pass", "Low Pass", "High Pass")},
              "n_uitgesloten_zonder_hoek": int(n_all - len(dl)),
              "opmerking": "logit(p geslaagd) = som(coefficient x kenmerk); length_m_sq is lengte in m kwadraat gedeeld door 100; x_rel = 0..1 in aanvalsrichting. "
                           "LEKKEND: lengte en hoek zijn de werkelijke baan van de bal."}

    # --- v2 zonder doel-kenmerken (alle passes): alleen voor de trap bekend
    names0 = ["const", "under_pressure", "high_pass", "low_pass", "head", "cross", "set_piece", "x_rel", "y_centraliteit"]
    X0 = np.column_stack([np.ones(n_all), d.pressure, d.high, d.low, d["head"], d.cross, d.setpiece, d.x_rel, d.y_cent])
    aucs = {}
    w0, _ = _fit_eval("v2_zonder_doel_alle_passes", X0, y_all, ~test_all, d["match"].to_numpy(), aucs)

    # --- v2 met beoogde ontvanger, op passes met bekende beoogde ontvanger (en het oude model op exact dezelfde passes)
    ds = d[d["has_target"] & ang_ok]
    test_s = ds["group"].isin(SB_TEST_GROUPS).to_numpy()
    ys = ds["ok"].to_numpy()
    ms = ds["match"].to_numpy()
    names2 = ["const", "length_m_beoogd", "length_m_beoogd_sq", "forward_cos_beoogd", "under_pressure", "high_pass", "low_pass", "head", "cross", "set_piece", "x_rel", "y_centraliteit"]
    X2 = np.column_stack([np.ones(len(ds)), ds.len_tgt, ds.len_tgt ** 2 / 100.0, ds.fwd_tgt, ds.pressure, ds.high, ds.low, ds["head"], ds.cross, ds.setpiece, ds.x_rel, ds.y_cent])
    X0s = np.column_stack([np.ones(len(ds)), ds.pressure, ds.high, ds.low, ds["head"], ds.cross, ds.setpiece, ds.x_rel, ds.y_cent])
    XLs = np.column_stack([np.ones(len(ds)), ds.len_act, ds.len_act ** 2 / 100.0, np.cos(ds.ang_act), ds.pressure, ds.high, ds.low, ds["head"], ds.cross, ds.setpiece, ds.x_rel])
    w2, p2 = _fit_eval("v2_beoogde_ontvanger", X2, ys, ~test_s, ms, aucs)
    _fit_eval("v2_zonder_doel_zelfde_passes", X0s, ys, ~test_s, ms, aucs)
    _, pls = _fit_eval("oud_lekkend_zelfde_passes", XLs, ys, ~test_s, ms, aucs)
    pr2 = ds["pressure"].to_numpy()
    tl = ds["len_tgt"].to_numpy()

    # onzekerheid van geslaagd-per-beoogde-lengte: bootstrap per wedstrijd
    rng = np.random.default_rng(20261013)
    uniq = np.unique(ms)
    per_match = {mid: np.array([[((tl[ms == mid] >= lo) & (tl[ms == mid] < hi)).sum(),
                                 ys[ms == mid][(tl[ms == mid] >= lo) & (tl[ms == mid] < hi)].sum()] for lo, hi in BINS]) for mid in uniq}
    boots = []
    for _ in range(200):
        tot = sum(per_match[m] for m in rng.choice(uniq, len(uniq)))
        with np.errstate(invalid="ignore", divide="ignore"):
            boots.append(tot[:, 1] / tot[:, 0])
    boots = np.array(boots)
    tab_tgt = _table(tl, ys, pr2)
    labels = [f"{lo}-{hi}" for lo, hi in BINS]
    for row in tab_tgt:
        lo_, hi_ = np.nanpercentile(boots[:, labels.index(row["lengte_m"])], [2.5, 97.5])
        row["ci95"] = [round(float(lo_), 3), round(float(hi_), 3)]

    v2 = {
        "bron": "StatsBomb open data (alleen intern onderzoek)",
        "licentie_status": LICENSE_STATSBOMB,
        "doel": "Kans dat een pass geslaagd is VOOR de trap: alleen kenmerken die bij het trappen bekend zijn plus richting/afstand naar de beoogde ontvanger. Geen werkelijke eindpositie, lengte of hoek van de bal.",
        "validatie": "training = competitie-seizoenen buiten SB_TEST_GROUPS, test = " + ", ".join(sorted(SB_TEST_GROUPS)) + "; AUC-interval = bootstrap 200x per wedstrijd, seed 20261013",
        "beoogde_ontvanger": "pass_recipient met positie uit het gekoppelde Ball Receipt*-event van die speler (bij mislukte passes de plek van de ontvanger, niet van de bal). "
                             "Ontbreekt bij ~26% van de mislukte passes (en 0% van de geslaagde): het model met doel-kenmerken geldt dus voor passes MET bekende beoogde ontvanger en is licht optimistisch over het slagingspercentage.",
        "n_passes_alle": int(n_all), "n_passes_met_beoogde_ontvanger": int(len(ds)),
        "geslaagd_totaal_met_beoogde_ontvanger": round(float(ys.mean()), 4), "geslaagd_totaal_alle": round(float(y_all.mean()), 4),
        "auc_test": aucs,
        "auc_verschil_lekkend_naar_v2_zelfde_passes": auc_diff_report(ys[test_s], pls, p2, ms[test_s]),
        "coefficienten_beoogde_ontvanger": dict(zip(names2, [round(float(v), 5) for v in w2])),
        "coefficienten_zonder_doel_alle_passes": dict(zip(names0, [round(float(v), 5) for v in w0])),
        "geslaagd_per_beoogde_lengte": tab_tgt,
        "geslaagd_per_werkelijke_lengte_zelfde_passes": _table(ds["len_act"].to_numpy(), ys, pr2),
        "opmerking": "logit(p geslaagd) = som(coefficient x kenmerk); length_m_beoogd_sq is beoogde lengte in m kwadraat gedeeld door 100; forward_cos_beoogd = cos van de hoek tussen passer->beoogde ontvanger en de aanvalsrichting; "
                     "y_centraliteit = |y-40|/40 van de passer. Kick Off uitgesloten, lengte > 0,5 m.",
    }
    return legacy, v2


def shot_model(s: pd.DataFrame) -> dict:
    s = s[(get(s, "shot_type") != "Penalty") & s["x"].notna()]
    n_voor = len(s)
    s = s[get(s, "shot_statsbomb_xg").notna()].copy()  # zelfde schoten voor eigen model en StatsBomb xG (geen nan_to_num -> 0)
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
    test = s["group"].isin(SB_TEST_GROUPS).to_numpy()
    w, conv = logistic_fit(X[~test], y[~test], label="schot")
    p_test = predict(X[test], w)
    sb = s["shot_statsbomb_xg"].to_numpy(dtype=float)
    g = s["match_id"].to_numpy()[test]
    r_own, r_sb = auc_report(y[test], p_test, g), auc_report(y[test], sb[test], g)
    res = {"n_schoten_zonder_strafschop": int(len(s)), "n_zonder_statsbomb_xg_uitgesloten": int(n_voor - len(s)),
           "doelpuntkans_totaal": round(float(y.mean()), 4),
           "auc_test": r_own["auc"], "auc_test_ci95": r_own["ci95"],
           "auc_statsbomb_xg_zelfde_testset": r_sb["auc"], "auc_statsbomb_xg_ci95": r_sb["ci95"],
           "correlatie_met_statsbomb_xg_test": round(float(np.corrcoef(p_test, sb[test])[0, 1]), 3),
           "geconvergeerd": bool(conv),
           "coefficienten": dict(zip(names, [round(float(v), 5) for v in w])),
           "opmerking": "logit(p doelpunt) = som(coefficient x kenmerk); afstand in meters, goal_angle_rad is de hoek waaronder het doel zichtbaar is"}
    curve = []
    d = dist_m.to_numpy()
    for lo, hi in [(0, 6), (6, 10), (10, 16), (16, 22), (22, 30), (30, 60)]:
        m = (d >= lo) & (d < hi)
        if m.sum() >= 100:
            curve.append({"afstand_m": f"{lo}-{hi}", "n": int(m.sum()), "doelpunt": round(float(y[m].mean()), 3),
                          "gem_statsbomb_xg": round(float(np.mean(sb[m])), 3)})
    res["kalibratie_per_afstand"] = curve
    return res


def foul_model(f: pd.DataFrame) -> dict:
    f = f[f["x"].notna()].copy()
    card = get(f, "foul_committed_card")
    got_card = card.isin(["Yellow Card", "Second Yellow", "Red Card"])
    rel_x = (f["x"] / 120.0).clip(0, 1)
    out = {"n_overtredingen": int(len(f)), "kaart_per_overtreding": round(float(got_card.mean()), 4), "per_derde": [],
           "definitie_kaart": "alleen foul_committed_card (Yellow Card, Second Yellow, Red Card); kaarten uit Bad Behaviour tellen NIET mee, "
                              "dus dit is lager dan cards_total/fouls_committed in data/reference/team_match_reference.csv (dat Bad Behaviour wel telt)"}
    for name, lo, hi in [("eigen derde (x 0-0,33)", 0, 1 / 3), ("middenderde", 1 / 3, 2 / 3), ("aanvallend derde", 2 / 3, 1.0001)]:
        m = (rel_x >= lo) & (rel_x < hi)
        out["per_derde"].append({"zone": name, "aandeel_overtredingen": round(float(m.mean()), 3), "kaart_per_overtreding": round(float(got_card[m].mean()), 3)})
    out["strafschop_per_overtreding"] = round(float(get(f, "foul_committed_penalty").fillna(False).astype(bool).mean()), 4)
    out["opmerking"] = "x in aanvalsrichting van het team dat de overtreding beging (0 = eigen doellijn)"
    return out


def main() -> int:
    skip = SkipLog("fit_event_models")
    p, s, f = load_all(skip)
    code = skip.finish()
    if p is None:
        return 1
    legacy, v2 = pass_models(p)
    common = {"bron": "StatsBomb open data (alleen intern onderzoek en kalibratie; niet in de Steam-build tot de licentie is bevestigd)",
              "licentie_status": LICENSE_STATSBOMB,
              "data": "763 wedstrijden: Premier League 2015/16 + recente competities en toernooien (events met period <= 4)",
              "validatie": "training = competitie-seizoenen buiten SB_TEST_GROUPS, test = " + ", ".join(sorted(SB_TEST_GROUPS)) + " (niet meer op match_id-pariteit); AUC-intervallen: bootstrap 200x per wedstrijd, seed 20261013"}
    res = {**common, "waarschuwing": "Het onderdeel pass_geslaagd bevat uitkomst-lekkage (zie pass_geslaagd.waarschuwing) en is niet als kans voor de pass bruikbaar: gebruik event_models_v2.json.",
           "pass_geslaagd": legacy, "schot_doelpunt": shot_model(s), "overtreding_locatie_kaart": foul_model(f)}
    write_json(OUT, res)
    write_json(OUT_V2, {**common, "pass_geslaagd_v2": v2})
    print("klaar ->", OUT, OUT_V2)
    print("oud (lekkend): n=%d, geslaagd %.3f, AUC test %s" % (legacy["n_passes"], legacy["geslaagd_totaal"], legacy["auc_test"]))
    print("v2 AUC:", {k: (v["auc"], v["ci95"]) for k, v in v2["auc_test"].items()})
    print("verschil lekkend->v2 (zelfde passes):", v2["auc_verschil_lekkend_naar_v2_zelfde_passes"])
    sh = res["schot_doelpunt"]
    print("schot: n=%d, doelpunt %.3f, AUC test %.3f (StatsBomb xG %.3f)" % (sh["n_schoten_zonder_strafschop"], sh["doelpuntkans_totaal"], sh["auc_test"], sh["auc_statsbomb_xg_zelfde_testset"]))
    return code


if __name__ == "__main__":
    sys.exit(main())
