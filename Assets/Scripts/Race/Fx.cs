using UnityEngine;
using MarbleGP.Track;

namespace MarbleGP.Race
{
    /// <summary>
    /// Particula simples e auto-destrutiva (cartoon, sem assets externos):
    /// voa para fora, encolhe e some. Usada em faiscas/fumaca de contato e batida.
    /// </summary>
    public class FxBit : MonoBehaviour
    {
        private Vector3 _vel;
        private float _life, _age;
        private Vector3 _baseScale;

        public void Init(Vector3 velocity, float life)
        {
            _vel = velocity;
            _life = life;
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.position += _vel * Time.deltaTime;
            _vel += Vector3.down * 5f * Time.deltaTime; // leve "gravidade"
            float t = 1f - _age / _life;
            transform.localScale = _baseScale * Mathf.Max(0f, t);
            if (_age >= _life) Destroy(gameObject);
        }
    }

    /// <summary>Helper para disparar bursts de particulas simples.</summary>
    public static class Fx
    {
        public static void Burst(Vector3 pos, Color color, int count, float speed, float size, float life)
        {
            for (int i = 0; i < count; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(s.GetComponent<Collider>());
                s.transform.position = pos;
                s.transform.localScale = Vector3.one * size * Random.Range(0.7f, 1.3f);
                s.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.CreateUnlit(color);

                Vector3 dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.3f; // tende para cima
                s.AddComponent<FxBit>().Init(dir.normalized * speed * Random.Range(0.6f, 1.2f), life);
            }
        }
    }
}
