"""Valideert competitions/profielen.json tegen competitions/competition.schema.json en controleert consistentie.

Extra controles: thuiswinst + gelijkspel + uitwinst = 1 (+-0,02), thuis- + uitdoelpunten = doelpunten per wedstrijd (+-0,05),
p10 <= waarde <= p90 waar die gegeven zijn; geen FotMob- of StatsBomb-bron in het profiel
(die waarden staan lokaal in data/derived/profielen_testdata.json, niet in git).
"""
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent.parent
VERBODEN_BRONNEN = ("fotmob", "statsbomb")


def waarden(obj, pad=""):
    """Alle waarde-objecten (met 'waarde') in een profiel, met hun pad."""
    if isinstance(obj, dict):
        if "waarde" in obj and "bron" in obj:
            yield pad, obj
        else:
            for k, v in obj.items():
                yield from waarden(v, f"{pad}/{k}")


def check(profielen: dict, schema: dict) -> list[str]:
    errors = [f"{'/'.join(map(str, e.absolute_path))}: {e.message}" for e in Draft202012Validator(schema).iter_errors(profielen)]
    if errors:
        return errors
    for c in profielen["competities"]:
        u = c["uitslagen"]
        som = u["thuiswinst"]["waarde"] + u["gelijkspel"]["waarde"] + u["uitwinst"]["waarde"]
        if abs(som - 1) > 0.02:
            errors.append(f"{c['id']}: thuiswinst+gelijk+uitwinst = {som:.3f}, hoort 1 te zijn")
        dp = u["thuisdoelpunten"]["waarde"] + u["uitdoelpunten"]["waarde"]
        if abs(dp - u["doelpunten_per_wedstrijd"]["waarde"]) > 0.05:
            errors.append(f"{c['id']}: thuis+uit doelpunten {dp:.2f} wijkt af van {u['doelpunten_per_wedstrijd']['waarde']}")
        for pad, w in waarden(c):
            lo, hi = w.get("p10"), w.get("p90")
            if lo is not None and hi is not None and not (lo <= w["waarde"] <= hi):
                errors.append(f"{c['id']}{pad}: waarde {w['waarde']} ligt buiten p10-p90 ({lo}-{hi})")
            if any(naam in w["bron"].lower() for naam in VERBODEN_BRONNEN):
                errors.append(f"{c['id']}{pad}: bron '{w['bron']}' hoort niet in het profiel (lokale testdata)")
    ids = [c["id"] for c in profielen["competities"]]
    if len(ids) != len(set(ids)):
        errors.append("competitie-id's zijn niet uniek")
    return errors


def main() -> int:
    schema = json.loads((ROOT / "competitions" / "competition.schema.json").read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)
    profielen = json.loads((ROOT / "competitions" / "profielen.json").read_text(encoding="utf-8"))
    errors = check(profielen, schema)
    print(f"{'OK  ' if not errors else 'FOUT'} profielen.json ({len(profielen['competities'])} competities)")
    for e in errors:
        print(f"     - {e}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
