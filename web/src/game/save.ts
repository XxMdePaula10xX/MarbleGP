// =====================================================================
// Persistência local em JSON — port de Save/{SaveManager,PlayerProfile,
// ChampionshipData,DailyData,AchievementData}.cs
// localStorage no lugar de Application.persistentDataPath (funciona no
// navegador e no WKWebView do Capacitor, onde é persistente).
// =====================================================================

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

function read<T>(key: string): T | null {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : null;
  } catch {
    return null;
  }
}

function write(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch (e) {
    console.error(`[SaveManager] Falha ao salvar ${key}:`, e);
  }
}

// ---- API (SaveManager) ---------------------------------------------------

export const SaveManager = {
  saveProfile(profile: PlayerProfile): void { write(KEY_PROFILE, profile); },
  loadProfile(): PlayerProfile {
    return { ...defaultProfile(), ...(read<Partial<PlayerProfile>>(KEY_PROFILE) ?? {}) };
  },
  hasProfile(): boolean { return localStorage.getItem(KEY_PROFILE) !== null; },

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
    localStorage.removeItem(KEY_PROFILE);
    localStorage.removeItem(KEY_CHAMPIONSHIP);
    localStorage.removeItem(KEY_ACHIEVEMENTS);
    localStorage.removeItem(KEY_DAILY);
    localStorage.removeItem(KEY_SETTINGS);
  },
};
