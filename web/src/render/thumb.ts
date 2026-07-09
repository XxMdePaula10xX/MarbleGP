// =====================================================================
// Miniatura procedural do traçado — substitui as thumbnails PNG do Unity.
// Desenha o loop do circuito num pequeno canvas (usado em listas/cards).
// =====================================================================

import { trackById } from '../data/circuits';
import { Track } from '../sim/track';

// Cache do BITMAP desenhado (canvas-fonte nunca montado no DOM). Cada chamada
// devolve um canvas novo copiando o bitmap via drawImage — cloneNode NÃO copia
// o conteúdo desenhado, só os atributos, então devolvia miniaturas em branco.
const cache = new Map<string, HTMLCanvasElement>();

export function trackThumb(trackId: string, w = 160, h = w): HTMLCanvasElement {
  const key = `${trackId}@${w}x${h}`;
  const dpr = Math.min(window.devicePixelRatio || 1, 2);
  const cw = w * dpr;
  const ch = h * dpr;

  const out = document.createElement('canvas');
  out.width = cw;
  out.height = ch;
  out.style.width = '100%';
  out.style.height = '100%';

  const hit = cache.get(key);
  if (hit) {
    out.getContext('2d')!.drawImage(hit, 0, 0);
    return out;
  }

  const canvas = document.createElement('canvas');
  canvas.width = cw;
  canvas.height = ch;
  const c = canvas.getContext('2d')!;

  const data = trackById(trackId);
  const track = new Track(data, 0);
  const pts = track.center;

  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const p of pts) {
    if (p.x < minX) minX = p.x;
    if (p.y < minY) minY = p.y;
    if (p.x > maxX) maxX = p.x;
    if (p.y > maxY) maxY = p.y;
  }
  const pad = data.trackWidth;
  const spanX = maxX - minX + pad * 2;
  const spanY = maxY - minY + pad * 2;
  // Ajusta o traçado à caixa (largura × altura), preservando a proporção.
  const s = Math.min((cw * 0.9) / spanX, (ch * 0.9) / spanY);
  const ox = cw / 2 - ((minX + maxX) / 2) * s;
  const oy = ch / 2 - ((minY + maxY) / 2) * s;

  // Fundo.
  c.fillStyle = '#0b1a2c';
  c.fillRect(0, 0, cw, ch);

  const path = new Path2D();
  path.moveTo(pts[0]!.x * s + ox, pts[0]!.y * s + oy);
  for (let i = 1; i < pts.length; i++) path.lineTo(pts[i]!.x * s + ox, pts[i]!.y * s + oy);
  path.closePath();

  // Asfalto (stroke grosso) + linha central.
  c.lineJoin = 'round';
  c.strokeStyle = '#31384a';
  c.lineWidth = data.trackWidth * s;
  c.stroke(path);
  c.strokeStyle = '#00dcf0';
  c.lineWidth = Math.max(1.5, 0.8 * s);
  c.stroke(path);

  // Ponto de largada.
  c.fillStyle = '#ffce46';
  c.beginPath();
  c.arc(pts[0]!.x * s + ox, pts[0]!.y * s + oy, Math.max(3, 1.6 * s), 0, Math.PI * 2);
  c.fill();

  cache.set(key, canvas);
  out.getContext('2d')!.drawImage(canvas, 0, 0);
  return out;
}
