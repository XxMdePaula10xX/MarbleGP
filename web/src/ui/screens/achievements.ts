// Tela: Conquistas — port de ShowAchievements (32 conquistas, grade 2 col).

import {
  ACHIEVEMENTS, AchievementManager, achievementCurrent, achievementProgress, achievementUnlocked,
} from '../../game/achievements';
import { btn, div, label, mount, pbar } from '../dom';
import { show } from '../router';
import { goMenu } from '../flow';

export function achievementsScreen(): void {
  show('achievements', root => {
    const data = AchievementManager.data;
    const unlocked = AchievementManager.unlockedCount();

    const head = div('sc-head');
    const h1 = document.createElement('h1');
    h1.textContent = 'CONQUISTAS';
    const sub = label(`${unlocked} de ${ACHIEVEMENTS.length} desbloqueadas`, 'sub');
    sub.style.color = 'var(--gold)';
    const right = div('right');
    pbar(right, unlocked / ACHIEVEMENTS.length, 'var(--gold)').parentElement!.style.width = '160px';
    mount(head, h1, sub, right);

    const list = div('vscroll list-2col');
    list.style.flex = '1';
    for (const a of ACHIEVEMENTS) {
      const done = achievementUnlocked(a, data);
      const card = div(`panel ach-card${done ? ' done' : ''}`);
      const seal = label(done ? '✓' : '·', 'seal');
      seal.style.color = done ? 'var(--gold)' : 'var(--mut)';
      const ti = label(a.title, 'ti');
      const ct = label(`${achievementCurrent(a, data)}/${a.target}`, 'ct');
      const de = label(a.desc, 'de');
      mount(card, seal, ti, ct, de);
      pbar(card, achievementProgress(a, data), done ? 'var(--gold)' : 'var(--blue)');
      list.appendChild(card);
    }

    const foot = div('sc-foot');
    mount(foot, btn('Voltar', 'ghost', goMenu));
    mount(root, head, list, foot);
  });
}
