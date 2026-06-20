using UnityEngine;

namespace MarbleGP.Core
{
    /// <summary>
    /// Constantes de balanceamento global (PRD secao 28 e 41).
    /// Mantido como ScriptableObject para permitir ajuste no Inspector
    /// sem recompilar. Um asset default e gerado pelo Editor/DataGenerator.
    /// Valores default refletem exatamente o "Balanceamento Inicial" do PRD.
    /// </summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "MarbleGP/Game Balance", order = 0)]
    public class GameBalance : ScriptableObject
    {
        [Header("Velocidade base (PRD 28)")]
        [Tooltip("Velocidade base da bolinha em unidades.")]
        public float baseSpeed = 10f;

        [Header("Energia (PRD 15 / 28)")]
        public float maxEnergy = 100f;
        public float energyConsumptionNormal = 18f;
        public float energyConsumptionPush = 24f;
        public float energyConsumptionSave = 12f;

        [Header("Penalidade de velocidade por carga de energia (PRD 28)")]
        [Tooltip("Energia acima deste valor aplica penaltyHighCharge.")]
        public float highChargeThreshold = 80f;
        public float midChargeThreshold = 50f;
        public float lowChargeThreshold = 20f;
        [Range(0f, 0.2f)] public float penaltyHighCharge = 0.04f;  // -4%
        [Range(0f, 0.2f)] public float penaltyMidCharge = 0.02f;   // -2%
        [Range(0f, 0.2f)] public float penaltyLowCharge = 0.06f;   // -6%

        [Header("Desgaste - limiares de efeito (PRD 14)")]
        public float wearWarnThreshold = 50f;   // comeca a perder desempenho
        public float wearHeavyThreshold = 70f;   // queda perceptivel
        public float wearCriticalThreshold = 85f; // risco de erro alto
        [Tooltip("Penalidade maxima de velocidade quando desgaste = 100.")]
        [Range(0f, 0.6f)] public float maxWearSpeedPenalty = 0.35f;

        [Header("Modos de corrida (PRD 28)")]
        public ModeSettings normalMode = new ModeSettings { speed = 1.00f, wear = 1.00f, energy = 1.00f, errorMod = 0.00f };
        public ModeSettings pushMode = new ModeSettings { speed = 1.07f, wear = 1.25f, energy = 1.30f, errorMod = 0.10f };
        public ModeSettings saveMode = new ModeSettings { speed = 0.92f, wear = 0.75f, energy = 0.70f, errorMod = -0.05f };

        [Header("Erros (PRD 13.6 / 41)")]
        public float baseErrorChance = 0.01f;
        [Tooltip("Tempo (s) com velocidade muito baixa fora do pit antes de acionar recuperacao (PRD 13.6).")]
        public float stuckRecoveryTime = 2.0f;
        [Tooltip("Velocidade abaixo da qual a bolinha e considerada presa.")]
        public float stuckSpeedThreshold = 0.4f;

        [Header("Pit stop (PRD 18.3)")]
        public float basePitTime = 3.0f;
        public float tireChangeTime = 1.2f;
        public float energyRefillTimePerUnit = 0.03f; // por unidade de energia recarregada
        public float maxRandomPitError = 0.6f;
        [Tooltip("Velocidade de deslocamento dentro do pit lane (limite).")]
        public float pitLaneSpeed = 6f;

        [Header("Pontuacao do campeonato (PRD 10)")]
        public int[] pointsByPosition = { 25, 20, 18, 15, 12, 10, 8, 6, 4, 3, 2, 1 };
        public bool fastestLapBonus = false; // MVP: somente pontuacao principal

        [Header("IA / Direcao")]
        [Tooltip("Distancia para considerar que chegou ao waypoint.")]
        public float waypointArriveDistance = 1.5f;
        [Tooltip("Antecipacao de curva: quanto maior, mais cedo a bolinha freia.")]
        public float cornerLookAhead = 2.5f;
        [Tooltip("Distancia para detectar bolinha a frente para ultrapassagem.")]
        public float overtakeDetectDistance = 3.0f;

        [System.Serializable]
        public struct ModeSettings
        {
            public float speed;
            public float wear;
            public float energy;
            public float errorMod;
        }

        public ModeSettings GetMode(RaceMode mode)
        {
            switch (mode)
            {
                case RaceMode.Push: return pushMode;
                case RaceMode.Save: return saveMode;
                // Modos fora do MVP usam Normal como base ate serem implementados.
                default: return normalMode;
            }
        }

        /// <summary>Pontos para uma posicao (1-based). Fora da tabela => 0.</summary>
        public int PointsForPosition(int position)
        {
            int idx = position - 1;
            if (idx < 0 || idx >= pointsByPosition.Length) return 0;
            return pointsByPosition[idx];
        }
    }
}
