using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MarbleGP.AI;
using MarbleGP.CameraSystem;
using MarbleGP.Race;

namespace MarbleGP.UI
{
    /// <summary>
    /// Reproduz o replay gravado pelo RaceRecorder: move as bolinhas (já paradas)
    /// pelo movimento gravado, com câmera cinematográfica e uma HUD de replay
    /// (play/pause, velocidade, reiniciar, e atalhos para os melhores momentos).
    /// Não re-simula — apenas interpola os keyframes. PRD extra.
    /// </summary>
    public class RaceReplayPlayer : MonoBehaviour
    {
        private List<MarbleController> _marbles;
        private List<ReplayFrame> _frames;
        private List<ReplayHighlight> _highlights;
        private float _duration;

        private CameraController _camCtrl;
        private Camera _cam;
        private Action _onExit;
        private readonly List<bool> _wasKinematic = new List<bool>();

        private float _t;
        private bool _playing = true;
        private float _speed = 1f;
        private int _focus;
        private bool _camSnapped;

        private Text _timeText, _playLabel, _speedLabel;
        private RectTransform _progressFill;
        private GameObject _canvas;

        public void Init(RaceRecorder rec, CameraController cam, Action onExit)
        {
            _marbles = new List<MarbleController>(rec.Marbles);
            _frames = rec.Frames;
            _highlights = rec.Highlights;
            _duration = Mathf.Max(0.1f, rec.Duration);
            _camCtrl = cam;
            _cam = cam != null ? cam.GetComponent<Camera>() : null;
            _onExit = onExit;

            // Câmera assume modo replay; congela o controlador normal.
            if (_camCtrl != null) _camCtrl.enabled = false;

            // Bolinhas viram cinemáticas para serem movidas pelo replay.
            foreach (var m in _marbles)
            {
                var rb = m != null ? m.GetComponent<Rigidbody>() : null;
                _wasKinematic.Add(rb != null && rb.isKinematic);
                if (rb != null) rb.isKinematic = true;
            }

            // Foco inicial: uma bolinha do jogador, senão o vencedor, senão a 0.
            _focus = 0;
            for (int i = 0; i < _marbles.Count; i++)
                if (_marbles[i] != null && _marbles[i].Runtime.isPlayer) { _focus = i; break; }

            BuildHud();
            ApplyFrame(0f);
        }

        private void Update()
        {
            if (_frames == null || _frames.Count == 0) return;

            if (_playing)
            {
                _t += Time.unscaledDeltaTime * _speed;
                if (_t >= _duration) { _t = _duration; _playing = false; RefreshPlayLabel(); }
            }

            ApplyFrame(_t);
            FollowCamera();

            if (_timeText != null) _timeText.text = $"{Fmt(_t)} / {Fmt(_duration)}";
            if (_progressFill != null)
            {
                float f = Mathf.Clamp01(_t / _duration);
                _progressFill.anchorMin = Vector2.zero;
                _progressFill.anchorMax = new Vector2(f, 1f);
                _progressFill.offsetMin = Vector2.zero; _progressFill.offsetMax = Vector2.zero;
            }
        }

        private void ApplyFrame(float t)
        {
            float fi = t / RaceRecorder.Interval;
            int i = Mathf.Clamp(Mathf.FloorToInt(fi), 0, _frames.Count - 1);
            int j = Mathf.Min(i + 1, _frames.Count - 1);
            float frac = Mathf.Clamp01(fi - i);
            var a = _frames[i]; var b = _frames[j];
            int n = Mathf.Min(_marbles.Count, a.pos.Length);
            for (int k = 0; k < n; k++)
            {
                if (_marbles[k] == null) continue;
                _marbles[k].transform.position = Vector3.Lerp(a.pos[k], b.pos[k], frac);
            }
        }

        private void FollowCamera()
        {
            if (_cam == null || _focus < 0 || _focus >= _marbles.Count || _marbles[_focus] == null) return;
            Vector3 focus = _marbles[_focus].transform.position;
            const float h = 30f, tilt = 22f;
            Vector3 target = focus + Vector3.up * h + Vector3.back * (h * Mathf.Tan(tilt * Mathf.Deg2Rad));
            Quaternion rot = Quaternion.Euler(90f - tilt, 0f, 0f);
            if (!_camSnapped)
            {
                _cam.transform.position = target; _camSnapped = true;
            }
            else
            {
                _cam.transform.position = Vector3.Lerp(_cam.transform.position, target, 6f * Time.unscaledDeltaTime);
            }
            _cam.transform.rotation = rot;
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, 12f, 5f * Time.unscaledDeltaTime);
        }

        private void Seek(float t, int focus)
        {
            _t = Mathf.Clamp(t, 0f, _duration);
            if (focus >= 0) _focus = focus;
            _playing = true;
            _camSnapped = false; // recentra na bolinha do destaque
            RefreshPlayLabel();
        }

        // ---- HUD ----

