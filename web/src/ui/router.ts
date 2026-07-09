// =====================================================================
// Roteador de telas — substitui o NewCanvas()/ClearMenuUI() do Unity.
// Cada tela é uma função que constrói seu DOM dentro de uma .screen.
// =====================================================================

export type ScreenBuilder = (root: HTMLElement) => void | (() => void);

let appRoot: HTMLElement | null = null;
let teardown: (() => void) | null = null;

export function initRouter(root: HTMLElement): void {
  appRoot = root;
}

/** Mostra uma tela, destruindo a anterior (e chamando seu teardown). */
export function show(name: string, builder: ScreenBuilder): void {
  if (!appRoot) throw new Error('Router não inicializado');
  teardown?.();
  teardown = null;
  appRoot.innerHTML = '';
  const screen = document.createElement('div');
  screen.className = `screen screen-${name}`;
  appRoot.appendChild(screen);
  const t = builder(screen);
  if (typeof t === 'function') teardown = t;
}
