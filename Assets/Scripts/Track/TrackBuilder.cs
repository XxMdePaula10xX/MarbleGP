using System.Collections.Generic;
using UnityEngine;
using MarbleGP.Data;

namespace MarbleGP.Track
{
    /// <summary>
    /// Constroi a geometria de uma pista a partir dos controlPoints da
    /// TrackDataSO: spline fechada (Catmull-Rom), linhas de corrida, malha de
    /// asfalto, grama ao redor, zebras (curbs), linha de chegada quadriculada,
    /// setas de sentido, pit lane com boxes coloridos, grid e checkpoints.
    /// Tudo procedural (abordagem code-first, sem cenas .unity manuais).
    /// </summary>
    public static class TrackBuilder
    {
        private const int SamplesPerSegment = 10;

        // Paleta do circuito (Marble Park: grama verde, asfalto azulado).
        private static readonly Color Grass = new Color(0.20f, 0.45f, 0.22f);
        private static readonly Color GrassDark = new Color(0.16f, 0.38f, 0.18f);
        private static readonly Color Asphalt = new Color(0.22f, 0.23f, 0.27f);
        private static readonly Color CurbRed = new Color(0.85f, 0.16f, 0.16f);
        private static readonly Color CurbWhite = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color Arrow = new Color(0.55f, 0.85f, 1f);
        private static readonly Color PitAsphalt = new Color(0.16f, 0.22f, 0.30f);

        public static TrackManager Build(TrackDataSO data, int marbleCount,
            IReadOnlyList<Color> teamColors = null, Transform parent = null)
        {
            var root = new GameObject($"Track_{data.trackName}");
            if (parent != null) root.transform.SetParent(parent, false);
            var tm = root.AddComponent<TrackManager>();

            Vector3[] center = SampleClosedSpline(data.controlPoints, SamplesPerSegment);
            if (center.Length < 8)
            {
                Debug.LogError($"[TrackBuilder] Pista '{data.trackName}' tem poucos control points.");
                return tm;
            }

            Vector3[] normals = ComputeNormals(center);
            float halfW = data.trackWidth * 0.5f;
            float laneOffset = data.trackWidth * 0.28f;

            Vector3[] ideal = center;
            Vector3[] inside = Offset(center, normals, -laneOffset);
            Vector3[] outside = Offset(center, normals, +laneOffset);
            Vector3[] pit = Offset(center, normals, halfW + 3.0f);

            tm.Init(data, new Lane(ideal), new Lane(inside), new Lane(outside), new Lane(pit));

            // Texturas opcionais (Assets/Resources/Textures). Fallback p/ cor solida.
            Texture2D asphaltTex = Resources.Load<Texture2D>("Textures/asphalt");
            Texture2D grassTex = Resources.Load<Texture2D>("Textures/grass");
            Texture2D curbTex = Resources.Load<Texture2D>("Textures/curb");

            // --- Cenario / pista ---
            BuildGround(root.transform, grassTex);
            BuildRoadMesh(root.transform, center, normals, halfW,
                MaterialFactory.CreateTextured(asphaltTex, Asphalt, 1f), "RoadMesh", 0f, 6f);
            BuildEdgeLines(root.transform, center, normals, halfW);
            BuildCurbs(root.transform, center, normals, halfW, curbTex);
            BuildCenterDashes(root.transform, center);
            BuildDirectionArrows(root.transform, center);
            BuildCheckeredLine(root.transform, center[0], normals[0], data.trackWidth);

            // --- Checkpoints ---
            tm.Checkpoints.Clear();
            int every = Mathf.Max(2, data.checkpointEvery);
            for (int i = 0; i < center.Length; i += every)
                tm.Checkpoints.Add(center[i]);

            // --- Grid, pit lane real e decoracao ---
            BuildGrid(tm, center, normals, marbleCount);
            BuildGridMarkers(root.transform, tm);
            BuildPitLane(root.transform, tm, center, normals, halfW, marbleCount, teamColors);
            BuildSign(root.transform, center[0], normals[0], halfW, data.trackName);

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

        // ---- Cenario -----------------------------------------------------

        private static void BuildGround(Transform parent, Texture2D grassTex)
        {
            // Grande plano de grama sob todo o circuito.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Grass";
            Object.Destroy(ground.GetComponent<Collider>());
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f); // Plane = 10u -> 400u
            // Tiling 40 => ~10 unidades por tile. Tint dessatura/escurece a grama ~18%
            // para nao cansar a vista e dar mais contraste com a pista (PRD 8).
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                MaterialFactory.CreateTextured(grassTex, Grass, 40f, new Color(0.78f, 0.82f, 0.78f));
        }

