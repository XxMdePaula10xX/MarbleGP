using System;
using UnityEngine;
using MarbleGP.Core;

namespace MarbleGP.Systems
{
    /// <summary>
    /// Clima dinamico (PRD 19). O tempo evolui na escala
    /// Dry &lt;-&gt; Cloudy &lt;-&gt; Damp &lt;-&gt; LightRain &lt;-&gt; HeavyRain ao longo da
    /// corrida, com probabilidade de piorar guiada pelo rainChance da pista.
    /// </summary>
    public class WeatherSystem
    {
        public Weather Current { get; private set; }

        /// <summary>Disparado quando o clima muda (para log/HUD).</summary>
        public event Action<Weather> OnChanged;

        private readonly float _rainChance;
        private float _timer;

        public WeatherSystem(Weather start, float rainChance)
        {
            Current = start;
            _rainChance = Mathf.Clamp01(rainChance);
            _timer = NextInterval();
        }

        public bool IsWet =>
            Current == Weather.Damp || Current == Weather.LightRain || Current == Weather.HeavyRain;

        /// <summary>Avalia possiveis transicoes de clima (mais dinamico, PRD 10).</summary>
        public void Tick(float dt)
        {
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = NextInterval();

            int idx = (int)Current;
            // Probabilidade de piorar guiada pela chance de chuva da pista (+base).
            float worsenProb = Mathf.Clamp01(_rainChance * 0.9f + 0.04f);
            float r = UnityEngine.Random.value;
            int delta = 0;

            if (r < worsenProb)
                delta = (UnityEngine.Random.value < 0.25f && _rainChance > 0.25f) ? 2 : 1; // pode pular 2 niveis
            else if (r > 0.7f)
                delta = -1; // tende a melhorar com o tempo

            int next = Mathf.Clamp(idx + delta, 0, 4);
            if (next != idx)
            {
                Current = (Weather)next;
                OnChanged?.Invoke(Current);
            }
        }

        private static float NextInterval() => UnityEngine.Random.Range(5f, 10f);

        /// <summary>Clima inicial sorteado a partir da chance de chuva da pista.</summary>
        public static Weather InitialFor(float rainChance)
        {
            float r = UnityEngine.Random.value;
            if (r < rainChance * 0.4f) return Weather.Damp;
            if (r < rainChance) return Weather.Cloudy;
            return Weather.Dry;
        }
    }
}
