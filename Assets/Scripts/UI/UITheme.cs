using UnityEngine;

namespace MarbleGP.UI
{
    /// <summary>
    /// Tema visual central (PRD 2). Cores globais reutilizadas pelas telas e
    /// HUD para manter identidade consistente.
    /// </summary>
    public static class UITheme
    {
        public static readonly Color BackgroundPanel = new Color(0.05f, 0.06f, 0.10f, 0.86f);
        public static readonly Color CardPanel = new Color(0.07f, 0.08f, 0.12f, 0.92f);
        public static readonly Color HeaderPanel = new Color(0.10f, 0.12f, 0.18f, 0.95f);

        public static readonly Color PrimaryButton = new Color(0.92f, 0.45f, 0.15f);
        public static readonly Color SecondaryButton = new Color(0.18f, 0.30f, 0.52f);
        public static readonly Color SelectedButton = new Color(0.95f, 0.62f, 0.20f);
        public static readonly Color DangerButton = new Color(0.80f, 0.26f, 0.20f);
        public static readonly Color NeutralButton = new Color(0.28f, 0.30f, 0.40f);

        public static readonly Color Success = new Color(0.25f, 0.72f, 0.42f);
        public static readonly Color Warning = new Color(1f, 0.80f, 0.30f);
        public static readonly Color Danger = new Color(0.95f, 0.35f, 0.30f);

        public static readonly Color Fuel = new Color(0.40f, 0.85f, 0.40f);
        public static readonly Color Energy = new Color(0.30f, 0.80f, 0.95f);
        public static readonly Color TyreWear = new Color(0.90f, 0.45f, 0.30f);

        public static readonly Color Gold = new Color(1f, 0.86f, 0.32f);
        public static readonly Color PlayerHighlight = new Color(1f, 0.85f, 0.25f);
        public static readonly Color TextDim = new Color(0.78f, 0.82f, 0.92f);
        public static readonly Color Neon = new Color(0.35f, 0.75f, 1f);
    }
}
