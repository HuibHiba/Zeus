"""Regressiecontrole voor een release-kandidaat (roadmap A3): alle bestaande controles in één opdracht, strikt na elkaar.

Gebruik (vanuit de repo):
  python scripts/release_check.py [--root C:/dev/zeus] [--exe build/GaffersNotebook.exe] [--skip test,smoke] [--only build]
                                  [--out MAP] [--dry-run]

Stappen (in deze volgorde, nooit twee zware processen tegelijk; zie docs/release-check.md):
  build     dotnet build Zeus.sln (Release, -m:1)
  test      dotnet test sim/Zeus.Sim.Tests (Release, --no-build, xUnit-parallelisme uit)
  smoke     scripts/smoke_godot.py (bouwt zelf het Godot-project, Debug)
  autotest  scripts/run_autotest.py --timeout 560, zonder de bewust rode scripts (EXPECTED_RED)
  autotest_expected_red  dezelfde runner voor alleen de bewust rode scripts; rood is hier het verwachte resultaat
  playtest  scripts/playtest_exe.py op de release-exe (alleen met --exe; laatste stap 'DEAD' is daar al verwacht)
  package   scripts/check_release_content.py --package <map van de exe> (alleen met --exe)
Na afloop altijd 'dotnet build-server shutdown'. Per stap een logbestand in --out (standaard <root>/local/release_check/<tijd>/).
Exitcode 0 alleen als elke stap groen of verwacht-rood is. Een verwacht-rode stap die groen wordt telt als fout: haal het
script dan uit EXPECTED_RED. Geen uitvoer van de deelscripts wordt geparsed: alleen exitcodes.
"""
import argparse
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

PY = sys.executable
# Autotestscripts (stem) die bewust rood staan; zie docs/hervatten.md.
EXPECTED_RED = ("10a_save_damaged",)
ORDER = ["build", "test", "smoke", "autotest", "autotest_expected_red", "playtest", "package"]


def plan(root: Path, exe, out: Path):
    """[(naam, argv, verwacht_rood)] in uitvoervolgorde."""
    r = str(root)
    scripts = sorted(p.stem for p in (root / "scripts" / "autotest").glob("*.json"))
    red = [s for s in scripts if s in EXPECTED_RED]
    green = [s for s in scripts if s not in EXPECTED_RED]
    steps = [
        ("build", ["dotnet", "build", "Zeus.sln", "-c", "Release", "-m:1", "--nologo", "-p:UseSharedCompilation=false", "-nodeReuse:false"], False),
        ("test", ["dotnet", "test", "sim/Zeus.Sim.Tests", "-c", "Release", "--no-build", "-m:1", "--nologo", "--", "xUnit.ParallelizeTestCollections=false"], False),
        ("smoke", [PY, "scripts/smoke_godot.py", "--root", r], False),
        ("autotest", [PY, "scripts/run_autotest.py", "--root", r, "--timeout", "560", "--only", ",".join(green)], False),
    ]
    if red:
        steps.append(("autotest_expected_red", [PY, "scripts/run_autotest.py", "--root", r, "--timeout", "560", "--only", ",".join(red)], True))
    if exe:
        exe = Path(exe).resolve()
        steps.append(("playtest", [PY, "scripts/playtest_exe.py", "-ExePath", str(exe), "-OutDir", str(out / "playtest")], False))
        steps.append(("package", [PY, "scripts/check_release_content.py", "--package", str(exe.parent)], False))
    return steps


def run(argv, cwd: Path, log: Path) -> int:
    """Start zonder shell (lijst-argumenten); stdout+stderr naar console en logbestand."""
    with log.open("w", encoding="utf-8") as f, subprocess.Popen(argv, cwd=str(cwd), stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                                                 text=True, encoding="utf-8", errors="replace") as p:
        for line in p.stdout:
            sys.stdout.write(line)
            f.write(line)
    return p.returncode


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=Path(__file__).resolve().parent.parent.as_posix())
    ap.add_argument("--exe", default=None, help="release-exe; zet de stappen playtest en package aan")
    ap.add_argument("--skip", default="", help="stappen overslaan, komma-gescheiden: " + ",".join(ORDER))
    ap.add_argument("--only", default="", help="alleen deze stappen, komma-gescheiden")
    ap.add_argument("--out", default=None, help="logmap (standaard <root>/local/release_check/<tijd>)")
    ap.add_argument("--dry-run", action="store_true", help="alleen het plan tonen, niets starten")
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    root = Path(a.root).resolve()
    out = Path(a.out).resolve() if a.out else root / "local" / "release_check" / datetime.now().strftime("%Y%m%d-%H%M%S")
    chosen = {s for s in (a.only or ",".join(ORDER)).split(",") if s} - {s for s in a.skip.split(",") if s}
    unknown = chosen - set(ORDER)
    if unknown:
        print("onbekende stap(pen): " + ", ".join(sorted(unknown)) + "; keuze uit " + ", ".join(ORDER))
        return 2
    steps = [s for s in plan(root, a.exe, out) if s[0] in chosen]
    if not steps:
        print("geen stappen gekozen")
        return 2

    print(f"root: {root}\nlogs: {out}")
    for i, (name, argv, red) in enumerate(steps, 1):
        print(f"{i}. {name}{' (verwacht rood)' if red else ''}: {subprocess.list2cmdline(argv)}")
    print("daarna: dotnet build-server shutdown")
    if a.dry_run:
        return 0

    out.mkdir(parents=True, exist_ok=True)
    results = []
    try:
        for i, (name, argv, red) in enumerate(steps, 1):
            print(f"\n== {name} ({i}/{len(steps)}) {datetime.now():%H:%M:%S}", flush=True)
            t0 = time.monotonic()
            try:
                code = run(argv, root, out / f"{i:02d}_{name}.log")
            except OSError as e:          # programma niet gevonden e.d.
                print(f"start mislukt: {e}")
                code = -1
            secs = time.monotonic() - t0
            if red:
                verdict = "VERWACHT ROOD" if code != 0 else "ONVERWACHT GROEN (haal het script uit EXPECTED_RED)"
            else:
                verdict = "GROEN" if code == 0 else "ROOD"
            results.append((name, verdict, code, secs))
            print(f"== {name}: {verdict} (exit {code}, {secs / 60:.1f} min)", flush=True)
            if name == "build" and code != 0:
                print("build mislukt; overige stappen overgeslagen")
                break
    finally:
        subprocess.run(["dotnet", "build-server", "shutdown"], cwd=str(root), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    ok = all(v in ("GROEN", "VERWACHT ROOD") for _, v, _, _ in results) and len(results) == len(steps)
    table = "\n".join(f"{n:<22} {v:<16} exit {c:<4} {s / 60:6.1f} min" for n, v, c, s in results)
    summary = f"\n{table}\n\nTOTAAL {sum(s for *_, s in results) / 60:.1f} min: {'GROEN' if ok else 'ROOD'}\nlogs: {out}"
    print(summary)
    (out / "result.txt").write_text(summary.strip() + "\n", encoding="utf-8")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
