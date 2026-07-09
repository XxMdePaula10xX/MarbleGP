// Tela: Configurações — toggles (FPS, som, háptica) + reset de dados.

import { Game } from '../../game/state';
import { Haptics } from '../../game/haptics';
import { SaveManager } from '../../game/save';
import { btn, div, el, label, mount } from '../dom';
import { show } from '../router';
import { goMenu, goProfile } from '../flow';

export function settingsScreen(): void {
  show('settings', root => {
    const head = div('sc-head');
    const h1 = el('h1');
    h1.textContent = 'CONFIGURAÇÕES';
    mount(head, h1);

    const body = div('vscroll');
    body.style.cssText = 'flex:1;display:flex;flex-direction:column;gap:10px';

    const toggle = (title: string, desc: string, get: () => boolean, set: (v: boolean) => void): HTMLElement => {
      const row = div('panel set-row');
      const info = div('');
      mount(info, label(title, 'set-ti'), label(desc, 'set-de'));
      const sw = el('button', 'switch');
      const sync = () => sw.classList.toggle('on', get());
      sw.innerHTML = '<i></i>';
      sw.addEventListener('click', () => { set(!get()); Game.saveSettings(); Haptics.tap(); sync(); });
      sync();
      mount(row, info, sw);
      return row;
    };

    mount(body,
      toggle('Mostrar FPS', 'Exibe o medidor de quadros na corrida (para diagnóstico).',
        () => Game.settings.showFps, v => { Game.settings.showFps = v; }),
      toggle('Vibração', 'Resposta tátil em pit, rádio, largada e batidas (só no celular).',
        () => Game.settings.haptics, v => { Game.settings.haptics = v; }),
      toggle('Som', 'Efeitos sonoros da corrida.',
        () => Game.settings.sound, v => { Game.settings.sound = v; }),
    );

    // Zona de perigo: apagar progresso.
    const danger = div('panel set-row');
    const dinfo = div('');
    mount(dinfo, label('Apagar progresso', 'set-ti'), label('Remove perfil, campeonato, conquistas e desafios.', 'set-de'));
    danger.appendChild(dinfo);
    let armed = false;
    const delB = btn('Apagar', 'red', () => {
      if (!armed) { armed = true; delB.textContent = 'Confirmar?'; setTimeout(() => { armed = false; delB.textContent = 'Apagar'; }, 3000); return; }
      SaveManager.deleteAll();
      goProfile();
    });
    danger.appendChild(delB);
    body.appendChild(danger);

    const foot = div('sc-foot');
    mount(foot, btn('Voltar', 'ghost', goMenu));
    mount(root, head, body, foot);
  });
}
