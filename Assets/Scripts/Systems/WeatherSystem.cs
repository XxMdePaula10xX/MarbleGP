using System;
using UnityEngine;
using MarbleGP.Core;

namespace MarbleGP.Systems
{
    /// <summary>
    /// Clima dinamico controlado POR VOLTA (PRD 10): no maximo 1-2(-3) mudancas
    /// por corrida, nunca na 1a volta, com intervalo minimo entre mudancas e
    /// transicoes logicas de apenas 1 nivel por vez
    /// (Seco-Nublado-Umido-ChuvaLeve-ChuvaForte).
    /// </summary>
    public class WeatherSystem
    {
        public Weather Current { get; private set; }
        public event Action<Weather> OnChanged;

        private readonly float _rainChance;
        private readonly int _maxChanges;
        private const int MinLapGap = 3;

        private int _changes;
        private int _lastChangeLap = -99;

        public WeatherSystem(Weather start, float rainChance, int totalLaps)
        {
            Current = start;
            _rainChance = Mathf.Clamp01(rainChance);
            _maxChanges = totalLaps <= 6 ? 1 : (totalLaps <= 15 ? 2 : 3);
        }

        public int MaxChanges => _maxChanges;

        public bool IsWet =>
            Current == Weather.Damp || Current == Weather.LightRain || Current == Weather.HeavyRain;

        /// <summary>Avalia uma possivel mudanca de clima ao completar uma volta (lider).</summary>
        public void EvaluateLap(int leaderLap)
        {
            if (leaderLap < 2) return;                          // nunca na 1a volta
            if (_changes >= _maxChanges) return;                // limite por corrida
            if (leaderLap - _lastChangeLap < MinLapGap) return; // intervalo minimo

            float p = Mathf.Clamp01(_rainChance * 0.55f + 0.1f);
            if (UnityEngine.Random.value > p) return;

            int idx = (int)Current;
            int dir;
            if (idx <= 0) dir = +1;            // do seco so da para piorar
            else if (idx >= 4) dir = -1;       // da chuva forte so melhora
            else
            {
                // Tende a piorar conforme rainChance; senao melhora. Sempre 1 nivel.
                dir = UnityEngine.Random.value < (_rainChance * 0.6f + 0.35f) ? +1 : -1;
            }

            int next = Mathf.Clamp(idx + dir, 0, 4);
            if (next == idx) return;

            Current = (Weather)next;
            _changes++;
            _lastChangeLap = leaderLap;
            OnChanged?.Invoke(Current);
        }

        /// <summary>Clima inicial sorteado a partir da chance de chuva da pista.</summary>
        public static Weather InitialFor(float rainChance)
        {
            float r = UnityEngine.Random.value;
            if (r < rainChance * 0.35f) return Weather.Damp;
            if (r < rainChance * 0.8f) return Weather.Cloudy;
            return Weather.Dry;
        }
    }
}
