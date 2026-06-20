using UnityEngine;
using MarbleGP.Track;

namespace MarbleGP.CameraSystem
{
    /// <summary>
    /// Camera ortografica top-down (PRD 22.1). Modos no MVP: visao geral e
    /// seguir bolinha selecionada. Permite zoom moderado.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        public enum Mode { Overview, Follow }

        [SerializeField] private float height = 40f;
        [SerializeField] private float tilt = 18f;       // leve inclinacao (PRD 22.1)
        [SerializeField] private float followSize = 14f;
        [SerializeField] private float minSize = 6f;
        [SerializeField] private float maxSize = 90f;
        [SerializeField] private float followLerp = 6f;

        public Mode CurrentMode { get; private set; } = Mode.Overview;

        private Camera _cam;
        private Transform _target;
        private Vector3 _overviewCenter;
        private float _overviewSize = 60f;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            transform.rotation = Quaternion.Euler(90f - tilt, 0f, 0f);
        }

        /// <summary>Enquadra a pista inteira a partir da geometria (PRD 22.1 visao geral).</summary>
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
            _overviewSize = Mathf.Clamp(extent * 1.25f, minSize, maxSize);
            SetOverview();
        }

        public void SetOverview()
        {
            CurrentMode = Mode.Overview;
            _cam.orthographicSize = _overviewSize;
        }

        public void Follow(Transform target)
        {
            _target = target;
            CurrentMode = Mode.Follow;
            _cam.orthographicSize = followSize;
        }

        public void ToggleMode(Transform fallbackTarget)
        {
            if (CurrentMode == Mode.Overview) Follow(_target != null ? _target : fallbackTarget);
            else SetOverview();
        }

        private void LateUpdate()
        {
            Vector3 focus = CurrentMode == Mode.Follow && _target != null
                ? _target.position
                : _overviewCenter;

            Vector3 desired = focus + Vector3.up * height
                              + Vector3.back * (height * Mathf.Tan(Mathf.Deg2Rad * tilt));
            transform.position = Vector3.Lerp(transform.position, desired, followLerp * Time.deltaTime);

            // Zoom moderado por scroll.
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - scroll * 20f, minSize, maxSize);
        }
    }
}
