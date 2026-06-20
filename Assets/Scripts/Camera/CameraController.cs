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
            CurrentMode = Mode.Overview;
            _targetSize = _overviewSize;
        }

        /// <summary>Alterna entre os modos disponiveis (botao Camera).</summary>
        public void Cycle()
        {
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
            Transform target = ResolveTarget();
            Vector3 focus = (CurrentMode != Mode.Overview && target != null)
                ? target.position
                : _overviewCenter;

            transform.position = Vector3.Lerp(transform.position, DesiredPosition(focus), moveLerp * Time.deltaTime);

            // Zoom por scroll (ajusta o alvo).
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
                _targetSize = Mathf.Clamp(_targetSize - scroll * 18f, minSize, maxSize);

            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetSize, sizeLerp * Time.deltaTime);
        }
    }
}
