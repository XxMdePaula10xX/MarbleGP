using System.Collections.Generic;
using UnityEngine;
using MarbleGP.Core;

namespace MarbleGP.Data
{
    /// <summary>
    /// Dados de um circuito (PRD 21 / 24.1).
    /// Para o MVP a geometria da pista e gerada por codigo a partir dos
    /// "controlPoints" abaixo (ver TrackBuilder), evitando depender de
    /// cenas .unity feitas a mao. trackLocked permite placeholders (PRD 21.2).
    /// </summary>
    [CreateAssetMenu(fileName = "Track_", menuName = "MarbleGP/Track", order = 40)]
    public class TrackDataSO : ScriptableObject
    {
        public string trackId = "track_id";
        public string trackName = "New Track";
        [Tooltip("Cena dedicada, se existir. Vazio => usa geracao procedural na RaceScene.")]
        public string sceneName = "";

        [Header("Parametros de corrida (PRD 42)")]
        public Difficulty difficulty = Difficulty.Easy;
        public int recommendedLaps = 5;
        public float trackLength = 1000f;

        [Header("Caracteristicas (PRD 21)")]
        [Range(0.5f, 2f)] public float abrasionLevel = 1.0f;   // desgaste relativo
        [Range(0f, 1f)] public float overtakeLevel = 0.5f;     // facilidade de ultrapassagem
        [Range(0f, 1f)] public float rainChance = 0f;          // MVP: 0
        public float pitLaneTimeLoss = 4f;

        [Header("MVP / disponibilidade")]
        public bool trackLocked = false; // true => placeholder do campeonato completo

        [TextArea] public string description;
        public Sprite thumbnail;

        [Header("Geometria procedural (control points no plano XZ)")]
        [Tooltip("Pontos de controle do circuito (sentido horario). " +
                 "O TrackBuilder gera waypoints suaves entre eles formando um loop fechado.")]
        public List<Vector2> controlPoints = new List<Vector2>();

        [Tooltip("Largura da pista em unidades.")]
        public float trackWidth = 8f;

        [Tooltip("A cada quantos waypoints colocar um checkpoint (PRD 9.3 / 26).")]
        public int checkpointEvery = 4;
    }
}
