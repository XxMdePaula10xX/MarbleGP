using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MarbleGP.AI;
using MarbleGP.Core;
using MarbleGP.Race;
using MarbleGP.Systems;
using MarbleGP.Track;
using MarbleGP.CameraSystem;

namespace MarbleGP.UI
{
    /// <summary>
    /// HUD da corrida (PRD 23.5) com visual polido: top bar central, timing
    /// tower com destaque do jogador, cards da equipe com barras de
    /// desgaste/energia e seletores de modo/pneu, log de eventos colorido com
    /// fade e minimapa. Consome apenas a API publica do RaceManager (PRD 39.14).
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        private RaceManager _race;
        private CameraController _camera;

        private Text _topCircuit, _topLap, _topWeather, _countdownText, _camLabel;
        private RectTransform _safetyBadge;

        // ---- Timing tower ----
        private class RankingRow
        {
            public Image bg, accent, chip;
            public Outline glow;
            public Text pos, arrow, number, code, gap, grip, pit;
        }
        private readonly List<RankingRow> _rows = new();
        private readonly Dictionary<MarbleRuntime, int> _posSnapshot = new();
        private float _snapshotTimer;
        private RectTransform _towerContainer;
        private Text _towerToggleLabel;
        private bool _towerCollapsed;

        // ---- Cards da equipe ----
        private class PlayerCard
        {
            public MarbleController ctrl;
            public GripType nextGrip;
            public Text title, tyre, mode, status, wearLabel, energyLabel, fuelLabel, pitLabel;
            public RectTransform wearFill, energyFill, fuelFill;
            public Button pitBtn;
            public GameObject modeSelector, tyreSelector;
        }
        private readonly List<PlayerCard> _cards = new();

        // ---- Log ----
        private struct LogItem { public string msg; public Color color; public float age; }
        private readonly List<LogItem> _logItems = new();
        private Text[] _logRows;
        private const int LogRows = 5;
        private const float LogFadeStart = 4.5f, LogFadeEnd = 7f;

        // ---- Minimap ----
        private Vector2 _mapMin, _mapMax;
        private RectTransform _mapContainer;
        private readonly List<(RectTransform rt, MarbleController ctrl)> _mapDots = new();

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

        // ================= BUILD =================

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("RaceHUD");
            canvas.transform.SetParent(transform, false);

            BuildTopBar(canvas.transform);
            BuildTimingTower(canvas.transform);
            BuildLog(canvas.transform);
            BuildMinimap(canvas.transform);
            BuildPlayerCards(canvas.transform);

