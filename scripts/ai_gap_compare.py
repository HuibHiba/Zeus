"""Gap table real (DFL) vs sim from docs/ai-gap/{real,sim}_metrics.json -> docs/ai-gap/vergelijking.csv + markdown on stdout.
Gap z = (sim - real) / real IQR (p75 - p25); on the median, or on the mean for counts (integer medians hide shifts).
Severity: |z| >= 1 hoog, >= 0.5 middel, else laag. Run: python -I scripts/ai_gap_compare.py
"""
import csv
import sys
import json
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
D = Path(__file__).resolve().parent.parent / "docs" / "ai-gap"
real = json.loads((D / "real_metrics.json").read_text(encoding="utf-8"))
sim = json.loads((D / "sim_metrics.json").read_text(encoding="utf-8"))
simall = {**sim["alle"], "per_ploeg_wedstrijd": sim["per_ploeg_wedstrijd"]}

ROWS = []  # (label, unit, getter on (source, is_sim) -> dist dict, use_mean)
for ph, name in (("in_bezit", "in bezit"), ("zonder_bal", "zonder bal")):
    for k, label, unit in (("lengte_m", "teamlengte", "m"), ("breedte_m", "teambreedte", "m"),
                           ("linie_verd_mid_m", "afstand verdediging-middenveld", "m"),
                           ("linie_mid_aanval_m", "afstand middenveld-aanval", "m"),
                           ("achterlijn_tov_bal_m", "achterlijn t.o.v. bal (- = achter bal)", "m"),
                           ("hull_m2", "convex-hull-oppervlak", "m²"), ("snelheid_m_s", "mediane snelheid veldspelers", "m/s")):
        ROWS.append((f"{label} ({name})", unit, lambda s, sm, ph=ph, k=k: s["team"][ph][k], False))
    for k, label, unit in (("stil_aandeel", "% stilstaand (<0,5 m/s)", "%"), ("sprint_aandeel", "% snel (>5,5 m/s)", "%"),
                           ("achterlijn_volgt_bal_x", "achterlijn schuift mee met bal (lengte, m per m)", "×"),
                           ("blok_volgt_bal_x", "blok schuift mee met bal (lengte)", "×"),
                           ("blok_volgt_bal_y", "blok schuift mee met bal (breedte)", "×")):
        ROWS.append((f"{label} ({name})", unit, lambda s, sm, ph=ph, k=k: s["per_ploeg_wedstrijd"][f"{k}_{ph}"], False))
for k, label, unit in (("binnen_10m", "teamgenoten ≤10 m van baldrager", "#"), ("binnen_20m", "teamgenoten ≤20 m van baldrager", "#"),
                       ("binnen_30m", "teamgenoten ≤30 m van baldrager", "#"), ("passopties_vrij", "vrije passopties 5-25 m", "#"),
                       ("driehoeken", "driehoeken 8-20 m met baldrager", "#"), ("dichtste_teamgenoot_m", "dichtste teamgenoot", "m")):
    ROWS.append((label, unit, lambda s, sm, k=k: s["baldrager"][k], unit == "#"))
for k, label, unit in (("vooruit_aandeel", "aandeel vooruitgaande geslaagde passes", "%"),
                       ("passlengte_m", "gem. lengte geslaagde pass", "m"),
                       ("passes_per_balbezit_gem", "passes per balbezit (gem. per ploeg)", "#")):
    ROWS.append((label, unit, lambda s, sm, k=k: s["per_ploeg_wedstrijd"][k], False))


def fmt(v, unit):
    return f"{100 * v:.0f}" if unit == "%" else f"{v:.0f}" if unit == "m²" else f"{v:.2f}" if unit == "×" else f"{v:.1f}"


out = []
for label, unit, get, use_mean in ROWS:
    r, s = get(real, False), get(simall, True)
    iqr = max(r["p75"] - r["p25"], 1e-9)
    key = "mean" if use_mean else "p50"
    z = (s[key] - r[key]) / iqr
    sev = "hoog" if abs(z) >= 1 else "middel" if abs(z) >= 0.5 else "laag"
    out.append({"metric": label, "eenheid": unit, "echt_p50": r["p50"], "echt_p25": r["p25"], "echt_p75": r["p75"],
                "echt_gem": r["mean"], "sim_p50": s["p50"], "sim_p25": s["p25"], "sim_p75": s["p75"], "sim_gem": s["mean"],
                "z_iqr": round(z, 2), "ernst": sev})
with open(D / "vergelijking.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.DictWriter(f, fieldnames=list(out[0]))
    w.writeheader()
    w.writerows(out)
print("| metric | eenheid | echt mediaan (p25-p75) | sim mediaan (p25-p75) | verschil (in IQR echt) | ernst |")
print("|---|---|---|---|---|---|")
for o, (label, unit, _, use_mean) in zip(out, ROWS):
    extra = f"; gem. {o['echt_gem']:.2f}" if use_mean else ""
    extra_s = f"; gem. {o['sim_gem']:.2f}" if use_mean else ""
    print(f"| {label} | {unit} | {fmt(o['echt_p50'], unit)} ({fmt(o['echt_p25'], unit)}-{fmt(o['echt_p75'], unit)}{extra}) "
          f"| {fmt(o['sim_p50'], unit)} ({fmt(o['sim_p25'], unit)}-{fmt(o['sim_p75'], unit)}{extra_s}) | {o['z_iqr']:+.2f} | {o['ernst']} |")
