// Teste de fumaça headless: roda uma corrida completa em marble_park e
// imprime o resultado. Uso: npx esbuild --bundle src/dev/simtest.ts
//   --format=esm --outfile=/tmp/simtest.mjs && node /tmp/simtest.mjs

import { buildQuickRace } from '../sim/quickrace';
import { FIXED_DT, RaceManager } from '../sim/race';

// RNG determinístico (mulberry32) para testes reproduzíveis.
function mulberry32(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const rand = mulberry32(42);
const setup = buildQuickRace('marble_park', 'red_comet');
const race = new RaceManager(setup, { aiDifficulty: 1 }, rand);

let logs = 0;
race.onRaceEvent = msg => { if (logs++ < 40) console.log('  ' + msg); };

let simTime = 0;
const MAX_SIM = 15 * 60; // 15 min de teto
while (race.state !== 'Finished' && simTime < MAX_SIM) {
  race.step(FIXED_DT);
  simTime += FIXED_DT;
}

console.log(`\n=== Estado final: ${race.state} após ${simTime.toFixed(0)}s simulados ===`);
if (race.result) {
  console.log(`Pista: ${race.result.trackName} — ${race.result.laps} voltas\n`);
  for (const e of race.result.entries) {
    console.log(
      `P${String(e.position).padStart(2)} ${e.marbleName.padEnd(12)} ${e.teamName.padEnd(20)}`
      + ` ${e.totalTime.toFixed(1)}s pits=${e.pitStops} melhor=${e.bestLapTime.toFixed(1)}s`
      + ` pts=${e.points}${e.isPlayer ? ' ← JOGADOR' : ''}${e.fastestLap ? ' ⚡' : ''}`,
    );
  }
  // Sanidade: todos com >=1 pit (regra do combustível 145/voltas)?
  const noPit = race.result.entries.filter(e => e.pitStops === 0).length;
  console.log(`\nBolinhas sem pit: ${noPit} (esperado: 0 ou poucas por timeout)`);
} else {
  console.error('FALHA: corrida não terminou!');
  for (const a of race.field.slice(0, 5)) {
    console.log(`${a.m.displayName}: state=${a.m.state} laps=${a.m.completedLaps} pos=(${a.m.x.toFixed(1)},${a.m.y.toFixed(1)}) v=${a.m.speed.toFixed(2)}`);
  }
  process.exit(1);
}