            _countdownText = UIFactory.Label(canvas.transform, "", 130, TextAnchor.MiddleCenter,
                new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.75f), new Color(1f, 0.92f, 0.3f));
            _countdownText.fontStyle = FontStyle.Bold;
        }

        private void BuildTopBar(Transform canvas)
        {
            var bar = UIFactory.Panel(canvas, new Vector2(0.24f, 0.93f), new Vector2(0.78f, 1f),
                Vector2.zero, Vector2.zero, new Color(0.04f, 0.05f, 0.08f, 0.82f));

            _topCircuit = UIFactory.Label(bar, "", 24, TextAnchor.MiddleLeft,
                new Vector2(0.03f, 0f), new Vector2(0.42f, 1f), Color.white);
            _topCircuit.fontStyle = FontStyle.Bold;

            _topLap = UIFactory.Label(bar, "", 26, TextAnchor.MiddleCenter,
                new Vector2(0.42f, 0f), new Vector2(0.62f, 1f), new Color(1f, 0.95f, 0.6f));
            _topLap.fontStyle = FontStyle.Bold;

            _topWeather = UIFactory.Label(bar, "", 22, TextAnchor.MiddleRight,
                new Vector2(0.62f, 0f), new Vector2(0.97f, 1f), new Color(0.7f, 0.85f, 1f));

            // Badge de safety marble (escondido por padrao).
            _safetyBadge = UIFactory.Panel(canvas, new Vector2(0.4f, 0.87f), new Vector2(0.6f, 0.92f),
                Vector2.zero, Vector2.zero, new Color(0.85f, 0.55f, 0.1f, 0.95f));
            var sbText = UIFactory.Label(_safetyBadge, "SAFETY MARBLE", 22, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Color.white);
            sbText.fontStyle = FontStyle.Bold;
            _safetyBadge.gameObject.SetActive(false);

            // Botao de camera (canto sup. direito).
            var cam = UIFactory.Button(canvas, "Cam: Geral", new Color(0.2f, 0.45f, 0.85f, 0.95f),
                new Vector2(0.79f, 0.94f), new Vector2(0.995f, 0.99f), Vector2.zero, Vector2.zero);
            _camLabel = cam.GetComponentInChildren<Text>();
            cam.onClick.AddListener(() => { _camera.Cycle(); _camLabel.text = _camera.CurrentLabel; });
        }

        // ---- Timing tower ----

        private void BuildTimingTower(Transform canvas)
        {
            int count = _race.Field.Count;
            var container = UIFactory.Panel(canvas, new Vector2(0.008f, 0.14f), new Vector2(0.235f, 0.99f),
                Vector2.zero, Vector2.zero, new Color(0.04f, 0.04f, 0.06f, 0.9f));
            _towerContainer = container;

            const float headerH = 48f;
            var header = UIFactory.Panel(container, new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, new Color(0.12f, 0.13f, 0.18f, 1f));
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, headerH);
            header.anchoredPosition = Vector2.zero;
            var htxt = UIFactory.Label(header, "TIMING", 22, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0f), new Vector2(0.7f, 1f), new Color(0.8f, 0.85f, 1f));
            htxt.fontStyle = FontStyle.Bold;

            // Botao recolher/expandir (PRD 4.1).
            var toggle = UIFactory.Button(header, "«", new Color(0.2f, 0.25f, 0.4f, 0.95f),
                new Vector2(0.74f, 0.15f), new Vector2(0.97f, 0.85f), Vector2.zero, Vector2.zero);
            _towerToggleLabel = toggle.GetComponentInChildren<Text>();
            toggle.onClick.AddListener(ToggleTower);

            float rowH = Mathf.Clamp(880f / Mathf.Max(1, count), 30f, 58f);
            for (int i = 0; i < count; i++)
                _rows.Add(CreateRow(container, i, rowH, headerH));
        }

        private void ToggleTower()
        {
            _towerCollapsed = !_towerCollapsed;
            _towerToggleLabel.text = _towerCollapsed ? "»" : "«";
            // Recolhido: estreita o painel e mostra so posicao + chip + sigla.
            _towerContainer.anchorMax = new Vector2(_towerCollapsed ? 0.095f : 0.235f, 0.99f);
            foreach (var row in _rows)
            {
                bool show = !_towerCollapsed;
                row.arrow.gameObject.SetActive(show);
                row.gap.gameObject.SetActive(show);
                row.grip.gameObject.SetActive(show);
                row.pit.gameObject.SetActive(show);
                row.number.gameObject.SetActive(show);
            }
        }

        private RankingRow CreateRow(RectTransform container, int index, float rowH, float topOffset)
        {
            var rowGo = new GameObject($"Row{index}", typeof(Image));
            rowGo.transform.SetParent(container, false);
            var bg = rowGo.GetComponent<Image>();
            bg.color = (index % 2 == 0) ? new Color(0.11f, 0.11f, 0.15f, 0.96f)
                                        : new Color(0.08f, 0.08f, 0.12f, 0.96f);
            var rt = rowGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-4f, rowH - 3f);
            rt.anchoredPosition = new Vector2(0f, -(topOffset + index * rowH));

            var row = new RankingRow { bg = bg };

            // Glow (Outline) usado para destacar o jogador.
            row.glow = rowGo.AddComponent<Outline>();
            row.glow.effectColor = new Color(1f, 0.85f, 0.2f, 0f);
            row.glow.effectDistance = new Vector2(2.5f, 2.5f);

            // Faixa de cor da equipe (esquerda).
            var accentRt = UIFactory.Panel(rowGo.transform, new Vector2(0f, 0f), new Vector2(0.03f, 1f),
                Vector2.zero, Vector2.zero, Color.gray);
            row.accent = accentRt.GetComponent<Image>();

            row.pos = UIFactory.Label(rowGo.transform, "", 22, TextAnchor.MiddleCenter,
                new Vector2(0.04f, 0f), new Vector2(0.15f, 1f), Color.white);
            row.pos.fontStyle = FontStyle.Bold;

            row.arrow = UIFactory.Label(rowGo.transform, "", 17, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0f), new Vector2(0.21f, 1f), Color.white);

            var chipRt = UIFactory.Panel(rowGo.transform, new Vector2(0.22f, 0.18f), new Vector2(0.29f, 0.82f),
                Vector2.zero, Vector2.zero, Color.gray);
            row.chip = chipRt.GetComponent<Image>();
            row.number = UIFactory.Label(chipRt, "", 15, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Color.white);
            row.number.fontStyle = FontStyle.Bold;

            row.code = UIFactory.Label(rowGo.transform, "", 21, TextAnchor.MiddleLeft,
                new Vector2(0.31f, 0f), new Vector2(0.5f, 1f), Color.white);
            row.code.fontStyle = FontStyle.Bold;

            row.gap = UIFactory.Label(rowGo.transform, "", 18, TextAnchor.MiddleRight,
                new Vector2(0.49f, 0f), new Vector2(0.73f, 1f), new Color(0.85f, 0.85f, 0.9f));

            row.grip = UIFactory.Label(rowGo.transform, "", 21, TextAnchor.MiddleCenter,
                new Vector2(0.74f, 0f), new Vector2(0.85f, 1f), Color.white);
            row.grip.fontStyle = FontStyle.Bold;

            // Pit stops (PRD 4.2).
            row.pit = UIFactory.Label(rowGo.transform, "", 17, TextAnchor.MiddleCenter,
                new Vector2(0.85f, 0f), new Vector2(0.99f, 1f), new Color(0.7f, 0.78f, 0.9f));

            return row;
        }

        // ---- Log ----

        private void BuildLog(Transform canvas)
        {
            var panel = UIFactory.Panel(canvas, new Vector2(0.24f, 0f), new Vector2(0.78f, 0.135f),
                Vector2.zero, Vector2.zero, new Color(0.03f, 0.03f, 0.05f, 0.7f));
            _logRows = new Text[LogRows];
            for (int i = 0; i < LogRows; i++)
            {
                float yMin = 0.02f + i * 0.19f;
                _logRows[i] = UIFactory.Label(panel, "", 18, TextAnchor.LowerLeft,
                    new Vector2(0.02f, yMin), new Vector2(0.99f, yMin + 0.19f), Color.white);
            }
        }

        // ---- Minimap ----

        private void BuildMinimap(Transform canvas)
        {
            _mapContainer = UIFactory.Panel(canvas, new Vector2(0.008f, 0.005f), new Vector2(0.16f, 0.135f),
                Vector2.zero, Vector2.zero, new Color(0.05f, 0.07f, 0.05f, 0.85f));

            var lane = _race.Track != null ? _race.Track.IdealLine : null;
            if (lane == null) return;

            Vector3 min = lane.Points[0], max = lane.Points[0];
            foreach (var p in lane.Points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            Vector2 margin = new Vector2((max.x - min.x) * 0.08f + 1f, (max.z - min.z) * 0.08f + 1f);
            _mapMin = new Vector2(min.x - margin.x, min.z - margin.y);
            _mapMax = new Vector2(max.x + margin.x, max.z + margin.y);

            // Tracado da pista (pontos cinza).
            for (int i = 0; i < lane.Points.Length; i += 2)
            {
                var d = Dot(_mapContainer, new Color(0.5f, 0.55f, 0.5f), 5f);
                PlaceNorm(d, Norm(lane.Points[i]));
            }
            // Linha de chegada.
            var sf = Dot(_mapContainer, Color.white, 8f);
            PlaceNorm(sf, Norm(lane.Points[0]));

            // Pontos das bolinhas.
            foreach (var c in _race.Field)
            {
                float size = c.Runtime.isPlayer ? 12f : 8f;
                var dot = Dot(_mapContainer, c.Runtime.MarbleColor, size);
                if (c.Runtime.isPlayer)
                {
                    var o = dot.gameObject.AddComponent<Outline>();
                    o.effectColor = Color.white; o.effectDistance = new Vector2(1.5f, 1.5f);
                }
                _mapDots.Add((dot.rectTransform, c));
            }
        }

        private Vector2 Norm(Vector3 world)
            => new Vector2(Mathf.InverseLerp(_mapMin.x, _mapMax.x, world.x),
                           Mathf.InverseLerp(_mapMin.y, _mapMax.y, world.z));

        private static Image Dot(Transform parent, Color color, float sizePx)
        {
            var go = new GameObject("Dot", typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.rectTransform.sizeDelta = new Vector2(sizePx, sizePx);
            return img;
        }

        private static void PlaceNorm(Image img, Vector2 n) => PlaceNorm(img.rectTransform, n);
        private static void PlaceNorm(RectTransform rt, Vector2 n)
        {
            rt.anchorMin = rt.anchorMax = n;
            rt.anchoredPosition = Vector2.zero;
        }

        // ---- Cards da equipe ----

        private void BuildPlayerCards(Transform canvas)
        {
            var players = new List<MarbleController>();
            foreach (var c in _race.Field) if (c.Runtime.isPlayer) players.Add(c);

            float top = 0.99f, h = 0.29f, gap = 0.01f;
            for (int i = 0; i < players.Count; i++)
            {
                float yMax = top - i * (h + gap);
                BuildCard(canvas, players[i], yMax - h, yMax);
            }
        }

        private void BuildCard(Transform canvas, MarbleController ctrl, float yMin, float yMax)
        {
            var panel = UIFactory.Panel(canvas, new Vector2(0.785f, yMin), new Vector2(0.995f, yMax),
                Vector2.zero, Vector2.zero, new Color(0.07f, 0.08f, 0.12f, 0.92f));
            // Faixa superior com a cor da equipe.
            var headRt = UIFactory.Panel(panel, new Vector2(0f, 0.86f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, ctrl.Runtime.TeamPrimary);

            var card = new PlayerCard { ctrl = ctrl, nextGrip = ctrl.Runtime.grip.gripId };

            card.title = UIFactory.Label(headRt, "", 22, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0f), new Vector2(1f, 1f), Color.white);
            card.title.fontStyle = FontStyle.Bold;

            card.tyre = UIFactory.Label(panel, "", 16, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.74f), new Vector2(0.55f, 0.85f), new Color(0.9f, 0.9f, 1f));
            card.mode = UIFactory.Label(panel, "", 16, TextAnchor.MiddleRight,
                new Vector2(0.5f, 0.74f), new Vector2(0.96f, 0.85f), new Color(0.9f, 1f, 0.9f));

            // Barras: desgaste, energia e combustivel (PRD 7).
            card.wearLabel = UIFactory.Label(panel, "Wear", 14, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.61f), new Vector2(0.3f, 0.73f), Color.white);
            card.wearFill = BuildBar(panel, 0.61f, 0.73f, new Color(0.9f, 0.35f, 0.3f));

            card.energyLabel = UIFactory.Label(panel, "Energy", 14, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.48f), new Vector2(0.3f, 0.60f), Color.white);
            card.energyFill = BuildBar(panel, 0.48f, 0.60f, new Color(0.3f, 0.8f, 0.95f));

            card.fuelLabel = UIFactory.Label(panel, "Fuel", 14, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.35f), new Vector2(0.3f, 0.47f), Color.white);
            card.fuelFill = BuildBar(panel, 0.35f, 0.47f, new Color(0.4f, 0.85f, 0.4f));

            card.status = UIFactory.Label(panel, "", 15, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.22f), new Vector2(0.96f, 0.33f), new Color(0.85f, 0.85f, 0.9f));

            // Botoes.
            card.pitBtn = UIFactory.Button(panel, "PIT", new Color(0.85f, 0.35f, 0.2f),
                new Vector2(0.04f, 0.03f), new Vector2(0.34f, 0.2f), Vector2.zero, Vector2.zero);
            card.pitLabel = card.pitBtn.GetComponentInChildren<Text>();
            var capturedCard = card;
            card.pitBtn.onClick.AddListener(() =>
                _race.RequestPit(capturedCard.ctrl, capturedCard.nextGrip, true, 60f));

            var modeBtn = UIFactory.Button(panel, "MODE", new Color(0.2f, 0.55f, 0.85f),
                new Vector2(0.36f, 0.03f), new Vector2(0.66f, 0.2f), Vector2.zero, Vector2.zero);
            modeBtn.onClick.AddListener(() => ToggleSelector(capturedCard, true));

            var tyreBtn = UIFactory.Button(panel, "TYRE", new Color(0.55f, 0.35f, 0.8f),
                new Vector2(0.68f, 0.03f), new Vector2(0.96f, 0.2f), Vector2.zero, Vector2.zero);
            tyreBtn.onClick.AddListener(() => ToggleSelector(capturedCard, false));

            BuildModeSelector(panel, card);
            BuildTyreSelector(panel, card);

            _cards.Add(card);
        }

        private RectTransform BuildBar(Transform parent, float yMin, float yMax, Color fillColor)
        {
            var bg = UIFactory.Panel(parent, new Vector2(0.3f, yMin), new Vector2(0.96f, yMax),
                Vector2.zero, Vector2.zero, new Color(0.15f, 0.15f, 0.18f, 1f));
            var fill = UIFactory.Panel(bg, new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, fillColor);
            return fill;
        }

        private void BuildModeSelector(RectTransform card, PlayerCard pc)
        {
            var sel = UIFactory.Panel(card, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.62f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.02f, 0.04f, 0.97f));
            pc.modeSelector = sel.gameObject;
            UIFactory.Label(sel, "Modo", 16, TextAnchor.UpperCenter,
                new Vector2(0f, 0.78f), new Vector2(1f, 0.98f), Color.white);
            AddSelOption(sel, "Normal", 0, 3, () => ApplyMode(pc, RaceMode.Normal), new Color(0.3f, 0.5f, 0.7f));
            AddSelOption(sel, "Push", 1, 3, () => ApplyMode(pc, RaceMode.Push), new Color(0.85f, 0.45f, 0.2f));
            AddSelOption(sel, "Save", 2, 3, () => ApplyMode(pc, RaceMode.Save), new Color(0.25f, 0.7f, 0.45f));
            sel.gameObject.SetActive(false);
        }

        private void BuildTyreSelector(RectTransform card, PlayerCard pc)
        {
            var sel = UIFactory.Panel(card, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.62f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.02f, 0.04f, 0.97f));
            pc.tyreSelector = sel.gameObject;
            UIFactory.Label(sel, "Pneu p/ proximo pit", 15, TextAnchor.UpperCenter,
                new Vector2(0f, 0.78f), new Vector2(1f, 0.98f), Color.white);
            AddSelOption(sel, "S", 0, 5, () => ApplyTyre(pc, GripType.Soft), new Color(0.9f, 0.2f, 0.2f));
            AddSelOption(sel, "M", 1, 5, () => ApplyTyre(pc, GripType.Medium), new Color(0.95f, 0.8f, 0.2f));
            AddSelOption(sel, "H", 2, 5, () => ApplyTyre(pc, GripType.Hard), new Color(0.9f, 0.9f, 0.9f));
            AddSelOption(sel, "I", 3, 5, () => ApplyTyre(pc, GripType.Intermediate), new Color(0.3f, 0.8f, 0.35f));
            AddSelOption(sel, "W", 4, 5, () => ApplyTyre(pc, GripType.Rain), new Color(0.3f, 0.55f, 0.95f));
            sel.gameObject.SetActive(false);
        }

        private void AddSelOption(RectTransform parent, string label, int col, int cols,
            UnityEngine.Events.UnityAction onClick, Color color)
        {
            float w = 0.96f / cols;
            float xMin = 0.02f + col * w;
            var btn = UIFactory.Button(parent, label, color,
                new Vector2(xMin, 0.1f), new Vector2(xMin + w - 0.01f, 0.72f), Vector2.zero, Vector2.zero);
            btn.onClick.AddListener(onClick);
        }

        private void ToggleSelector(PlayerCard pc, bool modeSel)
        {
            if (pc.modeSelector != null) pc.modeSelector.SetActive(modeSel && !pc.modeSelector.activeSelf);
            if (pc.tyreSelector != null) pc.tyreSelector.SetActive(!modeSel && !pc.tyreSelector.activeSelf);
        }

        private void ApplyMode(PlayerCard pc, RaceMode m)
        {
            _race.SetMode(pc.ctrl, m);
            if (pc.modeSelector != null) pc.modeSelector.SetActive(false);
        }

        private void ApplyTyre(PlayerCard pc, GripType g)
        {
            pc.nextGrip = g;
            PushLog($"🔧 {pc.ctrl.Runtime.DisplayName}: pneu p/ pit -> {g}.");
            if (pc.tyreSelector != null) pc.tyreSelector.SetActive(false);
        }

        // ================= UPDATE =================

        private void Update()
        {
            if (_race == null || _race.Track == null) return;

            int leaderLap = 1;
            if (_race.Field.Count > 0)
                leaderLap = Mathf.Clamp(_race.Field[0].Runtime.completedLaps + 1, 1, _race.TotalLaps);
            _topCircuit.text = _race.Config.track.trackName;
            _topLap.text = $"VOLTA {leaderLap}/{_race.TotalLaps}";
            _topWeather.text = $"Clima: {_race.WeatherLabelCurrent()}";
            _safetyBadge.gameObject.SetActive(_race.SafetyMarbleActive);

            UpdateTimingTower();
            UpdateCards();
            UpdateLog();
            UpdateMinimap();
        }

        private void UpdateTimingTower()
        {
            var field = _race.Field;
            _snapshotTimer -= Time.deltaTime;
            bool refresh = _snapshotTimer <= 0f;
            if (refresh) _snapshotTimer = 1.5f;

            for (int i = 0; i < _rows.Count && i < field.Count; i++)
            {
                var m = field[i].Runtime;
                var row = _rows[i];

                row.pos.text = (i + 1).ToString();
                row.code.text = m.driver != null ? m.driver.shortCode : "MAR";
                row.accent.color = m.TeamPrimary;
                row.chip.color = m.TeamPrimary;
                row.number.text = m.driver != null ? m.driver.number.ToString() : "";
                row.number.color = m.TeamSecondary;
                row.gap.text = i == 0 ? "Leader" : $"+{m.gapToLeader:0.000}";
                row.grip.text = m.grip != null ? m.grip.DisplayLetter : "?";
                row.grip.color = m.grip != null ? m.grip.DisplayColor : Color.gray;
                // Coluna de pit: contador, ou icone quando esta no pit.
                bool inPit = m.state == MarbleRaceState.InPit || m.state == MarbleRaceState.EnteringPit
                          || m.state == MarbleRaceState.ExitingPit;
                row.pit.text = inPit ? "P" : m.pitStops.ToString();
                row.pit.color = inPit ? new Color(1f, 0.65f, 0.2f) : new Color(0.7f, 0.78f, 0.9f);
                // Lider em dourado.
                row.code.color = i == 0 ? new Color(1f, 0.88f, 0.35f) : Color.white;

                int prev = _posSnapshot.TryGetValue(m, out var p) ? p : (i + 1);
                if (i + 1 < prev) { row.arrow.text = "▲"; row.arrow.color = new Color(0.3f, 0.9f, 0.4f); }
                else if (i + 1 > prev) { row.arrow.text = "▼"; row.arrow.color = new Color(0.9f, 0.3f, 0.3f); }
                else row.arrow.text = "";
                if (refresh) _posSnapshot[m] = i + 1;

                // Destaque do jogador: fundo + glow dourado.
                if (m.isPlayer)
                {
                    row.bg.color = new Color(0.2f, 0.17f, 0.05f, 0.96f);
                    row.glow.effectColor = new Color(1f, 0.85f, 0.2f, 0.9f);
                }
                else
                {
                    row.bg.color = (i % 2 == 0) ? new Color(0.11f, 0.11f, 0.15f, 0.96f)
                                                : new Color(0.08f, 0.08f, 0.12f, 0.96f);
                    row.glow.effectColor = new Color(0f, 0f, 0f, 0f);
                }
            }
        }

        private void UpdateCards()
        {
            foreach (var card in _cards)
            {
                var m = card.ctrl.Runtime;
                card.title.text = $"{m.DisplayName}   P{m.position}";
                card.tyre.text = $"Pneu: {m.grip.gripId}  (pit: {card.nextGrip})";
                card.mode.text = $"Modo: {m.mode}";

                SetBar(card.wearFill, m.wear / 100f);
                card.wearFill.GetComponent<Image>().color = m.wear > 70f
                    ? new Color(1f, 0.4f, 0.3f) : new Color(0.85f, 0.55f, 0.3f);
                card.wearLabel.text = $"Wear {m.wear:0}%";

                SetBar(card.energyFill, m.energy / 100f);
                card.energyFill.GetComponent<Image>().color = m.energy < 20f
                    ? new Color(1f, 0.85f, 0.25f) : new Color(0.3f, 0.8f, 0.95f);
                card.energyLabel.text = $"Energy {m.energy:0}";

                // Combustivel: verde > amarelo > vermelho (PRD 7).
                SetBar(card.fuelFill, m.fuel / 100f);
                Color fuelColor = m.fuel <= 0f ? new Color(1f, 0.2f, 0.2f)
                    : m.fuel < 10f ? new Color(1f, 0.35f, 0.25f)
                    : m.fuel < 25f ? new Color(1f, 0.85f, 0.3f) : new Color(0.4f, 0.85f, 0.4f);
                card.fuelFill.GetComponent<Image>().color = fuelColor;
                card.fuelLabel.text = m.FuelEmpty ? "FUEL EMPTY" : $"Fuel {m.fuel:0}";
                card.fuelLabel.color = m.fuel < 25f ? new Color(1f, 0.8f, 0.3f) : Color.white;

                // Status + alertas.
                string alert = "";
                if (m.FuelEmpty) alert = "  ⛽ sem combustivel";
                else if (m.wear > 70f) alert = "  ⚠ desgaste";
                else if (m.energy < 20f) alert = "  ⚠ energia";
                card.status.text = $"{StatusText(m.state)}{alert}   |   Pits: {m.pitStops}";

                // Estado do botao PIT.
                bool pitting = m.state == MarbleRaceState.EnteringPit
                            || m.state == MarbleRaceState.InPit
                            || m.state == MarbleRaceState.ExitingPit;
                if (pitting) { card.pitLabel.text = "IN PIT"; card.pitBtn.interactable = false; }
                else if (m.pitRequested) { card.pitLabel.text = "QUEUED"; card.pitBtn.interactable = false; }
                else { card.pitLabel.text = "PIT"; card.pitBtn.interactable = m.state == MarbleRaceState.Racing; }
            }
        }

        private static void SetBar(RectTransform fill, float t)
        {
            t = Mathf.Clamp01(t);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(t, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        private static string StatusText(MarbleRaceState s)
        {
            switch (s)
            {
                case MarbleRaceState.OnGrid: return "No grid";
                case MarbleRaceState.Racing: return "Em pista";
                case MarbleRaceState.EnteringPit: return "Entrando no pit";
                case MarbleRaceState.InPit: return "No pit";
                case MarbleRaceState.ExitingPit: return "Saindo do pit";
                case MarbleRaceState.Finished: return "Terminou";
                default: return s.ToString();
            }
        }

        private void UpdateMinimap()
        {
            foreach (var (rt, c) in _mapDots)
                PlaceNorm(rt, Norm(c.transform.position));
        }

        // ---- Log com cores + fade ----

        private void OnCountdown(int v) => _countdownText.text = v <= 0 ? "GO!" : v.ToString();

        private void PushLog(string msg)
        {
            _logItems.Add(new LogItem { msg = msg, color = ClassifyLog(msg), age = 0f });
            if (_logItems.Count > LogRows + 3) _logItems.RemoveAt(0);
        }

        private void UpdateLog()
        {
            for (int i = _logItems.Count - 1; i >= 0; i--)
            {
                var it = _logItems[i];
                it.age += Time.deltaTime;
                _logItems[i] = it;
                if (it.age > LogFadeEnd) _logItems.RemoveAt(i);
            }

            // Mostra os ultimos N de baixo para cima.
            int shown = Mathf.Min(LogRows, _logItems.Count);
            for (int r = 0; r < LogRows; r++)
            {
                var row = _logRows[r];
                int idx = _logItems.Count - shown + r;
                if (idx < 0 || idx >= _logItems.Count) { row.text = ""; continue; }
                var it = _logItems[idx];
                float alpha = it.age < LogFadeStart ? 1f
                    : Mathf.InverseLerp(LogFadeEnd, LogFadeStart, it.age);
                var c = it.color; c.a = Mathf.Clamp01(alpha);
                row.text = it.msg;
                row.color = c;
            }
        }

        private static Color ClassifyLog(string msg)
        {
            if (msg.Contains("ultrapassou")) return new Color(0.4f, 0.95f, 0.55f);
            if (msg.Contains("Safety Marble na")) return new Color(1f, 0.45f, 0.3f);
            if (msg.Contains("recolhido") || msg.Contains("limpa")) return new Color(0.5f, 0.95f, 0.6f);
            if (msg.Contains("venceu") || msg.Contains("🏆") || msg.Contains("🏁")) return new Color(1f, 0.9f, 0.4f);
            if (msg.Contains("pit") || msg.Contains("Pit") || msg.Contains("🔧") || msg.Contains("📞"))
                return new Color(1f, 0.65f, 0.25f);
            if (msg.Contains("Clima") || msg.Contains("🌦")) return new Color(0.55f, 0.8f, 1f);
            if (msg.Contains("modo") || msg.Contains("⚙")) return new Color(0.55f, 0.75f, 1f);
            if (msg.Contains("⚠") || msg.Contains("suja")) return new Color(1f, 0.85f, 0.3f);
            return Color.white;
        }

        private void OnFinished(RaceResult result) => _countdownText.text = "FIM";
    }
}
