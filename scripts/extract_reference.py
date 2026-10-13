"""Referentietabellen uit StatsBomb-events: overtredingen, kaarten, passuitkomsten, duels, hervattingen, schoten.

Leest data/raw/events_*.pkl.gz (eerder opgeslagen pandas-events van mijn eigen download; pickles zijn zelf gemaakt, niet extern)
en schrijft per team per wedstrijd een tabel plus een samenvatting per competitiegroep. Doel: gecontroleerde referenties voor de
kalibratie van de simulatie (vooral overtredingen, kaarten en passuitkomsten).
Bron: StatsBomb open data (bronvermelding met logo; commercieel gebruik onduidelijk, alleen intern onderzoek).
"""
import gzip
import json
import pickle
import sys
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent))
from zeus_common import SkipLog, atomic_write_text  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "data" / "raw"
OUT_DIR = ROOT / "data" / "reference"


def col(df, name):
    return df[name] if name in df.columns else pd.Series([np.nan] * len(df), index=df.index)


def team_stats(ev: pd.DataFrame, team: str) -> dict:
    t = ev[ev["team"] == team]
    ty = col(t, "type")
    p = t[ty == "Pass"]
    out = col(p, "pass_outcome")
    ptype = col(p, "pass_type")
    d = {
        "passes": len(p),
        "passes_complete": int(out.isna().sum()),
        "passes_incomplete": int((out == "Incomplete").sum()),
        "passes_out": int((out == "Out").sum()),
        "passes_offside": int((out == "Pass Offside").sum()),
        "throw_ins": int((ptype == "Throw-in").sum()),
        "corners": int((ptype == "Corner").sum()),
        "free_kicks_pass": int((ptype == "Free Kick").sum()),
        "goal_kicks": int((ptype == "Goal Kick").sum()),
    }
    fc = t[ty == "Foul Committed"]
    card = col(fc, "foul_committed_card")
    bb = t[ty == "Bad Behaviour"]
    bcard = col(bb, "bad_behaviour_card")
    d["fouls_committed"] = len(fc)
    d["fouls_won"] = int((ty == "Foul Won").sum())
    d["penalties_conceded"] = int(col(fc, "foul_committed_penalty").fillna(False).astype(bool).sum())
    d["yellow"] = int((card == "Yellow Card").sum() + (bcard == "Yellow Card").sum())
    d["second_yellow"] = int((card == "Second Yellow").sum() + (bcard == "Second Yellow").sum())
    d["red_direct"] = int((card == "Red Card").sum() + (bcard == "Red Card").sum())
    du = t[ty == "Duel"]
    dtype = col(du, "duel_type")
    dout = col(du, "duel_outcome")
    d["tackles"] = int((dtype == "Tackle").sum())
    d["tackles_won"] = int(((dtype == "Tackle") & dout.isin(["Won", "Success In Play", "Success Out"])).sum())
    d["aerial_duels_lost"] = int((dtype == "Aerial Lost").sum())
    d["interceptions"] = int((ty == "Interception").sum())
    d["blocks"] = int((ty == "Block").sum())
    d["clearances"] = int((ty == "Clearance").sum())
    d["ball_recoveries"] = int((ty == "Ball Recovery").sum())
    dr = t[ty == "Dribble"]
    d["dribbles"] = len(dr)
    d["dribbles_complete"] = int((col(dr, "dribble_outcome") == "Complete").sum())
    d["dribbled_past"] = int((ty == "Dribbled Past").sum())
    d["offsides"] = int((ty == "Offside").sum())
    s = t[ty == "Shot"]
    so = col(s, "shot_outcome")
    d["shots"] = len(s)
    d["shots_on_target"] = int(so.isin(["Goal", "Saved", "Saved To Post"]).sum())
    d["goals"] = int((so == "Goal").sum())
    d["penalty_shots"] = int((col(s, "shot_type") == "Penalty").sum())
    return d


