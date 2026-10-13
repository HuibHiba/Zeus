// WP8 proef B: hetzelfde wegwerpmodel als proto/core-csharp, in JavaScript op Node.
// Doel: snelheid en determinisme vergelijken, niet voetbalrealisme.
import { readFileSync } from 'node:fs';
import { performance } from 'node:perf_hooks';
import path from 'node:path';

const root = process.argv[2] ?? 'C:\\dev\\zeus';
const L = 105, W = 68, Dt = 0.1, SPEED = 6.0, TICKS = 54000;

function loadTactic(file) {
  const doc = JSON.parse(readFileSync(path.join(root, 'tactics', file), 'utf8'));
  const ids = doc.spelers.map((s) => s.id);
  const resolve = (name) => {
    const f = doc.fases[name];
    if (f.gelijk_aan) return resolve(f.gelijk_aan);
    const map = new Map(f.spelers.map((s) => [s.id, s]));
    return ids.map((id) => {
      const s = map.get(id);
      const [fx, fy] = s.volg_bal ?? [0, 0];
      return { x: s.pos[0], y: s.pos[1], fx, fy };
    });
  };
  const phases = {};
  for (const n of Object.keys(doc.fases)) phases[n] = resolve(n);
  return phases;
}

// mulberry32: kleine, snelle, seedbare PRNG
function rngFrom(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const worldX = (t, sx) => (t === 0 ? (sx - 0.5) * L : (0.5 - sx) * L);
const worldY = (t, sy) => (t === 0 ? (sy - 0.5) * W : (0.5 - sy) * W);
const rel = (t, wx) => (t === 0 ? wx / L + 0.5 : 0.5 - wx / L);
const dist = (ax, ay, bx, by) => Math.hypot(ax - bx, ay - by);

function runMatch(tac, seed) {
  const rnd = rngFrom(seed);
  const rint = (n) => Math.floor(rnd() * n);
  const x = [new Float64Array(11), new Float64Array(11)];
  const y = [new Float64Array(11), new Float64Array(11)];
  const res = { goals: [0, 0], passes: [0, 0], complete: [0, 0], shots: [0, 0], hash: 0x811c9dc5 };
  const add = (a, b, c, d) => {
    for (const v of [a, b, c, d]) res.hash = Math.imul(res.hash ^ (v & 0xffffffff), 16777619) >>> 0;
  };
  for (let t = 0; t < 2; t++) {
    const s = tac[t].opbouw;
    for (let i = 0; i < 11; i++) { x[t][i] = worldX(t, s[i].x); y[t][i] = worldY(t, s[i].y); }
  }
  let poss = 0, carrier = 9, countdown = 40;

  function decide(tick) {
    const t = poss, o = 1 - t;
    const cx = x[t][carrier], cy = y[t][carrier];
    for (let j = 0; j < 11; j++) {
      if (dist(cx, cy, x[o][j], y[o][j]) < 2.0 && rnd() < 0.15) {
        add(tick, 1, o, j); poss = o; carrier = j; return;
      }
    }
    const goalX = t === 0 ? L / 2 : -L / 2;
    const dGoal = dist(cx, cy, goalX, 0);
    if (dGoal < 25 && rnd() < 0.35) {
      res.shots[t]++;
      const p = 0.02 + 0.35 * (1 - dGoal / 25) ** 2;
      if (rnd() < p) { res.goals[t]++; add(tick, 2, t, carrier); poss = o; carrier = 9; }
      else { add(tick, 3, t, carrier); poss = o; carrier = 0; }
      return;
    }
    const ownRel = rel(t, cx);
    const options = [];
    for (let j = 0; j < 11; j++) {
      if (j === carrier) continue;
      const d = dist(cx, cy, x[t][j], y[t][j]);
      if (d >= 8 && d <= 35 && rel(t, x[t][j]) > ownRel - 0.05) options.push(j);
    }
    if (options.length === 0) { carrier = rint(11); return; }
    const r = options[rint(options.length)];
    const dd = dist(cx, cy, x[t][r], y[t][r]);
    let pressure = 0;
    for (let j = 0; j < 11; j++) if (dist(x[t][r], y[t][r], x[o][j], y[o][j]) < 4.0) pressure++;
    const pp = Math.min(0.95, Math.max(0.3, 0.92 - 0.004 * dd - 0.12 * pressure));
    res.passes[t]++;
    if (rnd() < pp) { res.complete[t]++; add(tick, 4, t, r); carrier = r; }
    else {
      let near = 0, best = Infinity;
      for (let j = 0; j < 11; j++) {
        const d = dist(x[t][r], y[t][r], x[o][j], y[o][j]);
        if (d < best) { best = d; near = j; }
      }
      add(tick, 5, o, near); poss = o; carrier = near;
    }
  }

  for (let tick = 0; tick < TICKS; tick++) {
    const bx = x[poss][carrier];
    for (let t = 0; t < 2; t++) {
      const slots = tac[t][t === poss ? 'opbouw' : 'verdedigen'];
      const ballRel = rel(t, bx);
      for (let i = 0; i < 11; i++) {
        const s = slots[i];
        const tx = worldX(t, s.x + s.fx * (ballRel - 0.5) * 0.5);
        const ty = worldY(t, s.y);
        const dx = tx - x[t][i], dy = ty - y[t][i];
        const d = Math.sqrt(dx * dx + dy * dy);
        const step = SPEED * Dt;
        if (d <= step) { x[t][i] = tx; y[t][i] = ty; }
        else { x[t][i] += (dx / d) * step; y[t][i] += (dy / d) * step; }
      }
    }
    if (--countdown <= 0) { decide(tick); countdown = 30 + rint(40); }
  }
  return res;
}

const tac = [loadTactic('laag_blok_442.json'), loadTactic('man_om_man_433.json')];
const h1 = runMatch(tac, 42), h2 = runMatch(tac, 42);
console.log(`Determinisme (seed 42): ${h1.hash === h2.hash ? 'OK' : 'FOUT'}  hash=${h1.hash.toString(16).toUpperCase()}`);
let t0 = performance.now();
runMatch(tac, 1);
console.log(`1 wedstrijd: ${(performance.now() - t0).toFixed(0)} ms`);
const n = 200;
let passes = 0, complete = 0, goals = 0, shots = 0;
t0 = performance.now();
for (let s = 0; s < n; s++) {
  const r = runMatch(tac, 1000 + s);
  passes += r.passes[0] + r.passes[1]; complete += r.complete[0] + r.complete[1];
  goals += r.goals[0] + r.goals[1]; shots += r.shots[0] + r.shots[1];
}
const ms = performance.now() - t0;
console.log(`${n} wedstrijden: ${ms.toFixed(0)} ms totaal, ${(ms / n).toFixed(1)} ms per wedstrijd, 1000 wedstrijden ca. ${(ms * 1000 / n / 1000).toFixed(1)} s`);
console.log(`Gemiddeld per wedstrijd: passes ${(passes / n).toFixed(0)}, passnauwkeurigheid ${(100 * complete / passes).toFixed(0)}%, schoten ${(shots / n).toFixed(1)}, doelpunten ${(goals / n).toFixed(2)}`);
