// Tela: Resultado da corrida — port de ShowResults (banner do vencedor,
// tabela, conquistas novas, avaliação do desafio diário, replay).

import { AchievementManager } from '../../game/achievements';
import { evaluateDaily, todayKey } from '../../game/daily';
import { SaveManager } from '../../game/save';
import { Game } from '../../game/state';
import type { RaceResult } from '../../sim/systems';
import type { RaceRecorder } from '../../sim/recorder';
import { teamById } from '../../data/teams';
import { Haptics } from '../../game/haptics';
import { countUp, onVisible } from '../motion';
import { icon } from '../icons';
import { btn, div, el, formatTime, label, mount, trim } from '../dom';
import { show } from '../router';
import { goChampionship, goDaily, goMenu, goReplay, goStrategy, type RaceContext } from '../flow';
import { exportResultImage } from './sharecard';

export function resultsScreen(
  result: RaceResult, ctx: RaceContext, recorder: RaceRecorder | null, skipRecord = false,
): void {
  // Registra estatísticas e conquistas novas (uma vez, ao entrar; a volta
  // do replay passa skipRecord para não contar em dobro).
  const newAchievements = skipRecord ? [] : AchievementManager.recordRace(result);

  // Campeonato: aplica resultado à classificação.
  if (!skipRecord && ctx.isChampionship && Game.championship.hasActiveSeason) {
    Game.championship.applyResult(result);
  }

  // Avalia o Desafio do Dia.
  let dailyDone = false, dailyMet = false;
  if (!skipRecord && ctx.isDaily && ctx.dailyDef) {
    const ev = evaluateDaily(ctx.dailyDef, result);
    dailyMet = ev.met;
    dailyDone = true;
    const d = SaveManager.loadDaily();
    if (d.lastDateKey !== ctx.dailyDef.dateKey) {
      d.lastDateKey = ctx.dailyDef.dateKey;
      d.objectiveMet = false; d.bestPosition = 0; d.bestOvertakes = 0; d.attempts = 0;
    }
    d.attempts++;
    if (ev.playerBestPos > 0 && (d.bestPosition === 0 || ev.playerBestPos < d.bestPosition)) {
      d.bestPosition = ev.playerBestPos;
    }
    if (ev.playerOvertakes > d.bestOvertakes) d.bestOvertakes = ev.playerOvertakes;
    if (ev.met && !d.objectiveMet) {
      d.objectiveMet = true;
      const y = new Date();
      y.setDate(y.getDate() - 1);
      d.streak = d.lastWinDateKey === todayKey(y) ? d.streak + 1 : 1;
      d.lastWinDateKey = ctx.dailyDef.dateKey;
    }
    SaveManager.saveDaily(d);
  }

  show('results', root => {
    const head = div('sc-head');
    const h1 = el('h1');
    h1.textContent = 'RESULTADO DA CORRIDA';
    h1.style.color = 'var(--gold)';
    mount(head, h1, label(`${result.trackName} · ${result.laps} voltas`, 'sub'));

    const winner = result.entries[0];
    const banner = div('panel res-winner');
    const fastest = result.entries.find(e => e.fastestLap);
    if (winner) {
      const info = div('');
      const nm = label(winner.marbleName, 'nm');
      mount(info, nm, label(winner.teamName, 'tm'));
      if (winner.fastestLap) {
        const flchip = div('fl-chip');
        flchip.innerHTML = `${icon('bolt', 12)} VOLTA MAIS RÁPIDA`;
        info.appendChild(flchip);
      }
      const stats = div('stats');
      let ptsVl: HTMLElement | null = null;
      const st = (lb: string, vl: string) => {
        const s = div('st');
        const v = label(vl, 'vl');
        mount(s, v, label(lb, 'lb'));
        stats.appendChild(s);
        return v;
      };
      st('TEMPO', formatTime(winner.totalTime));
      st('PNEU', winner.finalTyre);
      st('PITS', String(winner.pitStops));
      st('COMBUST.', `${Math.round(winner.finalFuel)}%`);
      ptsVl = st('PONTOS', `+${winner.points}`);
      mount(banner, label('1º', 'p1'), info, stats);
      // Pontuação conta de 0 até o valor quando o banner entra em cena.
      if (ptsVl) onVisible(banner, () => countUp(ptsVl!, winner.points, 900, v => `+${Math.round(v)}`));
      Haptics.success();
    }

    // Pódio 1-2-3 (colunas que sobem).
    const podium = buildPodium(result.entries.slice(0, 3));

    // Faixa: desafio diário ou conquistas novas.
    let strip: HTMLElement | null = null;
    if (dailyDone) {
      strip = div('res-banner');
      strip.textContent = dailyMet ? '✓  DESAFIO DO DIA CUMPRIDO!' : 'Desafio do dia não cumprido — tente de novo';
      strip.style.color = dailyMet ? 'var(--green)' : 'var(--orange)';
      strip.style.borderColor = dailyMet ? 'var(--green)' : 'var(--orange)';
    } else if (newAchievements.length > 0) {
      strip = div('res-banner');
      strip.textContent = newAchievements.length === 1
        ? `★  Nova conquista: ${newAchievements[0]!.title}`
        : `★  ${newAchievements.length} novas conquistas desbloqueadas!`;
      strip.style.color = 'var(--gold)';
      strip.style.borderColor = 'var(--gold)';
    }

    // Tabela.
    const tableBox = div('res-table');
    const table = el('table');
    table.innerHTML = `<thead><tr>
      <th>P</th><th>MARBLE</th><th>EQUIPE</th><th>PNEU</th><th>PIT</th>
      <th>COMB</th><th>ENER</th><th>STATUS</th><th>PTS</th></tr></thead>`;
    const tbody = el('tbody');
    for (const e of result.entries) {
      const tr = el('tr', e.position === 1 ? 'first' : e.isPlayer ? 'me' : '');
      const accent = (() => {
        try { return teamById(e.teamId).primaryColor; } catch { return '#888'; }
      })();
      tr.innerHTML = `
        <td class="num" style="border-left:3px solid ${accent}">${e.position}</td>
        <td>${trim(e.marbleName, 16)}${e.fastestLap ? ' ⚡' : ''}</td>
        <td style="color:var(--dim)">${trim(e.teamName, 18)}</td>
        <td class="num">${e.finalTyre}</td>
        <td class="num">${e.pitStops}</td>
        <td class="num" style="color:var(--fuel)">${Math.round(e.finalFuel)}</td>
        <td class="num" style="color:var(--energy)">${Math.round(e.finalEnergy)}</td>
        <td style="color:var(--dim)">${e.statusText === 'Finished' ? 'Completou' : e.statusText}</td>
        <td class="num" style="font-weight:800">${e.points}</td>`;
      tbody.appendChild(tr);
    }
    table.appendChild(tbody);
    tableBox.appendChild(table);

    const foot = div('sc-foot');
    mount(foot, btn('Voltar ao Menu', 'ghost', goMenu));
    const shareB = btn('Compartilhar', 'ghost', () => {
      Haptics.tap();
      void exportResultImage(result);
    });
    shareB.insertAdjacentHTML('afterbegin', icon('share', 16));
    mount(foot, shareB);
    if (recorder && recorder.frames.length >= 2) {
      mount(foot, btn('Ver Replay', 'purple', () => {
        goReplay(recorder, () => resultsScreenAgain(result, ctx, recorder));
      }));
    }
    mount(foot, div('spacer'));
    if (ctx.isChampionship) {
      mount(foot, btn('Classificação / Próxima', 'primary', goChampionship));
    } else if (ctx.isDaily) {
      // Volta ao Desafio do Dia, que recria o RNG semeado do zero (senão a
      // 2ª corrida continuaria o fluxo de RNG já consumido e perderia a
      // reprodutibilidade da seed do dia).
      mount(foot, btn('Jogar de Novo', 'primary', goDaily));
    } else {
      // Corrida rápida: volta à Estratégia (como no Unity), para o jogador
      // reescolher pneu/modo/duração antes de correr de novo.
      mount(foot, btn('Correr de Novo', 'primary', () => goStrategy(ctx.setup.trackId)));
    }

    mount(root, head, banner, podium, strip, tableBox, foot);
  });
}