        private void BuildHud()
        {
            var canvas = UIFactory.CreateCanvas("ReplayHUD");
            _canvas = canvas.gameObject;
            var root = UIFactory.SafeAreaRoot(canvas.transform);

            // Selo REPLAY + tempo (topo).
            var top = UIFactory.Panel(root, new Vector2(0.4f, 0.92f), new Vector2(0.6f, 0.98f),
                Vector2.zero, Vector2.zero, new Color(0.04f, 0.07f, 0.12f, 0.95f));
            UIFactory.NeonBorder(top.gameObject, MarbleUITheme.NeonCyan, 0.7f, 1.6f);
            UIFactory.Label(top, "REPLAY", 15, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0f), new Vector2(0.45f, 1f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;
            _timeText = UIFactory.Label(top, "", 14, TextAnchor.MiddleRight,
                new Vector2(0.45f, 0f), new Vector2(0.94f, 1f), Color.white);

            // Chips de melhores momentos (acima da barra inferior).
            BuildHighlightChips(root);

            // Barra inferior de controles.
            var bar = UIFactory.Panel(root, new Vector2(0f, 0f), new Vector2(1f, 0.1f),
                Vector2.zero, Vector2.zero, new Color(0.03f, 0.05f, 0.09f, 0.96f));

            var play = UIFactory.Button(bar, "PAUSA", MarbleUITheme.NeonBlue,
                new Vector2(0.04f, 0.2f), new Vector2(0.11f, 0.8f), Vector2.zero, Vector2.zero);
            _playLabel = play.GetComponentInChildren<Text>();
            _playLabel.fontSize = 14;
            play.onClick.AddListener(() => { _playing = !_playing; if (_t >= _duration) _t = 0f; RefreshPlayLabel(); });

            var restart = UIFactory.Button(bar, "INÍCIO", new Color(0.30f, 0.34f, 0.45f),
                new Vector2(0.12f, 0.2f), new Vector2(0.19f, 0.8f), Vector2.zero, Vector2.zero);
            restart.GetComponentInChildren<Text>().fontSize = 14;
            restart.onClick.AddListener(() => Seek(0f, _focus));

            var speed = UIFactory.Button(bar, "1x", new Color(0.30f, 0.34f, 0.45f),
                new Vector2(0.20f, 0.2f), new Vector2(0.26f, 0.8f), Vector2.zero, Vector2.zero);
            _speedLabel = speed.GetComponentInChildren<Text>();
            speed.onClick.AddListener(CycleSpeed);

            // Barra de progresso (não interativa).
            var pbBg = UIFactory.Panel(bar, new Vector2(0.27f, 0.38f), new Vector2(0.82f, 0.62f),
                Vector2.zero, Vector2.zero, new Color(0.07f, 0.1f, 0.16f, 1f));
            _progressFill = UIFactory.SolidPanel(pbBg, Vector2.zero, new Vector2(0f, 1f), MarbleUITheme.NeonCyan);
            _progressFill.GetComponent<Image>().raycastTarget = false;

            var exit = UIFactory.Button(bar, "Sair do Replay", MarbleUITheme.NeonOrange,
                new Vector2(0.84f, 0.2f), new Vector2(0.97f, 0.8f), Vector2.zero, Vector2.zero);
            exit.GetComponentInChildren<Text>().fontSize = 15;
            exit.onClick.AddListener(Exit);

            RefreshPlayLabel();
        }

        private void BuildHighlightChips(Transform root)
        {
            if (_highlights == null || _highlights.Count == 0) return;
            UIFactory.Label(root, "MELHORES MOMENTOS", 12, TextAnchor.LowerLeft,
                new Vector2(0.04f, 0.155f), new Vector2(0.5f, 0.19f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;

            int count = Mathf.Min(_highlights.Count, 10);
            float x0 = 0.04f, w = 0.072f, gap = 0.008f;
            for (int i = 0; i < count; i++)
            {
                var h = _highlights[i];
                float x = x0 + i * (w + gap);
                var btn = UIFactory.Button(root, ChipLabel(h.kind), ChipColor(h.kind),
                    new Vector2(x, 0.11f), new Vector2(x + w, 0.15f), Vector2.zero, Vector2.zero);
                btn.GetComponentInChildren<Text>().fontSize = 14;
                var hl = h;
                btn.onClick.AddListener(() => Seek(hl.time, hl.focusIndex));
            }
        }

        private static string ChipLabel(HighlightKind k)
        {
            switch (k)
            {
                case HighlightKind.Overtake: return "ULTR";
                case HighlightKind.Crash: return "BATIDA";
                case HighlightKind.Finish: return "FIM";
                default: return "SC";
            }
        }

        private static Color ChipColor(HighlightKind k)
        {
            switch (k)
            {
                case HighlightKind.Overtake: return MarbleUITheme.NeonGreen;
                case HighlightKind.Crash: return MarbleUITheme.NeonRed;
                case HighlightKind.Finish: return MarbleUITheme.NeonGold;
                default: return MarbleUITheme.Warning;
            }
        }

        private void CycleSpeed()
        {
            _speed = _speed >= 2f ? 0.5f : (_speed >= 1f ? 2f : 1f);
            if (_speedLabel != null) _speedLabel.text = _speed == 0.5f ? "0.5x" : (_speed == 2f ? "2x" : "1x");
        }

        private void RefreshPlayLabel()
        {
            if (_playLabel != null) _playLabel.text = _playing ? "PAUSA" : "PLAY";
        }

        private static string Fmt(float t)
        {
            if (t < 0f) t = 0f;
            int m = (int)(t / 60f);
            int s = (int)(t % 60f);
            return $"{m}:{s:00}";
        }

        private void Exit()
        {
            // Restaura câmera e física.
            if (_camCtrl != null) _camCtrl.enabled = true;
            for (int i = 0; i < _marbles.Count; i++)
            {
                var rb = _marbles[i] != null ? _marbles[i].GetComponent<Rigidbody>() : null;
                if (rb != null) rb.isKinematic = i < _wasKinematic.Count && _wasKinematic[i];
            }
            if (_canvas != null) Destroy(_canvas);
            var cb = _onExit; _onExit = null;
            Destroy(this);
            cb?.Invoke();
        }
    }
}
