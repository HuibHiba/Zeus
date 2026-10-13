"""Spelsjablonen per fase uit de DFL/Sportec 'integrated dataset' (7 wedstrijden Bundesliga 2022/23, 25 Hz, CC BY 4.0).

Zelfde opzet als skillcorner_templates.py, maar dan uit DFL-tracking + eventdata:
  (a) balbezit per moment uit de EVENTS: team van de laatste ballcontact-gebeurtenis (Play, OtherBallAction,
      BallClaiming, ShotAtGoal) niet langer dan MAX_GAP_S geleden, alleen als de bal in het spel is
      (BallStatus = 1 in de tracking). Ter controle vergeleken met de 'BallPossession'-vlag in de balframes.
  (b) gemiddelde positie per positie en per rol-groep (GK/DEF/MID/WING/ATT) voor het team in en zonder balbezit,
      genormaliseerd: x 0 = eigen doellijn, 1 = tegenstanders doellijn (aanvalsrichting per helft uit de gemiddelde
      x van de keeper), y 0 = links, 1 = rechts (richting wordt aan de linker/rechter posities gecontroleerd).
  (c) lijnhoogte van het verdedigende team: x van de achterste veldspeler, gemiddelde x van de 4 diepste veldspelers
      (achterlijn), gemiddelde x van het blok (10 veldspelers), teamlengte en -breedte (veldspelers, meters).
  (d) hoog/midden/laag blok = terzielen (33/67 percentiel) van de blok-x van de verdedigende ploeg.
Daarna vergelijking met data/skillcorner_templates.json. Draai met `python -I scripts/dfl_templates.py`
(externe data, alleen lezen, streaming). Eenheden: DFL-X/Y in meters met oorsprong in de middenstip.
"""
import bisect
import glob
import json
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from datetime import datetime
from multiprocessing import Pool
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "external" / "dfl"
OUT = ROOT / "data" / "dfl_templates.json"
SC_JSON = ROOT / "data" / "skillcorner_templates.json"
L, W = 105.0, 68.0
STEP = 25            # elk 25e beeld = 1 s bij 25 Hz
MAX_GAP_S = 6.0      # maximale leeftijd van het laatste ballcontact-event
MIN_FIELD = 9        # minimaal aantal geregistreerde veldspelers per team voor teamgrootheden
GROUP = {"TW": "GK", "IVL": "DEF", "IVR": "DEF", "IVZ": "DEF", "LV": "DEF", "RV": "DEF",
         "DML": "MID", "DMR": "MID", "DMZ": "MID", "DLM": "MID", "DRM": "MID", "ZO": "MID",
         "HL": "MID", "HR": "MID", "LM": "MID", "RM": "MID",
         "OLM": "WING", "ORM": "WING", "LA": "WING", "RA": "WING",
         "STZ": "ATT", "STL": "ATT", "STR": "ATT"}
LEFT = {"LV", "IVL", "DML", "DLM", "HL", "OLM", "LM", "LA", "STL"}
SC_GROUP = {"GK": "GK", "CB": "DEF", "LCB": "DEF", "RCB": "DEF", "LB": "DEF", "RB": "DEF", "LWB": "DEF", "RWB": "DEF",
            "DM": "MID", "LDM": "MID", "RDM": "MID", "AM": "MID", "LM": "MID", "RM": "MID",
            "LW": "WING", "RW": "WING", "CF": "ATT", "LF": "ATT", "RF": "ATT"}
POS_MAP = {"TW": "GK", "LV": "LB", "RV": "RB", "IVL": "LCB", "IVR": "RCB", "IVZ": "CB", "DML": "LDM", "DMR": "RDM",
           "DMZ": "DM", "ZO": "AM", "OLM": "LW", "ORM": "RW", "STZ": "CF"}
GROUPS = ["GK", "DEF", "MID", "WING", "ATT"]


def ts_ms(s: str) -> float:
    return datetime.fromisoformat(s).timestamp() * 1000.0


def parse_info(path: str):
    root = ET.parse(path).getroot()
    teams, players = {}, {}
    for t in root.iter("Team"):
        teams[t.get("Role")] = t.get("TeamId")
        for p in t.iter("Player"):
            players[p.get("PersonId")] = [t.get("TeamId"), p.get("PlayingPosition")]
    return teams, players


