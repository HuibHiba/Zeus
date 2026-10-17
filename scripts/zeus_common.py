"""Gedeelde hulpfuncties voor de datascripts van Zeus online.

- auc (met tie-correctie via gemiddelde rangen), bootstrap-betrouwbaarheidsintervallen (per wedstrijd geclusterd, vaste seed)
- logistic_fit (gestandaardiseerde kenmerken, ridge, convergentievlag; coefficienten terug op de oorspronkelijke schaal)
- atomaire schrijffuncties (tijdelijk bestand + os.replace) en JSON zonder NaN
- SkipLog: telt overgeslagen/gefaalde items per reden, schrijft data/skiplog_<script>.txt en geeft een exitcode
- pass_targets: beoogde ontvanger van een pass (positie uit het Ball Receipt*-event van de beoogde ontvanger)
"""
import gzip
import json
import math
import os
import pickle
import threading
from collections import Counter
from pathlib import Path

import numpy as np
import pandas as pd

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "data"
YARD = 0.9144

LICENSE_STATSBOMB = "intern (StatsBomb, commercieel gebruik niet bevestigd)"
LICENSE_WYSCOUT = "CC BY 4.0 op primaire Figshare-records bevestigd (2026-10-17); productvrijgave volgens Licenties en Steam blijft open"
LEAK_WARNING = ("Deze coefficienten bevatten uitkomst-lekkage (lengte/hoek/eindpunt van de pass zijn bij mislukte passes de plek waar de bal "
                "eindigde) en zijn niet als kans voor de pass bruikbaar; gebruik het bijbehorende *_v2.json.")

# StatsBomb: testset = hele competitie-seizoenen (geen splitsing per match_id-pariteit, die lekt tussen wedstrijden van hetzelfde team/seizoen)
SB_TEST_GROUPS = {"UEFA Euro 2024", "Ligue 1 2022/2023", "La Liga 2020/2021", "Major League Soccer 2023", "Copa America 2024"}
WY_TEST_GROUPS = {"Ligue 1 2017/18", "EK 2016", "Bundesliga 2017/18"}


# ---------------------------------------------------------------- bestanden

def atomic_write_bytes(path: Path, data: bytes) -> None:
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(f"{path.name}.tmp{os.getpid()}_{threading.get_ident()}")
    try:
        tmp.write_bytes(data)
        os.replace(tmp, path)
    finally:
        if tmp.exists():
            tmp.unlink()


def atomic_write_text(path: Path, text: str) -> None:
    atomic_write_bytes(path, text.encode("utf-8"))


def atomic_pickle_gz(path: Path, obj) -> None:
    path = Path(path)
    tmp = path.with_name(f"{path.name}.tmp{os.getpid()}_{threading.get_ident()}")
    try:
        with gzip.open(tmp, "wb") as f:
            pickle.dump(obj, f)
        os.replace(tmp, path)
    finally:
        if tmp.exists():
            tmp.unlink()


def _json_default(o):
    if isinstance(o, np.integer):
        return int(o)
    if isinstance(o, np.floating):
        return float(o)
    if isinstance(o, np.bool_):
        return bool(o)
    if isinstance(o, np.ndarray):
        return o.tolist()
    raise TypeError(f"niet serialiseerbaar: {type(o)}")


def dumps_json(obj) -> str:
    """JSON zonder NaN/Infinity (ValueError als die toch voorkomen)."""
    return json.dumps(obj, indent=2, ensure_ascii=False, allow_nan=False, default=_json_default)


def write_json(path: Path, obj) -> None:
    atomic_write_text(path, dumps_json(obj))


def num(v, nd=3):
    """Float afgerond, NaN -> None."""
    v = float(v)
    return None if (math.isnan(v) or math.isinf(v)) else round(v, nd)


# ---------------------------------------------------------------- stille fouten

