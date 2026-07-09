// Tela: Menu principal — marca + botões tonais com ícones + 1 ação primária.

import { Game } from '../../game/state';
import { Haptics } from '../../game/haptics';
import { brandLockup } from '../brand';
import { teamEmblem } from '../emblem';
import { icon } from '../icons';
import { div, el, label, mount } from '../dom';
import { show } from '../router';
import { goAchievements, goChampionship, goDaily, goGarage, goSettings, goTrackSelect } from '../flow';

interface MenuItem { label: string; icon: string; cls: string; go: () => void; }

export function menuScreen(): void {
  show('menu', root => {
    const brand = div('menu-brand');
    brand.appendChild(brandLockup());

    const team = div('panel menu-team');
    mount(team, teamEmblem('red_comet', 26), label('SUA EQUIPE', 'lb'), label(Game.profile.teamName, 'nm'));

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

    // Engrenagem de configurações (canto inferior esquerdo).
    const gear = el('button', 'menu-gear');
    gear.innerHTML = icon('gear', 20);
    gear.setAttribute('aria-label', 'Configurações');
    gear.addEventListener('click', () => { Haptics.tap(); goSettings(); });

    mount(root, brand, team, buttons, version, gear);
  });
}
