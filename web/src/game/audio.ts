// =====================================================================
// Efeitos sonoros mínimos via WebAudio (sem assets). Gated por
// Game.settings.sound. Bips da largada, tom do GO e "whoosh" leve.
//
// iOS exige que o AudioContext seja criado/resumido DENTRO de um gesto do
// usuário — senão fica "suspended" para sempre e o jogo sai mudo. Por isso
// unlockAudio() é chamado no primeiro toque (ver main.ts). Também suspende
// o contexto em segundo plano para poupar bateria.
// =====================================================================

import { Game } from './state';

let ctx: AudioContext | null = null;

function ensureCtx(): AudioContext | null {
  if (ctx) return ctx;
  try {
    const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctor) return null;
    ctx = new Ctor();
  } catch {
    return null;
  }
  return ctx;
}

/**
 * Desbloqueia o áudio. DEVE ser chamado dentro de um gesto do usuário
 * (pointerdown/touchend/keydown) — ver o listener único em main.ts. Cria o
 * contexto, resume e toca um buffer silencioso para "acordar" o iOS.
 */
export function unlockAudio(): void {
  const a = ensureCtx();
  if (!a) return;
  if (a.state === 'suspended') void a.resume();
  try {
    const buf = a.createBuffer(1, 1, 22050);
    const src = a.createBufferSource();
    src.buffer = buf;
    src.connect(a.destination);
    src.start(0);
  } catch { /* ok */ }
}

function ac(): AudioContext | null {
  if (!Game.settings.sound) return null;
  const a = ensureCtx();
  if (!a) return null;
  if (a.state === 'suspended') void a.resume();
  return a;
}

// Suspende/retoma o contexto conforme o app vai a segundo plano — evita
// manter o hardware de áudio ligado à toa e um teardown implícito.
if (typeof document !== 'undefined') {
  document.addEventListener('visibilitychange', () => {
    if (!ctx) return;
    if (document.visibilityState === 'hidden') void ctx.suspend();
    else if (Game.settings.sound) void ctx.resume();
  });
}

function tone(freq: number, dur: number, type: OscillatorType = 'sine', gain = 0.12): void {
  const a = ac();
  if (!a) return;
  const osc = a.createOscillator();
  const g = a.createGain();
  osc.type = type;
  osc.frequency.value = freq;
  g.gain.setValueAtTime(0, a.currentTime);
  g.gain.linearRampToValueAtTime(gain, a.currentTime + 0.01);
  g.gain.exponentialRampToValueAtTime(0.0001, a.currentTime + dur);
  osc.connect(g); g.connect(a.destination);
  osc.start();
  osc.stop(a.currentTime + dur + 0.02);
}

export const Sound = {
  /** Bip de cada luz vermelha acendendo. */
  lightOn(): void { tone(440, 0.12, 'square', 0.08); },
  /** Tom grave e forte do "lights out / GO". */
  go(): void { tone(180, 0.35, 'sawtooth', 0.16); tone(360, 0.3, 'sine', 0.1); },
  /** Clique curto de UI. */
  click(): void { tone(660, 0.05, 'triangle', 0.05); },
  /** Alerta (rádio / evento). */
  alert(): void { tone(520, 0.14, 'sine', 0.1); },
};
