// =====================================================================
// Estratégia de pit da IA — port de Race/AIStrategyManager.cs (PRD 6/11)
// =====================================================================

import type { GripType, Weather } from '../core/types';
import { gripIsDry, weatherIsWet } from '../core/types';
import type { MarbleActor, RaceConditions } from './controller';
import type { MarbleRuntime } from './runtime';
import type { FuelSystem } from './systems';

export class AIStrategyManager {
  private fuel: FuelSystem;
  private cond: RaceConditions;
  private totalLaps: number;

  constructor(fuel: FuelSystem, cond: RaceConditions, totalLaps: number) {
    this.fuel = fuel;
    this.cond = cond;
    this.totalLaps = totalLaps;
  }

  evaluate(actor: MarbleActor): void {
    const m = actor.m;
    if (m.isPlayer || m.pitRequested) return;
    if (m.state !== 'Racing') return;

    const lapsRemaining = this.totalLaps - m.completedLaps;
    if (lapsRemaining <= 1) return; // nunca parar na última volta
    const lap = m.completedLaps + 1;

    const w = this.cond.currentWeather;
    const wet = weatherIsWet(w);
    const slick = gripIsDry(m.grip.gripId);
    const wetTyre = m.grip.gripId === 'Rain' || m.grip.gripId === 'Intermediate';

    // --- 1) CRÍTICO: pit imediato, ignora janela/bloqueios ---
    const fuelCritical = m.fuel < Math.max(18, this.fuel.fuelForLaps(m, 1.5)) || m.fuel < 20;
    const heavyWrong = w === 'HeavyRain' && slick;
    const damage = m.coreFailTimer > 0;
    if (fuelCritical || damage || heavyWrong) {
      this.requestPit(m, lapsRemaining, wet);
      return;
    }

    // --- 2) BLOQUEIO: não para cedo demais ---
    if (m.completedLaps < 1) return;

    // --- 3) MOTIVO real para parar ---
    // Hard (aiPitQuality alto) reage um pouco antes ao desgaste/clima.
    const wearThresh = m.aiPitQuality >= 1.2 ? 72 : m.aiPitQuality <= 0.8 ? 82 : 75;
    const wrongTyre = (wet && slick) || (!wet && wetTyre);
    const reason = m.fuel < 40 || m.wear > wearThresh || wrongTyre || m.energy < 12;
    if (!reason) return;

    // --- 4) Apenas dentro da janela estratégica ---
    if (!this.inPitWindow(lap)) return;

    this.requestPit(m, lapsRemaining, wet);
  }

  private requestPit(m: MarbleRuntime, lapsRemaining: number, wet: boolean): void {
    m.pitTargetGrip = this.chooseTyre(m, lapsRemaining, wet);
    m.pitChangeTires = true;
    m.pitRefillAmount = 100;
    m.pitRequested = true;
  }

  /** Janela estratégica de pit por duração. */
  private inPitWindow(lap: number): boolean {
    const start = this.totalLaps <= 6 ? 2 : this.totalLaps <= 15 ? 4 : 6;
    return lap >= start && lap <= this.totalLaps - 1;
  }

  private chooseTyre(m: MarbleRuntime, lapsRemaining: number, wet: boolean): GripType {
    if (this.cond.currentWeather === ('HeavyRain' as Weather)) return 'Rain';
    if (wet) return 'Intermediate';

    let target: GripType = lapsRemaining <= 4 ? 'Soft' : lapsRemaining >= 10 ? 'Hard' : 'Medium';

    switch (m.driver.personality) {
      case 'Aggressive': target = lapsRemaining <= 6 ? 'Soft' : 'Medium'; break;
      case 'Conservative': target = lapsRemaining >= 8 ? 'Hard' : 'Medium'; break;
    }
    return target;
  }
}
