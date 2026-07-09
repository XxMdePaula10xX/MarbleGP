// =====================================================================
// Rádio do box — port de Race/TeamRadioSystem.cs
// Decisões táticas rápidas oferecidas ao jogador durante a corrida.
// =====================================================================

import type { RaceMode } from '../core/types';
import type { MarbleActor } from './controller';

export type RadioTone = 'Attack' | 'Defend' | 'Save' | 'Hold';

export interface RadioOption {
  label: string;
  mode: RaceMode;
  hold: boolean;  // mantém o modo atual (sem efeito)
  tone: RadioTone;
}

export interface RadioDecision {
  actor: MarbleActor;
  prompt: string;
  options: RadioOption[];
  duration: number; // tempo para decidir (s)
}

const EFFECT_DURATION = 18;

export class TeamRadioSystem {
  private cooldown = 28; // primeira chamada ~28s após o início
  private rand: () => number;

  constructor(rand: () => number = Math.random) {
    this.rand = rand;
  }

  get effectSeconds(): number { return EFFECT_DURATION; }

  /** Avalia se é hora de uma decisão; devolve uma ou null. */
  tick(dt: number, players: MarbleActor[], totalLaps: number): RadioDecision | null {
    this.cooldown -= dt;
    if (this.cooldown > 0 || players.length === 0) return null;

    // Sorteia UNIFORMEMENTE entre as bolinhas do jogador elegíveis
    // (reservoir sampling, 1 item) — ambas recebem decisões na corrida.
    let pick: MarbleActor | null = null;
    let eligible = 0;
    for (const p of players) {
      const m = p.m;
      const nearEnd = m.completedLaps >= totalLaps - 1;
      if (m.state !== 'Racing' || nearEnd) continue;
      eligible++;
      if (Math.floor(this.rand() * eligible) === 0) pick = p;
    }
    if (!pick) {
      this.cooldown = 12;
      return null;
    }

    this.cooldown = 45 + this.rand() * 30;
    return this.build(pick);
  }

  private build(actor: MarbleActor): RadioDecision {
    const m = actor.m;
    const options: RadioOption[] = [];
    let prompt: string;

    if (m.wear > 65) {
      prompt = `${m.displayName}: pneus castigados. Plano?`;
      options.push(opt('FORÇAR', 'Attack', 'Attack'), opt('POUPAR', 'Save', 'Save'), hold());
    } else if (m.fuel < 45) {
      prompt = `${m.displayName}: combustível ficando curto.`;
      options.push(opt('ECONOMIZAR', 'Save', 'Save'), hold());
    } else if (m.position === 1) {
      prompt = `${m.displayName} LIDERA! Como administrar?`;
      options.push(opt('ABRIR', 'Push', 'Attack'), opt('CONTROLAR', 'Defend', 'Defend'), hold());
    } else {
      prompt = `${m.displayName} em P${m.position}. Partir pra cima?`;
      options.push(opt('ATACAR', 'Attack', 'Attack'), opt('POUPAR', 'Save', 'Save'), hold());
    }

    return { actor, prompt, options, duration: 8 };
  }
}

function opt(label: string, mode: RaceMode, tone: RadioTone): RadioOption {
  return { label, mode, hold: false, tone };
}

function hold(): RadioOption {
  return { label: 'MANTER', mode: 'Normal', hold: true, tone: 'Hold' };
}
