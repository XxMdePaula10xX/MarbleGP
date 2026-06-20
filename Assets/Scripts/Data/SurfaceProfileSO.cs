using UnityEngine;
using MarbleGP.Core;

namespace MarbleGP.Data
{
    /// <summary>Superficie / textura da bolinha (PRD 16 / 24.1).</summary>
    [CreateAssetMenu(fileName = "Surface_", menuName = "MarbleGP/Surface Profile", order = 31)]
    public class SurfaceProfileSO : ScriptableObject
    {
        public SurfaceType surfaceId = SurfaceType.MicroGrooved;
        public string surfaceName = "Micro-Grooved";

        [Tooltip("Modificador de velocidade em reta.")]
        public float speedModifier = 1.00f;
        [Tooltip("Modificador de controle em curva.")]
        public float controlModifier = 1.00f;
        [Tooltip("Modificador de desgaste.")]
        public float wearModifier = 1.00f;
        [Tooltip("Modificador de consumo de energia.")]
        public float energyModifier = 1.00f;
        [Tooltip("Modificador de desempenho em pista molhada.")]
        public float wetModifier = 1.00f;

        [TextArea] public string description;
    }
}
