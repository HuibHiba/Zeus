"""UI-autotests: start de game met een venster en laat godot/AutoTest.cs elk script in scripts/autotest/ doorlopen.

Gebruik (vanuit de repo):
  python scripts\\run_autotest.py [--root C:/dev/zeus] [--only menu,library] [--build]

Per script: eigen lege APPDATA (.tmp_appdata/autotest_<naam>, dus eerste start met tour en welkom), ZEUS_NOSAVE=1, ZEUS_LANG=en
en de regels 'env NAAM=WAARDE' uit het script. Uitvoer (autotest.log en screenshots) komt in .tmp_autotest/<naam>/.
Exitcode 0 = alle stappen PASS en geen fouten in de Godot-uitvoer. Niet headless: de stappen sturen echte invoer en maken
screenshots, daar is een venster voor nodig. --build bouwt eerst (Debug); zonder --build moet het project al gebouwd zijn.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

from smoke_godot import BAD, GODOT, IGNORE


def steps(path):
    text = path.read_text(encoding="utf-8")
    return json.loads(text) if text.lstrip().startswith("[") else text.splitlines()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=Path(__file__).resolve().parent.parent.as_posix())
    ap.add_argument("--only", default="")
    ap.add_argument("--build", action="store_true")
    ap.add_argument("--timeout", type=int, default=300)
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    root = Path(a.root)
    project = root / "godot"
    if not GODOT.exists():
        print(f"Godot niet gevonden op {GODOT}")
        return 2
    if a.build and subprocess.run(["dotnet", "build", str(project / "Zeus.Godot.csproj"), "--nologo", "-v", "q"]).returncode != 0:
        return 1
    scripts = sorted((root / "scripts" / "autotest").glob("*.json"))
    if a.only:
        scripts = [s for s in scripts if s.stem in a.only.split(",")]
    failures = 0
    for script in scripts:
        name = script.stem
        out = root / ".tmp_autotest" / name
        appdata = root / ".tmp_appdata" / f"autotest_{name}"
        for d in (out, appdata):
            shutil.rmtree(d, ignore_errors=True)
            d.mkdir(parents=True)
        env = dict(os.environ, APPDATA=str(appdata), ZEUS_NOSAVE="1", ZEUS_LANG="en", ZEUS_AUTOTEST_OUT=str(out))
        game_steps = []
        for line in steps(script):
            if line.strip().startswith("env "):
                key, _, value = line.strip()[4:].partition("=")
                env[key.strip()] = value.strip()
            else:
                game_steps.append(line)
        (out / "steps.json").write_text(json.dumps(game_steps, indent=1), encoding="utf-8")      # the game only sees its own steps
        env["ZEUS_AUTOTEST"] = str(out / "steps.json")
        try:
            r = subprocess.run([str(GODOT), "--path", str(project)], env=env, capture_output=True, text=True,
                               encoding="utf-8", errors="replace", timeout=a.timeout)
            output, code = r.stdout + "\n" + r.stderr, r.returncode
        except subprocess.TimeoutExpired as e:
            output, code = (e.stdout or b"").decode("utf-8", "replace") if isinstance(e.stdout, bytes) else (e.stdout or ""), -1
        (out / "godot.txt").write_text(output, encoding="utf-8")
        bad = [ln.strip() for ln in output.splitlines() if any(b in ln for b in BAD) and not any(i in ln for i in IGNORE)]
        log = (out / "autotest.log").read_text(encoding="utf-8").splitlines() if (out / "autotest.log").exists() else ["geen autotest.log (timeout of crash)"]
        ok = code == 0 and not bad and log[-1].startswith("PASS")
        failures += not ok
        print(f"{'OK  ' if ok else 'FOUT'}  {name} (exit {code})")
        for ln in log:
            print("      " + ln)
        for ln in bad[:8]:
            print("      godot: " + ln)
    print("ALLES PASS" if failures == 0 else f"{failures} script(s) met fouten")
    return 0 if failures == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
