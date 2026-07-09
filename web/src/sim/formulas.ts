// =====================================================================
// Fórmulas centrais do jogo — port de Systems/RaceFormulas.cs (PRD 41)
// Funções puras para facilitar teste e balanceamento.
// =====================================================================

import type { Weather } from '../core/types';
import { gripPerformanceForWeather, gripIsDry, driverMult, weatherIsWet } from '../core/types';
import { Balance } from '../data/balance';
import { modeTune } from './modes';
import type { MarbleRuntime } from './runtime';

function clamp01(v: number): number { return v < 0 ? 0 : v > 1 ? 1 : v; }

function inverseLerp(a: number, b: number, v: number): number {
  return clamp01((v - a) / (b - a));
}

/**
 * Velocidade alvo final (PRD 41):
 * FinalSpeed = BaseSpeed * DriverSpeed * Grip * Surface * Mode * Wear * Energy * Fuel * TrackCond
 */
export function finalSpeed(m: MarbleRuntime, weather: Weather, trackCond = 1): number {
  const base = Balance.baseSpeed;
  const driver = driverMult.speed(m.driver);
  const grip = m.grip.speedMultiplier * gripPerformanceForWeather(m.grip, weather);
  const surface = m.surface.speedModifier;
  const mode = modeTune(m.mode).speed;
  const wear = wearSpeedPenalty(m.wear);
  const energy = energySpeedPenalty(m.energy);
  const fuel = fuelSpeedPenalty(m.fuel);

  return base * driver * grip * surface * mode * wear * energy * fuel * trackCond
    * m.upgSpeedFactor * m.aiSpeedMult;
}

/** Penalidade multiplicativa de velocidade por desgaste (PRD 14/28). */
export function wearSpeedPenalty(wear: number): number {
  if (wear <= Balance.wearWarnThreshold) return 1;
  const t = inverseLerp(Balance.wearWarnThreshold, 100, wear);
  return 1 - t * Balance.maxWearSpeedPenalty;
}

/** Penalidade de velocidade por energia baixa (PRD 4.2). */
export function energySpeedPenalty(energy: number): number {
  if (energy > 60) return 1;
  if (energy >= 30) return 0.98;
  if (energy >= 10) return 0.95;
  return 0.90;
}

/** Combustível vazio => modo emergência 35% (PRD 4.1). */
export function fuelSpeedPenalty(fuel: number): number {
  return fuel <= 0 ? 0.35 : 1;
}

/** Consumo de combustível por volta (PRD 4.1/5.1). */
export function fuelUsePerLap(m: MarbleRuntime, baseFuelPerLap: number): number {
  const mode = modeTune(m.mode).fuel;
  const tyre = m.grip.fuelMultiplier;
  return baseFuelPerLap * mode * tyre;
}

/** Variação de energia por volta (+ regenera / − dreno) (PRD 4.2). */
export function energyChangePerLap(m: MarbleRuntime): number {
  let delta = modeTune(m.mode).energyDelta;
  if (delta < 0) {
    const tyre = m.grip.energyMultiplier;
    const driverMgmt = 1 / Math.max(0.01, driverMult.energyMgmt(m.driver));
    delta *= tyre * driverMgmt * m.upgEnergyFactor;
  }
  return delta;
}

/** Desgaste base (%/volta) escalado pela duração da corrida (PRD 13). */
export function baseWearPerLap(totalLaps: number): number {
  return totalLaps <= 6 ? 18 : totalLaps <= 15 ? 8 : 5;
}

/**
 * Ganho de desgaste por volta (PRD 13/14):
 * baseWear(duração) * pneu * modo * superfície * abrasão * clima * piloto * upgrades.
 */
export function wearGainPerLap(
  m: MarbleRuntime, trackAbrasion: number, weather: Weather, totalLaps: number,
): number {
  const base = baseWearPerLap(totalLaps);
  const tyre = m.grip.wearMultiplier;
  const mode = modeTune(m.mode).wear;
  const surface = m.surface.wearModifier;

  const wet = weatherIsWet(weather);
  const weatherWear = wet && gripIsDry(m.grip.gripId) ? 1.2
    : !wet && m.grip.gripId === 'Rain' ? 1.5 : 1;

  // Bom tireManagement REDUZ desgaste => divide pelo multiplicador.
  const driverMgmt = 1 / Math.max(0.01, driverMult.tireMgmt(m.driver));
  return base * tyre * mode * surface * trackAbrasion * weatherWear * driverMgmt * m.upgWearFactor;
}

/** Chance de erro por avaliação (PRD 41), em 0..1. */
export function errorChance(m: MarbleRuntime, weather: Weather, trackErrorAdd = 0): number {
  let wearPenalty = 0;
  if (m.wear > Balance.wearCriticalThreshold) wearPenalty = 0.08;
  else if (m.wear > Balance.wearHeavyThreshold) wearPenalty = 0.04;

  const aggressionPenalty = (m.driver.aggression / 100) * 0.03;

  const wet = weatherIsWet(weather);
  const weatherPenalty = weather === 'HeavyRain' ? 0.06
    : weather === 'LightRain' ? 0.03
    : weather === 'Damp' ? 0.015 : 0;

  const slick = gripIsDry(m.grip.gripId);
  const wrongTyrePenalty = wet && slick ? 0.06 : 0;
  const wetSkillBonus = wet ? (m.driver.wetSkill / 100) * 0.05 : 0;

  const controlBonus = (m.driver.control / 100) * 0.04;
  const consistencyBonus = (m.driver.consistency / 100) * 0.04;

  let chance = Balance.baseErrorChance + wearPenalty + aggressionPenalty
    + weatherPenalty + wrongTyrePenalty + trackErrorAdd
    - controlBonus - consistencyBonus - wetSkillBonus;

  chance *= modeTune(m.mode).errorMult;

  // Personalidade (PRD 12).
  if (m.driver.personality === 'Veteran') chance *= 0.7;
  if (m.driver.personality === 'Rookie') chance *= 1.3;
  if (m.driver.personality === 'RiskTaker') chance *= 1.2;
  if (m.driver.personality === 'Smooth') chance *= 0.85;

  chance *= m.upgErrorFactor; // upgrades StrategyCenter/AICoaching
  chance *= m.aiErrorMult;    // dificuldade da IA
  return clamp01(chance);
}

/** Tempo total de pit em segundos (PRD 18.3/41). */
export function pitTime(
  m: MarbleRuntime, changeTires: boolean, energyRefilled: number, rand: () => number = Math.random,
): number {
  const tireTime = changeTires ? Balance.tireChangeTime : 0;
  const refillTime = energyRefilled * Balance.energyRefillTimePerUnit;
  const teamBonus = (m.team.pitCrewRating / 100) * 1.0;
  const driverBonus = (m.driver.pitSkill / 100) * 0.5;
  const randomError = rand() * Balance.maxRandomPitError;

  const time = Balance.basePitTime + tireTime + refillTime + randomError
    - teamBonus - driverBonus - m.upgPitReduction;
  return Math.max(1.0, time);
}
