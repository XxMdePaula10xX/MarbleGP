using UnityEngine;

namespace MarbleGP.UI
{
    /// <summary>
    /// Paleta visual central premium sci-fi racing. Versao menos saturada /
    /// mais "broadcast" (PRD de UI). Usada por todo o jogo via UITheme/UIFactory.
    /// </summary>
    public static class MarbleUITheme
    {
        // Paineis (escuros, glass)
        public static readonly Color BackgroundDark = new Color32(3, 9, 18, 235);
        public static readonly Color PanelDark = new Color32(5, 16, 28, 226);
        public static readonly Color PanelSoft = new Color32(8, 28, 44, 220);

        // Neon controlado (menos puro)
        public static readonly Color NeonBlue = new Color32(26, 132, 255, 255);
        public static readonly Color NeonCyan = new Color32(0, 220, 240, 255);
        public static readonly Color NeonOrange = new Color32(255, 116, 18, 255);
        public static readonly Color NeonGold = new Color32(255, 206, 70, 255);
        public static readonly Color NeonGreen = new Color32(46, 215, 128, 255);
        public static readonly Color NeonRed = new Color32(240, 56, 64, 255);
        public static readonly Color NeonPurple = new Color32(145, 82, 255, 255);

        public static readonly Color TextPrimary = new Color32(238, 248, 255, 255);
        public static readonly Color TextSecondary = new Color32(160, 185, 205, 255);
        public static readonly Color TextMuted = new Color32(95, 118, 140, 255);

        public static readonly Color Fuel = new Color32(73, 220, 112, 255);
        public static readonly Color Energy = new Color32(70, 195, 235, 255);
        public static readonly Color TyreWear = new Color32(230, 135, 70, 255);
        public static readonly Color Warning = new Color32(255, 200, 70, 255);
        public static readonly Color Danger = new Color32(242, 65, 72, 255);

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
