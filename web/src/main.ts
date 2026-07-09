// =====================================================================
// Boot do Marble GP — substitui GameManager.Awake + AppController.Start.
// =====================================================================

import './styles/base.css';
import './styles/screens.css';
import './styles/race.css';

import { Game } from './game/state';
import { Storage } from './game/save';
import { bindNotificationLifecycle } from './game/notifications';
import { monogram, wordmark } from './ui/brand';
import { initRouter } from './ui/router';
import { goMenu, goProfile } from './ui/flow';

const app = document.getElementById('app');
if (!app) throw new Error('#app não encontrado');

// ---- Rede de segurança em device ------------------------------------
// No TestFlight não há console acessível. Duas superfícies de erro:
//  • showFatal: overlay cheio, usado só por falha de uma ETAPA do boot
//    (indica que o jogo pode não ter montado).
//  • logBanner: faixa pequena no rodapé para erros globais assíncronos
//    (não cobre o app — se ele funciona atrás, dá pra ver e jogar).
function detail(err: unknown): string {
  if (err instanceof Error) {
    return err.message + (err.stack ? `\n${err.stack}` : '');
  }
  if (err && typeof err === 'object') {
    try { return JSON.stringify(err); } catch { /* fallthrough */ }
  }
  return String(err);
}

function showFatal(label: string, err: unknown): void {
  let box = document.getElementById('fatal-overlay');
  if (!box) {
    box = document.createElement('pre');
    box.id = 'fatal-overlay';
    box.style.cssText =
      'position:fixed;inset:0;z-index:99999;margin:0;padding:16px;' +
      'overflow:auto;background:#160b0b;color:#ffb4b4;' +
      'font:11px/1.45 ui-monospace,Menlo,monospace;white-space:pre-wrap;' +
      '-webkit-user-select:text;user-select:text;';
    document.body.appendChild(box);
  }
  box.textContent += `[${label}] ${detail(err)}\n\n`;
}

function logBanner(label: string, err: unknown): void {
  let box = document.getElementById('err-banner');
  if (!box) {
    box = document.createElement('div');
    box.id = 'err-banner';
    box.style.cssText =
      'position:fixed;left:0;right:0;bottom:0;z-index:99998;max-height:38%;' +
      'overflow:auto;margin:0;padding:8px 12px;background:rgba(60,20,20,.92);' +
      'color:#ffc9c9;font:10px/1.4 ui-monospace,Menlo,monospace;' +
      'white-space:pre-wrap;-webkit-user-select:text;user-select:text;';
    box.addEventListener('click', () => box?.remove()); // toque descarta
    document.body.appendChild(box);
  }
  box.textContent += `[${label}] ${detail(err)}\n`;
}

window.addEventListener('error', (e) => logBanner('error', e.error ?? e.message));
window.addEventListener('unhandledrejection', (e) => {
  logBanner('unhandledrejection', (e as PromiseRejectionEvent).reason);
});

// ---- Splash de boot: revelação da marca, depois entra no jogo ----
function boot(): void {
  const reduced =
    window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
  const start = (): void => {
    (Game.profile.created ? goMenu : goProfile)();
  };

  if (reduced) {
    start();
    return;
  }

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

// ---- Sequência de boot, resiliente -----------------------------------
// Cada etapa é isolada: uma falha (ex.: plugin nativo) não impede o jogo
// de renderizar. A hidratação nativa tem timeout para nunca travar o boot.
async function main(): Promise<void> {
  try {
    initRouter(app!);
  } catch (e) {
    showFatal('initRouter', e);
  }
  try {
    bindNotificationLifecycle();
  } catch (e) {
    showFatal('notifications', e);
  }
  try {
    await Promise.race([
      Storage.hydrate(),
      new Promise<void>((res) => window.setTimeout(res, 2500)),
    ]);
  } catch (e) {
    showFatal('hydrate', e);
  }
  try {
    Game.init();
  } catch (e) {
    showFatal('Game.init', e);
  }
  try {
    boot();
  } catch (e) {
    showFatal('boot', e);
  }
}

void main();