        // ---- Mesh de pista -----------------------------------------------

        private static void BuildRoadMesh(Transform parent, Vector3[] center, Vector3[] normals,
            float halfW, Material mat, string name, float y, float uvTile = 0f, bool closed = true)
        {
            int n = center.Length;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            Vector3 up = Vector3.up * y;
            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) dist += Vector3.Distance(center[i - 1], center[i]);
                verts[i * 2] = center[i] + normals[i] * halfW + up;
                verts[i * 2 + 1] = center[i] - normals[i] * halfW + up;
                if (uvTile > 0f)
                {
                    // UVs em escala de mundo: textura repete a cada 'uvTile' unidades.
                    float u = dist / uvTile;
                    float vEdge = (2f * halfW) / uvTile;
                    uvs[i * 2] = new Vector2(u, 0f);
                    uvs[i * 2 + 1] = new Vector2(u, vEdge);
                }
                else
                {
                    float u = i / (float)n;
                    uvs[i * 2] = new Vector2(u, 0f);
                    uvs[i * 2 + 1] = new Vector2(u, 1f);
                }
            }

            var tris = new List<int>(n * 6);
            int segCount = closed ? n : n - 1; // faixa aberta nao fecha o loop
            for (int i = 0; i < segCount; i++)
            {
                int a = i * 2, b = i * 2 + 1;
                int c = ((i + 1) % n) * 2, d = ((i + 1) % n) * 2 + 1;
                // Winding com a face para CIMA (+Y), senao a camera top-down
                // descarta os triangulos por back-face culling (pista invisivel).
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
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
            var mat = MaterialFactory.CreateUnlit(Color.white);
            BuildThinStrip(parent, Offset(center, normals, halfW - 0.2f), 0.18f, mat, "EdgeR");
            BuildThinStrip(parent, Offset(center, normals, -(halfW - 0.2f)), 0.18f, mat, "EdgeL");
        }

        private static void BuildThinStrip(Transform parent, Vector3[] line, float width, Material mat, string name)
        {
            var normals = ComputeNormals(line);
            BuildRoadMesh(parent, line, normals, width, mat, name, 0.03f);
        }

        // ---- Zebras (curbs) ----------------------------------------------

        private static void BuildCurbs(Transform parent, Vector3[] center, Vector3[] normals, float halfW, Texture2D curbTex)
        {
            var holder = new GameObject("Curbs");
            holder.transform.SetParent(parent, false);
            Vector3[] right = Offset(center, normals, halfW + 0.35f);
            Vector3[] left = Offset(center, normals, -(halfW + 0.35f));

            if (curbTex != null)
            {
                // Faixa continua texturizada (zebra repete a cada ~2 unidades).
                var mat = MaterialFactory.CreateTextured(curbTex, CurbRed, 1f);
                BuildRoadMesh(holder.transform, right, ComputeNormals(right), 0.5f, mat, "CurbR", 0.06f, 2f);
                BuildRoadMesh(holder.transform, left, ComputeNormals(left), 0.5f, mat, "CurbL", 0.06f, 2f);
            }
            else
            {
                var red = MaterialFactory.CreateUnlit(CurbRed);
                var white = MaterialFactory.CreateUnlit(CurbWhite);
                CurbStrip(holder.transform, right, red, white);
                CurbStrip(holder.transform, left, red, white);
            }
        }

