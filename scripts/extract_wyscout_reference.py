"""Referentietabellen per competitie uit de Wyscout public dataset (CC BY 4.0, Pappalardo e.a. 2019).

Leest data/external/wyscout/events.zip en matches.zip (alleen lezen, extern) en geeft per team per wedstrijd tellers voor
passes (geslaagd/niet geslaagd), overtredingen, kaarten, duels, buitenspel, hervattingen, schoten en doelpunten, plus een
samenvatting per competitie (Premier League, La Liga, Serie A, Bundesliga, Ligue 1 in 2017/18; WK 2018; EK 2016).
Draai met `python -I scripts/extract_wyscout_reference.py`. Bron: Wyscout/Pappalardo e.a., Scientific Data 2019, CC BY 4.0:
naamsvermelding verplicht.
"""
import json
import sys
import zipfile
from collections import defaultdict
from pathlib import Path

import pandas as pd

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "external" / "wyscout"
OUT_DIR = ROOT / "data" / "reference"
NAMES = {
    "England": "Premier League 2017/18", "Spain": "La Liga 2017/18", "Italy": "Serie A 2017/18",
    "Germany": "Bundesliga 2017/18", "France": "Ligue 1 2017/18",
    "World_Cup": "WK 2018", "European_Championship": "EK 2016",
}
TAG = {"goal": 101, "own_goal": 102, "intercept": 1401, "clear": 1501, "red": 1701, "yellow": 1702, "yellow2": 1703,
       "acc": 1801, "inacc": 1802, "won": 703, "lost": 701}
FREE_KICK_SUB = {30: "corners", 31: "free_kicks", 32: "free_kick_crosses", 33: "free_kick_shots", 34: "goal_kicks", 35: "penalties", 36: "throw_ins"}
DUEL_SUB = {10: "duels_air", 11: "duels_ground_attack", 12: "duels_ground_defend", 13: "duels_loose_ball"}
FOUL_SUB = {20: "fouls_plain", 21: "fouls_hand", 22: "fouls_late_card", 23: "fouls_out_of_game", 24: "fouls_protest", 25: "fouls_simulation", 26: "fouls_time_lost", 27: "fouls_violent"}


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    ev_zip = zipfile.ZipFile(SRC / "events.zip")
    ma_zip = zipfile.ZipFile(SRC / "matches.zip")
    rows = []
    for key, label in NAMES.items():
        matches = json.loads(ma_zip.read(f"matches_{key}.json"))
        teams_by_match = {m["wyId"]: list(m["teamsData"].keys()) for m in matches}
        print(label, len(matches), "wedstrijden", flush=True)
        events = json.loads(ev_zip.read(f"events_{key}.json"))
        acc = defaultdict(lambda: defaultdict(int))
        n_pso = n_pso_goal = 0
        for e in events:
            if e.get("matchPeriod") == "P":  # strafschoppenreeks: geen onderdeel van de wedstrijd
                n_pso += 1
                n_pso_goal += TAG["goal"] in {t["id"] for t in e.get("tags", [])}
                continue
            c = acc[(e["matchId"], str(e["teamId"]))]
            ev, sub = e["eventId"], e["subEventId"]
            tags = {t["id"] for t in e.get("tags", [])}
            if ev == 8:
                c["passes"] += 1
                c["passes_accurate"] += TAG["acc"] in tags
                c["passes_inaccurate"] += TAG["inacc"] in tags
                if sub == 80:
                    c["crosses"] += 1
                if sub == 84:
                    c["launches"] += 1
            elif ev == 2:
                c["fouls"] += 1
                if sub in FOUL_SUB:
                    c[FOUL_SUB[sub]] += 1
                c["yellow"] += TAG["yellow"] in tags
                c["second_yellow"] += TAG["yellow2"] in tags
                c["red_direct"] += TAG["red"] in tags
            elif ev == 1:
                c["duels"] += 1
                if sub in DUEL_SUB:
                    c[DUEL_SUB[sub]] += 1
                c["duels_won"] += TAG["won"] in tags
                c["duels_lost"] += TAG["lost"] in tags
            elif ev == 6:
                c["offsides"] += 1
            elif ev == 3:
                if sub in FREE_KICK_SUB:
                    c[FREE_KICK_SUB[sub]] += 1
            elif ev == 10:
                c["shots"] += 1
            elif ev == 5 and sub == 50:
                c["ball_out"] += 1
            if TAG["goal"] in tags and ev in (10, 3):
                c["goals"] += 1
            if TAG["intercept"] in tags:
                c["interceptions"] += 1
            if TAG["clear"] in tags or sub == 71:
                c["clearances"] += 1
        del events
        if n_pso:
            print(f"  {label}: {n_pso} penaltyreeks-events (matchPeriod P) uitgesloten, waarvan {n_pso_goal} met goal-tag", flush=True)
        for mid, tids in teams_by_match.items():
            for tid in tids:
                c = acc.get((mid, tid))
                if c is None:
                    continue
                rows.append({"competitie": label, "match_id": mid, "team_id": tid, **c})
    df = pd.DataFrame(rows).fillna(0)
    df["cards_total"] = df.yellow + df.second_yellow + df.red_direct
    df["pass_completion"] = df.passes_accurate / df.passes
    df.to_csv(OUT_DIR / "wyscout_team_match.csv", index=False)
    metrics = [c for c in df.columns if c not in ("competitie", "match_id", "team_id")]
    summary = {"bron": "Wyscout public dataset (Pappalardo e.a. 2019), CC BY 4.0", "team_wedstrijden": len(df), "competities": {}}
    for comp, sub in df.groupby("competitie"):
        summary["competities"][comp] = {"team_wedstrijden": len(sub), **{
            m: {"gem": round(float(sub[m].mean()), 3), "p10": round(float(sub[m].quantile(.1)), 3),
                "p50": round(float(sub[m].quantile(.5)), 3), "p90": round(float(sub[m].quantile(.9)), 3)} for m in metrics}}
    (OUT_DIR / "wyscout_summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"{len(df)} team-wedstrijden -> {OUT_DIR}")


if __name__ == "__main__":
    sys.exit(main())
