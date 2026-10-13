"""Kansmodellen uit de Wyscout public dataset (CC BY 4.0): passuitkomst, schot naar doelpunt, overtreding en duelen.

Leest data/external/wyscout/events.zip (alleen lezen, extern) en schrijft calibration/wyscout_models.json. Zelfde aanpak als
fit_event_models.py (eigen logistische regressie, validatie op een aparte testset: hele competities, WY_TEST_GROUPS),
maar op een bron met een licentie die (volgens de metadata, nog te bevestigen) commercieel gebruik toestaat. Wyscout-posities zijn 0-100 (% van het veld, in aanvalsrichting);
omgerekend naar meters (105 x 68). Wyscout heeft geen 'onder druk'-vlag, dus die ontbreekt in de passmodellen.
Uitkomst-lekkage: de tweede positie van een Wyscout-pass is bij mislukte passes de plek waar de bal eindigde, en er is geen beoogde
ontvanger. Lengte en richting lekken dus de uitkomst. `pass_geslaagd` gebruikt daarom alleen kenmerken van voor de trap (subtype,
positie van de trap); het oude model met lengte en richting staat apart als `pass_geslaagd_oud_lekkend` (niet als kans bruikbaar).
Bron: Pappalardo e.a., Scientific Data 2019, CC BY 4.0 (naamsvermelding verplicht).
"""
import json
import sys
import zipfile
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent))
from zeus_common import LEAK_WARNING, LICENSE_WYSCOUT, WY_TEST_GROUPS, auc_report, logistic_fit, predict, write_json  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "external" / "wyscout" / "events.zip"
OUT = ROOT / "calibration" / "wyscout_models.json"
NAMES = {"England": "Premier League 2017/18", "Spain": "La Liga 2017/18", "Italy": "Serie A 2017/18",
         "Germany": "Bundesliga 2017/18", "France": "Ligue 1 2017/18", "World_Cup": "WK 2018", "European_Championship": "EK 2016"}
L, W = 105.0, 68.0


def collect():
    zf = zipfile.ZipFile(SRC)
    passes, shots, fouls, duels = [], [], [], []
    for key, label in NAMES.items():
        for e in json.loads(zf.read(f"events_{key}.json")):
            if e.get("matchPeriod") == "P":  # strafschoppenreeks uitsluiten
                continue
            pos = e.get("positions") or []
            if not pos:
                continue
            tags = {t["id"] for t in e.get("tags", [])}
            ev, sub = e["eventId"], e["subEventId"]
            x0, y0 = pos[0]["x"], pos[0]["y"]
            if ev == 8 and len(pos) >= 2 and sub != 81:
                x1, y1 = pos[1]["x"], pos[1]["y"]
                dx, dy = (x1 - x0) * L / 100.0, (y1 - y0) * W / 100.0
                ln = float(np.hypot(dx, dy))
                passes.append((e["matchId"], label, sub, ln, dx / ln if ln > 0.01 else 0.0, x0 / 100.0, 1801 in tags))
            elif ev == 10 or (ev == 3 and sub == 33):
                dxm, dym = (100 - x0) * L / 100.0, (y0 - 50) * W / 100.0
                shots.append((e["matchId"], label, ev == 3, max(dxm, 0.1), dym, 403 in tags, 101 in tags))
            elif ev == 2:
                fouls.append((e["matchId"], label, x0 / 100.0, bool({1701, 1702, 1703} & tags)))
            elif ev == 1:
                duels.append((e["matchId"], label, sub, x0 / 100.0, 703 in tags, 701 in tags, 1601 in tags))
        print(label, "klaar", flush=True)
    return (pd.DataFrame(passes, columns=["match", "comp", "sub", "len", "fwd", "x", "ok"]),
            pd.DataFrame(shots, columns=["match", "comp", "fk", "dx", "dy", "head", "goal"]),
            pd.DataFrame(fouls, columns=["match", "comp", "x", "card"]),
            pd.DataFrame(duels, columns=["match", "comp", "sub", "x", "won", "lost", "slide"]))


