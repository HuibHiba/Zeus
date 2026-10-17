"""Shared positional-play metrics for the AI-gap analysis (real tracking vs sim), one definition for both sources.

Input per frame and team: positions already rotated so that THIS team attacks towards +x (metres, centre origin).
`team_frame` returns team-shape metrics; `carrier_frame` the metrics around the ball carrier (in possession only).
"""
import numpy as np

CARRIER_RADIUS = 2.5   # carrier = possession-team player nearest the ball, at most this far away (m)
LANE_CLEAR = 2.0       # pass lane is blocked when an opponent is within this distance of the segment (m)
STILL = 0.5            # m/s: below this an outfield player counts as standing still
MIN_OUTFIELD = 9       # fewer tracked outfield players: frame skipped (red cards, tracking gaps)


def hull_area(pts: np.ndarray) -> float:
    """Convex hull area (monotone chain + shoelace)."""
    p = sorted(map(tuple, pts))
    if len(p) < 3:
        return 0.0

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    lower, upper = [], []
    for q in p:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], q) <= 0:
            lower.pop()
        lower.append(q)
    for q in reversed(p):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], q) <= 0:
            upper.pop()
        upper.append(q)
    h = np.array(lower[:-1] + upper[:-1])
    x, y = h[:, 0], h[:, 1]
    return float(abs(np.dot(x, np.roll(y, -1)) - np.dot(y, np.roll(x, -1))) / 2)


def team_frame(out_xy: np.ndarray, out_speed: np.ndarray, ball_x: float):
    """out_xy: (n,2) outfield players of one team (attacking +x); ball_x in the same orientation."""
    if len(out_xy) < MIN_OUTFIELD:
        return None
    xs = np.sort(out_xy[:, 0])
    back, front = xs[:4].mean(), xs[-2:].mean()
    mid = xs[4:-2].mean()
    return {
        "lengte_m": float(xs[-1] - xs[0]),
        "breedte_m": float(out_xy[:, 1].max() - out_xy[:, 1].min()),
        "linie_verd_mid_m": float(mid - back),
        "linie_mid_aanval_m": float(front - mid),
        "achterlijn_tov_bal_m": float(back - ball_x),   # negative = back line behind the ball
        "hull_m2": hull_area(out_xy),
        "snelheid_m_s": float(np.median(out_speed)),
        "stil_aandeel": float((out_speed < STILL).mean()),
        "sprint_aandeel": float((out_speed > 5.5).mean()),
    }


def carrier_frame(mates_xy: np.ndarray, opp_xy: np.ndarray, ball_xy: np.ndarray):
    """mates_xy: all players of the possession team incl. keeper; opp_xy: all opponents. None without a clear carrier."""
    d_ball = np.linalg.norm(mates_xy - ball_xy, axis=1)
    c = int(np.argmin(d_ball))
    if d_ball[c] > CARRIER_RADIUS or len(mates_xy) < 10:
        return None
    car = mates_xy[c]
    mates = np.delete(mates_xy, c, axis=0)
    d = np.linalg.norm(mates - car, axis=1)
    # free lane: no opponent within LANE_CLEAR of the segment carrier -> mate
    cand = mates[(d >= 5) & (d <= 25)]
    free = 0
    for m in cand:
        seg = m - car
        t = np.clip(((opp_xy - car) @ seg) / (seg @ seg), 0, 1)
        if np.min(np.linalg.norm(opp_xy - (car + t[:, None] * seg), axis=1)) > LANE_CLEAR:
            free += 1
    tri_ok = (d >= 8) & (d <= 20)
    idx = np.flatnonzero(tri_ok)
    tri = 0
    for i in range(len(idx)):
        for j in range(i + 1, len(idx)):
            ab = np.linalg.norm(mates[idx[i]] - mates[idx[j]])
            if 8 <= ab <= 20:
                tri += 1
    return {
        "binnen_10m": int((d <= 10).sum()),
        "binnen_20m": int((d <= 20).sum()),
        "binnen_30m": int((d <= 30).sum()),
        "passopties_vrij": free,
        "driehoeken": tri,
        "dichtste_teamgenoot_m": float(d.min()),
    }


def passes_summary(passes):
    """passes: list of (team, run_id, dx_forward, length) for completed passes; run_id identifies a possession run."""
    if not passes:
        return {}
    dx = np.array([p[2] for p in passes])
    ln = np.array([p[3] for p in passes])
    return {"vooruit_aandeel": float((dx > 0).mean()), "passlengte_m": float(ln.mean()),
            "passlengte_mediaan_m": float(np.median(ln))}


def runs_from_sequence(seq):
    """seq: chronological list of (team, is_pass) ball actions. Possession run = maximal block of one team.
    Returns the number of passes per run (list)."""
    runs, cur_team, n = [], None, 0
    for team, is_pass in seq:
        if team != cur_team:
            if cur_team is not None:
                runs.append(n)
            cur_team, n = team, 0
        n += int(is_pass)
    if cur_team is not None:
        runs.append(n)
    return runs


def pct(v):
    v = np.asarray(v, dtype=float)
    if v.size == 0:
        return {"n": 0}
    p25, p50, p75 = np.percentile(v, [25, 50, 75])
    return {"p25": round(float(p25), 3), "p50": round(float(p50), 3), "p75": round(float(p75), 3),
            "mean": round(float(v.mean()), 3), "n": int(v.size)}


if __name__ == "__main__":  # self-check
    sq = np.array([[0, 0], [10, 0], [10, 10], [0, 10], [5, 5]], float)
    assert abs(hull_area(sq) - 100) < 1e-9
    assert runs_from_sequence([("A", 1), ("A", 1), ("B", 0), ("A", 1)]) == [2, 0, 1]
    mates = np.array([[0, 0], [10, 0], [5, 12], [40, 0], [0, -30], [-1, 20], [3, 3], [-10, -10], [20, 20], [0, 30]], float)
    r = carrier_frame(mates, np.array([[5, 0.5]]), np.array([0.5, 0]))
    assert r["passopties_vrij"] >= 1 and r["driehoeken"] >= 1, r
    print("ok")
