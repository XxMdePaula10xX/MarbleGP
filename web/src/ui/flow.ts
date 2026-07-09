// =====================================================================
// Fluxo de navegação entre telas (substitui os Show* do AppController).
// =====================================================================

import type { RaceSetup } from '../sim/runtime';
import type { RaceResult } from '../sim/systems';
import type { RaceRecorder } from '../sim/recorder';
import type { DailyChallengeDef } from '../game/daily';
import { menuScreen } from './screens/menu';
import { profileScreen } from './screens/profile';
import { trackSelectScreen } from './screens/trackselect';
import { strategyScreen } from './screens/strategy';
import { raceScreen } from './screens/race';
import { resultsScreen } from './screens/results';
import { achievementsScreen } from './screens/achievements';
import { dailyScreen } from './screens/daily';
import { championshipScreen, upgradesScreen } from './screens/championship';
import { garageScreen } from './screens/garage';
import { replayScreen } from './screens/replay';
import { settingsScreen } from './screens/settings';

export interface RaceContext {
  setup: RaceSetup;
  isChampionship: boolean;
  isDaily: boolean;
  dailyDef?: DailyChallengeDef;
  /** RNG semeado (desafio diário reproduzível). */
  seededRand?: () => number;
}

export function goProfile(): void { profileScreen(); }
export function goMenu(): void { menuScreen(); }
export function goTrackSelect(): void { trackSelectScreen(); }
export function goStrategy(trackId: string): void { strategyScreen(trackId); }
export function goRace(ctx: RaceContext): void { raceScreen(ctx); }
export function goResults(result: RaceResult, ctx: RaceContext, recorder: RaceRecorder | null): void {
  resultsScreen(result, ctx, recorder);
}
export function goAchievements(): void { achievementsScreen(); }
export function goDaily(): void { dailyScreen(); }
export function goChampionship(): void { championshipScreen(); }
export function goUpgrades(): void { upgradesScreen(); }
export function goGarage(): void { garageScreen(); }
export function goSettings(): void { settingsScreen(); }
export function goReplay(recorder: RaceRecorder, onClose: () => void): void {
  replayScreen(recorder, onClose);
}
