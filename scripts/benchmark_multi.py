"""WP2 uitbreiding: dezelfde team-per-wedstrijd maatstaven voor alle recente open StatsBomb-competities.

Hergebruikt load_events/team_rows uit benchmark_pl.py. Schrijft data/team_match_all.csv met een
kolom `competition` (naam + seizoen) en `kind` (club of toernooi). Ruwe events worden gecachet in
data/raw/ (staat in .gitignore). Bron: StatsBomb open data, bronvermelding met logo bij publicatie.
"""
import sys
import warnings
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import pandas as pd
from statsbombpy import sb

sys.path.insert(0, str(Path(__file__).resolve().parent))
import benchmark_pl as bp  # noqa: E402

warnings.filterwarnings("ignore")
OUT = bp.ROOT / "data" / "team_match_all.csv"
MIN_YEAR = 2020  # alleen relatief recente seizoenen
TOURNAMENTS = {"FIFA World Cup", "UEFA Euro", "Copa America", "African Cup of Nations"}


def season_year(name: str) -> int:
    return int(name[:4])


def main() -> None:
    bp.RAW.mkdir(parents=True, exist_ok=True)
    comps = sb.competitions()
    comps = comps[(comps["competition_gender"] == "male")]
    comps = comps[comps["season_name"].map(season_year) >= MIN_YEAR]
    comps = comps[comps["competition_name"] != "Indian Super league"]  # klein en afwijkend; eerst de hoofdmassa
    print(f"{len(comps)} competitie-seizoenen:")
    for _, c in comps.iterrows():
        print(f"  {c['competition_name']} {c['season_name']}")

    frames = []
    for _, c in comps.iterrows():
        label = f"{c['competition_name']} {c['season_name']}"
        matches = sb.matches(competition_id=int(c["competition_id"]), season_id=int(c["season_id"]))
        print(f"{label}: {len(matches)} wedstrijden", flush=True)

        def work(m: pd.Series) -> list[dict]:
            try:
                return bp.team_rows(m, bp.load_events(int(m["match_id"])))
            except Exception as exc:  # een kapotte wedstrijd mag de rest niet stoppen
                print(f"  overgeslagen {m['match_id']}: {exc}", flush=True)
                return []

        with ThreadPoolExecutor(max_workers=8) as pool:
            rows = [r for rs in pool.map(work, [row for _, row in matches.iterrows()]) for r in rs]
        df = pd.DataFrame(rows)
        if df.empty:
            continue
        df["competition"] = label
        df["kind"] = "toernooi" if c["competition_name"] in TOURNAMENTS else "club"
        frames.append(df)

    table = pd.concat(frames, ignore_index=True)
    table.to_csv(OUT, index=False)
    print(f"{len(table)} rijen -> {OUT}")


if __name__ == "__main__":
    main()
