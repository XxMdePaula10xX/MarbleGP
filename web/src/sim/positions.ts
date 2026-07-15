// =====================================================================
// Posições ao vivo + validação de voltas por checkpoints —
// port de Race/RacePositionSystem.cs (PRD 26 / 9.3)
// =====================================================================

import type { Vec2 } from '../core/types';
import type { MarbleActor } from './controller';
import type { MarbleRuntime } from './runtime';
import type { Track } from './track';

export class RacePositionSystem {
  private track: Track;
  private checkpointRadius: number;
  private refLapTime: number;
  private nextCheckpoint = new Map<MarbleRuntime, number>();

  constructor(track: Track, baseSpeed: number) {
    this.track = track;
    this.checkpointRadius = Math.max(2.2, track.data.trackWidth * 0.5);
    this.refLapTime = track.data.trackLength / Math.max(1, baseSpeed);
  }

  register(m: MarbleRuntime): void {
    this.nextCheckpoint.set(m, 1 % Math.max(1, this.track.checkpointCount));
  }

  /**
   * Resincroniza o próximo checkpoint após um pit (a bolinha pulou alguns
   * no pit lane). Ajusta para o checkpoint logo à frente da posição atual.
   */
  resyncCheckpoint(actor: MarbleActor): void {
    const n = this.track.checkpointCount;
    if (n === 0) return;
    const arc = this.track.arcFraction(actor.pos);
    let idx = Math.round(arc * n) % n;
    // O pit inicia ao FECHAR uma volta; se o arco caiu na linha (idx==0), o
    // próximo não pode ser 0 — senão contaria volta-fantasma.
    if (idx === 0) idx = 1 % n;
    this.nextCheckpoint.set(actor.m, idx);
    actor.m.currentCheckpoint = (idx - 1 + n) % n;
  }

  /**
   * Atualiza voltas via sequência de checkpoints. Uma volta só conta ao
   * cruzar todos na ordem e voltar ao checkpoint 0.
   * @returns true se a bolinha COMPLETOU uma volta neste passo.
   */
  updateLap(actor: MarbleActor, totalLaps: number): boolean {
    const m = actor.m;
    if (this.track.checkpointCount === 0) return false;
    if (m.state === 'Finished') return false;

    const next = this.nextCheckpoint.get(m);
    if (next === undefined) return false;
    const cpPos = this.track.checkpoints[next]!;
    const d = Math.hypot(m.x - cpPos.x, m.y - cpPos.y);

    if (d <= this.checkpointRadius) {
      m.currentCheckpoint = next;
      this.nextCheckpoint.set(m, (next + 1) % this.track.checkpointCount);

      // Validar o checkpoint 0 (linha de largada) fecha uma volta.
      if (next === 0 && m.completedLaps < totalLaps && m.totalTime > 1) {
        return this.completeLap(m, totalLaps);
      }
    }
    return false;
  }

  private completeLap(m: MarbleRuntime, totalLaps: number): boolean {
    m.completedLaps++;
    if (m.currentLapTime < m.bestLapTime) m.bestLapTime = m.currentLapTime;
    m.currentLapTime = 0;
    if (m.completedLaps >= totalLaps) {
      m.state = 'Finished';
      m.pitRequested = false; // terminou: cancela qualquer pit pendente
    }
    return true;
  }

  /** Recalcula raceProgress e ordena o campo, setando position (1-based). */
  updatePositions(field: MarbleActor[]): void {
    const n = this.track.checkpointCount;
    if (n === 0) return;

    for (const actor of field) {
      const m = actor.m;
      // Progresso por CHECKPOINTS (robusto): ordem grosseira = checkpoints
      // passados; ordem fina = proximidade do próximo checkpoint.
      const cp = Math.min(Math.max(m.currentCheckpoint, 0), n - 1);
      const nx = this.nextCheckpoint.get(m);
      const next = nx !== undefined ? Math.min(Math.max(nx, 0), n - 1) : (cp + 1) % n;

      const nextPos: Vec2 = this.track.checkpoints[next]!;
      const cpPos: Vec2 = this.track.checkpoints[cp]!;
      const segLen = Math.hypot(nextPos.x - cpPos.x, nextPos.y - cpPos.y);
      const distToNext = Math.hypot(nextPos.x - m.x, nextPos.y - m.y);
      const frac = segLen > 0.01 ? 1 - distToNext / segLen : 0;

      m.raceProgress = m.completedLaps + (cp + frac) / n;
    }

    field.sort((a, b) => {
      const ma = a.m, mb = b.m;
      const fa = ma.state === 'Finished', fb = mb.state === 'Finished';
      if (fa && fb) {
        if (ma.completedLaps !== mb.completedLaps) return mb.completedLaps - ma.completedLaps;
        return ma.totalTime - mb.totalTime;
      }
      if (fa !== fb) return fa ? -1 : 1;
      return mb.raceProgress - ma.raceProgress;
    });

    // Estabilização anti-flicker: bolinhas lado a lado têm arco quase igual;
    // uma só toma a posição quando está CLARAMENTE à frente.
    const hysteresis = 0.0035;
    for (let pass = 0; pass < field.length; pass++) {
      let swapped = false;
      for (let i = 0; i + 1 < field.length; i++) {
        const ma = field[i]!.m, mb = field[i + 1]!.m;
        if (ma.state === 'Finished' || mb.state === 'Finished') continue;
        if (mb.position !== 0 && ma.position > mb.position
          && Math.abs(ma.raceProgress - mb.raceProgress) < hysteresis) {
          const tmp = field[i]!;
          field[i] = field[i + 1]!;
          field[i + 1] = tmp;
          swapped = true;
        }
      }
      if (!swapped) break;
    }

    const leader = field.length > 0 ? field[0]!.m : null;
    for (let i = 0; i < field.length; i++) {
      const newPos = i + 1;
      const m = field[i]!.m;
      // NÃO conta ultrapassagem aqui: ganho de posição inclui rival em pit,
      // batida ou flicker de empate. A estatística m.overtakes é alimentada
      // pela detecção robusta em RaceManager.detectOvertakes().
      m.position = newPos;

      if (!leader || m === leader) m.gapToLeader = 0;
      else if (m.state === 'Finished' && leader.state === 'Finished')
        m.gapToLeader = m.totalTime - leader.totalTime;
      else
        m.gapToLeader = Math.max(0, (leader.raceProgress - m.raceProgress) * this.refLapTime);
    }
  }
}
