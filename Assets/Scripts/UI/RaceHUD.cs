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
        private Transform _canvasT;

        private Text _topCircuit, _topLap, _topWeather, _countdownText, _camLabel;
        private RectTransform _safetyBadge;

        // ---- Timing tower ----
        private class RankingRow
        {
            public Image bg, accent, chip, logo, gripRing;
            public Outline glow;
            public Text pos, arrow, number, code, gap, gripLetter, pit;
            public float flash; // >0 subiu (verde), <0 caiu (vermelho)
        }
        private readonly List<RankingRow> _rows = new();
        private readonly Dictionary<MarbleRuntime, int> _posSnapshot = new();
        private readonly Dictionary<MarbleRuntime, int> _framePos = new();
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
            public Text wearVal, energyVal, fuelVal;
            public RectTransform wearFill, energyFill, fuelFill;
            public Button pitBtn;
            public Outline pitGlow;
            public GameObject modeSelector, tyreSelector;
            public RectTransform panel;        // painel do card (re-ancorado ao recolher)
            public GameObject content;         // tudo que some no modo recolhido
            public Text mini;                  // resumo vertical no modo recolhido
            public float yMin, yMax;           // ancoras verticais originais
            public readonly List<GameObject> detail = new(); // reservado p/ recolher futuro
        }
        private readonly List<PlayerCard> _cards = new();
        private Text _cardsToggleLabel;
        private bool _cardsCollapsed;

        // ---- Log ----
        private struct LogItem { public string msg; public Color color; public float age; }
        private readonly List<LogItem> _logItems = new();
        private Text[] _logRows;
        private RectTransform _logPanel;
        private Text _logToggleLabel;
        private bool _logCollapsed;
        private const int LogRows = 5;
        private const float LogFadeStart = 4.5f, LogFadeEnd = 7f;

        // ---- Banner de broadcast (eventos importantes) ----
        private RectTransform _banner;
        private CanvasGroup _bannerCg;
        private Text _bannerText;
        private Outline _bannerGlow;
        private float _bannerTimer;
        private string _lastWeather;
        private bool _safetyWas, _lastLapBanner;
        private const float BannerTotal = 2.8f;

        // ---- Rádio do box (decisões-relâmpago) ----
        private RectTransform _hudRoot;     // raiz (safe area) para sobreposicoes
        private GameObject _radioCard;
        private RectTransform _radioBar;
        private RadioDecision _radioDecision;
        private float _radioTimer;

        public void Bind(RaceManager race, CameraController cam)
        {
            _race = race;
            _camera = cam;
            BuildUI();

            _race.OnCountdown += OnCountdown;
            _race.OnRaceStarted += () => _countdownText.text = "";
            _race.OnRaceEvent += PushLog;
            _race.OnRaceFinished += OnFinished;
            _race.OnRadioDecision += ShowRadio;
        }

        // ================= BUILD =================

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("RaceHUD");
            canvas.transform.SetParent(transform, false);
            _canvasT = canvas.transform;

            // Conteudo da HUD vive dentro da safe area (notch / home indicator);
            // o countdown e o tutorial modal cobrem a tela inteira.
            var safe = UIFactory.SafeAreaRoot(canvas.transform);
            _hudRoot = safe;

            BuildTopBar(safe);
            BuildTimingTower(safe);
            BuildLog(safe);
            BuildPlayerCards(safe);
            BuildBanner(safe);

            _countdownText = UIFactory.Label(canvas.transform, "", 130, TextAnchor.MiddleCenter,
                new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.75f), new Color(1f, 0.92f, 0.3f));
            _countdownText.fontStyle = FontStyle.Bold;

            if (!_raceTutorialSeen) ShowRaceTutorial(); // mini tutorial na 1a corrida da sessao
        }

        // ---- Banner de broadcast ----

        private void BuildBanner(Transform canvas)
        {
            var go = new GameObject("Banner", typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(canvas, false);
            _banner = go.GetComponent<RectTransform>();
            _banner.anchorMin = new Vector2(0.33f, 0.78f);
            _banner.anchorMax = new Vector2(0.67f, 0.865f);
            _banner.offsetMin = Vector2.zero; _banner.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color32(6, 14, 26, 242);
            if (UIFactory.RoundedSprite != null) { img.sprite = UIFactory.RoundedSprite; img.type = Image.Type.Sliced; }
            img.raycastTarget = false;
            _bannerGlow = go.AddComponent<Outline>();
            _bannerGlow.effectColor = MarbleUITheme.NeonCyan;
            _bannerGlow.effectDistance = new Vector2(2.5f, 2.5f);
            _bannerCg = go.GetComponent<CanvasGroup>();
            _bannerCg.alpha = 0f;
            _bannerCg.blocksRaycasts = false;
            _bannerText = UIFactory.Label(_banner, "", 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);
            _bannerText.fontStyle = FontStyle.Bold;
        }

        private void ShowBanner(string text, Color color)
        {
            if (_banner == null) return;
            _bannerText.text = text;
            _bannerText.color = color;
            _bannerGlow.effectColor = new Color(color.r, color.g, color.b, 0.9f);
            _bannerTimer = BannerTotal;
        }

        private void UpdateBanner()
        {
            // Deteccao de eventos importantes.
            string w = _race.WeatherLabelCurrent();
            if (!string.IsNullOrEmpty(_lastWeather) && w != _lastWeather)
                ShowBanner($"CLIMA: {w.ToUpper()}", MarbleUITheme.NeonBlue);
            _lastWeather = w;

            bool sm = _race.SafetyMarbleActive;
            if (sm && !_safetyWas) ShowBanner("SAFETY MARBLE", MarbleUITheme.Warning);
            _safetyWas = sm;

            int leaderLap = _race.Field.Count > 0 ? _race.Field[0].Runtime.completedLaps + 1 : 1;
            if (!_lastLapBanner && _race.TotalLaps > 1 && leaderLap >= _race.TotalLaps)
            { ShowBanner("ÚLTIMA VOLTA", MarbleUITheme.NeonGold); _lastLapBanner = true; }

            // Animacao (fade + leve pop).
            if (_bannerTimer > 0f && _banner != null)
            {
                _bannerTimer -= Time.deltaTime;
                float elapsed = BannerTotal - _bannerTimer;
                float a = elapsed < 0.25f ? elapsed / 0.25f
                        : _bannerTimer < 0.45f ? _bannerTimer / 0.45f : 1f;
                _bannerCg.alpha = Mathf.Clamp01(a);
                _banner.localScale = Vector3.one * Mathf.Min(1f, 0.86f + elapsed * 0.9f);
                if (_bannerTimer <= 0f) _bannerCg.alpha = 0f;
            }
        }

        // ---- Mini tutorial (PRD) ----

        private static bool _raceTutorialSeen;
        private GameObject _tutorialPanel;

        private void ShowRaceTutorial()
        {
            if (_tutorialPanel != null || _canvasT == null) return;
            _raceTutorialSeen = true;

            var overlay = UIFactory.Panel(_canvasT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.82f));
            overlay.SetAsLastSibling();
            _tutorialPanel = overlay.gameObject;
            var safe = UIFactory.SafeAreaRoot(overlay);

            var card = UIFactory.Panel(safe, new Vector2(0.27f, 0.16f), new Vector2(0.73f, 0.84f),
                Vector2.zero, Vector2.zero, UITheme.CardPanel);
            var glow = card.gameObject.AddComponent<Outline>();
            glow.effectColor = UITheme.Neon; glow.effectDistance = new Vector2(2f, 2f);

            UIFactory.Label(card, "COMO JOGAR", 34, TextAnchor.UpperCenter,
                new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f), Color.white).fontStyle = FontStyle.Bold;

            string tips =
                "Você é o ESTRATEGISTA — as bolinhas correm sozinhas.\n\n" +
                "•  CLASSIFICAÇÃO (esquerda): posições ao vivo, gap e pneu. Recolha com «.\n" +
                "•  Cards (direita): suas bolinhas, com PIT / MODO / PNEU. Recolha com ▶.\n" +
                "     –  PIT: chama a parada (troca pneu + reabastece).\n" +
                "     –  MODO: Save (poupa) · Normal · Push (mais rápido, arrisca).\n" +
                "     –  PNEU: escolhe o próximo pneu do pit.\n" +
                "•  O combustível NÃO chega ao fim: ao menos 1 pit é obrigatório.\n" +
                "•  Desgaste e chuva mudam o ritmo — o pneu certo importa!\n" +
                "•  Log de eventos da corrida (embaixo).\n" +
                "•  Botão Pausa (no topo) para pausar ou sair da corrida.";
            UIFactory.Label(card, tips, 18, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.17f), new Vector2(0.95f, 0.83f), UITheme.TextDim);

            var ok = UIFactory.Button(card, "Entendi!", UITheme.PrimaryButton,
                new Vector2(0.38f, 0.05f), new Vector2(0.62f, 0.14f), Vector2.zero, Vector2.zero);
            ok.onClick.AddListener(() =>
            {
                if (_tutorialPanel != null) { Destroy(_tutorialPanel); _tutorialPanel = null; }
            });
        }

        private void BuildTopBar(Transform canvas)
        {
            var bar = UIFactory.Panel(canvas, new Vector2(0.24f, 0.93f), new Vector2(0.78f, 1f),
                Vector2.zero, Vector2.zero, MarbleUITheme.PanelDark);
            UIFactory.NeonBorder(bar.gameObject, MarbleUITheme.NeonCyan, 0.5f, 1.6f);

            _topCircuit = UIFactory.Label(bar, "", 24, TextAnchor.MiddleLeft,
                new Vector2(0.03f, 0f), new Vector2(0.42f, 1f), Color.white);
            _topCircuit.fontStyle = FontStyle.Bold;

            _topLap = UIFactory.Label(bar, "", 26, TextAnchor.MiddleCenter,
                new Vector2(0.42f, 0f), new Vector2(0.62f, 1f), new Color(1f, 0.95f, 0.6f));
            _topLap.fontStyle = FontStyle.Bold;

            _topWeather = UIFactory.Label(bar, "", 22, TextAnchor.MiddleRight,
                new Vector2(0.58f, 0f), new Vector2(0.78f, 1f), new Color(0.7f, 0.85f, 1f));

            // Pausa + ajuda (tutorial) no proprio top bar.
            var pause = UIFactory.Button(bar, "Pausa", new Color(0.30f, 0.33f, 0.45f, 0.95f),
                new Vector2(0.795f, 0.16f), new Vector2(0.89f, 0.84f), Vector2.zero, Vector2.zero);
            pause.GetComponentInChildren<Text>().fontSize = 16;
            pause.onClick.AddListener(() => _race.PauseRequested?.Invoke());

            var help = UIFactory.Button(bar, "?", new Color(0.22f, 0.45f, 0.85f, 0.95f),
                new Vector2(0.9f, 0.16f), new Vector2(0.985f, 0.84f), Vector2.zero, Vector2.zero);
            help.onClick.AddListener(ShowRaceTutorial);

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
            // Compacto: ~290px de largura (era ~410px), para liberar a pista.
            var container = UIFactory.Panel(canvas, new Vector2(0.008f, 0.14f), new Vector2(0.158f, 0.99f),
                Vector2.zero, Vector2.zero, new Color32(3, 9, 18, 255)); // opaco: pista nao vaza
            UIFactory.NeonBorder(container.gameObject, MarbleUITheme.NeonCyan, 0.35f, 1.4f);
            _towerContainer = container;

            const float headerH = 42f;
            var header = UIFactory.Panel(container, new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, MarbleUITheme.PanelSoft);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, headerH);
            header.anchoredPosition = Vector2.zero;
            var htxt = UIFactory.Label(header, "CLASSIFICAÇÃO", 15, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0f), new Vector2(0.78f, 1f), new Color(0.8f, 0.85f, 1f));
            htxt.fontStyle = FontStyle.Bold;

            // Botao recolher/expandir (PRD 4.1).
            var toggle = UIFactory.Button(header, "«", new Color(0.2f, 0.25f, 0.4f, 0.95f),
                new Vector2(0.78f, 0.15f), new Vector2(0.97f, 0.85f), Vector2.zero, Vector2.zero);
            _towerToggleLabel = toggle.GetComponentInChildren<Text>();
            toggle.GetComponentInChildren<Text>().fontSize = 14;
            toggle.onClick.AddListener(ToggleTower);

            float rowH = Mathf.Clamp(840f / Mathf.Max(1, count), 28f, 48f);
            for (int i = 0; i < count; i++)
                _rows.Add(CreateRow(container, i, rowH, headerH));
        }

        private void ToggleTower()
        {
            _towerCollapsed = !_towerCollapsed;
            _towerToggleLabel.text = _towerCollapsed ? "»" : "«";
            // Recolhido: estreita o painel e mostra so posicao + chip + sigla.
            _towerContainer.anchorMax = new Vector2(_towerCollapsed ? 0.072f : 0.158f, 0.99f);
            foreach (var row in _rows)
            {
                bool show = !_towerCollapsed;
                row.arrow.gameObject.SetActive(show);
                row.gap.gameObject.SetActive(show);
                row.gripRing.gameObject.SetActive(show);
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
            if (UIFactory.RoundedSprite != null) { bg.sprite = UIFactory.RoundedSprite; bg.type = Image.Type.Sliced; }
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

            // Faixa de cor da equipe (esquerda) — retangulo limpo (sem cantos
            // arredondados, para nao virar "pilula" oval).
            var accentGo = new GameObject("Accent", typeof(Image));
            accentGo.transform.SetParent(rowGo.transform, false);
            row.accent = accentGo.GetComponent<Image>();
            row.accent.raycastTarget = false;
            var accentRt = accentGo.GetComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0f, 0.08f);
            accentRt.anchorMax = new Vector2(0.022f, 0.92f);
            accentRt.offsetMin = Vector2.zero; accentRt.offsetMax = Vector2.zero;

            row.pos = UIFactory.Label(rowGo.transform, "", 18, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0f), new Vector2(0.15f, 1f), Color.white);
            row.pos.fontStyle = FontStyle.Bold;

            row.arrow = UIFactory.Label(rowGo.transform, "", 13, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0f), new Vector2(0.21f, 1f), Color.white);

            var chipRt = UIFactory.Panel(rowGo.transform, new Vector2(0.22f, 0.2f), new Vector2(0.30f, 0.8f),
                Vector2.zero, Vector2.zero, Color.gray);
            row.chip = chipRt.GetComponent<Image>();
            row.number = UIFactory.Label(chipRt, "", 12, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Color.white);
            row.number.fontStyle = FontStyle.Bold;

            // Logo da equipe sobre o chip (preenchido dinamicamente; some se nao existir).
            var logoGo = new GameObject("Logo", typeof(Image));
            logoGo.transform.SetParent(chipRt, false);
            row.logo = logoGo.GetComponent<Image>();
            row.logo.raycastTarget = false;
            row.logo.preserveAspect = true;
            row.logo.enabled = false;
            var logoRt = logoGo.GetComponent<RectTransform>();
            logoRt.anchorMin = new Vector2(0.05f, 0.05f);
            logoRt.anchorMax = new Vector2(0.95f, 0.95f);
            logoRt.offsetMin = Vector2.zero; logoRt.offsetMax = Vector2.zero;

            row.code = UIFactory.Label(rowGo.transform, "", 17, TextAnchor.MiddleLeft,
                new Vector2(0.32f, 0f), new Vector2(0.49f, 1f), Color.white);
            row.code.fontStyle = FontStyle.Bold;

            row.gap = UIFactory.Label(rowGo.transform, "", 13, TextAnchor.MiddleRight,
                new Vector2(0.49f, 0f), new Vector2(0.73f, 1f), new Color(0.85f, 0.85f, 0.9f));

            // Badge de pneu (anel colorido + letra), estilo transmissao.
            // Badge QUADRADO (anchor central + sizeDelta), senao a linha larga
            // deixava o circulo oval.
            var tb = UIFactory.TyreBadge(rowGo.transform, new Vector2(0.81f, 0.5f), new Vector2(0.81f, 0.5f));
            float badge = Mathf.Min(rowH * 0.55f, 22f);
            tb.ring.rectTransform.sizeDelta = new Vector2(badge, badge);
            row.gripRing = tb.ring;
            row.gripLetter = tb.letter;

            // Pit stops (PRD 4.2).
            row.pit = UIFactory.Label(rowGo.transform, "", 13, TextAnchor.MiddleCenter,
                new Vector2(0.88f, 0f), new Vector2(0.99f, 1f), new Color(0.7f, 0.78f, 0.9f));

            return row;
        }

        // ---- Log ----

        private void BuildLog(Transform canvas)
        {
            _logPanel = UIFactory.Panel(canvas, new Vector2(0.24f, 0f), new Vector2(0.78f, 0.15f),
                Vector2.zero, Vector2.zero, MarbleUITheme.PanelDark);
            UIFactory.NeonBorder(_logPanel.gameObject, MarbleUITheme.NeonCyan, 0.4f, 1.4f);

            // Cabecalho do log.
            var hdr = UIFactory.Panel(_logPanel, new Vector2(0f, 0.8f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, UITheme.HeaderPanel);
            UIFactory.Label(hdr, "EVENTOS DA CORRIDA", 15, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0f), new Vector2(0.8f, 1f), UITheme.Neon).fontStyle = FontStyle.Bold;
            var toggle = UIFactory.Button(hdr, "▼", new Color(0.22f, 0.27f, 0.42f, 0.95f),
                new Vector2(0.955f, 0.12f), new Vector2(0.992f, 0.88f), Vector2.zero, Vector2.zero);
            _logToggleLabel = toggle.GetComponentInChildren<Text>();
            _logToggleLabel.fontSize = 14;
            toggle.onClick.AddListener(ToggleLog);

            _logRows = new Text[LogRows];
            for (int i = 0; i < LogRows; i++)
            {
                float yMin = 0.02f + i * 0.152f;
                _logRows[i] = UIFactory.Label(_logPanel, "", 16, TextAnchor.LowerLeft,
                    new Vector2(0.025f, yMin), new Vector2(0.97f, yMin + 0.152f), Color.white);
            }
        }

        private void ToggleLog()
        {
            _logCollapsed = !_logCollapsed;
            _logToggleLabel.text = _logCollapsed ? "▲" : "▼";
            foreach (var r in _logRows) r.gameObject.SetActive(!_logCollapsed);
            // Recolhe PRA BAIXO: o topo do painel desce, o cabecalho fica no rodape.
            _logPanel.anchorMin = new Vector2(0.24f, 0f);
            _logPanel.anchorMax = new Vector2(0.78f, _logCollapsed ? 0.035f : 0.15f);
        }

        // ---- Cards da equipe ----

        private void BuildPlayerCards(Transform canvas)
        {
            var players = new List<MarbleController>();
            foreach (var c in _race.Field) if (c.Runtime.isPlayer) players.Add(c);

            float top = 0.99f, h = 0.29f, gap = 0.012f;
            for (int i = 0; i < players.Count; i++)
            {
                float yMax = top - i * (h + gap);
                BuildCard(canvas, players[i], yMax - h, yMax);
            }

            // Botao recolher/expandir o painel direito (fica no vao, sempre visivel,
            // abaixo da top bar e a esquerda dos cards).
            var toggle = UIFactory.Button(canvas, "▶", new Color(0.2f, 0.25f, 0.4f, 0.95f),
                new Vector2(0.754f, 0.86f), new Vector2(0.783f, 0.905f), Vector2.zero, Vector2.zero);
            _cardsToggleLabel = toggle.GetComponentInChildren<Text>();
            _cardsToggleLabel.fontSize = 16;
            toggle.onClick.AddListener(ToggleCards);
        }

        private void ToggleCards()
        {
            _cardsCollapsed = !_cardsCollapsed;
            _cardsToggleLabel.text = _cardsCollapsed ? "◀" : "▶";
            foreach (var card in _cards)
            {
                var p = card.panel;
                if (_cardsCollapsed)
                {
                    p.anchorMin = new Vector2(0.963f, card.yMin);
                    p.anchorMax = new Vector2(0.998f, card.yMax);
                }
                else
                {
                    p.anchorMin = new Vector2(0.785f, card.yMin);
                    p.anchorMax = new Vector2(0.995f, card.yMax);
                }
                p.offsetMin = Vector2.zero; p.offsetMax = Vector2.zero;
                if (card.content != null) card.content.SetActive(!_cardsCollapsed);
                if (card.mini != null) card.mini.gameObject.SetActive(_cardsCollapsed);
            }
        }

        private void BuildCard(Transform canvas, MarbleController ctrl, float yMin, float yMax)
        {
            var panel = UIFactory.Panel(canvas, new Vector2(0.785f, yMin), new Vector2(0.995f, yMax),
                Vector2.zero, Vector2.zero, MarbleUITheme.PanelDark);
            UIFactory.NeonBorder(panel.gameObject, ctrl.Runtime.TeamPrimary, 0.7f, 1.8f);

            // Container com todo o conteudo detalhado (some no modo recolhido).
            var content = UIFactory.Panel(panel, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            content.GetComponent<Image>().raycastTarget = false;

            // Header escuro com faixa fina da cor da equipe a esquerda (texto sempre legivel).
            var headRt = UIFactory.Panel(content, new Vector2(0f, 0.86f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, UITheme.HeaderPanel);
            var sideAcc = UIFactory.Panel(headRt, new Vector2(0f, 0.12f), new Vector2(0.022f, 0.88f),
                Vector2.zero, Vector2.zero, ctrl.Runtime.TeamPrimary);
            sideAcc.GetComponent<Image>().raycastTarget = false;

            var card = new PlayerCard { ctrl = ctrl, panel = panel, content = content.gameObject,
                yMin = yMin, yMax = yMax,
                nextGrip = ctrl.Runtime.grip != null ? ctrl.Runtime.grip.gripId : GripType.Medium };

            // Resumo vertical exibido quando o painel esta recolhido.
            card.mini = UIFactory.Label(panel, "", 15, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Color.white);
            card.mini.fontStyle = FontStyle.Bold;
            card.mini.gameObject.SetActive(false);

            card.title = UIFactory.Label(headRt, "", 20, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0f), new Vector2(1f, 1f), Color.white);
            card.title.fontStyle = FontStyle.Bold;

            card.tyre = UIFactory.Label(content, "", 16, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.74f), new Vector2(0.55f, 0.85f), new Color(0.9f, 0.9f, 1f));
            card.mode = UIFactory.Label(content, "", 16, TextAnchor.MiddleRight,
                new Vector2(0.5f, 0.74f), new Vector2(0.96f, 0.85f), new Color(0.9f, 1f, 0.9f));

            // Barras: desgaste, energia e combustivel (PRD 7).
            card.wearLabel = UIFactory.Label(content, "Desgaste", 15, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.61f), new Vector2(0.34f, 0.73f), MarbleUITheme.TextSecondary);
            card.wearFill = BuildBar(content, 0.61f, 0.73f, MarbleUITheme.TyreWear);
            card.wearVal = UIFactory.Label(content, "", 12, TextAnchor.MiddleRight,
                new Vector2(0.37f, 0.61f), new Vector2(0.94f, 0.73f), Color.white);

            card.energyLabel = UIFactory.Label(content, "Energia", 15, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.48f), new Vector2(0.34f, 0.60f), MarbleUITheme.TextSecondary);
            card.energyFill = BuildBar(content, 0.48f, 0.60f, MarbleUITheme.Energy);
            card.energyVal = UIFactory.Label(content, "", 12, TextAnchor.MiddleRight,
                new Vector2(0.37f, 0.48f), new Vector2(0.94f, 0.60f), Color.white);

            card.fuelLabel = UIFactory.Label(content, "Combust.", 15, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.35f), new Vector2(0.34f, 0.47f), MarbleUITheme.TextSecondary);
            card.fuelFill = BuildBar(content, 0.35f, 0.47f, MarbleUITheme.Fuel);
            card.fuelVal = UIFactory.Label(content, "", 12, TextAnchor.MiddleRight,
                new Vector2(0.37f, 0.35f), new Vector2(0.94f, 0.47f), Color.white);

            card.status = UIFactory.Label(content, "", 15, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.22f), new Vector2(0.96f, 0.33f), new Color(0.85f, 0.85f, 0.9f));

            // Botoes de acao (so texto, sem icone para nao sobrepor a palavra).
            card.pitBtn = UIFactory.Button(content, "PIT", UITheme.PrimaryButton,
                new Vector2(0.04f, 0.03f), new Vector2(0.34f, 0.2f), Vector2.zero, Vector2.zero);
            card.pitLabel = card.pitBtn.GetComponentInChildren<Text>();
            card.pitLabel.fontSize = 16;
            card.pitGlow = card.pitBtn.gameObject.AddComponent<Outline>();
            card.pitGlow.effectColor = new Color(1f, 0.7f, 0.2f, 0f);
            card.pitGlow.effectDistance = new Vector2(2.5f, 2.5f);
            var capturedCard = card;
            card.pitBtn.onClick.AddListener(() =>
                _race.RequestPit(capturedCard.ctrl, capturedCard.nextGrip, true, 60f));

            var modeBtn = UIFactory.Button(content, "MODO", UITheme.SecondaryButton,
                new Vector2(0.36f, 0.03f), new Vector2(0.66f, 0.2f), Vector2.zero, Vector2.zero);
            modeBtn.GetComponentInChildren<Text>().fontSize = 16;
            modeBtn.onClick.AddListener(() => ToggleSelector(capturedCard, true));

            var tyreBtn = UIFactory.Button(content, "PNEU", new Color(0.46f, 0.30f, 0.70f),
                new Vector2(0.68f, 0.03f), new Vector2(0.96f, 0.2f), Vector2.zero, Vector2.zero);
            tyreBtn.GetComponentInChildren<Text>().fontSize = 16;
            tyreBtn.onClick.AddListener(() => ToggleSelector(capturedCard, false));

            BuildModeSelector(content, card);
            BuildTyreSelector(content, card);

            // Elementos escondidos no modo recolhido (mantem titulo + barras).
            card.detail.Add(card.pitBtn.gameObject);
            card.detail.Add(modeBtn.gameObject);
            card.detail.Add(tyreBtn.gameObject);
            card.detail.Add(card.status.gameObject);

            _cards.Add(card);
        }

        private RectTransform BuildBar(Transform parent, float yMin, float yMax, Color fillColor)
        {
            var bg = UIFactory.Panel(parent, new Vector2(0.35f, yMin), new Vector2(0.96f, yMax),
                Vector2.zero, Vector2.zero, new Color(0.06f, 0.09f, 0.14f, 1f));
            var fill = UIFactory.SolidPanel(bg, new Vector2(0f, 0f), new Vector2(1f, 1f), fillColor);
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
                leaderLap = Mathf.Clamp(_race.Field[0].Runtime.completedLaps + 1, 1, Mathf.Max(1, _race.TotalLaps));
            _topCircuit.text = _race.Config.track.trackName;
            _topLap.text = $"VOLTA {leaderLap}/{_race.TotalLaps}";
            _topWeather.text = $"Clima: {_race.WeatherLabelCurrent()}";
            _safetyBadge.gameObject.SetActive(_race.SafetyMarbleActive);

            UpdateTimingTower();
            UpdateCards();
            UpdateLog();
            UpdateBanner();
            UpdateRadio();
        }

        // ---- Rádio do box ----

        private static Color RadioColor(RadioTone t)
        {
            switch (t)
            {
                case RadioTone.Attack: return MarbleUITheme.NeonOrange;
                case RadioTone.Defend: return MarbleUITheme.NeonBlue;
                case RadioTone.Save: return MarbleUITheme.NeonGreen;
                default: return new Color(0.34f, 0.38f, 0.48f);
            }
        }

        private void ShowRadio(RadioDecision d)
        {
            if (_hudRoot == null || d == null || d.options.Count == 0) return;
            CloseRadio(); // substitui uma decisao em aberto
            _radioDecision = d;
            _radioTimer = d.duration;

            var card = UIFactory.Panel(_hudRoot, new Vector2(0.31f, 0.56f), new Vector2(0.69f, 0.79f),
                Vector2.zero, Vector2.zero, new Color(0.04f, 0.07f, 0.12f, 0.97f));
            UIFactory.NeonBorder(card.gameObject, MarbleUITheme.NeonCyan, 0.9f, 2.4f);
            _radioCard = card.gameObject;
            _radioCard.AddComponent<UIFadeIn>(); // fade/pop de entrada (auto-adiciona CanvasGroup)

            var hdr = UIFactory.Panel(card, new Vector2(0f, 0.8f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, MarbleUITheme.PanelSoft);
            UIFactory.Label(hdr, "RÁDIO DO BOX", 13, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0f), new Vector2(0.96f, 1f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;
            UIFactory.Label(card, d.prompt, 16, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.78f), Color.white).fontStyle = FontStyle.Bold;

            int n = d.options.Count;
            const float gap = 0.02f, totalW = 0.92f;
            float w = (totalW - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var opt = d.options[i];
                float x0 = 0.04f + i * (w + gap);
                var btn = UIFactory.Button(card, opt.label, RadioColor(opt.tone),
                    new Vector2(x0, 0.14f), new Vector2(x0 + w, 0.46f), Vector2.zero, Vector2.zero);
                btn.GetComponentInChildren<Text>().fontSize = 15;
                var captured = opt;
                btn.onClick.AddListener(() => { _race.ApplyRadio(d.ctrl, captured); CloseRadio(); });
            }

            // Barra de tempo para decidir.
            var barBg = UIFactory.Panel(card, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.09f),
                Vector2.zero, Vector2.zero, new Color(0.06f, 0.09f, 0.14f, 1f));
            _radioBar = UIFactory.SolidPanel(barBg, new Vector2(0f, 0f), new Vector2(1f, 1f), MarbleUITheme.NeonCyan);
            _radioBar.GetComponent<Image>().raycastTarget = false;
        }

        private void UpdateRadio()
        {
            if (_radioCard == null) return;
            _radioTimer -= Time.deltaTime; // pausa junto com o jogo (timeScale 0)
            if (_radioBar != null && _radioDecision != null)
            {
                float f = Mathf.Clamp01(_radioTimer / Mathf.Max(0.01f, _radioDecision.duration));
                _radioBar.anchorMin = new Vector2(0f, 0f);
                _radioBar.anchorMax = new Vector2(f, 1f);
                _radioBar.offsetMin = Vector2.zero; _radioBar.offsetMax = Vector2.zero;
            }
            if (_radioTimer <= 0f) CloseRadio(); // ignorada -> sem efeito
        }

        private void CloseRadio()
        {
            if (_radioCard != null) Destroy(_radioCard);
            _radioCard = null; _radioBar = null; _radioDecision = null;
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

                // Logo da equipe no chip; sem logo, mostra o numero sobre a cor da equipe.
                var logoSp = m.team != null ? UIFactory.LoadSprite("Logos/" + m.team.teamId) : null;
                if (logoSp != null)
                {
                    row.logo.enabled = true;
                    row.logo.sprite = logoSp;
                    row.chip.color = new Color(0f, 0f, 0f, 0f); // transparente: sem borda branca
                    row.number.gameObject.SetActive(false);
                }
                else
                {
                    row.logo.enabled = false;
                    row.chip.color = m.TeamPrimary;
                    // No modo recolhido a coluna do numero some (painel estreito);
                    // nao reativar aqui, senao o numero "volta" todo frame.
                    row.number.gameObject.SetActive(!_towerCollapsed);
                    row.number.text = m.driver != null ? m.driver.number.ToString() : "";
                    row.number.color = m.TeamSecondary;
                }
                row.gap.text = i == 0 ? "Leader" : $"+{m.gapToLeader:0.000}";
                MarbleUITheme.TyreInfo(m.grip != null ? m.grip.gripId.ToString() : "", out var tl, out var tc);
                row.gripRing.color = tc;
                row.gripLetter.text = tl;
                row.gripLetter.color = tc;
                // Coluna de pit: contador, ou icone quando esta no pit.
                bool inPit = m.state == MarbleRaceState.InPit || m.state == MarbleRaceState.EnteringPit
                          || m.state == MarbleRaceState.ExitingPit;
                row.pit.text = inPit ? "P" : m.pitStops.ToString();
                row.pit.color = inPit ? new Color(1f, 0.65f, 0.2f) : new Color(0.7f, 0.78f, 0.9f);
                // Posicao com cor de medalha no top 3; lider tambem na sigla.
                row.pos.color = i < 3 ? UITheme.Medal(i + 1) : Color.white;
                row.code.color = i == 0 ? UITheme.Gold : Color.white;

                int prev = _posSnapshot.TryGetValue(m, out var p) ? p : (i + 1);
                if (i + 1 < prev) { row.arrow.text = "▲"; row.arrow.color = new Color(0.3f, 0.9f, 0.4f); }
                else if (i + 1 > prev) { row.arrow.text = "▼"; row.arrow.color = new Color(0.9f, 0.3f, 0.3f); }
                else row.arrow.text = "";
                if (refresh) _posSnapshot[m] = i + 1;

                // Destaque: jogador (dourado), top 3 (acento da medalha) e zebra.
                if (m.isPlayer)
                {
                    row.bg.color = new Color(0.2f, 0.17f, 0.05f, 0.96f);
                    row.glow.effectColor = new Color(1f, 0.85f, 0.2f, 0.9f);
                }
                else if (i < 3)
                {
                    var med = UITheme.Medal(i + 1);
                    row.bg.color = new Color(med.r * 0.18f, med.g * 0.16f, med.b * 0.12f, 0.96f);
                    row.glow.effectColor = new Color(med.r, med.g, med.b, 0.35f);
                }
                else
                {
                    row.bg.color = (i % 2 == 0) ? new Color(0.11f, 0.11f, 0.15f, 0.96f)
                                                : new Color(0.08f, 0.08f, 0.12f, 0.96f);
                    row.glow.effectColor = new Color(0f, 0f, 0f, 0f);
                }

                // Flash quando muda de posicao (microinteracao broadcast).
                int framePrev = _framePos.TryGetValue(m, out var fp) ? fp : (i + 1);
                if (i + 1 < framePrev) row.flash = 1f;
                else if (i + 1 > framePrev) row.flash = -1f;
                _framePos[m] = i + 1;
                if (Mathf.Abs(row.flash) > 0.02f)
                {
                    Color fc = row.flash > 0f ? MarbleUITheme.NeonGreen : MarbleUITheme.NeonRed;
                    row.bg.color = Color.Lerp(row.bg.color, fc, Mathf.Abs(row.flash) * 0.45f);
                    row.flash = Mathf.MoveTowards(row.flash, 0f, Time.deltaTime * 2.2f);
                }
            }
        }

        private void UpdateCards()
        {
            foreach (var card in _cards)
            {
                var m = card.ctrl.Runtime;
                card.title.text = $"{m.DisplayName}   P{m.position}";

                // Resumo compacto (modo recolhido): P# / sigla / pneu / combust.
                if (card.mini != null && _cardsCollapsed)
                {
                    MarbleUITheme.TyreInfo(m.grip != null ? m.grip.gripId.ToString() : "", out var mL, out var mC);
                    string code = m.driver != null ? m.driver.shortCode : "MAR";
                    card.mini.text = $"P{m.position}\n{code}\n<color=#{ColorUtility.ToHtmlStringRGB(mC)}>{mL}</color>\n{m.fuel:0}%";
                }

                MarbleUITheme.TyreInfo(m.grip != null ? m.grip.gripId.ToString() : "", out var curL, out _);
                MarbleUITheme.TyreInfo(card.nextGrip.ToString(), out var nxtL, out _);
                card.tyre.text = $"Atual: {curL}    ·    Próx: {nxtL}";
                card.mode.text = $"Modo: {m.mode}";

                SetBar(card.wearFill, m.wear / 100f);
                card.wearFill.GetComponent<Image>().color = m.wear > 70f ? MarbleUITheme.NeonRed : MarbleUITheme.TyreWear;
                card.wearVal.text = $"{m.wear:0}%";

                SetBar(card.energyFill, m.energy / 100f);
                card.energyFill.GetComponent<Image>().color = m.energy < 20f ? MarbleUITheme.Warning : MarbleUITheme.Energy;
                card.energyVal.text = $"{m.energy:0}%";

                // Combustivel: verde > amarelo > vermelho (PRD 7).
                SetBar(card.fuelFill, m.fuel / 100f);
                Color fuelColor = m.fuel <= 0f ? MarbleUITheme.NeonRed
                    : m.fuel < 10f ? new Color(1f, 0.35f, 0.25f)
                    : m.fuel < 25f ? MarbleUITheme.Warning : MarbleUITheme.Fuel;
                card.fuelFill.GetComponent<Image>().color = fuelColor;
                card.fuelVal.text = m.FuelEmpty ? "VAZIO" : $"{m.fuel:0}%";
                card.fuelVal.color = m.fuel < 25f ? MarbleUITheme.Warning : Color.white;

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

                // Glow pulsante quando o PIT esta disponivel e e recomendado
                // (combustivel/desgaste altos), reforcando a acao importante.
                if (card.pitGlow != null)
                {
                    bool urge = card.pitBtn.interactable && (m.fuel < 25f || m.wear > 70f);
                    float a = urge ? 0.5f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 0f;
                    card.pitGlow.effectColor = new Color(1f, 0.7f, 0.2f, a);
                }
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

        // ---- Log com cores + fade ----

        private void OnCountdown(int v) => _countdownText.text = v <= 0 ? "GO!" : v.ToString();

        private void PushLog(string msg)
        {
            _logItems.Add(new LogItem { msg = msg, color = ClassifyLog(msg), age = 0f });
            if (_logItems.Count > LogRows + 3) _logItems.RemoveAt(0);

            // Banner para eventos fortes vindos do log.
            if (msg.Contains("bateu forte")) ShowBanner("BATIDA FORTE", MarbleUITheme.NeonRed);
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

            // Placeholder quando nao ha eventos ainda.
            if (_logItems.Count == 0)
            {
                for (int r = 0; r < LogRows; r++) _logRows[r].text = "";
                _logRows[LogRows - 1].text = "Aguardando eventos da corrida…";
                _logRows[LogRows - 1].color = MarbleUITheme.TextMuted;
                return;
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

        private void OnFinished(RaceResult result) { _countdownText.text = "FIM"; CloseRadio(); }
    }
}
