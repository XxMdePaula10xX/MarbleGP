using UnityEngine;
using MarbleGP.Core;
using MarbleGP.Systems;
using MarbleGP.Track;

namespace MarbleGP.AI
{
    /// <summary>
    /// Movimento fisico GUIADO da bolinha (PRD 27). Usa Rigidbody numa esfera,
    /// mas a direcao e velocidade sao controladas pela IA via waypoints, com
    /// separacao suave entre bolinhas para evitar caos (PRD 13.2: nao usar
    /// fisica 100% livre). Prioriza estabilidade (PRD 27.2 MVP).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MarbleController : MonoBehaviour
    {
        public MarbleRuntime Runtime { get; private set; }
        public Transform Visual { get; private set; }

        // Definidos pela IA a cada passo:
        public float DesiredSpeed { get; set; }
        public RacingLine Line { get; set; } = RacingLine.Ideal;
        public Vector3 ExternalTarget { get; set; } // usado em pit (override do alvo)
        public bool UseExternalTarget { get; set; }

        public float CurrentSpeed => new Vector3(_rb.velocity.x, 0f, _rb.velocity.z).magnitude;
        public float DistanceLastStep { get; private set; }

        /// <summary>Disparado num contato relevante entre bolinhas (this, other, impacto).</summary>
        public event System.Action<MarbleController, MarbleController, float> Contact;

        private Rigidbody _rb;
        private TrackManager _track;
        private GameBalance _bal;
        private float _radius = 0.5f;
        private float _stuckTimer;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.FreezeRotation; // rolagem e visual
        }

        public void Configure(MarbleRuntime runtime, TrackManager track, GameBalance bal, Transform visual, float radius)
        {
            Runtime = runtime;
            _track = track;
            _bal = bal;
            Visual = visual;
            _radius = radius;
        }

        /// <summary>Passo de fisica chamado pelo RaceManager (FixedUpdate centralizado).</summary>
        public void PhysicsStep(float dt)
        {
            Vector3 pos = transform.position;

            // 1) Alvo de direcao.
            Vector3 target = UseExternalTarget
                ? ExternalTarget
                : _track.GetSteerTarget(Line, pos, Mathf.RoundToInt(_bal.cornerLookAhead));

            Vector3 dir = target - pos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) dir.Normalize();

            // 2) Separacao suave de bolinhas proximas (steering, PRD 13.4).
            Vector3 separation = ComputeSeparation(pos);
            Vector3 steer = (dir + separation * 0.6f).normalized;

            // 3) Velocidade alvo -> velocidade horizontal (preserva gravidade em Y).
            Vector3 desiredVel = steer * DesiredSpeed;
            Vector3 horizVel = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z);
            float accel = _bal.baseSpeed * 3f; // resposta de aceleracao
            Vector3 newHoriz = Vector3.MoveTowards(horizVel, desiredVel, accel * dt);
            _rb.velocity = new Vector3(newHoriz.x, _rb.velocity.y, newHoriz.z);

            // 4) Rolagem visual proporcional a velocidade.
            RollVisual(newHoriz, dt);

            DistanceLastStep = newHoriz.magnitude * dt;
        }

        private Vector3 ComputeSeparation(Vector3 pos)
        {
            Vector3 sum = Vector3.zero;
            var hits = Physics.OverlapSphere(pos, _radius * 3f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.attachedRigidbody == _rb) continue;
                if (h.GetComponent<MarbleController>() == null) continue;
                Vector3 away = pos - h.transform.position;
                away.y = 0f;
                float d = away.magnitude;
                if (d > 0.01f && d < _radius * 3f)
                    sum += away.normalized * (1f - d / (_radius * 3f));
            }
            return sum;
        }

        private void RollVisual(Vector3 horizVel, float dt)
        {
            if (Visual == null) return;
            float speed = horizVel.magnitude;
            if (speed < 0.01f) return;
            Vector3 axis = Vector3.Cross(Vector3.up, horizVel.normalized);
            float angle = (speed * dt / Mathf.Max(0.1f, _radius)) * Mathf.Rad2Deg;
            Visual.Rotate(axis, angle, Space.World);
        }

        /// <summary>Deteccao de "presa" e recuperacao (PRD 13.6 - nunca presa para sempre).</summary>
        /// <returns>true se foi reposicionada neste passo.</returns>
        public bool HandleStuckRecovery(float dt)
        {
            if (Runtime.state == MarbleRaceState.InPit || Runtime.state == MarbleRaceState.Finished)
                return false;

            if (CurrentSpeed < _bal.stuckSpeedThreshold)
            {
                _stuckTimer += dt;
                if (_stuckTimer >= _bal.stuckRecoveryTime)
                {
                    // Reposiciona no waypoint mais proximo da linha ideal.
                    _track.IdealLine.ClosestArcFraction(transform.position, out int near);
                    Vector3 wp = _track.IdealLine.Point(near + 1);
                    transform.position = new Vector3(wp.x, transform.position.y, wp.z);
                    _rb.velocity = Vector3.zero;
                    _stuckTimer = 0f;
                    Runtime.totalTime += 1.5f; // pequena penalidade (PRD 13.6)
                    return true;
                }
            }
            else _stuckTimer = 0f;
            return false;
        }

        public void Freeze()
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var other = collision.collider.GetComponentInParent<MarbleController>();
            if (other == null || other == this) return;
            float impact = collision.relativeVelocity.magnitude;
            if (impact < 2.0f) return; // ignora toques irrelevantes
            Contact?.Invoke(this, other, impact);
        }
    }
}
