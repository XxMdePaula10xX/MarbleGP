using System.Collections.Generic;
using UnityEngine;
using MarbleGP.Core;
using MarbleGP.Data;

namespace MarbleGP.Track
{
    /// <summary>
    /// Guarda a geometria de uma pista e responde consultas de navegacao
    /// (PRD 24.2 TrackManager). Para o MVP a geometria e construida por
    /// TrackBuilder a partir dos controlPoints da TrackDataSO.
    /// </summary>
    public class TrackManager : MonoBehaviour
    {
        public TrackDataSO Data { get; private set; }

        public Lane IdealLine { get; private set; }
        public Lane InsideLine { get; private set; }
        public Lane OutsideLine { get; private set; }
        public Lane PitLane { get; private set; }

        /// <summary>Posicoes dos checkpoints na ordem correta (PRD 9.3 / 26).</summary>
        public List<Vector3> Checkpoints { get; private set; } = new List<Vector3>();
        public int CheckpointCount => Checkpoints.Count;

        /// <summary>Fracao de arco (0..1) onde fica a linha de largada/chegada.</summary>
        public float StartFinishArc { get; private set; } = 0f;

        /// <summary>Fracao de arco onde a bolinha deve divergir para o pit.</summary>
        public float PitEntryArc { get; private set; } = 0.85f;
        /// <summary>Fracao onde retorna a pista vinda do pit.</summary>
        public float PitExitArc { get; private set; } = 0.05f;

        /// <summary>Posicoes de grid pre-calculadas (PRD 9.2).</summary>
        public List<Vector3> GridPositions { get; private set; } = new List<Vector3>();
        public List<Vector3> PitBoxes { get; private set; } = new List<Vector3>();

        public void Init(TrackDataSO data, Lane ideal, Lane inside, Lane outside, Lane pit)
        {
            Data = data;
            IdealLine = ideal;
            InsideLine = inside;
            OutsideLine = outside;
            PitLane = pit;
        }

        public Lane GetLane(RacingLine line)
        {
            switch (line)
            {
                case RacingLine.Inside: return InsideLine;
                case RacingLine.Outside: return OutsideLine;
                case RacingLine.Pit: return PitLane ?? IdealLine;
                default: return IdealLine;
            }
        }

        /// <summary>
        /// Ponto-alvo de direcao numa linha, dado o avanco (lookahead) em pontos
        /// a partir do ponto mais proximo da posicao informada (PRD 13.4).
        /// </summary>
        public Vector3 GetSteerTarget(RacingLine line, Vector3 fromPos, int lookahead)
        {
            Lane lane = GetLane(line);
            lane.ClosestArcFraction(fromPos, out int nearest);
            return lane.Point(nearest + Mathf.Max(1, lookahead));
        }

        /// <summary>Curvatura aproximada a frente (0 reto .. 1 curva fechada) p/ controle de velocidade.</summary>
        public float CurvatureAhead(Vector3 fromPos, int lookahead)
        {
            IdealLine.ClosestArcFraction(fromPos, out int nearest);
            Vector3 p0 = IdealLine.Point(nearest);
            Vector3 p1 = IdealLine.Point(nearest + lookahead);
            Vector3 p2 = IdealLine.Point(nearest + lookahead * 2);
            Vector3 a = (p1 - p0).normalized;
            Vector3 b = (p2 - p1).normalized;
            float dot = Vector3.Dot(a, b); // 1 reto .. -1 volta total
            return Mathf.Clamp01(1f - dot); // 0 reto .. ~1 curva forte
        }

        /// <summary>Fracao de arco da posicao na linha ideal (PRD 26, ordenacao fina).</summary>
        public float ArcFraction(Vector3 pos) => IdealLine.ClosestArcFraction(pos, out _);
    }
}
