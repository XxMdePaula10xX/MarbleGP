// =====================================================================
// Estado global do app — port de Core/GameManager.cs (sem cenas Unity)
// Perfil ativo, campeonato em andamento e a corrida corrente.
// =====================================================================

import type { RaceSetup } from '../sim/runtime';
import { ChampionshipManager } from './championship';
import { SaveManager, defaultProfile, defaultSettings, type PlayerProfile, type Settings } from './save';

class GameStateSingleton {
  profile: PlayerProfile = defaultProfile();
  championship: ChampionshipManager = new ChampionshipManager();
  settings: Settings = defaultSettings();

  /** Setup da corrida montada pelos menus, consumida pela tela de corrida. */
  currentRace: RaceSetup | null = null;
  /** True quando a corrida atual faz parte do campeonato. */
  raceIsChampionship = false;
  /** True quando a corrida atual é o Desafio do Dia. */
  raceIsDaily = false;

  /**
   * Carrega o estado salvo. Chamado no boot APÓS Storage.hydrate() (ver
   * main.ts), pois a hidratação do armazenamento nativo é assíncrona e o
   * Game é construído no import (síncrono). Antes disso, os campos ficam
   * com os padrões acima.
   */
  init(): void {
    this.profile = SaveManager.loadProfile();
    this.championship.loadSeason();
    this.settings = SaveManager.loadSettings();
  }

  saveProfile(): void {
    SaveManager.saveProfile(this.profile);
  }

  saveSettings(): void {
    SaveManager.saveSettings(this.settings);
  }

  /**
   * Reinicializa TODO o estado em memória para os padrões. Chamado após
   * SaveManager.deleteAll() no fluxo "Apagar progresso" — sem isto os
   * singletons cacheados regravam os dados antigos no próximo save.
   * (AchievementManager tem cache estático próprio; reinicie-o à parte.)
   */
  resetToDefaults(): void {
    this.profile = defaultProfile();
    this.championship.data = null;
    this.settings = defaultSettings();
    this.currentRace = null;
    this.raceIsChampionship = false;
    this.raceIsDaily = false;
  }
}

export const Game = new GameStateSingleton();
