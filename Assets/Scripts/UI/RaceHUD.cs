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
    /// HUD da corrida construido por codigo (PRD 23.5). O ranking ao vivo segue
    /// o layout de "timing tower" pedido na referencia: posicao, chip/cor da
    /// equipe (logo placeholder), sigla de 3 letras do piloto, gap para o lider
    /// e indicador do anel (estilo pneu), com setas de variacao de posicao.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        private RaceManager _race;
        private CameraController _camera;

        private Text _topText;
        private Text _countdownText;
        private Text _logText;
        private readonly List<string> _log = new();

        // --- Timing tower ---
        private class RankingRow
        {
            public Image bg;
            public Text pos;
            public Text arrow;
            public Image chip;
            public Text number;
            public Text code;
            public Text gap;
            public Text grip;
        }
        private readonly List<RankingRow> _rows = new();
        private readonly Dictionary<MarbleRuntime, int> _posSnapshot = new();
        private float _snapshotTimer;

        // --- Painel da equipe do jogador ---
        private class PlayerPanel { public MarbleController ctrl; public Text info; }
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

            BuildTimingTower(canvas.transform);

            // Inferior: log de eventos.
            var bottom = UIFactory.Panel(canvas.transform, new Vector2(0.24f, 0f), new Vector2(0.78f, 0.13f),
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

        // ---- Timing tower (referencia do usuario) ------------------------

        private void BuildTimingTower(Transform canvas)
        {
            int count = _race.Field.Count;

            // Container a esquerda.
            var container = UIFactory.Panel(canvas, new Vector2(0.005f, 0.13f), new Vector2(0.235f, 0.93f),
                Vector2.zero, Vector2.zero, new Color(0.03f, 0.03f, 0.05f, 0.85f));

            // Cabecalho "LAP x/y" fixo no topo do container.
            const float headerH = 44f;
            var header = UIFactory.Panel(container, new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, new Color(0.10f, 0.10f, 0.14f, 1f));
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, headerH);
            header.anchoredPosition = Vector2.zero;
            _topLap = UIFactory.Label(header, "LAP 1", 24, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Color.white);

            float rowH = Mathf.Clamp(900f / Mathf.Max(1, count), 28f, 60f);

            for (int i = 0; i < count; i++)
            {
                var row = CreateRow(container, i, rowH, headerH);
                _rows.Add(row);
            }
        }

        private Text _topLap;

        private RankingRow CreateRow(RectTransform container, int index, float rowH, float topOffset)
        {
            var rowGo = new GameObject($"Row{index}", typeof(Image));
            rowGo.transform.SetParent(container, false);
            var bg = rowGo.GetComponent<Image>();
            bg.color = (index % 2 == 0)
                ? new Color(0.10f, 0.10f, 0.13f, 0.95f)
                : new Color(0.07f, 0.07f, 0.10f, 0.95f);

            var rt = rowGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, rowH - 2f);
            rt.anchoredPosition = new Vector2(0f, -(topOffset + index * rowH));

            var row = new RankingRow { bg = bg };

            // Posicao.
            row.pos = UIFactory.Label(rowGo.transform, "", 22, TextAnchor.MiddleCenter,
                new Vector2(0.01f, 0f), new Vector2(0.15f, 1f), Color.white);
            // Seta de variacao.
            row.arrow = UIFactory.Label(rowGo.transform, "", 18, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0f), new Vector2(0.22f, 1f), Color.white);

            // Chip/cor da equipe (logo placeholder) com numero.
            var chipGo = new GameObject("Chip", typeof(Image));
            chipGo.transform.SetParent(rowGo.transform, false);
            row.chip = chipGo.GetComponent<Image>();
            var chipRt = chipGo.GetComponent<RectTransform>();
            chipRt.anchorMin = new Vector2(0.23f, 0.18f);
            chipRt.anchorMax = new Vector2(0.31f, 0.82f);
            chipRt.offsetMin = Vector2.zero; chipRt.offsetMax = Vector2.zero;
            row.number = UIFactory.Label(chipGo.transform, "", 16, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Color.white);

            // Sigla de 3 letras.
            row.code = UIFactory.Label(rowGo.transform, "", 22, TextAnchor.MiddleLeft,
                new Vector2(0.34f, 0f), new Vector2(0.6f, 1f), Color.white);

            // Gap para o lider.
            row.gap = UIFactory.Label(rowGo.transform, "", 20, TextAnchor.MiddleRight,
                new Vector2(0.55f, 0f), new Vector2(0.88f, 1f), new Color(0.85f, 0.85f, 0.9f));

            // Indicador do anel (estilo pneu).
            row.grip = UIFactory.Label(rowGo.transform, "", 22, TextAnchor.MiddleCenter,
                new Vector2(0.88f, 0f), new Vector2(0.99f, 1f), Color.white);

            return row;
        }

        private void UpdateTimingTower()
        {
            var field = _race.Field;

            // Atualiza snapshot de posicoes a cada 1.5s para as setas.
            _snapshotTimer -= Time.deltaTime;
            bool refreshSnapshot = _snapshotTimer <= 0f;
            if (refreshSnapshot) _snapshotTimer = 1.5f;

            for (int i = 0; i < _rows.Count && i < field.Count; i++)
            {
                var m = field[i].Runtime;
                var row = _rows[i];

                row.pos.text = (i + 1).ToString();
                row.code.text = m.driver != null ? m.driver.shortCode : "MAR";
                row.chip.color = m.TeamPrimary;
                row.number.text = m.driver != null ? m.driver.number.ToString() : "";
                row.number.color = m.TeamSecondary;

                // Gap.
                row.gap.text = i == 0 ? "Leader" : $"+{m.gapToLeader:0.000}";

                // Indicador de anel.
                row.grip.text = m.grip != null ? m.grip.DisplayLetter : "?";
                row.grip.color = m.grip != null ? m.grip.DisplayColor : Color.gray;

                // Seta de variacao de posicao.
                int prev = _posSnapshot.TryGetValue(m, out var p) ? p : (i + 1);
                if (i + 1 < prev) { row.arrow.text = "▲"; row.arrow.color = new Color(0.3f, 0.9f, 0.4f); }
                else if (i + 1 > prev) { row.arrow.text = "▼"; row.arrow.color = new Color(0.9f, 0.3f, 0.3f); }
                else { row.arrow.text = ""; }
                if (refreshSnapshot) _posSnapshot[m] = i + 1;

                // Destaca a linha do jogador.
                if (m.isPlayer)
                    row.bg.color = new Color(0.18f, 0.16f, 0.05f, 0.95f);
            }
        }

        // ---- Painel da equipe do jogador ---------------------------------

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

                var pit = UIFactory.Button(panel, "PIT", new Color(0.8f, 0.3f, 0.2f),
                    new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.4f), Vector2.zero, Vector2.zero);
                var captured = ctrl;
                pit.onClick.AddListener(() =>
                    _race.RequestPit(captured, captured.Runtime.grip.gripId, true, 60f));

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

            int leaderLap = 1;
            if (_race.Field.Count > 0)
                leaderLap = Mathf.Clamp(_race.Field[0].Runtime.completedLaps + 1, 1, _race.TotalLaps);
            _topText.text = $"{_race.Config.track.trackName}   |   Clima: {_race.Config.weather}";
            if (_topLap != null) _topLap.text = $"LAP {leaderLap}/{_race.TotalLaps}";

            UpdateTimingTower();

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

        private void OnCountdown(int v) => _countdownText.text = v <= 0 ? "GO!" : v.ToString();

        private void PushLog(string msg)
        {
            _log.Add(msg);
            if (_log.Count > 5) _log.RemoveAt(0);
            _logText.text = string.Join("\n", _log);
        }

        private void OnFinished(RaceResult result) => _countdownText.text = "FIM";
    }
}
