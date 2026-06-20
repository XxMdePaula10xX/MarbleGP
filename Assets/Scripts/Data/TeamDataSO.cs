using UnityEngine;
using MarbleGP.Core;

namespace MarbleGP.Data
{
    /// <summary>Dados base de uma equipe (PRD 11 / 24.1).</summary>
    [CreateAssetMenu(fileName = "Team_", menuName = "MarbleGP/Team", order = 10)]
    public class TeamDataSO : ScriptableObject
    {
        public string teamId = "team_id";
        public string teamName = "New Team";

        [Header("Identidade visual")]
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.black;
        public string emblemId = "default";

        [Header("Caracteristicas")]
        public TeamStyle teamStyle = TeamStyle.Balanced;
        public TeamBonusType baseBonusType = TeamBonusType.None;

        [Range(1, 100)] public int pitCrewRating = 50;
        [Range(1, 100)] public int developmentRating = 50;

        [TextArea] public string description;
    }
}
