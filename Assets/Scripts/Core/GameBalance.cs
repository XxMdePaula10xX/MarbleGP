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
        [Tooltip("Abaixo desta carga a IA passa a economizar (perde um pouco de ritmo).")]
        public float lowChargeThreshold = 20f;

        [Header("Desgaste - limiares de efeito (PRD 14)")]
        public float wearWarnThreshold = 50f;   // comeca a perder desempenho
        public float wearHeavyThreshold = 70f;   // queda perceptivel
        public float wearCriticalThreshold = 85f; // risco de erro alto
        [Tooltip("Penalidade maxima de velocidade quando desgaste = 100.")]
        [Range(0f, 0.6f)] public float maxWearSpeedPenalty = 0.35f;

        // Modos de corrida agora vivem em ModeTuning.cs (fonte unica).

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

        /// <summary>Pontos para uma posicao (1-based). Fora da tabela => 0.</summary>
        public int PointsForPosition(int position)
        {
            int idx = position - 1;
            if (idx < 0 || idx >= pointsByPosition.Length) return 0;
            return pointsByPosition[idx];
        }
    }
}
