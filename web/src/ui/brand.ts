// =====================================================================
// Identidade de marca: monograma orbital (bolinha + anel) e wordmark.
// Reutilizado no menu, boot/splash e telas.
// =====================================================================

import { div, el } from './dom';

/** Monograma orbital em SVG. spin=true anima a órbita externa. */
export function monogram(size = 40, spin = false): SVGSVGElement {
  const tmp = document.createElement('div');
  tmp.innerHTML = `
    <svg class="monogram" viewBox="0 0 40 40" width="${size}" height="${size}" aria-hidden="true">
      <g class="${spin ? 'spin' : ''}">
        <circle cx="20" cy="20" r="17" fill="none" stroke="#22e4d4" stroke-width="1.4" opacity=".45"/>
        <circle cx="20" cy="3" r="2.2" fill="#e9be5c"/>
      </g>
      <circle cx="20" cy="20" r="9" fill="none" stroke="#e9be5c" stroke-width="1.5"/>
      <circle cx="20" cy="20" r="3.4" fill="#22e4d4"/>
    </svg>`;
  return tmp.firstElementChild as SVGSVGElement;
}

/** Wordmark "MARBLE GP" (GP em ouro). */
export function wordmark(fontSize = 34): HTMLElement {
  const w = div('wordmark');
  w.style.fontSize = `${fontSize}px`;
  w.innerHTML = 'MARBLE<span class="gp">GP</span>';
  return w;
}

/** Lockup completo: monograma + wordmark + tagline mono. */
export function brandLockup(): HTMLElement {
  const box = div('');
  box.style.cssText = 'display:flex;align-items:center;gap:14px;justify-content:center';
  const wm = div('');
  wm.style.cssText = 'display:flex;flex-direction:column;gap:2px';
  wm.appendChild(wordmark(30));
  const tag = el('div', 'mono');
  tag.style.cssText = 'font-size:10px;letter-spacing:.28em;color:var(--mut);text-transform:uppercase';
  tag.textContent = 'Strategy Racing Championship';
  wm.appendChild(tag);
  box.append(monogram(46, true), wm);
  return box;
}
