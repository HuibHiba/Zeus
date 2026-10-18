"""Automatische speeltest van de RELEASE-exe van Gaffer's Notebook (alleen Windows, alleen stdlib + Pillow).

Start de exe verborgen met eigen APPDATA/LOCALAPPDATA/TEMP, stuurt WM_*-berichten naar het eigen
venster, maakt schermafdrukken via --write-movie, en controleert per stap of het proces leeft.
Klikposities zijn pixels in een 1600x900-schermafdruk (letterbox/volledig scherm wordt verrekend) en horen bij de layout van RC6 (kaft, rondleiding, About, bibliotheek, Club-tab, spelerkaart, highlights, editor-TEST).

Gebruik:  python playtest_exe.py -ExePath C:\\dev\\zeus-exe-test2\\GaffersNotebook.exe [-OutDir DIR]
Exitcode 0 = alle stappen leefden, geen ERROR-regels, exe sloot zelf af met 0; 1 = anders. Alleen eigen PID wordt gestopt.
"""
import argparse, ctypes, json, os, pathlib, re, shutil, subprocess, sys, tempfile, time
from ctypes import wintypes

U = ctypes.windll.user32
W, H = 1600, 900  # venster/viewport van het spel (project.godot, sinds RC6)
VK = {'ESC': 0x1B, 'ENTER': 0x0D, 'SPACE': 0x20, 'F9': 0x78, 'TAB': 0x09, 'END': 0x23, 'B': 0x42, 'BACKSPACE': 0x08}
BAD = re.compile(r'ERROR|Unhandled|exception|SCRIPT ERROR|Traceback', re.I)


class Game:
    def __init__(self, exe, out, fps=10):
        self.exe, self.out = pathlib.Path(exe).resolve(), pathlib.Path(out).resolve()
        self.out.mkdir(parents=True, exist_ok=True)
        self.frames = self.out / 'frames'
        self.frames.mkdir(exist_ok=True)
        env = os.environ.copy()
        for var, sub in [('APPDATA', 'Roaming'), ('LOCALAPPDATA', 'Local'), ('TEMP', 'Temp'), ('TMP', 'Temp')]:
            (self.out / 'profile' / sub).mkdir(parents=True, exist_ok=True)
            env[var] = str(self.out / 'profile' / sub)
        si = subprocess.STARTUPINFO(); si.dwFlags = 1; si.wShowWindow = 0  # SW_HIDE
        args = [str(self.exe), '--write-movie', str(self.frames / 'f.png'), '--fixed-fps', str(fps), '--max-fps', str(fps)]
        self.p = subprocess.Popen(args, cwd='C:\\Windows', env=env, startupinfo=si,
                                  stdout=open(self.out / 'app.out', 'wb'), stderr=open(self.out / 'app.err', 'wb'))
        self.hwnd = self._find_window()

    def _find_window(self):
        found = []
        @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
        def visit(h, _):
            pid = wintypes.DWORD(); U.GetWindowThreadProcessId(h, ctypes.byref(pid))
            if pid.value == self.p.pid:
                r = wintypes.RECT(); U.GetClientRect(h, ctypes.byref(r)); found.append((r.right * r.bottom, h))
            return True
        end = time.monotonic() + 30
        while time.monotonic() < end and self.alive():
            found.clear(); U.EnumWindows(visit, 0)
            if found and max(found)[0] > 0: return max(found)[1]
            time.sleep(.3)
        raise RuntimeError('geen venster van eigen proces')

    def alive(self):
        return self.p.poll() is None

    def _post(self, msg, wp, lp):
        U.GetWindowThreadProcessId(self.hwnd, ctypes.byref(wintypes.DWORD()))
        if not U.PostMessageW(self.hwnd, msg, wp, lp): raise ctypes.WinError()

    def click(self, x, y):
        """x, y: pixels in een schermafdruk van W x H; houdt rekening met letterbox (volledig scherm)."""
        r = wintypes.RECT(); U.GetClientRect(self.hwnd, ctypes.byref(r))
        k = min(r.right / W, r.bottom / H)
        pos = (round((r.bottom - H * k) / 2 + y * k) << 16) | round((r.right - W * k) / 2 + x * k)
        for m, w in [(0x200, 0), (0x201, 1), (0x202, 0)]:
            self._post(m, w, pos); time.sleep(.08)
        # Muis weg van de knop: anders opent na ~0,5 s een tooltip en negeert het venster daarna de volgende toets (Esc ging verloren).
        self._post(0x200, 0, (round((r.bottom - H * k) / 2 + (H - 4) * k) << 16) | round((r.right - W * k) / 2 + 4 * k))

    def wheel(self, x, y, notches=-3):
        """Muiswiel (WM_MOUSEWHEEL, schermcoordinaten van het clientgebied); negatief = omlaag."""
        r = wintypes.RECT(); U.GetClientRect(self.hwnd, ctypes.byref(r)); k = min(r.right / W, r.bottom / H)
        pt = wintypes.POINT(round((r.right - W * k) / 2 + x * k), round((r.bottom - H * k) / 2 + y * k)); self._post(0x200, 0, (pt.y << 16) | pt.x)
        U.ClientToScreen(self.hwnd, ctypes.byref(pt))
        for _ in range(abs(notches)):
            self._post(0x20A, (((120 if notches > 0 else -120) & 0xFFFF) << 16), ((pt.y & 0xFFFF) << 16) | (pt.x & 0xFFFF)); time.sleep(.1)

    def key(self, name):
        k = VK[name]; lp = 1 | (U.MapVirtualKeyW(k, 0) << 16)
        self._post(0x100, k, lp); time.sleep(.08); self._post(0x101, k, lp | 0xC0000000)

    def shot(self, name):
        """Kopieer het op één na nieuwste voltooide frame; ruim oude frames op."""
        time.sleep(.6)
        fr = sorted(self.frames.glob('f*.png'))  # op naam (framenummer), niet op mtime
        for old in fr[:-4]:
            try: old.unlink()
            except OSError: pass
        if len(fr) < 2: return None
        dst = self.out / (name + '.png')
        try: shutil.copyfile(fr[-2], dst)
        except OSError: return None
        return str(dst)

    def log_text(self):
        logs = list((self.out / 'profile').rglob('godot*.log'))
        return '\n'.join(p.read_text(errors='replace') for p in logs)

    def stop(self):
        if self.alive():
            U.PostMessageW(self.hwnd, 0x10, 0, 0)  # WM_CLOSE
            try: self.p.wait(10)
            except subprocess.TimeoutExpired:
                self.p.kill(); self.p.wait(); self.killed = True  # alleen eigen PID
        return self.p.returncode


