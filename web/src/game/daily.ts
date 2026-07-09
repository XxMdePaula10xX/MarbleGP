// =====================================================================
// Desafio Diário — port de Core/DailyChallenge.cs
// Cenário fixo derivado da data (pista, clima, pneu, modo) + objetivo.
// Reproduzível e igual para todos no mesmo dia; placar local + streak.
// =====================================================================

import type { GripType, RaceMode, TrackData, Weather } from '../core/types';
import { TRACKS } from '../data/circuits';
import type { RaceResult } from '../sim/systems';

export type DailyObjective = 'Win' | 'Podium' | 'Top5' | 'Overtakes' | 'FastestLap';

export interface DailyChallengeDef {
  dateKey: string;   // "yyyyMMdd"
  seed: number;
  track: TrackData;
  weather: Weather;
  laps: number;
  startGrip: GripType;
  startMode: RaceMode;
  objective: DailyObjective;
  target: number;    // usado por Overtakes
  title: string;
  description: string;
}

/** Hash estável da data (mesma fórmula do C#: h=17; h=h*31+c). */
function seedFromKey(key: string): number {
  let h = 17;
  for (let i = 0; i < key.length; i++) {
    h = (Math.imul(h, 31) + key.charCodeAt(i)) | 0;
  }
  return h & 0x7fffffff;
}

/**
 * RNG determinístico (mulberry32). O seed vem de seedFromKey (mesma data =>
 * mesmo seed), então o desafio é 100% reprodutível DENTRO da plataforma web
 * (todo jogador no mesmo dia joga o mesmo cenário). Observação: o build Unity
 * legado consumia o seed com System.Random (algoritmo diferente), então os
 * cenários NÃO coincidem entre Unity e web — irrelevante agora que a web é a
 * plataforma oficial.
 */
export function seededRandom(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function todayKey(now = new Date()): string {
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  return `${y}${m}${d}`;
}

export function dailyChallengeFor(dateKey: string): DailyChallengeDef {
  const seed = seedFromKey(dateKey);
  const rng = seededRandom(seed);
  const next = (n: number) => Math.floor(rng() * n);

  const track = TRACKS[next(TRACKS.length)]!;

  const weathers: Weather[] = ['Dry', 'Dry', 'Cloudy', 'Damp', 'LightRain'];
  const weather = weathers[next(weathers.length)]!;
  const wet = weather === 'Damp' || weather === 'LightRain';

  const laps = 4 + next(3); // 4..6
  const grip: GripType = wet
    ? 'Intermediate'
    : (['Soft', 'Medium', 'Hard'] as GripType[])[next(3)]!;
  const mode = (['Normal', 'Push', 'Save'] as RaceMode[])[next(3)]!;

  const objectives: DailyObjective[] = ['Win', 'Podium', 'Top5', 'Overtakes', 'FastestLap'];
  const objective = objectives[next(5)]!;
  let target = 0;
  let description: string;
  switch (objective) {
    case 'Win': description = 'Vença a corrida.'; break;
    case 'Podium': description = 'Termine no pódio (top 3).'; break;
    case 'Top5': description = 'Termine entre os 5 primeiros.'; break;
    case 'FastestLap': description = 'Marque a volta mais rápida da corrida.'; break;
    default:
      target = 3 + next(4); // 3..6
      description = `Faça pelo menos ${target} ultrapassagens.`;
      break;
  }

  return {
    dateKey, seed, track, weather, laps,
    startGrip: grip, startMode: mode,
    objective, target,
    title: 'DESAFIO DO DIA',
    description,
  };
}

export function todaysChallenge(): DailyChallengeDef {
  return dailyChallengeFor(todayKey());
}

export interface DailyEvaluation {
  met: boolean;
  playerBestPos: number;
  playerOvertakes: number;
  playerFastest: boolean;
}

/** Avalia o resultado do jogador contra o objetivo do dia. */
export function evaluateDaily(def: DailyChallengeDef, result: RaceResult | null): DailyEvaluation {
  let playerBestPos = Number.MAX_SAFE_INTEGER;
  let playerOvertakes = 0;
  let playerFastest = false;
  if (!result) return { met: false, playerBestPos: 0, playerOvertakes, playerFastest };

  for (const e of result.entries) {
    if (!e.isPlayer) continue;
    if (e.position < playerBestPos) playerBestPos = e.position;
    playerOvertakes += e.overtakes;
    if (e.fastestLap) playerFastest = true;
  }
  if (playerBestPos === Number.MAX_SAFE_INTEGER) {
    return { met: false, playerBestPos: 0, playerOvertakes, playerFastest };
  }

  let met: boolean;
  switch (def.objective) {
    case 'Win': met = playerBestPos === 1; break;
    case 'Podium': met = playerBestPos <= 3; break;
    case 'Top5': met = playerBestPos <= 5; break;
    case 'FastestLap': met = playerFastest; break;
    case 'Overtakes': met = playerOvertakes >= def.target; break;
  }
  return { met, playerBestPos, playerOvertakes, playerFastest };
}
