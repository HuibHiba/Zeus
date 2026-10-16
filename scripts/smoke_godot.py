"""Rooktest voor de Godot-app: start elk scherm headless en meld fouten.

Gebruik (vanuit C:\\dev\\zeus):
  C:\\dev\\zeus\\.venv\\Scripts\\python.exe scripts\\smoke_godot.py [--root C:\\dev\\zeus] [--scenes Menu,Editor,...]

Per scherm: bouwt het project eenmalig, start Godot headless met --quit-after en zoekt in de uitvoer naar 'ERROR',
'SCRIPT ERROR', 'Unhandled exception' en 'Parser Error'. Exitcode 0 = alles schoon. Dit controleert alleen dat de schermen
zonder fouten laden en niet crashen, niet hoe ze eruitzien of werken.
"""
import argparse
import os
import subprocess
import sys
from pathlib import Path

GODOT = Path(os.environ["LOCALAPPDATA"]) / ("Microsoft/WinGet/Packages/GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe/"
                                           "Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe")
BAD = ("SCRIPT ERROR", "Unhandled exception", "Parser Error", "ERROR:", "Failed to load", "Cannot open file")
# Tekstregels die geen echte fout zijn (omgeving van de testmachine).
IGNORE = ("certificate", "Certificate", "TLS", "XR", "OpenXR", "audio driver", "ALSA", "Dummy")
SCENES = ["Menu", "Main", "Editor", "Meter", "Challenges", "Season", "Squad"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=str(Path(__file__).resolve().parent.parent))
    ap.add_argument("--scenes", default=",".join(SCENES))
    ap.add_argument("--frames", type=int, default=40)
    a = ap.parse_args()
    root = Path(a.root)
    project = root / "godot"
    if not GODOT.exists():
        print(f"Godot niet gevonden op {GODOT}")
        return 2
    build = subprocess.run(["dotnet", "build", str(project / "Zeus.Godot.csproj"), "--nologo", "-v", "q"], capture_output=True, text=True)
    if build.returncode != 0:
        print("BOUWEN MISLUKT\n" + build.stdout[-2000:])
        return 1
    failures = 0
    for scene in a.scenes.split(","):
        scene = scene.strip()
        tscn = project / f"{scene}.tscn"
        if not tscn.exists():
            print(f"SKIP  {scene}: {tscn.name} bestaat niet")
            continue
        try:
            r = subprocess.run([str(GODOT), "--headless", "--path", str(project), f"res://{scene}.tscn", "--quit-after", str(a.frames)],
                               capture_output=True, text=True, timeout=180)
            out = r.stdout + "\n" + r.stderr
            bad = [ln.strip() for ln in out.splitlines() if any(b in ln for b in BAD) and not any(i in ln for i in IGNORE)]
            code = r.returncode
        except subprocess.TimeoutExpired:
            bad, code = ["timeout na 180 s"], -1
        if bad or code not in (0,):
            failures += 1
            print(f"FOUT  {scene} (exit {code})")
            for ln in bad[:6]:
                print("      " + ln)
        else:
            print(f"OK    {scene}")
    print("ALLES SCHOON" if failures == 0 else f"{failures} scherm(en) met fouten")
    return 0 if failures == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
