using System;
using UnityEngine;
using MarbleGP.Core;
using MarbleGP.Data;

namespace MarbleGP.Systems
{
    /// <summary>
    /// Calcula e aplica desgaste dos aneis de aderencia (PRD 14 / 24.2).
    /// O desgaste por volta do PRD e convertido em desgaste por distancia
    /// percorrida, dando degradacao suave acoplada a velocidade real.
    /// </summary>
    public class TireWearSystem
    {
        private readonly GameBalance bal;
        private readonly float trackLength;
        private readonly float trackAbrasion;
        private readonly int totalLaps;

        /// <summary>Disparado uma vez quando o desgaste cruza o limiar de aviso (PRD 20 MVP).</summary>
        public event Action<MarbleRuntime> OnHighWearAlert;

        public TireWearSystem(GameBalance balance, TrackDataSO track, int totalLaps)
        {
            bal = balance;
            trackLength = Mathf.Max(1f, track.trackLength);
            trackAbrasion = track.abrasionLevel;
            this.totalLaps = Mathf.Max(1, totalLaps);
        }

        /// <summary>Aplica desgaste para a distancia percorrida neste frame (clima vivo).</summary>
        public void Apply(MarbleRuntime m, float distanceTraveled, Weather weather)
        {
            if (m.state == MarbleRaceState.InPit) return;

            float wearPerLap = RaceFormulas.WearGainPerLap(m, bal, trackAbrasion, weather, totalLaps);
            float lapFraction = distanceTraveled / trackLength;
            float prev = m.wear;
            m.wear = Mathf.Clamp(m.wear + wearPerLap * lapFraction, 0f, 100f);

            if (prev < bal.wearHeavyThreshold && m.wear >= bal.wearHeavyThreshold)
                OnHighWearAlert?.Invoke(m);
        }

        /// <summary>Reseta o anel (troca no pit).</summary>
        public void FitNewGrip(MarbleRuntime m, GripRingSO newGrip)
        {
            m.grip = newGrip;
            m.wear = 0f;
        }
    }
}
