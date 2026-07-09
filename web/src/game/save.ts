// =====================================================================
// Persistência local em JSON — port de Save/{SaveManager,PlayerProfile,
// ChampionshipData,DailyData,AchievementData}.cs
//
// Armazenamento híbrido:
//   • localStorage — backend síncrono usado por toda a UI (mesma API do port).
//   • Capacitor Preferences — no iOS, espelha cada chave para armazenamento
//     nativo (durável e incluído no backup do iPhone). O WKWebView pode
//     limpar o localStorage sob pressão de disco; o Preferences não.
//
// Na web, Capacitor.isNativePlatform() é false: comportamento idêntico ao
// localStorage puro, sem custo. Não há login — os dados ficam no aparelho.
// =====================================================================

import { Capacitor } from '@capacitor/core';

import type { UpgradeType } from '../core/types';

// ---- Schemas (espelham os [Serializable] do Unity) --------------------

export interface PlayerProfile {
  playerName: string;
  teamName: string;
  primaryColorHex: string;
  secondaryColorHex: string;
  emblemId: string;
  difficulty: number; // 0=Easy 1=Medium 2=Hard
  created: boolean;
  marbleColorHex: string[]; // cor de cada bolinha (vazio => cor da equipe)
}

export function defaultProfile(): PlayerProfile {
  return {
    playerName: 'Player',
    teamName: 'My Team',
    primaryColorHex: '#E62020',
    secondaryColorHex: '#202020',
    emblemId: 'default',
    difficulty: 1,
    created: false,
    marbleColorHex: ['', ''],
  };
}

export interface UpgradeState { type: UpgradeType; level: number; }

export interface DriverStanding {
  driverId: string;
  teamId: string;
  points: number;
  wins: number;
  podiums: number;
  poles: number;
  fastestLaps: number;
}

export interface TeamStanding {
  teamId: string;
  points: number;
  wins: number;
  podiums: number;
}

export interface RoundResultSummary {
  trackId: string;
  winnerDriverId: string;
  winnerTeamId: string;
}

export interface ChampionshipData {
  playerTeamId: string;
  currentRound: number;
  active: boolean;
  credits: number;
  upgrades: UpgradeState[];
  calendarTrackIds: string[];
  driverStandings: DriverStanding[];
  teamStandings: TeamStanding[];
  history: RoundResultSummary[];
}

export interface DailyData {
  lastDateKey: string;    // "yyyyMMdd" do desafio refletido
  objectiveMet: boolean;
  bestPosition: number;   // 0 = não correu hoje
  bestOvertakes: number;
  attempts: number;
  streak: number;
  lastWinDateKey: string;
}

export function defaultDaily(): DailyData {
  return {
    lastDateKey: '', objectiveMet: false, bestPosition: 0,
    bestOvertakes: 0, attempts: 0, streak: 0, lastWinDateKey: '',
  };
}

export interface AchievementData {
  racesPlayed: number;
  racesWon: number;
  podiums: number;
  totalOvertakes: number;
  fastestLaps: number;
  totalPitStops: number;
  championshipsWon: number;
  bestWinStreak: number;
  currentWinStreak: number;
  maxOvertakesInRace: number;
  wonWithFuelToSpare: boolean;
  wonBothPodium: boolean;
  tracksWon: string[];
  tyresWon: string[];
  unlocked: string[];
  seasonClaimed: boolean;
}

export function defaultAchievements(): AchievementData {
  return {
    racesPlayed: 0, racesWon: 0, podiums: 0, totalOvertakes: 0, fastestLaps: 0,
    totalPitStops: 0, championshipsWon: 0, bestWinStreak: 0, currentWinStreak: 0,
    maxOvertakesInRace: 0, wonWithFuelToSpare: false, wonBothPodium: false,
    tracksWon: [], tyresWon: [], unlocked: [], seasonClaimed: false,
  };
}

// ---- Configurações -----------------------------------------------------

export interface Settings {
  showFps: boolean;   // medidor de FPS (padrão: oculto)
  sound: boolean;     // efeitos sonoros
  haptics: boolean;   // vibração
}

export function defaultSettings(): Settings {
  return { showFps: false, sound: true, haptics: true };
}

// ---- Chaves ------------------------------------------------------------

const KEY_PROFILE = 'marblegp.profile';
const KEY_CHAMPIONSHIP = 'marblegp.championship';
const KEY_ACHIEVEMENTS = 'marblegp.achievements';
const KEY_DAILY = 'marblegp.daily';
const KEY_SETTINGS = 'marblegp.settings';

