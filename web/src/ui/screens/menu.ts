// Tela: Menu principal — marca + botões tonais com ícones + 1 ação primária.

import { Game } from '../../game/state';
import { Haptics } from '../../game/haptics';
import { brandLockup } from '../brand';
import { icon } from '../icons';
import { div, el, label, mount } from '../dom';
import { show } from '../router';
import { goAchievements, goChampionship, goDaily, goGarage, goTrackSelect } from '../flow';

interface MenuItem { label: string; icon: string; cls: string; go: () => void; }

export function menuScreen(): void {
  show('menu', root => {
    const brand = div('menu-brand');
    brand.appendChild(brandLockup());

    const team = div('panel menu-team');
    const dot = div('teamdot');
    dot.style.background = Game.profile.primaryColorHex;
    mount(team, dot, label('SUA EQUIPE', 'lb'), label(Game.profile.teamName, 'nm'));

    const items: MenuItem[] = [
      { label: 'Corrida Rápida', icon: 'flag', cls: 'primary', go: goTrackSelect },
      { label: 'Desafio do Dia', icon: 'sun', cls: 'ghost', go: goDaily },
      { label: 'Campeonato', icon: 'trophy', cls: 'ghost', go: goChampionship },
      { label: 'Garagem', icon: 'wrench', cls: 'ghost', go: goGarage },
      { label: 'Conquistas', icon: 'star', cls: 'ghost', go: goAchievements },
    ];

    const buttons = div('menu-buttons');
    for (const it of items) {
      const b = el('button', `btn ${it.cls} menu-btn`);
      b.type = 'button';
      b.innerHTML = `${icon(it.icon, 19)}<span class="ml">${it.label}</span>`;
      if (it.cls === 'primary') b.insertAdjacentHTML('beforeend', `<span class="mk">${icon('play', 14)}</span>`);
      b.addEventListener('click', () => { Haptics.tap(); it.go(); });
      buttons.appendChild(b);
    }

    const version = label(`v1.0 · ${Game.profile.playerName}`, 'menu-version');
    mount(root, brand, team, buttons, version);
  });
}
