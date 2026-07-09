// Tela: Garagem — port de ShowGarage (nome da equipe, cores, bolinhas).

import { driversOfTeam } from '../../data/drivers';
import { Game } from '../../game/state';
import { btn, div, el, label, mount } from '../dom';
import { show } from '../router';
import { goMenu } from '../flow';

const PALETTE = [
  '#E62929', '#F28C1A', '#F2D926', '#33BF4D',
  '#268CE6', '#8C4DD9', '#F266B3', '#EBEBEB', '#1F1F26',
];

const PLAYER_TEAM = 'red_comet';

export function garageScreen(): void {
  show('garage', root => {
    const profile = Game.profile;

    const head = div('sc-head');
    const h1 = el('h1');
    h1.textContent = 'GARAGEM';
    mount(head, h1, label('Personalize sua equipe e bolinhas.', 'sub'));

    const body = div('garage-body');

    // Nome da equipe.
    const nameCard = div('panel garage-card');
    nameCard.appendChild(label('Nome da equipe', 'nm'));
    const nameField = el('input', 'tfield', { maxlength: 22 });
    nameField.value = profile.teamName;
    nameField.addEventListener('change', () => {
      if (nameField.value.trim()) {
        profile.teamName = nameField.value.trim();
        Game.saveProfile();
      }
    });
    nameCard.appendChild(nameField);
    body.appendChild(nameCard);

    // Cores da equipe.
    body.appendChild(colorCard('Cor primária', profile.primaryColorHex, c => {
      profile.primaryColorHex = c;
      Game.saveProfile();
      garageScreen();
    }));
    body.appendChild(colorCard('Cor secundária', profile.secondaryColorHex, c => {
      profile.secondaryColorHex = c;
      Game.saveProfile();
      garageScreen();
    }));

    // Bolinhas do jogador.
    const drivers = driversOfTeam(PLAYER_TEAM);
    drivers.slice(0, 2).forEach((d, i) => {
      const card = div('panel garage-card');
      const rowTop = div('');
      rowTop.style.cssText = 'display:flex;align-items:center;gap:12px';
      const prev = div('marble-preview');
      prev.style.background = profile.marbleColorHex[i] || profile.primaryColorHex;
      mount(rowTop, prev, label(`#${d.number}  ${d.marbleName}  (${d.shortCode})`, 'nm'));
      card.appendChild(rowTop);
      card.appendChild(label('Escolha a cor da bolinha:', 'sub'));
      card.appendChild(swatchRow(profile.marbleColorHex[i] || profile.primaryColorHex, c => {
        profile.marbleColorHex[i] = c;
        Game.saveProfile();
        garageScreen();
      }));
      body.appendChild(card);
    });

    const foot = div('sc-foot');
    mount(foot, btn('Salvar e Voltar', 'grn', () => {
      if (nameField.value.trim()) profile.teamName = nameField.value.trim();
      Game.saveProfile();
      goMenu();
    }));
    mount(root, head, body, foot);
  });
}

function colorCard(title: string, current: string, onPick: (hex: string) => void): HTMLElement {
  const card = div('panel garage-card');
  card.appendChild(label(title, 'nm'));
  card.appendChild(swatchRow(current, onPick));
  return card;
}

function swatchRow(current: string, onPick: (hex: string) => void): HTMLElement {
  const row = div('swatch-row');
  for (const hex of PALETTE) {
    const sw = el('button', `swatch${sameColor(hex, current) ? ' sel' : ''}`);
    sw.style.background = hex;
    if (sameColor(hex, current)) sw.textContent = '✓';
    sw.addEventListener('click', () => onPick(hex));
    row.appendChild(sw);
  }
  return row;
}

function sameColor(a: string, b: string): boolean {
  return a.toLowerCase() === b.toLowerCase();
}