def _auc(y, X, w, mask, match):
    return auc_report(y[mask], predict(X[mask], w), match[mask])


def pass_model(p: pd.DataFrame) -> dict:
    p = p[p["len"] > 0.5]
    sub = p["sub"]
    names = ["const", "length_m", "length_m_sq", "forward_cos", "high_pass", "head_pass", "launch", "smart_pass", "cross", "x_rel"]
    X = np.column_stack([np.ones(len(p)), p["len"], p["len"] ** 2 / 100.0, p["fwd"], (sub == 83), (sub == 82), (sub == 84), (sub == 86), (sub == 80), p["x"]]).astype(float)
    # lekvrij: alleen subtype (bedoeling vooraf) en plek van de trap, geen lengte of richting van de baan
    X2 = np.column_stack([np.ones(len(p)), (sub == 83), (sub == 82), (sub == 84), (sub == 86), (sub == 80), p["x"]]).astype(float)
    names2 = ["const", "high_pass", "head_pass", "launch", "smart_pass", "cross", "x_rel"]
    y = p["ok"].to_numpy().astype(float)
    test = p["comp"].isin(WY_TEST_GROUPS).to_numpy()
    train = ~test
    match = p["match"].to_numpy()
    w, conv = logistic_fit(X[train], y[train], label="wyscout_pass_oud")
    w2, conv2 = logistic_fit(X2[train], y[train], label="wyscout_pass_v2")
    curve = []
    for lo, hi in [(0, 5), (5, 10), (10, 15), (15, 20), (20, 30), (30, 40), (40, 150)]:
        m = ((p["len"] >= lo) & (p["len"] < hi)).to_numpy()
        if m.sum() > 500:
            curve.append({"lengte_m": f"{lo}-{hi}", "n": int(m.sum()), "geslaagd": round(float(y[m].mean()), 3)})
    by_comp = {c: round(float(g["ok"].mean()), 3) for c, g in p.groupby("comp")}
    by_sub = {int(s): round(float(g["ok"].mean()), 3) for s, g in p.groupby("sub")}
    return {"n_passes": int(len(p)), "geslaagd_totaal": round(float(y.mean()), 4), "auc_test": _auc(y, X2, w2, test, match) | {"geconvergeerd": conv2},
            "coefficienten": dict(zip(names2, [round(float(v), 5) for v in w2])),
            "geslaagd_per_competitie": by_comp, "geslaagd_per_subtype": by_sub,
            "opmerking": "subtype: 80 cross, 82 head pass, 83 high pass, 84 launch, 85 simple pass, 86 smart pass; zonder 'onder druk' (niet in Wyscout); "
                         "geen lengte of richting (lekt de uitkomst, zie pass_geslaagd_oud_lekkend)",
            "pass_geslaagd_oud_lekkend": {"waarschuwing": LEAK_WARNING.replace("het bijbehorende *_v2.json", "pass_geslaagd (dit bestand)"),
                                          "auc_test": _auc(y, X, w, test, match) | {"geconvergeerd": conv},
                                          "coefficienten": dict(zip(names, [round(float(v), 5) for v in w])), "kalibratie_per_werkelijke_lengte": curve}}


