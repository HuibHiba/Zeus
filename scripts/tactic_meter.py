"""Effectiviteitsmeter (PRD mijlpaal 3): vergelijk twee tactieken over N wedstrijden.

Gebruik (vanuit C:\\dev\\zeus):
  .\\.venv\\Scripts\\python.exe scripts\\tactic_meter.py tactics/laag_blok_442.json tactics/man_om_man_433.json [--n 20] [--out data/meter/rapport.md]

Elke seed wordt twee keer gespeeld met omgewisselde thuis/uit-rol, zodat een thuis/uit-verschil de vergelijking niet kleurt.
Uitvoer: uitslagen, gemiddelden per tactiek en de drie grootste verschillen ("waarom"-lijst).
Dit meet tactisch verschil in de simulatie; het is geen bewijs van voetbalrealisme.
"""
import argparse
import json
import subprocess
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "data" / "meter"
STATS = ("goals", "shots", "passes", "ballControls", "interceptions", "fouls", "yellowCards", "redCards")
LABELS = {"goals": "doelpunten", "shots": "schoten", "passes": "passes", "ballControls": "balcontacten",
          "interceptions": "onderscheppingen", "fouls": "overtredingen", "yellowCards": "gele kaarten", "redCards": "rode kaarten"}


def play(job):
    seed, home, away, tag = job
    summ = OUT / f"s_{tag}_{seed}.json"
    cmd = ["dotnet", "run", "--project", "sim/Zeus.Runner", "-c", "Release", "--no-build", "--", "--home", home, "--away", away,
           "--seed", str(seed), "--until-finished", "--out", str(OUT / f"m_{tag}_{seed}.jsonl"), "--every", "1000",
           "--summary", str(summ)]
    subprocess.run(cmd, cwd=ROOT, check=True, capture_output=True)
    return json.loads(summ.read_text(encoding="utf-8"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("tactic_a")
    ap.add_argument("tactic_b")
    ap.add_argument("--n", type=int, default=20, help="aantal seeds (elk twee keer gespeeld)")
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--out", default=str(OUT / "rapport.md"))
    args = ap.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    subprocess.run(["dotnet", "build", "sim/Zeus.Runner", "-c", "Release", "--nologo", "-v", "q"], cwd=ROOT, check=True, capture_output=True)

    jobs = []
    for seed in range(1, args.n + 1):
        jobs.append((seed, args.tactic_a, args.tactic_b, "ab"))   # A thuis
        jobs.append((seed, args.tactic_b, args.tactic_a, "ba"))   # B thuis
    with ThreadPoolExecutor(args.workers) as pool:
        results = list(pool.map(play, jobs))

    tot = {"a": {k: 0 for k in STATS}, "b": {k: 0 for k in STATS}}
    wins = draws = losses = 0
    for (seed, home, away, tag), r in zip(jobs, results):
        a, b = (r["home"], r["away"]) if tag == "ab" else (r["away"], r["home"])
        for k in STATS:
            tot["a"][k] += a[k]
            tot["b"][k] += b[k]
        wins += a["goals"] > b["goals"]
        losses += a["goals"] < b["goals"]
        draws += a["goals"] == b["goals"]
    m = len(jobs)
    avg = {s: {k: tot[s][k] / m for k in STATS} for s in tot}
    share = {s: 100 * tot[s]["ballControls"] / max(1, tot["a"]["ballControls"] + tot["b"]["ballControls"]) for s in tot}

    lines = ["# Tactiekvergelijking", "",
             f"A = `{args.tactic_a}` · B = `{args.tactic_b}` · {m} wedstrijden ({args.n} seeds, thuis/uit gewisseld)", "",
             f"**Uitslagen voor A:** {wins} winst · {draws} gelijk · {losses} verlies "
             f"(punten/wedstrijd {(3 * wins + draws) / m:.2f} tegen {(3 * losses + draws) / m:.2f})", "",
             "| Maat | A | B |", "|---|---:|---:|"]
    for k in STATS:
        lines.append(f"| {LABELS[k]} per wedstrijd | {avg['a'][k]:.2f} | {avg['b'][k]:.2f} |")
    lines.append(f"| balbezit (aandeel balcontacten) | {share['a']:.1f}% | {share['b']:.1f}% |")

    diffs = sorted(((abs(avg['a'][k] - avg['b'][k]) / max(1e-9, (avg['a'][k] + avg['b'][k]) / 2), k) for k in STATS
                    if k not in ("goals", "redCards")), reverse=True)[:3]
    lines += ["", "## Waarom (3 grootste verschillen)"]
    for rel, k in diffs:
        hi = "A" if avg["a"][k] > avg["b"][k] else "B"
        lines.append(f"- **{LABELS[k]}**: {hi} heeft er {100 * rel:.0f}% meer/minder dan het gemiddelde ({avg['a'][k]:.1f} vs {avg['b'][k]:.1f}).")
    lines += ["", "*Verdiende winst:* vergelijk doelpunten met schoten; veel schoten maar weinig goals wijst op toeval of lage kwaliteit.", ""]
    Path(args.out).write_text("\n".join(lines), encoding="utf-8")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
