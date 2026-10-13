"""TESTDATA (niet voor het product): haalt de wedstrijdpagina's van een competitie op bij FotMob, rustig en met stop bij een blokkade.

Gebruik: python -I scripts/fotmob_test_fetch.py 57 eredivisie [maximum]
- competitie-id 57 = Eredivisie. Eerst de competitiepagina (1 verzoek), dan elke afgelopen wedstrijd die nog ontbreekt.
- 3 seconden tussen verzoeken, eerlijke User-Agent, alleen gewone pagina's (robots.txt verbiedt alleen /api/*).
- Stopt direct bij een andere reactie dan 200 (bijv. 403 of 429); geen pogingen om beveiliging te omzeilen.
Licentie voor hergebruik onduidelijk: data alleen lokaal en niet voor het product (besluit maker, 2026-10-13).
"""
import json
import re
import sys
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEST = ROOT / "data" / "external" / "testdata_fotmob"
UA = "Mozilla/5.0 (compatible; ZeusResearchTest/0.1; personal test, low rate)"
DELAY = 3.0


def get(url: str) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=40) as r:
        if r.status != 200:
            raise RuntimeError(f"HTTP {r.status}")
        return r.read()


def next_data(html: bytes) -> dict:
    m = re.search(rb'<script id="__NEXT_DATA__"[^>]*>(.*?)</script>', html, re.S)
    return json.loads(m.group(1))


def find_matches(obj, out):
    """Zoek alle wedstrijdobjecten (met id, pageUrl en status.finished) ergens in de competitie-JSON."""
    if isinstance(obj, dict):
        if "pageUrl" in obj and "id" in obj and isinstance(obj.get("status"), dict):
            out[str(obj["id"])] = obj
        for v in obj.values():
            find_matches(v, out)
    elif isinstance(obj, list):
        for v in obj:
            find_matches(v, out)


def main() -> None:
    league_id, slug = sys.argv[1], sys.argv[2]
    limit = int(sys.argv[3]) if len(sys.argv) > 3 else 10_000
    DEST.mkdir(parents=True, exist_ok=True)
    league_html = get(f"https://www.fotmob.com/leagues/{league_id}/overview/{slug}")
    (DEST / f"league_{league_id}.html").write_bytes(league_html)
    matches = {}
    find_matches(next_data(league_html), matches)
    finished = sorted((m for m in matches.values() if m["status"].get("finished")), key=lambda m: m["status"].get("utcTime", ""))
    todo = [m for m in finished if not (DEST / f"match_{m['id']}.html").exists()][:limit]
    print(f"{len(matches)} wedstrijden gevonden, {len(finished)} gespeeld, {len(todo)} nog op te halen", flush=True)
    for i, m in enumerate(todo, 1):
        time.sleep(DELAY)
        try:
            body = get("https://www.fotmob.com" + m["pageUrl"].split("#")[0])
        except Exception as exc:  # 403/429/time-out: stoppen, niet omzeilen
            print(f"STOP bij {m['id']}: {exc}")
            break
        (DEST / f"match_{m['id']}.html").write_bytes(body)
        print(f"{i}/{len(todo)} {m['id']} {m['status'].get('scoreStr')}", flush=True)


if __name__ == "__main__":
    sys.exit(main())
