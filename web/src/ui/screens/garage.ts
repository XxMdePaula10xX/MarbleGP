// Tela: Garagem — nome da equipe + cor SECUNDÁRIA (a primária é fixa da
// equipe, para as bolinhas não se confundirem na pista).

import { driversOfTeam } from '../../data/drivers';
import { teamById } from '../../data/teams';
import { Game } from '../../game/state';
import { Haptics } from '../../game/haptics';
import { teamEmblem } from '../emblem';
import { btn, div, el, label, mount } from '../dom';
import { show } from '../router';
import { goMenu } from '../flow';

// Paleta de cores SECUNDÁRIAS (detalhe/anel).
const SECONDARY = [
  '#FFFFFF', '#1A1A1A', '#E9BE5C', '#20E0E0', '#F07000',
  '#7A3FB0', '#C0241F', '#2ED760', '#C0C8D0',
];

const PLAYER_TEAM = 'red_comet';

export function garageScreen(): void {
  show('garage', root => {
    const profile = Game.profile;
    const team = teamById(PLAYER_TEAM);

    const head = div('sc-head');
    const h1 = el('h1');
    h1.textContent = 'GARAGEM';
    mount(head, h1, label('Personalize o nome e o detalhe da sua equipe.', 'sub'));

    const body = div('garage-body');

    // Identidade da equipe: emblema + cor fixa da equipe.
    const idCard = div('panel garage-card');
    const idRow = div('');
    idRow.style.cssText = 'display:flex;align-items:center;gap:14px';
    idRow.appendChild(teamEmblem(PLAYER_TEAM, 52));
    const idInfo = div('');
    mount(idInfo, label(team.teamName, 'nm'));
    const colorRow = div('');
    colorRow.style.cssText = 'display:flex;align-items:center;gap:8px;margin-top:4px';
    const chip = div('');
    chip.style.cssText = `width:16px;height:16px;border-radius:50%;background:${team.raceColor};box-shadow:0 0 8px -1px ${team.raceColor}`;
    mount(colorRow, chip, label('Cor da equipe (fixa)', 'sub'));
    idInfo.appendChild(colorRow);
    mount(idRow, idInfo);
    idCard.appendChild(idRow);
    body.appendChild(idCard);

    // Nome da equipe.
    const nameCard = div('panel garage-card');
    nameCard.appendChild(label('Nome da equipe', 'nm'));
    const nameField = el('input', 'tfield', { maxlength: 22 });
    nameField.value = profile.teamName;
    nameField.addEventListener('change', () => {
      if (nameField.value.trim()) { profile.teamName = nameField.value.trim(); Game.saveProfile(); }
    });
    nameCard.appendChild(nameField);
    body.appendChild(nameCard);

    // Cor secundária (detalhe/anel das bolinhas).
    const secCard = div('panel garage-card');
    mount(secCard, label('Cor do detalhe (secundária)', 'nm'),
      label('Aparece no anel das suas bolinhas.', 'sub'));
    const row = div('swatch-row');
    for (const hex of SECONDARY) {
      const sw = el('button', `swatch${sameColor(hex, profile.secondaryColorHex) ? ' sel' : ''}`);
      sw.style.background = hex;
      if (sameColor(hex, profile.secondaryColorHex)) sw.textContent = '✓';
      sw.addEventListener('click', () => {
        profile.secondaryColorHex = hex;
        Game.saveProfile();
        Haptics.tap();
        garageScreen();
      });
      row.appendChild(sw);
    }
    secCard.appendChild(row);

    // Preview das 2 bolinhas com a cor da equipe + detalhe escolhido.
    const prevRow = div('');
    prevRow.style.cssText = 'display:flex;gap:16px;margin-top:12px';
    for (const d of driversOfTeam(PLAYER_TEAM).slice(0, 2)) {
      const wrap = div('');
      wrap.style.cssText = 'display:flex;align-items:center;gap:9px';
      const marble = div('marble-preview');
      marble.style.background = team.raceColor;
      marble.style.borderColor = profile.secondaryColorHex;
      mount(wrap, marble, label(`${d.shortCode}`, 'sub'));
      prevRow.appendChild(wrap);
    }
    secCard.appendChild(prevRow);
    body.appendChild(secCard);

    const foot = div('sc-foot');
    mount(foot, btn('Salvar e Voltar', 'primary', () => {
      if (nameField.value.trim()) profile.teamName = nameField.value.trim();
      Game.saveProfile();
      goMenu();
    }));
    mount(root, head, body, foot);
  });
}

function sameColor(a: string, b: string): boolean {
  return a.toLowerCase() === b.toLowerCase();
}
