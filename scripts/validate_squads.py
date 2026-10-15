"""Valideer voorlopige selecties; schema plus unieke ids, GK en optionele tactiekslots."""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def pairs(items):
    result = {}
    for key, value in items:
        if key in result:
            raise ValueError(f"dubbel veld: {key}")
        result[key] = value
    return result

def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"), object_pairs_hook=pairs)

def validate(path, tactic=None):
    import jsonschema
    squad = read(path)
    schema = read(ROOT / "schema/speler.schema.json")
    errors = sorted(jsonschema.Draft202012Validator(schema).iter_errors(squad), key=lambda e: str(e.path))
    if errors:
        raise ValueError("; ".join(f"$.{'.'.join(map(str, e.path))}: {e.message}" for e in errors))
    players = squad["starters"] + squad["bench"]
    if len({p["id"] for p in players}) != 18:
        raise ValueError("$.players.id: dubbel speler-id")
    if sum(p["id"] == "GK" and p["role"] == "keeper" for p in squad["starters"]) != 1:
        raise ValueError("$.starters: GK met rol keeper ontbreekt")
    if tactic and {p["id"] for p in squad["starters"]} != {p["id"] for p in read(tactic)["spelers"]}:
        raise ValueError("$.starters.id: basis-id's passen niet op tactiekslots")
    return squad

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="*", type=Path)
    parser.add_argument("--tactic", type=Path)
    args = parser.parse_args()
    failed = False
    for path in args.paths or sorted((ROOT / "squads").glob("*.json")):
        try:
            squad = validate(path, args.tactic)
            print(f"OK {path.name}: {squad['name']} (11 + 7, voorlopig)")
        except (ValueError, OSError, ImportError) as error:
            print(f"FOUT {path}: {error}")
            failed = True
    return int(failed)

if __name__ == "__main__":
    raise SystemExit(main())
