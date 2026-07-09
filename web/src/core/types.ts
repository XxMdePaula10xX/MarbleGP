// =====================================================================
// Tipos centrais do Marble GP — port fiel dos enums/SOs do Unity
// (Assets/Scripts/Core/Enums.cs + Data/*.cs)
// =====================================================================

export type TeamStyle =
  | 'Aggressive' | 'Balanced' | 'Technical' | 'Strategic' | 'Fast'
  | 'Precise' | 'Resistant' | 'Unpredictable' | 'Premium' | 'AllOut' | 'Fluid';

export type TeamBonusType =
  | 'None' | 'TopSpeed' | 'Consistency' | 'Cornering' | 'PitStops' | 'Acceleration'
  | 'WetWeather' | 'LowDamage' | 'Overtaking' | 'Development' | 'EarlyPace';

export type Personality =
  | 'Balanced' | 'Aggressive' | 'Conservative' | 'RiskTaker'
  | 'Defensive' | 'Smooth' | 'Rookie' | 'Veteran';

export type GripType = 'Soft' | 'Medium' | 'Hard' | 'Intermediate' | 'Rain';

export type SurfaceType = 'Polished' | 'MicroGrooved' | 'Textured' | 'Rough' | 'AeroSmooth';

export type RaceMode = 'Normal' | 'Push' | 'Save' | 'Attack' | 'Defend' | 'Cooldown';

export type Weather = 'Dry' | 'Cloudy' | 'Damp' | 'LightRain' | 'HeavyRain';

export type Difficulty = 'Easy' | 'Medium' | 'Hard';

export type RaceState = 'PreRace' | 'Countdown' | 'Racing' | 'Paused' | 'Finished';

export type MarbleRaceState =
  | 'OnGrid' | 'Racing' | 'EnteringPit' | 'InPit' | 'ExitingPit'
  | 'Finished' | 'Recovering' | 'Retired';

export type UpgradeType =
  | 'PitCrew'        // reduz tempo de pit stop
  | 'EnergyCoreLab'  // melhora consumo de energia
  | 'GripResearch'   // reduz desgaste dos aneis
  | 'SurfaceLab'     // melhora controle
  | 'StrategyCenter' // reduz erros / melhora previsoes
  | 'MarbleMaterial' // aumenta velocidade
  | 'AICoaching';    // reduz erros (consistencia)

// ---------------------------------------------------------------------

export interface Vec2 { x: number; y: number; }

export interface TeamData {
  teamId: string;
  teamName: string;
  primaryColor: string;   // hex #RRGGBB
  secondaryColor: string;
  teamStyle: TeamStyle;
  baseBonusType: TeamBonusType;
  pitCrewRating: number;      // 1-100
  developmentRating: number;  // 1-100
  description: string;
}

export interface MarbleDriver {
  driverId: string;
  marbleName: string;
  shortCode: string;   // sigla de 3 letras (ex.: CM1)
  number: number;
  teamId: string;
  // Atributos 1-100
  speed: number;
  acceleration: number;
  control: number;
  aggression: number;
  defense: number;
  consistency: number;
  tireManagement: number;
  energyManagement: number;
  pitSkill: number;
  wetSkill: number;
  personality: Personality;
}

export interface GripRing {
  gripId: GripType;
  compoundName: string;
  speedMultiplier: number;
  gripMultiplier: number;
  wearRate: number;          // desgaste por volta em condicoes normais
  wetPerformance: number;
  dryPerformance: number;
  energyMultiplier: number;
  fuelMultiplier: number;
  wearMultiplier: number;
  description: string;
}

export interface SurfaceProfile {
  surfaceId: SurfaceType;
  surfaceName: string;
  speedModifier: number;
  controlModifier: number;
  wearModifier: number;
  energyModifier: number;
  wetModifier: number;
  description: string;
}

export interface TrackData {
  trackId: string;
  trackName: string;
  difficulty: Difficulty;
  recommendedLaps: number;
  trackLength: number;       // calculado do perimetro dos control points
  abrasionLevel: number;     // 0.5-2 desgaste relativo
  overtakeLevel: number;     // 0-1 facilidade de ultrapassagem
  rainChance: number;        // 0-1
  pitLaneTimeLoss: number;
  description: string;
  controlPoints: Vec2[];     // plano XZ (sentido horario)
  trackWidth: number;
  checkpointEvery: number;   // a cada quantos waypoints ha um checkpoint
}

// ---- Helpers de atributo (MarbleDriverSO.AttrMultiplier) -------------

/** Converte atributo 1-100 num multiplicador em torno de 1.0 (50 => 1.0). */
export function attrMultiplier(attr: number, spread: number): number {
  return 1 + ((attr - 50) / 50) * spread;
}

export const driverMult = {
  speed: (d: MarbleDriver) => attrMultiplier(d.speed, 0.10),
  accel: (d: MarbleDriver) => attrMultiplier(d.acceleration, 0.30),
  control: (d: MarbleDriver) => attrMultiplier(d.control, 0.15),
  tireMgmt: (d: MarbleDriver) => attrMultiplier(d.tireManagement, 0.30),
  energyMgmt: (d: MarbleDriver) => attrMultiplier(d.energyManagement, 0.30),
};

// ---- Grip helpers (GripRingSO) ---------------------------------------

export function gripIsDry(g: GripType): boolean {
  return g === 'Soft' || g === 'Medium' || g === 'Hard';
}

export function gripDisplayLetter(g: GripType): string {
  switch (g) {
    case 'Soft': return 'S';
    case 'Medium': return 'M';
    case 'Hard': return 'H';
    case 'Intermediate': return 'I';
    case 'Rain': return 'W';
  }
}

/** Cor do indicador de composto (estilo F1). */
export function gripDisplayColor(g: GripType): string {
  switch (g) {
    case 'Soft': return '#e63333';
    case 'Medium': return '#f2cc33';
    case 'Hard': return '#ebebeb';
    case 'Intermediate': return '#4dcc59';
    case 'Rain': return '#4d8cf2';
  }
}

export function weatherIsWet(w: Weather): boolean {
  return w === 'Damp' || w === 'LightRain' || w === 'HeavyRain';
}

/** Desgaste efetivo por volta dado o clima (Rain desgasta 2.2x no seco). */
export function gripWearForWeather(grip: GripRing, weather: Weather): number {
  if (grip.gripId === 'Rain' && !weatherIsWet(weather)) return grip.wearRate * 2.2;
  return grip.wearRate;
}

/** Fator de performance por clima, graduado por tipo de pneu (GripRingSO.PerformanceForWeather). */
export function gripPerformanceForWeather(grip: GripRing, weather: Weather): number {
  if (gripIsDry(grip.gripId)) {
    switch (weather) {
      case 'Damp': return 0.85;
      case 'LightRain': return 0.72;
      case 'HeavyRain': return 0.58;
      default: return 1.0;
    }
  }
  if (grip.gripId === 'Intermediate') {
    switch (weather) {
      case 'Dry': return 0.9;
      case 'Cloudy': return 0.92;
      case 'Damp': return 1.0;
      case 'LightRain': return 1.0;
      case 'HeavyRain': return 0.85;
    }
  }
  // Rain
  switch (weather) {
    case 'Dry': return 0.70;
    case 'Cloudy': return 0.74;
    case 'Damp': return 0.9;
    case 'LightRain': return 0.98;
    case 'HeavyRain': return 1.05;
  }
}
