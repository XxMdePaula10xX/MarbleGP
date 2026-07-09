// =====================================================================
// Renderer Canvas 2D da corrida. Ambiente (grama/arquibancada/árvores)
// pré-renderizado num canvas offscreen — desenhado 1x, copiado por frame
// (a otimização de performance para mobile). Pista/zebras/pit vetoriais
// via Path2D pré-computados. Câmera com zoom/pan/follow.
// =====================================================================

import type { Vec2, Weather } from '../core/types';
import { offsetLine } from '../sim/track';
import type { Track } from '../sim/track';
import type { MarbleActor } from '../sim/controller';

export interface CameraState {
  x: number; y: number; z: number;    // atuais
  tx: number; ty: number; tz: number; // alvos (suavizados)
  follow: boolean;
}

interface KerbSeg { path: Path2D; color: string; }

/** RNG determinístico por pista (decoração idêntica a cada visita). */
function seeded(seedStr: string): () => number {
  let a = 0;
  for (let i = 0; i < seedStr.length; i++) a = (Math.imul(a, 31) + seedStr.charCodeAt(i)) | 0;
  a = a >>> 0;
  return () => {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const BG_MARGIN = 26; // unidades de mundo em volta da pista

/** Converte hex (#rgb/#rrggbb) para [r,g,b]. */
function toRgb(hex: string): [number, number, number] {
  const h = hex.replace('#', '');
  const n = parseInt(h.length === 3 ? h.split('').map(x => x + x).join('') : h, 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}
function lighten(hex: string, k: number): string {
  const [r, g, b] = toRgb(hex);
  return `rgb(${Math.round(r + (255 - r) * k)},${Math.round(g + (255 - g) * k)},${Math.round(b + (255 - b) * k)})`;
}
function darken(hex: string, k: number): string {
  const [r, g, b] = toRgb(hex);
  return `rgb(${Math.round(r * (1 - k))},${Math.round(g * (1 - k))},${Math.round(b * (1 - k))})`;
}
function roundRectPath(c: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number): void {
  c.beginPath();
  c.moveTo(x + r, y);
  c.arcTo(x + w, y, x + w, y + h, r);
  c.arcTo(x + w, y + h, x, y + h, r);
  c.arcTo(x, y + h, x, y, r);
  c.arcTo(x, y, x + w, y, r);
  c.closePath();
}

export class RaceRenderer {
  readonly cam: CameraState = { x: 0, y: 0, z: 1, tx: 0, ty: 0, tz: 1, follow: false };

  private canvas: HTMLCanvasElement;
  private ctx: CanvasRenderingContext2D;
  private track: Track;
  private dpr: number;

  // Bounds do mundo e escala base (pista inteira visível em z=1).
  private minX = 0; private minY = 0; private maxX = 0; private maxY = 0;
  private baseScale = 1;

  // Camadas pré-computadas.
  private bg: HTMLCanvasElement | null = null;
  private bgScale = 8; // px por unidade de mundo no offscreen
  private asphalt!: Path2D;
  private asphaltInner!: Path2D;
  private edgeL!: Path2D;
  private edgeR!: Path2D;
  private kerbs: KerbSeg[] = [];
  private pitRibbon: Path2D | null = null;
  private pitEdge: Path2D | null = null;
  private pitAwnings: Array<{ x: number; y: number; a: number }> = [];
  private checkSquares: Array<{ x: number; y: number; s: number; dark: boolean; a: number }> = [];
  private gridSlots: Array<{ x: number; y: number; a: number }> = [];
  private startPos: Vec2;
  private W = 0; private H = 0;
  private vignette: CanvasGradient | null = null;
  // Gradientes cacheados (centrados na origem; usados com translate por bolinha
  // → zero alocação por frame). AO é único; corpo é por cor de equipe.
  private aoGrad: CanvasGradient | null = null;
  private leaderGlow: CanvasGradient | null = null;
  private bodyGrads = new Map<string, CanvasGradient>();

  // Partículas de faísca (contato/batida). Espaço de mundo.
  private particles: Array<{ x: number; y: number; vx: number; vy: number; life: number; max: number }> = [];

  constructor(canvas: HTMLCanvasElement, track: Track) {
    this.canvas = canvas;
    this.track = track;
    this.dpr = Math.min(window.devicePixelRatio || 1, 2); // cap DPR=2 (perf mobile)
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Canvas 2D indisponível');
    this.ctx = ctx;

    this.startPos = track.center[0]!;
    this.computeBounds();
    this.buildPaths();
    this.resize();
  }

  resize(): void {
    const rect = this.canvas.getBoundingClientRect();
    this.W = Math.max(64, Math.round(rect.width * this.dpr));
    this.H = Math.max(64, Math.round(rect.height * this.dpr));
    this.canvas.width = this.W;
    this.canvas.height = this.H;
    this.vignette = null; // depende de W/H

    const spanX = this.maxX - this.minX, spanY = this.maxY - this.minY;
    this.baseScale = Math.min(this.W / spanX, this.H / spanY) * 0.92;
    if (!this.bg) this.bakeBackground();
  }

  /** Escala total atual (px de canvas por unidade de mundo). */
  get scale(): number { return this.baseScale * this.cam.z; }

  worldToScreen(wx: number, wy: number): Vec2 {
    const s = this.scale;
    return {
      x: this.W / 2 + (wx - this.cam.x) * s,
      y: this.H / 2 + (wy - this.cam.y) * s,
    };
  }

  screenToWorld(sx: number, sy: number): Vec2 {
    const s = this.scale;
    return {
      x: this.cam.x + (sx * this.dpr - this.W / 2) / s,
      y: this.cam.y + (sy * this.dpr - this.H / 2) / s,
    };
  }

  get devicePixelRatioUsed(): number { return this.dpr; }

  // ------------------------------------------------------------------

  private computeBounds(): void {
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    const consider = (p: Vec2) => {
      if (p.x < minX) minX = p.x;
      if (p.y < minY) minY = p.y;
      if (p.x > maxX) maxX = p.x;
      if (p.y > maxY) maxY = p.y;
    };
    for (const p of this.track.center) consider(p);
    for (const p of this.track.pitPath) consider(p);
    const pad = this.track.halfWidth + 6;
    this.minX = minX - pad; this.minY = minY - pad;
    this.maxX = maxX + pad; this.maxY = maxY + pad;
    this.cam.x = this.cam.tx = (this.minX + this.maxX) / 2;
    this.cam.y = this.cam.ty = (this.minY + this.maxY) / 2;
  }

  private ribbon(line: Vec2[], normals: Vec2[], half: number, closed: boolean): Path2D {
    const left = offsetLine(line, normals, -half);
    const right = offsetLine(line, normals, +half);
    const p = new Path2D();
    p.moveTo(left[0]!.x, left[0]!.y);
    for (let i = 1; i < left.length; i++) p.lineTo(left[i]!.x, left[i]!.y);
    if (closed) p.lineTo(left[0]!.x, left[0]!.y);
    for (let i = right.length - 1; i >= 0; i--) p.lineTo(right[i]!.x, right[i]!.y);
    if (closed) p.lineTo(right[right.length - 1]!.x, right[right.length - 1]!.y);
    p.closePath();
    return p;
  }

  private polyline(line: Vec2[], closed: boolean): Path2D {
    const p = new Path2D();
    p.moveTo(line[0]!.x, line[0]!.y);
    for (let i = 1; i < line.length; i++) p.lineTo(line[i]!.x, line[i]!.y);
    if (closed) p.closePath();
    return p;
  }

  private buildPaths(): void {
    const t = this.track;
    const half = t.halfWidth;
    const n = t.center.length;

    this.asphalt = this.ribbon(t.center, t.normals, half, true);
    this.asphaltInner = this.polyline(t.center, true);
    this.edgeL = this.polyline(offsetLine(t.center, t.normals, -(half - 0.25)), true);
    this.edgeR = this.polyline(offsetLine(t.center, t.normals, +(half - 0.25)), true);

    // Zebras nas curvas: segmentos alternados vermelho/branco onde a
    // curvatura local é alta (mesma ideia do protótipo aprovado).
    for (const side of [-1, 1] as const) {
      const line = offsetLine(t.center, t.normals, side * (half + 0.45));
      for (let i = 0; i < n; i += 2) {
        const p0 = t.center[i]!, p1 = t.center[(i + 4) % n]!, pm = t.center[(i + 2) % n]!;
        // Curvatura discreta: desvio do ponto médio da corda.
        const mx = (p0.x + p1.x) / 2, my = (p0.y + p1.y) / 2;
        const dev = Math.hypot(pm.x - mx, pm.y - my);
        if (dev < 0.55) continue; // trecho reto: sem zebra
        const a = line[i]!, b = line[(i + 2) % n]!;
        const seg = new Path2D();
        seg.moveTo(a.x, a.y);
        seg.lineTo(b.x, b.y);
        this.kerbs.push({ path: seg, color: (i / 2) % 2 === 0 ? '#b83232' : '#c9ccd2' });
      }
    }

    // Pit lane (faixa aberta) + linha-limite (borda interna, voltada à pista)
    // + telhados das garagens atrás de cada box.
    if (t.pitPath.length > 2) {
      const pn = t.pitPath.length;
      const normals: Vec2[] = [];
      for (let i = 0; i < pn; i++) {
        const prev = t.pitPath[Math.max(0, i - 1)]!;
        const next = t.pitPath[Math.min(pn - 1, i + 1)]!;
        let tx = next.x - prev.x, ty = next.y - prev.y;
        const l = Math.hypot(tx, ty) || 1;
        normals.push({ x: ty / l, y: -tx / l });
      }
      this.pitRibbon = this.ribbon(t.pitPath, normals, 1.7, false);
      // borda interna (lado da pista): pitPath deslocado -1.7 na normal.
      this.pitEdge = this.polyline(offsetLine(t.pitPath, normals, -1.7), false);
      // garagem atrás de cada box (deslocada para fora, +1.9).
      for (const bk of t.pitBoxPathIndex) {
        const p = t.pitPath[bk]!, nm = normals[bk]!;
        const nxt = t.pitPath[Math.min(pn - 1, bk + 1)]!;
        this.pitAwnings.push({
          x: p.x + nm.x * 1.9, y: p.y + nm.y * 1.9,
          a: Math.atan2(nxt.y - p.y, nxt.x - p.x),
        });
      }
    }

    // Linha de largada quadriculada (2 fileiras).
    const sp = t.center[0]!, snm = t.normals[0]!;
    const along = { x: -snm.y, y: snm.x };
    const sq = 0.7;
    const cols = Math.max(2, Math.round(t.data.trackWidth / sq));
    for (let row = 0; row < 2; row++) {
      for (let col = 0; col < cols; col++) {
        const off = (col - (cols - 1) * 0.5) * sq;
        this.checkSquares.push({
          x: sp.x + snm.x * off + along.x * row * sq,
          y: sp.y + snm.y * off + along.y * row * sq,
          s: sq,
          dark: (row + col) % 2 === 0,
          a: Math.atan2(along.y, along.x),
        });
      }
    }

    // Marcas do grid.
    for (let i = 0; i < t.gridPositions.length; i++) {
      const g = t.gridPositions[i]!;
      const { nearestIndex } = t.idealLine.closestArcFraction(g);
      const p0 = t.idealLine.point(nearestIndex);
      const p1 = t.idealLine.point(nearestIndex + 1);
      this.gridSlots.push({ x: g.x, y: g.y, a: Math.atan2(p1.y - p0.y, p1.x - p0.x) });
    }
  }

  /** Ambiente estático desenhado UMA vez num canvas offscreen. */
  private bakeBackground(): void {
    const spanX = this.maxX - this.minX + BG_MARGIN * 2;
    const spanY = this.maxY - this.minY + BG_MARGIN * 2;
    // Limita a textura a ~2048px no maior lado (memória iOS).
    this.bgScale = Math.min(8, 2048 / Math.max(spanX, spanY));
    const w = Math.ceil(spanX * this.bgScale);
    const h = Math.ceil(spanY * this.bgScale);

    const off = document.createElement('canvas');
    off.width = w; off.height = h;
    const c = off.getContext('2d')!;
    const rand = seeded(this.track.data.trackId);

    // Mundo -> px do offscreen.
    c.setTransform(this.bgScale, 0, 0, this.bgScale, (-this.minX + BG_MARGIN) * this.bgScale, (-this.minY + BG_MARGIN) * this.bgScale);

    // Grama base com vinheta radial.
    const cx = (this.minX + this.maxX) / 2, cy = (this.minY + this.maxY) / 2;
    const R = Math.max(spanX, spanY) * 0.75;
    const grad = c.createRadialGradient(cx, cy, R * 0.15, cx, cy, R);
    // Grama mais escura e dessaturada, para casar com a UI sci-fi e destacar a pista.
    grad.addColorStop(0, '#16341f');
    grad.addColorStop(0.7, '#102a19');
    grad.addColorStop(1, '#0a1c11');
    c.fillStyle = grad;
    c.fillRect(this.minX - BG_MARGIN, this.minY - BG_MARGIN, spanX, spanY);

    // Ruído da grama (manchas claras/escuras).
    for (let i = 0; i < 900; i++) {
      const x = this.minX - BG_MARGIN + rand() * spanX;
      const y = this.minY - BG_MARGIN + rand() * spanY;
      const r = 0.4 + rand() * 1.6;
      c.fillStyle = rand() < 0.5 ? 'rgba(255,255,255,0.025)' : 'rgba(0,0,0,0.05)';
      c.beginPath();
      c.arc(x, y, r, 0, Math.PI * 2);
      c.fill();
    }

    // Árvores espalhadas (fora da pista E fora do pit lane).
    const isNearTrack = (x: number, y: number): boolean => {
      const limit = (this.track.halfWidth + 9) ** 2;
      for (const p of this.track.center) {
        if ((p.x - x) ** 2 + (p.y - y) ** 2 < limit) return true;
      }
      for (const p of this.track.pitPath) {
        if ((p.x - x) ** 2 + (p.y - y) ** 2 < 36) return true;
      }
      return false;
    };
    for (let i = 0; i < 70; i++) {
      const x = this.minX - BG_MARGIN * 0.6 + rand() * (spanX - BG_MARGIN * 1.2);
      const y = this.minY - BG_MARGIN * 0.6 + rand() * (spanY - BG_MARGIN * 1.2);
      if (isNearTrack(x, y)) continue;
      const r = 1.2 + rand() * 1.8;
      c.fillStyle = 'rgba(0,0,0,0.25)';
      c.beginPath(); c.arc(x + 0.4, y + 0.5, r, 0, Math.PI * 2); c.fill();
      const g2 = c.createRadialGradient(x - r * 0.3, y - r * 0.3, r * 0.1, x, y, r);
      const leaf = rand() < 0.25 ? ['#e8a7c8', '#b96b92'] : ['#3f7a44', '#26512b'];
      g2.addColorStop(0, leaf[0]!);
      g2.addColorStop(1, leaf[1]!);
      c.fillStyle = g2;
      c.beginPath(); c.arc(x, y, r, 0, Math.PI * 2); c.fill();
    }

    // ---- Infraestrutura de circuito: barreiras, placas e arquibancadas ----
    const TAU = Math.PI * 2;
    const n = this.track.center.length;
    // Torcida: mais tons neutros que vibrantes, para não virar ruído colorido.
    const crowd = ['#c9b79a', '#a98f74', '#d6dae2', '#8a97ab', '#b8bfcb', '#d06a6a', '#5a90c0', '#c8a840', '#68b080'];

    // 1) Placas de publicidade (hoardings) na borda EXTERNA. Base escura com
    //    acentos de cor esparsos (a cada ~4 segmentos), para não poluir.
    const adLine = offsetLine(this.track.center, this.track.normals, this.track.halfWidth + 0.5);
    const adCols = ['#1a4e7a', '#1c6b4a', '#7a2030', '#0e5c66', '#2a3550'];
    for (let i = 0; i < n; i++) {
      const a = adLine[i]!, b = adLine[(i + 1) % n]!;
      c.strokeStyle = i % 4 === 0 ? adCols[(i / 4 | 0) % adCols.length]! : '#182234';
      c.lineWidth = 0.62;
      c.lineCap = 'butt';
      c.beginPath(); c.moveTo(a.x, a.y); c.lineTo(b.x, b.y); c.stroke();
    }
    // linha branca fina no topo das placas
    c.strokeStyle = 'rgba(235,242,255,.4)'; c.lineWidth = 0.1;
    c.beginPath();
    for (let i = 0; i <= n; i++) { const p = adLine[i % n]!; i === 0 ? c.moveTo(p.x, p.y) : c.lineTo(p.x, p.y); }
    c.stroke();

    // 2) Barreira de proteção na borda INTERNA (infield).
    const innerBar = offsetLine(this.track.center, this.track.normals, -(this.track.halfWidth + 0.5));
    c.strokeStyle = '#0c1420'; c.lineWidth = 0.6;
    c.beginPath();
    for (let i = 0; i <= n; i++) { const p = innerBar[i % n]!; i === 0 ? c.moveTo(p.x, p.y) : c.lineTo(p.x, p.y); }
    c.stroke();

    // 3) Arquibancadas em degraus, tangentes à pista, em 3 pontos.
    const drawStand = (idx: number, len: number): void => {
      const cpt = this.track.center[idx % n]!, nm = this.track.normals[idx % n]!;
      const nxt = this.track.center[(idx + 2) % n]!;
      const ang = Math.atan2(nxt.y - cpt.y, nxt.x - cpt.x);
      const dist = this.track.halfWidth + 3.2;
      c.save();
      c.translate(cpt.x + nm.x * dist, cpt.y + nm.y * dist);
      c.rotate(ang);
      const half = len / 2, depth = 6.5, rows = 6;
      // sombra na grama
      c.fillStyle = 'rgba(0,0,0,.28)';
      c.fillRect(-half - 0.5, 0.4, len + 1, 1.6);
      // telhado (cobertura)
      c.fillStyle = '#0d1420';
      c.fillRect(-half - 0.8, -depth - 1.6, len + 1.6, 1.6);
      c.fillStyle = 'rgba(34,228,212,.25)'; // borda luminosa do teto
      c.fillRect(-half - 0.8, -depth - 0.2, len + 1.6, 0.18);
      // degraus (concreto claro → escuro ao fundo) + torcida
      for (let r = rows - 1; r >= 0; r--) {
        const ry = -depth + r * (depth / rows);
        const t = r / rows;
        c.fillStyle = `rgb(${34 - t * 12},${46 - t * 16},${64 - t * 22})`;
        c.fillRect(-half, ry, len, depth / rows + 0.06);
        const seats = Math.floor(len / 0.52);
        for (let s = 0; s < seats; s++) {
          c.fillStyle = crowd[Math.floor(rand() * crowd.length)]!;
          c.globalAlpha = 0.6 + rand() * 0.25;
          c.beginPath();
          c.arc(-half + 0.28 + s * 0.52, ry + depth / rows * 0.55, 0.18, 0, TAU);
          c.fill();
        }
        c.globalAlpha = 1;
      }
      // barreira frontal branca + pista de acesso
      c.fillStyle = '#c9d4e6'; c.fillRect(-half, -0.05, len, 0.32);
      c.restore();
    };
    // Nas RETAS (índice 0 = largada; n/2 = reta oposta) as arquibancadas
    // ficam bem alinhadas; em pistas não-ovais seguem a tangente igual.
    drawStand(0, 24);
    drawStand(Math.floor(n / 2), 20);

    this.bg = off;
  }

  // ------------------------------------------------------------------

  /** Atualiza a câmera (suavização + follow). */
  updateCamera(dt: number, followTarget: Vec2 | null): void {
    if (this.cam.follow && followTarget) {
      this.cam.tx = followTarget.x;
      this.cam.ty = followTarget.y;
      if (this.cam.tz < 2.1) this.cam.tz = 2.1;
    }
    const lp = Math.min(1, dt * 7);
    this.cam.x += (this.cam.tx - this.cam.x) * lp;
    this.cam.y += (this.cam.ty - this.cam.y) * lp;
    this.cam.z += (this.cam.tz - this.cam.z) * lp;
    this.stepParticles(dt);
  }

  /** Emite um punhado de faíscas num ponto do mundo (contato/batida). */
  spark(x: number, y: number, strength = 1): void {
    const count = Math.min(18, Math.round(6 * strength));
    for (let i = 0; i < count; i++) {
      const a = Math.random() * Math.PI * 2;
      const sp = (2 + Math.random() * 6) * strength;
      const max = 0.25 + Math.random() * 0.35;
      this.particles.push({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: max, max });
    }
    if (this.particles.length > 220) this.particles.splice(0, this.particles.length - 220);
  }

  private stepParticles(dt: number): void {
    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i]!;
      p.life -= dt;
      if (p.life <= 0) { this.particles.splice(i, 1); continue; }
      p.x += p.vx * dt; p.y += p.vy * dt;
      p.vx *= 0.88; p.vy *= 0.88;
    }
  }

  resetCamera(): void {
    this.cam.follow = false;
    this.cam.tx = (this.minX + this.maxX) / 2;
    this.cam.ty = (this.minY + this.maxY) / 2;
    this.cam.tz = 1;
  }

  zoomBy(factor: number, pivot?: Vec2): void {
    const oldZ = this.cam.tz;
    const newZ = Math.min(6, Math.max(0.6, oldZ * factor));
    if (pivot) {
      // Zoom no cursor: mantém o ponto do mundo sob o cursor.
      const k = 1 - oldZ / newZ;
      this.cam.tx += (pivot.x - this.cam.tx) * k;
      this.cam.ty += (pivot.y - this.cam.ty) * k;
    }
    this.cam.tz = newZ;
  }

  panBy(dxScreen: number, dyScreen: number): void {
    const s = this.scale;
    this.cam.follow = false;
    this.cam.tx -= (dxScreen * this.dpr) / s;
    this.cam.ty -= (dyScreen * this.dpr) / s;
    this.cam.x = this.cam.tx;
    this.cam.y = this.cam.ty;
  }

  // ------------------------------------------------------------------

  render(marbles: MarbleActor[], weather: Weather, now: number): void {
    const c = this.ctx;
    const s = this.scale;

    c.setTransform(1, 0, 0, 1, 0, 0);
    c.clearRect(0, 0, this.W, this.H);

    // Transform mundo->tela.
    c.setTransform(s, 0, 0, s, this.W / 2 - this.cam.x * s, this.H / 2 - this.cam.y * s);

    // 1) Ambiente pré-renderizado (1 drawImage).
    if (this.bg) {
      c.drawImage(
        this.bg,
        this.minX - BG_MARGIN, this.minY - BG_MARGIN,
        (this.maxX - this.minX) + BG_MARGIN * 2, (this.maxY - this.minY) + BG_MARGIN * 2,
      );
    }

    // 2) Zebras (embaixo da pista, sobram só as bordas).
    c.lineWidth = 1.0;
    c.lineCap = 'butt';
    for (const k of this.kerbs) {
      c.strokeStyle = k.color;
      c.stroke(k.path);
    }

    // 3) Pit lane (asfalto azulado + linha-limite tracejada + garagens).
    if (this.pitRibbon && this.pitEdge) {
      c.fillStyle = '#1d2c3e';
      c.fill(this.pitRibbon);
      // linha-limite branca contínua na borda de dentro
      c.strokeStyle = 'rgba(235,242,255,0.6)';
      c.lineWidth = 0.18;
      c.stroke(this.pitEdge);
      // faixa amarela tracejada (velocidade limitada)
      c.strokeStyle = 'rgba(233,190,92,0.7)';
      c.lineWidth = 0.14;
      c.setLineDash([0.8, 0.6]);
      c.stroke(this.pitEdge);
      c.setLineDash([]);
      // garagens (telhado escuro) atrás de cada box
      c.fillStyle = '#101a28';
      for (const g of this.pitAwnings) {
        c.save(); c.translate(g.x, g.y); c.rotate(g.a);
        c.fillRect(-1.1, -2.0, 2.2, 1.4);
        c.restore();
      }
    }

    // 4) Asfalto + bordas.
    const wet = weather === 'LightRain' || weather === 'HeavyRain' || weather === 'Damp';
    c.fillStyle = weather === 'HeavyRain' || weather === 'LightRain' ? '#2e3340' : '#383b44';
    c.fill(this.asphalt, 'evenodd');

    // Linha de borracha ("racing line" rubberizada) ao longo da ideal.
    c.strokeStyle = 'rgba(20,16,22,0.5)';
    c.lineWidth = this.track.halfWidth * 0.42;
    c.lineCap = 'round'; c.lineJoin = 'round';
    c.stroke(this.asphaltInner);

    c.strokeStyle = 'rgba(255,255,255,0.85)';
    c.lineWidth = 0.22;
    c.stroke(this.edgeL);
    c.stroke(this.edgeR);

    // Reflexo molhado (chuva): brilho azulado + faixas de reflexo animadas.
    if (wet) {
      c.fillStyle = weather === 'Damp' ? 'rgba(140,180,255,0.05)' : 'rgba(150,190,255,0.10)';
      c.fill(this.asphalt, 'evenodd');
    }

    // 5) Linha de largada + grid.
    for (const q of this.checkSquares) {
      c.save();
      c.translate(q.x, q.y);
      c.rotate(q.a);
      c.fillStyle = q.dark ? '#141414' : '#f2f2f2';
      c.fillRect(-q.s / 2, -q.s / 2, q.s, q.s);
      c.restore();
    }
    c.fillStyle = 'rgba(255,255,255,0.6)';
    for (const g of this.gridSlots) {
      c.save();
      c.translate(g.x, g.y);
      c.rotate(g.a);
      c.fillRect(-0.65, -0.65, 0.18, 1.3);
      c.fillRect(-0.65, -0.65, 1.3, 0.18);
      c.restore();
    }

    // 6) Boxes coloridos do pit (com marca de posição e cantos).
    const pitIdx = this.track.pitBoxPathIndex;
    for (let i = 0; i < Math.min(pitIdx.length, marbles.length); i++) {
      const p = this.track.pitPath[pitIdx[i]!]!;
      c.fillStyle = marbles[i]!.m.teamPrimary;
      c.globalAlpha = 0.9;
      c.fillRect(p.x - 0.7, p.y - 0.7, 1.4, 1.4);
      c.globalAlpha = 1;
      c.strokeStyle = 'rgba(255,255,255,0.4)';
      c.lineWidth = 0.08;
      c.strokeRect(p.x - 0.7, p.y - 0.7, 1.4, 1.4);
    }

    // 7) Bolinhas (maiores + motion blur + oclusão + corpo + aros de destaque).
    const r = 0.58;
    if (!this.leaderGlow) {
      this.leaderGlow = c.createRadialGradient(0, 0, r * 0.4, 0, 0, r * 2.4);
      this.leaderGlow.addColorStop(0, 'rgba(233,190,92,0.5)');
      this.leaderGlow.addColorStop(1, 'rgba(233,190,92,0)');
    }
    for (const a of marbles) {
      const m = a.m;
      const body = m.marbleColor; // cor de corrida da equipe
      const speed = Math.hypot(a.vx, a.vy);
      const inPit = m.state === 'InPit' || m.state === 'EnteringPit' || m.state === 'ExitingPit';

      // Glow dourado do líder (halo atrás da bolinha).
      if (m.position === 1 && !inPit) {
        c.save();
        c.translate(m.x, m.y);
        c.fillStyle = this.leaderGlow;
        c.beginPath(); c.arc(0, 0, r * 2.4, 0, Math.PI * 2); c.fill();
        c.restore();
      }

      // Trilha.
      const tr = m.trail;
      if (tr.length > 1) {
        c.strokeStyle = body;
        c.globalAlpha = 0.32;
        c.lineWidth = r * 0.9;
        c.lineCap = 'round';
        c.beginPath();
        c.moveTo(tr[0]!.x, tr[0]!.y);
        for (let i = 1; i < tr.length; i++) c.lineTo(tr[i]!.x, tr[i]!.y);
        c.stroke();
        c.globalAlpha = 1;
      }

      // Motion blur: rastro alongado no sentido da velocidade (só rápido).
      if (speed > 4 && a.m.state === 'Racing') {
        const k = Math.min(1, (speed - 4) / 8);
        const bx = a.vx / (speed || 1), by = a.vy / (speed || 1);
        c.strokeStyle = body;
        c.globalAlpha = 0.22 * k;
        c.lineWidth = r * 1.7;
        c.lineCap = 'round';
        c.beginPath();
        c.moveTo(m.x - bx * (0.6 + k), m.y - by * (0.6 + k));
        c.lineTo(m.x, m.y);
        c.stroke();
        c.globalAlpha = 1;
      }

      // Gradientes cacheados (origem) → translate por bolinha.
      if (!this.aoGrad) {
        this.aoGrad = c.createRadialGradient(0.14, 0.18, r * 0.2, 0.14, 0.18, r * 1.35);
        this.aoGrad.addColorStop(0, 'rgba(0,0,0,0.45)');
        this.aoGrad.addColorStop(1, 'rgba(0,0,0,0)');
      }
      let bodyGrad = this.bodyGrads.get(body);
      if (!bodyGrad) {
        bodyGrad = c.createRadialGradient(-r * 0.32, -r * 0.36, r * 0.1, 0, 0, r);
        bodyGrad.addColorStop(0, lighten(body, 0.35));
        bodyGrad.addColorStop(0.6, body);
        bodyGrad.addColorStop(1, darken(body, 0.4));
        this.bodyGrads.set(body, bodyGrad);
      }

      c.save();
      c.translate(m.x, m.y);
      // Oclusão de contato (AO).
      c.fillStyle = this.aoGrad;
      c.beginPath(); c.arc(0.14, 0.18, r * 1.35, 0, Math.PI * 2); c.fill();
      // Corpo (esfera sombreada).
      c.fillStyle = bodyGrad;
      c.beginPath(); c.arc(0, 0, r, 0, Math.PI * 2); c.fill();
      // Anel secundário.
      c.strokeStyle = m.teamSecondary; c.lineWidth = 0.13;
      c.beginPath(); c.arc(0, 0, r * 0.72, 0, Math.PI * 2); c.stroke();
      // Brilho especular.
      c.fillStyle = 'rgba(255,255,255,0.8)';
      c.beginPath(); c.arc(-r * 0.3, -r * 0.35, r * 0.17, 0, Math.PI * 2); c.fill();
      // Aro forte do jogador: contorno escuro + anel dourado pulsante (bem visível).
      if (m.isPlayer) {
        c.strokeStyle = 'rgba(0,0,0,0.5)';
        c.lineWidth = 0.22;
        c.beginPath(); c.arc(0, 0, r + 0.30, 0, Math.PI * 2); c.stroke();
        c.strokeStyle = `rgba(233,190,92,${0.7 + 0.3 * Math.sin(now / 280)})`;
        c.lineWidth = 0.16;
        c.beginPath(); c.arc(0, 0, r + 0.30, 0, Math.PI * 2); c.stroke();
      }
      c.restore();
    }

    // 7b) Faíscas de contato.
    if (this.particles.length > 0) {
      for (const p of this.particles) {
        const k = p.life / p.max;
        c.globalAlpha = k;
        c.fillStyle = k > 0.5 ? '#fff2c0' : '#ff9d3a';
        c.beginPath();
        c.arc(p.x, p.y, 0.06 + 0.1 * k, 0, Math.PI * 2);
        c.fill();
      }
      c.globalAlpha = 1;
    }

    // 7c) Rótulos/alertas em espaço de tela (legíveis em qualquer zoom, só o
    //     que importa: PIT, sigla do jogador, alerta de combustível baixo).
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.textAlign = 'center';
    const dpr = this.dpr;
    const rPix = r * this.scale;
    for (const a of marbles) {
      const m = a.m;
      const inPit = m.state === 'InPit' || m.state === 'EnteringPit' || m.state === 'ExitingPit';
      const lowFuel = m.isPlayer && m.fuel < 20 && m.state === 'Racing';
      if (!inPit && !m.isPlayer && !lowFuel) continue; // não polui com IA comum
      const sp = this.worldToScreen(m.x, m.y);
      const top = sp.y - rPix - 4 * dpr;

      if (inPit) {
        c.font = `700 ${9 * dpr}px ui-monospace, Menlo, monospace`;
        const w = c.measureText('PIT').width + 8 * dpr;
        c.fillStyle = 'rgba(255,138,46,0.92)';
        roundRectPath(c, sp.x - w / 2, top - 12 * dpr, w, 13 * dpr, 3 * dpr); c.fill();
        c.fillStyle = '#1a0b00';
        c.fillText('PIT', sp.x, top - 2.5 * dpr);
      } else if (m.isPlayer) {
        // sigla do jogador
        c.font = `800 ${9.5 * dpr}px ui-monospace, Menlo, monospace`;
        c.fillStyle = 'rgba(4,10,20,0.7)';
        const w = c.measureText(m.driver.shortCode).width + 7 * dpr;
        roundRectPath(c, sp.x - w / 2, top - 12 * dpr, w, 13 * dpr, 3 * dpr); c.fill();
        c.fillStyle = '#e9be5c';
        c.fillText(m.driver.shortCode, sp.x, top - 2.5 * dpr);
        if (lowFuel && Math.sin(now / 200) > 0) {
          // alerta de combustível baixo: "!" amarelo pulsante ao lado.
          const ax = sp.x + w / 2 + 7 * dpr, ay = top - 5.5 * dpr;
          c.fillStyle = '#ffcc2e';
          c.beginPath(); c.arc(ax, ay, 6 * dpr, 0, Math.PI * 2); c.fill();
          c.fillStyle = '#1a0b00';
          c.font = `900 ${9 * dpr}px ui-monospace`;
          c.fillText('!', ax, ay + 3.2 * dpr);
        }
      }
    }
    c.textAlign = 'left';

    // 8) Chuva (streaks em espaço de tela).
    if (weather === 'LightRain' || weather === 'HeavyRain') {
      c.setTransform(1, 0, 0, 1, 0, 0);
      const drops = weather === 'HeavyRain' ? 90 : 40;
      c.strokeStyle = 'rgba(160,200,255,0.25)';
      c.lineWidth = 1 * this.dpr;
      c.beginPath();
      for (let i = 0; i < drops; i++) {
        const x = ((i * 379 + now * 0.7) % (this.W + 60)) - 30;
        const y = ((i * 631 + now * 1.9) % (this.H + 80)) - 40;
        c.moveTo(x, y);
        c.lineTo(x - 4 * this.dpr, y + 12 * this.dpr);
      }
      c.stroke();
    }

    // 9) Vinheta (gradiente cacheado; só depende de W/H → rebuild no resize).
    c.setTransform(1, 0, 0, 1, 0, 0);
    if (!this.vignette) {
      this.vignette = c.createRadialGradient(
        this.W / 2, this.H / 2, Math.min(this.W, this.H) * 0.34,
        this.W / 2, this.H / 2, Math.max(this.W, this.H) * 0.74);
      this.vignette.addColorStop(0, 'rgba(0,0,0,0)');
      this.vignette.addColorStop(0.7, 'rgba(2,5,12,0.22)');
      this.vignette.addColorStop(1, 'rgba(2,5,12,0.58)');
    }
    c.fillStyle = this.vignette;
    c.fillRect(0, 0, this.W, this.H);
  }
}
