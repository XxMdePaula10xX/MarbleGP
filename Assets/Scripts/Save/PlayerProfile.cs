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

        /// <summary>Cor de cada bolinha do jogador (garagem, PRD 31). Vazio => usa cor da equipe.</summary>
        public string[] marbleColorHex = new string[2];

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

        /// <summary>Cor da bolinha i do jogador, ou null se nao customizada.</summary>
        public Color? GetMarbleColor(int i)
        {
            if (marbleColorHex == null || i < 0 || i >= marbleColorHex.Length) return null;
            if (string.IsNullOrEmpty(marbleColorHex[i])) return null;
            return ColorUtility.TryParseHtmlString(marbleColorHex[i], out var c) ? c : (Color?)null;
        }

        public void SetMarbleColor(int i, Color color)
        {
            if (marbleColorHex == null || marbleColorHex.Length < 2) marbleColorHex = new string[2];
            if (i < 0 || i >= marbleColorHex.Length) return;
            marbleColorHex[i] = "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private static Color ParseColor(string hex, Color fallback)
            => ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
    }
}
