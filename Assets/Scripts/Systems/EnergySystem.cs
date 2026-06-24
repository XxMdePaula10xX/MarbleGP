using System;
using UnityEngine;
using MarbleGP.Core;
using MarbleGP.Data;

namespace MarbleGP.Systems
{
    /// <summary>
    /// Calcula e aplica consumo do nucleo de energia (PRD 15 / 24.2).
    /// Consumo por volta convertido em consumo por distancia.
    /// </summary>
    public class EnergySystem
    {
        private readonly GameBalance bal;
        private readonly float trackLength;

        /// <summary>Disparado uma vez quando a energia cruza para baixo do limiar baixo (PRD 20 MVP).</summary>
        public event Action<MarbleRuntime> OnLowEnergyAlert;

        public EnergySystem(GameBalance balance, TrackDataSO track)
        {
            bal = balance;
            trackLength = Mathf.Max(1f, track.trackLength);
        }

        public void Apply(MarbleRuntime m, float distanceTraveled)
        {
            if (m.state == MarbleRaceState.InPit) return;

            // Variacao assinada por volta: Save regenera, Normal/Push drenam (PRD 4.2).
            float changePerLap = RaceFormulas.EnergyChangePerLap(m);
            float lapFraction = distanceTraveled / trackLength;
            float prev = m.energy;
            // Mesmo teto do Refill (bal.maxEnergy), evitando divergencia se o
            // balanceamento mudar o maximo de energia.
            m.energy = Mathf.Clamp(m.energy + changePerLap * lapFraction, 0f, bal.maxEnergy);

            if (prev >= 20f && m.energy < 20f)
                OnLowEnergyAlert?.Invoke(m);
        }

        /// <summary>Recarrega energia no pit, respeitando o maximo. Retorna quanto foi recarregado.</summary>
        public float Refill(MarbleRuntime m, float amount)
        {
            float before = m.energy;
            m.energy = Mathf.Clamp(m.energy + amount, 0f, bal.maxEnergy);
            return m.energy - before;
        }
    }
}
