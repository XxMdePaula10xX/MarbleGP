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
  { g: 'Soft', nm: 'SOFT', de: 'Mais aderência' },
  { g: 'Medium', nm: 'MEDIUM', de: 'Equilibrado' },
  { g: 'Hard', nm: 'HARD', de: 'Durável' },
  { g: 'Intermediate', nm: 'INTER', de: 'Pista úmida' },
  { g: 'Rain', nm: 'RAIN', de: 'Chuva forte' },
];

const MODES: Array<{ m: RaceMode; nm: string; de: string }> = [
  { m: 'Save', nm: 'SAVE', de: 'Economiza energia' },
  { m: 'Normal', nm: 'NORMAL', de: 'Equilíbrio' },
  { m: 'Push', nm: 'PUSH', de: 'Máximo ritmo' },
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

    // Coluna esquerda: mapa.
    const left = div('panel strat-col strat-left');
    left.style.padding = '12px';
    const th = div('th-big circ-prev');
    th.style.padding = '0';
    th.appendChild(trackThumb(t.trackId, 240));
    mount(left,
      label('MAPA DO CIRCUITO', 'menu-sub'),
      th,
      label(`${t.recommendedLaps} voltas recomendadas · ${Math.round(t.trackLength)} m`, 'sub'),
    );

    // Coluna central: escolhas.
    const mid = div('strat-col');

    const tyreGroup = div('panel opt-group');
    tyreGroup.appendChild(label('PNEUS', 'gt'));
    const tyreRow = div('opt-row');
    for (const ty of TYRES) {
      const card = document.createElement('button');
      card.className = `opt-card${grip === ty.g ? ' sel' : ''}`;
      const dot = div('tyre-dot');
      dot.style.color = gripDisplayColor(ty.g);
      dot.textContent = gripDisplayLetter(ty.g);
      mount(card, dot, label(ty.nm, 't'), label(ty.de, 'd'));
      card.addEventListener('click', () => { grip = ty.g; strategyScreen(trackId); });
      tyreRow.appendChild(card);
    }
    tyreGroup.appendChild(tyreRow);

    const modeGroup = div('panel opt-group');
    modeGroup.appendChild(label('MODO DE CORRIDA', 'gt'));
    const modeRow = div('opt-row');
    for (const md of MODES) {
      const card = document.createElement('button');
      card.className = `opt-card${mode === md.m ? ' sel' : ''}`;
      mount(card, label(md.nm, 't'), label(md.de, 'd'));
      card.addEventListener('click', () => { mode = md.m; strategyScreen(trackId); });
      modeRow.appendChild(card);
    }
    modeGroup.appendChild(modeRow);

    const durGroup = div('panel opt-group');
    durGroup.appendChild(label('DURAÇÃO DA CORRIDA', 'gt'));
    const durRow = div('opt-row');
    for (const du of DURATIONS) {
      const card = document.createElement('button');
      card.className = `opt-card${selectedLaps === du.laps ? ' sel' : ''}`;
      mount(card, label(du.nm, 't'), label(du.de, 'd'));
      card.addEventListener('click', () => { selectedLaps = du.laps; strategyScreen(trackId); });
      durRow.appendChild(card);
    }
    durGroup.appendChild(durRow);

    mount(mid, tyreGroup, modeGroup, durGroup);

    // Coluna direita: resumo.
    const right = div('panel strat-summary');
    right.appendChild(label('RESUMO DA ESTRATÉGIA', 'gt'));
    const stops = selectedLaps >= 18 ? 2 : 1;
    const sumRow = (lb: string, vl: string, sb: string, color: string) => {
      const r = div('sum-row');
      const v = label(vl, 'vl');
      v.style.color = color;
      mount(r, label(lb, 'lb'), v, label(sb, 'sb'));
      right.appendChild(r);
    };
    sumRow('PARADAS PREVISTAS', String(stops), 'parada(s) no pit', 'var(--orange)');
    sumRow('COMBUSTÍVEL', 'ATENÇÃO', 'não chega ao fim sem parar', 'var(--wear)');
    sumRow('PNEU INICIAL', grip.toUpperCase(), tyreNote(grip), 'var(--cyan)');
    const tip = label('DICA: macio é mais rápido mas desgasta antes. Planeje o pit!', 'sb');
    tip.style.marginTop = 'auto';
    right.appendChild(tip);

    mount(body, left, mid, right);

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
