"""Poort voor 3D (T1): Match.Step mag in godot/ alleen in MatchDriver.cs worden aangeroepen.

Gebruik: python scripts/check_match_step.py   (exit 1 bij een treffer buiten MatchDriver.cs)
Uitgezonderd: godot/tests/ (xunit) en de offline simulaties van Editor.Test.cs en Meter.cs (los van de live wedstrijd).
"""
import re
import sys
from pathlib import Path

GODOT = Path(__file__).resolve().parent.parent / "godot"
TOEGESTAAN = {"MatchDriver.cs", "Editor.Test.cs", "Meter.cs"}
STEP = re.compile(r"\.Step\(\)")

fouten = []
for f in sorted(GODOT.rglob("*.cs")):
    if f.name in TOEGESTAAN or "tests" in f.relative_to(GODOT).parts:
        continue
    for n, regel in enumerate(f.read_text(encoding="utf-8").splitlines(), 1):
        if STEP.search(regel):
            fouten.append(f"{f.relative_to(GODOT.parent).as_posix()}:{n}: {regel.strip()}")
if fouten:
    print("Match.Step buiten MatchDriver.cs:\n" + "\n".join(fouten))
    sys.exit(1)
print("OK: geen Match.Step buiten MatchDriver.cs")
