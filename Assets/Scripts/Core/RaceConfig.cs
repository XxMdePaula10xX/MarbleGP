using System.Collections.Generic;
using MarbleGP.Data;

namespace MarbleGP.Core
{
    /// <summary>
    /// Estrategia inicial escolhida para uma bolinha antes da corrida (PRD 7.3 / 8).
    /// </summary>
    [System.Serializable]
    public class MarbleStrategy
    {
        public MarbleDriverSO driver;
        public GripType grip = GripType.Medium;
        public SurfaceType surface = SurfaceType.MicroGrooved;
        public float startEnergy = 70f;        // carga inicial (PRD 8)
        public RaceMode startMode = RaceMode.Normal;
        public bool isPlayerControlled = false; // true para as 2 bolinhas do jogador
    }

    /// <summary>
    /// Configuracao completa de uma corrida, montada pelos menus e consumida
    /// pelo RaceManager (PRD 7.3). Transportada via GameManager entre cenas.
    /// </summary>
    [System.Serializable]
    public class RaceConfig
    {
        public TrackDataSO track;
        public int laps = 5;
        public Weather weather = Weather.Dry; // MVP: Dry
        public string playerTeamId;
        public List<MarbleStrategy> entries = new List<MarbleStrategy>();

        public int MarbleCount => entries.Count;
    }
}
