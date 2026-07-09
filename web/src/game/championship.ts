// =====================================================================
// Campeonato — port de Core/ChampionshipManager.cs (PRD 29/30)
// Calendário, pontos, classificações, créditos e upgrades da equipe.
// =====================================================================

import type { UpgradeType } from '../core/types';
import { DRIVERS } from '../data/drivers';
import { TEAMS } from '../data/teams';
import { TRACKS, trackById } from '../data/circuits';
import type { RaceResult } from '../sim/systems';
import { NEUTRAL_UPGRADES, type TeamUpgradeEffects } from '../sim/race';
import { buildQuickRace } from '../sim/quickrace';
import type { RaceSetup } from '../sim/runtime';
import {
  SaveManager,
  type ChampionshipData, type DriverStanding, type TeamStanding, type UpgradeState,
} from './save';

export const MAX_UPGRADE_LEVEL = 5;
const UPGRADE_BASE_COST = 120;

const ALL_UPGRADES: UpgradeType[] = [
  'PitCrew', 'EnergyCoreLab', 'GripResearch', 'SurfaceLab',
  'StrategyCenter', 'MarbleMaterial', 'AICoaching',
];

export class ChampionshipManager {
  data: ChampionshipData | null = null;

  get hasActiveSeason(): boolean { return this.data !== null && this.data.active; }
  get isSeasonOver(): boolean {
    return this.data !== null && this.data.currentRound >= this.data.calendarTrackIds.length;
  }
  get currentRound(): number { return this.data?.currentRound ?? 0; }
  get totalRounds(): number { return this.data?.calendarTrackIds.length ?? 0; }
  get credits(): number { return this.data?.credits ?? 0; }

  // ---- Ciclo de vida --------------------------------------------------

  /** Inicia uma nova temporada com todas as pistas disponíveis. */
  startNewSeason(playerTeamId: string): void {
    this.data = {
      playerTeamId,
      active: true,
      currentRound: 0,
      credits: 0,
      upgrades: ALL_UPGRADES.map((type): UpgradeState => ({ type, level: 0 })),
      calendarTrackIds: TRACKS.map(t => t.trackId),
      driverStandings: DRIVERS.map((d): DriverStanding => ({
        driverId: d.driverId, teamId: d.teamId,
        points: 0, wins: 0, podiums: 0, poles: 0, fastestLaps: 0,
      })),
      teamStandings: TEAMS.map((t): TeamStanding => ({
        teamId: t.teamId, points: 0, wins: 0, podiums: 0,
      })),
      history: [],
    };
    this.save();
  }

  loadSeason(): boolean {
    const data = SaveManager.loadChampionship();
    if (!data) return false;
    this.data = data;
    // Robustez contra saves antigos/corrompidos.
    this.data.calendarTrackIds ??= [];
    this.data.history ??= [];
    this.ensureUpgrades();
    this.ensureStandings();
    return this.data.active;
  }

  save(): void {
    if (this.data) SaveManager.saveChampionship(this.data);
  }

  currentTrackId(): string | null {
    if (!this.data || this.isSeasonOver) return null;
    return this.data.calendarTrackIds[this.data.currentRound] ?? null;
  }

  currentTrackName(): string {
    const id = this.currentTrackId();
    return id ? trackById(id).trackName : '—';
  }

  /** Monta o RaceSetup da etapa atual com o grid completo. */
  buildRoundRace(): RaceSetup | null {
    const trackId = this.currentTrackId();
    if (!trackId || !this.data) return null;
    return buildQuickRace(trackId, this.data.playerTeamId, Math.min(20, DRIVERS.length));
  }

  // ---- Aplicação de resultado ------------------------------------------

  applyResult(result: RaceResult): void {
    if (!this.data || !result) return;

    for (const e of result.entries) {
      const ds = this.data.driverStandings.find(x => x.driverId === e.driverId);
      if (ds) {
        ds.points += e.points;
        if (e.position === 1) ds.wins++;
        if (e.position <= 3) ds.podiums++;
        if (e.fastestLap) ds.fastestLaps++;
      }
      const ts = this.data.teamStandings.find(x => x.teamId === e.teamId);
      if (ts) {
        ts.points += e.points; // soma das 2 bolinhas
        if (e.position === 1) ts.wins++;
        if (e.position <= 3) ts.podiums++;
      }
    }

    // Créditos da equipe do jogador: participação + desempenho.
    let earned = 50;
    for (const e of result.entries.filter(x => x.isPlayer)) {
      earned += e.points * 10 + (e.position <= 3 ? 40 : 0);
    }
    this.data.credits += earned;

    const winner = result.entries.find(x => x.position === 1);
    this.data.history.push({
      trackId: this.currentTrackId() ?? '',
      winnerDriverId: winner?.driverId ?? '',
      winnerTeamId: winner?.teamId ?? '',
    });

    this.data.currentRound++;
    this.save();
  }

  // ---- Classificações ordenadas ------------------------------------------

