// =====================================================================
// As 10 equipes do grid — port de Editor/DataGenerator.cs (CreateTeams)
// =====================================================================

import type { TeamData } from '../core/types';

export const TEAMS: TeamData[] = [
  {
    teamId: 'red_comet', teamName: 'Red Comet Racing',
    primaryColor: '#E62020',
    raceColor: '#FF3B3B', secondaryColor: '#1A1A1A',
    pitCrewRating: 55,
    description: 'Agressiva e rápida. Forte em retas, desgaste alto.',
  },
  {
    teamId: 'blue_orbit', teamName: 'Blue Orbit GP',
    primaryColor: '#1E5FE0',
    raceColor: '#2F8FFF', secondaryColor: '#FFFFFF',
    pitCrewRating: 60,
    description: 'Equilibrada e consistente.',
  },
  {
    teamId: 'emerald_rollers', teamName: 'Emerald Rollers',
    primaryColor: '#1FA84A',
    raceColor: '#2ED760', secondaryColor: '#D4AF37',
    pitCrewRating: 55,
    description: 'Técnica, forte em curvas.',
  },
  {
    teamId: 'shadow_marble', teamName: 'Shadow Marble Team',
    primaryColor: '#202020',
    raceColor: '#FF7A2E', secondaryColor: '#7A3FB0',
    pitCrewRating: 70,
    description: 'Estratégica, excelente nos pit stops.',
  },
  {
    teamId: 'solar_spin', teamName: 'Solar Spin',
    primaryColor: '#F2C200',
    raceColor: '#FFC21F', secondaryColor: '#F07000',
    pitCrewRating: 55,
    description: 'Veloz, forte aceleração.',
  },
  {
    teamId: 'frostline', teamName: 'Frostline Racing',
    primaryColor: '#7FD0F0',
    raceColor: '#7FD8F0', secondaryColor: '#C0C8D0',
    pitCrewRating: 58,
    description: 'Precisa, ótima na chuva.',
  },
  {
    teamId: 'iron_sphere', teamName: 'Iron Sphere',
    primaryColor: '#8A8A8A',
    raceColor: '#A8B0BC', secondaryColor: '#C0241F',
    pitCrewRating: 60,
    description: 'Resistente, baixo dano.',
  },
  {
    teamId: 'neon_pulse', teamName: 'Neon Pulse',
    primaryColor: '#FF4FA3',
    raceColor: '#FF4FA3', secondaryColor: '#20E0E0',
    pitCrewRating: 54,
    description: 'Imprevisível, boa em ultrapassagem.',
  },
  {
    teamId: 'jungle_curve', teamName: 'Jungle Curve',
    primaryColor: '#2E6B2E',
    raceColor: '#8FD13A', secondaryColor: '#6B4A2A',
    pitCrewRating: 53,
    description: 'Técnica em curvas lentas.',
  },
  {
    teamId: 'royal_club', teamName: 'Royal Marble Club',
    primaryColor: '#6A2FB0',
    raceColor: '#A46BFF', secondaryColor: '#D4AF37',
    pitCrewRating: 64,
    description: 'Premium, forte desenvolvimento.',
  },
];

export function teamById(id: string): TeamData {
  const t = TEAMS.find(t => t.teamId === id);
  if (!t) throw new Error(`Equipe desconhecida: ${id}`);
  return t;
}
