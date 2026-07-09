// Tela: Perfil (primeira execução) — port de ShowProfileScreen.

import { Game } from '../../game/state';
import { btn, div, el, mount } from '../dom';
import { show } from '../router';
import { goMenu } from '../flow';

export function profileScreen(): void {
  show('profile', root => {
    const box = div('panel profile-box');
    const h = el('h1'); h.textContent = 'MARBLE GP MANAGER';
    const sub = div('sub'); sub.textContent = 'Criar Perfil';

    const nameField = el('input', 'tfield', { placeholder: 'Nome do jogador', maxlength: 18 });
    const teamField = el('input', 'tfield', { placeholder: 'Nome da equipe', maxlength: 22 });

    mount(box, h, sub, nameField, teamField,
      btn('Continuar', 'grn', () => {
        const p = Game.profile;
        p.playerName = nameField.value.trim() || 'Player';
        p.teamName = teamField.value.trim() || 'My Team';
        p.created = true;
        Game.saveProfile();
        goMenu();
      }),
    );
    root.appendChild(box);
  });
}