def shot_model(s: pd.DataFrame) -> dict:
    dist = np.hypot(s["dx"], s["dy"])
    a1, a2 = np.arctan2(3.66 - s["dy"], s["dx"]), np.arctan2(-3.66 - s["dy"], s["dx"])
    ang = np.abs(a1 - a2)
    X = np.column_stack([np.ones(len(s)), dist, dist ** 2 / 100.0, ang, s["head"], s["fk"]]).astype(float)
    y = s["goal"].to_numpy().astype(float)
    test = s["comp"].isin(WY_TEST_GROUPS).to_numpy()
    train = ~test
    w, conv = logistic_fit(X[train], y[train], label="wyscout_schot")
    d = dist.to_numpy()
    curve = []
    for lo, hi in [(0, 6), (6, 10), (10, 16), (16, 22), (22, 30), (30, 80)]:
        m = (d >= lo) & (d < hi)
        if m.sum() >= 100:
            curve.append({"afstand_m": f"{lo}-{hi}", "n": int(m.sum()), "doelpunt": round(float(y[m].mean()), 3)})
    return {"n_schoten": int(len(s)), "doelpuntkans_totaal": round(float(y.mean()), 4), "auc_test": _auc(y, X, w, test, s["match"].to_numpy()) | {"geconvergeerd": conv},
            "coefficienten": dict(zip(["const", "dist_m", "dist_m_sq", "goal_angle_rad", "head_or_body", "free_kick"], [round(float(v), 5) for v in w])),
            "kalibratie_per_afstand": curve, "doelpuntkans_per_competitie": {c: round(float(g["goal"].mean()), 3) for c, g in s.groupby("comp")},
            "opmerking": "inclusief directe vrije trappen en excl. strafschoppen; schotevents van Wyscout bevatten ook niet-doelgerichte pogingen"}


def foul_model(f: pd.DataFrame) -> dict:
    zones = [("eigen derde", 0, 1 / 3), ("middenderde", 1 / 3, 2 / 3), ("aanvallend derde", 2 / 3, 1.01)]
    return {"n_overtredingen": int(len(f)), "kaart_per_overtreding": round(float(f["card"].mean()), 4),
            "per_derde": [{"zone": n, "aandeel": round(float(((f.x >= lo) & (f.x < hi)).mean()), 3),
                           "kaart_per_overtreding": round(float(f[(f.x >= lo) & (f.x < hi)]["card"].mean()), 3)} for n, lo, hi in zones],
            "kaart_per_overtreding_per_competitie": {c: round(float(g["card"].mean()), 3) for c, g in f.groupby("comp")}}


def duel_model(d: pd.DataFrame) -> dict:
    out = {"n_duels": int(len(d)), "per_subtype": {}}
    for sub, name in [(10, "luchtduel"), (11, "grondduel aanval"), (12, "grondduel verdediging"), (13, "duel om losse bal")]:
        g = d[d["sub"] == sub]
        dec = g[g.won | g.lost]
        out["per_subtype"][name] = {"n": int(len(g)), "gewonnen_van_beslist": round(float(dec.won.mean()), 3) if len(dec) else None}
    t = d[d.slide]
    out["glijdende_tackle"] = {"n": int(len(t)), "gewonnen_van_beslist": round(float(t[t.won | t.lost].won.mean()), 3)}
    return out


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    p, s, f, d = collect()
    res = {"bron": "Wyscout public dataset (Pappalardo e.a. 2019), CC BY 4.0, naamsvermelding verplicht",
           "data": "7 competities: Premier League, La Liga, Serie A, Bundesliga, Ligue 1 (2017/18), WK 2018, EK 2016",
           "licentie_status": LICENSE_WYSCOUT,
           "validatie": "training = competities buiten WY_TEST_GROUPS, test = " + ", ".join(sorted(WY_TEST_GROUPS)) + "; AUC-interval = bootstrap 200x per wedstrijd",
           "pass_geslaagd": pass_model(p), "schot_doelpunt": shot_model(s),
           "overtreding": foul_model(f), "duel": duel_model(d)}
    write_json(OUT, res)
    print("klaar ->", OUT)
    print("pass: n=%d geslaagd %.3f AUC %s (oud lekkend %s)" % (res["pass_geslaagd"]["n_passes"], res["pass_geslaagd"]["geslaagd_totaal"],
          res["pass_geslaagd"]["auc_test"]["auc"], res["pass_geslaagd"]["pass_geslaagd_oud_lekkend"]["auc_test"]["auc"]))
    print("schot: n=%d doelpunt %.3f AUC %s" % (res["schot_doelpunt"]["n_schoten"], res["schot_doelpunt"]["doelpuntkans_totaal"], res["schot_doelpunt"]["auc_test"]["auc"]))


if __name__ == "__main__":
    sys.exit(main())