/** Pódio 1-2-3: colunas que sobem com easing (ordem visual 2-1-3). */
function buildPodium(top3: RaceResult['entries']): HTMLElement {
  const wrap = div('res-podium');
  const heights: Record<number, string> = { 1: '100%', 2: '64%', 3: '46%' };
  const order = [top3[1], top3[0], top3[2]]; // 2º · 1º · 3º
  for (const e of order) {
    if (!e) { wrap.appendChild(div('')); continue; }
    let color = '#888';
    try { color = teamById(e.teamId).primaryColor; } catch { /* keep */ }
    const col = div(`pod-col p${e.position}`);
    const cap = div('pod-cap');
    mount(cap, label(`P${e.position}`, 'pp'), label(trim(e.marbleName, 14), 'nm'), label(`${e.points} pts`, 'pt'));
    const bar = div('pod-bar');
    const dot = div('pod-dot');
    dot.style.background = color;
    bar.appendChild(dot);
    mount(col, cap, bar);
    // sobe com easing
    bar.style.height = '0%';
    onVisible(wrap, () => { bar.style.height = heights[e.position] ?? '40%'; });
    wrap.appendChild(col);
  }
  return wrap;
}

/** Reexibe a tela de resultado sem re-registrar estatísticas (volta do replay). */
function resultsScreenAgain(result: RaceResult, ctx: RaceContext, recorder: RaceRecorder | null): void {
  resultsScreen(result, ctx, recorder, true);
}
