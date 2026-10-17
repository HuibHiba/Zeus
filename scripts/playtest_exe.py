"""Automatische speeltest van de RELEASE-exe van Gaffer's Notebook (alleen Windows, alleen stdlib + Pillow).

Start de exe verborgen met eigen APPDATA/LOCALAPPDATA/TEMP, stuurt WM_*-berichten naar het eigen
venster, maakt schermafdrukken via --write-movie, en controleert per stap of het proces leeft.
Klikposities zijn pixels in een 1520x780-schermafdruk (omgerekend naar fracties van het venster) en horen bij de menu-indeling van export 9fbf071.

Gebruik:  python playtest_exe.py -ExePath C:\\dev\\zeus-exe-test2\\GaffersNotebook.exe [-OutDir DIR]
Exitcode 0 = alle stappen leefden, geen ERROR-regels, exe sloot zelf af met 0; 1 = anders. Alleen eigen PID wordt gestopt.
"""
import argparse, ctypes, json, os, pathlib, re, shutil, subprocess, sys, tempfile, time
from ctypes import wintypes

U = ctypes.windll.user32
VK = {'ESC': 0x1B, 'ENTER': 0x0D, 'SPACE': 0x20, 'F9': 0x78, 'TAB': 0x09, 'END': 0x23, 'B': 0x42}
BAD = re.compile(r'ERROR|Unhandled|exception|SCRIPT ERROR|Traceback', re.I)


class Game:
    def __init__(self, exe, out):
        self.exe, self.out = pathlib.Path(exe).resolve(), pathlib.Path(out).resolve()
        self.out.mkdir(parents=True, exist_ok=True)
        self.frames = self.out / 'frames'
        self.frames.mkdir(exist_ok=True)
        env = os.environ.copy()
        for var, sub in [('APPDATA', 'Roaming'), ('LOCALAPPDATA', 'Local'), ('TEMP', 'Temp'), ('TMP', 'Temp')]:
            (self.out / 'profile' / sub).mkdir(parents=True, exist_ok=True)
            env[var] = str(self.out / 'profile' / sub)
        si = subprocess.STARTUPINFO(); si.dwFlags = 1; si.wShowWindow = 0  # SW_HIDE
        args = [str(self.exe), '--write-movie', str(self.frames / 'f.png'), '--fixed-fps', '10', '--max-fps', '10']
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

    def click(self, fx, fy):
        r = wintypes.RECT(); U.GetClientRect(self.hwnd, ctypes.byref(r))
        pos = (round(fy * r.bottom) << 16) | round(fx * r.right)
        for m, w in [(0x200, 0), (0x201, 1), (0x202, 0)]:
            self._post(m, w, pos); time.sleep(.08)

    def key(self, name):
        k = VK[name]; lp = 1 | (U.MapVirtualKeyW(k, 0) << 16)
        self._post(0x100, k, lp); time.sleep(.08); self._post(0x101, k, lp | 0xC0000000)

    def shot(self, name):
        """Kopieer het op één na nieuwste voltooide frame; ruim oude frames op."""
        time.sleep(.6)
        fr = sorted(self.frames.glob('f*.png'), key=lambda p: p.stat().st_mtime)
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


W, H = 1520, 780  # coordinaten hieronder zijn pixels in een schermafdruk van 1520x780 (zelfde beeldverhouding als het venster)


def full_time(g):
    """Wacht tot de eindkaart (gele play-again-knop) zichtbaar is; geeft True/False."""
    from PIL import Image
    end = time.monotonic() + 90
    while time.monotonic() < end and g.alive():
        s = g.shot('_poll')
        if s:
            with Image.open(s) as im:
                r, gr, b = im.convert('RGB').getpixel((200, 440))
            if r > 220 and 170 < gr < 220 and b < 120: return True
        time.sleep(1.5)
    return False


def flow(g, S):
    def at(x, y): return lambda: g.click(x / W, y / H)
    def key(k): return lambda: g.key(k)
    def step(name, fn, wait=2.0):
        fn(); time.sleep(wait); S.append((name, g.alive(), g.shot(name)))
    step('01_start', lambda: None, 8)
    step('02_welcome_sluiten', at(1199, 63))  # kruisje van het welkomstvenster (Continue ligt boven een Go-there-knop)
    step('03_start_match', at(1162, 610), 4)
    # De build heeft geen snelheidstoets (alleen Settings > Match length); stap 'versnellen (8)' bestaat hier niet.
    step('04_end_rest_berekenen', key('END'), 1)
    ok = full_time(g); S.append(('05_eindkaart_zichtbaar', ok and g.alive(), g.shot('05_eindkaart')))
    step('06_play_again_ENTER', key('ENTER'), 4)
    step('07_tweede_wedstrijd_esc_settings', key('ESC'))
    step('08_settings_dicht', key('ESC'))
    step('09_esc_naar_home', key('ESC'))
    step('10_home_screen_knop', at(885, 631))
    step('11_home_bevestigen_OK', at(760, 440), 3)
    step('12_settings_open', at(137, 425))
    step('13_settings_dicht', key('ESC'))
    step('14_tactic_maker', at(583, 333), 3)
    step('15_tactic_maker_menu_terug', at(1427, 90), 3)
    step('16_challenges', at(505, 288), 3)
    step('17_challenges_back', at(1427, 50), 3)
    step('18_league_season', at(126, 288), 3)
    step('19_league_back', at(1427, 62), 3)
    step('20_squad', at(123, 333), 3)
    step('21_squad_back', at(1480, 62), 3)
    step('22_afsluiten_esc', key('ESC'))
    step('23_quit_game', at(1013, 636), 2)
    step('24_quit_bevestigen_OK', at(760, 440), 5)


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
    quit_by_game = not g.alive()  # stap 23 (Quit game) moet het proces zelf afsluiten
    code = g.stop()
    bad = [l for l in g.log_text().splitlines() if BAD.search(l)]
    for n, alive, shot in S: print(('OK   ' if alive else 'DEAD ') + n, shot or '')
    print('exitcode exe:', code, '(afgesloten door Quit game)' if quit_by_game else '(NIET door Quit game: WM_CLOSE/kill nodig)'); print('slechte logregels:', len(bad)); [print('  ', l) for l in bad[:20]]
    ok = all(s[1] for s in S[:-1]) and not bad and quit_by_game and code == 0
    print('RESULTAAT:', 'PASS' if ok else 'FAIL'); return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
