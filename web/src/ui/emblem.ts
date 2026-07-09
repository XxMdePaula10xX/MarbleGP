// =====================================================================
// Emblemas de equipe (logos): escudo hexagonal com as iniciais, nas cores
// da equipe. Procedural — sem depender de assets. Reusado em resultado,
// pódio, classificação e menu.
// =====================================================================

import { teamById } from '../data/teams';

/** Iniciais de 2 letras a partir do nome da equipe. */
function initials(name: string): string {
  const words = name.split(/\s+/).filter(w => !/^(gp|racing|team|club)$/i.test(w));
  if (words.length >= 2) return (words[0]![0]! + words[1]![0]!).toUpperCase();
  return name.slice(0, 2).toUpperCase();
}

/** SVG do emblema da equipe. */
export function teamEmblem(teamId: string, size = 34): SVGSVGElement {
  let primary = '#7f93b3', secondary = '#0e1626', name = teamId;
  try {
    const t = teamById(teamId);
    primary = t.raceColor; secondary = t.secondaryColor; name = t.teamName;
  } catch { /* usa fallback */ }
  const ini = initials(name);
  const idg = `eg${teamId.replace(/[^a-z0-9]/gi, '')}`;

  const tmp = document.createElement('div');
  tmp.innerHTML = `
    <svg class="emblem" viewBox="0 0 40 40" width="${size}" height="${size}" aria-label="${name}">
      <defs>
        <linearGradient id="${idg}" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stop-color="${primary}"/>
          <stop offset="1" stop-color="${shade(primary, 0.42)}"/>
        </linearGradient>
      </defs>
      <path d="M20 2 34 9.5v15L20 38 6 24.5v-15z" fill="url(#${idg})"
        stroke="${secondary}" stroke-width="1.6" stroke-linejoin="round"/>
      <path d="M20 2 34 9.5v15L20 38 6 24.5v-15z" fill="none"
        stroke="rgba(255,255,255,.28)" stroke-width="0.7" stroke-linejoin="round"/>
      <text x="20" y="24.5" text-anchor="middle" font-family="ui-monospace,Menlo,monospace"
        font-size="14" font-weight="800" fill="${textOn(primary)}">${ini}</text>
    </svg>`;
  return tmp.firstElementChild as SVGSVGElement;
}

function toRgb(hex: string): [number, number, number] {
  const h = hex.replace('#', '');
  const n = parseInt(h.length === 3 ? h.split('').map(x => x + x).join('') : h, 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}
function shade(hex: string, k: number): string {
  const [r, g, b] = toRgb(hex);
  return `rgb(${Math.round(r * (1 - k))},${Math.round(g * (1 - k))},${Math.round(b * (1 - k))})`;
}
/** Preto ou branco conforme luminância, para contraste do texto. */
function textOn(hex: string): string {
  const [r, g, b] = toRgb(hex);
  const lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  return lum > 0.6 ? '#0a0f18' : '#ffffff';
}
