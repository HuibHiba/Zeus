"""WP2: benchmarktabel van een clubseizoen (Premier League 2015/16, StatsBomb open data).

Haalt alle wedstrijden op, bewaart de ruwe events in data/raw/ en schrijft per team per
wedstrijd 10 maatstaven naar data/pl_1516_team_match.csv.
Bron: StatsBomb open data, bronvermelding met logo verplicht bij publicatie.
"""
import gzip
import pickle
import warnings
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import pandas as pd
from statsbombpy import sb

warnings.filterwarnings("ignore")

COMPETITION_ID, SEASON_ID = 2, 27  # Premier League 2015/2016
ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "data" / "raw"
OUT = ROOT / "data" / "pl_1516_team_match.csv"
PITCH_LENGTH = 120  # StatsBomb-coördinaten: 120 x 80 yards, niet meters
YARD_TO_M = 0.9144


def load_events(match_id: int) -> pd.DataFrame:
    path = RAW / f"events_{match_id}.pkl.gz"
    if path.exists():
        with gzip.open(path, "rb") as f:
            return pickle.load(f)
    events = sb.events(match_id=match_id)
    with gzip.open(path, "wb") as f:
        pickle.dump(events, f)
    return events


def x_of(location) -> float:
    return location[0] if isinstance(location, list) else float("nan")


def team_rows(match: pd.Series, events: pd.DataFrame) -> list[dict]:
    ev = events[events["period"] <= 4].copy()  # geen strafschoppenreeks
    ev["x"] = ev["location"].map(x_of)
    teams = [match["home_team"], match["away_team"]]
    rows = []
    for team in teams:
        opp = teams[1] if team == teams[0] else teams[0]
        shots = ev[(ev["type"] == "Shot") & (ev["team"] == team)]
        passes = ev[(ev["type"] == "Pass") & (ev["team"] == team)]
        opp_passes = ev[(ev["type"] == "Pass") & (ev["team"] == opp)]
        # PPDA: passes van de tegenstander in zijn eigen 60% gedeeld door onze
        # verdedigende acties in diezelfde zone (tackle, interceptie, overtreding).
        opp_build = opp_passes[opp_passes["x"] <= 0.6 * PITCH_LENGTH]
        own = ev[ev["team"] == team]
        def_actions = own[
            (own["x"] >= 0.4 * PITCH_LENGTH)
            & (
                ((own["type"] == "Duel") & (own.get("duel_type") == "Tackle"))
                | (own["type"] == "Interception")
                | (own["type"] == "Foul Committed")
            )
        ]
        goals = (shots["shot_outcome"] == "Goal").sum()
        xg = shots["shot_statsbomb_xg"].sum()
        rows.append(
            {
                "match_id": match["match_id"],
                "team": team,
                "opponent": opp,
                "goals": int(goals),
                "shots": len(shots),
                "xg": round(float(xg), 3),
                "xg_per_shot": round(float(xg) / len(shots), 3) if len(shots) else float("nan"),
                "passes": len(passes),
                "pass_accuracy": round(float(passes["pass_outcome"].isna().mean()), 3) if len(passes) else float("nan"),
                "pass_length_m": round(float(passes["pass_length"].mean()) * YARD_TO_M, 2) if len(passes) else float("nan"),
                "possession_share": round(float((ev["possession_team"] == team).mean()), 3),
                "ppda": round(len(opp_build) / len(def_actions), 2) if len(def_actions) else float("nan"),
            }
        )
    return rows


def main() -> None:
    RAW.mkdir(parents=True, exist_ok=True)
    matches = sb.matches(competition_id=COMPETITION_ID, season_id=SEASON_ID)
    print(f"{len(matches)} wedstrijden")

    def work(m: pd.Series) -> list[dict]:
        return team_rows(m, load_events(int(m["match_id"])))

    with ThreadPoolExecutor(max_workers=8) as pool:
        results = list(pool.map(work, [row for _, row in matches.iterrows()]))
    table = pd.DataFrame([r for rows in results for r in rows])
    table.to_csv(OUT, index=False)
    print(f"{len(table)} rijen -> {OUT}")


if __name__ == "__main__":
    main()
