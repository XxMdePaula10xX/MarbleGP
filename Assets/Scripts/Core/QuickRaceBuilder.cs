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
            float startEnergy = 70f, RaceMode startMode = RaceMode.Normal)
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
                    config.entries.Add(new MarbleStrategy
                    {
                        driver = driver,
                        grip = defaultGrip,
                        surface = defaultSurface,
                        startEnergy = startEnergy,
                        startMode = startMode,
                        isPlayerControlled = team.teamId == playerTeamId
                    });
                    count++;
                }
                if (count >= maxMarbles) break;
            }

            return config;
        }
    }
}
