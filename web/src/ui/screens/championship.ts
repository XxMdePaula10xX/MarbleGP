// Telas: Campeonato (hub + classificações) e Upgrades — port de
// ShowChampionshipHub / ShowUpgrades / BuildStandings.

import { AchievementManager } from '../../game/achievements';
import {
  ALL_UPGRADE_TYPES, MAX_UPGRADE_LEVEL, driverCode, driverName, teamName,
  upgradeDesc, upgradeName,
} from '../../game/championship';
import { Game } from '../../game/state';
import { teamEmblem } from '../emblem';
import { driversOfTeam } from '../../data/drivers';
import { teamById } from '../../data/teams';
import { btn, div, el, label, mount, trim } from '../dom';
import { show } from '../router';
import { goMenu, goRace, goUpgrades, goChampionship } from '../flow';

const PLAYER_TEAM = 'red_comet';

const TUTORIAL =
  'Você dirige a ESTRATÉGIA da equipe ao longo de várias etapas.\n\n' +
  '•  Cada etapa é uma corrida numa pista diferente do calendário.\n' +
  '•  Pontuação por chegada: P1 = 25, P2 = 20, P3 = 18, ...\n' +
  '•  Há classificação de PILOTOS e de EQUIPES (soma das 2 bolinhas).\n' +
  '•  Correndo você ganha CRÉDITOS para gastar em UPGRADES.\n' +
  '•  Upgrades melhoram pit, energia, desgaste, velocidade e erros.\n' +
  '•  Clique em "Correr Etapa" para disputar a próxima corrida.\n' +
  '•  O progresso é salvo automaticamente após cada etapa.';

let tutorialSeen = false;

export function championshipScreen(): void {
  const champ = Game.championship;

  show('championship', root => {
    const head = div('sc-head');
    const h1 = el('h1');
    h1.textContent = 'CAMPEONATO';
    const help = btn('?', 'blue', () => showInfo(root, 'CAMPEONATO', TUTORIAL));
    help.style.padding = '6px 14px';
    mount(head, h1, help);

    if (!champ.hasActiveSeason) {
      const boxWrap = div('');
      boxWrap.style.cssText = 'flex:1;display:grid;place-items:center';
      const box = div('panel profile-box');
      mount(box,
        label('Nenhuma temporada em andamento.', 'sub'),
        btn('Iniciar Temporada', 'grn', () => {
          champ.startNewSeason(PLAYER_TEAM);
          AchievementManager.noteSeasonStart();
          championshipScreen();
        }),
        btn('Voltar', 'ghost', goMenu),
      );
      boxWrap.appendChild(box);
      mount(root, head, boxWrap);
      if (!tutorialSeen) { tutorialSeen = true; showInfo(root, 'CAMPEONATO', TUTORIAL); }
      return;
    }

    // Cabeçalho da etapa.
    const sub = label('', 'sub');
    if (champ.isSeasonOver) {
      const top = champ.driverStandingsSorted()[0];
      const champName = top ? driverName(top.driverId) : '—';
      sub.textContent = `Temporada encerrada! Campeão: ${champName}`;
      sub.style.color = 'var(--gold)';
      // Título do jogador conta uma vez.
      const playerChamp = !!top && driversOfTeam(PLAYER_TEAM).some(d => d.driverId === top.driverId);
      AchievementManager.noteChampionResult(playerChamp);
    } else {
      sub.textContent = `Etapa ${champ.currentRound + 1}/${champ.totalRounds} — ${champ.currentTrackName()}`;
    }
    const credits = label(`Créditos: ${champ.credits}`, 'right');
    credits.style.color = 'var(--green)';
    mount(head, sub, credits);

    // Classificações.
    const body = div('champ-body');
    body.append(
      standingsPanel('PILOTOS', champ.driverStandingsSorted().map((d, i) => ({
        rank: i + 1,
        teamId: d.teamId,
        name: `${driverCode(d.driverId)}  ${driverName(d.driverId)}`,
        points: d.points, wins: d.wins,
        me: d.teamId === PLAYER_TEAM,
      }))),
      standingsPanel('EQUIPES', champ.teamStandingsSorted().map((t, i) => ({
        rank: i + 1,
        teamId: t.teamId,
        name: teamName(t.teamId),
        points: t.points, wins: t.wins,
        me: t.teamId === PLAYER_TEAM,
      }))),
    );

    const foot = div('sc-foot');
    mount(foot, btn('Voltar ao Menu', 'ghost', goMenu), btn('Upgrades', 'purple', goUpgrades), div('spacer'));
    if (!champ.isSeasonOver) {
      mount(foot, btn('Correr Etapa', 'orange', () => {
        const setup = champ.buildRoundRace();
        if (setup) goRace({ setup, isChampionship: true, isDaily: false });
      }));
    } else {
      mount(foot, btn('Nova Temporada', 'grn', () => {
        champ.startNewSeason(PLAYER_TEAM);
        AchievementManager.noteSeasonStart();
        championshipScreen();
      }));
    }
    mount(root, head, body, foot);
  });
}

