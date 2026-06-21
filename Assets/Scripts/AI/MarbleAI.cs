using System.Collections.Generic;
using UnityEngine;
using MarbleGP.Core;
using MarbleGP.Systems;
using MarbleGP.Track;

namespace MarbleGP.AI
{
    /// <summary>
    /// "Cerebro" da bolinha (PRD 13). Decide velocidade desejada, linha de
    /// corrida, tentativas de ultrapassagem (com compromisso de lado para a
    /// disputa acontecer de verdade na pista), defesa, variacao de ritmo e
    /// erros, conforme atributos e personalidade. Apenas alimenta o controller.
    /// </summary>
    public class MarbleAI
    {
        private readonly MarbleController _ctrl;
        private readonly MarbleRuntime _m;
        private readonly TrackManager _track;
        private readonly GameBalance _bal;
        private readonly IRaceConditions _cond;
        private readonly Rigidbody _rb;

        private float _errorRecoverTimer;

        // Estado de ataque (compromisso de linha por alguns segundos).
        private RacingLine _attackSide = RacingLine.Ideal;
        private float _attackTimer;
        // Semente para variacao de ritmo (ruido suave por bolinha).
        private readonly float _paceSeed;

        public MarbleAI(MarbleController ctrl, TrackManager track, GameBalance bal, IRaceConditions cond)
        {
            _ctrl = ctrl;
            _m = ctrl.Runtime;
            _track = track;
            _bal = bal;
            _cond = cond;
            _rb = ctrl.GetComponent<Rigidbody>();
            _paceSeed = Random.value * 100f;
        }

        public void Think(List<MarbleController> field, float dt)
        {
            if (_m.state == MarbleRaceState.Finished || _m.state == MarbleRaceState.Retired)
            {
                _ctrl.DesiredSpeed = 0f;
                return;
            }

            Weather weather = _cond.CurrentWeather;

            // 1) Velocidade-base teorica (PRD 41) com variacao de ritmo por volta (PRD 2.8).
            float maxSpeed = RaceFormulas.FinalSpeed(_m, _bal, weather, _cond.TrackSpeedMod) * PaceNoise();

            // 2) Aceleracao/frenagem por trecho: freia mais em curva fechada,
            //    acelera nas retas (PRD 2.3). Floor menor = variacao mais visivel.
            float curvature = _track.CurvatureAhead(_ctrl.transform.position, 2);
            float controlFactor = _m.driver.ControlMultiplier * _m.grip.gripMultiplier
                                * _m.surface.controlModifier * _m.upgControlFactor;
            // RiskTaker freia menos (entra mais rapido); Smooth/Conservative mais seguro.
            float bravery = Bravery();
            float corner = Mathf.Lerp(maxSpeed, maxSpeed * 0.34f,
                Mathf.Clamp01(curvature / Mathf.Max(0.5f, controlFactor) * (2f - bravery)));

            // 3) Disputa com compromisso de lado (PRD 2.2).
            RacingLine line = RacingLine.Ideal;
            MarbleController ahead = FindAhead(field);
            if (_attackTimer > 0f)
            {
                _attackTimer -= dt;
                line = _attackSide;
                corner *= 1.05f; // empurra para concluir a ultrapassagem
            }
            else if (ahead != null)
            {
                line = DecideOvertakeOrFollow(ahead, ref corner);
            }
            else
            {
                line = DefendIfThreatened(field, RacingLine.Ideal);
            }

            // 4) Reacao a desgaste/energia (PRD 13.4).
            if (_m.wear > _bal.wearCriticalThreshold) corner *= 0.9f;
            if (_m.energy < _bal.lowChargeThreshold) corner *= 0.95f;
            if (_m.mode == RaceMode.Save) corner *= 0.99f;

            // 5) Erros ocasionais (PRD 13.6 / 2.5).
            if (_errorRecoverTimer > 0f)
            {
                _errorRecoverTimer -= dt;
                corner *= 0.65f; // perdeu tempo no erro
            }
            else if (Random.value < RaceFormulas.ErrorChance(_m, _bal, weather, _cond.TrackErrorAdd) * dt)
            {
                _errorRecoverTimer = Random.Range(0.3f, 1.0f);
                line = RacingLine.Outside; // abriu a curva
            }

            _ctrl.DesiredSpeed = Mathf.Max(0f, corner);
            _ctrl.Line = line;
        }

        // ---- Ritmo / personalidade ---------------------------------------

        /// <summary>Variacao suave de ritmo; menos consistente => oscila mais (PRD 2.8).</summary>
        private float PaceNoise()
        {
            float amp = (1f - _m.driver.consistency / 100f) * 0.07f;
            if (_m.driver.personality == Personality.Rookie) amp += 0.03f;
            if (_m.driver.personality == Personality.Veteran) amp *= 0.6f;
            float noise = Mathf.PerlinNoise(Time.time * 0.35f + _paceSeed, _paceSeed) - 0.5f;
            return 1f + noise * 2f * amp;
        }

