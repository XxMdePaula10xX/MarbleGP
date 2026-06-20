using UnityEngine;
using MarbleGP.Core;

namespace MarbleGP.Data
{
    /// <summary>
    /// Dados base de uma bolinha/piloto (PRD 12 / 24.1).
    /// Atributos 1-100. Estes sao os dados IMUTAVEIS; o estado mutavel
    /// durante a corrida vive em MarbleRuntime (PRD 39.15).
    /// </summary>
    [CreateAssetMenu(fileName = "Driver_", menuName = "MarbleGP/Marble Driver", order = 20)]
    public class MarbleDriverSO : ScriptableObject
    {
        public string driverId = "driver_id";
        public string marbleName = "New Marble";
        [Tooltip("Sigla de 3 letras exibida no ranking (ex.: CM1).")]
        public string shortCode = "MAR";
        public int number = 0;
        [Tooltip("teamId da TeamDataSO a que pertence.")]
        public string teamId = "team_id";

        [Header("Atributos (1-100)")]
        [Range(1, 100)] public int speed = 50;
        [Range(1, 100)] public int acceleration = 50;
        [Range(1, 100)] public int control = 50;
        [Range(1, 100)] public int aggression = 50;
        [Range(1, 100)] public int defense = 50;
        [Range(1, 100)] public int consistency = 50;
        [Range(1, 100)] public int tireManagement = 50;
        [Range(1, 100)] public int energyManagement = 50;
        [Range(1, 100)] public int pitSkill = 50;
        [Range(1, 100)] public int wetSkill = 50;

        [Header("Comportamento")]
        public Personality personality = Personality.Balanced;
        public string materialPreset = "default";

        // ---- Helpers de conversao atributo->multiplicador ----------------

        /// <summary>Converte um atributo 1-100 num multiplicador em torno de 1.0.</summary>
        /// <param name="spread">Quanto o atributo afeta (0.1 => 0.95..1.05).</param>
        public static float AttrMultiplier(int attr, float spread)
        {
            // 50 => 1.0 ; 1 => 1-spread ; 100 => 1+spread (aprox)
            return 1f + ((attr - 50f) / 50f) * spread;
        }

        public float SpeedMultiplier => AttrMultiplier(speed, 0.10f);
        public float AccelMultiplier => AttrMultiplier(acceleration, 0.30f);
        public float ControlMultiplier => AttrMultiplier(control, 0.15f);
        public float TireMgmtMultiplier => AttrMultiplier(tireManagement, 0.30f); // >1 desgasta mais; invertido no uso
        public float EnergyMgmtMultiplier => AttrMultiplier(energyManagement, 0.30f);
    }
}