function safeTeamColor(teamId: string): string {
  try { return teamById(teamId).primaryColor; } catch { return '#888'; }
}

interface StandRowData { rank: number; teamId: string; name: string; points: number; wins: number; me: boolean; }

function standingsPanel(title: string, rows: StandRowData[]): HTMLElement {
  const panel = div('panel stand-panel');
  const hd = div('hd2');
  mount(hd, label(title, ''), label('PTS · V', 'pts'));
  const box = div('stand-rows');
  for (const r of rows) {
    const row = div(`stand-row${r.rank === 1 ? ' first' : r.me ? ' me' : ''}`);
    mount(row, label(String(r.rank), 'rk'), teamEmblem(r.teamId, 18), label(trim(r.name, 26), ''), label(String(r.points), 'pt'), label(`${r.wins}V`, 'wn'));
    box.appendChild(row);
  }
  mount(panel, hd, box);
  return panel;
}

// ---------------------------------------------------------------------

export function upgradesScreen(): void {
  const champ = Game.championship;

  show('upgrades', root => {
    const head = div('sc-head');
    const h1 = el('h1');
    h1.textContent = 'UPGRADES DA EQUIPE';
    const credits = label(`Créditos: ${champ.credits}`, 'right');
    credits.style.color = 'var(--green)';
    mount(head, h1, credits);

    const list = div('upg-list');
    for (const type of ALL_UPGRADE_TYPES) {
      const level = champ.getLevel(type);
      const row = div('panel upg-row');
      const pips = div('pips');
      for (let i = 0; i < MAX_UPGRADE_LEVEL; i++) {
        const p = el('i', i < level ? 'on' : '');
        pips.appendChild(p);
      }
      mount(row, label(upgradeName(type), 'nm'), pips);
      if (champ.isMaxed(type)) {
        const mx = label('MÁX', '');
        mx.style.cssText = 'color:var(--green);font-weight:900;grid-row:1/3;align-self:center';
        row.appendChild(mx);
      } else {
        const cost = champ.upgradeCost(type);
        const b = btn(`Melhorar (${cost})`, champ.canUpgrade(type) ? 'grn buy' : 'ghost buy', () => {
          if (champ.buyUpgrade(type)) upgradesScreen();
        });
        b.disabled = !champ.canUpgrade(type);
        row.appendChild(b);
      }
      row.appendChild(label(upgradeDesc(type), 'de'));
      list.appendChild(row);
    }

    const foot = div('sc-foot');
    mount(foot, btn('Voltar', 'ghost', goChampionship));
    mount(root, head, list, foot);
  });
}

// ---------------------------------------------------------------------

function showInfo(root: HTMLElement, title: string, body: string): void {
  const overlay = div('pause-overlay');
  const box = div('panel pause-box');
  const h = el('h2');
  h.textContent = title;
  const txt = div('sub');
  txt.style.whiteSpace = 'pre-line';
  txt.textContent = body;
  mount(box, h, txt, btn('Entendi!', 'primary', () => overlay.remove()));
  overlay.appendChild(box);
  root.appendChild(overlay);
}
