"""Run N seeds of the simulation and print per-team means versus the calibration targets.

Usage (from C:\\dev\\zeus):
  .\\.venv\\Scripts\\python.exe scripts\\calibration_run.py [--seeds 8] [--label naam] [--build] [--reuse]
         [--home tactics/laag_blok_442.json] [--away tactics/man_om_man_433.json]

Counts are per team per simulated match (two halves plus added time, no rescaling).
Targets: calibration/event_models.json and data/reference (StatsBomb PL 2015/16).
This calibrates counts; it is not evidence of football realism.
"""
import argparse
import json
import subprocess
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "data" / "cal"

TARGETS = {
    "passes": (485, "p10-p90 347-625"),
    "pass_geslaagd_%": (79, "75-83, bereikt de bedoelde ontvanger"),
    "pass_onderschept_%": (19.5, "17-22"),
    "pass_uit_%": (2, "ca. 2"),
    "wissels_totaal": (188, "94 per team, event PossessionChange (totaal beide teams)"),
    "duelpogingen": (33, "tackles + overtredingen, event Duel"),
    "duel_overtreding_%": (38, ""),
    "duel_balwinst_%": (37, ""),
    "duel_losse_bal_%": (10, ""),
    "overtredingen": (12.5, "p10-p90 8-18"),
    "geel": (1.58, ""),
    "rood_totaal": (0.08, "direct rood + tweede geel"),
    "schoten": (13, "p10-p90 7-20"),
    "doelpunten": (1.3, ""),
    "doelpunt_per_schot_%": (10, ""),
    "buitenspel": (1.8, ""),
    "inworpen": (23, ""),
    "corners": (5.4, ""),
    "onderscheppingen": (None, "event Interception"),
}
PASS_BINS = [(0, 5), (5, 10), (10, 15), (15, 20), (20, 30), (30, 40), (40, 100)]
# Doelen per BEDOELDE lengte uit het lekvrije calibration/event_models_v2.json (alleen passes met bekende beoogde ontvanger,
# dus een licht optimistische bovengrens). De eerdere doelen (46.8, 88.4, ... 43.5) kwamen uit het oude model op de WERKELIJKE lengte
# en bevatten uitkomst-lekkage: mislukte passes eindigen daar korter of langer dan bedoeld.
_V2 = json.loads((ROOT / "calibration" / "event_models_v2.json").read_text(encoding="utf-8"))["pass_geslaagd_v2"]["geslaagd_per_beoogde_lengte"]
PASS_TARGET = [100 * row["geslaagd"] for row in _V2]
assert len(PASS_TARGET) == len(PASS_BINS)
SHOT_BINS = [(0, 6), (6, 10), (10, 16), (16, 22), (22, 30), (30, 100)]
SHOT_TARGET = [37.2, 16.6, 12.6, 5.4, 2.8, 2.6]
NEXT_TYPES = ("BallControl", "Interception", "Out", "Save", "Goal", "Offside", "Foul", "DoubleTouch", "Duel", "Pass", "Shot")
KEYS = ("passes", "ok", "icpt", "out", "changes", "duels", "d_foul", "d_won", "d_loose", "fouls", "yellow", "red",
        "shots", "goals", "offside", "throw", "corner", "icpt_total")


def run_seed(seed, home, away, label, config=None):
    ev = OUT / f"e_{label}_{seed}.jsonl"
    summ = OUT / f"x_{label}_{seed}.json"
    cmd = ["dotnet", "run", "--no-build", "--project", "sim/Zeus.Runner", "-c", "Release", "--", "--home", home,
           "--away", away, "--seed", str(seed), "--until-finished", "--summary", str(summ), "--events", str(ev)]
    if config:
        cmd += ["--config", config]
    subprocess.run(cmd, cwd=ROOT, check=True, capture_output=True)
    return ev


