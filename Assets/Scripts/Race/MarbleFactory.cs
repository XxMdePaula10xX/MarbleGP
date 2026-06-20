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
        public const float Radius = 0.5f;

        public static MarbleController Spawn(MarbleRuntime runtime, Vector3 pos,
            TrackManager track, GameBalance bal, Transform parent)
        {
            var go = new GameObject($"Marble_{runtime.DisplayName}");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;

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
                MaterialFactory.Create(primary, smoothness: 0.85f, metallic: 0.2f);

            // Faixa/marcador na cor secundaria (PRD 22.2).
            var band = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            band.name = "Band";
            Object.Destroy(band.GetComponent<Collider>());
            band.transform.SetParent(visual.transform, false);
            band.transform.localScale = Vector3.one * 0.45f;
            band.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            Color secondary = runtime.TeamSecondary;
            band.GetComponent<MeshRenderer>().sharedMaterial =
                MaterialFactory.Create(secondary, smoothness: 0.6f);

            var ctrl = go.AddComponent<MarbleController>();
            ctrl.Configure(runtime, track, bal, visual.transform, Radius);
            return ctrl;
        }
    }
}
