// =====================================================================
// Os 15 circuitos — port de Editor/DataGenerator.cs (CreateTracks)
// Geometria: control points no plano XZ; Loop() gera curva radial
// r(θ)=1+amp·sin(lobes·θ+phase) escalada por (rx,ry) e girada por rot —
// curva "estrela" que nunca se auto-intersecta.
// =====================================================================

import type { Difficulty, TrackData, Vec2 } from '../core/types';

function loop(n: number, rx: number, ry: number, amp: number, lobes: number, phase: number, rot: number): Vec2[] {
  const pts: Vec2[] = [];
  for (let i = 0; i < n; i++) {
    const t = (i / n) * Math.PI * 2;
    const r = 1 + amp * Math.sin(lobes * t + phase);
    const a = t + rot;
    pts.push({ x: Math.cos(a) * rx * r, y: Math.sin(a) * ry * r });
  }
  return pts;
}

const OVAL: Vec2[] = [
  { x: -30, y: -18 }, { x: 0, y: -22 }, { x: 30, y: -18 }, { x: 38, y: 0 },
  { x: 30, y: 18 }, { x: 0, y: 22 }, { x: -30, y: 18 }, { x: -38, y: 0 },
];

const HARBOR: Vec2[] = [
  { x: -45, y: -20 }, { x: 0, y: -24 }, { x: 45, y: -20 }, { x: 48, y: -5 },
  { x: 20, y: -5 }, { x: 20, y: 15 }, { x: 48, y: 15 }, { x: 45, y: 28 },
  { x: 0, y: 30 }, { x: -45, y: 28 }, { x: -48, y: 5 }, { x: -48, y: -8 },
];

const SPIRAL: Vec2[] = [
  { x: -32, y: -20 }, { x: -5, y: -26 }, { x: 25, y: -22 }, { x: 36, y: -4 },
  { x: 14, y: 2 }, { x: -2, y: -8 }, { x: -16, y: 4 }, { x: 0, y: 16 },
  { x: 24, y: 18 }, { x: 34, y: 30 }, { x: 0, y: 34 }, { x: -34, y: 26 }, { x: -40, y: 2 },
];

function perimeter(pts: Vec2[]): number {
  let total = 0;
  for (let i = 0; i < pts.length; i++) {
    const a = pts[i]!, b = pts[(i + 1) % pts.length]!;
    total += Math.hypot(b.x - a.x, b.y - a.y);
  }
  return total;
}

function track(
  trackId: string, trackName: string, difficulty: Difficulty, laps: number,
  abrasion: number, overtake: number, rainChance: number, width: number,
  description: string, controlPoints: Vec2[],
): TrackData {
  return {
    trackId, trackName, difficulty,
    recommendedLaps: laps, abrasionLevel: abrasion, overtakeLevel: overtake,
    rainChance, description, controlPoints,
    trackWidth: width, checkpointEvery: 4,
    trackLength: perimeter(controlPoints),
  };
}

export const TRACKS: TrackData[] = [
  track('marble_park', 'Marble Park', 'Easy', 5, 1.0, 0.5, 0.10, 8,
    'Circuito inicial, equilibrado.', OVAL),
  track('neon_harbor', 'Neon Harbor', 'Medium', 6, 1.1, 0.8, 0.35, 8,
    'Urbano costeiro, retas longas e curvas de 90 graus.', HARBOR),
  track('spiral_canyon', 'Spiral Canyon', 'Hard', 5, 1.4, 0.4, 0.25, 7,
    'Muitas curvas, desgaste alto.', SPIRAL),

  // Campeonato completo — 12 circuitos com geometria própria.
  track('sakura_speedway', 'Sakura Speedway', 'Medium', 6, 1.05, 0.70, 0.25, 8,
    'Curvas fluidas sob as cerejeiras; ritmo constante e médio desgaste.',
    loop(14, 40, 26, 0.14, 3, 0.40, 0.10)),
  track('desert_loop', 'Desert Loop', 'Easy', 6, 1.30, 0.85, 0.05, 8,
    'Retas longuíssimas no deserto; vácuo e ultrapassagens fáceis, asfalto abrasivo.',
    loop(14, 50, 20, 0.0, 0, 0, 0)),
  track('ice_bowl', 'Ice Bowl', 'Medium', 5, 0.70, 0.55, 0.40, 9,
    'Tigela de gelo larga e escorregadia; baixo desgaste, mas pouca aderência.',
    loop(14, 34, 32, 0.16, 5, 0, 0)),
  track('volcano_ring', 'Volcano Ring', 'Hard', 5, 1.50, 0.45, 0.15, 7,
    'Anel vulcânico estreito e quente; desgaste altíssimo, ultrapassar é difícil.',
    loop(14, 30, 30, 0.22, 2, 1.57, 0)),
  track('rainforest_gp', 'Rainforest GP', 'Hard', 5, 1.20, 0.50, 0.60, 7,
    'Traçado sinuoso e muito úmido; o pneu de chuva costuma decidir a corrida.',
    loop(14, 42, 22, 0.28, 2, 0, 0)),
  track('metro_marble', 'Metro Marble Circuit', 'Medium', 6, 1.10, 0.60, 0.30, 7,
    'Circuito urbano de cantos retos; muros próximos exigem precisão.',
    loop(14, 34, 32, 0.16, 4, 0, 0.785)),
  track('royal_garden', 'Royal Garden', 'Medium', 6, 1.00, 0.55, 0.25, 7,
    'Jardim real técnico e elegante; três grandes setores de curvas.',
    loop(14, 32, 30, 0.20, 3, 1.57, 0)),
  track('skybridge', 'Skybridge Circuit', 'Medium', 6, 1.00, 0.85, 0.20, 8,
    'Pontes suspensas rápidas; curvas amplas e muita ultrapassagem.',
    loop(14, 46, 22, 0.10, 4, 0.30, 0)),
  track('factory_run', 'Factory Run', 'Hard', 5, 1.30, 0.50, 0.20, 7,
    'Linha de montagem apertada; muitas curvas e desgaste elevado.',
    loop(14, 36, 28, 0.18, 6, 0, 0)),
  track('moonbase_gp', 'Moonbase GP', 'Medium', 6, 0.90, 0.70, 0.0, 8,
    'Base lunar de baixa gravidade; pista limpa, ampla e sem chuva.',
    loop(14, 38, 36, 0.0, 0, 0, 0)),
  track('atlantis_drift', 'Atlantis Drift', 'Hard', 5, 1.10, 0.60, 0.50, 8,
    'Cidade submersa ondulante; aderência variável e clima instável.',
    loop(14, 42, 24, 0.13, 5, 0.60, 0)),
  track('final_orbit', 'Final Orbit', 'Hard', 7, 1.40, 0.75, 0.30, 8,
    'Palco final do campeonato; longa, veloz e impiedosa com os pneus.',
    loop(14, 46, 28, 0.16, 3, 0.30, 0.20)),
];

export function trackById(id: string): TrackData {
  const t = TRACKS.find(t => t.trackId === id);
  if (!t) throw new Error(`Circuito desconhecido: ${id}`);
  return t;
}