# Coordinaten hieronder: pixels in een schermafdruk van 1600x900 (de viewport van het spel; letterbox/volledig scherm wordt in click() verrekend).
# Layout van RC6 (0.1.0-beta.1, viewport 16:9; kaft 1,1x, eindkaart-tour rechts naast de kaart, editorpaneel vanaf y 50): kaft, rondleiding (menu/match/end), About, Tactiekbibliotheek, Club-tab, spelerkaart, Watch highlights, editor met TEST-tab.
KICKOFF, CONFIRM_OK = (1200, 795), (800, 500)
MENU = {'league': (167, 322), 'squad': (163, 374), 'meter': (199, 426), 'settings': (177, 478), 'challenges': (545, 322),
        'editor': (555, 374), 'help': (509, 426), 'about': (516, 478), 'library': (676, 522)}


def px(path, xy):
    from PIL import Image
    with Image.open(path) as im: return im.convert('RGB').getpixel(xy)


def tour_on(p): return px(p, (20, 400))[0] < 16        # de rondleiding dimt het hele scherm
def cover_on(p): return px(p, (615, 300))[0] > 220 and px(p, (800, 650))[0] < 100 and not tour_on(p)   # crème titelblok op groene kaft, ondimd
def menu_on(p): return px(p, (1400, 780))[0] > 230 and px(p, (1400, 780))[2] < 120 and not tour_on(p)   # gele KICK OFF-knop, ondimd
def changed(a, b):
    from PIL import Image, ImageChops
    with Image.open(a) as x, Image.open(b) as y: return ImageChops.difference(x.convert('RGB'), y.convert('RGB')).getbbox() is not None


def frame_no(g):
    fr = sorted(g.frames.glob('f*.png')); return int(fr[-1].stem[1:]) if fr else 0


def wait_frame(g, n):
    while frame_no(g) < n and g.alive(): time.sleep(.2)


