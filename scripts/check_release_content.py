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
# gewone Engelse woorden: alleen in tactiek-/selectie-/uitdagingsdata zoeken, niet in code, docs of UI-teksten
GEWOON_WOORD = {"Read"}
SPELDATA = re.compile(r"(^|/)(tactics|squads|challenges)/.*\.json$")


def treft(naam: str, p: re.Pattern, rel: str, tekst: str) -> bool:
    return (naam not in GEWOON_WOORD or bool(SPELDATA.search(rel))) and bool(p.search(tekst))


def namen() -> tuple[list[str], str]:
    pad = ROOT / "local" / "namen.json"
    if not pad.exists():
        return INGEBOUWD, "ingebouwde lijst (local/namen.json ontbreekt)"
    d = json.loads(pad.read_text(encoding="utf-8"))
    lijst = list(d.get("speler_per_positie", {}).values()) + d.get("clubs_en_tegenstanders", []) + d.get("overige_termen", [])
    return sorted(set(lijst) | set(INGEBOUWD)), "local/namen.json + ingebouwde lijst"


def patroon(naam: str) -> re.Pattern:
    if naam in HOOFDLETTERGEVOELIG:
        # niet na '.' of voor '(': code-identifiers als SafeFile.Read( of void Read( zijn geen spelersnaam
        return re.compile(r"(?<![\w.])" + re.escape(naam) + r"(?![\w(])")
    return re.compile(r"(?<!\w)" + re.escape(naam) + r"(?!\w)", re.IGNORECASE)


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
                if treft(n, p, rel, tekst):
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


# --- modus 2: build-map / uitgepakt pakket (python scripts/check_release_content.py --package <map>) ---------
NOTICES = ["GaffersNotebook.exe", "VERSIE.txt", "LEESMIJ.txt", "GODOT_LICENSES.txt", "DOTNET_LICENSE.txt",
           "DOTNET_THIRD_PARTY_NOTICES.txt", "WYSCOuT_NOTICE.txt", "BRONNEN_NOTICE.txt", "FONT_OFL.txt"]
RUNTIME = "data_Zeus.Godot_windows_x86_64"
TOEGESTAAN = [re.compile(p) for p in (
    r"data/(tactics|squads|challenges)/[^/]+\.json", r"data/lang/(en|nl)\.json",
    RUNTIME + r"/[^/]+\.(dll|json|exe)", r"GaffersNotebook\.pck")]
VERBODEN_EXT = {".pdb", ".log", ".tmp", ".sav"}
# Paden en loginnamen; 'lth' alleen als padsegment, als los woord zit het ook in 'health'/'width'.
LEK = re.compile(rb"(?i)c:[\\/](users|dev)\b|groevenbeek|[\\/]lth[\\/]")
EMAIL = re.compile(rb"(?i)[\w.+-]+@[\w-]+\.[\w.-]*[a-z]{2,}")
DERDEN = {"GODOT_LICENSES.txt", "DOTNET_LICENSE.txt", "DOTNET_THIRD_PARTY_NOTICES.txt", "FONT_OFL.txt",
          "GaffersNotebook.exe"}  # derden-teksten/engine bevatten e-mail van auteurs


def lekken(data: bytes, email: bool) -> set[str]:
    hits = set()
    for blob in (data, data.replace(b"\x00", b"")):   # ASCII, en UTF-16LE na wegstrepen van nulbytes
        hits |= {m.group(0).decode("latin-1") for m in LEK.finditer(blob)}
        if email:
            hits |= {m.group(0).decode("latin-1") for m in EMAIL.finditer(blob)}
    return hits


def check_package(root: Path) -> int:
    fouten = []
    bestanden = sorted(f.relative_to(root).as_posix() for f in root.rglob("*") if f.is_file())
    fouten += [f"ontbreekt: {n}" for n in NOTICES if n not in bestanden]
    pats = [(n, patroon(n)) for n in namen()[0]]
    for rel in bestanden:
        if Path(rel).suffix.lower() in VERBODEN_EXT:
            fouten.append(f"{rel}: verboden extensie")
        if rel not in NOTICES and not any(p.fullmatch(rel) for p in TOEGESTAAN):
            fouten.append(f"{rel}: niet op de whitelist")
        eigen = rel == "GaffersNotebook.exe" or re.fullmatch(RUNTIME + r"/Zeus\.[^/]+\.dll", rel)
        if not (rel.endswith((".txt", ".json")) or eigen):
            continue
        data = (root / rel).read_bytes()
        fouten += [f"{rel}: bevat '{h}'" for h in sorted(lekken(data, email=rel not in DERDEN))]
        if rel.startswith("data/") or rel in ("LEESMIJ.txt", "BRONNEN_NOTICE.txt"):
            tekst = data.decode("utf-8", "replace")
            fouten += [f"{rel}: bevat naam '{n}'" for n, p in pats if treft(n, p, rel, tekst)]
            if "â€" in tekst:
                fouten.append(f"{rel}: mojibake (dubbel gecodeerde UTF-8)")
    if "VERSIE.txt" in bestanden:
        versie = (root / "VERSIE.txt").read_text(encoding="utf-8").strip()
        if not re.fullmatch(r"\d+\.\d+\.\d+(-[\w.]+)? \(\S+ \S+\)", versie):
            fouten.append(f"VERSIE.txt: verwacht '<versie> (<hash> <tijd>)', kreeg '{versie}'")
    print(f"{'OK  ' if not fouten else 'FOUT'} pakket {root}: {len(bestanden)} bestanden")
    for e in fouten:
        print(f"     - {e}")
    return 1 if fouten else 0


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "--package":
        sys.exit(check_package(Path(sys.argv[2])))
    sys.exit(main())
