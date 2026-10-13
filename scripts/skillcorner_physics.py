"""Natuurkundewaarden uit SkillCorner open data (A-League 2024/25, broadcast-tracking, 10 fps, MIT).

Leest data/external/skillcorner/data/matches/* (alleen lezen). Geeft:
- loopsnelheden en versnellingen van spelers (alleen echt gedetecteerde, niet geextrapoleerde frames)
- balsnelheid (korte vensters) en gemiddelde passsnelheid uit eventdata
- passafstanden, spelfases, hoogte van de verdedigingslinie en pressingketens
Draai met `python -I scripts/skillcorner_physics.py` (de data is extern en wordt als onbetrouwbaar behandeld).
Bron: SkillCorner open data, MIT; bronvermelding gewenst.
"""
import json
import sys
from collections import defaultdict
from pathlib import Path

import numpy as np
import pandas as pd

ROOT = Path(__file__).resolve().parent.parent
MATCHES = ROOT / "data" / "external" / "skillcorner" / "data" / "matches"
OUT = ROOT / "data" / "skillcorner_summary.json"
FPS = 10.0
MAX_SPEED = 12.0  # m/s; sneller is een meetfout
MAX_ACC = 10.0    # m/s^2


def load_tracking(path: Path):
    ball, players = [], defaultdict(list)
    with open(path, encoding="utf-8") as f:
        for line in f:
            d = json.loads(line)
            if d["period"] is None:
                continue
            fr = d["frame"]
            b = d["ball_data"]
            ball.append((fr, b["x"], b["y"], b["z"], bool(b["is_detected"])))
            for p in d["player_data"]:
                players[p["player_id"]].append((fr, p["x"], p["y"], bool(p["is_detected"])))
    return ball, players


def player_kinematics(rows):
    """Snelheid (venster +-0,2 s) en versnelling (snelheidsverschil over 0,4 s), alleen op gedetecteerde frames."""
    a = np.array(rows, dtype=float)
    a = a[np.argsort(a[:, 0])]
    fr, x, y, det = a[:, 0], a[:, 1], a[:, 2], a[:, 3] > 0
    n = len(fr)
    speed = np.full(n, np.nan)
    acc = np.full(n, np.nan)
    if n < 10:
        return speed, acc
    k = np.arange(2, n - 2)
    ok = fr[k + 2] - fr[k - 2] == 4
    for off in (-2, -1, 0, 1, 2):
        ok &= det[k + off]
    sp = np.hypot(x[k + 2] - x[k - 2], y[k + 2] - y[k - 2]) / (4 / FPS)
    sp[~ok] = np.nan
    sp[sp > MAX_SPEED] = np.nan
    speed[k] = sp
    k3 = np.arange(4, n - 4)
    ok2 = fr[k3 + 2] - fr[k3 - 2] == 4
    a_val = (speed[k3 + 2] - speed[k3 - 2]) / (4 / FPS)
    a_val[~ok2] = np.nan
    a_val[np.abs(a_val) > MAX_ACC] = np.nan
    acc[k3] = a_val
    return speed, acc


def pct(arr, qs=(50, 90, 99, 99.9)):
    arr = np.asarray(arr, dtype=float)
    arr = arr[~np.isnan(arr)]
    if arr.size == 0:
        return {"n": 0}
    return {f"p{q}": round(float(np.percentile(arr, q)), 2) for q in qs} | {"n": int(arr.size)}


