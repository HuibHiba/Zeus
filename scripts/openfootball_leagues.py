"""Uitslagenstatistieken per competitie en seizoen uit de openfootball-repos (CC0, Football.TXT-formaat).

Leest data/external/openfootball-*/ (alleen lezen) en schrijft data/leagues_results_summary.csv met per bestand
(competitie-seizoen): wedstrijden, doelpunten per wedstrijd (totaal, thuis, uit) en thuis/gelijk/uit-percentages.
Draai met `python -I scripts/openfootball_leagues.py`. Bron: openfootball (CC0 / publiek domein).
"""
import re
import sys
from pathlib import Path

import pandas as pd

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "external"
OUT = ROOT / "data" / "leagues_results_summary.csv"
SKIP_REPOS = {"openfootball-internationals"}  # interlands apart bekijken (neutrale velden, ander karakter)
TIME = r"(?:\d{1,2}[:.]\d{2}\s+)?"
PAT_V = re.compile(rf"^\s*{TIME}(.+?)\s+v\s+(.+?)\s+(\d+)-(\d+)")
PAT_B = re.compile(rf"^\s*{TIME}(.+?)\s{{2,}}(\d+)-(\d+)(?:\s*\([^)]*\))?\s{{2,}}(.+?)\s*$")
SEASON = re.compile(r"(\d{4}[-/]\d{2}|\d{4})")


def parse(path: Path):
    n = hg = ag = hw = d = aw = 0
    for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
        if line.lstrip().startswith(("#", "=", "»", "▪", "Matchday", "Group")):
            continue
        m = PAT_V.match(line)
        if m:
            x, y = int(m.group(3)), int(m.group(4))
        else:
            m = PAT_B.match(line)
            if not m:
                continue
            x, y = int(m.group(2)), int(m.group(3))
        n += 1
        hg += x; ag += y
        hw += x > y; d += x == y; aw += x < y
    return n, hg, ag, hw, d, aw


def main() -> None:
    rows = []
    for repo in sorted(SRC.glob("openfootball-*")):
        if repo.name in SKIP_REPOS:
            continue
        for p in repo.rglob("*.txt"):
            if ".git" in p.parts or p.name.upper().startswith(("README", "LICENSE")):
                continue
            n, hg, ag, hw, d, aw = parse(p)
            if n < 30:
                continue
            rel = p.relative_to(repo)
            season = SEASON.search(str(rel))
            rows.append({
                "bron": repo.name.replace("openfootball-", ""), "pad": str(rel).replace("\\", "/"),
                "seizoen": season.group(1) if season else "", "wedstrijden": n,
                "doelpunten_pw": round((hg + ag) / n, 2), "thuis_pw": round(hg / n, 2), "uit_pw": round(ag / n, 2),
                "thuiswinst": round(hw / n, 3), "gelijk": round(d / n, 3), "uitwinst": round(aw / n, 3),
            })
    df = pd.DataFrame(rows)
    df.to_csv(OUT, index=False)
    print(f"{len(df)} competitie-seizoenen, {int(df.wedstrijden.sum())} wedstrijden -> {OUT}")


if __name__ == "__main__":
    sys.exit(main())
