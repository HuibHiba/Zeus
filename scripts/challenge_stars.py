"""Herijking uitdagingen 11-36 op HEAD: draait Zeus.Runner --challenge-calibrate per uitdaging, rekent sterkansen met bootstrap."""
import glob, json, os, random, subprocess, sys

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
EXE = glob.glob(os.path.join(REPO, r"sim\Zeus.Runner\bin\Release\*\Zeus.Runner.exe"))[0]
OUT = os.path.join(REPO, "local", "challenge-recal")
TACTICS = "hoge_druk_433,positiespel_4231,counter_532,laag_blok_442"
SEEDS, FIRST = 40, 801
os.makedirs(OUT, exist_ok=True)


def specs():
    for f in sorted(glob.glob(os.path.join(REPO, "challenges", "*.json"))):
        d = json.load(open(f, encoding="utf8"))
        if int(d["id"][:2]) >= 11:
            yield d


def run(d):
    path = os.path.join(OUT, d["id"] + ".jsonl")
    if os.path.exists(path):
        return path
    a = [EXE, "--challenge-calibrate", "--opponent", d["tegenstander"].removesuffix(".json"),
         "--opponent-level", d.get("niveau_tegenstander", "halfprof"), "--level", d.get("niveau_speler", "halfprof"),
         "--first-seed", str(FIRST), "--seeds", str(SEEDS), "--max-parallel", "16", "--tactics", TACTICS, "--results", path]
    if d.get("start_voor"): a += ["--start-for", str(d["start_voor"])]
    if d.get("start_tegen"): a += ["--start-against", str(d["start_tegen"])]
    if d.get("tegenstander_rood"): a += ["--opponent-sent-off"]
    if d.get("start_minuut"): a += ["--start-minute", str(d["start_minuut"])]
    subprocess.run(a, cwd=REPO, check=True, stdout=subprocess.DEVNULL)
    return path


def value(m, rs):
    n = len(rs)
    avg = lambda f: sum(f(r) for r in rs) / n
    return {"doelpunten_voor": lambda: avg(lambda r: r["gf"]), "doelpunten_tegen": lambda: avg(lambda r: r["ga"]),
            "doelsaldo": lambda: avg(lambda r: r["gf"] - r["ga"]), "schoten_voor": lambda: avg(lambda r: r["sf"]),
            "schoten_tegen": lambda: avg(lambda r: r["sa"]),
            "balbezit": lambda: 100 * sum(r["cf"] for r in rs) / max(1, sum(r["cf"] + r["ca"] for r in rs)),
            "overtredingen": lambda: avg(lambda r: r["fouls"]), "gele_kaarten": lambda: avg(lambda r: r["yellow"]),
            "rode_kaarten": lambda: sum(r["red"] for r in rs), "winst": lambda: sum(r["gf"] > r["ga"] for r in rs),
            "ongeslagen": lambda: sum(r["gf"] >= r["ga"] for r in rs)}[m]()


def meets(crits, rs):
    return all((value(c["maat"], rs) >= c["waarde"] - 1e-9) if c["operator"] == ">=" else (value(c["maat"], rs) <= c["waarde"] + 1e-9) for c in crits)


def stars(d, rs):
    s1 = meets(d["criteria"], rs)
    s2 = s1 and meets(d["ster_2"], rs)
    s3 = s2 and meets(d["ster_3"], rs)
    return s1, s2, s3


def probs(d, rows):
    rnd = random.Random(1)
    n = d["wedstrijden"]
    hit = [0, 0, 0]
    for _ in range(3000):
        for i, s in enumerate(stars(d, [rnd.choice(rows) for _ in range(n)])): hit[i] += s
    return [round(100 * h / 3000) for h in hit]


if __name__ == "__main__":
    ds = list(specs())
    if "--run" in sys.argv:
        for d in ds:
            print("run", d["id"], flush=True)
            run(d)
    print("| uitdaging | " + " | ".join(TACTICS.split(",")) + " |")
    for d in ds:
        p = os.path.join(OUT, d["id"] + ".jsonl")
        if not os.path.exists(p): continue
        by = {}
        for line in open(p):
            r = json.loads(line); by.setdefault(r["tactic"], []).append(r)
        print(f"| {d['id']} | " + " | ".join("/".join(map(str, probs(d, by[t]))) for t in TACTICS.split(",") if t in by) + " |")
