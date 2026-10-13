"""Meet de simulatie over meerdere tactiekparen (standaard 6 paren x 20 seeds) en rapporteer per team het gemiddelde,
de spreiding en het verschil tussen de paren, naast de referentiedoelen.

Gebruik (vanuit C:\\dev\\zeus):
  .\\.venv\\Scripts\\python.exe scripts\\calibration_pairs.py --build --label r3 [--seeds 20] [--config cfg.json]
         [--pairs LL,MM,TT,LM,ML,TL] [--reuse]

Tactieken: L = laag_blok_442, M = man_om_man_433, T = testcase_a_433. Een paar 'LM' = L thuis, M uit.
Dit kalibreert aantallen; het is geen bewijs van voetbalrealisme. Hergebruikt de analyse van calibration_run.py.
"""
import argparse
import statistics
import subprocess
from concurrent.futures import ThreadPoolExecutor

import calibration_run as cr

TACTICS = {"L": "tactics/laag_blok_442.json", "M": "tactics/man_om_man_433.json", "T": "tactics/testcase_a_433.json"}
# (naam, doel, p10, p90) per team per wedstrijd; p10-p90 alleen waar de referentie ze geeft.
TARGETS = {
    "schoten": (13, 7, 20),
    "doelpunten": (1.3, None, None),
    "passes": (485, 347, 625),
    "overtredingen": (12.5, 8, 18),
    "geel": (1.58, None, None),
    "inworpen": (23, None, None),
    "corners": (5.4, None, None),
    "buitenspel": (1.8, None, None),
    "strafschoppen": (0.12, None, None),
}


RAW = {"schoten": "shots", "doelpunten": "goals", "passes": "passes", "overtredingen": "fouls", "geel": "yellow",
       "inworpen": "throw", "corners": "corner", "buitenspel": "offside", "strafschoppen": "pens"}


ABANDONED = []


def run_tolerant(seed, home, away, label, config):
    """Als calibration_run.run_seed, maar een Abandoned-wedstrijd (exit 2: minder dan zeven spelers) telt mee en wordt gemeld."""
    ev = cr.OUT / f"e_{label}_{seed}.jsonl"
    summ = cr.OUT / f"x_{label}_{seed}.json"
    cmd = ["dotnet", "run", "--no-build", "--project", "sim/Zeus.Runner", "-c", "Release", "--", "--home", home,
           "--away", away, "--seed", str(seed), "--until-finished", "--summary", str(summ), "--events", str(ev)]
    if config:
        cmd += ["--config", config]
    r = subprocess.run(cmd, cwd=cr.ROOT, capture_output=True)
    if r.returncode == 2:
        ABANDONED.append((label, seed))
    elif r.returncode != 0:
        raise RuntimeError(r.stderr.decode(errors="replace"))
    return ev


