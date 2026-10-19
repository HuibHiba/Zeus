"""B5 vermoeidheid: referentie-aggregaten voor afstand, sprints en verval tussen de helften (Q1 2027).

Leest lokale bronbestanden (buiten git) en schrijft alleen eigen aggregaten naar calibration/b5_vermoeidheid_referentie.json.
Bronnen (allemaal CC BY 4.0, naamsvermelding verplicht):
  dfl     DFL/Sportec IDSSE, Bassek e.a., Sci Data 12:195 (2025), doi:10.6084/m9.figshare.28196177   -> data/external/dfl/*.xml
  santos  Augusto Nascimento Santos, Running performance of professional soccer players, doi:10.5281/zenodo.12548183
          -> local/b5/santos.csv (139 speler-wedstrijden, Braziliaans profteam)
  novara  GPS U19 per formatie en positie, Novara e.a., doi:10.5281/zenodo.4114077 -> local/b5/novara.xlsx (23 spelers U19, 31 wedstrijden)
  csl     Yang, CSL 2021 hoge-intensiteitsruns, doi:10.5281/zenodo.17355298: het bestand bevat maar 97 regels (steekproef) en geen
          tijdsverloop, dus niet bruikbaar voor verval; hier alleen vermeld in de uitvoer.
Definities: sprint = aaneengesloten episode met snelheid > 7,0 m/s van minimaal 1,0 s; hoge snelheid (HSR) = afstand boven 5,5 m/s.
Draai: python scripts/b5_vermoeidheid_data.py [--only santos,novara,dfl]   (dfl duurt ca. 10-30 min; één proces).
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
from defusedxml import ElementTree as ET  # externe XML

sys.path.insert(0, str(Path(__file__).resolve().parent))
from zeus_common import atomic_write_text  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "calibration" / "b5_vermoeidheid_referentie.json"
DFL = ROOT / "data" / "external" / "dfl"
DT = 0.04          # 25 Hz
SPRINT_V = 7.0     # m/s
SPRINT_MIN_S = 1.0
HSR_V = 5.5        # m/s
MIN_HALF_S = 40 * 60


def stats(vals):
    v = np.asarray([x for x in vals if x == x], dtype=float)
    if len(v) == 0:
        return None
    return {"n": int(len(v)), "gem": round(float(v.mean()), 3), "sd": round(float(v.std(ddof=1)), 3) if len(v) > 1 else None,
            "p10": round(float(np.percentile(v, 10)), 3), "p50": round(float(np.percentile(v, 50)), 3), "p90": round(float(np.percentile(v, 90)), 3)}


def sprint_count(v: np.ndarray) -> int:
    above = np.concatenate(([0], (v > SPRINT_V).astype(np.int8), [0]))
    d = np.diff(above)
    starts, ends = np.where(d == 1)[0], np.where(d == -1)[0]
    return int(np.sum((ends - starts) * DT >= SPRINT_MIN_S))


def santos():
    import pandas as pd
    s = pd.read_csv(ROOT / "local" / "b5" / "santos.csv", sep=";", decimal=",", encoding="latin1", index_col=False)
    s.columns = [c.strip() for c in s.columns]
    hsr, sp7 = "Distance > 5.5 m/s", "Distance > 7 m/s"
    nspr = [c for c in s.columns if c.startswith("N") and "Sprints" in c][0]
    total_col = [c for c in s.columns if c.lower().startswith("total")][0]
    out = {"bron": "Santos, doi:10.5281/zenodo.12548183, CC BY 4.0", "n_speler_wedstrijden": int(len(s)), "tempo_codes": sorted(s["tempo"].unique().tolist()),
           "totaal": {"afstand_km": stats(s[total_col] / 1000), "hsr_5_5_m": stats(s[hsr]), "sprint_7_m": stats(s[sp7]), "sprints_n": stats(s[nspr])}}
    pos_col = s.columns[1]  # 'posição' (codering 1-5 niet gedocumenteerd in de bron)
    out["per_positiecode"] = {str(int(p)): {"afstand_km": stats(g[total_col] / 1000), "sprints_n": stats(g[nspr]), "n": int(len(g))} for p, g in s.groupby(pos_col)}
    out["opmerking"] = "Eén waarde per speler-wedstrijd (tempo=3, volledige wedstrijd); geen helften. Positiecodes 1-5 niet gedocumenteerd."
    return out


def novara():
    import pandas as pd
    x = pd.read_excel(ROOT / "local" / "b5" / "novara.xlsx", sheet_name="1st - 2nd half", header=None)
    acc = {}
    for j in range(x.shape[1]):
        if x.iat[3, j] == "1st half" and isinstance(x.iat[0, j], str):
            pos, met = x.iat[0, j].strip(), str(x.iat[2, j]).strip()
            a = pd.to_numeric(x.iloc[4:, j], errors="coerce")
            b = pd.to_numeric(x.iloc[4:, j + 1], errors="coerce")
            ok = a.notna() & b.notna()
            acc.setdefault((pos, met), []).append((a[ok].to_numpy(), b[ok].to_numpy()))
    out = {"bron": "Novara e.a., doi:10.5281/zenodo.4114077, CC BY 4.0",
           "metrieken": "TD = totale afstand (km per helft), VHSRD = hoge-snelheidsafstand (m per helft), S > 25 km/h = aantal sprintacties per helft",
           "per_positie": {}, "alle_posities": {}}
    for (pos, met), lst in sorted(acc.items()):
        if met not in ("TD", "VHSRD", "S > 25 km/h"):
            continue
        a = np.concatenate([p[0] for p in lst]); b = np.concatenate([p[1] for p in lst])
        out["per_positie"].setdefault(pos, {})[met] = {"n": int(len(a)), "helft1": round(float(a.mean()), 3), "helft2": round(float(b.mean()), 3),
                                                        "verhouding_2_1": round(float(b.mean() / a.mean()), 3) if a.mean() else None}
    for met in ("TD", "VHSRD", "S > 25 km/h"):
        a = np.concatenate([p[0] for (pos, m), l in acc.items() if m == met for p in l])
        b = np.concatenate([p[1] for (pos, m), l in acc.items() if m == met for p in l])
        out["alle_posities"][met] = {"n": int(len(a)), "helft1": round(float(a.mean()), 3), "helft2": round(float(b.mean()), 3), "verhouding_2_1": round(float(b.mean() / a.mean()), 3)}
    return out


def dfl_match(mid: str):
    info = ET.parse(str(next(DFL.glob(f"DFL_02_01_matchinformation_*{mid}.xml")))).getroot()
    keepers = {p.get("PersonId") for p in info.iter("Player") if p.get("PlayingPosition") == "TW"}
    pos = next(DFL.glob(f"DFL_04_03_positions_raw_observed_*{mid}.xml"))
    halves = {}  # (person, section) -> samenvatting
    for _, el in ET.iterparse(str(pos), events=("end",)):
        if el.tag != "FrameSet":
            continue
        team, pid = el.get("TeamId"), el.get("PersonId")
        if team in ("referee", "BALL") or pid in keepers:
            el.clear(); continue
        frames = el.findall("Frame")
        if len(frames) >= 25 * 60 * 5:
            v = np.array([float(f.get("S")) for f in frames], dtype=np.float32) / 3.6  # S is km/u (zie dfl_physics.py)
            v = np.where(v <= 12.0, v, 0.0)
            halves[(pid, el.get("GameSection"))] = {"t": len(v) * DT, "dist": float(v.sum() * DT / 1000.0),
                                                    "hsr": float(v[v > HSR_V].sum() * DT), "spr": sprint_count(v)}
        el.clear()
    return halves


def dfl():
    mids = sorted({p.stem.rsplit("_", 1)[-1] for p in DFL.glob("DFL_04_03_positions_raw_observed_*.xml")})
    per90, h1, h2 = [], [], []
    for mid in mids:
        h = dfl_match(mid)
        print(f"dfl {mid}: {len(h)} speler-helften", flush=True)
        for pid in {k[0] for k in h}:
            a, b = h.get((pid, "firstHalf")), h.get((pid, "secondHalf"))
            if not (a and b) or a["t"] < MIN_HALF_S or b["t"] < MIN_HALF_S:
                continue  # alleen spelers die beide helften (vrijwel) volledig speelden
            f = 5400.0 / (a["t"] + b["t"])
            per90.append({"afstand_km": (a["dist"] + b["dist"]) * f, "hsr_m": (a["hsr"] + b["hsr"]) * f, "sprints": (a["spr"] + b["spr"]) * f})
            h1.append(a); h2.append(b)
    # ruwe helften (geen normalisatie op duur): blessuretijd is stilstand en zou anders als verval tellen; zelfde maat als Novara en de sim
    def norm(hs, k): return float(np.mean([x[k] for x in hs]))
    ratio = lambda k: round(norm(h2, k) / norm(h1, k), 4)
    return {"bron": "DFL/Sportec IDSSE, Bassek e.a. 2025, doi:10.6084/m9.figshare.28196177, CC BY 4.0 (DFL noemen + artikel citeren)",
            "n_wedstrijden": len(mids), "n_veldspelers_volledig": len(per90),
            "per_90": {"afstand_km": stats([p["afstand_km"] for p in per90]), "hsr_m": stats([p["hsr_m"] for p in per90]), "sprints": stats([p["sprints"] for p in per90])},
            "verval_helft2_ten_opzichte_van_helft1": {"afstand": ratio("dist"), "hsr": ratio("hsr"), "sprints": ratio("spr")},
            "definities": f"sprint = episode > {SPRINT_V} m/s van >= {SPRINT_MIN_S} s; HSR = afstand boven {HSR_V} m/s; alleen veldspelers die beide helften >= 40 min speelden"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="santos,novara,dfl")
    args = ap.parse_args()
    res = json.loads(OUT.read_text(encoding="utf-8")) if OUT.exists() else {}
    for name, fn in (("santos", santos), ("novara", novara), ("dfl", dfl)):
        if name in args.only.split(","):
            res[name] = fn()
            atomic_write_text(OUT, json.dumps(res, indent=1, ensure_ascii=False) + "\n")
            print(name, "klaar", flush=True)
    res["csl"] = {"bron": "Yang, doi:10.5281/zenodo.17355298, CC BY 4.0", "opmerking": "Bestand bevat 97 ruwe runs (geen tijdsverloop) en een positietabel; niet bruikbaar voor verval."}
    atomic_write_text(OUT, json.dumps(res, indent=1, ensure_ascii=False) + "\n")


if __name__ == "__main__":
    main()
