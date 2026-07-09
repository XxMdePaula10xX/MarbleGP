// Tela: Estratégia pré-corrida — port de ShowStrategy (pneu, modo, duração).

import type { GripType, RaceMode } from '../../core/types';
import { gripDisplayColor, gripDisplayLetter } from '../../core/types';
import { trackById } from '../../data/circuits';
import { buildQuickRace } from '../../sim/quickrace';
import { trackThumb } from '../../render/thumb';
import { btn, div, label, mount } from '../dom';
import { show } from '../router';
import { goRace, goTrackSelect } from '../flow';

let grip: GripType = 'Medium';
let mode: RaceMode = 'Normal';
let selectedLaps = 5;

const TYRES: Array<{ g: GripType; nm: string; de: string }> = [
  { g: 'Soft', nm: 'SOFT', de: 'aderência' },
  { g: 'Medium', nm: 'MEDIUM', de: 'equilíbrio' },
  { g: 'Hard', nm: 'HARD', de: 'durável' },
  { g: 'Intermediate', nm: 'INTER', de: 'úmido' },
  { g: 'Rain', nm: 'RAIN', de: 'chuva' },
];

const MODES: Array<{ m: RaceMode; nm: string; de: string }> = [
  { m: 'Save', nm: 'SAVE', de: 'economiza' },
  { m: 'Normal', nm: 'NORMAL', de: 'equilíbrio' },
  { m: 'Push', nm: 'PUSH', de: 'máximo' },
];

const DURATIONS: Array<{ laps: number; nm: string; de: string }> = [
  { laps: 5, nm: 'RÁPIDA', de: '5 voltas' },
  { laps: 12, nm: 'NORMAL', de: '12 voltas' },
  { laps: 20, nm: 'LONGA', de: '20 voltas' },
];

function tyreNote(g: GripType): string {
  switch (g) {
    case 'Soft': return 'macio, desgasta antes';
    case 'Hard': return 'duro, dura mais';
    case 'Rain': return 'para chuva forte';
    case 'Intermediate': return 'para pista úmida';
    default: return 'equilibrado';
  }
}

export function strategyScreen(trackId: string): void {
  show('strategy', root => {
    const t = trackById(trackId);
    const rainPct = Math.round(t.rainChance * 100);

    const head = div('sc-head');
    const h1 = document.createElement('h1');
    h1.textContent = `Estratégia — ${t.trackName}`;
    const wx = label(`${rainPct >= 30 ? 'INSTÁVEL' : 'SECO'} · chuva ${rainPct}%`, 'sub');
    wx.style.marginLeft = 'auto';
    mount(head, h1, wx);

    const body = div('strat-body');

    // ---- Coluna esquerda: escolhas (pneu / modo / duração) ----
    const choices = div('strat-choices');

    const optGroup = (
      title: string,
      items: Array<{ nm: string; de: string; sel: boolean; dot?: GripType; onPick: () => void }>,
    ): HTMLElement => {
      const g = div('panel opt-group');
      g.appendChild(label(title, 'gt'));
      const row = div('opt-row');
      for (const it of items) {
        const card = document.createElement('button');
        card.className = `opt-card${it.sel ? ' sel' : ''}`;
        if (it.dot) {
          const dot = div('tyre-dot');
          dot.style.color = gripDisplayColor(it.dot);
          dot.textContent = gripDisplayLetter(it.dot);
          card.appendChild(dot);
        }
        mount(card, label(it.nm, 't'), label(it.de, 'd'));
        card.addEventListener('click', it.onPick);
        row.appendChild(card);
      }
      g.appendChild(row);
      return g;
    };

    mount(choices,
      optGroup('PNEUS', TYRES.map(ty => ({
        nm: ty.nm, de: ty.de, sel: grip === ty.g, dot: ty.g,
        onPick: () => { grip = ty.g; strategyScreen(trackId); },
      }))),
      optGroup('MODO DE CORRIDA', MODES.map(md => ({
        nm: md.nm, de: md.de, sel: mode === md.m,
        onPick: () => { mode = md.m; strategyScreen(trackId); },
      }))),
      optGroup('DURAÇÃO DA CORRIDA', DURATIONS.map(du => ({
        nm: du.nm, de: du.de, sel: selectedLaps === du.laps,
        onPick: () => { selectedLaps = du.laps; strategyScreen(trackId); },
      }))),
    );

    // ---- Coluna direita: mapa + resumo ----
    const right = div('panel strat-summary');
    const mapWrap = div('strat-map');
    mapWrap.appendChild(trackThumb(t.trackId, 300, 130));
    const mapMeta = label(`${t.recommendedLaps} voltas rec. · ${Math.round(t.trackLength)} m`, 'mm');

    right.appendChild(label('RESUMO DA ESTRATÉGIA', 'gt'));
    right.appendChild(mapWrap);
    right.appendChild(mapMeta);

    const stops = selectedLaps >= 18 ? 2 : 1;
    const sumRow = (lb: string, vl: string, sb: string, color: string) => {
      const r = div('sum-row');
      const v = label(vl, 'vl');
      v.style.color = color;
      mount(r, label(lb, 'lb'), v, label(sb, 'sb'));
      right.appendChild(r);
    };
    sumRow('PARADAS PREVISTAS', String(stops), 'no pit', 'var(--orange)');
    sumRow('COMBUSTÍVEL', 'ATENÇÃO', 'não chega sem parar', 'var(--wear)');
    sumRow('PNEU INICIAL', grip.toUpperCase(), tyreNote(grip), 'var(--cyan)');
    const tip = label('DICA: macio é mais rápido, mas desgasta antes.', 'tip');
    right.appendChild(tip);

    mount(body, choices, right);

    const foot = div('sc-foot');
    mount(foot,
      btn('Voltar', 'ghost', goTrackSelect),
      div('spacer'),
      btn('🏁 INICIAR CORRIDA', 'orange start-race-btn', () => {
        const setup = buildQuickRace(trackId, 'red_comet', 20, grip, 'MicroGrooved', 100, mode);
        setup.laps = selectedLaps;
        goRace({ setup, isChampionship: false, isDaily: false });
      }),
    );
    mount(root, head, body, foot);
  });
}