        /// <summary>0..~1.4: coragem na curva (RiskTaker entra mais rapido).</summary>
        private float Bravery()
        {
            switch (_m.driver.personality)
            {
                case Personality.RiskTaker: return 1.25f;
                case Personality.Aggressive: return 1.12f;
                case Personality.Smooth: return 1.05f;
                case Personality.Conservative: return 0.85f;
                case Personality.Rookie: return 0.9f;
                default: return 1f;
            }
        }

        // ---- Disputa -----------------------------------------------------

        private MarbleController FindAhead(List<MarbleController> field)
        {
            Vector3 pos = _ctrl.transform.position;
            Vector3 fwd = HorizVel(_rb).normalized;
            MarbleController best = null;
            float bestDist = _bal.overtakeDetectDistance;
            foreach (var other in field)
            {
                if (other == _ctrl) continue;
                if (other.Runtime.state != MarbleRaceState.Racing) continue;
                Vector3 to = other.transform.position - pos; to.y = 0f;
                float d = to.magnitude;
                if (d < bestDist && Vector3.Dot(fwd, to.normalized) > 0.6f) { best = other; bestDist = d; }
            }
            return best;
        }

        private RacingLine DecideOvertakeOrFollow(MarbleController ahead, ref float speed)
        {
            bool faster = _ctrl.DesiredSpeed >= ahead.CurrentSpeed * 0.97f;
            bool hasEnergy = _m.energy > _bal.lowChargeThreshold;
            bool freshTyre = _m.wear < 70f;

            float willAttack = _m.driver.aggression / 100f;
            switch (_m.driver.personality)
            {
                case Personality.Aggressive: willAttack += 0.2f; break;
                case Personality.RiskTaker: willAttack += 0.28f; break;
                case Personality.Conservative: willAttack -= 0.2f; break;
                case Personality.Defensive: willAttack -= 0.12f; break;
                case Personality.Veteran: willAttack += 0.05f; break;
            }
            if (_m.mode == RaceMode.Push || _m.mode == RaceMode.Attack) willAttack += 0.18f;
            if (_m.mode == RaceMode.Save) willAttack -= 0.15f;

            // Curva fechada nao e bom lugar para atacar.
            float curvature = _track.CurvatureAhead(_ctrl.transform.position, 2);
            bool goodZone = curvature < 0.25f;

            if (faster && hasEnergy && freshTyre && goodZone && Random.value < willAttack * 0.6f)
            {
                // Compromete um lado por alguns segundos (movimento lateral real).
                _attackSide = ChooseAttackSide();
                _attackTimer = Random.Range(1.5f, 2.6f);
                speed *= 1.05f;
                return _attackSide;
            }

            // Sem ataque: segue atras, reduzindo um pouco para nao bater.
            speed = Mathf.Min(speed, ahead.CurrentSpeed * 0.99f);
            return RacingLine.Ideal;
        }

        private RacingLine ChooseAttackSide()
        {
            // Agressivos preferem o lado interno; cautelosos, o externo.
            switch (_m.driver.personality)
            {
                case Personality.Aggressive:
                case Personality.RiskTaker:
                    return Random.value < 0.7f ? RacingLine.Inside : RacingLine.Outside;
                case Personality.Conservative:
                case Personality.Smooth:
                    return Random.value < 0.6f ? RacingLine.Outside : RacingLine.Inside;
                default:
                    return Random.value < 0.5f ? RacingLine.Inside : RacingLine.Outside;
            }
        }

        /// <summary>Defensivos/veteranos bloqueiam a linha interna quando ameacados (PRD 2.4).</summary>
        private RacingLine DefendIfThreatened(List<MarbleController> field, RacingLine fallback)
        {
            if (_m.driver.personality != Personality.Defensive &&
                _m.driver.personality != Personality.Veteran) return fallback;

            Vector3 pos = _ctrl.transform.position;
            Vector3 back = -HorizVel(_rb).normalized;
            foreach (var other in field)
            {
                if (other == _ctrl || other.Runtime.state != MarbleRaceState.Racing) continue;
                Vector3 to = other.transform.position - pos; to.y = 0f;
                if (to.magnitude < _bal.overtakeDetectDistance && Vector3.Dot(back, to.normalized) > 0.6f)
                    return RacingLine.Inside; // fecha a porta
            }
            return fallback;
        }

        private static Vector3 HorizVel(Rigidbody rb)
            => rb != null ? new Vector3(rb.velocity.x, 0f, rb.velocity.z) : Vector3.forward;
    }
}