const ALL_KEYS = [
  KEY_PROFILE, KEY_CHAMPIONSHIP, KEY_ACHIEVEMENTS, KEY_DAILY, KEY_SETTINGS,
] as const;

// ---- Espelho nativo (Capacitor Preferences) ---------------------------
// Carregado sob demanda para não pesar no bundle da web.
//
// IMPORTANTE: sempre desestruture { Preferences } do módulo e chame os
// métodos direto. NUNCA resolva uma Promise com o objeto Preferences (ex.:
// `async () => Preferences`): o motor de Promises faz um "thenable check"
// no proxy do plugin, o que dispara Preferences.then() e o Capacitor lança
// "Preferences.then() is not implemented on ios".
const isNative = Capacitor.isNativePlatform();

/** Espelha (fire-and-forget) uma escrita para o armazenamento nativo. */
function mirrorToNative(key: string, raw: string): void {
  if (!isNative) return;
  void (async () => {
    try {
      const { Preferences } = await import('@capacitor/preferences');
      await Preferences.set({ key, value: raw });
    } catch (e) {
      console.error(`[SaveManager] Falha ao espelhar ${key}:`, e);
    }
  })();
}

function read<T>(key: string): T | null {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : null;
  } catch {
    return null;
  }
}

function write(key: string, value: unknown): void {
  let raw: string;
  try {
    raw = JSON.stringify(value);
  } catch (e) {
    console.error(`[SaveManager] Falha ao serializar ${key}:`, e);
    return;
  }
  try {
    localStorage.setItem(key, raw);
  } catch (e) {
    console.error(`[SaveManager] Falha ao salvar ${key}:`, e);
  }
  mirrorToNative(key, raw);
}

// ---- API (SaveManager) ---------------------------------------------------

export const SaveManager = {
  saveProfile(profile: PlayerProfile): void { write(KEY_PROFILE, profile); },
  loadProfile(): PlayerProfile {
    return { ...defaultProfile(), ...(read<Partial<PlayerProfile>>(KEY_PROFILE) ?? {}) };
  },
  hasProfile(): boolean {
    try { return localStorage.getItem(KEY_PROFILE) !== null; } catch { return false; }
  },

  saveChampionship(data: ChampionshipData): void { write(KEY_CHAMPIONSHIP, data); },
  loadChampionship(): ChampionshipData | null { return read<ChampionshipData>(KEY_CHAMPIONSHIP); },

  saveAchievements(data: AchievementData): void { write(KEY_ACHIEVEMENTS, data); },
  loadAchievements(): AchievementData {
    return { ...defaultAchievements(), ...(read<Partial<AchievementData>>(KEY_ACHIEVEMENTS) ?? {}) };
  },

  saveDaily(data: DailyData): void { write(KEY_DAILY, data); },
  loadDaily(): DailyData {
    return { ...defaultDaily(), ...(read<Partial<DailyData>>(KEY_DAILY) ?? {}) };
  },

  saveSettings(data: Settings): void { write(KEY_SETTINGS, data); },
  loadSettings(): Settings {
    return { ...defaultSettings(), ...(read<Partial<Settings>>(KEY_SETTINGS) ?? {}) };
  },

  deleteAll(): void {
    for (const key of ALL_KEYS) localStorage.removeItem(key);
    if (isNative) {
      void (async () => {
        try {
          const { Preferences } = await import('@capacitor/preferences');
          await Promise.all(ALL_KEYS.map((key) => Preferences.remove({ key })));
        } catch (e) {
          console.error('[SaveManager] Falha ao limpar Preferences:', e);
        }
      })();
    }
  },
};

// ---- Boot: hidratação do armazenamento nativo -------------------------

export const Storage = {
  /**
   * No iOS, copia do Preferences nativo para o localStorage as chaves que
   * ainda não existirem localmente. Deve rodar UMA vez, antes de qualquer
   * leitura do SaveManager (ver main.ts → Game.init()).
   *
   * O localStorage é a fonte de verdade em runtime (API síncrona); o
   * Preferences é o espelho durável. Só copiamos quando o local está
   * ausente para não sobrescrever escritas recentes desta sessão.
   */
  async hydrate(): Promise<void> {
    if (!isNative) return;
    try {
      const { Preferences } = await import('@capacitor/preferences');
      await Promise.all(
        ALL_KEYS.map(async (key) => {
          if (localStorage.getItem(key) !== null) return; // já presente localmente
          const { value } = await Preferences.get({ key });
          if (value != null) localStorage.setItem(key, value);
        }),
      );
    } catch (e) {
      console.error('[Storage] Falha ao hidratar do Preferences:', e);
    }
  },
};