        private static void CurbStrip(Transform parent, Vector3[] line, Material red, Material white)
        {
            int n = line.Length;
            for (int i = 0; i < n; i += 2)
            {
                Vector3 a = line[i];
                Vector3 b = line[(i + 2) % n];
                Vector3 mid = (a + b) * 0.5f + Vector3.up * 0.06f;
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.01f) continue;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "Curb";
                Object.Destroy(seg.GetComponent<Collider>());
                seg.transform.SetParent(parent, false);
                seg.transform.position = mid;
                seg.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                seg.transform.localScale = new Vector3(0.7f, 0.12f, len);
                seg.GetComponent<MeshRenderer>().sharedMaterial = ((i / 2) % 2 == 0) ? red : white;
            }
        }

        // ---- Marcacoes de pista ------------------------------------------

        private static void BuildCenterDashes(Transform parent, Vector3[] center)
        {
            var holder = new GameObject("CenterDashes");
            holder.transform.SetParent(parent, false);
            var mat = MaterialFactory.CreateUnlit(new Color(0.85f, 0.85f, 0.5f));
            int n = center.Length;
            for (int i = 4; i < n; i += 5) // pula a regiao da largada
            {
                Vector3 a = center[i];
                Vector3 b = center[(i + 1) % n];
                Vector3 dir = b - a;
                if (dir.magnitude < 0.01f) continue;
                var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dash.name = "Dash";
                Object.Destroy(dash.GetComponent<Collider>());
                dash.transform.SetParent(holder.transform, false);
                dash.transform.position = a + Vector3.up * 0.04f;
                dash.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                dash.transform.localScale = new Vector3(0.16f, 0.02f, 1.1f);
                dash.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        private static void BuildDirectionArrows(Transform parent, Vector3[] center)
        {
            var holder = new GameObject("DirectionArrows");
            holder.transform.SetParent(parent, false);
            var mat = MaterialFactory.CreateUnlit(Arrow);
            int n = center.Length;
            for (int i = 6; i < n; i += 14)
            {
                Vector3 c = center[i] + Vector3.up * 0.05f;
                Vector3 f = (center[(i + 1) % n] - center[i]).normalized;
                Vector3 r = Vector3.Cross(Vector3.up, f).normalized;
                Vector3 tip = c + f * 1.0f;
                Bar(holder.transform, c - f * 0.5f - r * 0.7f, tip, mat);
                Bar(holder.transform, c - f * 0.5f + r * 0.7f, tip, mat);
            }
        }

        private static void Bar(Transform parent, Vector3 a, Vector3 b, Material mat)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) return;
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Bar";
            Object.Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(parent, false);
            bar.transform.position = (a + b) * 0.5f;
            bar.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            bar.transform.localScale = new Vector3(0.22f, 0.02f, len);
            bar.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void BuildCheckeredLine(Transform parent, Vector3 pos, Vector3 normal, float width)
        {
            var holder = new GameObject("StartFinishLine");
            holder.transform.SetParent(parent, false);
            var black = MaterialFactory.CreateUnlit(new Color(0.08f, 0.08f, 0.08f));
            var white = MaterialFactory.CreateUnlit(Color.white);

            Vector3 along = Vector3.Cross(normal, Vector3.up).normalized; // ao longo da pista
            float sq = 0.7f;
            int cols = Mathf.Max(2, Mathf.RoundToInt(width / sq));
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    float x = (col - (cols - 1) * 0.5f) * sq;
                    Vector3 p = pos + normal * x + along * (row * sq) + Vector3.up * 0.05f;
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Check";
                    Object.Destroy(cube.GetComponent<Collider>());
                    cube.transform.SetParent(holder.transform, false);
                    cube.transform.position = p;
                    cube.transform.rotation = Quaternion.LookRotation(along, Vector3.up);
                    cube.transform.localScale = new Vector3(sq, 0.04f, sq);
                    cube.GetComponent<MeshRenderer>().sharedMaterial = ((row + col) % 2 == 0) ? black : white;
                }
            }
        }

        // ---- Grid e pit --------------------------------------------------

        private static void BuildGrid(TrackManager tm, Vector3[] center, Vector3[] normals, int marbleCount)
        {
            tm.GridPositions.Clear();
            int n = center.Length;
            float spacingPts = 2.5f;
            for (int i = 0; i < marbleCount; i++)
            {
                int idx = (n - 3 - Mathf.RoundToInt(i * spacingPts)) % n;
                idx = (idx + n) % n;
                float side = (i % 2 == 0) ? -1f : 1f;
                Vector3 pos = center[idx] + normals[idx] * (side * 1.4f) + Vector3.up * 0.5f;
                tm.GridPositions.Add(pos);
            }
        }

        private static void BuildGridMarkers(Transform parent, TrackManager tm)
        {
            var holder = new GameObject("GridMarkers");
            holder.transform.SetParent(parent, false);
            var mat = MaterialFactory.CreateUnlit(new Color(0.9f, 0.9f, 0.9f, 1f));
            for (int i = 0; i < tm.GridPositions.Count; i++)
            {
                Vector3 p = tm.GridPositions[i];
                p.y = 0.035f;
                var slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slot.name = $"GridSlot{i + 1}";
                Object.Destroy(slot.GetComponent<Collider>());
                slot.transform.SetParent(holder.transform, false);
                slot.transform.position = p;
                slot.transform.localScale = new Vector3(1.3f, 0.02f, 0.25f);
                slot.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        /// <summary>
        /// Pit lane REAL (PRD 3 / 18): caminho finito que ramifica da reta principal,
        /// segue paralelo pelos boxes e reentra na pista. Constroi o caminho de
        /// navegacao (tm.PitPath), a malha visual, a faixa separadora, os boxes
        /// coloridos por equipe e as placas PIT IN / PIT OUT.
        /// </summary>
        private static void BuildPitLane(Transform parent, TrackManager tm, Vector3[] center,
            Vector3[] normals, float halfW, int marbleCount, IReadOnlyList<Color> teamColors)
        {
            int n = center.Length;
            int pitLen = Mathf.Clamp(Mathf.RoundToInt(n * 0.24f), 14, n / 2);
            float maxOffset = halfW + 3.2f; // distancia do pit lane ate o centro da pista
            const float side = 1f;          // lado externo

            // Caminho do pit (com rampas de entrada/saida e plato no meio).
            var path = new Vector3[pitLen + 1];
            for (int k = 0; k <= pitLen; k++)
            {
                int idx = k % n;
                float t = k / (float)pitLen;
                float ramp = (t < 0.18f) ? Mathf.SmoothStep(0f, 1f, t / 0.18f)
                          : (t > 0.82f) ? Mathf.SmoothStep(0f, 1f, (1f - t) / 0.18f)
                          : 1f;
                path[k] = center[idx] + normals[idx] * (side * maxOffset * ramp);
            }
            tm.PitPath = new List<Vector3>(path);

            // Malha do pit lane (faixa aberta).
            var pitNormals = ComputeNormals(path);
            BuildRoadMesh(parent, path, pitNormals, 1.7f, MaterialFactory.Create(PitAsphalt),
                "PitLane", 0.02f, 0f, false);

            // Boxes na regiao do plato, um por bolinha.
            var holder = new GameObject("PitBoxes");
            holder.transform.SetParent(parent, false);
            tm.PitBoxes.Clear();
            tm.PitBoxPathIndex = new List<int>();
            for (int i = 0; i < marbleCount; i++)
            {
                float bt = marbleCount > 1 ? 0.34f + (i / (float)(marbleCount - 1)) * 0.32f : 0.5f;
                int bk = Mathf.Clamp(Mathf.RoundToInt(bt * pitLen), 2, pitLen - 2);
                Vector3 stop = path[bk];
                tm.PitBoxes.Add(stop);
                tm.PitBoxPathIndex.Add(bk);

                // Garagem (pad colorido) um pouco mais para fora do pit lane.
                int cidx = bk % n;
                Vector3 pad = path[bk] + normals[cidx] * (side * 1.6f);
                Color c = (teamColors != null && i < teamColors.Count) ? teamColors[i] : Color.gray;
                var padGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                padGo.name = $"PitBox{i + 1}";
                Object.Destroy(padGo.GetComponent<Collider>());
                padGo.transform.SetParent(holder.transform, false);
                padGo.transform.position = new Vector3(pad.x, 0.06f, pad.z);
                padGo.transform.localScale = new Vector3(1.5f, 0.05f, 1.5f);
                padGo.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.CreateUnlit(c);
            }

            // Placas pequenas nas pontas reais do pit lane.
            BuildText(holder.transform, path[1] + Vector3.up * 0.3f, "PIT IN", 1.6f, new Color(0.4f, 1f, 0.5f));
            BuildText(holder.transform, path[pitLen - 1] + Vector3.up * 0.3f, "PIT OUT", 1.6f, new Color(0.9f, 0.9f, 1f));
        }

        private static void BuildSign(Transform parent, Vector3 pos, Vector3 normal, float halfW, string trackName)
        {
            // Menor e bem afastado da pista para nao atrapalhar a leitura (PRD 8).
            Vector3 p = pos + normal * (halfW + 10f) + Vector3.up * 0.2f;
            BuildText(parent, p, trackName.ToUpper(), 2.6f, new Color(0.95f, 0.92f, 0.7f, 0.8f));
        }

        private static void BuildText(Transform parent, Vector3 pos, string text, float size, Color color)
        {
            var go = new GameObject("Text3D");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // deitado, legivel de cima
            var tm = go.AddComponent<TextMesh>();
            MaterialFactory.ApplyFont(tm);
            tm.text = text;
            tm.characterSize = 0.15f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.transform.localScale = Vector3.one * size;
        }
    }
}
