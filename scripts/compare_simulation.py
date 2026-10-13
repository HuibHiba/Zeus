"""Compare runner event totals with a local team-match benchmark; no calibration verdict."""
import argparse
import csv
import json
import math
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
COUNTERS = ("passes", "ballControls", "interceptions", "fouls", "yellowCards", "redCards")


def nonnegative_number(value, label):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
        raise ValueError(f"{label} must be a finite nonnegative number")
    return value


def compare(summary_path, benchmark_path):
    summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
    status = summary.get("status")
    if status != "Finished":
        raise ValueError(f"match status is {status!r}, not 'Finished'; refusing to extrapolate a partial match "
                         "(re-run the runner with --until-finished)")
    seconds = nonnegative_number(summary["timeSeconds"], "timeSeconds")
    if seconds == 0:
        raise ValueError("timeSeconds must be positive for a rate comparison")
    with benchmark_path.open(encoding="utf-8-sig", newline="") as source:
        reader = csv.DictReader(source)
        if "passes" not in (reader.fieldnames or []):
            raise ValueError("Benchmark CSV must contain a passes column")
        reference_values = [nonnegative_number(float(row["passes"]), "benchmark passes") for row in reader]
    if not reference_values:
        raise ValueError("Benchmark CSV has no team-match rows")
    reference = statistics.mean(reference_values)
    warnings = [
        "90-minute rates extrapolate event totals; a short run is not a full match.",
        "The reference is per recorded team-match; stoppage time is not normalized in the CSV.",
        "ballControls includes loose-ball contacts and does not measure pass accuracy.",
        "No pass-success percentage: totals do not link each pass to its outcome.",
        "This comparison gives deviations, not a calibration PASS/FAIL verdict.",
        "The benchmark CSV has no fouls or cards reference; these counters are diagnostic only.",
    ]
    teams = {}
    for side in ("home", "away"):
        counters = summary[side]
        totals = {}
        for key in COUNTERS:
            count = nonnegative_number(counters[key], f"{side}.{key}")
            if int(count) != count:
                raise ValueError(f"{side}.{key} must be an integer event count")
            totals[key] = int(count)
        rate = totals["passes"] * 5400 / seconds
        teams[side] = {
            "totals": totals,
            "passesPer90Minutes": rate,
            "passesDeviationPercent": (rate / reference - 1) * 100 if reference else None,
        }
    return {
        "summaryPath": str(summary_path.resolve()),
        "benchmarkPath": str(benchmark_path.resolve()),
        "timeSeconds": seconds,
        "reference": {"teamMatchRows": len(reference_values), "meanPassesPerTeamMatch": reference},
        "teams": teams,
        "warnings": warnings,
    }


def markdown(report):
    reference = report["reference"]
    lines = [
        "# Simulatie versus lokale benchmark", "",
        f"Speelduur: {report['timeSeconds']:g} s. Referentie: {reference['teamMatchRows']} teamwedstrijden, "
        f"gemiddeld {reference['meanPassesPerTeamMatch']:.3f} passes per teamwedstrijd.", "",
        "| Team | Passes | Passes per 90 min (extrapolatie) | Afwijking |",
        "| --- | ---: | ---: | ---: |",
    ]
    for side, team in report["teams"].items():
        deviation = team["passesDeviationPercent"]
        formatted = f"{deviation:+.2f}%" if deviation is not None else "geen referentie"
        lines.append(f"| {side} | {team['totals']['passes']} | {team['passesPer90Minutes']:.2f} | {formatted} |")
    lines += ["", "Diagnostische eventtotalen (geen passnauwkeurigheid):", "",
              "| Team | Balcontacten | Intercepties | Overtredingen | Geel | Rood |",
              "| --- | ---: | ---: | ---: | ---: | ---: |"]
    for side, team in report["teams"].items():
        totals = team["totals"]
        lines.append(f"| {side} | " + " | ".join(str(totals[k]) for k in COUNTERS[1:]) + " |")
    lines += ["", "Beperkingen:", ""] + [f"- {warning}" for warning in report["warnings"]]
    lines += ["", f"Bronbestand: `{report['benchmarkPath']}`.", f"Simulatiebestand: `{report['summaryPath']}`."]
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("summary", type=Path, help="Runner JSON from --summary")
    parser.add_argument("--benchmark", type=Path, default=ROOT / "data" / "pl_1516_team_match.csv")
    parser.add_argument("--out", type=Path, help="Destination; .md selects Markdown unless --format overrides")
    parser.add_argument("--format", choices=("json", "markdown"))
    args = parser.parse_args()
    try:
        report = compare(args.summary, args.benchmark)
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.error(str(error))
    output_format = args.format or ("markdown" if args.out and args.out.suffix.lower() == ".md" else "json")
    output = markdown(report) if output_format == "markdown" else json.dumps(report, indent=2, ensure_ascii=False, allow_nan=False) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output, encoding="utf-8")
    else:
        sys.stdout.write(output)


if __name__ == "__main__":
    main()
