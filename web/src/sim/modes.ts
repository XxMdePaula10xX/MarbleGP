// =====================================================================
// Multiplicadores dos modos de corrida — port de Core/ModeTuning.cs
// =====================================================================

import type { RaceMode } from '../core/types';

export interface ModeTune {
  speed: number;        // multiplicador de velocidade
  wear: number;         // multiplicador de desgaste
  fuel: number;         // multiplicador de consumo de combustivel
  energyDelta: number;  // variacao de energia por volta (+regen / -dreno)
  errorMult: number;    // multiplicador da chance de erro
}

const TABLE: Record<RaceMode, ModeTune> = {
  Push:     { speed: 1.08, wear: 1.25, fuel: 1.35, energyDelta: -22, errorMult: 1.20 },
  Save:     { speed: 0.92, wear: 0.75, fuel: 0.78, energyDelta: 8,   errorMult: 0.80 },
  Attack:   { speed: 1.10, wear: 1.30, fuel: 1.40, energyDelta: -24, errorMult: 1.25 },
  Defend:   { speed: 0.96, wear: 0.95, fuel: 0.95, energyDelta: -6,  errorMult: 0.95 },
  Cooldown: { speed: 0.90, wear: 0.70, fuel: 0.80, energyDelta: 6,   errorMult: 0.80 },
  Normal:   { speed: 1.00, wear: 1.00, fuel: 1.00, energyDelta: -8,  errorMult: 1.00 },
};

export function modeTune(mode: RaceMode): ModeTune {
  return TABLE[mode];
}