  driverStandingsSorted(): DriverStanding[] {
    return [...(this.data?.driverStandings ?? [])]
      .sort((a, b) => b.points - a.points || b.wins - a.wins);
  }

  teamStandingsSorted(): TeamStanding[] {
    return [...(this.data?.teamStandings ?? [])]
      .sort((a, b) => b.points - a.points || b.wins - a.wins);
  }

  // ---- Upgrades de equipe ---------------------------------------------

  private ensureUpgrades(): void {
    if (!this.data) return;
    this.data.upgrades ??= [];
    for (const type of ALL_UPGRADES) {
      if (!this.data.upgrades.some(x => x.type === type)) {
        this.data.upgrades.push({ type, level: 0 });
      }
    }
  }

  private ensureStandings(): void {
    if (!this.data) return;
    this.data.driverStandings ??= [];
    this.data.teamStandings ??= [];
    for (const d of DRIVERS) {
      if (!this.data.driverStandings.some(x => x.driverId === d.driverId)) {
        this.data.driverStandings.push({
          driverId: d.driverId, teamId: d.teamId,
          points: 0, wins: 0, podiums: 0, poles: 0, fastestLaps: 0,
        });
      }
    }
    for (const t of TEAMS) {
      if (!this.data.teamStandings.some(x => x.teamId === t.teamId)) {
        this.data.teamStandings.push({ teamId: t.teamId, points: 0, wins: 0, podiums: 0 });
      }
    }
  }

  getLevel(type: UpgradeType): number {
    return this.data?.upgrades.find(x => x.type === type)?.level ?? 0;
  }

  upgradeCost(type: UpgradeType): number {
    return UPGRADE_BASE_COST * (this.getLevel(type) + 1);
  }

  isMaxed(type: UpgradeType): boolean { return this.getLevel(type) >= MAX_UPGRADE_LEVEL; }

  canUpgrade(type: UpgradeType): boolean {
    return this.data !== null && !this.isMaxed(type) && this.data.credits >= this.upgradeCost(type);
  }

  buyUpgrade(type: UpgradeType): boolean {
    if (!this.canUpgrade(type) || !this.data) return false;
    this.data.credits -= this.upgradeCost(type);
    const u = this.data.upgrades.find(x => x.type === type)!;
    u.level++;
    this.save();
    return true;
  }

  /** Efeitos agregados aplicados às bolinhas do jogador (PRD 30). */
  effects(): TeamUpgradeEffects {
    const e = { ...NEUTRAL_UPGRADES };
    if (!this.data) return e;
    for (const u of this.data.upgrades) {
      switch (u.type) {
        case 'PitCrew': e.pitTimeReduction += 0.3 * u.level; break;
        case 'EnergyCoreLab': e.energyFactor -= 0.04 * u.level; break;
        case 'GripResearch': e.wearFactor -= 0.05 * u.level; break;
        case 'MarbleMaterial': e.speedFactor += 0.015 * u.level; break;
        case 'SurfaceLab': e.controlFactor += 0.02 * u.level; break;
        case 'StrategyCenter': e.errorFactor *= Math.pow(0.94, u.level); break;
        case 'AICoaching': e.errorFactor *= Math.pow(0.92, u.level); break;
      }
    }
    e.energyFactor = Math.max(0.6, e.energyFactor);
    e.wearFactor = Math.max(0.6, e.wearFactor);
    return e;
  }
}

export const ALL_UPGRADE_TYPES = ALL_UPGRADES;

export function upgradeName(type: UpgradeType): string {
  switch (type) {
    case 'PitCrew': return 'Pit Crew';
    case 'EnergyCoreLab': return 'Energy Core Lab';
    case 'GripResearch': return 'Grip Research';
    case 'SurfaceLab': return 'Surface Lab';
    case 'StrategyCenter': return 'Strategy Center';
    case 'MarbleMaterial': return 'Marble Material';
    case 'AICoaching': return 'AI Coaching';
  }
}

export function upgradeDesc(type: UpgradeType): string {
  switch (type) {
    case 'PitCrew': return 'Reduz o tempo de pit stop (-0.3s/nível).';
    case 'EnergyCoreLab': return 'Reduz o consumo de energia (-4%/nível).';
    case 'GripResearch': return 'Reduz o desgaste dos anéis (-5%/nível).';
    case 'SurfaceLab': return 'Melhora o controle em curva (+2%/nível).';
    case 'StrategyCenter': return 'Reduz erros sob pressão (-6%/nível).';
    case 'MarbleMaterial': return 'Aumenta a velocidade (+1.5%/nível).';
    case 'AICoaching': return 'Reduz erros das bolinhas (-8%/nível).';
  }
}

export function driverName(driverId: string): string {
  return DRIVERS.find(d => d.driverId === driverId)?.marbleName ?? driverId;
}

export function driverCode(driverId: string): string {
  return DRIVERS.find(d => d.driverId === driverId)?.shortCode ?? 'MAR';
}

export function teamName(teamId: string): string {
  return TEAMS.find(t => t.teamId === teamId)?.teamName ?? teamId;
}