def parse_events(path: str, players: dict):
    """Ballcontact-events (tijd ms, team) + positie van invallers uit Substitution."""
    ev = []
    for _, el in ET.iterparse(path, events=("end",)):
        if el.tag != "Event":
            continue
        t = ts_ms(el.get("EventTime"))
        for c in el.iter():
            if c.tag in ("Play", "OtherBallAction", "BallClaiming", "ShotAtGoal") and c.get("Team"):
                ev.append((t, c.get("Team")))
            elif c.tag == "Substitution" and c.get("PlayerIn") in players and c.get("PlayingPosition"):
                players[c.get("PlayerIn")][1] = c.get("PlayingPosition")
        el.clear()
    ev.sort()
    return ev


def parse_tracking(path: str, players: dict):
    """Per 1 s (N % 25 == 0): bal en spelers. Geeft (ball{(half,N)->(t,x,y,status,bp)}, pl{pid->{(half,N)->(x,y)}})."""
    ball, pl = {}, defaultdict(dict)
    for _, el in ET.iterparse(path, events=("end",)):
        if el.tag != "FrameSet":
            continue
        team, half, pid = el.get("TeamId"), el.get("GameSection"), el.get("PersonId")
        if team == "referee" or (team != "BALL" and pid not in players):
            el.clear()
            continue
        frames = el.findall("Frame")
        if frames:
            k = (-int(frames[0].get("N"))) % STEP
            for f in frames[k::STEP]:
                try:
                    x, y = float(f.get("X")), float(f.get("Y"))
                except (TypeError, ValueError):
                    continue
                if not (np.isfinite(x) and np.isfinite(y)) or abs(x) > 60 or abs(y) > 40:
                    continue
                n = int(f.get("N"))
                if team == "BALL":
                    ball[(half, n)] = (ts_ms(f.get("T")), x, y, f.get("BallStatus"), f.get("BallPossession"))
                else:
                    pl[pid][(half, n)] = (x, y)
        el.clear()
    return ball, pl


def process_match(args):
    mid, info_f, ev_f, pos_f = args
    teams, players = parse_info(info_f)
    ev = parse_events(ev_f, players)
    ev_t = [e[0] for e in ev]
    ball, pl = parse_tracking(pos_f, players)
    # aanvalsrichting per team en helft: keeper staat bij eigen doel
    gk = defaultdict(list)
    for pid, d in pl.items():
        team, pos = players[pid]
        if pos == "TW":
            for (half, n), (x, y) in d.items():
                gk[(team, half)].append(x)
    direction = {}
    for (team, half), xs in gk.items():
        direction[(team, half)] = 1 if np.mean(xs) < 0 else -1
    # bezit en snapshots
    by_t = defaultdict(list)  # (half,n) -> [(pid, team, pos, x, y)]
    for pid, d in pl.items():
        team, pos = players[pid]
        if pos is None or pos not in GROUP:
            continue
        for key, (x, y) in d.items():
            by_t[key].append((team, pos, x, y))
    agree = defaultdict(int)
    snaps = []
    for key, (t, bx, by, status, bp) in sorted(ball.items()):
        if status != "1":
            continue
        i = bisect.bisect_right(ev_t, t) - 1
        if i < 0 or (t - ev_t[i]) / 1000.0 > MAX_GAP_S:
            continue
        poss = ev[i][1]
        if bp in ("1", "2"):
            agree[(poss == teams.get("home"), bp)] += 1
        rows = by_t.get(key, [])
        half = key[0]
        if not rows or (poss, half) not in direction:
            continue
        pdir = direction[(poss, half)]
        bxn = 0.5 + pdir * bx / L
        recs = {}
        for team, pos, x, y in rows:
            d = direction.get((team, half))
            if d is None:
                continue
            recs.setdefault(team, []).append((pos, 0.5 + d * x / L, 0.5 - d * y / W))  # y: 0 = links (te controleren)
        for team, r in recs.items():
            snaps.append({"mid": mid, "in": team == poss, "ball_x": bxn if team == poss else 1 - bxn, "players": r})
    return mid, snaps, dict(agree), {f"{k[0]}|{k[1]}": v for k, v in direction.items()}, teams


def field_stats(players):
    f = [(p, x, y) for p, x, y in players if p != "TW"]
    if len(f) < MIN_FIELD:
        return None
    xs = np.array([a[1] for a in f])
    ys = np.array([a[2] for a in f])
    srt = np.sort(xs)
    return {"achterste": float(srt[0]), "achterlijn": float(srt[:4].mean()), "blok": float(xs.mean()),
            "voorste": float(srt[-1]), "lengte_m": float((xs.max() - xs.min()) * L), "breedte_m": float((ys.max() - ys.min()) * W)}


