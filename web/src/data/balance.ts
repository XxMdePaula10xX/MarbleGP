// =====================================================================
// Constantes de balanceamento global — port de Core/GameBalance.cs
// (valores refletem o "Balanceamento Inicial" do PRD, secoes 28 e 41)
// =====================================================================

export const Balance = {
  // Velocidade base (PRD 28)
  baseSpeed: 10,

  // Energia (PRD 15/28)
  maxEnergy: 100,
  lowChargeThreshold: 20, // abaixo disso a IA economiza

  // Desgaste — limiares de efeito (PRD 14)
  wearWarnThreshold: 50,
  wearHeavyThreshold: 70,
  wearCriticalThreshold: 85,
  maxWearSpeedPenalty: 0.35, // penalidade max de velocidade com desgaste 100

  // Erros (PRD 13.6/41)
  baseErrorChance: 0.01,
  stuckRecoveryTime: 2.0,
  stuckSpeedThreshold: 0.4,

  // Pit stop (PRD 18.3)
  basePitTime: 3.0,
  tireChangeTime: 1.2,
  energyRefillTimePerUnit: 0.03,
  maxRandomPitError: 0.6,
  pitLaneSpeed: 6,

  // Pontuacao do campeonato (PRD 10)
  pointsByPosition: [25, 20, 18, 15, 12, 10, 8, 6, 4, 3, 2, 1],

  // IA / Direcao
  waypointArriveDistance: 1.5,
  cornerLookAhead: 2.5,
  overtakeDetectDistance: 3.0,
} as const;

/** Pontos para uma posicao (1-based). Fora da tabela => 0. */
export function pointsForPosition(position: number): number {
  return Balance.pointsByPosition[position - 1] ?? 0;
}
