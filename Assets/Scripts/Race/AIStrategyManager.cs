using UnityEngine;
using MarbleGP.AI;
using MarbleGP.Core;
using MarbleGP.Data;
using MarbleGP.Systems;

namespace MarbleGP.Race
{
    /// <summary>
    /// Estrategia de pit das bolinhas da IA (PRD 6 / 11). Avalia combustivel,
    /// desgaste, energia, clima e voltas restantes e marca pitRequested quando
    /// necessario. Garante pelo menos 1 parada (o tanque nao chega ao fim).
    /// </summary>
    public class AIStrategyManager
    {
        private readonly GameDatabase _db;
        private readonly FuelSystem _fuel;
        private readonly IRaceConditions _cond;
        private readonly int _totalLaps;

        public AIStrategyManager(GameDatabase db, FuelSystem fuel, IRaceConditions cond, int totalLaps)
        {
            _db = db;
            _fuel = fuel;
            _cond = cond;
            _totalLaps = totalLaps;
        }

        public void Evaluate(MarbleController ctrl)
        {
            var m = ctrl.Runtime;
            if (m.isPlayer || m.pitRequested) return;
            if (m.state != MarbleRaceState.Racing) return;

            int lapsRemaining = _totalLaps - m.completedLaps;
            if (lapsRemaining <= 1) return; // nunca parar na ultima volta
            int lap = m.completedLaps + 1;

            Weather w = _cond.CurrentWeather;
            bool wet = IsWet(w);
            bool slick = m.grip.gripId == GripType.Soft || m.grip.gripId == GripType.Medium || m.grip.gripId == GripType.Hard;
            bool wetTyre = m.grip.gripId == GripType.Rain || m.grip.gripId == GripType.Intermediate;

            // --- 1) CRITICO: pit imediato, ignora janela/bloqueios (PRD 6) ---
            bool fuelCritical = m.fuel < Mathf.Max(18f, _fuel.FuelForLaps(m, 1.5f)) || m.fuel < 20f;
            bool heavyWrong = w == Weather.HeavyRain && slick;   // muito lento na chuva forte
            bool damage = m.coreFailTimer > 0f;
            if (fuelCritical || damage || heavyWrong) { RequestPit(m, w, lapsRemaining); return; }

            // --- 2) BLOQUEIO: nao para cedo demais (PRD 6) ---
            if (m.completedLaps < 1) return;          // nunca na volta 1 (exceto critico)

            // --- 3) MOTIVO real para parar ---
            // Hard (aiPitQuality alto) reage um pouco antes ao desgaste/clima.
            float wearThresh = m.aiPitQuality >= 1.2f ? 72f : (m.aiPitQuality <= 0.8f ? 82f : 75f);
            bool wrongTyre = (wet && slick) || (!wet && wetTyre);
            bool reason = m.fuel < 40f || m.wear > wearThresh || wrongTyre || (m.energy < 12f);
            if (!reason) return;

            // --- 4) Apenas dentro da janela estrategica ---
            if (!InPitWindow(lap)) return;

            RequestPit(m, w, lapsRemaining);
        }

        private void RequestPit(MarbleRuntime m, Weather w, int lapsRemaining)
        {
            GripType target = ChooseTyre(m, lapsRemaining, IsWet(w));
            if (_db.GetGrip(target) == null) target = m.grip.gripId;
            m.pitTargetGrip = target;
            m.pitChangeTires = true;
            m.pitRefillAmount = 100f;
            m.pitRequested = true;
        }

        /// <summary>Janela estrategica de pit por duracao (PRD 6).</summary>
        private bool InPitWindow(int lap)
        {
            int start = _totalLaps <= 6 ? 2 : (_totalLaps <= 15 ? 4 : 6);
            return lap >= start && lap <= _totalLaps - 1;
        }

        private GripType ChooseTyre(MarbleRuntime m, int lapsRemaining, bool wet)
        {
            if (_cond.CurrentWeather == Weather.HeavyRain) return GripType.Rain;
            if (wet) return GripType.Intermediate;

            GripType target = lapsRemaining <= 4 ? GripType.Soft
                            : lapsRemaining >= 10 ? GripType.Hard : GripType.Medium;

            switch (m.driver.personality)
            {
                case Personality.Aggressive: target = lapsRemaining <= 6 ? GripType.Soft : GripType.Medium; break;
                case Personality.Conservative: target = lapsRemaining >= 8 ? GripType.Hard : GripType.Medium; break;
            }
            return target;
        }

        private static float WearThreshold(Personality p)
        {
            switch (p)
            {
                case Personality.Aggressive: return 68f;
                case Personality.RiskTaker: return 86f;
                case Personality.Conservative: return 82f;
                case Personality.Veteran: return 76f;
                default: return 75f;
            }
        }

        private static bool IsWet(Weather w)
            => w == Weather.Damp || w == Weather.LightRain || w == Weather.HeavyRain;
    }
}
