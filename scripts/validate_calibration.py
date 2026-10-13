"""Controleert alle calibration/*.json: elk bestand heeft een bovenliggend veld `licentie_status` en bevat geen NaN of Infinity.

Strikte JSON-parsers (en Godot/C#) weigeren NaN/Infinity; een ontbrekende licentie-aanduiding maakt onduidelijk of een model
in het product mag. Draai met `python -I scripts/validate_calibration.py`; exitcode 1 bij een fout of als er geen bestanden zijn.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CAL = ROOT / "calibration"


def _reject(constant: str):
    raise ValueError(f"niet-eindige waarde {constant}")


def check(path: Path) -> list[str]:
    try:
        # parse_constant wordt alleen aangeroepen voor NaN, Infinity en -Infinity
        data = json.loads(path.read_text(encoding="utf-8"), parse_constant=_reject)
    except (OSError, ValueError) as exc:
        return [f"ongeldige JSON: {exc}"]
    errors = []
    if not isinstance(data, dict):
        return ["hoogste niveau is geen object"]
    lic = data.get("licentie_status")
    if not isinstance(lic, str) or not lic.strip():
        errors.append("veld licentie_status ontbreekt of is leeg")
    return errors


def main() -> int:
    paths = sorted(CAL.glob("*.json"))
    if not paths:
        print(f"FOUT: geen calibration/*.json gevonden in {CAL}")
        return 1
    failed = 0
    for path in paths:
        errors = check(path)
        print(f"{'OK  ' if not errors else 'FOUT'} {path.name}")
        for e in errors:
            print(f"     - {e}")
        failed += bool(errors)
    print(f"{len(paths) - failed} van {len(paths)} bestanden in orde")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
