using UnityEngine;

namespace MarbleGP.UI
{
    /// <summary>
    /// Tema visual central. Mantido por compatibilidade: agora aponta para a
    /// paleta premium MarbleUITheme, retonando todas as telas de uma vez.
    /// </summary>
    public static class UITheme
    {
        public static readonly Color BackgroundPanel = MarbleUITheme.BackgroundDark;
        public static readonly Color CardPanel = MarbleUITheme.PanelDark;
        public static readonly Color HeaderPanel = MarbleUITheme.PanelSoft;

        public static readonly Color PrimaryButton = MarbleUITheme.NeonOrange;
        public static readonly Color SecondaryButton = new Color32(18, 60, 105, 235);
        public static readonly Color SelectedButton = MarbleUITheme.NeonOrange;
        public static readonly Color DangerButton = MarbleUITheme.NeonRed;
        public static readonly Color NeutralButton = new Color32(34, 48, 66, 235);

        public static readonly Color Success = MarbleUITheme.NeonGreen;
        public static readonly Color Warning = MarbleUITheme.Warning;
        public static readonly Color Danger = MarbleUITheme.Danger;

        public static readonly Color Fuel = MarbleUITheme.Fuel;
        public static readonly Color Energy = MarbleUITheme.Energy;
        public static readonly Color TyreWear = MarbleUITheme.TyreWear;

        public static readonly Color Gold = MarbleUITheme.NeonGold;
        public static readonly Color Silver = MarbleUITheme.Silver;
        public static readonly Color Bronze = MarbleUITheme.Bronze;
        public static readonly Color PlayerHighlight = MarbleUITheme.NeonGold;
        public static readonly Color TextDim = MarbleUITheme.TextSecondary;
        public static readonly Color Neon = MarbleUITheme.NeonCyan;

        public static readonly Color Glass = MarbleUITheme.PanelDark;
        public static readonly Color GlassLight = MarbleUITheme.PanelSoft;
        public static readonly Color AccentRed = MarbleUITheme.NeonRed;

        public static Color Medal(int pos) => MarbleUITheme.Medal(pos);
    }
}
