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
        public static float FinalSpeed(MarbleRuntime m, GameBalance bal, Weather weather, float trackCond = 1f)
        {
            // Defesa: se algum dado-base nao foi atribuido (config invalida), evita
            // um NullReferenceException por frame e devolve uma velocidade neutra.
            if (bal == null) return 0f;
            if (m.driver == null || m.grip == null || m.surface == null)
                return bal.baseSpeed * m.upgSpeedFactor * m.aiSpeedMult;

            float baseSpeed = bal.baseSpeed;
            float driver = m.driver.SpeedMultiplier;
            float grip = m.grip.speedMultiplier * m.grip.PerformanceForWeather(weather);
            float surface = m.surface.speedModifier;
            float mode = ModeTuning.Get(m.mode).speed;
            float wear = WearSpeedPenalty(m.wear, bal);
            float energy = EnergySpeedPenalty(m.energy);
            float fuel = FuelSpeedPenalty(m.fuel);

            return baseSpeed * driver * grip * surface * mode * wear * energy * fuel * trackCond
                 * m.upgSpeedFactor * m.aiSpeedMult;
        }

        /// <summary>Penalidade multiplicativa de velocidade por desgaste (PRD 14 / 28).</summary>
        public static float WearSpeedPenalty(float wear, GameBalance bal)
        {
            if (wear <= bal.wearWarnThreshold) return 1f;
            // Interpola de 1.0 (no limiar de aviso) ate (1 - maxWearSpeedPenalty) em wear=100.
            float t = Mathf.InverseLerp(bal.wearWarnThreshold, 100f, wear);
            return 1f - t * bal.maxWearSpeedPenalty;
        }

        /// <summary>Penalidade de velocidade por energia BAIXA (bateria, PRD 4.2).</summary>
        public static float EnergySpeedPenalty(float energy)
        {
            if (energy > 60f) return 1f;
            if (energy >= 30f) return 0.98f;
            if (energy >= 10f) return 0.95f;
            return 0.90f;
        }

        /// <summary>Penalidade por combustivel: vazio => modo emergencia 35% (PRD 4.1).</summary>
        public static float FuelSpeedPenalty(float fuel) => fuel <= 0f ? 0.35f : 1f;

        /// <summary>Consumo de combustivel por volta (PRD 4.1 / 5.1).</summary>
        public static float FuelUsePerLap(MarbleRuntime m, float baseFuelPerLap)
        {
            float mode = ModeTuning.Get(m.mode).fuel;
            float tyre = m.grip.FuelMult;
            return baseFuelPerLap * mode * tyre;
        }

        /// <summary>Variacao de energia por volta (assinada: + regenera / - dreno) (PRD 4.2).</summary>
        public static float EnergyChangePerLap(MarbleRuntime m)
        {
            float delta = ModeTuning.Get(m.mode).energyDelta;
            if (delta < 0f) // dreno: pneu macio e baixa gestao gastam mais
            {
                float tyre = m.grip.EnergyMult;
                float driverMgmt = 1f / Mathf.Max(0.01f, m.driver.EnergyMgmtMultiplier);
                delta *= tyre * driverMgmt * m.upgEnergyFactor;
            }
            return delta;
        }

        /// <summary>Desgaste base (%/volta) escalado pela duracao da corrida (PRD 13).</summary>
        public static float BaseWearPerLap(int totalLaps)
            => totalLaps <= 6 ? 18f : (totalLaps <= 15 ? 8f : 5f);

        /// <summary>
        /// Ganho de desgaste por volta (PRD 13/14). Forte e diferenciado por pneu:
        /// baseWear(duracao) * pneu.WearMult * modo * superficie * abrasao * clima * piloto.
        /// </summary>
        public static float WearGainPerLap(MarbleRuntime m, GameBalance bal, float trackAbrasion,
            Weather weather, int totalLaps)
        {
            float baseWear = BaseWearPerLap(totalLaps);
            float tyre = m.grip.WearMult;
            float mode = ModeTuning.Get(m.mode).wear;
            float surface = m.surface.wearModifier;

            bool wet = weather == Weather.Damp || weather == Weather.LightRain || weather == Weather.HeavyRain;
            float weatherWear = (wet && m.grip.IsDryTyre) ? 1.2f
                              : (!wet && m.grip.gripId == GripType.Rain) ? 1.5f : 1f;

            // Bom tireManagement REDUZ desgaste => dividimos pelo multiplicador.
            float driverMgmt = 1f / Mathf.Max(0.01f, m.driver.TireMgmtMultiplier);
            return baseWear * tyre * mode * surface * trackAbrasion * weatherWear * driverMgmt * m.upgWearFactor;
        }

        /// <summary>Chance de erro por avaliacao (PRD 41 - ErrorChance), em 0..1.</summary>
        public static float ErrorChance(MarbleRuntime m, GameBalance bal, Weather weather, float trackErrorAdd = 0f)
        {
            float wearPenalty = 0f;
            if (m.wear > bal.wearCriticalThreshold) wearPenalty = 0.08f;
            else if (m.wear > bal.wearHeavyThreshold) wearPenalty = 0.04f;

            float aggressionPenalty = (m.driver.aggression / 100f) * 0.03f;

            bool wet = weather == Weather.Damp || weather == Weather.LightRain || weather == Weather.HeavyRain;
            float weatherPenalty = (weather == Weather.HeavyRain) ? 0.06f
                                 : (weather == Weather.LightRain) ? 0.03f
                                 : (weather == Weather.Damp) ? 0.015f : 0f;

            // Pneu seco (slick) em pista molhada aumenta muito o risco (PRD 19).
            bool slick = m.grip.gripId == GripType.Soft || m.grip.gripId == GripType.Medium || m.grip.gripId == GripType.Hard;
            float wrongTyrePenalty = (wet && slick) ? 0.06f : 0f;
            // Habilidade em pista molhada reduz erro no molhado (PRD 12 WetSkill).
            float wetSkillBonus = wet ? (m.driver.wetSkill / 100f) * 0.05f : 0f;

            float controlBonus = (m.driver.control / 100f) * 0.04f;
            float consistencyBonus = (m.driver.consistency / 100f) * 0.04f;

            float chance = bal.baseErrorChance + wearPenalty + aggressionPenalty
                         + weatherPenalty + wrongTyrePenalty + trackErrorAdd
                         - controlBonus - consistencyBonus - wetSkillBonus;

            // Multiplicador do modo (Save 0.8 / Normal 1.0 / Push 1.2) (PRD 5.2).
            chance *= ModeTuning.Get(m.mode).errorMult;

            // Veterano erra menos sob pressao; Rookie erra mais (PRD 12).
            if (m.driver.personality == Personality.Veteran) chance *= 0.7f;
            if (m.driver.personality == Personality.Rookie) chance *= 1.3f;
            if (m.driver.personality == Personality.RiskTaker) chance *= 1.2f;
            if (m.driver.personality == Personality.Smooth) chance *= 0.85f;

            chance *= m.upgErrorFactor; // upgrades StrategyCenter / AICoaching (PRD 30)
            chance *= m.aiErrorMult;    // dificuldade da IA (PRD 12)
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
