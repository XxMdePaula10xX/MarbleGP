using System;
using UnityEngine;
using MarbleGP.Track;

namespace MarbleGP.CameraSystem
{
    /// <summary>
    /// Camera ortografica top-down (PRD 22.1). Modos: visao geral, seguir
    /// bolinha 1 do jogador, seguir bolinha 2 e seguir o lider. Movimento
    /// suavizado e zoom moderado por scroll.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        public enum Mode { Overview, FollowP1, FollowP2, FollowLeader }

        [SerializeField] private float height = 38f;
        [SerializeField] private float tilt = 20f;
        [SerializeField] private float followSize = 11f;   // zoom mais proximo (PRD 4)
        [SerializeField] private float minSize = 6f;
        [SerializeField] private float maxSize = 110f;
        [SerializeField] private float moveLerp = 7f;
        [SerializeField] private float sizeLerp = 6f;

        public Mode CurrentMode { get; private set; } = Mode.Overview;

        private Camera _cam;
        private Vector3 _overviewCenter;
        private float _overviewSize = 60f;
        private float _targetSize;

        private Transform _p1, _p2;
        private Func<Transform> _leaderGetter;

        // ---- Controle manual por toque (pinça/arraste) -------------------
        private bool _manual;            // o jogador assumiu a camera (pinch/pan)
        private bool _twoFingerActive;   // ha 2 dedos na tela neste frame
        private Vector3 _manualCenter;   // ponto-foco no chao quando manual
        private float _lastPinchDist;
        private Vector2 _lastPanMid;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            transform.rotation = Quaternion.Euler(90f - tilt, 0f, 0f);
            _targetSize = _overviewSize;
        }

        public void SetSubjects(Transform p1, Transform p2, Func<Transform> leaderGetter)
        {
            _p1 = p1;
            _p2 = p2;
            _leaderGetter = leaderGetter;
        }

        /// <summary>Enquadra a pista inteira (visao geral, PRD 22.1).</summary>
        public void FrameTrack(TrackManager track)
        {
            if (track == null || track.IdealLine == null) return;
            Vector3 min = track.IdealLine.Points[0];
            Vector3 max = min;
            foreach (var p in track.IdealLine.Points)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            _overviewCenter = (min + max) * 0.5f;
            float extent = Mathf.Max(max.x - min.x, max.z - min.z) * 0.5f;
            _overviewSize = Mathf.Clamp(extent * 1.12f, minSize, maxSize);
            SetOverview();
            // Posiciona imediatamente para nao "voar" no inicio.
            transform.position = DesiredPosition(_overviewCenter);
            _cam.orthographicSize = _overviewSize;
        }

        public void SetOverview()
        {
            _manual = false;             // retoma controle automatico
            CurrentMode = Mode.Overview;
            _targetSize = _overviewSize;
        }

        /// <summary>Alterna entre os modos disponiveis (botao Camera).</summary>
        public void Cycle()
        {
            _manual = false;             // botao Camera retoma o automatico
            CurrentMode = (Mode)(((int)CurrentMode + 1) % 4);
            _targetSize = CurrentMode == Mode.Overview ? _overviewSize : followSize;
        }

        /// <summary>Mantido por compatibilidade com chamadas antigas.</summary>
        public void ToggleMode(Transform fallback) => Cycle();

        public string CurrentLabel
        {
            get
            {
                switch (CurrentMode)
                {
                    case Mode.FollowP1: return "Cam: Bolinha 1";
                    case Mode.FollowP2: return "Cam: Bolinha 2";
                    case Mode.FollowLeader: return "Cam: Lider";
                    default: return "Cam: Geral";
                }
            }
        }

        private Transform ResolveTarget()
        {
            switch (CurrentMode)
            {
                case Mode.FollowP1: return _p1;
                case Mode.FollowP2: return _p2;
                case Mode.FollowLeader: return _leaderGetter != null ? _leaderGetter() : null;
                default: return null;
            }
        }

        private Vector3 DesiredPosition(Vector3 focus)
            => focus + Vector3.up * height + Vector3.back * (height * Mathf.Tan(Mathf.Deg2Rad * tilt));

        private void LateUpdate()
        {
            // Gestos de toque (pinça/arraste) tem prioridade e ligam o modo manual.
            HandleTouch();

            // Zoom por scroll (desktop/editor) — ajusta o alvo sem virar manual.
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
                _targetSize = Mathf.Clamp(_targetSize - scroll * 18f, minSize, maxSize);

            Vector3 focus;
            if (_manual)
            {
                focus = _manualCenter;
            }
            else
            {
                Transform target = ResolveTarget();
                focus = (CurrentMode != Mode.Overview && target != null)
                    ? target.position
                    : _overviewCenter;
            }

            // Durante a pinça/arraste a camera acompanha sem suavizacao (1:1 no
            // dedo); fora disso, mantem o movimento/zoom suaves.
            float posT = _twoFingerActive ? 1f : Mathf.Clamp01(moveLerp * Time.deltaTime);
            float szT  = _twoFingerActive ? 1f : Mathf.Clamp01(sizeLerp * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, DesiredPosition(focus), posT);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetSize, szT);
        }

        // ---- Pinça para zoom (no ponto dos dedos) + arraste com 2 dedos ----

        private void HandleTouch()
        {
            if (Input.touchCount < 2) { _twoFingerActive = false; return; }

            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);
            Vector2 mid = (t0.position + t1.position) * 0.5f;
            float dist = Vector2.Distance(t0.position, t1.position);

            // Primeiro frame com 2 dedos: assume o controle no ponto atual,
            // sem mover (evita "salto").
            if (!_twoFingerActive)
            {
                _twoFingerActive = true;
                _manual = true;
                _manualCenter = CurrentGroundFocus();
                _lastPinchDist = dist;
                _lastPanMid = mid;
                return;
            }

            float s0 = _cam.orthographicSize;

            // Pontos no chao (y=0) sob o centro dos dedos, antes e agora.
            Vector3 gPrev = ScreenToGround(_lastPanMid);
            Vector3 gNow = ScreenToGround(mid);

            // ARRASTE: "cola" o mundo sob os dedos (o ponto anterior vai para o atual).
            _manualCenter += new Vector3(gPrev.x - gNow.x, 0f, gPrev.z - gNow.z);

            // ZOOM: escala em torno do ponto sob os dedos (gPrev, ja colado).
            // Para uma camera ortografica, manter um ponto fixo sob o dedo ao mudar
            // o size s0->s1 significa mover o foco: F1 = P + (F0 - P) * (s1/s0).
            if (_lastPinchDist > 1f && dist > 1f)
            {
                float s1 = Mathf.Clamp(s0 * (_lastPinchDist / dist), minSize, maxSize);
                float k = s1 / s0;
                Vector3 f0 = _manualCenter;
                _manualCenter = new Vector3(
                    gPrev.x + (f0.x - gPrev.x) * k, 0f,
                    gPrev.z + (f0.z - gPrev.z) * k);
                _targetSize = s1;
            }

            _lastPinchDist = dist;
            _lastPanMid = mid;
        }

        /// <summary>Ponto do chao (plano y=0) sob uma coordenada de tela.</summary>
        private Vector3 ScreenToGround(Vector2 screenPos)
        {
            Ray ray = _cam.ScreenPointToRay(screenPos);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter)) return ray.GetPoint(enter);
            return _manualCenter;
        }

        /// <summary>Ponto do chao que a camera olha agora (centro da tela).</summary>
        private Vector3 CurrentGroundFocus()
        {
            Vector3 g = ScreenToGround(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            return new Vector3(g.x, 0f, g.z);
        }
    }
}
