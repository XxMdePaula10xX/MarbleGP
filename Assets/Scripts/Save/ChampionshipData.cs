using System.Collections.Generic;
using MarbleGP.Core;

namespace MarbleGP.Save
{
    /// <summary>Nivel de um upgrade da equipe (PRD 30).</summary>
    [System.Serializable]
    public class UpgradeState
    {
        public UpgradeType type;
        public int level;
    }

    /// <summary>Classificacao de uma bolinha/piloto na temporada (PRD 29.2).</summary>
    [System.Serializable]
    public class DriverStanding
    {
        public string driverId;
        public string teamId;
        public int points;
        public int wins;
        public int podiums;
        public int poles;
        public int fastestLaps;
    }

    /// <summary>Classificacao de uma equipe na temporada (PRD 29.2).</summary>
    [System.Serializable]
    public class TeamStanding
    {
        public string teamId;
        public int points;
        public int wins;
        public int podiums;
    }

    /// <summary>Resumo de um resultado de etapa (historico, PRD 29.3).</summary>
    [System.Serializable]
    public class RoundResultSummary
    {
        public string trackId;
        public string winnerDriverId;
        public string winnerTeamId;
    }

    /// <summary>
    /// Estado completo de uma temporada de campeonato (PRD 29.3). Serializavel
    /// para JSON via SaveManager. Guarda calendario, etapa atual, classificacoes
    /// e historico de resultados.
    /// </summary>
    [System.Serializable]
    public class ChampionshipData
    {
        public string playerTeamId;
        public int currentRound = 0;
        public bool active = false;

        /// <summary>Creditos da equipe para comprar upgrades (PRD 30).</summary>
        public int credits = 0;
        /// <summary>Niveis dos upgrades da equipe do jogador (PRD 30).</summary>
        public List<UpgradeState> upgrades = new List<UpgradeState>();

        public List<string> calendarTrackIds = new List<string>();
        public List<DriverStanding> driverStandings = new List<DriverStanding>();
        public List<TeamStanding> teamStandings = new List<TeamStanding>();
        public List<RoundResultSummary> history = new List<RoundResultSummary>();
    }
}
