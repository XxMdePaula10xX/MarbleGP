using UnityEngine;
using MarbleGP.Core;

namespace MarbleGP.Data
{
    /// <summary>Anel de aderencia (PRD 14 / 24.1). Substitui o conceito de pneu.</summary>
    [CreateAssetMenu(fileName = "Grip_", menuName = "MarbleGP/Grip Ring", order = 30)]
    public class GripRingSO : ScriptableObject
    {
        public GripType gripId = GripType.Medium;
        public string compoundName = "Medium Grip";

        [Header("Performance")]
        [Tooltip("Multiplicador de velocidade base (PRD 28).")]
        public float speedMultiplier = 1.00f;
        [Tooltip("Multiplicador de aderencia em curva.")]
        public float gripMultiplier = 1.00f;

        [Header("Desgaste")]
        [Tooltip("Desgaste por volta em condicoes normais (PRD 28).")]
        public float wearRate = 11f;

        [Header("Condicoes")]
        [Range(0f, 2f)] public float wetPerformance = 0.7f;
        [Range(0f, 2f)] public float dryPerformance = 1.0f;
        public float optimalTemperature = 25f;

        [TextArea] public string description;

        /// <summary>Desgaste efetivo por volta dado o clima (Rain desgasta muito no seco).</summary>
        public float WearForWeather(Weather weather)
        {
            bool wet = weather == Weather.Damp || weather == Weather.LightRain || weather == Weather.HeavyRain;
            if (gripId == GripType.Rain && !wet) return wearRate * 2.2f; // 10 -> 22 no seco (PRD 28)
            return wearRate;
        }

        /// <summary>Fator de performance conforme clima (seco x molhado).</summary>
        public float PerformanceForWeather(Weather weather)
        {
            bool wet = weather == Weather.Damp || weather == Weather.LightRain || weather == Weather.HeavyRain;
            return wet ? wetPerformance : dryPerformance;
        }
    }
}
