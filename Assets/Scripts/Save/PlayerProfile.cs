using System;
using UnityEngine;

namespace MarbleGP.Save
{
    /// <summary>
    /// Perfil local do jogador (PRD 7.1). Serializavel para JSON.
    /// Cores guardadas como HTML (#RRGGBB) para portabilidade do JSON.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public string playerName = "Player";
        public string teamName = "My Team";
        public string primaryColorHex = "#E62020";
        public string secondaryColorHex = "#202020";
        public string emblemId = "default";
        public int difficulty = 1; // 0=Easy 1=Medium 2=Hard
        public bool created = false;

        public Color PrimaryColor
        {
            get => ParseColor(primaryColorHex, Color.red);
            set => primaryColorHex = "#" + ColorUtility.ToHtmlStringRGB(value);
        }

        public Color SecondaryColor
        {
            get => ParseColor(secondaryColorHex, Color.black);
            set => secondaryColorHex = "#" + ColorUtility.ToHtmlStringRGB(value);
        }

        private static Color ParseColor(string hex, Color fallback)
            => ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
    }
}
