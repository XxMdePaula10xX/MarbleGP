// =====================================================================
// Helpers de DOM — substituem o UIFactory do Unity.
// =====================================================================

type Attrs = Record<string, string | number | boolean | undefined>;

export function el<K extends keyof HTMLElementTagNameMap>(
  tag: K, className = '', attrs: Attrs = {},
): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (className) node.className = className;
  for (const [k, v] of Object.entries(attrs)) {
    if (v === undefined || v === false) continue;
    if (k === 'text') node.textContent = String(v);
    else if (k === 'html') node.innerHTML = String(v);
    else node.setAttribute(k, String(v));
  }
  return node;
}

export function div(className = '', attrs: Attrs = {}): HTMLDivElement {
  return el('div', className, attrs);
}

export function label(text: string, className = ''): HTMLDivElement {
  const d = div(className);
  d.textContent = text;
  return d;
}

export function btn(text: string, className: string, onClick: () => void): HTMLButtonElement {
  const b = el('button', `btn ${className}`.trim());
  b.type = 'button';
  b.textContent = text;
  b.addEventListener('click', onClick);
  return b;
}

export function mount(parent: HTMLElement, ...children: Array<HTMLElement | null>): void {
  for (const c of children) if (c) parent.appendChild(c);
}

/** Barra de progresso: track + fill (retorna o fill p/ updates). */
export function pbar(parent: HTMLElement, fraction: number, color: string): HTMLElement {
  const track = div('pbar');
  const fill = el('i');
  fill.style.width = `${Math.round(Math.min(1, Math.max(0, fraction)) * 100)}%`;
  fill.style.background = color;
  track.appendChild(fill);
  parent.appendChild(track);
  return fill;
}

export function formatTime(t: number): string {
  if (t <= 0) return '--:--';
  const m = Math.floor(t / 60);
  const s = t - m * 60;
  return `${m}:${s.toFixed(3).padStart(6, '0')}`;
}

export function trim(s: string, n: number): string {
  return !s ? '' : s.length <= n ? s : s.slice(0, n);
}
