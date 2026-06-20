using UnityEngine;
using MarbleGP.Core;
using MarbleGP.Systems;
using MarbleGP.Track;

namespace MarbleGP.Race
{
    /// <summary>
    /// Camada visual de cada bolinha (PRD 3/10/13): sombra, etiqueta com a sigla
    /// (billboard), rastro de velocidade, brilho do modo (Push/Save), overlay de
    /// pit com barra de progresso, estados de desgaste/energia e destaque do
    /// vencedor. Lê o MarbleRuntime e nao interfere na fisica/IA.
    /// </summary>
    public class MarbleVisual : MonoBehaviour
    {
        private MarbleRuntime _runtime;
        private float _radius;
        private Transform _body;
        private Rigidbody _rb;
        private Camera _cam;

        private Transform _shadow;
        private TextMesh _label;
        private TrailRenderer _trail;
        private Light _glow;

        private GameObject _pitOverlay;
        private TextMesh _pitLabel;
        private Transform _pitBarFill;
        private float _pitBarWidth = 1.4f;

        private Vector3 _bodyBaseScale;
        private bool _isWinner;
        private Color _teamColor;

        public void Configure(MarbleRuntime runtime, float radius, Transform body, Color teamColor)
        {
            _runtime = runtime;
            _radius = radius;
            _body = body;
            _teamColor = teamColor;
            _rb = GetComponent<Rigidbody>();
            _bodyBaseScale = body != null ? body.localScale : Vector3.one;

            BuildShadow();
            BuildLabel();
            BuildTrail();
            BuildGlow();
            BuildPitOverlay();
        }

        private void Start() => _cam = Camera.main;

        // ---- Construcao --------------------------------------------------

        private void BuildShadow()
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Shadow";
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(transform, false);
            s.transform.localScale = new Vector3(_radius * 2.3f, 0.02f, _radius * 2.3f);
            s.transform.localPosition = new Vector3(0f, -_radius + 0.04f, 0f);
            s.GetComponent<MeshRenderer>().sharedMaterial =
                MaterialFactory.CreateUnlit(new Color(0f, 0f, 0f, 1f));
            _shadow = s.transform;
        }

