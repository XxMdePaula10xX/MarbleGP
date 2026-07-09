// =====================================================================
// Conquistas — port de Core/AchievementManager.cs (PRD 32)
// 32 conquistas derivadas de contadores acumulados; persiste via save.
// =====================================================================

import type { RaceResult } from '../sim/systems';
import { SaveManager, type AchievementData } from './save';

export interface Achievement {
  id: string;
  category: string;
  title: string;
  desc: string;
  target: number;
  value: (d: AchievementData) => number;
}

const B = (b: boolean) => (b ? 1 : 0);

function a(
  id: string, category: string, title: string, desc: string,
  target: number, value: (d: AchievementData) => number,
): Achievement {
  return { id, category, title, desc, target, value };
}

/** Catálogo das 32 conquistas (idêntico ao Unity). */
export const ACHIEVEMENTS: Achievement[] = [
  // Vitórias
  a('win_1', 'Vitórias', 'Primeira Vitória', 'Vença a sua primeira corrida.', 1, d => d.racesWon),
  a('win_3', 'Vitórias', 'Tri de Vitórias', 'Vença 3 corridas.', 3, d => d.racesWon),
  a('win_5', 'Vitórias', 'Pé no Acelerador', 'Vença 5 corridas.', 5, d => d.racesWon),
  a('win_10', 'Vitórias', 'Vencedor Nato', 'Vença 10 corridas.', 10, d => d.racesWon),
  a('win_25', 'Vitórias', 'Dominante', 'Vença 25 corridas.', 25, d => d.racesWon),
  a('win_50', 'Vitórias', 'Lenda das Pistas', 'Vença 50 corridas.', 50, d => d.racesWon),
  // Sequências
  a('streak_3', 'Sequências', 'Embalado', 'Vença 3 corridas seguidas.', 3, d => d.bestWinStreak),
  a('streak_5', 'Sequências', 'Imparável', 'Vença 5 corridas seguidas.', 5, d => d.bestWinStreak),
  // Pódios
  a('pod_1', 'Pódios', 'Ao Pódio', 'Termine entre os 3 primeiros.', 1, d => d.podiums),
  a('pod_10', 'Pódios', 'Frequentador do Pódio', 'Suba ao pódio 10 vezes.', 10, d => d.podiums),
  a('pod_25', 'Pódios', 'Habitué do Champanhe', 'Suba ao pódio 25 vezes.', 25, d => d.podiums),
  // Corridas disputadas
  a('play_1', 'Carreira', 'Estreia', 'Dispute a sua primeira corrida.', 1, d => d.racesPlayed),
  a('play_10', 'Carreira', 'Veterano', 'Dispute 10 corridas.', 10, d => d.racesPlayed),
  a('play_50', 'Carreira', 'Profissional', 'Dispute 50 corridas.', 50, d => d.racesPlayed),
  a('play_100', 'Carreira', 'Centurião', 'Dispute 100 corridas.', 100, d => d.racesPlayed),
  // Campeonatos
  a('champ_1', 'Campeonatos', 'Campeão', 'Vença um campeonato.', 1, d => d.championshipsWon),
  a('champ_3', 'Campeonatos', 'Dinastia', 'Vença 3 campeonatos.', 3, d => d.championshipsWon),
  // Ultrapassagens
  a('ot_10', 'Ultrapassagens', 'Atacante', 'Faça 10 ultrapassagens no total.', 10, d => d.totalOvertakes),
  a('ot_50', 'Ultrapassagens', 'Afiado', 'Faça 50 ultrapassagens no total.', 50, d => d.totalOvertakes),
  a('ot_100', 'Ultrapassagens', 'Ferocidade', 'Faça 100 ultrapassagens no total.', 100, d => d.totalOvertakes),
  a('ot_500', 'Ultrapassagens', 'Furacão', 'Faça 500 ultrapassagens no total.', 500, d => d.totalOvertakes),
  a('ot_race_5', 'Ultrapassagens', 'Show de Bola', 'Faça 5 ultrapassagens numa única corrida.', 5, d => d.maxOvertakesInRace),
  // Voltas mais rápidas
  a('fl_1', 'Voltas Rápidas', 'Volta Voadora', 'Marque a volta mais rápida de uma corrida.', 1, d => d.fastestLaps),
  a('fl_10', 'Voltas Rápidas', 'Cronômetro', 'Marque 10 voltas mais rápidas.', 10, d => d.fastestLaps),
  a('fl_25', 'Voltas Rápidas', 'Relâmpago', 'Marque 25 voltas mais rápidas.', 25, d => d.fastestLaps),
  // Pit stops
  a('pit_10', 'Estratégia', 'Box, Box!', 'Realize 10 pit stops no total.', 10, d => d.totalPitStops),
  a('pit_50', 'Estratégia', 'Mestre dos Boxes', 'Realize 50 pit stops no total.', 50, d => d.totalPitStops),
  // Circuitos
  a('trk_5', 'Circuitos', 'Viajante', 'Vença em 5 circuitos diferentes.', 5, d => d.tracksWon.length),
  a('trk_all', 'Circuitos', 'Conquistador Global', 'Vença em todos os 15 circuitos.', 15, d => d.tracksWon.length),
  // Pneus
  a('tyre_all', 'Pneus', 'Mestre dos Compostos', 'Vença usando os 5 tipos de pneu.', 5, d => d.tyresWon.length),
  // Especiais
  a('fuel_spare', 'Especiais', 'Sobrou Tanque', 'Vença com mais de 40% de combustível.', 1, d => B(d.wonWithFuelToSpare)),
  a('both_pod', 'Especiais', 'Dobradinha', 'Coloque as suas 2 bolinhas no pódio na mesma corrida.', 1, d => B(d.wonBothPodium)),
];

