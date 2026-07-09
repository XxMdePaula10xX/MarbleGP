// =====================================================================
// As 10 equipes do grid — port de Editor/DataGenerator.cs (CreateTeams)
// =====================================================================

import type { TeamData } from '../core/types';

export const TEAMS: TeamData[] = [
  {
    teamId: 'red_comet', teamName: 'Red Comet Racing',
    primaryColor: '#E62020', secondaryColor: '#1A1A1A',
    teamStyle: 'Aggressive', baseBonusType: 'TopSpeed',
    pitCrewRating: 55, developmentRating: 50,
    description: 'Agressiva e rápida. Forte em retas, desgaste alto.',
  },
  {
    teamId: 'blue_orbit', teamName: 'Blue Orbit GP',
    primaryColor: '#1E5FE0', secondaryColor: '#FFFFFF',
    teamStyle: 'Balanced', baseBonusType: 'Consistency',
    pitCrewRating: 60, developmentRating: 55,
    description: 'Equilibrada e consistente.',
  },
  {
    teamId: 'emerald_rollers', teamName: 'Emerald Rollers',
    primaryColor: '#1FA84A', secondaryColor: '#D4AF37',
    teamStyle: 'Technical', baseBonusType: 'Cornering',
    pitCrewRating: 55, developmentRating: 58,
    description: 'Técnica, forte em curvas.',
  },
  {
    teamId: 'shadow_marble', teamName: 'Shadow Marble Team',
    primaryColor: '#202020', secondaryColor: '#7A3FB0',
    teamStyle: 'Strategic', baseBonusType: 'PitStops',
    pitCrewRating: 70, developmentRating: 52,
    description: 'Estratégica, excelente nos pit stops.',
  },
  {
    teamId: 'solar_spin', teamName: 'Solar Spin',
    primaryColor: '#F2C200', secondaryColor: '#F07000',
    teamStyle: 'Fast', baseBonusType: 'Acceleration',
    pitCrewRating: 55, developmentRating: 52,
    description: 'Veloz, forte aceleração.',
  },
  {
    teamId: 'frostline', teamName: 'Frostline Racing',
    primaryColor: '#7FD0F0', secondaryColor: '#C0C8D0',
    teamStyle: 'Precise', baseBonusType: 'WetWeather',
    pitCrewRating: 58, developmentRating: 55,
    description: 'Precisa, ótima na chuva.',
  },
  {
    teamId: 'iron_sphere', teamName: 'Iron Sphere',
    primaryColor: '#8A8A8A', secondaryColor: '#C0241F',
    teamStyle: 'Resistant', baseBonusType: 'LowDamage',
    pitCrewRating: 60, developmentRating: 50,
    description: 'Resistente, baixo dano.',
  },
  {
    teamId: 'neon_pulse', teamName: 'Neon Pulse',
    primaryColor: '#FF4FA3', secondaryColor: '#20E0E0',
    teamStyle: 'Unpredictable', baseBonusType: 'Overtaking',
    pitCrewRating: 54, developmentRating: 53,
    description: 'Imprevisível, boa em ultrapassagem.',
  },
  {
    teamId: 'jungle_curve', teamName: 'Jungle Curve',
    primaryColor: '#2E6B2E', secondaryColor: '#6B4A2A',
    teamStyle: 'Technical', baseBonusType: 'Cornering',
    pitCrewRating: 53, developmentRating: 56,
    description: 'Técnica em curvas lentas.',
  },
  {
    teamId: 'royal_club', teamName: 'Royal Marble Club',
    primaryColor: '#6A2FB0', secondaryColor: '#D4AF37',
    teamStyle: 'Premium', baseBonusType: 'Development',
    pitCrewRating: 64, developmentRating: 66,
    description: 'Premium, forte desenvolvimento.',
  },
];

export function teamById(id: string): TeamData {
  const t = TEAMS.find(t => t.teamId === id);
  if (!t) throw new Error(`Equipe desconhecida: ${id}`);
  return t;
}
