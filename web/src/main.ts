// =====================================================================
// Boot do Marble GP — substitui GameManager.Awake + AppController.Start.
// =====================================================================

import './styles/base.css';
import './styles/screens.css';
import './styles/race.css';

import { Game } from './game/state';
import { bindNotificationLifecycle } from './game/notifications';
import { monogram, wordmark } from './ui/brand';
import { initRouter } from './ui/router';
import { goMenu, goProfile } from './ui/flow';

const app = document.getElementById('app');
if (!app) throw new Error('#app não encontrado');

initRouter(app);
bindNotificationLifecycle();

// ---- Splash de boot: revelação da marca, depois entra no jogo ----
function boot(): void {
  const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
  const start = (): void => { (Game.profile.created ? goMenu : goProfile)(); };

  if (reduced) { start(); return; }

  const splash = document.createElement('div');
  splash.id = 'boot';
  const mark = document.createElement('div');
  mark.className = 'bootmark';
  const mg = monogram(84, true);
  mg.classList.add('ring');
  mark.appendChild(mg);
  const wm = wordmark(40);
  wm.style.marginTop = '20px';
  mark.appendChild(wm);
  const sub = document.createElement('div');
  sub.className = 'mono-sub';
  sub.textContent = 'Strategy Racing Championship';
  mark.appendChild(sub);
  splash.appendChild(mark);
  document.body.appendChild(splash);

  start(); // constrói a tela por baixo do splash
  window.setTimeout(() => {
    splash.classList.add('out');
    window.setTimeout(() => splash.remove(), 650);
  }, 1250);
}

boot();
