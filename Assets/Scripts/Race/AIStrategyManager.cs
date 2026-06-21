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
            if (lapsRemaining <= 1) return; // sem sentido parar na ultima volta

            bool wet = IsWet(_cond.CurrentWeather);
            bool slick = m.grip.gripId == GripType.Soft || m.grip.gripId == GripType.Medium || m.grip.gripId == GripType.Hard;
            bool wetTyre = m.grip.gripId == GripType.Rain || m.grip.gripId == GripType.Intermediate;

            bool needPit = false;

            // 1) Combustivel insuficiente para terminar (margem) ou < 1.5 volta.
            if (m.fuel < _fuel.FuelForLaps(m, lapsRemaining) * 1.05f) needPit = true;
            if (m.fuel < _fuel.FuelForLaps(m, 1.5f)) needPit = true;

            // 2) Desgaste alto (limiar por personalidade).
            float wearThresh = WearThreshold(m.driver.personality);
            if (m.wear > wearThresh) needPit = true;

            // 3) Pneu errado para o clima (PRD 11).
            if (wet && slick && lapsRemaining > 2) needPit = true;
            if (!wet && wetTyre && lapsRemaining > 2) needPit = true;

            // 4) Energia muito baixa e ainda longe do fim.
            if (m.energy < 12f && lapsRemaining > 3) needPit = true;

            if (!needPit) return;

            // Escolhe pneu alvo conforme clima e voltas restantes.
            GripType target = ChooseTyre(m, lapsRemaining, wet);
            if (_db.GetGrip(target) == null) target = m.grip.gripId;

            m.pitTargetGrip = target;
            m.pitChangeTires = true;
            m.pitRefillAmount = 100f;
            m.pitRequested = true;
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
