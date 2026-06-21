using System;
using UnityEngine;
using MarbleGP.Core;
using MarbleGP.Data;

namespace MarbleGP.Systems
{
    /// <summary>
    /// Combustivel = autonomia (PRD 4.1). So volta no pit. O consumo base por
    /// volta escala com o numero de voltas (130/totalLaps) para garantir que
    /// ninguem complete a corrida inteira sem ao menos uma parada.
    /// </summary>
    public class FuelSystem
    {
        private readonly float trackLength;
        private readonly float baseFuelPerLap;

        /// <summary>Disparado quando o combustivel zera (modo emergencia).</summary>
        public event Action<MarbleRuntime> OnFuelEmpty;

        public float BaseFuelPerLap => baseFuelPerLap;

        public FuelSystem(TrackDataSO track, int totalLaps)
        {
            trackLength = Mathf.Max(1f, track.trackLength);
            baseFuelPerLap = 130f / Mathf.Max(1, totalLaps);
        }

        public void Apply(MarbleRuntime m, float distanceTraveled)
        {
            if (m.state == MarbleRaceState.InPit) return;

            float perLap = RaceFormulas.FuelUsePerLap(m, baseFuelPerLap);
            float lapFraction = distanceTraveled / trackLength;
            bool wasEmpty = m.fuel <= 0f;
            m.fuel = Mathf.Clamp(m.fuel - perLap * lapFraction, 0f, 100f);

            if (!wasEmpty && m.fuel <= 0f) OnFuelEmpty?.Invoke(m);
        }

        public void Refill(MarbleRuntime m) => m.fuel = 100f;

        /// <summary>Combustivel necessario (estimado) para completar 'laps' voltas no modo atual.</summary>
        public float FuelForLaps(MarbleRuntime m, float laps)
            => RaceFormulas.FuelUsePerLap(m, baseFuelPerLap) * laps;
    }
}
