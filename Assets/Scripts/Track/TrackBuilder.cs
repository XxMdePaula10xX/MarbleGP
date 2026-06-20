using System.Collections.Generic;
using UnityEngine;
using MarbleGP.Data;

namespace MarbleGP.Track
{
    /// <summary>
    /// Constroi a geometria de uma pista a partir dos controlPoints da
    /// TrackDataSO: gera uma spline fechada (Catmull-Rom), as linhas de
    /// corrida, a malha visual da pista, o pit lane, o grid e os checkpoints.
    /// Evita depender de cenas .unity feitas a mao (abordagem code-first).
    /// </summary>
    public static class TrackBuilder
    {
        private const int SamplesPerSegment = 10;

        public static TrackManager Build(TrackDataSO data, int marbleCount, Transform parent = null)
        {
            var root = new GameObject($"Track_{data.trackName}");
            if (parent != null) root.transform.SetParent(parent, false);
            var tm = root.AddComponent<TrackManager>();

            // 1) Centerline suave (loop fechado) a partir dos control points.
            Vector3[] center = SampleClosedSpline(data.controlPoints, SamplesPerSegment);
            if (center.Length < 8)
            {
                Debug.LogError($"[TrackBuilder] Pista '{data.trackName}' tem poucos control points.");
                return tm;
            }

            // 2) Normais (perpendicular no plano XZ) para deslocar as linhas.
            Vector3[] normals = ComputeNormals(center);

            float halfW = data.trackWidth * 0.5f;
            float laneOffset = data.trackWidth * 0.28f;

            Vector3[] ideal = center;
            Vector3[] inside = Offset(center, normals, -laneOffset);
            Vector3[] outside = Offset(center, normals, +laneOffset);
            // Pit lane: loop paralelo deslocado para fora da pista.
            Vector3[] pit = Offset(center, normals, halfW + 2.5f);

            tm.Init(data,
                new Lane(ideal), new Lane(inside), new Lane(outside), new Lane(pit));

            // 3) Malha visual da pista + pit lane + linha de chegada.
            BuildRoadMesh(root.transform, center, normals, halfW,
                MaterialFactory.Create(new Color(0.18f, 0.19f, 0.22f)), "RoadMesh");
            BuildRoadMesh(root.transform, pit, normals, 1.6f,
                MaterialFactory.Create(new Color(0.10f, 0.30f, 0.38f)), "PitMesh");
            BuildEdgeLines(root.transform, center, normals, halfW);
            BuildStartLine(root.transform, center[0], normals[0], data.trackWidth);

            // 4) Checkpoints (PRD 9.3 / 26).
            tm.Checkpoints.Clear();
            int every = Mathf.Max(2, data.checkpointEvery);
            for (int i = 0; i < center.Length; i += every)
                tm.Checkpoints.Add(center[i]);

            // 5) Grid de largada atras da linha de chegada (PRD 9.2).
            BuildGrid(tm, center, normals, marbleCount);

            // 6) Boxes do pit (um por par; reaproveitados).
            BuildPitBoxes(tm, pit, marbleCount);

            return tm;
        }

        // ---- Spline ------------------------------------------------------

        private static Vector3[] SampleClosedSpline(List<Vector2> cps, int perSeg)
        {
            int n = cps.Count;
            var pts = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = cps[(i - 1 + n) % n];
                Vector2 p1 = cps[i];
                Vector2 p2 = cps[(i + 1) % n];
                Vector2 p3 = cps[(i + 2) % n];
                for (int s = 0; s < perSeg; s++)
                {
                    float t = s / (float)perSeg;
                    Vector2 p = CatmullRom(p0, p1, p2, p3, t);
                    pts.Add(new Vector3(p.x, 0f, p.y));
                }
            }
            return pts.ToArray();
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Vector3[] ComputeNormals(Vector3[] pts)
        {
            int n = pts.Length;
            var normals = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = pts[(i - 1 + n) % n];
                Vector3 next = pts[(i + 1) % n];
                Vector3 tangent = (next - prev).normalized;
                // Normal a direita no plano XZ.
                normals[i] = new Vector3(tangent.z, 0f, -tangent.x).normalized;
            }
            return normals;
        }

        private static Vector3[] Offset(Vector3[] pts, Vector3[] normals, float dist)
        {
            var outp = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++)
                outp[i] = pts[i] + normals[i] * dist;
            return outp;
        }

        // ---- Mesh --------------------------------------------------------

        private static void BuildRoadMesh(Transform parent, Vector3[] center, Vector3[] normals,
            float halfW, Material mat, string name)
        {
            int n = center.Length;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
            {
                verts[i * 2] = center[i] + normals[i] * halfW;
                verts[i * 2 + 1] = center[i] - normals[i] * halfW;
                float u = i / (float)n;
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
            }

            var tris = new List<int>(n * 6);
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = ((i + 1) % n) * 2;
                int d = ((i + 1) % n) * 2 + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

            var mesh = new Mesh { name = name };
            mesh.indexFormat = verts.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void BuildEdgeLines(Transform parent, Vector3[] center, Vector3[] normals, float halfW)
        {
            var mat = MaterialFactory.Create(Color.white);
            BuildThinStrip(parent, Offset(center, normals, halfW), 0.25f, mat, "EdgeR");
            BuildThinStrip(parent, Offset(center, normals, -halfW), 0.25f, mat, "EdgeL");
        }

        private static void BuildThinStrip(Transform parent, Vector3[] line, float width, Material mat, string name)
        {
            var normals = ComputeNormals(line);
            BuildRoadMesh(parent, RaiseY(line, 0.02f), normals, width, mat, name);
        }

        private static Vector3[] RaiseY(Vector3[] pts, float y)
        {
            var outp = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++) outp[i] = pts[i] + Vector3.up * y;
            return outp;
        }

        private static void BuildStartLine(Transform parent, Vector3 pos, Vector3 normal, float width)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "StartFinishLine";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 0.03f;
            go.transform.rotation = Quaternion.LookRotation(Vector3.Cross(normal, Vector3.up), Vector3.up);
            go.transform.localScale = new Vector3(width, 0.05f, 0.6f);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.Create(Color.white);
        }

        // ---- Grid e pit boxes --------------------------------------------

        private static void BuildGrid(TrackManager tm, Vector3[] center, Vector3[] normals, int marbleCount)
        {
            tm.GridPositions.Clear();
            // Comeca um pouco atras da linha de chegada, alternando lados (PRD 9.2).
            int n = center.Length;
            float spacingPts = 2.5f; // em "passos" de waypoint
            for (int i = 0; i < marbleCount; i++)
            {
                int idx = (n - 2 - Mathf.RoundToInt(i * spacingPts)) % n;
                idx = (idx + n) % n;
                float side = (i % 2 == 0) ? -1f : 1f;
                Vector3 pos = center[idx] + normals[idx] * (side * 1.2f) + Vector3.up * 0.5f;
                tm.GridPositions.Add(pos);
            }
        }

        private static void BuildPitBoxes(TrackManager tm, Vector3[] pit, int marbleCount)
        {
            tm.PitBoxes.Clear();
            int n = pit.Length;
            // Boxes distribuidos na metade "de tras" do pit lane.
            int start = Mathf.RoundToInt(n * 0.45f);
            for (int i = 0; i < marbleCount; i++)
            {
                int idx = (start + i * 2) % n;
                tm.PitBoxes.Add(pit[idx] + Vector3.up * 0.5f);
            }
        }
    }
}