def main() -> int:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    skip = SkipLog("extract_reference")
    pl = pd.read_csv(ROOT / "data" / "pl_1516_team_match.csv")[["match_id"]].drop_duplicates()
    allm = pd.read_csv(ROOT / "data" / "team_match_all.csv")[["match_id", "competition", "kind"]].drop_duplicates("match_id")
    group = {int(m): "Premier League 2015/16" for m in pl.match_id}
    for _, r in allm.iterrows():
        group[int(r.match_id)] = "toernooien 2020-2024" if r.kind == "toernooi" else "club-seizoenen 2020-2023 (een team + tegenstanders)"
    rows = []
    for path in sorted(RAW.glob("events_*.pkl.gz")):
        match_id = int(path.stem.split("_")[1].split(".")[0])
        try:
            with gzip.open(path, "rb") as f:
                ev = pickle.load(f)
            ev = ev[ev["period"] <= 4]
        except Exception as exc:  # beschadigd cachebestand: tellen, niet stil overslaan
            skip.skip(path.name, f"leesfout: {type(exc).__name__}")
            continue
        teams = list(ev["team"].dropna().unique())
        if len(teams) != 2:
            skip.skip(path.name, f"aantal teams {len(teams)} i.p.v. 2")
            continue
        skip.ok()
        minutes = float(ev["minute"].max()) if "minute" in ev.columns else np.nan
        for team in teams:
            row = {"match_id": match_id, "groep": group.get(match_id, "onbekend"), "team": team, "minuten": minutes}
            row.update(team_stats(ev, team))
            rows.append(row)
    code = skip.finish()
    if not rows:
        return 1
    df = pd.DataFrame(rows)
    df["cards_total"] = df.yellow + df.second_yellow + df.red_direct
    df["pass_completion"] = df.passes_complete / df.passes
    atomic_write_text(OUT_DIR / "team_match_reference.csv", df.to_csv(index=False))

    metrics = [c for c in df.columns if c not in ("match_id", "groep", "team")]
    summary = {"bron": "StatsBomb open data, eigen extractie (events met period <= 4)", "aantal_team_wedstrijden": len(df), "groepen": {}}
    for g, sub in df.groupby("groep"):
        summary["groepen"][g] = {"team_wedstrijden": len(sub), **{
            m: {"gem": round(float(sub[m].mean()), 3), "p10": round(float(sub[m].quantile(.1)), 3),
                "p50": round(float(sub[m].quantile(.5)), 3), "p90": round(float(sub[m].quantile(.9)), 3)} for m in metrics}}
    summary["definities"] = {
        "passes": "alle events van type Pass (ook set pieces en inworpen)",
        "passes_complete": "pass_outcome leeg (geslaagd)",
        "passes_incomplete/out/offside": "pass_outcome Incomplete / Out / Pass Offside; Injury Clearance en Unknown niet apart geteld",
        "yellow/second_yellow/red_direct": "kaart in Foul Committed of Bad Behaviour; tweede geel is apart van directe rood; cards_total telt alle drie",
        "tackles_won": "Duel type Tackle met uitkomst Won, Success In Play of Success Out",
        "minuten": "hoogste eventminuut tot en met verlenging; benadering van speelduur (geen blessuretijdcorrectie)",
    }
    atomic_write_text(OUT_DIR / "reference_summary.json", json.dumps(summary, indent=2, ensure_ascii=False, allow_nan=False))
    print(f"{len(df)} team-wedstrijden -> {OUT_DIR}")
    for g, s in summary["groepen"].items():
        print(f"\n{g} ({s['team_wedstrijden']} team-wedstrijden)")
        for m in ("passes", "pass_completion", "fouls_committed", "yellow", "red_direct", "second_yellow", "tackles", "interceptions", "offsides", "throw_ins", "corners", "shots"):
            v = s[m]
            print(f"  {m:18s} gem {v['gem']:8.3f}  p10 {v['p10']:8.3f}  p50 {v['p50']:8.3f}  p90 {v['p90']:8.3f}")
    return code


if __name__ == "__main__":
    sys.exit(main())
