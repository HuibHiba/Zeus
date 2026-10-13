"""Controleert dat paden die in een build kunnen komen geen echte namen of interne licentiestatus bevatten.

Gecontroleerde paden: tactics/, godot/, schema/, sim/, competitions/, LICENSES/ (scripts/ en docs/ zijn intern en vallen erbuiten).
Faalt (exit 1) als een naam uit local/namen.json voorkomt, of een JSON-bestand een veld "licentie_status" met "intern" heeft.
Ontbreekt local/namen.json, dan geldt alleen de ingebouwde lijst (de bekende namen uit de oude tactiek).
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PADEN = ["tactics", "godot", "schema", "sim", "competitions", "LICENSES"]
TEKST = {".json", ".cs", ".gd", ".md", ".py", ".tscn", ".tres", ".cfg", ".txt", ".csproj", ".godot", ".csv", ".sln"}
SKIP_DIRS = {"bin", "obj", ".godot", ".git", "__pycache__"}
INGEBOUWD = ["Feyenoord", "Como", "AZ", "Hadj Moussa", "Valente", "Ferri", "Zechiël", "Vanhoutte", "Targhalline",
             "Mármol", "St. Juste", "Javi López", "Read", "Ernst"]
# korte of gewone woorden alleen als hoofdletter-gevoelig los woord, de rest hoofdletterongevoelig
HOOFDLETTERGEVOELIG = {"AZ", "Como", "Read", "Ernst", "Ferri", "Valente"}


def namen() -> tuple[list[str], str]:
    pad = ROOT / "local" / "namen.json"
    if not pad.exists():
        return INGEBOUWD, "ingebouwde lijst (local/namen.json ontbreekt)"
    d = json.loads(pad.read_text(encoding="utf-8"))
    lijst = list(d.get("speler_per_positie", {}).values()) + d.get("clubs_en_tegenstanders", []) + d.get("overige_termen", [])
    return sorted(set(lijst) | set(INGEBOUWD)), "local/namen.json + ingebouwde lijst"


def patroon(naam: str) -> re.Pattern:
    vlag = 0 if naam in HOOFDLETTERGEVOELIG else re.IGNORECASE
    return re.compile(r"(?<!\w)" + re.escape(naam) + r"(?!\w)", vlag)


def licentie_intern(obj) -> bool:
    if isinstance(obj, dict):
        v = obj.get("licentie_status")
        if isinstance(v, str) and "intern" in v.lower():
            return True
        return any(licentie_intern(x) for x in obj.values())
    if isinstance(obj, list):
        return any(licentie_intern(x) for x in obj)
    return False


def main() -> int:
    lijst, bron = namen()
    pats = [(n, patroon(n)) for n in lijst]
    fouten, gelezen = [], 0
    for map_ in PADEN:
        for f in sorted((ROOT / map_).rglob("*")):
            if not f.is_file() or SKIP_DIRS & set(f.relative_to(ROOT).parts) or f.suffix.lower() not in TEKST:
                continue
            try:
                tekst = f.read_text(encoding="utf-8")
            except UnicodeDecodeError:
                continue
            gelezen += 1
            rel = f.relative_to(ROOT).as_posix()
            for n, p in pats:
                if p.search(tekst):
                    fouten.append(f"{rel}: bevat naam '{n}'")
            if f.suffix.lower() == ".json":
                try:
                    if licentie_intern(json.loads(tekst)):
                        fouten.append(f"{rel}: licentie_status 'intern'")
                except json.JSONDecodeError:
                    pass
    print(f"{'OK  ' if not fouten else 'FOUT'} release-inhoud: {gelezen} bestanden, {len(pats)} namen ({bron})")
    for e in fouten:
        print(f"     - {e}")
    return 1 if fouten else 0


if __name__ == "__main__":
    sys.exit(main())