export function achievementCurrent(ach: Achievement, d: AchievementData): number {
  return Math.min(Math.max(ach.value(d), 0), ach.target);
}

export function achievementUnlocked(ach: Achievement, d: AchievementData): boolean {
  return ach.value(d) >= ach.target;
}

export function achievementProgress(ach: Achievement, d: AchievementData): number {
  return ach.target <= 0 ? 1 : Math.min(1, Math.max(0, ach.value(d) / ach.target));
}

// ---------------------------------------------------------------------

export class AchievementManager {
  private static _data: AchievementData | null = null;

  static get data(): AchievementData {
    return (this._data ??= SaveManager.loadAchievements());
  }

  static save(): void { SaveManager.saveAchievements(this.data); }

  static unlockedCount(): number {
    return ACHIEVEMENTS.filter(x => achievementUnlocked(x, this.data)).length;
  }

  /** Atualiza estatísticas ao fim de uma corrida; devolve conquistas novas. */
  static recordRace(result: RaceResult | null): Achievement[] {
    if (!result) return [];
    const d = this.data;
    d.racesPlayed++;

    let bestPos = Number.MAX_SAFE_INTEGER;
    let raceOvertakes = 0, racePits = 0, podiumPlayers = 0;
    let anyFastest = false;
    for (const e of result.entries) {
      if (!e.isPlayer) continue;
      if (e.position < bestPos) bestPos = e.position;
      raceOvertakes += e.overtakes;
      racePits += e.pitStops;
      if (e.fastestLap) anyFastest = true;
      if (e.position <= 3) podiumPlayers++;
    }

    if (bestPos === Number.MAX_SAFE_INTEGER) {
      this.save();
      return []; // sem bolinhas do jogador
    }

    d.totalOvertakes += raceOvertakes;
    d.totalPitStops += racePits;
    d.maxOvertakesInRace = Math.max(d.maxOvertakesInRace, raceOvertakes);
    if (anyFastest) d.fastestLaps++;
    if (bestPos <= 3) d.podiums++;
    if (podiumPlayers >= 2) d.wonBothPodium = true;

    if (bestPos === 1) {
      d.racesWon++;
      d.currentWinStreak++;
      d.bestWinStreak = Math.max(d.bestWinStreak, d.currentWinStreak);
      if (result.trackName && !d.tracksWon.includes(result.trackName)) {
        d.tracksWon.push(result.trackName);
      }
      const win = result.entries.find(e => e.isPlayer && e.position === 1);
      if (win) {
        if (win.finalTyre && !d.tyresWon.includes(win.finalTyre)) d.tyresWon.push(win.finalTyre);
        if (win.finalFuel > 40) d.wonWithFuelToSpare = true;
      }
    } else {
      d.currentWinStreak = 0;
    }

    const newly = this.detectNew();
    this.save();
    return newly;
  }

  /** Reseta o "claim" do campeonato ao iniciar uma nova temporada. */
  static noteSeasonStart(): void {
    this.data.seasonClaimed = false;
    this.save();
  }

  /** Contabiliza o título uma única vez quando o jogador é campeão. */
  static noteChampionResult(playerIsChampion: boolean): Achievement[] {
    if (!playerIsChampion || this.data.seasonClaimed) return [];
    this.data.championshipsWon++;
    this.data.seasonClaimed = true;
    const newly = this.detectNew();
    this.save();
    return newly;
  }

  private static detectNew(): Achievement[] {
    const newly: Achievement[] = [];
    for (const ach of ACHIEVEMENTS) {
      if (achievementUnlocked(ach, this.data) && !this.data.unlocked.includes(ach.id)) {
        this.data.unlocked.push(ach.id);
        newly.push(ach);
      }
    }
    return newly;
  }
}
