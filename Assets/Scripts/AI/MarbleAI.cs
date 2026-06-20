using System.Collections.Generic;
using UnityEngine;
using MarbleGP.Core;
using MarbleGP.Systems;
using MarbleGP.Track;

namespace MarbleGP.AI
{
    /// <summary>
    /// "Cerebro" da bolinha (PRD 13). Decide velocidade desejada, linha de
    /// corrida, tentativas de ultrapassagem, defesa e erros, com base nos
    /// atributos e personalidade do piloto. Nao controla fisica diretamente:
    /// apenas alimenta o MarbleController.
    /// </summary>
    public class MarbleAI
    {
        private readonly MarbleController _ctrl;
        private readonly MarbleRuntime _m;
        private readonly TrackManager _track;
        private readonly GameBalance _bal;
        private readonly Weather _weather;

        private float _errorRecoverTimer;

        public MarbleAI(MarbleController ctrl, TrackManager track, GameBalance bal, Weather weather)
        {
            _ctrl = ctrl;
            _m = ctrl.Runtime;
            _track = track;
            _bal = bal;
            _weather = weather;
        }

        /// <summary>Decisao de IA por passo (PRD 13.4). neighbors = todos os controllers.</summary>
        public void Think(List<MarbleController> field, float dt)
        {
            if (_m.state == MarbleRaceState.Finished || _m.state == MarbleRaceState.Retired)
            {
                _ctrl.DesiredSpeed = 0f;
                return;
            }

            // 1) Velocidade-base teorica (formulas do PRD 41).
            float maxSpeed = RaceFormulas.FinalSpeed(_m, _bal, _weather);

            // 2) Ajuste de velocidade antes de curvas (PRD 13.4).
            float curvature = _track.CurvatureAhead(_ctrl.transform.position, 2);
            float controlFactor = _m.driver.ControlMultiplier * _m.grip.gripMultiplier * _m.surface.controlModifier;
            float cornerSpeed = Mathf.Lerp(maxSpeed, maxSpeed * 0.45f, curvature / Mathf.Max(0.5f, controlFactor));

            // 3) Disputa: detectar bolinha a frente e decidir linha (PRD 13.5).
            RacingLine line = RacingLine.Ideal;
            MarbleController ahead = FindAhead(field);
            if (ahead != null)
            {
                line = DecideOvertakeOrDefend(ahead, ref cornerSpeed);
            }

            // 4) Reacao a desgaste/energia (PRD 13.4): suavizar se critico.
            if (_m.wear > _bal.wearCriticalThreshold) cornerSpeed *= 0.9f;
            if (_m.energy < _bal.lowChargeThreshold) cornerSpeed *= 0.95f;

            // 5) Erros ocasionais conforme atributos/personalidade (PRD 13.6).
            if (_errorRecoverTimer > 0f)
            {
                _errorRecoverTimer -= dt;
                cornerSpeed *= 0.7f; // perdendo tempo apos erro
            }
            else if (Random.value < RaceFormulas.ErrorChance(_m, _bal, _weather) * dt)
            {
                _errorRecoverTimer = Random.Range(0.3f, 0.9f); // abre a curva / perde velocidade
                line = RacingLine.Outside;
            }

            _ctrl.DesiredSpeed = Mathf.Max(0f, cornerSpeed);
            _ctrl.Line = line;
        }

        // ---- Disputa -----------------------------------------------------

        private MarbleController FindAhead(List<MarbleController> field)
        {
            Vector3 pos = _ctrl.transform.position;
            Vector3 fwd = new Vector3(_ctrl.GetComponent<Rigidbody>().velocity.x, 0f,
                                      _ctrl.GetComponent<Rigidbody>().velocity.z).normalized;
            MarbleController best = null;
            float bestDist = _bal.overtakeDetectDistance;
            foreach (var other in field)
            {
                if (other == _ctrl) continue;
                if (other.Runtime.state == MarbleRaceState.Finished) continue;
                Vector3 to = other.transform.position - pos;
                to.y = 0f;
                float d = to.magnitude;
                if (d < bestDist && Vector3.Dot(fwd, to.normalized) > 0.6f) // a frente
                {
                    best = other;
                    bestDist = d;
                }
            }
            return best;
        }

        private RacingLine DecideOvertakeOrDefend(MarbleController ahead, ref float speed)
        {
            // Condicoes de ultrapassagem (PRD 13.5).
            bool fasterPotential = _ctrl.DesiredSpeed >= ahead.CurrentSpeed * 0.98f;
            float aggression = _m.driver.aggression / 100f;
            bool hasEnergy = _m.energy > _bal.lowChargeThreshold;

            // Personalidade modula a vontade de atacar (PRD 12).
            float willAttack = aggression;
            switch (_m.driver.personality)
            {
                case Personality.Aggressive: willAttack += 0.2f; break;
                case Personality.RiskTaker: willAttack += 0.25f; break;
                case Personality.Conservative: willAttack -= 0.2f; break;
                case Personality.Defensive: willAttack -= 0.1f; break;
            }
            if (_m.mode == RaceMode.Push || _m.mode == RaceMode.Attack) willAttack += 0.15f;

            if (fasterPotential && hasEnergy && Random.value < willAttack)
            {
                _m.overtakes += 0; // contabilizado de fato ao trocar posicao (RacePositionSystem)
                // Escolhe lado interno para atacar.
                return Random.value < 0.5f ? RacingLine.Inside : RacingLine.Outside;
            }

            // Sem ataque: segue atras, reduzindo um pouco para nao colidir.
            speed = Mathf.Min(speed, ahead.CurrentSpeed * 0.99f);
            return RacingLine.Ideal;
        }
    }
}
