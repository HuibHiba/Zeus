"""Valideert alle tactiekbestanden in tactics/ tegen schema/tactic.schema.json.

Naast het schema: elk spelers-id in een fase komt overeen met `spelers`, en
triggers verwijzen alleen naar bestaande ids.
"""
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent.parent


def schema_errors(error) -> list[str]:
    # oneOf bij een volledige fase verbergt anders de concrete padfout.
    def leaves(e):
        return [leaf for child in e.context for leaf in leaves(child)] if e.context else [e]
    path_errors = [e for e in leaves(error) if any(str(k).startswith("pad") for k in e.absolute_path)]
    messages = []
    for e in path_errors or [error]:
        location = '/'.join(map(str, e.absolute_path))
        field = next((str(k) for k in e.absolute_path if str(k).startswith("pad")), "")
        if field == "pad" and e.validator in ("minItems", "maxItems") and e.validator_value in (1, 8):
            message = "pad moet 1 tot en met 8 punten bevatten"
        elif field == "pad" and e.validator in ("minimum", "maximum"):
            message = "padpunt buiten het veld: coordinaten moeten binnen 0..1 liggen"
        elif field == "pad_snelheid" and e.validator == "enum":
            message = "onbekende padsnelheid: kies wandelen, joggen of sprint"
        elif field == "pad_wacht" and e.validator in ("minimum", "maximum"):
            message = "pad_wacht moet binnen 0..5 seconden liggen"
        else:
            message = e.message
        messages.append(f"{location}: {message}")
    return messages


def reject_nonfinite(value: str):
    raise ValueError(f"niet-eindig getal '{value}': padpunten en andere getallen moeten eindig zijn")


def check(path: Path, schema: dict) -> list[str]:
    data = json.loads(path.read_text(encoding="utf-8"), parse_constant=reject_nonfinite)
    errors = [message for e in Draft202012Validator(schema).iter_errors(data) for message in schema_errors(e)]
    if errors:
        return errors
    ids = {s["id"] for s in data["spelers"]}
    if len(ids) != 11:
        errors.append("spelers: ids niet uniek")
    for naam, fase in data["fases"].items():
        if "gelijk_aan" in fase:
            doel = fase["gelijk_aan"]
            if doel not in data["fases"]:
                errors.append(f"fases/{naam}: gelijk_aan verwijst naar onbekende fase '{doel}' (bestaande fases: {', '.join(sorted(data['fases']))})")
            elif doel == naam or "gelijk_aan" in data["fases"][doel]:
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
    paths = sorted((ROOT / "tactics").glob("*.json"))
    if not paths:
        print(f"FOUT: geen tactiekbestanden gevonden in {ROOT / 'tactics'} (map ontbreekt of is leeg)")
        return 1
    for path in paths:
        try:
            errors = check(path, schema)
        except (OSError, ValueError, KeyError, TypeError) as exc:  # kapot JSON of onverwachte structuur: melden, niet crashen
            errors = [f"kon niet worden gecontroleerd: {type(exc).__name__}: {exc}"]
        print(f"{'OK  ' if not errors else 'FOUT'} {path.name}")
        for e in errors:
            print(f"     - {e}")
        failed += bool(errors)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