def settings_file(g):
    f = list((g.out / 'profile').rglob('instellingen.json'))
    return json.loads(f[0].read_text(encoding='utf-8')) if f else {}


def match_on(p): return abs(px(p, (90, 10))[0] - 210) < 12 and abs(px(p, (90, 10))[1] - 105) < 14   # zalmkleurig HOME-vak van het scorebord, ondimd
def cream(p, xy): r, gr, b = px(p, xy); return r > 215 and gr > 205 and b > 170


def find_player(p):
    """Zwaartepunt van het eerste rode (thuis)spelersbolletje op het veld, in schermpixels, of None."""
    from PIL import Image
    with Image.open(p) as im:
        im = im.convert('RGB'); q = im.load()
        pts = [(x, y) for y in range(76, 816, 2) for x in range(60, 1200, 2) if sum(abs(a - b) for a, b in zip(q[x, y], (210, 105, 78))) < 40]
    if not pts: return None
    x0, y0 = pts[0]; blob = [t for t in pts if abs(t[0] - x0) < 14 and abs(t[1] - y0) < 14]
    return round(sum(t[0] for t in blob) / len(blob)), round(sum(t[1] for t in blob) / len(blob))


def flow(g, S):
    last = {}

    def step(name, act=None, wait=2.0, check=None):
        """act: toetsnaam (str), (x, y) of None. check(pad) -> bool op de schermafdruk; zonder check telt alleen 'proces leeft'."""
        if isinstance(act, str): g.key(act)
        elif act: g.click(*act)
        time.sleep(wait)
        shot = g.shot(name); prev = last.get('shot'); last['shot'] = shot
        try: ok = g.alive() and (check is None or (shot is not None and bool(check(shot))))
        except Exception: ok = False
        S.append((name, ok, shot)); return shot

    def poll(name, cond, timeout):
        """Neemt elke 2 s een schermafdruk tot cond(pad) waar is; legt het resultaat vast als stap."""
        end = time.monotonic() + timeout; shot = None
        while time.monotonic() < end and g.alive():
            shot = g.shot(name)
            if shot and cond(shot): break
            time.sleep(2)
        ok = bool(shot) and g.alive() and cond(shot); last['shot'] = shot; S.append((name, ok, shot))

    def screen(p): return not menu_on(p) and not match_on(p)     # een volledig scherm of dialoog (niet menu, niet wedstrijd)

    step('01_kaft_zichtbaar', None, 4, cover_on)
    step('02_kaft_openen_spatie', 'SPACE', 3, tour_on)                          # na de kaft start de rondleiding (Tour 1/6)
    t1 = last['shot']
    step('03_tour_next', (940, 518), 1.5, lambda p: tour_on(p) and changed(t1, p))   # Next naar stap 2
    step('04_tour_overslaan_esc', 'ESC', 1.5, menu_on)                          # Esc slaat de hele rondleiding over
    step('05_settings', MENU['settings'], 2, screen)
    step('06_club_tab', (692, 271), 1.5, screen)
    step('07_club_shirt_blauw', (513, 350), 1.5, lambda p: bool(settings_file(g).get('club_kit1')))   # keuze staat in instellingen.json
    step('08_settings_dicht', (546, 710), 1.5, menu_on)                 # Resume; Esc ging hier verloren zolang een kleur-tooltip openstond
    step('09_about', MENU['about'], 2, screen)
    step('10_about_terug', (800, 805), 1.5, menu_on)
    step('11_tactiekbibliotheek', MENU['library'], 2, screen)
    lib = last['shot']
    step('12_bibliotheek_item', (575, 215), 1.5, lambda p: changed(lib, p))
    step('13_bibliotheek_terug', (800, 798), 1.5, menu_on)
    step('14_kick_off', KICKOFF, 4, tour_on)                                    # eerste wedstrijd: 'Coach with B'-kaart
    step('15_match_tour_got_it', (904, 848), 1.5, match_on)
    k = frame_no(g)
    wait_frame(g, k + 300)                                                      # ~ minuut 10 van de wedstrijd (20x)
    step('16_pauze_spatie', 'SPACE', 1.0, match_on)
    before = last['shot']; pl = find_player(before)
    step('17_spelerkaart_klik', pl, 1.5, lambda p: pl is not None and changed(before, p))   # spelerkaart (en ring om de speler) verschijnt
    step('18_hervatten_spatie', 'SPACE', 1.5, match_on)
    step('19_coach_B', 'B', 1.0, match_on)
    poll('20_coachmenu_bij_dode_bal', lambda p: cream(p, (760, 250)), 120)       # subs/tactics-paneel opent bij het eerstvolgende rustmoment
    step('21_coach_hervatten_ENTER', 'ENTER', 1.5, match_on)
    step('22_END_eindresultaat', 'END', 1.0)
    poll('23_eindkaart', lambda p: cream(p, (300, 150)) and not cream(p, (1160, 150)), 60)
    step('24_eindkaart_tour_got_it', (1479, 496), 1.5, lambda p: not tour_on(p) and cream(p, (300, 150)))
    end = last['shot']
    step('25_watch_highlights', (400, 495), 5, lambda p: changed(end, p))
    step('26_highlights_overslaan_esc', 'ESC', 2, lambda p: cream(p, (300, 150)))
    step('27_play_again_ENTER', 'ENTER', 4, lambda p: match_on(p))               # nieuwe wedstrijd
    step('28_esc_settings_in_wedstrijd', 'ESC', 1.5, lambda p: not match_on(p))
    step('29_home_screen_knop', (926, 710), 1.5)
    step('30_home_bevestigen_OK', CONFIRM_OK, 3, menu_on)
    step('31_challenges', MENU['challenges'], 3, screen)
    step('32_challenges_terug', (1490, 50), 3, menu_on)
    step('33_tactiekmeter', MENU['meter'], 4, screen)
    step('34_meter_terug', (171, 843), 3, menu_on)
    step('35_league_intro', MENU['league'], 3, screen)
    step('36_league_start_seizoen', (800, 688), 3, screen)
    step('37_league_terug', (1490, 46), 3, menu_on)
    step('38_squad', MENU['squad'], 3, screen)
    step('39_squad_terug', (1495, 40), 3, menu_on)
    m = last['shot']
    step('40_help_welkomstvenster', MENU['help'], 2, lambda p: changed(m, p))     # venstertje 'Welcome' (menu blijft zichtbaar)
    step('41_help_sluiten_esc', 'ESC', 2, menu_on)
    step('42_tactic_maker', MENU['editor'], 3, screen)
    step('43_editor_test_tab', (1534, 303), 1.5, screen)
    step('44_editor_play_quick_test', (1334, 576), 1.0)
    q = last['shot']
    step('45_editor_test_resultaat', None, 40, lambda p: changed(q, p))          # snelle test: 4 korte wedstrijden
    r45 = last['shot']; g.wheel(1330, 542, -5)                                  # paneel omlaag scrollen: daar staan uitslag en heatmap
    step('46_editor_test_uitslag_scroll', None, 1.5, lambda p: changed(r45, p))
    step('47_editor_menu_terug', (1507, 132), 3, menu_on)
    step('48_afsluiten_esc', 'ESC', 1.5, screen)
    step('49_quit_game', (1053, 710), 2, screen)                              # in het menu: Resume / Quit game
    step('50_quit_bevestigen_OK', CONFIRM_OK, 5)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('-ExePath', '--exe', required=True)
    ap.add_argument('-OutDir', '--out', default=None)
    a = ap.parse_args()
    out = a.out or tempfile.mkdtemp(prefix='playtest_')
    g = Game(a.exe, out); S = []
    try:
        flow(g, S)
    except Exception as e:
        S.append(('EXCEPTION ' + repr(e), g.alive(), None))
    quit_by_game = not g.alive()  # laatste stap (Quit game) moet het proces zelf afsluiten
    code = g.stop()
    bad = [l for l in g.log_text().splitlines() if BAD.search(l)]
    for n, alive, shot in S: print(('OK   ' if alive else 'DEAD ') + n, shot or '')
    print('exitcode exe:', code, '(afgesloten door Quit game)' if quit_by_game else '(NIET door Quit game: WM_CLOSE/kill nodig)'); print('slechte logregels:', len(bad)); [print('  ', l) for l in bad[:20]]
    ok = all(s[1] for s in S[:-1]) and not bad and quit_by_game and code == 0
    print('RESULTAAT:', 'PASS' if ok else 'FAIL'); return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
