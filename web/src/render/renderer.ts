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
  private checkSquares: Array<{ x: number; y: number; s: number; dark: boolean; a: number }> = [];
  private gridSlots: Array<{ x: number; y: number; a: number }> = [];
  private startPos: Vec2;
  private W = 0; private H = 0;

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
        this.kerbs.push({ path: seg, color: (i / 2) % 2 === 0 ? '#d92929' : '#f2f2f2' });
      }
    }

    // Pit lane (faixa aberta).
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
    grad.addColorStop(0, '#22492c');
    grad.addColorStop(0.7, '#1a3a22');
    grad.addColorStop(1, '#122718');
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

    // Faixas de "corte de grama" concêntricas ao traçado.
    c.save();
    c.clip(this.asphalt, 'evenodd'); // nada — só para manter tipo; removido abaixo
    c.restore();

    // Árvores espalhadas (fora da pista).
    const isNearTrack = (x: number, y: number): boolean => {
      let best = Infinity;
      const cpts = this.track.center;
      for (let i = 0; i < cpts.length; i += 4) {
        const d = (cpts[i]!.x - x) ** 2 + (cpts[i]!.y - y) ** 2;
        if (d < best) best = d;
      }
      return best < (this.track.halfWidth + 7) ** 2;
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

    // Arquibancada perto da linha de largada (lado externo).
    const sp = this.startPos, snm = this.track.normals[0]!;
    const standDist = this.track.halfWidth + 8;
    const bx = sp.x - snm.x * standDist, by = sp.y - snm.y * standDist;
    const along = { x: -snm.y, y: snm.x };
    c.save();
    c.translate(bx, by);
    c.rotate(Math.atan2(along.y, along.x));
    c.fillStyle = '#1c2734';
    c.fillRect(-14, -5.4, 28, 5.2);
    c.fillStyle = '#141c26';
    c.fillRect(-14.6, -5.9, 29.2, 1);
    // Torcida: pontinhos coloridos em fileiras.
    const crowdCols = ['#e8c39a', '#d2a276', '#e6e6e6', '#e05555', '#5aa0e0', '#e0c040', '#58c580', '#b070d5'];
    for (let r = 0; r < 4; r++) {
      for (let s = 0; s < 36; s++) {
        c.fillStyle = crowdCols[Math.floor(rand() * crowdCols.length)]!;
        c.beginPath();
        c.arc(-13 + s * 0.73, -1.1 - r * 1.05, 0.26, 0, Math.PI * 2);
        c.fill();
      }
    }
    c.restore();

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

    // 3) Pit lane.
    if (this.pitRibbon) {
      c.fillStyle = '#233444';
      c.fill(this.pitRibbon);
    }

    // 4) Asfalto + bordas.
    c.fillStyle = weather === 'HeavyRain' || weather === 'LightRain' ? '#2e3340' : '#383b44';
    c.fill(this.asphalt, 'evenodd');
    c.strokeStyle = 'rgba(255,255,255,0.85)';
    c.lineWidth = 0.22;
    c.stroke(this.edgeL);
    c.stroke(this.edgeR);

    // Reflexo molhado (chuva): brilho suave no asfalto.
    if (weather === 'LightRain' || weather === 'HeavyRain' || weather === 'Damp') {
      c.fillStyle = weather === 'Damp' ? 'rgba(140,180,255,0.04)' : 'rgba(140,180,255,0.08)';
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

    // 6) Boxes coloridos do pit.
    const pitIdx = this.track.pitBoxPathIndex;
    for (let i = 0; i < Math.min(pitIdx.length, marbles.length); i++) {
      const p = this.track.pitPath[pitIdx[i]!]!;
      c.fillStyle = marbles[i]!.m.teamPrimary;
      c.globalAlpha = 0.85;
      c.fillRect(p.x - 0.8, p.y - 0.8, 1.6, 1.6);
      c.globalAlpha = 1;
    }

    // 7) Bolinhas (trilha + corpo + brilho).
    const r = 0.5;
    for (const a of marbles) {
      const m = a.m;
      // Trilha.
      const tr = m.trail;
      if (tr.length > 1) {
        c.strokeStyle = m.teamPrimary;
        c.globalAlpha = 0.35;
        c.lineWidth = r * 0.9;
        c.lineCap = 'round';
        c.beginPath();
        c.moveTo(tr[0]!.x, tr[0]!.y);
        for (let i = 1; i < tr.length; i++) c.lineTo(tr[i]!.x, tr[i]!.y);
        c.stroke();
        c.globalAlpha = 1;
      }
      // Sombra.
      c.fillStyle = 'rgba(0,0,0,0.35)';
      c.beginPath();
      c.arc(m.x + 0.16, m.y + 0.2, r, 0, Math.PI * 2);
      c.fill();
      // Corpo.
      c.fillStyle = m.teamPrimary;
      c.beginPath();
      c.arc(m.x, m.y, r, 0, Math.PI * 2);
      c.fill();
      // Anel secundário.
      c.strokeStyle = m.teamSecondary;
      c.lineWidth = 0.14;
      c.beginPath();
      c.arc(m.x, m.y, r * 0.72, 0, Math.PI * 2);
      c.stroke();
      // Brilho especular.
      c.fillStyle = 'rgba(255,255,255,0.75)';
      c.beginPath();
      c.arc(m.x - r * 0.3, m.y - r * 0.35, r * 0.18, 0, Math.PI * 2);
      c.fill();
      // Destaque das bolinhas do jogador: aro dourado pulsante.
      if (m.isPlayer) {
        c.strokeStyle = `rgba(255,206,70,${0.55 + 0.35 * Math.sin(now / 300)})`;
        c.lineWidth = 0.12;
        c.beginPath();
        c.arc(m.x, m.y, r + 0.28, 0, Math.PI * 2);
        c.stroke();
      }
    }

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

    // 9) Vinheta.
    c.setTransform(1, 0, 0, 1, 0, 0);
    const v = c.createRadialGradient(this.W / 2, this.H / 2, Math.min(this.W, this.H) * 0.42,
      this.W / 2, this.H / 2, Math.max(this.W, this.H) * 0.72);
    v.addColorStop(0, 'rgba(0,0,0,0)');
    v.addColorStop(1, 'rgba(0,0,0,0.4)');
    c.fillStyle = v;
    c.fillRect(0, 0, this.W, this.H);
  }
}
