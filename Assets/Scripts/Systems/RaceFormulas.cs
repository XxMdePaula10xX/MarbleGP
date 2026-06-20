using UnityEngine;
using MarbleGP.Core;
using MarbleGP.Data;

namespace MarbleGP.Systems
{
    /// <summary>
    /// Implementacao central das formulas do PRD secao 41.
    /// Mantida estatica e pura para facilitar teste e balanceamento.
    /// </summary>
    public static class RaceFormulas
    {
        /// <summary>
        /// Velocidade alvo final de uma bolinha (PRD 41 - FinalSpeed).
        /// FinalSpeed = BaseSpeed * DriverSpeed * Grip * Surface * Mode * Wear * Energy * TrackCond
        /// </summary>
        public static float FinalSpeed(MarbleRuntime m, GameBalance bal, Weather weather)
        {
            float baseSpeed = bal.baseSpeed;
            float driver = m.driver.SpeedMultiplier;
            float grip = m.grip.speedMultiplier * m.grip.PerformanceForWeather(weather);
            float surface = m.surface.speedModifier;
            float mode = bal.GetMode(m.mode).speed;
            float wear = WearSpeedPenalty(m.wear, bal);
            float energy = EnergySpeedPenalty(m.energy, bal);
            float trackCond = 1f; // MVP: seco/neutro

            return baseSpeed * driver * grip * surface * mode * wear * energy * trackCond * m.upgSpeedFactor;
        }

        /// <summary>Penalidade multiplicativa de velocidade por desgaste (PRD 14 / 28).</summary>
        public static float WearSpeedPenalty(float wear, GameBalance bal)
        {
            if (wear <= bal.wearWarnThreshold) return 1f;
            // Interpola de 1.0 (no limiar de aviso) ate (1 - maxWearSpeedPenalty) em wear=100.
            float t = Mathf.InverseLerp(bal.wearWarnThreshold, 100f, wear);
            return 1f - t * bal.maxWearSpeedPenalty;
        }

        /// <summary>Penalidade de velocidade por carga de energia (PRD 28).</summary>
        public static float EnergySpeedPenalty(float energy, GameBalance bal)
        {
            if (energy > bal.highChargeThreshold) return 1f - bal.penaltyHighCharge;   // -4%
            if (energy >= bal.midChargeThreshold) return 1f - bal.penaltyMidCharge;    // -2%
            if (energy < bal.lowChargeThreshold) return 1f - bal.penaltyLowCharge;     // -6%
            return 1f; // zona 20-50: sem penalidade
        }

        /// <summary>Ganho de desgaste por volta (PRD 41 - WearGain).</summary>
        public static float WearGainPerLap(MarbleRuntime m, GameBalance bal, float trackAbrasion, Weather weather)
        {
            float baseWear = m.grip.WearForWeather(weather);
            float mode = bal.GetMode(m.mode).wear;
            float surface = m.surface.wearModifier;
            // Bom tireManagement REDUZ desgaste => dividimos pelo multiplicador.
            float driverMgmt = 1f / Mathf.Max(0.01f, m.driver.TireMgmtMultiplier);
            return baseWear * mode * surface * trackAbrasion * driverMgmt * m.upgWearFactor;
        }

        /// <summary>Consumo de energia por volta (PRD 41 - EnergyConsumption).</summary>
        public static float EnergyConsumptionPerLap(MarbleRuntime m, GameBalance bal)
        {
            float baseConsumption;
            switch (m.mode)
            {
                case RaceMode.Push: baseConsumption = bal.energyConsumptionPush; break;
                case RaceMode.Save: baseConsumption = bal.energyConsumptionSave; break;
                default: baseConsumption = bal.energyConsumptionNormal; break;
            }
            float surface = m.surface.energyModifier;
            float driverMgmt = 1f / Mathf.Max(0.01f, m.driver.EnergyMgmtMultiplier);
            return baseConsumption * surface * driverMgmt * m.upgEnergyFactor;
        }

        /// <summary>Chance de erro por avaliacao (PRD 41 - ErrorChance), em 0..1.</summary>
        public static float ErrorChance(MarbleRuntime m, GameBalance bal, Weather weather)
        {
            float wearPenalty = 0f;
            if (m.wear > bal.wearCriticalThreshold) wearPenalty = 0.08f;
            else if (m.wear > bal.wearHeavyThreshold) wearPenalty = 0.04f;

            float aggressionPenalty = (m.driver.aggression / 100f) * 0.03f;
            float modeMod = bal.GetMode(m.mode).errorMod;
            float weatherPenalty = (weather == Weather.HeavyRain) ? 0.06f
                                 : (weather == Weather.LightRain) ? 0.03f : 0f;

            float controlBonus = (m.driver.control / 100f) * 0.04f;
            float consistencyBonus = (m.driver.consistency / 100f) * 0.04f;

            float chance = bal.baseErrorChance + wearPenalty + aggressionPenalty
                         + modeMod * 0.1f + weatherPenalty - controlBonus - consistencyBonus;

            // Veterano erra menos sob pressao; Rookie erra mais (PRD 12).
            if (m.driver.personality == Personality.Veteran) chance *= 0.7f;
            if (m.driver.personality == Personality.Rookie) chance *= 1.3f;
            if (m.driver.personality == Personality.RiskTaker) chance *= 1.2f;
            if (m.driver.personality == Personality.Smooth) chance *= 0.85f;

            chance *= m.upgErrorFactor; // upgrades StrategyCenter / AICoaching (PRD 30)
            return Mathf.Clamp01(chance);
        }

        /// <summary>Tempo total de pit em segundos (PRD 18.3 / 41).</summary>
        public static float PitTime(MarbleRuntime m, GameBalance bal, bool changeTires, float energyRefilled)
        {
            float tireTime = changeTires ? bal.tireChangeTime : 0f;
            float refillTime = energyRefilled * bal.energyRefillTimePerUnit;
            float teamBonus = m.team != null ? (m.team.pitCrewRating / 100f) * 1.0f : 0f;
            float driverBonus = (m.driver.pitSkill / 100f) * 0.5f;
            float randomError = Random.Range(0f, bal.maxRandomPitError);

            float time = bal.basePitTime + tireTime + refillTime + randomError
                       - teamBonus - driverBonus - m.upgPitReduction; // PitCrew (PRD 30)
            return Mathf.Max(1.0f, time);
        }
    }
}
