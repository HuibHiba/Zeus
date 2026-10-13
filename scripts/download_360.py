"""Haalt StatsBomb 360-freeze-frames op voor alle beschikbare wedstrijden van recente competities en bewaart ze lokaal.

Elke wedstrijd wordt een bestand data/raw360/frames_<match_id>.pkl.gz (pandas DataFrame met event_uuid, visible_area en
freeze_frame per event). Bestaande bestanden worden overgeslagen. Rustig tempo (4 parallelle verzoeken, via de officiele
statsbombpy-bibliotheek op de open-data repo). Bron: StatsBomb open data (bronvermelding met logo; commercieel gebruik onduidelijk).
"""
import sys
import warnings
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import pandas as pd
from statsbombpy import sb

sys.path.insert(0, str(Path(__file__).resolve().parent))
from zeus_common import SkipLog, atomic_pickle_gz  # noqa: E402

warnings.filterwarnings("ignore")
ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "data" / "raw360"


def fetch(match_id: int, skip: SkipLog) -> str:
    path = OUT / f"frames_{match_id}.pkl.gz"
    if path.exists():
        skip.ok()
        return "bestaand"
    try:
        frames = sb.frames(match_id=match_id, fmt="dataframe")
    except Exception as exc:  # een kapotte wedstrijd mag de rest niet stoppen, maar wordt wel geteld
        skip.skip(match_id, f"download: {type(exc).__name__}")
        return f"fout: {exc}"
    if frames is None or len(frames) == 0:
        skip.skip(match_id, "leeg: geen frames")
        return "leeg"
    atomic_pickle_gz(path, frames)  # tijdelijk bestand + os.replace: geen half bestand dat later als 'bestaand' telt
    skip.ok()
    return f"{len(frames)} rijen"


def main() -> int:
    skip = SkipLog("download_360")
    OUT.mkdir(parents=True, exist_ok=True)
    comps = sb.competitions()
    comps = comps[(comps["competition_gender"] == "male") & comps["match_available_360"].notna()]
    ids = []
    for _, c in comps.iterrows():
        m = sb.matches(competition_id=int(c["competition_id"]), season_id=int(c["season_id"]))
        if "match_status_360" in m.columns:
            m = m[m["match_status_360"] == "available"]
        print(f"{c['competition_name']} {c['season_name']}: {len(m)} wedstrijden met 360", flush=True)
        ids += [int(x) for x in m["match_id"]]
    print(f"totaal {len(ids)} wedstrijden", flush=True)
    done = 0
    with ThreadPoolExecutor(max_workers=4) as pool:
        for res in pool.map(lambda i: fetch(i, skip), ids):
            done += 1
            if done % 25 == 0:
                print(f"{done}/{len(ids)} ({res})", flush=True)
    code = skip.finish()
    print("klaar")
    return code


if __name__ == "__main__":
    sys.exit(main())