def analyse(path):
    events = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    res = {}
    for team in ("Home", "Away"):
        res[team] = dict.fromkeys(KEYS, 0)
        res[team]["pbin"] = [[0, 0] for _ in PASS_BINS]
        res[team]["sbin"] = [[0, 0] for _ in SHOT_BINS]
    for i, e in enumerate(events):
        t = res.get(e["team"])
        if t is None:
            continue
        ty, out = e["type"], e["outcome"]
        if ty == "Pass":
            t["passes"] += 1
            parts = out.split(" ")
            recv = parts[1] if out.startswith("to ") and len(parts) > 1 else None
            nxt = next((f for f in events[i + 1:i + 80] if f["type"] in NEXT_TYPES), None)
            ok = (nxt is not None and nxt["type"] in ("BallControl", "Interception") and nxt["team"] == e["team"]
                  and recv is not None and nxt["playerId"] == recv)
            if ok:
                t["ok"] += 1
            elif nxt is not None and nxt["type"] == "Interception" or (
                    nxt is not None and nxt["type"] == "BallControl" and nxt["team"] != e["team"]):
                t["icpt"] += 1
            elif nxt is None or nxt["type"] in ("Out", "Offside", "Goal", "Save"):
                t["out"] += 1
            if len(parts) > 2:  # length bins (open play only: restarts have no length)
                length = float(parts[2])
                for b, (lo, hi) in enumerate(PASS_BINS):
                    if lo <= length < hi:
                        t["pbin"][b][0] += 1
                        t["pbin"][b][1] += 1 if ok else 0
                        break
        elif ty == "Interception":
            t["icpt_total"] += 1
        elif ty == "PossessionChange":
            t["changes"] += 1
        elif ty == "Duel":
            t["duels"] += 1
            if out == "Foul":
                t["d_foul"] += 1
            elif out == "WonBall":
                t["d_won"] += 1
            elif out == "LooseBall":
                t["d_loose"] += 1
        elif ty == "Foul":
            t["fouls"] += 1
        elif ty == "Card":
            if out in ("Yellow", "SecondYellowRed"):
                t["yellow"] += 1
            if out in ("Red", "SecondYellowRed"):
                t["red"] += 1
        elif ty == "Shot" and not out.startswith("restart"):
            t["shots"] += 1
            gx = 52.5 if e["location"]["x"] >= 0 else -52.5
            dist = ((gx - e["location"]["x"]) ** 2 + e["location"]["y"] ** 2) ** .5
            goal = any(f["type"] == "Goal" and f["team"] == e["team"] and f["tick"] - e["tick"] <= 80 for f in events[i + 1:i + 40])
            if goal:
                t["goals_shot"] = t.get("goals_shot", 0) + 1
            for b, (lo, hi) in enumerate(SHOT_BINS):
                if lo <= dist < hi:
                    t["sbin"][b][0] += 1
                    t["sbin"][b][1] += 1 if goal else 0
                    break
        elif ty == "Goal":
            t["goals"] += 1
        elif ty == "Offside":
            t["offside"] += 1
        elif ty == "Out":
            if out == "ThrowIn":
                t["throw"] += 1
            if out == "Corner":
                t["corner"] += 1
    return res


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", type=int, default=8)
    ap.add_argument("--home", default="tactics/laag_blok_442.json")
    ap.add_argument("--away", default="tactics/man_om_man_433.json")
    ap.add_argument("--label", default="cal")
    ap.add_argument("--config", default=None, help="MatchConfig-overrides (JSON) voor de runner")
    ap.add_argument("--build", action="store_true", help="dotnet build -c Release first")
    ap.add_argument("--reuse", action="store_true", help="analyse existing event files only")
    a = ap.parse_args()
    if a.build:
        subprocess.run(["dotnet", "build", "sim/Zeus.Runner", "-c", "Release", "-v", "q", "--nologo"], cwd=ROOT, check=True)
    OUT.mkdir(parents=True, exist_ok=True)
    seeds = list(range(1, a.seeds + 1))
    if a.reuse:
        files = [OUT / f"e_{a.label}_{s}.jsonl" for s in seeds]
    else:
        with ThreadPoolExecutor(max_workers=4) as ex:
            files = list(ex.map(lambda s: run_seed(s, a.home, a.away, a.label, a.config), seeds))
    allres = [analyse(f) for f in files]
    def metrics(teams):
        n = len(allres) * len(teams)

        def tot(key):
            return sum(r[t].get(key, 0) for r in allres for t in teams) / n

        ps, du, sh = tot("passes"), tot("duels"), tot("shots")
        return {
            "passes": ps,
            "pass_geslaagd_%": 100 * tot("ok") / ps if ps else 0,
            "pass_onderschept_%": 100 * tot("icpt") / ps if ps else 0,
            "pass_uit_%": 100 * tot("out") / ps if ps else 0,
            "wissels_totaal": tot("changes") * 2,
            "duelpogingen": du,
            "duel_overtreding_%": 100 * tot("d_foul") / du if du else 0,
            "duel_balwinst_%": 100 * tot("d_won") / du if du else 0,
            "duel_losse_bal_%": 100 * tot("d_loose") / du if du else 0,
            "overtredingen": tot("fouls"),
            "geel": tot("yellow"),
            "rood_totaal": tot("red"),
            "schoten": sh,
            "doelpunten": tot("goals"),
            "doelpunt_per_schot_%": 100 * tot("goals_shot") / sh if sh else 0,
            "buitenspel": tot("offside"),
            "inworpen": tot("throw"),
            "corners": tot("corner"),
            "onderscheppingen": tot("icpt_total"),
        }

    m, mh, ma = metrics(("Home", "Away")), metrics(("Home",)), metrics(("Away",))
    print(f"{len(allres)} seeds, gemiddelde per team per wedstrijd (label {a.label}); thuis = laag_blok_442, uit = man_om_man_433")
    print(f"{'maat':24s}{'sim':>10s}{'doel':>10s}{'thuis':>10s}{'uit':>10s}   bron/opmerking")
    for k, (tg, note) in TARGETS.items():
        print(f"{k:24s}{m[k]:10.2f}{'' if tg is None else format(tg, '.2f'):>10s}{mh[k]:10.2f}{ma[k]:10.2f}   {note}")
    print("\nPassgeslaagd per bedoelde lengte (open spel)  sim% (n) vs doel% (v2, bovengrens)")
    for b, (lo, hi) in enumerate(PASS_BINS):
        n_ = sum(r[t]["pbin"][b][0] for r in allres for t in r)
        k_ = sum(r[t]["pbin"][b][1] for r in allres for t in r)
        print(f"  {lo:>3}-{hi:<3} m  {100 * k_ / n_ if n_ else 0:6.1f}% (n={n_:5d})  doel {PASS_TARGET[b]:.1f}%")
    print("\nDoelpunt per schot per afstand  sim% (n) vs doel%")
    for b, (lo, hi) in enumerate(SHOT_BINS):
        n_ = sum(r[t]["sbin"][b][0] for r in allres for t in r)
        k_ = sum(r[t]["sbin"][b][1] for r in allres for t in r)
        print(f"  {lo:>3}-{hi:<3} m  {100 * k_ / n_ if n_ else 0:6.1f}% (n={n_:5d})  doel {SHOT_TARGET[b]:.1f}%")


if __name__ == "__main__":
    main()
