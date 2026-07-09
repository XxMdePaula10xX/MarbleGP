// =====================================================================
// Set de ícones de linha próprio (substitui os emojis). Traço = currentColor,
// então herda a cor do botão/contexto. Coeso com a estética de telemetria.
// =====================================================================

const P: Record<string, string> = {
  // bandeira quadriculada (corrida)
  flag: '<path d="M5 3v18"/><path d="M5 4h13l-2 4 2 4H5"/>',
  // troféu (campeonato)
  trophy: '<path d="M6 4h12l-1 6a5 5 0 0 1-10 0z"/><path d="M9 21h6M12 15v6"/><path d="M6 5H3v2a3 3 0 0 0 3 3M18 5h3v2a3 3 0 0 1-3 3"/>',
  // chave inglesa (garagem)
  wrench: '<path d="M14 6a3.5 3.5 0 0 0-5 5l-6 6 2 2 6-6a3.5 3.5 0 0 0 5-5l-2 2-2-2z"/>',
  // estrela (conquistas)
  star: '<path d="M12 3l2.6 5.3 5.8.8-4.2 4.1 1 5.8L12 16.8 6.8 19l1-5.8L3.6 9.1l5.8-.8z"/>',
  // sol (desafio do dia)
  sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4 12H2M22 12h-2M5 5l1.5 1.5M17.5 17.5L19 19M19 5l-1.5 1.5M6.5 17.5L5 19"/>',
  // pneu (pit / composto)
  tyre: '<circle cx="12" cy="12" r="8.5"/><circle cx="12" cy="12" r="3.2"/><path d="M12 3.5v3M12 17.5v3M3.5 12h3M17.5 12h3"/>',
  // raio (modo push / energia)
  bolt: '<path d="M13 2 4 14h7l-1 8 9-12h-7z"/>',
  // volante (corrida)
  wheel: '<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="2.4"/><path d="M12 14.4V21M9.9 10.9 4.2 7.6M14.1 10.9l5.7-3.3"/>',
  // engrenagem (config/estratégia)
  gear: '<circle cx="12" cy="12" r="3"/><path d="M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M19.1 4.9 17 7M7 17l-2.1 2.1"/>',
  // seta play
  play: '<path d="M6 4v16l13-8z"/>',
  // seta esquerda (voltar)
  back: '<path d="M15 5l-7 7 7 7"/>',
  // rádio
  radio: '<path d="M4 9h16v10H4zM7 9 17 4M8 14h.01M12 12v4"/>',
  // dado/gráfico (classificação)
  chart: '<path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/>',
  // relógio (tempo)
  clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  // compartilhar
  share: '<circle cx="6" cy="12" r="2.5"/><circle cx="18" cy="6" r="2.5"/><circle cx="18" cy="18" r="2.5"/><path d="M8.2 10.8 15.8 7.2M8.2 13.2l7.6 3.6"/>',
  // bandeira de saída (sair)
  exit: '<path d="M14 4h4v16h-4M10 8l-4 4 4 4M6 12h9"/>',
  // reticências / medalha
  medal: '<circle cx="12" cy="14" r="6"/><path d="M9 8 7 2M15 8l2-6M12 11v6M9.5 14h5"/>',
};

/** Retorna um SVG (string) de ícone de linha. currentColor no traço. */
export function icon(name: keyof typeof P | string, size = 18): string {
  const body = P[name] ?? P['star']!;
  return `<svg class="bi" viewBox="0 0 24 24" width="${size}" height="${size}" fill="none"
    stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"
    aria-hidden="true">${body}</svg>`;
}

/** Cria um elemento SVG de ícone (para inserir via appendChild). */
export function iconEl(name: string, size = 18): SVGSVGElement {
  const tmp = document.createElement('div');
  tmp.innerHTML = icon(name, size);
  return tmp.firstElementChild as SVGSVGElement;
}
