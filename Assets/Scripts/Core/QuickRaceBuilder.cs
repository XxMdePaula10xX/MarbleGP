using System.Collections.Generic;
using MarbleGP.Data;

namespace MarbleGP.Core
{
    /// <summary>
    /// Monta uma RaceConfig de "Corrida Rapida" a partir do GameDatabase
    /// (PRD 7.3 / 39.4: nao hardcodar). Marca as 2 bolinhas da equipe do
    /// jogador como controladas por ele.
    /// </summary>
    public static class QuickRaceBuilder
    {
        /// <summary>
        /// Cria uma corrida com ate maxMarbles bolinhas (PRD 9.1 MVP: 8),
        /// usando a estrategia padrao recebida para todas (jogador customiza depois).
        /// </summary>
        public static RaceConfig Build(GameDatabase db, TrackDataSO track, string playerTeamId,
            int maxMarbles = 20, GripType defaultGrip = GripType.Medium,
            SurfaceType defaultSurface = SurfaceType.MicroGrooved,
            float startEnergy = 100f, RaceMode startMode = RaceMode.Normal)
        {
            var config = new RaceConfig
            {
                track = track,
                laps = track.recommendedLaps,
                weather = Weather.Dry, // MVP
                playerTeamId = playerTeamId
            };

            int count = 0;
            foreach (var team in db.teams)
            {
                foreach (var driver in db.GetTeamDrivers(team.teamId))
                {
                    if (count >= maxMarbles) break;
                    bool isPlayer = team.teamId == playerTeamId;
                    config.entries.Add(new MarbleStrategy
                    {
                        driver = driver,
                        // Bolinhas do jogador usam a estrategia escolhida; a IA recebe
                        // pneu/modo variados por personalidade, dando um grid diverso.
                        grip = isPlayer ? defaultGrip : AiGrip(driver),
                        surface = defaultSurface,
                        startEnergy = startEnergy,
                        startMode = isPlayer ? startMode : AiMode(driver),
                        isPlayerControlled = isPlayer
                    });
                    count++;
                }
                if (count >= maxMarbles) break;
            }

            return config;
        }

        /// <summary>Pneu inicial da IA conforme o perfil do piloto (seco; MVP).</summary>
        private static GripType AiGrip(MarbleDriverSO d)
        {
            switch (d != null ? d.personality : Personality.Balanced)
            {
                case Personality.Aggressive:
                case Personality.RiskTaker: return GripType.Soft;   // rapido, gasta mais
                case Personality.Conservative:
                case Personality.Veteran:   return GripType.Hard;   // durавel
                case Personality.Smooth:    return GripType.Medium;
                default:                    return GripType.Medium;
            }
        }

        /// <summary>Modo inicial da IA conforme o perfil do piloto.</summary>
        private static RaceMode AiMode(MarbleDriverSO d)
        {
            switch (d != null ? d.personality : Personality.Balanced)
            {
                case Personality.Aggressive:
                case Personality.RiskTaker: return RaceMode.Push;
                case Personality.Conservative: return RaceMode.Save;
                default: return RaceMode.Normal;
            }
        }
    }
}
