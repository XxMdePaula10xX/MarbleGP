using System.Collections.Generic;
using UnityEngine;
using MarbleGP.AI;

namespace MarbleGP.Race
{
    public enum HighlightKind { Overtake, Crash, Finish, SafetyMarble }

    public class ReplayHighlight
    {
        public float time;
        public int focusIndex;     // bolinha a seguir (indice fixo no snapshot)
        public string label;
        public HighlightKind kind;
    }

    /// <summary>Um keyframe: posicao de cada bolinha (ordem fixa do snapshot).</summary>
    public class ReplayFrame { public Vector3[] pos; }

    /// <summary>
    /// Grava a corrida (posições amostradas + marcadores de destaque) para um
    /// replay/highlights depois. Não re-simula: guarda o movimento, leve o
    /// suficiente para minutos de corrida com 20 bolinhas (~0.5 MB). PRD extra.
    /// </summary>
    public class RaceRecorder : MonoBehaviour
    {
        public const float Interval = 0.08f; // ~12.5 amostras/s

        public float Duration { get; private set; }
        public IReadOnlyList<MarbleController> Marbles => _marbles;
        public List<ReplayFrame> Frames => _frames;
        public List<ReplayHighlight> Highlights => _highlights;

        private RaceManager _race;
        private List<MarbleController> _marbles;
        private readonly List<ReplayFrame> _frames = new List<ReplayFrame>();
        private readonly List<ReplayHighlight> _highlights = new List<ReplayHighlight>();
        private float _t, _sample;
        private bool _recording;

        public void Begin(RaceManager race)
        {
            _race = race;
            _marbles = new List<MarbleController>(race.Field); // ordem fixa (snapshot)
            _race.OnRaceStarted += () => _recording = true;
            _race.OnRaceEvent += OnEvent;
            _race.OnRaceFinished += _ => { CaptureFrame(); _recording = false; };
        }

        private void LateUpdate()
        {
            if (!_recording || _marbles == null) return;
            _t += Time.deltaTime;
            Duration = _t;
            _sample -= Time.deltaTime;
            if (_sample <= 0f) { _sample = Interval; CaptureFrame(); }
        }

        private void CaptureFrame()
        {
            if (_marbles == null) return;
            int n = _marbles.Count;
            var f = new ReplayFrame { pos = new Vector3[n] };
            for (int i = 0; i < n; i++)
                f.pos[i] = _marbles[i] != null ? _marbles[i].transform.position : Vector3.zero;
            _frames.Add(f);
        }

        private void OnEvent(string msg)
        {
            if (!_recording || _highlights.Count >= 24) return;
            HighlightKind kind;
            if (msg.Contains("ultrapassou")) kind = HighlightKind.Overtake;
            else if (msg.Contains("bateu forte")) kind = HighlightKind.Crash;
            else if (msg.Contains("Safety Marble na")) kind = HighlightKind.SafetyMarble;
            else if (msg.Contains("cruzou a linha em 1o") || msg.Contains("venceu")) kind = HighlightKind.Finish;
            else return;

            _highlights.Add(new ReplayHighlight
            {
                time = _t,
                focusIndex = FocusFor(msg),
                label = Clean(msg),
                kind = kind,
            });
        }

        private int FocusFor(string msg)
        {
            for (int i = 0; i < _marbles.Count; i++)
            {
                var nm = _marbles[i] != null ? _marbles[i].Runtime.DisplayName : null;
                if (!string.IsNullOrEmpty(nm) && msg.Contains(nm)) return i;
            }
            var leader = _race.Field.Count > 0 ? _race.Field[0] : null;
            int li = leader != null ? _marbles.IndexOf(leader) : 0;
            return li >= 0 ? li : 0;
        }

        private static string Clean(string m)
        {
            int i = 0;
            while (i < m.Length && !char.IsLetterOrDigit(m[i])) i++;
            return i < m.Length ? m.Substring(i) : m;
        }
    }
}
