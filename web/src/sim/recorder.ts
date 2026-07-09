// =====================================================================
// Gravador de replay — port de Race/RaceRecorder.cs
// Guarda posições amostradas (~12.5 Hz) + destaques; não re-simula.
// =====================================================================

import type { MarbleActor } from './controller';
import type { RaceManager } from './race';

export type HighlightKind = 'Overtake' | 'Crash' | 'Finish' | 'SafetyMarble';

export interface ReplayHighlight {
  time: number;
  focusIndex: number; // bolinha a seguir (índice fixo no snapshot)
  label: string;
  kind: HighlightKind;
}

/** Um keyframe: posição (x,y) de cada bolinha, na ordem fixa do snapshot. */
export interface ReplayFrame { pos: Array<{ x: number; y: number }>; }

export class RaceRecorder {
  static readonly INTERVAL = 0.08; // ~12.5 amostras/s

  duration = 0;
  readonly marbles: MarbleActor[];
  readonly frames: ReplayFrame[] = [];
  readonly highlights: ReplayHighlight[] = [];

  readonly race: RaceManager;
  private t = 0;
  private sample = 0;
  private recording = false;

  constructor(race: RaceManager) {
    this.race = race;
    this.marbles = [...race.field]; // ordem fixa (snapshot)

    const prevStarted = race.onRaceStarted;
    race.onRaceStarted = () => { this.recording = true; prevStarted?.(); };

    const prevEvent = race.onRaceEvent;
    race.onRaceEvent = msg => { this.onEvent(msg); prevEvent?.(msg); };

    const prevFinished = race.onRaceFinished;
    race.onRaceFinished = r => { this.captureFrame(); this.recording = false; prevFinished?.(r); };
  }

  /** Chame a cada frame do jogo (dt em segundos). */
  tick(dt: number): void {
    if (!this.recording) return;
    this.t += dt;
    this.duration = this.t;
    this.sample -= dt;
    if (this.sample <= 0) {
      this.sample = RaceRecorder.INTERVAL;
      this.captureFrame();
    }
  }

  private captureFrame(): void {
    this.frames.push({
      pos: this.marbles.map(a => ({ x: a.m.x, y: a.m.y })),
    });
  }

  private onEvent(msg: string): void {
    if (!this.recording || this.highlights.length >= 24) return;
    let kind: HighlightKind;
    if (msg.includes('ultrapassou')) kind = 'Overtake';
    else if (msg.includes('bateu forte')) kind = 'Crash';
    else if (msg.includes('Safety Marble na')) kind = 'SafetyMarble';
    else if (msg.includes('cruzou a linha em 1º') || msg.includes('venceu')) kind = 'Finish';
    else return;

    this.highlights.push({
      time: this.t,
      focusIndex: this.focusFor(msg),
      label: clean(msg),
      kind,
    });
  }

  private focusFor(msg: string): number {
    for (let i = 0; i < this.marbles.length; i++) {
      const nm = this.marbles[i]!.m.displayName;
      if (nm && msg.includes(nm)) return i;
    }
    const leader = this.race.leader;
    const li = leader ? this.marbles.indexOf(leader) : 0;
    return li >= 0 ? li : 0;
  }
}

function clean(m: string): string {
  let i = 0;
  while (i < m.length && !/[\p{L}\p{N}]/u.test(m[i]!)) i++;
  return i < m.length ? m.slice(i) : m;
}
