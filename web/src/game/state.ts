// =====================================================================
// Estado global do app — port de Core/GameManager.cs (sem cenas Unity)
// Perfil ativo, campeonato em andamento e a corrida corrente.
// =====================================================================

import type { RaceSetup } from '../sim/runtime';
import { ChampionshipManager } from './championship';
import { SaveManager, type PlayerProfile, type Settings } from './save';

class GameStateSingleton {
  profile: PlayerProfile;
  championship: ChampionshipManager;
  settings: Settings;

  /** Setup da corrida montada pelos menus, consumida pela tela de corrida. */
  currentRace: RaceSetup | null = null;
  /** True quando a corrida atual faz parte do campeonato. */
  raceIsChampionship = false;
  /** True quando a corrida atual é o Desafio do Dia. */
  raceIsDaily = false;

  constructor() {
    this.profile = SaveManager.loadProfile();
    this.championship = new ChampionshipManager();
    this.championship.loadSeason();
    this.settings = SaveManager.loadSettings();
  }

  saveProfile(): void {
    SaveManager.saveProfile(this.profile);
  }

  saveSettings(): void {
    SaveManager.saveSettings(this.settings);
  }
}

export const Game = new GameStateSingleton();
