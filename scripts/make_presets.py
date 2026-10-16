"""Genereert extra voorbeeldtactieken in tactics/ uit gemeten lijnhoogtes per rolgroep (zie vault: Tactiekdatamodel v0).

Gebruik (vanuit C:\\dev\\zeus):
  .\\.venv\\Scripts\\python.exe scripts\\make_presets.py
  .\\.venv\\Scripts\\python.exe scripts\\validate_tactics.py

De x-waarden komen uit de SkillCorner/DFL-sjablonen (halfprofniveau, indicatief); de instructies en teaminstellingen
zijn ontwerpkeuzes per stijl, geen gemeten waarden.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TACTICS = ROOT / "tactics"

# x per rolgroep (0 = eigen doellijn, 1 = tegenstanders doellijn) per fase/blok.
GROUPS = ["GK", "CB", "FB", "DM", "CM", "AM", "W", "ST"]
LINES = {
    "laag":      [0.03, 0.15, 0.17, 0.22, 0.25, 0.27, 0.26, 0.32],
    "midden":    [0.08, 0.34, 0.38, 0.46, 0.50, 0.53, 0.51, 0.56],
    "hoog":      [0.15, 0.55, 0.64, 0.62, 0.67, 0.72, 0.71, 0.77],
    "opbouw":    [0.08, 0.18, 0.27, 0.28, 0.31, 0.34, 0.35, 0.39],
    "aanval":    [0.12, 0.61, 0.72, 0.67, 0.73, 0.78, 0.79, 0.82],
}
# (id, rol, groep, y) ; y 0 = links, 1 = rechts in aanvalsrichting
F433 = [("GK", "keeper", "GK", .5), ("RB", "back", "FB", .85), ("RC", "centrale_verdediger", "CB", .62), ("LC", "centrale_verdediger", "CB", .38),
        ("LB", "back", "FB", .15), ("RCM", "controlerende_middenvelder", "DM", .65), ("CM", "box_to_box", "CM", .5),
        ("LCM", "controlerende_middenvelder", "DM", .35), ("RW", "buitenspeler", "W", .88), ("ST", "spits", "ST", .5), ("LW", "buitenspeler", "W", .12)]
F4231 = [("GK", "keeper", "GK", .5), ("RB", "back", "FB", .85), ("RC", "centrale_verdediger", "CB", .62), ("LC", "centrale_verdediger", "CB", .38),
         ("LB", "back", "FB", .15), ("RDM", "controlerende_middenvelder", "DM", .6), ("LDM", "controlerende_middenvelder", "DM", .4),
         ("AM", "aanvallende_middenvelder", "AM", .5), ("RW", "buitenspeler", "W", .88), ("ST", "spits", "ST", .5), ("LW", "buitenspeler", "W", .12)]
F532 = [("GK", "keeper", "GK", .5), ("RWB", "back", "FB", .93), ("RC", "centrale_verdediger", "CB", .72), ("CC", "centrale_verdediger", "CB", .5),
        ("LC", "centrale_verdediger", "CB", .28), ("LWB", "back", "FB", .07), ("RCM", "box_to_box", "CM", .66), ("LCM", "box_to_box", "CM", .34),
        ("CMD", "controlerende_middenvelder", "DM", .5), ("RS", "spits", "ST", .6), ("LS", "targetman", "ST", .4)]


def spelers_for(formation, line, instr=None, follow=None):
    out = []
    for pid, _rol, grp, y in formation:
        entry = {"id": pid, "pos": [round(LINES[line][GROUPS.index(grp)], 3), y]}
        if follow and grp in follow:  # a group without an entry (the keeper in opbouw/aanval) gets no volg_bal
            entry["volg_bal"] = list(follow[grp])
        if instr and instr.get(pid):
            entry["instructies"] = list(instr[pid])
        out.append(entry)
    return out


def build(naam, opmerking, team, formation, block, instr, blocks=None, wissels=None, triggers=None, follow_spel=None):
    """follow_spel: volg_bal [lengte, breedte] van alle veldspelers in opbouw en aanval (AI-fix ronde 2, docs/ai-diagnose.md):
    zonder volgen staan ze in die fases op hun formatieplek stil. De keeper volgt niet."""
    follow = {g: [0.3, 0.4] for g in GROUPS}
    follow_play = {g: follow_spel for g in GROUPS if g != "GK"} if follow_spel else None
    fases = {
        "verdedigen": {"blokhoogte": block, "spelers": spelers_for(formation, block, instr.get("verdedigen"), follow)},
        "opbouw": {"spelers": spelers_for(formation, "opbouw", instr.get("opbouw"), follow_play)},
        "aanval": {"spelers": spelers_for(formation, "aanval", instr.get("aanval"), follow_play)},
        "balverlies": {"gelijk_aan": "verdedigen"},
        "balwinst": {"gelijk_aan": "opbouw"},
        "stilstaand": {"gelijk_aan": "verdedigen"},
    }
    data = {"schemaVersie": "0.1", "naam": naam, "opmerking": opmerking, "team": team,
            "spelers": [{"id": p, "rol": r} for p, r, _g, _y in formation], "fases": fases}
    if blocks:
        data["verdedigen_varianten"] = {b: {"blokhoogte": b, "spelers": spelers_for(formation, b, instr.get("verdedigen"), follow)} for b in blocks}
    if wissels:
        data["blokwissels"] = wissels
    if triggers:
        data["triggers"] = triggers
    return data


PRESETS = {
    "hoge_druk_433": build(
        "Hoge druk 4-3-3 (voorbeeld)",
        "Hoog blok met aanvallende backs; vooral druk na balverlies. Na minuut 70 met voorsprong terug naar een middenblok.",
        {"tempo": 0.85, "pressing_intensiteit": 0.95, "pressing_vorm": "zone", "lijnhoogte": 0.85, "compactheid": 0.7},
        F433, "hoog",
        {"verdedigen": {"RW": ["start_pressing"], "LW": ["start_pressing"], "ST": ["start_pressing"], "RB": ["kom_op"], "LB": ["kom_op"]},
         "aanval": {"RB": ["kom_op"], "LB": ["kom_op"], "RW": ["sluit_naar_binnen"], "LW": ["sluit_naar_binnen"], "ST": ["loop_diep"]}},
        blocks=["hoog", "midden"],
        wissels=[{"naam": "Voorsprong beheren", "wanneer": {"stand": "voor", "vanaf_minuut": 70}, "naar": "midden"}],
        triggers=[{"naam": "Druk bij terugspelen op de keeper", "wanneer": {"fase": "verdedigen", "voorwaarde": "bal_naar_keeper"},
                   "dan": [{"speler": "ST", "instructie": "start_pressing"}]}],
        follow_spel=[0.15, 0.25]),
    "positiespel_4231": build(
        "Positiespel 4-2-3-1 (voorbeeld)",
        "Veel balbezit: backs breed, 6'en zakken in de opbouw, buitenspelers snijden naar binnen; middenblok zonder bal.",
        {"tempo": 0.45, "pressing_intensiteit": 0.5, "pressing_vorm": "gemengd", "lijnhoogte": 0.6, "compactheid": 0.8},
        F4231, "midden",
        {"opbouw": {"RDM": ["laat_zakken"], "RB": ["blijf_breed"], "LB": ["blijf_breed"], "AM": ["vrije_man_zoeken"]},
         "aanval": {"RW": ["sluit_naar_binnen"], "LW": ["sluit_naar_binnen"], "AM": ["vrije_man_zoeken"], "ST": ["loop_zestien_in"]},
         "verdedigen": {"LDM": ["blijf_achter"]}},
        follow_spel=[0.25, 0.35]),
    "counter_532": build(
        "Counter 5-3-2 (voorbeeld)",
        "Laag blok met vijf achter; balwinst direct diep naar twee spitsen, wingbacks komen laat mee.",
        {"tempo": 0.8, "pressing_intensiteit": 0.2, "pressing_vorm": "zone", "lijnhoogte": 0.2, "compactheid": 0.85},
        F532, "laag",
        {"verdedigen": {"CC": ["dek_ruimte_achter_lijn"], "CMD": ["blijf_achter"]},
         "opbouw": {"RS": ["loop_diep"], "LS": ["vrije_man_zoeken"]},
         "aanval": {"RS": ["loop_diep"], "LS": ["loop_zestien_in"], "RWB": ["kom_op"], "LWB": ["kom_op"]}},
        triggers=[{"naam": "Druk als de tegenstander slecht aanneemt", "wanneer": {"fase": "verdedigen", "voorwaarde": "slechte_aanname"},
                   "dan": [{"speler": "RS", "instructie": "start_pressing"}]}],
        follow_spel=[0.15, 0.25]),
}

if __name__ == "__main__":
    for name, data in PRESETS.items():
        path = TACTICS / f"{name}.json"
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("geschreven:", path.relative_to(ROOT))
