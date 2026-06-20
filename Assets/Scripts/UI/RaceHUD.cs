using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MarbleGP.AI;
using MarbleGP.Core;
using MarbleGP.Race;
using MarbleGP.Systems;
using MarbleGP.CameraSystem;

namespace MarbleGP.UI
{
    /// <summary>
    /// HUD da corrida construido por codigo (PRD 23.5): topo (volta/pista/clima),
    /// esquerda (ranking ao vivo), direita (painel da equipe do jogador com
    /// botoes de pit e modo) e inferior (log de eventos). Separa UI da logica
    /// consumindo apenas a API publica do RaceManager (PRD 39.14).
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        private RaceManager _race;
        private CameraController _camera;

        private Text _topText;
        private Text _rankingText;
        private Text _countdownText;
        private Text _logText;
        private readonly List<string> _log = new();

        // Painel por bolinha do jogador.
        private class PlayerPanel
        {
            public MarbleController ctrl;
            public Text info;
        }
        private readonly List<PlayerPanel> _panels = new();

        public void Bind(RaceManager race, CameraController cam)
        {
            _race = race;
            _camera = cam;
            BuildUI();

            _race.OnCountdown += OnCountdown;
            _race.OnRaceStarted += () => _countdownText.text = "";
            _race.OnRaceEvent += PushLog;
            _race.OnRaceFinished += OnFinished;
        }

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("RaceHUD");
            canvas.transform.SetParent(transform, false);
            Color panelBg = new Color(0f, 0f, 0f, 0.55f);

            // Topo (PRD 23.5).
            var top = UIFactory.Panel(canvas.transform, new Vector2(0f, 0.94f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, panelBg);
            _topText = UIFactory.Label(top, "", 26, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Color.white);

            // Esquerda: ranking ao vivo.
            var left = UIFactory.Panel(canvas.transform, new Vector2(0f, 0.3f), new Vector2(0.22f, 0.94f),
                Vector2.zero, Vector2.zero, panelBg);
            _rankingText = UIFactory.Label(left, "", 20, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0f), new Vector2(1f, 0.98f), Color.white);

            // Inferior: log de eventos.
            var bottom = UIFactory.Panel(canvas.transform, new Vector2(0.22f, 0f), new Vector2(0.78f, 0.13f),
                Vector2.zero, Vector2.zero, panelBg);
            _logText = UIFactory.Label(bottom, "", 18, TextAnchor.LowerLeft,
                new Vector2(0.02f, 0f), new Vector2(1f, 1f), Color.white);

            // Centro: contagem regressiva.
            _countdownText = UIFactory.Label(canvas.transform, "", 120, TextAnchor.MiddleCenter,
                new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.75f), Color.yellow);

            // Botao de camera.
            var camBtn = UIFactory.Button(canvas.transform, "Camera", new Color(0.2f, 0.4f, 0.8f, 0.9f),
                new Vector2(0.78f, 0.94f), new Vector2(1f, 1f), new Vector2(6, 4), new Vector2(-6, -4));
            camBtn.onClick.AddListener(() =>
            {
                var firstPlayer = FindFirstPlayer();
                _camera.ToggleMode(firstPlayer != null ? firstPlayer.transform : null);
            });

            BuildPlayerPanels(canvas.transform);
        }

        private void BuildPlayerPanels(Transform canvas)
        {
            var players = new List<MarbleController>();
            foreach (var c in _race.Field)
                if (c.Runtime.isPlayer) players.Add(c);

            float top = 0.94f;
            float panelH = 0.2f;
            for (int i = 0; i < players.Count; i++)
            {
                var ctrl = players[i];
                float yMax = top - i * (panelH + 0.01f);
                float yMin = yMax - panelH;
                var panel = UIFactory.Panel(canvas, new Vector2(0.78f, yMin), new Vector2(1f, yMax),
                    new Vector2(4, 0), new Vector2(-4, 0), new Color(0.05f, 0.05f, 0.08f, 0.8f));

                var info = UIFactory.Label(panel, "", 18, TextAnchor.UpperLeft,
                    new Vector2(0.05f, 0.45f), new Vector2(1f, 1f), Color.white);
                _panels.Add(new PlayerPanel { ctrl = ctrl, info = info });

                // Botao Pit (PRD 18 / 8).
                var pit = UIFactory.Button(panel, "PIT", new Color(0.8f, 0.3f, 0.2f),
                    new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.4f), Vector2.zero, Vector2.zero);
                var captured = ctrl;
                pit.onClick.AddListener(() =>
                    _race.RequestPit(captured, captured.Runtime.grip.gripId, true, 60f));

                // Botao Modo (cicla Normal/Push/Save, PRD 17 MVP).
                var mode = UIFactory.Button(panel, "MODE", new Color(0.2f, 0.5f, 0.3f),
                    new Vector2(0.5f, 0.05f), new Vector2(0.95f, 0.4f), Vector2.zero, Vector2.zero);
                mode.onClick.AddListener(() => CycleMode(captured));
            }
        }

        private void CycleMode(MarbleController ctrl)
        {
            RaceMode next;
            switch (ctrl.Runtime.mode)
            {
                case RaceMode.Normal: next = RaceMode.Push; break;
                case RaceMode.Push: next = RaceMode.Save; break;
                default: next = RaceMode.Normal; break;
            }
            _race.SetMode(ctrl, next);
        }

        private MarbleController FindFirstPlayer()
        {
            foreach (var c in _race.Field)
                if (c.Runtime.isPlayer) return c;
            return _race.Field.Count > 0 ? _race.Field[0] : null;
        }

        private void Update()
        {
            if (_race == null || _race.Track == null) return;

            // Topo.
            int leaderLap = 1;
            if (_race.Field.Count > 0) leaderLap = Mathf.Clamp(_race.Field[0].Runtime.completedLaps + 1, 1, _race.TotalLaps);
            _topText.text = $"{_race.Config.track.trackName}   |   Volta {leaderLap}/{_race.TotalLaps}   |   Clima: {_race.Config.weather}";

            // Ranking ao vivo.
            var sb = new StringBuilder("RANKING\n");
            for (int i = 0; i < _race.Field.Count; i++)
            {
                var m = _race.Field[i].Runtime;
                string tag = m.isPlayer ? "►" : " ";
                sb.AppendLine($"{tag}{m.position,2}. {Trim(m.DisplayName, 10)}");
            }
            _rankingText.text = sb.ToString();

            // Paineis da equipe.
            foreach (var p in _panels)
            {
                var m = p.ctrl.Runtime;
                p.info.text =
                    $"{m.DisplayName}  P{m.position}\n" +
                    $"Anel: {m.grip.gripId}  Modo: {m.mode}\n" +
                    $"Desgaste: {m.wear:0}%  Energia: {m.energy:0}\n" +
                    $"Estado: {m.state}";
            }
        }

        private void OnCountdown(int v)
        {
            _countdownText.text = v <= 0 ? "GO!" : v.ToString();
        }

        private void PushLog(string msg)
        {
            _log.Add(msg);
            if (_log.Count > 5) _log.RemoveAt(0);
            _logText.text = string.Join("\n", _log);
        }

        private void OnFinished(RaceResult result)
        {
            _countdownText.text = "FIM";
            // A transicao para a tela de Results e feita pelo Bootstrap/fluxo de cenas.
        }

        private static string Trim(string s, int n) => s.Length <= n ? s : s.Substring(0, n);
    }
}
