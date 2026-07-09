// =====================================================================
// Montagem de corrida rápida — port de Core/QuickRaceBuilder.cs
// Grid de 20 (10 equipes × 2); as 2 bolinhas da equipe do jogador são
// controladas por ele; a IA recebe pneu/modo variados por personalidade.
// =====================================================================

import type { GripType, MarbleDriver, RaceMode, SurfaceType } from '../core/types';
import { DRIVERS } from '../data/drivers';
import { TEAMS } from '../data/teams';
import { trackById } from '../data/circuits';
import type { MarbleStrategy, RaceSetup } from './runtime';

export function buildQuickRace(
  trackId: string, playerTeamId: string,
  maxMarbles = 20,
  defaultGrip: GripType = 'Medium',
  defaultSurface: SurfaceType = 'MicroGrooved',
  startEnergy = 100,
  startMode: RaceMode = 'Normal',
): RaceSetup {
  const track = trackById(trackId);
  const entries: MarbleStrategy[] = [];

  let count = 0;
  outer:
  for (const team of TEAMS) {
    for (const driver of DRIVERS.filter(d => d.teamId === team.teamId)) {
      if (count >= maxMarbles) break outer;
      const isPlayer = team.teamId === playerTeamId;
      entries.push({
        driverId: driver.driverId,
        grip: isPlayer ? defaultGrip : aiGrip(driver),
        surface: defaultSurface,
        startEnergy,
        startMode: isPlayer ? startMode : aiMode(driver),
        isPlayerControlled: isPlayer,
      });
      count++;
    }
  }

  return {
    trackId,
    laps: track.recommendedLaps,
    weather: 'Dry',
    playerTeamId,
    entries,
  };
}

/** Pneu inicial da IA conforme o perfil do piloto. */
function aiGrip(d: MarbleDriver): GripType {
  switch (d.personality) {
    case 'Aggressive':
    case 'RiskTaker': return 'Soft';   // rápido, gasta mais
    case 'Conservative':
    case 'Veteran': return 'Hard';     // durável
    case 'Smooth': return 'Medium';
    default: return 'Medium';
  }
}

/** Modo inicial da IA conforme o perfil do piloto. */
function aiMode(d: MarbleDriver): RaceMode {
  switch (d.personality) {
    case 'Aggressive':
    case 'RiskTaker': return 'Push';
    case 'Conservative': return 'Save';
    default: return 'Normal';
  }
}
