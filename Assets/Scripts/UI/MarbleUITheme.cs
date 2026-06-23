using UnityEngine;

namespace MarbleGP.UI
{
    /// <summary>
    /// Paleta visual central premium sci-fi racing (referencia do PRD de UI).
    /// Usada por todo o jogo via UITheme/UIFactory para identidade consistente.
    /// </summary>
    public static class MarbleUITheme
    {
        public static readonly Color BackgroundDark = new Color32(5, 10, 18, 235);
        public static readonly Color PanelDark = new Color32(8, 18, 32, 225);
        public static readonly Color PanelSoft = new Color32(12, 28, 48, 210);

        public static readonly Color NeonBlue = new Color32(0, 170, 255, 255);
        public static readonly Color NeonCyan = new Color32(0, 235, 255, 255);
        public static readonly Color NeonOrange = new Color32(255, 120, 18, 255);
        public static readonly Color NeonGold = new Color32(255, 205, 64, 255);
        public static readonly Color NeonGreen = new Color32(40, 230, 130, 255);
        public static readonly Color NeonRed = new Color32(255, 62, 62, 255);
        public static readonly Color NeonPurple = new Color32(150, 75, 255, 255);

        public static readonly Color TextPrimary = new Color32(240, 248, 255, 255);
        public static readonly Color TextSecondary = new Color32(160, 185, 210, 255);
        public static readonly Color TextMuted = new Color32(100, 120, 145, 255);

        public static readonly Color Fuel = new Color32(65, 220, 120, 255);
        public static readonly Color Energy = new Color32(45, 180, 255, 255);
        public static readonly Color TyreWear = new Color32(255, 135, 55, 255);
        public static readonly Color Warning = new Color32(255, 205, 64, 255);
        public static readonly Color Danger = new Color32(255, 70, 70, 255);

        public static readonly Color Silver = new Color32(205, 214, 230, 255);
        public static readonly Color Bronze = new Color32(216, 140, 82, 255);

        /// <summary>Cor de medalha por posicao (1=ouro,2=prata,3=bronze).</summary>
        public static Color Medal(int pos)
            => pos == 1 ? NeonGold : pos == 2 ? Silver : pos == 3 ? Bronze : TextSecondary;

        /// <summary>Letra + cor do composto de pneu (badge).</summary>
        public static void TyreInfo(string gripId, out string letter, out Color color)
        {
            switch (gripId)
            {
                case "Soft": letter = "S"; color = NeonRed; break;
                case "Medium": letter = "M"; color = NeonGold; break;
                case "Hard": letter = "H"; color = new Color32(220, 226, 236, 255); break;
                case "Intermediate": letter = "I"; color = NeonGreen; break;
                case "Rain": letter = "W"; color = NeonBlue; break;
                default: letter = "?"; color = TextSecondary; break;
            }
        }
    }
}
