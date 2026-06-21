using UnityEngine;
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
        public float energy = 100f;        // 0-100 bateria de performance (PRD 4.2)
        public float fuel = 100f;          // 0-100 autonomia, so volta no pit (PRD 4.1)
        public float damage = 0f;          // 0-100 (reparavel no pit)

        public bool FuelEmpty => fuel <= 0f;

        // Eventos de corrida (PRD 10).
        public float recoverTimer = 0f;   // tempo de recuperacao apos batida forte
        public float coreFailTimer = 0f;  // falha de nucleo (dreno extra de energia)

        // Progresso de corrida (PRD 26)
        public int completedLaps = 0;
        public int currentCheckpoint = 0;
        public float raceProgress = 0f;    // calculado pelo RacePositionSystem
        public int position = 0;           // posicao ao vivo (1-based)
        public float gapToLeader = 0f;     // segundos atras do lider (timing tower)

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
        public float pitTotalTime = 1f;    // tempo total do servico (barra de progresso)
        public GripType pitTargetGrip = GripType.Medium; // anel a montar no pit
        public bool pitChangeTires = true;               // trocar anel?
        public float pitRefillAmount = 60f;              // energia a recarregar

        // Efeitos dos upgrades de equipe (PRD 30); neutros por padrao.
        // Aplicados pelo RaceManager apenas as bolinhas do jogador.
        public float upgPitReduction = 0f;
        public float upgEnergyFactor = 1f;
        public float upgWearFactor = 1f;
        public float upgSpeedFactor = 1f;
        public float upgControlFactor = 1f;
        public float upgErrorFactor = 1f;

        // Referencia ao balance para conveniencia (setada pelo RaceManager).
        public GameBalance GameBalanceRef;

        public float MaxEnergy => GameBalanceRef != null ? GameBalanceRef.maxEnergy : 100f;

        // Overrides visuais da garagem (PRD 31). Setados pelo RaceManager a
        // partir do PlayerProfile para as bolinhas do jogador.
        public Color? teamPrimaryOverride;
        public Color? teamSecondaryOverride;
        public Color? marbleColorOverride;
        public string teamNameOverride;

        public string DisplayName => driver != null ? driver.marbleName : "Marble";
        public string TeamName => team != null ? team.teamName : "Team";
        public string TeamDisplayName =>
            !string.IsNullOrEmpty(teamNameOverride) ? teamNameOverride : TeamName;

        /// <summary>Cor primaria da equipe (chip do ranking), respeitando override.</summary>
        public Color TeamPrimary =>
            teamPrimaryOverride ?? (team != null ? team.primaryColor : Color.gray);
        /// <summary>Cor secundaria da equipe, respeitando override.</summary>
        public Color TeamSecondary =>
            teamSecondaryOverride ?? (team != null ? team.secondaryColor : Color.black);
        /// <summary>Cor do corpo da bolinha: cor propria customizada ou cor da equipe.</summary>
        public Color MarbleColor => marbleColorOverride ?? TeamPrimary;
    }
}