def main() -> None:
    all_speed, all_acc, ball_speed = [], [], []
    top_speed_per_player = []
    pass_speed, pass_dist_all, pass_dist_ok, pass_dur = [], [], [], []
    phase_counts = defaultdict(int)
    line_height, chain_len = [], []
    n_matches = 0

    for mdir in sorted(MATCHES.iterdir()):
        mid = mdir.name
        trk = mdir / f"{mid}_tracking_extrapolated.jsonl"
        evt = mdir / f"{mid}_dynamic_events.csv"
        if not (trk.exists() and evt.exists()):
            continue
        n_matches += 1
        print(f"{mid} ...", flush=True)
        ball, players = load_tracking(trk)
        for pid, rows in players.items():
            sp, ac = player_kinematics(rows)
            valid = sp[~np.isnan(sp)]
            all_speed.append(valid)
            all_acc.append(ac[~np.isnan(ac)])
            if valid.size > 6000:  # minstens 10 minuten gedetecteerd
                top_speed_per_player.append(float(np.percentile(valid, 99.9)))

        b = np.array(ball, dtype=float)
        if len(b) > 4:
            fr = b[:, 0]; bx, by = b[:, 1], b[:, 2]; det = b[:, 4] > 0
            k = np.arange(1, len(b) - 1)
            ok = (fr[k + 1] - fr[k - 1] == 2) & det[k - 1] & det[k] & det[k + 1] & ~np.isnan(bx[k - 1]) & ~np.isnan(bx[k + 1])
            v = np.hypot(bx[k + 1] - bx[k - 1], by[k + 1] - by[k - 1]) / (2 / FPS)
            v = v[ok]
            ball_speed.append(v[v < 45])

        ev = pd.read_csv(evt, low_memory=False)
        pp = ev[ev.event_type == "player_possession"].reset_index(drop=True)
        pass_dist_all += pp["pass_distance"].dropna().tolist()
        pass_dur += pp.loc[pp.end_type == "pass", "duration"].dropna().tolist()
        for i in range(len(pp) - 1):
            r, nx = pp.iloc[i], pp.iloc[i + 1]
            if r.end_type == "pass" and r.pass_outcome == "successful" and pd.notna(r.pass_distance):
                pass_dist_ok.append(float(r.pass_distance))
                if r.team_id == nx.team_id:
                    dt = (nx.frame_start - r.frame_end) / FPS
                    if 0.2 <= dt <= 5 and r.pass_distance >= 5:
                        pass_speed.append(float(r.pass_distance) / dt)
        for ph in ev["team_in_possession_phase_type"].dropna():
            phase_counts[ph] += 1
        line_height += ev["last_defensive_line_height_start"].dropna().tolist()
        chain_len += ev["pressing_chain_length"].dropna().tolist()

    speeds = np.concatenate(all_speed)
    accs = np.concatenate(all_acc)
    bs = np.concatenate(ball_speed)
    total_phase = sum(phase_counts.values())
    summary = {
        "bron": "SkillCorner open data, A-League 2024/25, 10 fps, MIT",
        "wedstrijden": n_matches,
        "speler_snelheid_m_s": pct(speeds),
        "speler_snelheid_aandeel_tijd": {
            ">4 m/s": round(float((speeds > 4).mean()), 4),
            ">5.5 m/s": round(float((speeds > 5.5).mean()), 4),
            ">7 m/s": round(float((speeds > 7).mean()), 4),
        },
        "piek_per_speler_p99.9_m_s": pct(top_speed_per_player, (10, 50, 90)),
        "versnelling_m_s2": {
            "positief": pct(accs[accs > 0], (50, 90, 99)),
            "negatief_absoluut": pct(-accs[accs < 0], (50, 90, 99)),
        },
        "bal_snelheid_0.2s_m_s": pct(bs, (50, 90, 99, 99.9)),
        "pass_snelheid_gemiddeld_m_s": pct(pass_speed, (10, 50, 90)),
        "pass_afstand_alle_m": pct(pass_dist_all, (10, 50, 90)),
        "pass_afstand_geslaagd_m": pct(pass_dist_ok, (10, 50, 90)),
        "pass_duur_s": pct(pass_dur, (10, 50, 90)),
        "spelfases_aandeel": {k: round(v / total_phase, 3) for k, v in sorted(phase_counts.items(), key=lambda kv: -kv[1])},
        "hoogte_laatste_verdedigingslinie_start": pct(line_height, (10, 50, 90)),
        "pressingketen_lengte": pct(chain_len, (50, 90, 99)),
    }
    OUT.write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(summary, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    sys.exit(main())
