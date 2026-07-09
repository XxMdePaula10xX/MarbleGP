// =====================================================================
// Estado mutável de uma bolinha em corrida — port de Systems/MarbleRuntime.cs
// =====================================================================

import type {
  GripRing, GripType, MarbleDriver, MarbleRaceState, RaceMode,
  SurfaceProfile, SurfaceType, TeamData, Weather,
} from '../core/types';

/** Estratégia inicial de uma bolinha (Core/RaceConfig.cs → MarbleStrategy). */
export interface MarbleStrategy {
  driverId: string;
  grip: GripType;
  surface: SurfaceType;
  startEnergy: number;       // carga inicial (default 100)
  startMode: RaceMode;
  isPlayerControlled: boolean;
}

/** Configuração completa de uma corrida (Core/RaceConfig.cs). */
export interface RaceSetup {
  trackId: string;
  laps: number;
  weather: Weather;
  playerTeamId: string;
  entries: MarbleStrategy[];
}

export class MarbleRuntime {
  driver: MarbleDriver;
  team: TeamData;
  strategy: MarbleStrategy;
  isPlayer: boolean;

  // Equipamento atual (pode mudar no pit)
  grip: GripRing;
  surface: SurfaceProfile;
  mode: RaceMode = 'Normal';

  // Rádio do box: modo temporário aplicado por decisão do jogador.
  radioActive = false;
  radioModeTimer = 0;
  radioPrevMode: RaceMode = 'Normal';

  // Estado dinâmico
  wear = 0;        // 0-100
  energy = 100;    // 0-100 bateria de performance
  fuel = 100;      // 0-100 autonomia, só volta no pit
  damage = 0;      // 0-100

  get fuelEmpty(): boolean { return this.fuel <= 0; }

  // Eventos de corrida
  recoverTimer = 0;   // recuperação após batida forte
  coreFailTimer = 0;  // falha de núcleo (dreno extra de energia)

  // Pós-bandeirada
  finishHandled = false;
  finishCoastTimer = 0;

  // Dificuldade da IA (1 = neutro / jogador)
  aiSpeedMult = 1;
  aiErrorMult = 1;
  aiPitQuality = 1;

  // Progresso de corrida
  completedLaps = 0;
  currentCheckpoint = 0;
  raceProgress = 0;
  startLineCrossed = false;
  position = 0;        // 1-based
  gapToLeader = 0;     // segundos atrás do líder

  state: MarbleRaceState = 'OnGrid';

  // Estatísticas
  pitStops = 0;
  overtakes = 0;
  bestLapTime = Number.MAX_VALUE;
  currentLapTime = 0;
  totalTime = 0;

  // Pit
  pitRequested = false;
  pitTimer = 0;
  pitTotalTime = 1;
  pitTargetGrip: GripType = 'Medium';
  pitChangeTires = true;
  pitRefillAmount = 60;

  // Efeitos dos upgrades de equipe (neutros por padrão; só bolinhas do jogador)
  upgPitReduction = 0;
  upgEnergyFactor = 1;
  upgWearFactor = 1;
  upgSpeedFactor = 1;
  upgControlFactor = 1;
  upgErrorFactor = 1;

  // ---- Posição no mundo (substitui o Transform do Unity) ----
  x = 0;
  y = 0;
  heading = 0;        // radianos
  speed = 0;          // velocidade atual (unidades/s)
  laneOffset = 0;     // deslocamento lateral da linha ideal
  trackDist = 0;      // distância percorrida ao longo da volta (0..trackLength)
  trail: Array<{ x: number; y: number }> = [];

  constructor(
    driver: MarbleDriver, team: TeamData, strategy: MarbleStrategy,
    grip: GripRing, surface: SurfaceProfile,
  ) {
    this.driver = driver;
    this.team = team;
    this.strategy = strategy;
    this.isPlayer = strategy.isPlayerControlled;
    this.grip = grip;
    this.surface = surface;
    this.mode = strategy.startMode;
    this.energy = strategy.startEnergy;
  }

  get displayName(): string { return this.driver.marbleName; }
  get teamName(): string { return this.team.teamName; }
  get teamPrimary(): string { return this.team.primaryColor; }
  get teamSecondary(): string { return this.team.secondaryColor; }
}
