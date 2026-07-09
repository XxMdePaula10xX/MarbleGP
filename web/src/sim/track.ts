// =====================================================================
// Geometria da pista — port de Track/{TrackBuilder,TrackManager,Lane}.cs
// Spline Catmull-Rom fechada sobre os control points, linhas de corrida
// (ideal/interna/externa), checkpoints, grid de largada e pit lane real.
// Tudo em 2D (plano XZ do Unity vira XY do canvas).
// =====================================================================

import type { TrackData, Vec2 } from '../core/types';

export type RacingLine = 'Ideal' | 'Inside' | 'Outside' | 'Pit';

const SAMPLES_PER_SEGMENT = 10;

function catmullRom(p0: Vec2, p1: Vec2, p2: Vec2, p3: Vec2, t: number): Vec2 {
  const t2 = t * t, t3 = t2 * t;
  return {
    x: 0.5 * (2 * p1.x + (-p0.x + p2.x) * t
      + (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2
      + (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3),
    y: 0.5 * (2 * p1.y + (-p0.y + p2.y) * t
      + (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2
      + (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3),
  };
}

function sampleClosedSpline(cps: Vec2[], perSeg: number): Vec2[] {
  const n = cps.length;
  const pts: Vec2[] = [];
  for (let i = 0; i < n; i++) {
    const p0 = cps[(i - 1 + n) % n]!;
    const p1 = cps[i]!;
    const p2 = cps[(i + 1) % n]!;
    const p3 = cps[(i + 2) % n]!;
    for (let s = 0; s < perSeg; s++) {
      pts.push(catmullRom(p0, p1, p2, p3, s / perSeg));
    }
  }
  return pts;
}

/**
 * Normais laterais. Unity: tangent=(next-prev).normalized no plano XZ,
 * normal=(tangent.z, -tangent.x). Em 2D: normal=(ty, -tx).
 * Caminho ABERTO (pit lane) usa diferenças de um lado só nas pontas.
 */
export function computeNormals(pts: Vec2[], closed = true): Vec2[] {
  const n = pts.length;
  const normals: Vec2[] = new Array(n);
  for (let i = 0; i < n; i++) {
    const prev = closed ? pts[(i - 1 + n) % n]! : pts[Math.max(0, i - 1)]!;
    const next = closed ? pts[(i + 1) % n]! : pts[Math.min(n - 1, i + 1)]!;
    let tx = next.x - prev.x, ty = next.y - prev.y;
    const len = Math.hypot(tx, ty) || 1;
    tx /= len; ty /= len;
    normals[i] = { x: ty, y: -tx };
  }
  return normals;
}

export function offsetLine(pts: Vec2[], normals: Vec2[], dist: number): Vec2[] {
  return pts.map((p, i) => ({ x: p.x + normals[i]!.x * dist, y: p.y + normals[i]!.y * dist }));
}

// ---------------------------------------------------------------------
// Lane: loop de pontos com distâncias cumulativas (projeção por arco).
// ---------------------------------------------------------------------
export class Lane {
  readonly points: Vec2[];
  readonly cumDist: number[];
  readonly totalLength: number;

  get count(): number { return this.points.length; }

  constructor(points: Vec2[]) {
    this.points = points;
    this.cumDist = new Array(points.length);
    let acc = 0;
    for (let i = 0; i < points.length; i++) {
      this.cumDist[i] = acc;
      const next = points[(i + 1) % points.length]!;
      acc += Math.hypot(next.x - points[i]!.x, next.y - points[i]!.y);
    }
    this.totalLength = acc;
  }

  point(index: number): Vec2 {
    const n = this.count;
    return this.points[((index % n) + n) % n]!;
  }

  /** Projeta uma posição no loop; retorna fração 0..1 do arco e o índice mais próximo. */
  closestArcFraction(pos: Vec2): { arc: number; nearestIndex: number } {
    let bestSqr = Number.MAX_VALUE, bestArc = 0, nearestIndex = 0;
    for (let i = 0; i < this.count; i++) {
      const a = this.points[i]!;
      const b = this.point(i + 1);
      const abx = b.x - a.x, aby = b.y - a.y;
      const len2 = abx * abx + aby * aby;
      const t = len2 > 1e-6
        ? Math.min(1, Math.max(0, ((pos.x - a.x) * abx + (pos.y - a.y) * aby) / len2))
        : 0;
      const px = a.x + abx * t, py = a.y + aby * t;
      const dx = pos.x - px, dy = pos.y - py;
      const d = dx * dx + dy * dy;
      if (d < bestSqr) {
        bestSqr = d;
        bestArc = this.cumDist[i]! + Math.sqrt(len2) * t;
        nearestIndex = i;
      }
    }
    return { arc: this.totalLength > 0 ? bestArc / this.totalLength : 0, nearestIndex };
  }

  /**
   * Projeção restrita a uma janela de arco em torno de aroundArc (0..1) —
   * impede a projeção de saltar para um trecho paralelo em pistas em "S".
   */
  closestArcFractionWindowed(pos: Vec2, aroundArc: number, window: number): { arc: number; nearestIndex: number } {
    const total = this.totalLength > 0 ? this.totalLength : 1;
    let bestSqr = Number.MAX_VALUE, bestArc = 0, nearestIndex = -1;

    for (let i = 0; i < this.count; i++) {
      const segArc = this.cumDist[i]! / total;
      let dArc = (segArc - aroundArc + 0.5) % 1;
      if (dArc < 0) dArc += 1;
      dArc = Math.abs(dArc - 0.5);
      if (dArc > window) continue;

      const a = this.points[i]!;
      const b = this.point(i + 1);
      const abx = b.x - a.x, aby = b.y - a.y;
      const len2 = abx * abx + aby * aby;
      const t = len2 > 1e-6
        ? Math.min(1, Math.max(0, ((pos.x - a.x) * abx + (pos.y - a.y) * aby) / len2))
        : 0;
      const px = a.x + abx * t, py = a.y + aby * t;
      const dx = pos.x - px, dy = pos.y - py;
      const d = dx * dx + dy * dy;
      if (d < bestSqr) {
        bestSqr = d;
        bestArc = this.cumDist[i]! + Math.sqrt(len2) * t;
        nearestIndex = i;
      }
    }

    if (nearestIndex < 0) return this.closestArcFraction(pos); // fallback total
    return { arc: bestArc / total, nearestIndex };
  }
}

// ---------------------------------------------------------------------
// Track: geometria completa + consultas de navegação (TrackManager).
// ---------------------------------------------------------------------
export class Track {
  readonly data: TrackData;
  readonly center: Vec2[];
  readonly normals: Vec2[];
  readonly halfWidth: number;

  readonly idealLine: Lane;
  readonly insideLine: Lane;
  readonly outsideLine: Lane;

  readonly checkpoints: Vec2[] = [];
  readonly gridPositions: Vec2[] = [];

  /** Caminho FINITO do pit lane: entrada -> boxes -> saída. */
  readonly pitPath: Vec2[] = [];
  /** Índice em pitPath onde cada box manda a bolinha parar. */
  readonly pitBoxPathIndex: number[] = [];

  constructor(data: TrackData, marbleCount: number) {
    this.data = data;
    this.center = sampleClosedSpline(data.controlPoints, SAMPLES_PER_SEGMENT);
    this.normals = computeNormals(this.center);
    this.halfWidth = data.trackWidth * 0.5;

    const laneOffset = data.trackWidth * 0.28;
    this.idealLine = new Lane(this.center);
    this.insideLine = new Lane(offsetLine(this.center, this.normals, -laneOffset));
    this.outsideLine = new Lane(offsetLine(this.center, this.normals, +laneOffset));

    // Checkpoints: a cada checkpointEvery amostras do centro.
    const every = Math.max(2, data.checkpointEvery);
    for (let i = 0; i < this.center.length; i += every) this.checkpoints.push(this.center[i]!);

    this.buildGrid(marbleCount);
    this.buildPitLane(marbleCount);
  }

  get checkpointCount(): number { return this.checkpoints.length; }

  private buildGrid(marbleCount: number): void {
    const n = this.center.length;
    const spacingPts = 2.5;
    for (let i = 0; i < marbleCount; i++) {
      let idx = (n - 3 - Math.round(i * spacingPts)) % n;
      idx = (idx + n) % n;
      const side = i % 2 === 0 ? -1 : 1;
      const c = this.center[idx]!, nm = this.normals[idx]!;
      this.gridPositions.push({ x: c.x + nm.x * side * 1.4, y: c.y + nm.y * side * 1.4 });
    }
  }

  private buildPitLane(marbleCount: number): void {
    const n = this.center.length;
    const pitLen = Math.min(Math.max(Math.round(n * 0.24), 14), Math.floor(n / 2));
    const maxOffset = this.halfWidth + 3.2;
    const side = 1; // lado externo

    const smoothstep = (t: number) => t * t * (3 - 2 * t);
    for (let k = 0; k <= pitLen; k++) {
      const idx = k % n;
      const t = k / pitLen;
      const ramp = t < 0.18 ? smoothstep(t / 0.18)
        : t > 0.82 ? smoothstep((1 - t) / 0.18)
        : 1;
      const c = this.center[idx]!, nm = this.normals[idx]!;
      this.pitPath.push({ x: c.x + nm.x * side * maxOffset * ramp, y: c.y + nm.y * side * maxOffset * ramp });
    }

    for (let i = 0; i < marbleCount; i++) {
      const bt = marbleCount > 1 ? 0.34 + (i / (marbleCount - 1)) * 0.32 : 0.5;
      const bk = Math.min(Math.max(Math.round(bt * pitLen), 2), pitLen - 2);
      this.pitBoxPathIndex.push(bk);
    }
  }

  getLane(line: RacingLine): Lane {
    switch (line) {
      case 'Inside': return this.insideLine;
      case 'Outside': return this.outsideLine;
      default: return this.idealLine;
    }
  }

  /** Ponto-alvo de direção numa linha, com lookahead em pontos. */
  getSteerTarget(line: RacingLine, fromPos: Vec2, lookahead: number): Vec2 {
    const lane = this.getLane(line);
    const { nearestIndex } = lane.closestArcFraction(fromPos);
    return lane.point(nearestIndex + Math.max(1, lookahead));
  }

  /** Curvatura aproximada à frente (0 reto .. ~1 curva forte). */
  curvatureAhead(fromPos: Vec2, lookahead: number): number {
    const { nearestIndex } = this.idealLine.closestArcFraction(fromPos);
    const p0 = this.idealLine.point(nearestIndex);
    const p1 = this.idealLine.point(nearestIndex + lookahead);
    const p2 = this.idealLine.point(nearestIndex + lookahead * 2);
    let ax = p1.x - p0.x, ay = p1.y - p0.y;
    let bx = p2.x - p1.x, by = p2.y - p1.y;
    const la = Math.hypot(ax, ay) || 1, lb = Math.hypot(bx, by) || 1;
    ax /= la; ay /= la; bx /= lb; by /= lb;
    const dot = ax * bx + ay * by;
    return Math.min(1, Math.max(0, 1 - dot));
  }

  arcFraction(pos: Vec2): number {
    return this.idealLine.closestArcFraction(pos).arc;
  }

  arcFractionWindowed(pos: Vec2, aroundArc: number, window: number): number {
    return this.idealLine.closestArcFractionWindowed(pos, aroundArc, window).arc;
  }
}
