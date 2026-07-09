// =====================================================================
// As 20 bolinhas do grid — port de Editor/DataGenerator.cs (CreateDrivers)
// Ordem dos stats: spd, acc, ctrl, agg, def, cons, tire, energy, pit, wet
// =====================================================================

import type { MarbleDriver, Personality } from '../core/types';

function d(
  driverId: string, marbleName: string, shortCode: string, number: number, teamId: string,
  spd: number, acc: number, ctrl: number, agg: number, def: number,
  cons: number, tire: number, energy: number, pit: number, wet: number,
  personality: Personality,
): MarbleDriver {
  return {
    driverId, marbleName, shortCode, number, teamId,
    speed: spd, acceleration: acc, control: ctrl, aggression: agg, defense: def,
    consistency: cons, tireManagement: tire, energyManagement: energy,
    pitSkill: pit, wetSkill: wet, personality,
  };
}

export const DRIVERS: MarbleDriver[] = [
  // Red Comet: velocidade alta, controle/tire menor.
  d('comet_one', 'Comet One', 'CM1', 1, 'red_comet', 88, 82, 60, 78, 60, 62, 45, 55, 55, 50, 'Aggressive'),
  d('comet_two', 'Comet Two', 'CM2', 2, 'red_comet', 84, 80, 58, 70, 58, 60, 48, 55, 55, 50, 'RiskTaker'),
  // Blue Orbit: consistente.
  d('orbit_one', 'Orbit One', 'OB1', 3, 'blue_orbit', 72, 70, 75, 55, 70, 85, 70, 72, 65, 68, 'Balanced'),
  d('orbit_two', 'Orbit Two', 'OB2', 4, 'blue_orbit', 70, 68, 74, 52, 72, 82, 72, 70, 64, 66, 'Veteran'),
  // Emerald Rollers: curvas/controle.
  d('emerald_one', 'Emerald One', 'EM1', 5, 'emerald_rollers', 70, 68, 88, 58, 66, 70, 68, 64, 60, 72, 'Smooth'),
  d('emerald_two', 'Emerald Two', 'EM2', 6, 'emerald_rollers', 68, 66, 85, 55, 64, 72, 70, 62, 60, 74, 'Conservative'),
  // Shadow Marble: pit/estrategia.
  d('shadow_one', 'Shadow One', 'SH1', 7, 'shadow_marble', 74, 66, 72, 60, 74, 75, 72, 70, 90, 65, 'Defensive'),
  d('shadow_two', 'Shadow Two', 'SH2', 8, 'shadow_marble', 72, 64, 70, 58, 72, 76, 74, 72, 88, 64, 'Balanced'),
  // Solar Spin: aceleracao.
  d('solar_one', 'Solar One', 'SL1', 9, 'solar_spin', 78, 86, 64, 66, 60, 64, 55, 58, 58, 55, 'Aggressive'),
  d('solar_two', 'Solar Two', 'SL2', 10, 'solar_spin', 76, 84, 62, 64, 60, 66, 56, 60, 58, 55, 'RiskTaker'),
  // Frostline: chuva.
  d('frost_one', 'Frost One', 'FR1', 11, 'frostline', 70, 68, 78, 54, 70, 78, 70, 70, 64, 88, 'Smooth'),
  d('frost_two', 'Frost Two', 'FR2', 12, 'frostline', 68, 66, 76, 52, 72, 80, 72, 70, 64, 85, 'Conservative'),
  // Iron Sphere: defesa/resistencia.
  d('iron_one', 'Iron One', 'IR1', 13, 'iron_sphere', 66, 64, 72, 56, 82, 78, 80, 74, 62, 60, 'Defensive'),
  d('iron_two', 'Iron Two', 'IR2', 14, 'iron_sphere', 64, 62, 70, 54, 84, 80, 82, 76, 62, 60, 'Veteran'),
  // Neon Pulse: agressao/ultrapassagem.
  d('neon_one', 'Neon One', 'NP1', 15, 'neon_pulse', 80, 78, 66, 84, 58, 56, 52, 56, 58, 58, 'RiskTaker'),
  d('neon_two', 'Neon Two', 'NP2', 16, 'neon_pulse', 78, 76, 64, 86, 56, 54, 50, 56, 58, 58, 'Aggressive'),
  // Jungle Curve: curvas lentas.
  d('jungle_one', 'Jungle One', 'JG1', 17, 'jungle_curve', 68, 66, 86, 56, 66, 72, 70, 64, 60, 70, 'Smooth'),
  d('jungle_two', 'Jungle Two', 'JG2', 18, 'jungle_curve', 66, 64, 84, 54, 66, 74, 72, 64, 60, 70, 'Conservative'),
  // Royal Club: completo.
  d('royal_one', 'Royal One', 'RY1', 19, 'royal_club', 80, 76, 78, 64, 70, 80, 70, 72, 70, 68, 'Veteran'),
  d('royal_two', 'Royal Two', 'RY2', 20, 'royal_club', 78, 74, 76, 70, 66, 72, 66, 70, 70, 66, 'Balanced'),
];

export function driverById(id: string): MarbleDriver {
  const dr = DRIVERS.find(x => x.driverId === id);
  if (!dr) throw new Error(`Piloto desconhecido: ${id}`);
  return dr;
}

export function driversOfTeam(teamId: string): MarbleDriver[] {
  return DRIVERS.filter(x => x.teamId === teamId);
}
