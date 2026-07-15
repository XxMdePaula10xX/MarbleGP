// =====================================================================
// Fluxo de pit stop pelo pit lane real — port de Race/PitStopManager.cs
// (entrada -> box -> serviço -> saída -> pista)
// =====================================================================

import type { Vec2 } from '../core/types';
import { Balance } from '../data/balance';
import { GRIPS } from '../data/grips';
import { pitTime } from './formulas';
import type { MarbleActor } from './controller';
import type { MarbleRuntime } from './runtime';
import type { EnergySystem, FuelSystem, TireWearSystem } from './systems';
import type { Track } from './track';

type Phase = 'DrivingIn' | 'Servicing' | 'DrivingOut';

interface PitJob {
  actor: MarbleActor;
  phase: Phase;
  path: Vec2[] | null; // null => serviço no lugar (fallback)
  idx: number;
  boxIdx: number;
  box: Vec2;
}

export class PitStopManager {
  private track: Track;
  private tires: TireWearSystem;
  private energy: EnergySystem;
  private fuel: FuelSystem;
  private rand: () => number;

  private active = new Map<MarbleActor, PitJob>();
  private boxCursor = 0;

  onPitCompleted: ((m: MarbleRuntime) => void) | null = null;
  onPitExit: ((actor: MarbleActor) => void) | null = null;

  constructor(
    track: Track, tires: TireWearSystem, energy: EnergySystem, fuel: FuelSystem,
    rand: () => number = Math.random,
  ) {
    this.track = track;
    this.tires = tires;
    this.energy = energy;
    this.fuel = fuel;
    this.rand = rand;
  }

  isPitting(actor: MarbleActor): boolean { return this.active.has(actor); }

  /** Chamado ao completar a volta com pit solicitado (PRD 18.1). */
  beginEntry(actor: MarbleActor): void {
    if (this.active.has(actor)) return;
    const m = actor.m;
    // Bolinha que já terminou a corrida nunca entra no pit (defesa extra).
    if (m.state === 'Finished') { m.pitRequested = false; return; }
    m.pitRequested = false;
    m.state = 'EnteringPit';
    m.pitStops++;

    const path = this.track.pitPath;

    if (!path || path.length < 3) {
      // Fallback: serviço no lugar.
      this.startService(m);
      this.active.set(actor, {
        actor, phase: 'Servicing', path: null, idx: 0, boxIdx: 0, box: actor.pos,
      });
      actor.freeze();
      return;
    }

    const boxes = this.track.pitBoxPathIndex;
    const slot = boxes.length > 0 ? this.boxCursor++ % boxes.length : 0;
    const boxIdx = slot < boxes.length ? boxes[slot]! : Math.floor(path.length / 2);
    const box = path[boxIdx]!;

    this.active.set(actor, { actor, phase: 'DrivingIn', path, idx: 0, boxIdx, box });
  }

  /** Processa todas as paradas em andamento (chamado a cada passo). */
  tick(dt: number): void {
    if (this.active.size === 0) return;
    const done: MarbleActor[] = [];

    for (const job of this.active.values()) {
      const actor = job.actor;
      const m = actor.m;

      switch (job.phase) {
        case 'DrivingIn':
          this.driveIn(job, actor, m);
          break;

        case 'Servicing':
          actor.desiredSpeed = 0;
          actor.freeze();
          m.pitTimer -= dt;
          if (m.pitTimer <= 0) {
            this.onPitCompleted?.(m);
            m.state = 'ExitingPit';
            if (!job.path) {
              actor.externalTarget = null;
              m.state = 'Racing';
              done.push(actor);
              this.onPitExit?.(actor);
            } else {
              job.phase = 'DrivingOut';
              job.idx = job.boxIdx; // continua do box até o fim do pit lane
            }
          }
          break;

        case 'DrivingOut':
          if (this.driveOut(job, actor, m)) {
            done.push(actor);
            this.onPitExit?.(actor);
          }
          break;
      }
    }

    for (const a of done) this.active.delete(a);
  }

  private driveIn(job: PitJob, actor: MarbleActor, m: MarbleRuntime): void {
    const path = job.path!;
    const t = Math.min(job.idx, job.boxIdx);
    actor.externalTarget = path[t]!;
    actor.desiredSpeed = Balance.pitLaneSpeed;

    if (job.idx < job.boxIdx) {
      if (dist(actor.pos, path[job.idx]!) < 1.4) job.idx++;
    } else {
      // Chegou na região do box: mira o ponto exato e para.
      actor.externalTarget = job.box;
      actor.desiredSpeed = Balance.pitLaneSpeed * 0.7;
      if (dist(actor.pos, job.box) < 1.0) {
        this.startService(m);
        job.phase = 'Servicing';
        actor.freeze();
      }
    }
  }

  private driveOut(job: PitJob, actor: MarbleActor, m: MarbleRuntime): boolean {
    const path = job.path!;
    if (job.idx < path.length) {
      actor.externalTarget = path[job.idx]!;
      actor.desiredSpeed = Balance.pitLaneSpeed * 1.15;
      if (dist(actor.pos, path[job.idx]!) < 1.4) job.idx++;
    }

    if (job.idx >= path.length) {
      actor.externalTarget = null;
      m.state = 'Racing';
      return true;
    }
    return false;
  }

  private startService(m: MarbleRuntime): void {
    m.state = 'InPit';

    // Serviço 1: troca de anel (reseta desgaste).
    if (m.pitChangeTires) {
      this.tires.fitNewGrip(m, GRIPS[m.pitTargetGrip]);
    }

    // Serviço 2: reabastece combustível (sempre cheio) e restaura energia
    // até o nível-alvo m.pitRefillAmount. Encher menos = pit mais curto
    // (energyRefilled alimenta pitTime). Antes ignorava o alvo e enchia a 100.
    const target = Math.min(100, Math.max(0, m.pitRefillAmount));
    const energyRefilled = Math.max(0, target - m.energy);
    this.energy.refill(m, energyRefilled);
    this.fuel.refill(m);

    // Tempo total do pit.
    m.pitTimer = pitTime(m, m.pitChangeTires, energyRefilled, this.rand);
    m.pitTotalTime = Math.max(0.1, m.pitTimer);
  }
}

function dist(a: Vec2, b: Vec2): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}
