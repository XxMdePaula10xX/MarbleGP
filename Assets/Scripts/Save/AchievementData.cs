using System.Collections.Generic;

namespace MarbleGP.Save
{
    /// <summary>
    /// Estatísticas acumuladas do jogador, base para as conquistas (PRD 32).
    /// Persistido em achievements.json. As conquistas em si são derivadas destes
    /// números pelo catálogo em AchievementManager (não guardamos progresso
    /// duplicado — só os contadores).
    /// </summary>
    [System.Serializable]
    public class AchievementData
    {
        public int racesPlayed;
        public int racesWon;
        public int podiums;
        public int totalOvertakes;
        public int fastestLaps;
        public int totalPitStops;
        public int championshipsWon;
        public int bestWinStreak;
        public int currentWinStreak;
        public int maxOvertakesInRace;
        public bool wonWithFuelToSpare;   // venceu com >40% de combustivel
        public bool wonBothPodium;        // as 2 bolinhas do jogador no pódio

        public List<string> tracksWon = new List<string>();  // circuitos distintos vencidos
        public List<string> tyresWon = new List<string>();   // pneus distintos usados em vitórias
        public List<string> unlocked = new List<string>();   // ids ja desbloqueados (detecta novas)

        public bool seasonClaimed;        // o campeonato atual ja foi contabilizado
    }
}