class SkipLog:
    """Telt overgeslagen of gefaalde items per reden. finish() print, schrijft data/skiplog_<script>.txt en geeft de exitcode."""

    def __init__(self, script: str):
        self.script = script
        self.items: list[tuple[str, str]] = []
        self.n_ok = 0
        self._lock = threading.Lock()

    def ok(self, n: int = 1) -> None:
        with self._lock:
            self.n_ok += n

    def skip(self, ident, reason: str) -> None:
        with self._lock:
            self.items.append((str(ident), str(reason)[:200]))

    def finish(self, max_share: float = 0.01) -> int:
        total = self.n_ok + len(self.items)
        share = len(self.items) / total if total else 1.0
        by_reason = Counter(r.split(":")[0] for _, r in self.items)
        path = DATA / f"skiplog_{self.script}.txt"
        atomic_write_text(path, "".join(f"{i}\t{r}\n" for i, r in self.items))
        print(f"[{self.script}] verwerkt {self.n_ok}, overgeslagen/gefaald {len(self.items)} van {total} ({share:.2%}); lijst -> {path}")
        for r, n in by_reason.most_common():
            print(f"    {n:5d} x {r}")
        if total == 0 or self.n_ok == 0:
            print(f"[{self.script}] FOUT: niets verwerkt (alle invoer leeg of gefaald)")
            return 1
        if share > max_share:
            print(f"[{self.script}] FOUT: aandeel overgeslagen {share:.2%} > {max_share:.0%}")
            return 1
        return 0


# ---------------------------------------------------------------- statistiek

def _avg_ranks(p: np.ndarray) -> np.ndarray:
    n = len(p)
    order = np.argsort(p, kind="mergesort")
    sp = p[order]
    flag = np.concatenate(([True], sp[1:] != sp[:-1]))
    starts = np.flatnonzero(flag)
    ends = np.append(starts[1:], n)
    avg = (starts + 1 + ends) / 2.0  # rangen start+1 .. end
    ranks = np.empty(n)
    ranks[order] = np.repeat(avg, ends - starts)
    return ranks


def auc(y, p) -> float:
    """AUC via Mann-Whitney met gemiddelde rangen bij gelijke scores; NaN als een klasse leeg is."""
    y = np.asarray(y).astype(bool)
    p = np.asarray(p, dtype=float)
    n1 = int(y.sum())
    n0 = len(y) - n1
    if n1 == 0 or n0 == 0:
        return float("nan")
    r = _avg_ranks(p)
    return float((r[y].sum() - n1 * (n1 + 1) / 2) / (n1 * n0))


def _cluster_index(groups):
    codes, uniq = pd.factorize(pd.Series(groups))
    order = np.argsort(codes, kind="mergesort")
    bounds = np.searchsorted(codes[order], np.arange(len(uniq) + 1))
    return order, bounds, len(uniq)


def _boot_indices(order, bounds, g, n_boot, seed):
    rng = np.random.default_rng(seed)
    for _ in range(n_boot):
        pick = rng.integers(0, g, g)
        yield np.concatenate([order[bounds[k]:bounds[k + 1]] for k in pick])


def auc_report(y, p, groups=None, n_boot: int = 200, seed: int = 20261013) -> dict:
    """AUC met 95%-interval (bootstrap 200x, vaste seed; per wedstrijd/cluster geresampled als `groups` is gegeven)."""
    y = np.asarray(y).astype(bool)
    p = np.asarray(p, dtype=float)
    a = auc(y, p)
    if math.isnan(a):
        return {"auc": None, "ci95": None, "n": int(len(y))}
    if groups is None:
        groups = np.arange(len(y))
    order, bounds, g = _cluster_index(groups)
    vals = [auc(y[i], p[i]) for i in _boot_indices(order, bounds, g, n_boot, seed)]
    vals = [v for v in vals if not math.isnan(v)]
    lo, hi = np.percentile(vals, [2.5, 97.5])
    return {"auc": round(a, 4), "ci95": [round(float(lo), 4), round(float(hi), 4)], "n": int(len(y))}


def auc_diff_report(y, p_a, p_b, groups=None, n_boot: int = 200, seed: int = 20261013) -> dict:
    """Verschil AUC(b) - AUC(a) met gepaard bootstrap-interval (zelfde resamples)."""
    y = np.asarray(y).astype(bool)
    p_a, p_b = np.asarray(p_a, dtype=float), np.asarray(p_b, dtype=float)
    d = auc(y, p_b) - auc(y, p_a)
    if math.isnan(d):
        return {"verschil": None, "ci95": None, "binnen_ruis": None}
    if groups is None:
        groups = np.arange(len(y))
    order, bounds, g = _cluster_index(groups)
    vals = [auc(y[i], p_b[i]) - auc(y[i], p_a[i]) for i in _boot_indices(order, bounds, g, n_boot, seed)]
    vals = [v for v in vals if not math.isnan(v)]
    lo, hi = np.percentile(vals, [2.5, 97.5])
    return {"verschil": round(float(d), 4), "ci95": [round(float(lo), 4), round(float(hi), 4)], "binnen_ruis": bool(lo <= 0 <= hi)}


