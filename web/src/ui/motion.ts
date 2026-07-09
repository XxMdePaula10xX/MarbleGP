// =====================================================================
// Utilitários de movimento: count-up de números e animação de barras.
// Respeitam prefers-reduced-motion (aplicam o valor final na hora).
// =====================================================================

const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

/** Conta um número de 0 (ou from) até `to` em `ms`, formatando com `fmt`. */
export function countUp(
  node: HTMLElement, to: number, ms = 900,
  fmt: (v: number) => string = v => String(Math.round(v)), from = 0,
): void {
  if (reduced) { node.textContent = fmt(to); return; }
  let start: number | null = null;
  const step = (ts: number): void => {
    if (start === null) start = ts;
    const k = Math.min(1, (ts - start) / ms);
    const eased = 1 - Math.pow(1 - k, 3);
    node.textContent = fmt(from + (to - from) * eased);
    if (k < 1) requestAnimationFrame(step);
  };
  requestAnimationFrame(step);
}

/** Anima a largura de uma barra (0 → pct%) com easing. */
export function growBar(fill: HTMLElement, pct: number, ms = 900): void {
  if (reduced) { fill.style.width = `${pct}%`; return; }
  fill.style.width = '0%';
  fill.style.transition = `width ${ms}ms var(--ease)`;
  requestAnimationFrame(() => requestAnimationFrame(() => { fill.style.width = `${pct}%`; }));
}

/** Dispara `cb` quando `el` entra na viewport (uma vez). */
export function onVisible(el: Element, cb: () => void): void {
  if (reduced || !('IntersectionObserver' in window)) { cb(); return; }
  const io = new IntersectionObserver(entries => {
    for (const e of entries) {
      if (e.isIntersecting) { io.disconnect(); cb(); }
    }
  }, { threshold: 0.4 });
  io.observe(el);
}
