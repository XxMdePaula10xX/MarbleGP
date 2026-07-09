// Tela: Desafio do Dia — port de ShowDailyChallenge/StartDaily.

import { buildQuickRace } from '../../sim/quickrace';
import { seededRandom, todaysChallenge } from '../../game/daily';
import { SaveManager } from '../../game/save';
import { weatherLabel } from '../../sim/race';
import { gripDisplayLetter } from '../../core/types';
import { trackThumb } from '../../render/thumb';
import { btn, div, label, mount } from '../dom';
import { show } from '../router';
import { goMenu, goRace } from '../flow';

export function dailyScreen(): void {
  show('daily', root => {
    const def = todaysChallenge();
    const daily = SaveManager.loadDaily();

    const sameDay = daily.lastDateKey === def.dateKey;
    const met = sameDay && daily.objectiveMet;
    const attempts = sameDay ? daily.attempts : 0;
    const best = sameDay ? daily.bestPosition : 0;

    const head = div('sc-head');
    const h1 = document.createElement('h1');
    h1.textContent = 'DESAFIO DO DIA';
    const now = new Date();
    const sub = label(now.toLocaleDateString('pt-BR'), 'sub');
    sub.style.color = 'var(--green)';
    mount(head, h1, sub);

    const body = div('daily-body');

    // Card principal.
    const card = div('panel daily-card');
    const th = div('circ-prev th-big') as HTMLElement;
    th.appendChild(trackThumb(def.track.trackId, 200));
    mount(card,
      label(def.track.trackName.toUpperCase(), 'desc'),
      label(`${def.laps} voltas · ${weatherLabel(def.weather)}`, 'sub'),
      th,
      label(`Sugerido: pneu ${gripDisplayLetter(def.startGrip)} · modo ${def.startMode}`, 'sub'),
      label('OBJETIVO', 'obj'),
      label(def.description, 'desc'),
    );
    const status = label(
      met ? '✓ CUMPRIDO HOJE' : attempts > 0 ? `Tentativas hoje: ${attempts}` : 'Ainda não tentado hoje',
      'sub',
    );
    status.style.color = met ? 'var(--gold)' : 'var(--dim)';
    status.style.fontWeight = '800';
    card.appendChild(status);

    // Card lateral: streak + melhor de hoje.
    const side = div('panel daily-side');
    mount(side,
      label('SEQUÊNCIA', 'obj'),
      label(String(daily.streak), 'big'),
      label('dias seguidos', 'lb'),
      label('MELHOR HOJE', 'lb'),
      label(best > 0 ? `P${best}` : '—', 'best'),
    );

    mount(body, card, side);

    const foot = div('sc-foot');
    const play = btn(met ? 'Jogar de Novo' : 'Jogar', 'grn', () => {
      const setup = buildQuickRace(
        def.track.trackId, 'red_comet', 20, def.startGrip, 'MicroGrooved', 100, def.startMode,
      );
      setup.laps = def.laps;
      setup.weather = def.weather;
      goRace({
        setup, isChampionship: false, isDaily: true, dailyDef: def,
        seededRand: seededRandom(def.seed), // cenário reproduzível (igual p/ todos)
      });
    });
    mount(foot, btn('Voltar', 'ghost', goMenu), div('spacer'), play);
    mount(root, head, body, foot);
  });
}