def logistic_fit(X, y, ridge: float = 1.0, iters: int = 100, tol: float = 1e-7, label: str = ""):
    """Logistische regressie (Newton) op gestandaardiseerde kenmerken met ridge (niet op het intercept).

    Geeft (coefficienten op de oorspronkelijke kenmerkschaal, geconvergeerd). Print een waarschuwing als het niet convergeert.
    De eerste constante kolom is het intercept."""
    X = np.asarray(X, dtype=float)
    y = np.asarray(y, dtype=float)
    mu, sd = X.mean(0), X.std(0)
    const = sd < 1e-12
    sd_safe = np.where(const, 1.0, sd)
    Z = (X - np.where(const, 0.0, mu)) / sd_safe
    pen = np.where(const, 1e-8, ridge)
    w = np.zeros(X.shape[1])
    converged = False
    for _ in range(iters):
        p = 1 / (1 + np.exp(-np.clip(Z @ w, -30, 30)))
        g = Z.T @ (p - y) + pen * w
        h = (Z * (p * (1 - p))[:, None]).T @ Z + np.diag(pen)
        try:
            step = np.linalg.solve(h, g)
        except np.linalg.LinAlgError:
            break
        w -= step
        if np.max(np.abs(step)) < tol:
            converged = True
            break
    if not converged or not np.all(np.isfinite(w)):
        converged = False
        print(f"WAARSCHUWING logistic_fit{' ' + label if label else ''}: niet geconvergeerd (n={len(y)}, kenmerken={X.shape[1]})", flush=True)
    raw = w / sd_safe
    ci = np.flatnonzero(const)
    if len(ci):
        raw[ci[0]] = w[ci[0]] - float(np.sum(raw[~const] * mu[~const]))
        raw[ci[1:]] = w[ci[1:]]
    return raw, converged


def predict(X, w) -> np.ndarray:
    return 1 / (1 + np.exp(-np.clip(np.asarray(X, dtype=float) @ w, -30, 30)))


# ---------------------------------------------------------------- StatsBomb-hulpjes

def loc(series, i):
    return series.map(lambda v: v[i] if isinstance(v, (list, tuple, np.ndarray)) and len(v) > i else np.nan)


def match_groups() -> dict:
    """match_id -> competitie-seizoen (uit de eigen benchmarktabellen)."""
    out = {}
    try:
        for m in pd.read_csv(DATA / "pl_1516_team_match.csv")["match_id"].unique():
            out[int(m)] = "Premier League 2015/16"
        allm = pd.read_csv(DATA / "team_match_all.csv")[["match_id", "competition"]].drop_duplicates("match_id")
        for m, c in zip(allm.match_id, allm.competition):
            out[int(m)] = str(c)
    except FileNotFoundError:
        pass
    return out


def pass_targets(ev: pd.DataFrame, passes: pd.DataFrame) -> pd.DataFrame:
    """Beoogde ontvanger per pass: positie (tx, ty; StatsBomb-yards) uit het Ball Receipt*-event van pass_recipient dat aan de pass
    gekoppeld is (related_events). Bij mislukte passes is dat de plek van de ontvanger, niet het eindpunt van de bal.
    NaN als de beoogde ontvanger of zijn event ontbreekt. `ev` heeft kolommen x, y (uit location)."""
    rc = ev.loc[ev["type"] == "Ball Receipt*", ["id", "player", "x", "y"]].rename(columns={"id": "rid", "player": "rplayer", "x": "tx", "y": "ty"})
    ex = passes[["id", "pass_recipient", "related_events"]].copy()
    ex["related_events"] = ex["related_events"].map(lambda v: v if isinstance(v, (list, tuple, np.ndarray)) else [])
    ex = ex.explode("related_events").merge(rc, left_on="related_events", right_on="rid", how="left")
    ex = ex[ex["rplayer"].notna() & (ex["rplayer"] == ex["pass_recipient"])].drop_duplicates("id")
    return passes[["id"]].merge(ex[["id", "tx", "ty"]], on="id", how="left")[["tx", "ty"]].set_axis(passes.index)
