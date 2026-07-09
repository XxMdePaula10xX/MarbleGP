// =====================================================================
// Exporta o resultado da corrida como imagem premium (Canvas → PNG) para
// compartilhar. Usa Web Share com arquivo quando disponível; senão baixa.
// =====================================================================

import { teamById } from '../../data/teams';
import type { RaceResult } from '../../sim/systems';

function teamColor(teamId: string): string {
  try { return teamById(teamId).primaryColor; } catch { return '#7f93b3'; }
}

/** Desenha e compartilha/baixa um card 1080×1350 do resultado. */
export async function exportResultImage(result: RaceResult): Promise<void> {
  const W = 1080, H = 1350;
  const cv = document.createElement('canvas');
  cv.width = W; cv.height = H;
  const c = cv.getContext('2d');
  if (!c) return;

  // Fundo (grounds do jogo).
  const bg = c.createLinearGradient(0, 0, 0, H);
  bg.addColorStop(0, '#0b1730'); bg.addColorStop(1, '#05090f');
  c.fillStyle = bg; c.fillRect(0, 0, W, H);
  const glow = c.createRadialGradient(W * 0.2, 0, 80, W * 0.2, 0, W);
  glow.addColorStop(0, 'rgba(34,228,212,0.14)'); glow.addColorStop(1, 'transparent');
  c.fillStyle = glow; c.fillRect(0, 0, W, H);

  const cx = W / 2;
  const mono = "700 %spx ui-monospace, Menlo, Consolas, monospace";
  const sans = "%s -apple-system, 'Segoe UI', system-ui, sans-serif";

  // Marca.
  c.textAlign = 'center';
  c.fillStyle = '#eaf1ff';
  c.font = sans.replace('%s', '900 64px');
  c.fillText('MARBLE', cx - 44, 130);
  c.fillStyle = '#e9be5c';
  c.fillText('GP', cx + 118, 130);
  c.fillStyle = '#5b6e8c';
  c.font = mono.replace('%s', '22');
  c.fillText('RESULTADO · ' + result.trackName.toUpperCase(), cx, 178);

  // Pódio.
  const top3 = result.entries.slice(0, 3);
  const podY = 640, baseY = podY;
  const cols = [{ e: top3[1], x: cx - 240, h: 220, tag: '2' }, { e: top3[0], x: cx, h: 320, tag: '1' }, { e: top3[2], x: cx + 240, h: 150, tag: '3' }];
  const cw = 200;
  for (const col of cols) {
    if (!col.e) continue;
    const color = teamColor(col.e.teamId);
    // coluna
    const gx = col.x - cw / 2;
    const grad = c.createLinearGradient(0, baseY - col.h, 0, baseY);
    grad.addColorStop(0, hexA(color, 0.45)); grad.addColorStop(1, hexA(color, 0.08));
    c.fillStyle = grad;
    c.fillRect(gx, baseY - col.h, cw, col.h);
    c.fillStyle = color;
    c.fillRect(gx, baseY - col.h, cw, 5);
    // posição grande
    c.fillStyle = col.tag === '1' ? '#e9be5c' : '#cfd9ee';
    c.font = sans.replace('%s', '800 46px');
    c.fillText('P' + col.tag, col.x, baseY - col.h + 60);
    // nome + pontos
    c.fillStyle = '#eaf1ff';
    c.font = sans.replace('%s', '700 30px');
    c.fillText(clip(col.e.marbleName, 14), col.x, baseY + 44);
    c.fillStyle = '#93a6c6';
    c.font = mono.replace('%s', '24');
    c.fillText(col.e.points + ' PTS', col.x, baseY + 82);
  }

  // Faixa do vencedor.
  const w = top3[0];
  if (w) {
    c.fillStyle = 'rgba(233,190,92,0.12)';
    roundRect(c, 90, 250, W - 180, 150, 20); c.fill();
    c.strokeStyle = 'rgba(233,190,92,0.5)'; c.lineWidth = 2; roundRect(c, 90, 250, W - 180, 150, 20); c.stroke();
    c.textAlign = 'left';
    c.fillStyle = '#e9be5c'; c.font = sans.replace('%s', '900 40px');
    c.fillText('🏆 ' + clip(w.marbleName, 16), 130, 320);
    c.fillStyle = '#93a6c6'; c.font = sans.replace('%s', '400 26px');
    c.fillText(w.teamName + '  ·  ' + fmt(w.totalTime) + '  ·  ' + w.pitStops + ' pit(s)', 130, 362);
    c.textAlign = 'center';
  }

  // Top 10 lista.
  let y = 780;
  c.textAlign = 'left';
  for (let i = 0; i < Math.min(10, result.entries.length); i++) {
    const e = result.entries[i]!;
    if (e.isPlayer) { c.fillStyle = 'rgba(34,228,212,0.10)'; roundRect(c, 90, y - 30, W - 180, 44, 8); c.fill(); }
    c.fillStyle = e.position === 1 ? '#e9be5c' : e.isPlayer ? '#22e4d4' : '#8ea6c8';
    c.font = mono.replace('%s', '26');
    c.fillText(String(e.position).padStart(2, ' '), 120, y);
    c.fillStyle = teamColor(e.teamId); c.fillRect(180, y - 20, 8, 22);
    c.fillStyle = e.isPlayer ? '#eaf1ff' : '#c6d3ea'; c.font = sans.replace('%s', '600 26px');
    c.fillText(clip(e.marbleName, 18), 205, y);
    c.textAlign = 'right';
    c.fillStyle = '#93a6c6'; c.font = mono.replace('%s', '24');
    c.fillText(e.points + '', W - 120, y);
    c.textAlign = 'left';
    y += 52;
  }

  c.textAlign = 'center';
  c.fillStyle = '#5b6e8c'; c.font = mono.replace('%s', '20');
  c.fillText('marble gp · strategy racing championship', cx, H - 44);

  // Compartilha ou baixa.
  const blob: Blob | null = await new Promise(res => cv.toBlob(res, 'image/png'));
  if (!blob) return;
  const file = new File([blob], 'marblegp-resultado.png', { type: 'image/png' });
  const navShare = navigator as Navigator & { canShare?: (d: ShareData) => boolean };
  if (navShare.canShare && navShare.canShare({ files: [file] })) {
    try { await navigator.share({ files: [file], title: 'Marble GP', text: `Resultado em ${result.trackName}` }); return; }
    catch { /* usuário cancelou → cai no download */ }
  }
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = 'marblegp-resultado.png';
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 2000);
}

// ---- helpers ----
function hexA(hex: string, a: number): string {
  const h = hex.replace('#', '');
  const n = parseInt(h.length === 3 ? h.split('').map(x => x + x).join('') : h, 16);
  return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${a})`;
}
function roundRect(c: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number): void {
  c.beginPath();
  c.moveTo(x + r, y);
  c.arcTo(x + w, y, x + w, y + h, r);
  c.arcTo(x + w, y + h, x, y + h, r);
  c.arcTo(x, y + h, x, y, r);
  c.arcTo(x, y, x + w, y, r);
  c.closePath();
}
function clip(s: string, n: number): string { return s.length <= n ? s : s.slice(0, n - 1) + '…'; }
function fmt(t: number): string {
  const m = Math.floor(t / 60), s = t - m * 60;
  return `${m}:${s.toFixed(1).padStart(4, '0')}`;
}