def dist(v):
    v = np.asarray(v, dtype=float)
    return {"mean": round(float(v.mean()), 3), "p10": round(float(np.percentile(v, 10)), 3),
            "p50": round(float(np.percentile(v, 50)), 3), "p90": round(float(np.percentile(v, 90)), 3), "n": int(v.size)} if v.size else {"n": 0}


def aggregate(snaps, flip):
    """snaps -> {positie: [x,y]}, {groep: [x,y]} plus teamafmetingen."""
    pos_acc, grp_acc, dims = defaultdict(list), defaultdict(list), defaultdict(list)
    for s in snaps:
        pl = [(p, x, 1 - y if flip else y) for p, x, y in s["players"]]
        for p, x, y in pl:
            pos_acc[p].append((x, y))
            grp_acc[GROUP[p]].append((x, y))
        st = field_stats(pl)
        if st:
            for k, v in st.items():
                dims[k].append(v)
    pos = {p: {"x": round(float(np.mean([a[0] for a in v])), 3), "y": round(float(np.mean([a[1] for a in v])), 3), "n": len(v)}
           for p, v in sorted(pos_acc.items()) if len(v) >= 200}
    grp = {g: {"x": round(float(np.mean([a[0] for a in v])), 3), "y": round(float(np.mean([a[1] for a in v])), 3), "n": len(v)}
           for g, v in grp_acc.items() if len(v) >= 200}
    return pos, grp, {k: dist(v) for k, v in dims.items()}, len(snaps)


def sc_reference(sc):
    """A-League: n-gewogen gemiddelde per rol-groep en per gemapte positie, per fase."""
    out = {}
    for ph, roles in sc["posities"].items():
        g = defaultdict(lambda: [0.0, 0.0, 0])
        for role, d in roles.items():
            grp = SC_GROUP.get(role)
            if grp:
                a = g[grp]
                a[0] += d["x"] * d["n"]
                a[1] += d["y"] * d["n"]
                a[2] += d["n"]
        field = [(d["x"], d["n"]) for r, d in roles.items() if r != "GK" and r in SC_GROUP]
        out[ph] = {"groepen": {k: {"x": round(v[0] / v[2], 3), "y": round(v[1] / v[2], 3)} for k, v in g.items()},
                   "blok_x": round(sum(x * n for x, n in field) / sum(n for _, n in field), 3) if field else None,
                   "posities": {r: d for r, d in roles.items()}}
    return out


