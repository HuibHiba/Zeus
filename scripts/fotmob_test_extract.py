"""TESTDATA (niet voor het product): formaties, opstellingen en teamstatistieken uit lokaal opgeslagen FotMob-wedstrijdpagina's.

Leest data/external/testdata_fotmob/match_*.html (geen netwerk) en schrijft
data/testdata_fotmob_feyenoord.csv (een rij per team per wedstrijd) en data/testdata_fotmob_lineups.json.
Bron: FotMob (gewone pagina's, robots.txt verbiedt alleen /api/*). Licentie voor hergebruik onduidelijk: alleen lokaal,
niet gebruiken voor het product of een uit te brengen model (besluit 2026-10-13).
Draai met `python -I scripts/fotmob_test_extract.py`.
"""
import json
import re
import sys
from pathlib import Path

import pandas as pd

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "external" / "testdata_fotmob"
OUT_CSV = ROOT / "data" / "testdata_fotmob_matches.csv"
OUT_JSON = ROOT / "data" / "testdata_fotmob_lineups.json"


def load(path: Path) -> dict:
    html = path.read_text(encoding="utf-8")
    m = re.search(r'<script id="__NEXT_DATA__"[^>]*>(.*?)</script>', html, re.S)
    return json.loads(m.group(1))["props"]["pageProps"]


def to_number(v):
    if isinstance(v, (int, float)):
        return float(v)
    if isinstance(v, str):
        m = re.match(r"^\s*(-?\d+(?:\.\d+)?)", v.replace(",", "."))
        return float(m.group(1)) if m else None
    return None


def main() -> None:
    rows, lineups = [], {}
    skipped = 0
    for path in sorted(SRC.glob("match_*.html")):
        pp = load(path)
        gen = pp["general"]
        mid = gen["matchId"]
        home, away = gen["homeTeam"], gen["awayTeam"]
        date = str(gen.get("matchTimeUTCDate", ""))[:10]
        c = pp["content"]
        lu = c.get("lineup")
        stat_groups = ((c.get("stats") or {}).get("Periods") or {}).get("All", {}).get("stats")
        if not lu or not stat_groups or not lu.get("homeTeam") or not lu.get("awayTeam"):
            skipped += 1  # geen opstelling of statistieken (bijv. kleinere competitie)
            continue
        stats = {}
        for grp in stat_groups:
            for it in grp.get("stats", []):
                key = it.get("key") or it.get("title")
                vals = it.get("stats")
                if key and isinstance(vals, list) and len(vals) == 2 and key not in stats:
                    stats[key] = vals
        for idx, (team, side) in enumerate(((home, "homeTeam"), (away, "awayTeam"))):
            t = lu[side]
            row = {
                "match_id": mid, "datum": date, "team": team["name"], "tegenstander": (away if idx == 0 else home)["name"],
                "thuis": idx == 0, "formatie": t.get("formation"),
                "competitie": gen.get("leagueName") or gen.get("parentLeagueName"),
            }
            for key, vals in stats.items():
                row[key] = to_number(vals[idx])
                pm = re.search(r"\((\d+(?:\.\d+)?)%\)", str(vals[idx]))
                if pm:
                    row[key + "_pct"] = float(pm.group(1))
            rows.append(row)
            lineups[f"{mid}:{side}:{team['name']}"] = [
                {"naam": p["name"], "positionId": p.get("positionId"), "x": (p.get("horizontalLayout") or {}).get("x"),
                 "y": (p.get("horizontalLayout") or {}).get("y"), "rating": (p.get("performance") or {}).get("rating")}
                for p in t.get("starters", [])
            ]
    df = pd.DataFrame(rows).sort_values(["datum", "match_id", "thuis"], ascending=[True, True, False])
    df.to_csv(OUT_CSV, index=False)
    OUT_JSON.write_text(json.dumps(lineups, indent=1, ensure_ascii=False), encoding="utf-8")
    print(f"{len(df)} rijen, {df.shape[1]} kolommen -> {OUT_CSV} ({skipped} wedstrijden overgeslagen: geen opstelling of statistieken)")
    print("kolommen:", ", ".join(df.columns))


if __name__ == "__main__":
    sys.exit(main())
