// Tela: Menu principal — port de ShowMainMenu (6 botões).

import { Game } from '../../game/state';
import { btn, div, label, mount } from '../dom';
import { show } from '../router';
import { goAchievements, goChampionship, goDaily, goGarage, goTrackSelect } from '../flow';

export function menuScreen(): void {
  show('menu', root => {
    const title = label('MARBLE GP MANAGER', 'menu-title');
    const sub = label('STRATEGY RACING CHAMPIONSHIP', 'menu-sub');

    const team = div('panel menu-team');
    mount(team, label('SUA EQUIPE', 'lb'), label(Game.profile.teamName, 'nm'));

    const buttons = div('menu-buttons');
    mount(buttons,
      btn('🏁 Corrida Rápida', 'orange', goTrackSelect),
      btn('☀️ Desafio do Dia', 'grn', goDaily),
      btn('🏆 Campeonato', 'blue', goChampionship),
      btn('🔧 Garagem', 'purple', goGarage),
      btn('⭐ Conquistas', 'gold', goAchievements),
    );

    const version = label(`v1.0  ·  ${Game.profile.playerName}`, 'menu-version');
    mount(root, title, sub, team, buttons, version);
  });
}