def main() -> None:
    jobs = []
    for pf in sorted(glob.glob(str(SRC / "DFL_04_03_positions_raw_observed_*.xml"))):
        tag = Path(pf).stem.split("observed_")[1]
        ef, mf = SRC / f"DFL_03_02_events_raw_{tag}.xml", SRC / f"DFL_02_01_matchinformation_{tag}.xml"
        if ef.exists() and mf.exists():
            jobs.append((tag.rsplit("_", 1)[-1], str(mf), str(ef), pf))
    with Pool(min(4, len(jobs))) as pool:
        res = pool.map(process_match, jobs)
    snaps, bez, dirs = [], {}, {}
    for mid, s, agree, direction, teams in res:
        snaps += s
        tot = sum(agree.values())
        match = sum(v for (is_home, bp), v in agree.items() if (bp == "1") == is_home)
        anti = tot - match
        bez[mid] = {"snapshots_team": len(s), "bezit_flag_overeenkomst": round(max(match, anti) / tot, 3) if tot else None}
        dirs[mid] = direction
        print(mid, bez[mid], flush=True)

    # y-richting controleren aan linker/rechter posities (in-bezit en uit-bezit samen)
    ly = [y for s in snaps for p, x, y in s["players"] if p in LEFT]
    flip = bool(np.mean(ly) > 0.5)
    # blokhoogte: terzielen van de blok-x van de verdedigende ploeg
    uit = [s for s in snaps if not s["in"]]
    bx = []
    for s in uit:
        st = field_stats(s["players"])
        s["_blok"] = st["blok"] if st else None
        if st:
            bx.append(st["blok"])
    t1, t2 = (float(np.percentile(bx, q)) for q in (100 / 3, 200 / 3))
    for s in uit:
        b = s["_blok"]
        s["_cls"] = None if b is None else ("laag" if b < t1 else "midden" if b < t2 else "hoog")
    inn = [s for s in snaps if s["in"]]
    sets = {"in:alles": inn,
            "in:opbouw": [s for s in inn if s["ball_x"] < 1 / 3],
            "in:creatie": [s for s in inn if 1 / 3 <= s["ball_x"] < 2 / 3],
            "in:afwerking": [s for s in inn if s["ball_x"] >= 2 / 3],
            "uit:alles": uit}
    for c in ("hoog", "midden", "laag"):
        sets[f"uit:{c}_blok"] = [s for s in uit if s["_cls"] == c]
    result = {"bron": "DFL/Sportec integrated dataset, Bundesliga 2022/23, 25 Hz, CC BY 4.0",
              "wedstrijden": len(res),
              "methode": {"bezit": f"team van laatste ballcontact-event (Play/OtherBallAction/BallClaiming/ShotAtGoal), max {MAX_GAP_S} s oud, alleen BallStatus=1; 1 Hz",
                          "richting": "aanvalsrichting per team/helft uit gemiddelde x van keeper (TW)",
                          "y_gespiegeld": flip, "y_gemiddelde_linkerposities_na_controle": round(1 - np.mean(ly) if flip else float(np.mean(ly)), 3),
                          "rolgroepen": GROUP, "opmerking_opbouw_creatie_afwerking": "proxy: derde van het veld waarin de bal ligt (niet de SkillCorner-fase)",
                          "veldspeler_min": MIN_FIELD, "achterlijn": "gemiddelde x van de 4 diepste veldspelers"},
              "blok_drempels_blok_x": {"laag<": round(t1, 3), "hoog>=": round(t2, 3)},
              "bezit_validatie": bez, "posities": {}, "groepen": {}, "afmetingen": {}, "snapshots": {}}
    for k, ss in sets.items():
        pos, grp, dims, n = aggregate(ss, flip)
        result["posities"][k], result["groepen"][k], result["afmetingen"][k], result["snapshots"][k] = pos, grp, dims, n

    # vergelijking met A-League
    sc = sc_reference(json.loads(SC_JSON.read_text(encoding="utf-8")))
    pairs = [("in:opbouw", "in:build_up"), ("in:creatie", "in:create"), ("in:afwerking", "in:finish"),
             ("uit:hoog_blok", "uit:high_block"), ("uit:midden_blok", "uit:medium_block"), ("uit:laag_blok", "uit:low_block")]
    cmp = {}
    for dk, sk in pairs:
        if sk not in sc:
            continue
        row = {"groepen_dx": {}, "groepen_dy": {}, "posities_dx": {}, "sc_blok_x": sc[sk]["blok_x"]}
        for g, d in result["groepen"][dk].items():
            r = sc[sk]["groepen"].get(g)
            if r:
                row["groepen_dx"][g] = round(d["x"] - r["x"], 3)
                row["groepen_dy"][g] = round(d["y"] - r["y"], 3)
        for p, d in result["posities"][dk].items():
            r = sc[sk]["posities"].get(POS_MAP.get(p, ""))
            if r:
                row["posities_dx"][p] = round(d["x"] - r["x"], 3)
        dfl_blok = result["afmetingen"][dk].get("blok", {}).get("mean")
        row["dfl_blok_x"] = dfl_blok
        cmp[f"{dk} vs {sk}"] = row
    result["vergelijking_dfl_min_aleague"] = cmp
    OUT.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")

    # compacte tabel
    print(f"\ny gespiegeld: {flip}; blokdrempels blok-x: laag < {t1:.3f} <= midden < {t2:.3f} <= hoog")
    print(f"{'fase':<16}{'n':>6}" + "".join(f"{g:>13}" for g in GROUPS) + f"{'achtersteX':>11}{'achterlijn':>11}{'blokX':>7}{'lengte':>7}{'breedte':>8}")
    for k in sets:
        gr, dm = result["groepen"][k], result["afmetingen"][k]
        cells = "".join(f"{gr[g]['x']:>6.2f}/{gr[g]['y']:.2f}" if g in gr else f"{'-':>13}" for g in GROUPS)
        g = lambda key: dm.get(key, {}).get("mean", float("nan"))
        print(f"{k:<16}{result['snapshots'][k]:>6}{cells}{g('achterste'):>11.3f}{g('achterlijn'):>11.3f}{g('blok'):>7.3f}{g('lengte_m'):>7.1f}{g('breedte_m'):>8.1f}")
    print("\nVerschil DFL - A-League (x, groepen):")
    for name, row in cmp.items():
        print(name, "| dx:", row["groepen_dx"], "| blok DFL/SC:", row["dfl_blok_x"], row["sc_blok_x"])
    print("klaar ->", OUT)


if __name__ == "__main__":
    sys.exit(main())
