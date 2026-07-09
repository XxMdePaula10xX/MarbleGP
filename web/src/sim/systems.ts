// =====================================================================
// Sistemas por-distância: pneu, combustível, energia, clima, eventos —
// port de Systems/{TireWearSystem,FuelSystem,EnergySystem,WeatherSystem}.cs
// e Race/RaceEventSystem.cs
// =====================================================================

import type { GripRing, TrackData, Weather } from '../core/types';
import { Balance } from '../data/balance';
import { fuelUsePerLap, energyChangePerLap, wearGainPerLap } from './formulas';
import type { MarbleRuntime } from './runtime';

const clamp = (v: number, lo: number, hi: number) => (v < lo ? lo : v > hi ? hi : v);

// ---------------------------------------------------------------------
// Desgaste do anel (PRD 14/24.2) — desgaste por volta convertido em
// desgaste por distância percorrida.
// ---------------------------------------------------------------------
export class TireWearSystem {
  private trackLength: number;
  private trackAbrasion: number;
  private totalLaps: number;
  onHighWearAlert: ((m: MarbleRuntime) => void) | null = null;

  constructor(track: TrackData, totalLaps: number) {
    this.trackLength = Math.max(1, track.trackLength);
    this.trackAbrasion = track.abrasionLevel;
    this.totalLaps = Math.max(1, totalLaps);
  }

  apply(m: MarbleRuntime, distanceTraveled: number, weather: Weather): void {
    if (m.state === 'InPit') return;
    const wearPerLap = wearGainPerLap(m, this.trackAbrasion, weather, this.totalLaps);
    const lapFraction = distanceTraveled / this.trackLength;
    const prev = m.wear;
    m.wear = clamp(m.wear + wearPerLap * lapFraction, 0, 100);
    if (prev < Balance.wearHeavyThreshold && m.wear >= Balance.wearHeavyThreshold)
      this.onHighWearAlert?.(m);
  }

  fitNewGrip(m: MarbleRuntime, newGrip: GripRing): void {
    m.grip = newGrip;
    m.wear = 0;
  }
}

// ---------------------------------------------------------------------
// Combustível = autonomia (PRD 4.1). Só volta no pit.
// baseFuelPerLap = 145/voltas: garante ao menos 1 pit mesmo no combo mais
// econômico (Save 0.78 × Hard 0.92 => 104% > 100%) e Push em 1 parada.
// ---------------------------------------------------------------------
export class FuelSystem {
  private trackLength: number;
  readonly baseFuelPerLap: number;
  onFuelEmpty: ((m: MarbleRuntime) => void) | null = null;

  constructor(track: TrackData, totalLaps: number) {
    this.trackLength = Math.max(1, track.trackLength);
    this.baseFuelPerLap = 145 / Math.max(1, totalLaps);
  }

  apply(m: MarbleRuntime, distanceTraveled: number): void {
    if (m.state === 'InPit') return;
    const perLap = fuelUsePerLap(m, this.baseFuelPerLap);
    const lapFraction = distanceTraveled / this.trackLength;
    const wasEmpty = m.fuel <= 0;
    m.fuel = clamp(m.fuel - perLap * lapFraction, 0, 100);
    if (!wasEmpty && m.fuel <= 0) this.onFuelEmpty?.(m);
  }

  refill(m: MarbleRuntime): void { m.fuel = 100; }

  fuelForLaps(m: MarbleRuntime, laps: number): number {
    return fuelUsePerLap(m, this.baseFuelPerLap) * laps;
  }
}

// ---------------------------------------------------------------------
// Núcleo de energia (PRD 15/24.2).
// ---------------------------------------------------------------------
export class EnergySystem {
  private trackLength: number;
  onLowEnergyAlert: ((m: MarbleRuntime) => void) | null = null;

  constructor(track: TrackData) {
    this.trackLength = Math.max(1, track.trackLength);
  }

  apply(m: MarbleRuntime, distanceTraveled: number): void {
    if (m.state === 'InPit') return;
    const changePerLap = energyChangePerLap(m);
    const lapFraction = distanceTraveled / this.trackLength;
    const prev = m.energy;
    m.energy = clamp(m.energy + changePerLap * lapFraction, 0, Balance.maxEnergy);
    if (prev >= 20 && m.energy < 20) this.onLowEnergyAlert?.(m);
  }