        private void BuildLabel()
        {
            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, _radius * 1.7f, 0f);
            _label = go.AddComponent<TextMesh>();
            MaterialFactory.ApplyFont(_label);
            _label.text = _runtime.driver != null ? _runtime.driver.shortCode : "MAR";
            _label.characterSize = 0.12f;
            _label.fontSize = 80;
            _label.fontStyle = FontStyle.Bold;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = Color.white;
            go.transform.localScale = Vector3.one * (_radius * 1.4f);
        }

        private void BuildTrail()
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            _trail = go.AddComponent<TrailRenderer>();
            _trail.time = 0.35f;
            _trail.startWidth = _radius * 0.8f;
            _trail.endWidth = 0f;
            _trail.minVertexDistance = 0.15f;
            _trail.numCapVertices = 4;
            _trail.material = MaterialFactory.CreateUnlit(_teamColor);
            _trail.emitting = false;
        }

        private void BuildGlow()
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * _radius;
            _glow = go.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.range = _radius * 6f;
            _glow.intensity = 0f;
            _glow.enabled = false;
        }

        private void BuildPitOverlay()
        {
            _pitOverlay = new GameObject("PitOverlay");
            _pitOverlay.transform.SetParent(transform, false);
            _pitOverlay.transform.localPosition = new Vector3(0f, _radius * 2.6f, 0f);

            var labelGo = new GameObject("PitText");
            labelGo.transform.SetParent(_pitOverlay.transform, false);
            _pitLabel = labelGo.AddComponent<TextMesh>();
            MaterialFactory.ApplyFont(_pitLabel);
            _pitLabel.text = "PIT";
            _pitLabel.characterSize = 0.12f;
            _pitLabel.fontSize = 70;
            _pitLabel.fontStyle = FontStyle.Bold;
            _pitLabel.anchor = TextAnchor.MiddleCenter;
            _pitLabel.color = new Color(1f, 0.6f, 0.1f);
            labelGo.transform.localScale = Vector3.one * (_radius * 1.3f);
            labelGo.transform.localPosition = Vector3.up * 0.6f;

            var barBg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barBg.name = "BarBg";
            Destroy(barBg.GetComponent<Collider>());
            barBg.transform.SetParent(_pitOverlay.transform, false);
            barBg.transform.localScale = new Vector3(_pitBarWidth, 0.06f, 0.25f);
            barBg.GetComponent<MeshRenderer>().sharedMaterial =
                MaterialFactory.CreateUnlit(new Color(0.1f, 0.1f, 0.1f));

            var barFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barFill.name = "BarFill";
            Destroy(barFill.GetComponent<Collider>());
            barFill.transform.SetParent(_pitOverlay.transform, false);
            barFill.transform.localScale = new Vector3(0.01f, 0.08f, 0.3f);
            barFill.GetComponent<MeshRenderer>().sharedMaterial =
                MaterialFactory.CreateUnlit(new Color(0.3f, 0.9f, 0.4f));
            _pitBarFill = barFill.transform;

            _pitOverlay.SetActive(false);
        }

        // ---- Update ------------------------------------------------------

        private void Update()
        {
            if (_runtime == null) return;
            if (_cam == null) _cam = Camera.main;

            Billboard(_label != null ? _label.transform : null);
            UpdateLabel();
            UpdateTrailAndGlow();
            UpdatePitOverlay();
        }

        private void Billboard(Transform t)
        {
            if (t == null || _cam == null) return;
            t.rotation = _cam.transform.rotation;
        }

        private void UpdateLabel()
        {
            if (_label == null) return;
            string code = _runtime.driver != null ? _runtime.driver.shortCode : "MAR";
            string suffix = "";
            if (_runtime.mode == RaceMode.Push) suffix = " »";
            else if (_runtime.mode == RaceMode.Save) suffix = " ~";

            Color c = Color.white;
            if (_isWinner) { c = new Color(1f, 0.85f, 0.2f); code = "★ " + code; }
            else if (_runtime.wear > 70f) c = new Color(1f, 0.35f, 0.35f);
            else if (_runtime.energy < 20f) c = new Color(1f, 0.9f, 0.3f);

            _label.text = code + suffix;
            _label.color = c;
        }

        private void UpdateTrailAndGlow()
        {
            float speed = _rb != null ? new Vector3(_rb.velocity.x, 0f, _rb.velocity.z).magnitude : 0f;
            bool fast = speed > 4f && _runtime.state == MarbleRaceState.Racing;
            bool lowEnergy = _runtime.energy < 20f;

            if (_trail != null)
                _trail.emitting = fast && !lowEnergy;

            // Brilho do modo (PRD 10/17).
            if (_glow != null)
            {
                if (_isWinner) { _glow.enabled = true; _glow.color = new Color(1f, 0.85f, 0.3f); _glow.intensity = 3f; }
                else if (_runtime.mode == RaceMode.Push && _runtime.state == MarbleRaceState.Racing)
                { _glow.enabled = true; _glow.color = new Color(1f, 0.55f, 0.15f); _glow.intensity = 2.2f; }
                else if (_runtime.mode == RaceMode.Save && _runtime.state == MarbleRaceState.Racing)
                { _glow.enabled = true; _glow.color = new Color(0.3f, 0.6f, 1f); _glow.intensity = 0.9f; }
                else _glow.enabled = false;
            }

            // Pulso sutil no Push.
            if (_body != null)
            {
                float pulse = (_runtime.mode == RaceMode.Push && _runtime.state == MarbleRaceState.Racing)
                    ? 1f + 0.06f * Mathf.Sin(Time.time * 18f) : 1f;
                _body.localScale = _bodyBaseScale * pulse;
            }
        }

        private void UpdatePitOverlay()
        {
            if (_pitOverlay == null) return;
            bool inPitFlow = _runtime.state == MarbleRaceState.EnteringPit
                          || _runtime.state == MarbleRaceState.InPit
                          || _runtime.state == MarbleRaceState.ExitingPit;
            _pitOverlay.SetActive(inPitFlow);
            if (!inPitFlow) return;

            Billboard(_pitOverlay.transform);

            float progress = 0f;
            if (_runtime.state == MarbleRaceState.InPit)
                progress = Mathf.Clamp01(1f - _runtime.pitTimer / Mathf.Max(0.01f, _runtime.pitTotalTime));
            else if (_runtime.state == MarbleRaceState.ExitingPit)
                progress = 1f;

            if (_pitBarFill != null)
            {
                float w = Mathf.Max(0.01f, _pitBarWidth * progress);
                _pitBarFill.localScale = new Vector3(w, 0.08f, 0.3f);
                // Ancorado a esquerda: cresce da borda esquerda.
                _pitBarFill.localPosition = new Vector3(-_pitBarWidth * 0.5f + w * 0.5f, 0.02f, 0f);
            }
        }

        public void SetWinner() => _isWinner = true;
    }
}
