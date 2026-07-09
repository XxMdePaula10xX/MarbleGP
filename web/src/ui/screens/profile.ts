// Tela: Perfil (primeira execução) — primeira impressão com a marca.

import { Game } from '../../game/state';
import { Haptics } from '../../game/haptics';
import { brandLockup } from '../brand';
import { btn, div, el, label, mount } from '../dom';
import { show } from '../router';
import { goMenu } from '../flow';

export function profileScreen(): void {
  show('profile', root => {
    const brand = div('');
    brand.style.cssText = 'display:flex;justify-content:center;margin-bottom:6px';
    brand.appendChild(brandLockup());

    const box = div('panel profile-box');
    const sub = label('Crie seu perfil e assuma a estratégia da equipe.', 'sub');

    const nameField = el('input', 'tfield', { placeholder: 'Nome do jogador', maxlength: 18 });
    const teamField = el('input', 'tfield', { placeholder: 'Nome da equipe', maxlength: 22 });

    mount(box, sub, nameField, teamField,
      btn('Continuar', 'primary', () => {
        const p = Game.profile;
        p.playerName = nameField.value.trim() || 'Player';
        p.teamName = teamField.value.trim() || 'My Team';
        p.created = true;
        Game.saveProfile();
        Haptics.medium();
        goMenu();
      }),
    );
    mount(root, brand, box);
  });
}
