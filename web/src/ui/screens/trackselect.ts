// Tela: Seleção de circuito — port de ShowTrackSelect (lista + preview).

import { TRACKS } from '../../data/circuits';
import type { Difficulty, TrackData } from '../../core/types';
import { trackThumb } from '../../render/thumb';
import { btn, div, label, mount, trim } from '../dom';
import { show } from '../router';
import { goMenu, goStrategy } from '../flow';

let previewId: string | null = null;

function difficultyLabel(d: Difficulty): string {
  return d === 'Easy' ? 'Fácil' : d === 'Medium' ? 'Médio' : 'Difícil';
}

function difficultyDots(d: Difficulty): number {
  return d === 'Easy' ? 2 : d === 'Medium' ? 3 : 4;
}

export function trackSelectScreen(): void {
  show('trackselect', root => {
    if (!previewId) previewId = TRACKS[0]!.trackId;
    const preview = TRACKS.find(t => t.trackId === previewId) ?? TRACKS[0]!;

    const head = div('sc-head');
    const h1 = document.createElement('h1');
    h1.textContent = 'SELECIONAR CIRCUITO';
    mount(head, h1, label('Escolha o circuito da sua próxima corrida.', 'sub'));

    const body = div('tsel-body');

    // Lista (esquerda).
    const list = div('vscroll');
    list.style.display = 'flex';
    list.style.flexDirection = 'column';
    list.style.gap = '8px';
    TRACKS.forEach((t, i) => {
      const item = document.createElement('button');
      item.className = `circ-item${t.trackId === preview.trackId ? ' sel' : ''}`;
      const th = div('th');
      th.appendChild(trackThumb(t.trackId, 64));
      const nm = label(`${i + 1}. ${t.trackName}`, 'nm');
      const meta = label(
        `${difficultyLabel(t.difficulty)} · ${t.recommendedLaps}v · chuva ${Math.round(t.rainChance * 100)}%`,
        'meta',
      );
      mount(item, th, nm, meta);
      item.addEventListener('click', () => { previewId = t.trackId; trackSelectScreen(); });
      list.appendChild(item);
    });

    // Preview (direita).
    body.append(list, buildPreview(preview, TRACKS.indexOf(preview) + 1));

    const foot = div('sc-foot');
    mount(foot,
      btn('Voltar', 'ghost', goMenu),
      div('spacer'),
      btn('Aleatório', 'blue', () => {
        previewId = TRACKS[Math.floor(Math.random() * TRACKS.length)]!.trackId;
        trackSelectScreen();
      }),
      btn('Confirmar', 'grn', () => goStrategy(preview.trackId)),
    );
    mount(root, head, body, foot);
  });
}

function buildPreview(t: TrackData, num: number): HTMLElement {
  const panel = div('panel circ-prev');
  const thBig = div('th-big');
  thBig.appendChild(trackThumb(t.trackId, 360, 150));

  mount(panel,
    label('CIRCUITO ' + num, 'badge'),
    (() => { const h = document.createElement('h2'); h.textContent = t.trackName.toUpperCase(); return h; })(),
    thBig,
    pips('DIFICULDADE', difficultyDots(t.difficulty), 5, 'var(--green)'),
    pips('ULTRAPASSAGEM', Math.round(t.overtakeLevel * 8), 8, 'var(--cyan)'),
    pips('DESGASTE DE PNEU', Math.round(((t.abrasionLevel - 0.5) / 1.5) * 8), 8, 'var(--orange)'),
    label(trim(t.description, 160), 'desc'),
  );
  return panel;
}

function pips(lb: string, filled: number, total: number, color: string): HTMLElement {
  const box = div('stat-pips');
  box.appendChild(label(lb, 'lb'));
  const row = div('pips');
  for (let i = 0; i < total; i++) {
    const p = document.createElement('i');
    if (i < filled) p.style.background = color;
    row.appendChild(p);
  }
  box.appendChild(row);
  return box;
}
