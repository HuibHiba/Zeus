"""Valideert alle tactiekbestanden in tactics/ tegen schema/tactic.schema.json.

Naast het schema: elk spelers-id in een fase komt overeen met `spelers`, en
triggers verwijzen alleen naar bestaande ids.
"""
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent.parent


def check(path: Path, schema: dict) -> list[str]:
    data = json.loads(path.read_text(encoding="utf-8"))
    errors = [f"{'/'.join(map(str, e.absolute_path))}: {e.message}" for e in Draft202012Validator(schema).iter_errors(data)]
    if errors:
        return errors
    ids = {s["id"] for s in data["spelers"]}
    if len(ids) != 11:
        errors.append("spelers: ids niet uniek")
    for naam, fase in data["fases"].items():
        if "gelijk_aan" in fase:
            if fase["gelijk_aan"] == naam or "gelijk_aan" in data["fases"][fase["gelijk_aan"]]:
                errors.append(f"fases/{naam}: verwijst naar zichzelf of naar een andere verwijzing")
            continue
        fase_ids = [s["id"] for s in fase["spelers"]]
        if set(fase_ids) != ids or len(fase_ids) != 11:
            errors.append(f"fases/{naam}: spelers komen niet overeen met de selectie")
    varianten = data.get("verdedigen_varianten", {})
    for naam, fase in varianten.items():
        if {s["id"] for s in fase["spelers"]} != ids or len(fase["spelers"]) != 11:
            errors.append(f"verdedigen_varianten/{naam}: spelers komen niet overeen met de selectie")
    for w in data.get("blokwissels", []):
        if w["naar"] not in varianten:
            errors.append(f"blokwissel naar '{w['naar']}': geen variant met die blokhoogte in verdedigen_varianten")
    alle_fases = [f for f in data["fases"].values() if "spelers" in f] + list(varianten.values())
    for fase in alle_fases:
        for s in fase["spelers"]:
            if "dek" in s and "dek_man" not in s.get("instructies", []):
                errors.append(f"speler {s['id']}: 'dek' gezet zonder instructie dek_man")
            if "dek_man" in s.get("instructies", []) and "dek" not in s:
                errors.append(f"speler {s['id']}: dek_man zonder 'dek' (welke tegenstander?)")
    for t in data.get("triggers", []):
        for actie in t["dan"]:
            if actie["speler"] not in ids:
                errors.append(f"trigger '{t.get('naam', '?')}': onbekende speler {actie['speler']}")
    return errors


def main() -> int:
    schema = json.loads((ROOT / "schema" / "tactic.schema.json").read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)
    failed = 0
    for path in sorted((ROOT / "tactics").glob("*.json")):
        errors = check(path, schema)
        print(f"{'OK  ' if not errors else 'FOUT'} {path.name}")
        for e in errors:
            print(f"     - {e}")
        failed += bool(errors)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