def per_team_values(allres, key):
    return [r[t].get(RAW[key], 0) for r in allres for t in ("Home", "Away")]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", type=int, default=20)
    ap.add_argument("--pairs", default="LL,MM,TT,LM,ML,TL")
    ap.add_argument("--label", default="pairs")
    ap.add_argument("--config", default=None)
    ap.add_argument("--build", action="store_true")
    ap.add_argument("--reuse", action="store_true")
    a = ap.parse_args()
    if a.build:
        subprocess.run(["dotnet", "build", "sim/Zeus.Runner", "-c", "Release", "-v", "q", "--nologo"], cwd=cr.ROOT, check=True)
    cr.OUT.mkdir(parents=True, exist_ok=True)
    pairs = a.pairs.split(",")
    seeds = list(range(1, a.seeds + 1))
    jobs = [(p, s) for p in pairs for s in seeds]

    def one(job):
        p, s = job
        label = f"{a.label}_{p}"
        if a.reuse:
            return cr.OUT / f"e_{label}_{s}.jsonl"
        return run_tolerant(s, TACTICS[p[0]], TACTICS[p[1]], label, a.config)

    with ThreadPoolExecutor(max_workers=8) as ex:
        files = list(ex.map(one, jobs))
    results = {p: [] for p in pairs}
    for (p, _), f in zip(jobs, files):
        results[p].append(cr.analyse(f))

    keys = list(TARGETS)
    # kolommen: gemiddelde per team per paar
    print(f"{len(seeds)} seeds per paar, label {a.label}; waarde = gemiddelde per team per wedstrijd (std over team-wedstrijden)")
    print(f"{'maat':14s}{'doel':>8s}" + "".join(f"{p:>14s}" for p in pairs) + f"{'gem':>9s}{'spreiding paren':>17s}")
    pair_means = {}
    for k in keys:
        tg, lo, hi = TARGETS[k]
        means = []
        cells = []
        for p in pairs:
            v = per_team_values(results[p], k)
            m, sd = statistics.mean(v), statistics.pstdev(v)
            means.append(m)
            cells.append(f"{m:7.1f}({sd:4.1f})")
        pair_means[k] = means
        print(f"{k:14s}{tg:8.2f}" + "".join(f"{c:>14s}" for c in cells)
              + f"{statistics.mean(means):9.2f}{max(means) - min(means):17.2f}")
    print("\nVerdeling per team-wedstrijd over alle paren: gemiddelde, p10, p90, min, max (doel; referentie p10-p90)")
    for k in keys:
        v = sorted(x for p in pairs for x in per_team_values(results[p], k))
        n = len(v)
        tg, lo, hi = TARGETS[k]
        ref = "" if lo is None else f"  ref p10-p90 {lo}-{hi}"
        print(f"{k:14s} gem {statistics.mean(v):7.2f}  p10 {v[int(0.1 * n)]:6.1f}  p90 {v[int(0.9 * n)]:6.1f}  min {v[0]:6.1f}  max {v[-1]:6.1f}  doel {tg}{ref}")
    print("\nPer paar buiten p10-p90 van de referentie (gemiddelde per team):")
    for k in keys:
        tg, lo, hi = TARGETS[k]
        if lo is None:
            continue
        bad = [f"{p}={m:.1f}" for p, m in zip(pairs, pair_means[k]) if not lo <= m <= hi]
        print(f"  {k:14s}: {'alle binnen' if not bad else ', '.join(bad)}")
    if ABANDONED:
        print(f"\nLET OP: {len(ABANDONED)} wedstrijd(en) afgebroken (minder dan zeven spelers) en meegeteld: {ABANDONED[:6]}")
    # Score: gemiddelde absolute log-afwijking per team-slot (6 paren x 2 teams) van schoten, overtredingen en passes t.o.v. het doel.
    import math
    slots = [(p, t) for p in pairs for t in ("Home", "Away")]
    def dev(key, target):
        return statistics.mean(abs(math.log((statistics.mean(r[t][RAW[key]] for r in results[p]) + 1) / (target + 1))) for p, t in slots)
    print(f"\nSCORE (lager is beter): schoten {dev('schoten', 13):.2f}, overtredingen {dev('overtredingen', 12.5):.2f}, passes {dev('passes', 485):.2f}, "
          f"totaal {dev('schoten', 13) + 0.5 * dev('overtredingen', 12.5) + 0.5 * dev('passes', 485):.2f}")
    pens = sum(r[t]["pens"] for p in pairs for r in results[p] for t in r)
    pgoal = sum(r[t]["pen_goals"] for p in pairs for r in results[p] for t in r)
    print(f"\nStrafschoppen totaal {pens} in {len(pairs) * len(seeds)} wedstrijden, gescoord {pgoal} ({100 * pgoal / pens if pens else 0:.1f}%)")
    print("Goals per team: " + ", ".join(f"{p} thuis {statistics.mean(r['Home']['goals'] for r in results[p]):.1f}/uit {statistics.mean(r['Away']['goals'] for r in results[p]):.1f}" for p in pairs))
    print("Schoten per team: " + ", ".join(f"{p} thuis {statistics.mean(r['Home']['shots'] for r in results[p]):.1f}/uit {statistics.mean(r['Away']['shots'] for r in results[p]):.1f}" for p in pairs))


if __name__ == "__main__":
    main()
