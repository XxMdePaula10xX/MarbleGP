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

        [Header("Economia (PRD 5): consumo de energia e combustivel")]
        [Tooltip("Multiplicador de consumo de energia (Soft gasta mais).")]
        public float energyMultiplier = 1.0f;
        [Tooltip("Multiplicador de consumo de combustivel.")]
        public float fuelMultiplier = 1.0f;

        /// <summary>Multiplicador de energia seguro (fallback 1 se asset antigo).</summary>
        public float EnergyMult => energyMultiplier <= 0f ? 1f : energyMultiplier;
        /// <summary>Multiplicador de combustivel seguro (fallback 1 se asset antigo).</summary>
        public float FuelMult => fuelMultiplier <= 0f ? 1f : fuelMultiplier;

        [TextArea] public string description;

        /// <summary>Letra exibida no ranking ao vivo (estilo indicador de pneu).</summary>
        public string DisplayLetter
        {
            get
            {
                switch (gripId)
                {
                    case GripType.Soft: return "S";
                    case GripType.Medium: return "M";
                    case GripType.Hard: return "H";
                    case GripType.Intermediate: return "I";
                    case GripType.Rain: return "W";
                    default: return "?";
                }
            }
        }

        /// <summary>Cor do indicador (estilo F1: macio=vermelho, medio=amarelo, duro=branco).</summary>
        public Color DisplayColor
        {
            get
            {
                switch (gripId)
                {
                    case GripType.Soft: return new Color(0.90f, 0.20f, 0.20f);
                    case GripType.Medium: return new Color(0.95f, 0.80f, 0.20f);
                    case GripType.Hard: return new Color(0.92f, 0.92f, 0.92f);
                    case GripType.Intermediate: return new Color(0.30f, 0.80f, 0.35f);
                    case GripType.Rain: return new Color(0.30f, 0.55f, 0.95f);
                    default: return Color.gray;
                }
            }
        }

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
