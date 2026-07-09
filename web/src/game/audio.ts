// =====================================================================
// Efeitos sonoros mínimos via WebAudio (sem assets). Gated por
// Game.settings.sound. Bips da largada, tom do GO e "whoosh" leve.
// =====================================================================

import { Game } from './state';

let ctx: AudioContext | null = null;

function ac(): AudioContext | null {
  if (!Game.settings.sound) return null;
  try {
    if (!ctx) {
      const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
      if (!Ctor) return null;
      ctx = new Ctor();
    }
    if (ctx.state === 'suspended') void ctx.resume();
    return ctx;
  } catch { return null; }
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