  refill(m: MarbleRuntime, amount: number): number {
    const before = m.energy;
    m.energy = clamp(m.energy + amount, 0, Balance.maxEnergy);
    return m.energy - before;
  }
}

// ---------------------------------------------------------------------
// Clima dinâmico POR VOLTA (PRD 10): máx 1-2(-3) mudanças por corrida,
// nunca na 1ª volta, intervalo mínimo de 3 voltas, 1 nível por vez
// (Dry-Cloudy-Damp-LightRain-HeavyRain).
// ---------------------------------------------------------------------
const WEATHER_LADDER: Weather[] = ['Dry', 'Cloudy', 'Damp', 'LightRain', 'HeavyRain'];

export class WeatherSystem {
  current: Weather;
  onChanged: ((w: Weather) => void) | null = null;

  private rainChance: number;
  private maxChanges: number;
  private static readonly MIN_LAP_GAP = 3;
  private changes = 0;
  private lastChangeLap = -99;
  private rand: () => number;

  constructor(start: Weather, rainChance: number, totalLaps: number, rand: () => number = Math.random) {
    this.current = start;
    this.rainChance = clamp(rainChance, 0, 1);
    this.maxChanges = totalLaps <= 6 ? 1 : totalLaps <= 15 ? 2 : 3;
    this.rand = rand;
  }

  get isWet(): boolean {
    return this.current === 'Damp' || this.current === 'LightRain' || this.current === 'HeavyRain';
  }

  /** Avalia possível mudança de clima ao completar uma volta (líder). */
  evaluateLap(leaderLap: number): void {
    if (leaderLap < 2) return;
    if (this.changes >= this.maxChanges) return;
    if (leaderLap - this.lastChangeLap < WeatherSystem.MIN_LAP_GAP) return;

    const p = clamp(this.rainChance * 0.55 + 0.1, 0, 1);
    if (this.rand() > p) return;

    const idx = WEATHER_LADDER.indexOf(this.current);
    let dir: number;
    if (idx <= 0) dir = +1;
    else if (idx >= 4) dir = -1;
    else dir = this.rand() < this.rainChance * 0.6 + 0.35 ? +1 : -1;

    const next = clamp(idx + dir, 0, 4);
    if (next === idx) return;

    this.current = WEATHER_LADDER[next]!;
    this.changes++;
    this.lastChangeLap = leaderLap;
    this.onChanged?.(this.current);
  }

  /** Clima inicial sorteado a partir da chance de chuva da pista. */
  static initialFor(rainChance: number, rand: () => number = Math.random): Weather {
    const r = rand();
    if (r < rainChance * 0.35) return 'Damp';
    if (r < rainChance * 0.8) return 'Cloudy';
    return 'Dry';
  }
}

// ---------------------------------------------------------------------
// Eventos de corrida (PRD 20): Safety Marble e Pista Suja.
// ---------------------------------------------------------------------
export type RaceEventKind = 'None' | 'SafetyMarble' | 'DirtyTrack';

export class RaceEventSystem {
  private timer: number;
  private rand: () => number;

  constructor(rand: () => number = Math.random) {
    this.rand = rand;
    this.timer = this.nextInterval();
  }

  tick(dt: number): RaceEventKind {
    this.timer -= dt;
    if (this.timer > 0) return 'None';
    this.timer = this.nextInterval();
    const r = this.rand();
    if (r < 0.40) return 'SafetyMarble';
    if (r < 0.80) return 'DirtyTrack';
    return 'None';
  }

  private nextInterval(): number { return 18 + this.rand() * (32 - 18); }
}

// ---------------------------------------------------------------------
// Resultado da corrida (Race/RaceResult.cs)
// ---------------------------------------------------------------------
export interface RaceResultEntry {
  position: number;
  marbleName: string;
  teamName: string;
  teamId: string;
  driverId: string;
  totalTime: number;
  pitStops: number;
  bestLapTime: number;
  overtakes: number;
  finalWear: number;
  finalEnergy: number;
  finalFuel: number;
  finalTyre: string;
  statusText: string;
  points: number;
  isPlayer: boolean;
  fastestLap: boolean;
}

export interface RaceResult {
  trackName: string;
  laps: number;
  entries: RaceResultEntry[];
}
