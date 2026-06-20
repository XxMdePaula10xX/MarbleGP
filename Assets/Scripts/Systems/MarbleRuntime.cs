using MarbleGP.Core;
using MarbleGP.Data;

namespace MarbleGP.Systems
{
    /// <summary>
    /// Estado MUTAVEL de uma bolinha durante a corrida (PRD 39.15).
    /// Separado de MarbleDriverSO (imutavel). Uma instancia por bolinha por corrida.
    /// </summary>
    public class MarbleRuntime
    {
        public MarbleDriverSO driver;
        public TeamDataSO team;
        public MarbleStrategy strategy;
        public bool isPlayer;

        // Equipamento atual (pode mudar no pit)
        public GripRingSO grip;
        public SurfaceProfileSO surface;
        public RaceMode mode = RaceMode.Normal;

        // Estado dinamico
        public float wear = 0f;            // 0-100 (PRD 14)
        public float energy = 70f;         // 0-maxEnergy (PRD 15)
        public float damage = 0f;          // 0-100 (reparavel no pit)

        // Progresso de corrida (PRD 26)
        public int completedLaps = 0;
        public int currentCheckpoint = 0;
        public float raceProgress = 0f;    // calculado pelo RacePositionSystem
        public int position = 0;           // posicao ao vivo (1-based)

        public MarbleRaceState state = MarbleRaceState.OnGrid;

        // Estatisticas (PRD 8 / 23.6)
        public int pitStops = 0;
        public int overtakes = 0;
        public float bestLapTime = float.MaxValue;
        public float currentLapTime = 0f;
        public float totalTime = 0f;

        // Pit
        public bool pitRequested = false;  // jogador/IA pediu pit
        public float pitTimer = 0f;        // tempo restante de servico
        public GripType pitTargetGrip = GripType.Medium; // anel a montar no pit
        public bool pitChangeTires = true;               // trocar anel?
        public float pitRefillAmount = 60f;              // energia a recarregar

        // Referencia ao balance para conveniencia (setada pelo RaceManager).
        public GameBalance GameBalanceRef;

        public float MaxEnergy => GameBalanceRef != null ? GameBalanceRef.maxEnergy : 100f;

        public string DisplayName => driver != null ? driver.marbleName : "Marble";
        public string TeamName => team != null ? team.teamName : "Team";
    }
}
