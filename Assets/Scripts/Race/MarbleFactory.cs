using UnityEngine;
using MarbleGP.AI;
using MarbleGP.Core;
using MarbleGP.Systems;
using MarbleGP.Track;

namespace MarbleGP.Race
{
    /// <summary>
    /// Cria bolinhas em runtime como esferas 3D brilhantes coloridas pela
    /// equipe (PRD 22.2). Placeholder visual sem assets externos (PRD 39.8/9).
    /// </summary>
    public static class MarbleFactory
    {
        // Maior e mais visivel (PRD 3): ~2x o tamanho anterior.
        public const float Radius = 1.0f;

        public static MarbleController Spawn(MarbleRuntime runtime, Vector3 pos,
            TrackManager track, GameBalance bal, Transform parent)
        {
            var go = new GameObject($"Marble_{runtime.DisplayName}");
            if (parent != null) go.transform.SetParent(parent, false);
            // Apoia a bolinha sobre o solo (y = raio).
            go.transform.position = new Vector3(pos.x, Radius, pos.z);

            // Corpo fisico (esfera) - colisor separado do visual para a rolagem.
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.drag = 0.2f;
            rb.angularDrag = 0.5f;
            var col = go.AddComponent<SphereCollider>();
            col.radius = Radius;

            // Visual: esfera brilhante na cor primaria da equipe.
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            Object.Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * (Radius * 2f);
            Color primary = runtime.MarbleColor; // respeita customizacao da garagem (PRD 31)
            visual.GetComponent<MeshRenderer>().sharedMaterial =
                MaterialFactory.Create(primary, smoothness: 0.92f, metallic: 0.15f);

            // Marcador de equipe: ANEL fino na cor secundaria na base (legivel de
            // cima, sem o "ponto" grudado de antes). Nao rola (parented na raiz).
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "TeamRing";
            Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(go.transform, false);
            ring.transform.localScale = new Vector3(Radius * 2.5f, 0.05f, Radius * 2.5f);
            ring.transform.localPosition = new Vector3(0f, -Radius * 0.72f, 0f);
            ring.GetComponent<MeshRenderer>().sharedMaterial =
                MaterialFactory.Create(runtime.TeamSecondary, smoothness: 0.6f, metallic: 0.2f);

            var ctrl = go.AddComponent<MarbleController>();
            ctrl.Configure(runtime, track, bal, visual.transform, Radius);

            // Camada visual (sombra, etiqueta, rastro, brilho, pit overlay).
            var vis = go.AddComponent<MarbleVisual>();
            vis.Configure(runtime, Radius, visual.transform, primary);

            return ctrl;
        }
    }
}
