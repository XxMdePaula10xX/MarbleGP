using UnityEngine;

namespace MarbleGP.Track
{
    /// <summary>
    /// Uma linha de corrida fechada (loop) como sequencia de pontos com
    /// distancias cumulativas pre-calculadas para projecao por comprimento de arco.
    /// </summary>
    public class Lane
    {
        public readonly Vector3[] Points;
        public readonly float[] CumDist; // distancia cumulativa ate cada ponto
        public readonly float TotalLength;

        public int Count => Points.Length;

        public Lane(Vector3[] points)
        {
            Points = points;
            CumDist = new float[points.Length];
            float acc = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                CumDist[i] = acc;
                Vector3 next = points[(i + 1) % points.Length];
                acc += Vector3.Distance(points[i], next);
            }
            TotalLength = acc;
        }

        public Vector3 Point(int index) => Points[((index % Count) + Count) % Count];

        /// <summary>
        /// Projeta uma posicao no loop e retorna a fracao 0..1 ao longo do arco,
        /// alem do indice do segmento mais proximo (out).
        /// </summary>
        public float ClosestArcFraction(Vector3 pos, out int nearestIndex)
        {
            float bestSqr = float.MaxValue;
            float bestArc = 0f;
            nearestIndex = 0;

            for (int i = 0; i < Count; i++)
            {
                Vector3 a = Points[i];
                Vector3 b = Point(i + 1);
                Vector3 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(pos - a, ab) / len2) : 0f;
                Vector3 proj = a + ab * t;
                float d = (pos - proj).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    float segLen = Mathf.Sqrt(len2);
                    bestArc = CumDist[i] + segLen * t;
                    nearestIndex = i;
                }
            }
            return TotalLength > 0f ? bestArc / TotalLength : 0f;
        }
    }
}
