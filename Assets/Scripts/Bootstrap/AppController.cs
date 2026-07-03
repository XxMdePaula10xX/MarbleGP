using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MarbleGP.Core;
using MarbleGP.Data;
using MarbleGP.Race;
using MarbleGP.UI;
using MarbleGP.CameraSystem;
using MarbleGP.Save;

namespace MarbleGP.Bootstrap
{
    /// <summary>
    /// Orquestrador de TODO o fluxo do jogo numa unica cena (PRD 35), evitando
    /// depender de varias cenas .unity feitas a mao. Implementa, via UI
    /// procedural, as telas do MVP: Perfil -> Menu -> Selecao de Pista ->
    /// Estrategia -> Corrida (RaceManager + HUD) -> Resultado.
    /// Basta colocar este componente num GameObject vazio da cena.
    /// </summary>
    public class AppController : MonoBehaviour
    {
        private GameManager _gm;
        private CameraController _camera;
        private GameObject _uiRoot;       // canvas atual de menu
        private GameObject _raceRoot;     // objetos da corrida (destruidos ao sair)

        // Pausa / reinicio (PRD 8).
        private MarbleGP.Race.RaceManager _currentRace;
        private RaceConfig _lastConfig;
        private bool _lastWasChampionship;
        private GameObject _pausePanel;

        // Replay (PRD extra).
        private MarbleGP.Race.RaceRecorder _recorder;
        private RaceHUD _raceHud;

        // Selecoes correntes do fluxo.
        private TrackDataSO _selectedTrack;
        private TrackDataSO _previewTrack;   // circuito em destaque na selecao
        private GripType _grip = GripType.Medium;
        private RaceMode _mode = RaceMode.Normal;
        private float _startEnergy = 100f;
        private int _selectedLaps = 5; // Rapido/Normal/Longo (PRD 3)

        // Desafio diario.
        private DailyChallengeDef _dailyDef;
        private bool _dailyActive;
        private DailyData _daily;

        private void Start()
        {
            EnsureCoreSystems();

            if (_gm.Database == null)
            {
                ShowFatal("GameDatabase nao encontrado.\nRode Tools > Marble GP > Gerar Dados do MVP.");
                return;
            }

            if (!_gm.Profile.created) ShowProfileScreen();
            else ShowMainMenu();
        }

        // ---- Infra ------------------------------------------------------

        private void EnsureCoreSystems()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
            }

            _gm = GameManager.Instance;
            if (_gm == null)
            {
                var go = new GameObject("GameManager");
                _gm = go.AddComponent<GameManager>();
            }

            if (Camera.main == null)
            {
                var camGo = new GameObject("MainCamera", typeof(Camera));
                camGo.tag = "MainCamera";
            }
            _camera = Camera.main.GetComponent<CameraController>();
            if (_camera == null) _camera = Camera.main.gameObject.AddComponent<CameraController>();
            Camera.main.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
        }

        private void ClearMenuUI()
        {
            if (_uiRoot != null) Destroy(_uiRoot);
        }

        private Transform NewCanvas(string name)
        {
            ClearMenuUI();
            var canvas = UIFactory.CreateCanvas(name);
            _uiRoot = canvas.gameObject;
            // Fundo cobre a tela inteira (sangra ate as bordas, atras do notch).
            UIFactory.Background(canvas.transform, BackgroundKey(name), new Color(0.06f, 0.07f, 0.11f, 1f));
            // O conteudo da tela respeita a safe area (notch / home indicator).
            // Como Transform.transform devolve a si mesmo, os chamadores podem
            // continuar usando "canvas.transform" sem alteracoes.
            return UIFactory.SafeAreaRoot(canvas.transform);
        }

        private static string BackgroundKey(string canvasName)
        {
            switch (canvasName)
            {
                case "ProfileScreen":
                case "MainMenu": return "menu";
                case "TrackSelect": return "trackselect";
                case "Strategy": return "strategy";
                case "Results": return "results";
                case "Championship":
                case "Upgrades": return "championship";
                case "Garage": return "garage";
                default: return "menu";
            }
        }

        // ---- Tela: Perfil (PRD 7.1 / 23.1) ------------------------------

        private void ShowProfileScreen()
        {
            var canvas = NewCanvas("ProfileScreen");
            UIFactory.Label(canvas.transform, "MARBLE GP MANAGER", 54, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.95f), Color.white);
            UIFactory.Label(canvas.transform, "Criar Perfil", 30, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.8f), new Color(0.8f, 0.8f, 1f));

            var nameField = InputField(canvas.transform, "Nome do jogador",
                new Vector2(0.3f, 0.6f), new Vector2(0.7f, 0.68f));
            var teamField = InputField(canvas.transform, "Nome da equipe",
                new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.58f));

            var go = UIFactory.Button(canvas.transform, "Continuar", new Color(0.2f, 0.6f, 0.3f),
                new Vector2(0.4f, 0.35f), new Vector2(0.6f, 0.43f), Vector2.zero, Vector2.zero);
            go.onClick.AddListener(() =>
            {
                var p = _gm.Profile;
                p.playerName = string.IsNullOrWhiteSpace(nameField.text) ? "Player" : nameField.text;
                p.teamName = string.IsNullOrWhiteSpace(teamField.text) ? "My Team" : teamField.text;
                p.created = true;
                _gm.SaveProfile();
                ShowMainMenu();
            });
        }

        // ---- Tela: Menu principal (PRD 7.2 / 23.2) ----------------------

        private void ShowMainMenu()
        {
            var canvas = NewCanvas("MainMenu");
            var title = UIFactory.Label(canvas.transform, "MARBLE GP MANAGER", 70, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.96f), Color.white);
            title.fontStyle = FontStyle.Bold;
            var tglow = title.gameObject.AddComponent<Outline>();
            tglow.effectColor = new Color(MarbleUITheme.NeonCyan.r, MarbleUITheme.NeonCyan.g, MarbleUITheme.NeonCyan.b, 0.55f);
            tglow.effectDistance = new Vector2(2.5f, 2.5f);
            UIFactory.Label(canvas.transform, "STRATEGY RACING CHAMPIONSHIP", 22, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.785f), new Vector2(0.95f, 0.83f), MarbleUITheme.NeonCyan);

            // Card da equipe atual (glass).
            var teamCard = UIFactory.GlassPanel(canvas.transform, new Vector2(0.37f, 0.7f), new Vector2(0.63f, 0.76f));
            UIFactory.Label(teamCard, "SUA EQUIPE", 11, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.5f), new Vector2(0.5f, 0.95f), MarbleUITheme.NeonCyan);
            UIFactory.Label(teamCard, _gm.Profile.teamName, 20, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.55f), Color.white).fontStyle = FontStyle.Bold;

            MenuButton(canvas.transform, "Corrida Rápida", "flag", 0, () => ShowTrackSelect(), MarbleUITheme.NeonOrange);
            MenuButton(canvas.transform, "Desafio do Dia", "", 1, () => ShowDailyChallenge(), MarbleUITheme.NeonGreen);
            MenuButton(canvas.transform, "Campeonato", "trophy", 2, () => ShowChampionshipHub(), new Color32(18, 60, 105, 235));
            MenuButton(canvas.transform, "Garagem", "settings", 3, () => ShowGarage(), MarbleUITheme.NeonPurple);
            MenuButton(canvas.transform, "Conquistas", "trophy", 4, () => ShowAchievements(), MarbleUITheme.NeonGold);
            MenuButton(canvas.transform, "Sair", "", 5, () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }, UITheme.DangerButton);

            UIFactory.Label(canvas.transform, $"v1.0  ·  {_gm.Profile.playerName}", 14, TextAnchor.LowerRight,
                new Vector2(0.5f, 0.01f), new Vector2(0.98f, 0.05f), UITheme.TextDim);
        }

        private void MenuButton(Transform parent, string label, string icon, int index,
            UnityEngine.Events.UnityAction onClick, Color color)
        {
            float yMax = 0.66f - index * 0.10f;
            var btn = UIFactory.Button(parent, label, color,
                new Vector2(0.3f, yMax - 0.085f), new Vector2(0.7f, yMax), Vector2.zero, Vector2.zero);
            btn.GetComponentInChildren<Text>().fontSize = 23;
            if (!string.IsNullOrEmpty(icon))
                UIFactory.Icon(btn.transform, icon, new Vector2(0.06f, 0.22f), new Vector2(0.15f, 0.78f), Color.white);
            if (onClick != null) btn.onClick.AddListener(onClick);
        }

        // ---- Tela: Conquistas (PRD 32) ----------------------------------

        private void ShowAchievements()
        {
            var canvas = NewCanvas("Achievements");
            var cat = AchievementManager.Catalog;
            var data = AchievementManager.Data;
            int unlocked = AchievementManager.UnlockedCount();

            UIFactory.Label(canvas.transform, "CONQUISTAS", 44, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.9f), new Vector2(0.6f, 0.99f), Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(canvas.transform, $"{unlocked} de {cat.Count} desbloqueadas", 20, TextAnchor.UpperLeft,
                new Vector2(0.045f, 0.85f), new Vector2(0.6f, 0.9f), MarbleUITheme.NeonGold).fontStyle = FontStyle.Bold;

            // Barra geral de progresso.
            var pbBg = UIFactory.Panel(canvas.transform, new Vector2(0.55f, 0.865f), new Vector2(0.96f, 0.9f),
                Vector2.zero, Vector2.zero, new Color(0.06f, 0.09f, 0.14f, 1f));
            var pbFill = UIFactory.SolidPanel(pbBg, new Vector2(0f, 0f),
                new Vector2(cat.Count > 0 ? (float)unlocked / cat.Count : 0f, 1f),
                MarbleUITheme.NeonGold);
            pbFill.GetComponent<Image>().raycastTarget = false;

            // Scroll (grade de 2 colunas).
            var scrollGo = new GameObject("AchScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(canvas.transform, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.04f, 0.12f); srt.anchorMax = new Vector2(0.96f, 0.83f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vrt = viewportGo.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.002f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f);

            const int cols = 2;
            int rows = Mathf.CeilToInt(cat.Count / (float)cols);
            crt.sizeDelta = new Vector2(0f, rows * 165f);
            crt.anchoredPosition = Vector2.zero;
            scroll.viewport = vrt; scroll.content = crt;

            for (int i = 0; i < cat.Count; i++)
            {
                int r = i / cols, c = i % cols;
                float x0 = c / (float)cols, x1 = (c + 1) / (float)cols;
                float yTop = 1f - r / (float)rows, yBot = 1f - (r + 1) / (float)rows;
                BuildAchievementCard(contentGo.transform, cat[i], data,
                    new Vector2(x0 + 0.006f, yBot + 0.006f), new Vector2(x1 - 0.006f, yTop - 0.006f));
            }

            BackButton(canvas.transform, ShowMainMenu);
        }

        private void BuildAchievementCard(Transform parent, Achievement a, AchievementData data,
            Vector2 aMin, Vector2 aMax)
        {
            bool done = a.IsUnlocked(data);
            var card = UIFactory.Panel(parent, aMin, aMax, Vector2.zero, Vector2.zero,
                done ? new Color(0.11f, 0.12f, 0.06f, 0.96f) : MarbleUITheme.PanelDark);
            UIFactory.NeonBorder(card.gameObject, done ? MarbleUITheme.NeonGold : MarbleUITheme.PanelSoft,
                done ? 0.85f : 0.3f, done ? 2f : 1f);

            // Selo de status (✓ desbloqueada / cadeado simples).
            var seal = UIFactory.Label(card, done ? "✓" : "·", 34, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.4f), new Vector2(0.16f, 0.95f),
                done ? MarbleUITheme.NeonGold : UITheme.TextDim);
            seal.fontStyle = FontStyle.Bold;

            UIFactory.Label(card, a.title, 19, TextAnchor.UpperLeft,
                new Vector2(0.18f, 0.58f), new Vector2(0.97f, 0.94f),
                done ? Color.white : new Color(0.72f, 0.76f, 0.85f)).fontStyle = FontStyle.Bold;
            UIFactory.Label(card, a.desc, 13, TextAnchor.UpperLeft,
                new Vector2(0.18f, 0.32f), new Vector2(0.97f, 0.6f), UITheme.TextDim);

            // Barra de progresso + contador.
            var bg = UIFactory.Panel(card, new Vector2(0.18f, 0.14f), new Vector2(0.78f, 0.26f),
                Vector2.zero, Vector2.zero, new Color(0.06f, 0.09f, 0.14f, 1f));
            var fill = UIFactory.SolidPanel(bg, new Vector2(0f, 0f), new Vector2(a.Progress(data), 1f),
                done ? MarbleUITheme.NeonGold : MarbleUITheme.NeonBlue);
            fill.GetComponent<Image>().raycastTarget = false;
            UIFactory.Label(card, $"{a.Current(data)}/{a.target}", 13, TextAnchor.MiddleRight,
                new Vector2(0.79f, 0.13f), new Vector2(0.97f, 0.27f),
                done ? MarbleUITheme.NeonGold : UITheme.TextDim);
        }

        // ---- Tela: Desafio do Dia ---------------------------------------

        private void ShowDailyChallenge()
        {
            var canvas = NewCanvas("Daily");
            _dailyDef = DailyChallenge.Today(_gm.Database);
            _daily ??= SaveManager.LoadDaily();
            var def = _dailyDef;

            UIFactory.Label(canvas.transform, "DESAFIO DO DIA", 44, TextAnchor.UpperLeft,
                new Vector2(0.05f, 0.88f), new Vector2(0.7f, 0.98f), Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(canvas.transform, System.DateTime.Now.ToString("dd/MM/yyyy"), 18, TextAnchor.UpperLeft,
                new Vector2(0.055f, 0.83f), new Vector2(0.7f, 0.88f), MarbleUITheme.NeonGreen).fontStyle = FontStyle.Bold;

            if (def == null || def.track == null)
            {
                UIFactory.Label(canvas.transform, "Nenhum circuito disponível.", 22, TextAnchor.MiddleCenter,
                    new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.6f), UITheme.TextDim);
                BackButton(canvas.transform, ShowMainMenu);
                return;
            }

            bool sameDay = _daily.lastDateKey == def.dateKey;
            bool met = sameDay && _daily.objectiveMet;
            int attempts = sameDay ? _daily.attempts : 0;
            int best = sameDay ? _daily.bestPosition : 0;

            // Card principal: pista + cenario + objetivo.
            var card = UIFactory.GlassPanel(canvas.transform, new Vector2(0.06f, 0.28f), new Vector2(0.62f, 0.8f));
            UIFactory.Thumbnail(card, def.track.trackId, new Vector2(0.04f, 0.42f), new Vector2(0.5f, 0.93f));
            UIFactory.Label(card, def.track.trackName.ToUpper(), 24, TextAnchor.UpperLeft,
                new Vector2(0.53f, 0.8f), new Vector2(0.97f, 0.93f), Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(card, $"{def.laps} voltas  ·  {WeatherName(def.weather)}", 15, TextAnchor.UpperLeft,
                new Vector2(0.53f, 0.72f), new Vector2(0.97f, 0.8f), UITheme.TextDim);
            UIFactory.Label(card, $"Sugerido: pneu {def.startGrip} · modo {def.startMode}", 13, TextAnchor.UpperLeft,
                new Vector2(0.53f, 0.65f), new Vector2(0.97f, 0.72f), UITheme.TextDim);
            UIFactory.Label(card, "OBJETIVO", 13, TextAnchor.UpperLeft,
                new Vector2(0.53f, 0.55f), new Vector2(0.97f, 0.61f), MarbleUITheme.NeonGreen).fontStyle = FontStyle.Bold;
            UIFactory.Label(card, def.description, 18, TextAnchor.UpperLeft,
                new Vector2(0.53f, 0.42f), new Vector2(0.97f, 0.55f), Color.white).fontStyle = FontStyle.Bold;
            string status = met ? "✓ CUMPRIDO HOJE"
                : attempts > 0 ? $"Tentativas hoje: {attempts}" : "Ainda não tentado hoje";
            UIFactory.Label(card, status, 15, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.3f), new Vector2(0.97f, 0.4f),
                met ? MarbleUITheme.NeonGold : UITheme.TextDim).fontStyle = FontStyle.Bold;

            // Card lateral: streak + melhor de hoje.
            var side = UIFactory.GlassPanel(canvas.transform, new Vector2(0.65f, 0.28f), new Vector2(0.94f, 0.8f));
            UIFactory.Label(side, "SEQUÊNCIA", 14, TextAnchor.UpperCenter,
                new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.96f), MarbleUITheme.NeonGreen).fontStyle = FontStyle.Bold;
            UIFactory.Label(side, _daily.streak.ToString(), 60, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.56f), new Vector2(0.95f, 0.86f), Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(side, "dias seguidos", 13, TextAnchor.UpperCenter,
                new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.57f), UITheme.TextDim);
            UIFactory.Label(side, "MELHOR HOJE", 13, TextAnchor.UpperCenter,
                new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.43f), UITheme.TextDim).fontStyle = FontStyle.Bold;
            UIFactory.Label(side, best > 0 ? $"P{best}" : "—", 36, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.33f), MarbleUITheme.NeonGold).fontStyle = FontStyle.Bold;

            var play = UIFactory.Button(canvas.transform, met ? "Jogar de Novo" : "Jogar", MarbleUITheme.NeonGreen,
                new Vector2(0.35f, 0.12f), new Vector2(0.65f, 0.22f), Vector2.zero, Vector2.zero);
            play.GetComponentInChildren<Text>().fontSize = 26;
            play.onClick.AddListener(StartDaily);

            BackButton(canvas.transform, ShowMainMenu);
        }

        private void StartDaily()
        {
            var def = _dailyDef;
            if (def == null || def.track == null) return;
            UnityEngine.Random.InitState(def.seed); // cenario reproduzivel (mesmo p/ todos)
            string playerTeamId = _gm.Database.teams.Count > 0 ? _gm.Database.teams[0].teamId : "";
            var config = QuickRaceBuilder.Build(_gm.Database, def.track, playerTeamId,
                maxMarbles: 20, defaultGrip: def.startGrip, startMode: def.startMode);
            config.laps = def.laps;
            config.weather = def.weather;
            _dailyActive = true;
            RunRace(config, isChampionship: false);
        }

        private void UpdateDailyData(DailyChallengeDef def, bool met, int bestPos, int overtakes)
        {
            _daily ??= SaveManager.LoadDaily();
            var d = _daily;
            if (d.lastDateKey != def.dateKey)
            {
                d.lastDateKey = def.dateKey;
                d.objectiveMet = false; d.bestPosition = 0; d.bestOvertakes = 0; d.attempts = 0;
            }
            d.attempts++;
            if (bestPos > 0 && (d.bestPosition == 0 || bestPos < d.bestPosition)) d.bestPosition = bestPos;
            if (overtakes > d.bestOvertakes) d.bestOvertakes = overtakes;
            if (met && !d.objectiveMet)
            {
                d.objectiveMet = true;
                string yesterday = System.DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
                d.streak = d.lastWinDateKey == yesterday ? d.streak + 1 : 1;
                d.lastWinDateKey = def.dateKey;
            }
            SaveManager.SaveDaily(d);
        }

        private static string WeatherName(Weather w)
        {
            switch (w)
            {
                case Weather.Dry: return "Seco";
                case Weather.Cloudy: return "Nublado";
                case Weather.Damp: return "Úmido";
                case Weather.LightRain: return "Chuva leve";
                case Weather.HeavyRain: return "Chuva forte";
                default: return w.ToString();
            }
        }

        // ---- Tela: Selecao de pista (PRD 7.3 / 23.3) --------------------

        private void ShowTrackSelect()
        {
            var canvas = NewCanvas("TrackSelect");
            UIFactory.Label(canvas.transform, "SELECIONAR CIRCUITO", 44, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.9f), new Vector2(0.6f, 0.99f), Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(canvas.transform, "Escolha o circuito da sua próxima corrida.", 18, TextAnchor.UpperLeft,
                new Vector2(0.045f, 0.85f), new Vector2(0.6f, 0.9f), UITheme.TextDim);

            var tracks = _gm.Database.tracks;
            if (_previewTrack == null || !tracks.Contains(_previewTrack))
                _previewTrack = tracks.FirstOrDefault(t => !t.trackLocked) ?? tracks.FirstOrDefault();

            // Lista de circuitos (esquerda) com SCROLL para caber todas as pistas.
            var scrollGo = new GameObject("CircuitScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(canvas.transform, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.035f, 0.13f); srt.anchorMax = new Vector2(0.56f, 0.84f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vrt = viewportGo.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.002f); // quase invisivel, recebe o arrasto

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f);
            int n = tracks.Count;
            crt.sizeDelta = new Vector2(0f, n * 150f);
            crt.anchoredPosition = Vector2.zero;
            scroll.viewport = vrt; scroll.content = crt;

            for (int i = 0; i < n; i++)
            {
                float yTop = 1f - i / (float)n;
                float yBot = 1f - (i + 1) / (float)n;
                BuildCircuitListItem(contentGo.transform, tracks[i], i + 1,
                    new Vector2(0.01f, yBot + 0.004f), new Vector2(0.99f, yTop - 0.004f));
            }

            // Preview (direita).
            BuildCircuitPreview(canvas.transform, _previewTrack, tracks.IndexOf(_previewTrack) + 1);

            // Rodape: Voltar / Aleatorio / Confirmar.
            var back = UIFactory.Button(canvas.transform, "Voltar", UITheme.NeutralButton,
                new Vector2(0.04f, 0.03f), new Vector2(0.2f, 0.1f), Vector2.zero, Vector2.zero);
            back.onClick.AddListener(ShowMainMenu);
            var rand = UIFactory.Button(canvas.transform, "Aleatório", UITheme.SecondaryButton,
                new Vector2(0.42f, 0.03f), new Vector2(0.58f, 0.1f), Vector2.zero, Vector2.zero);
            rand.onClick.AddListener(() =>
            {
                var avail = tracks.Where(x => !x.trackLocked).ToList();
                if (avail.Count > 0) { _previewTrack = avail[UnityEngine.Random.Range(0, avail.Count)]; ShowTrackSelect(); }
            });
            bool canGo = _previewTrack != null && !_previewTrack.trackLocked;
            var confirm = UIFactory.Button(canvas.transform, "Confirmar",
                canGo ? UITheme.Success : UITheme.NeutralButton,
                new Vector2(0.78f, 0.03f), new Vector2(0.96f, 0.1f), Vector2.zero, Vector2.zero);
            confirm.interactable = canGo;
            confirm.onClick.AddListener(() => { _selectedTrack = _previewTrack; ShowStrategy(); });
        }

        private void BuildCircuitListItem(Transform parent, TrackDataSO t, int num, Vector2 aMin, Vector2 aMax)
        {
            bool locked = t.trackLocked;
            bool sel = t == _previewTrack;
            var card = UIFactory.Button(parent, "",
                locked ? new Color32(18, 22, 30, 235) : MarbleUITheme.PanelDark, aMin, aMax, Vector2.zero, Vector2.zero);
            UIFactory.NeonBorder(card.gameObject,
                sel ? MarbleUITheme.NeonBlue : (locked ? MarbleUITheme.PanelSoft : MarbleUITheme.NeonCyan),
                sel ? 0.95f : 0.3f, sel ? 2.2f : 1f);
            var ct = t;
            card.onClick.AddListener(() => { _previewTrack = ct; ShowTrackSelect(); });

            UIFactory.Thumbnail(card.transform, t.trackId, new Vector2(0.02f, 0.13f), new Vector2(0.125f, 0.87f));
            var nb = UIFactory.Panel(card.transform, new Vector2(0.215f, 0.55f), new Vector2(0.275f, 0.9f),
                Vector2.zero, Vector2.zero, sel ? MarbleUITheme.NeonBlue : MarbleUITheme.PanelSoft);
            UIFactory.Label(nb, num.ToString(), 17, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white).fontStyle = FontStyle.Bold;

            UIFactory.Label(card.transform, t.trackName, 22, TextAnchor.LowerLeft,
                new Vector2(0.3f, 0.54f), new Vector2(0.98f, 0.92f), locked ? UITheme.TextDim : Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(card.transform, locked ? "Bloqueado · vença o campeonato" : Trim(t.description, 56), 15,
                TextAnchor.UpperLeft, new Vector2(0.3f, 0.08f), new Vector2(0.70f, 0.52f), UITheme.TextDim);
            UIFactory.Label(card.transform,
                $"{t.difficulty}  ·  {t.recommendedLaps}v  ·  chuva {Mathf.RoundToInt(t.rainChance * 100f)}%", 15,
                TextAnchor.LowerRight, new Vector2(0.70f, 0.1f), new Vector2(0.97f, 0.45f), UITheme.TextDim);
        }

        private void BuildCircuitPreview(Transform parent, TrackDataSO t, int num)
        {
            var panel = UIFactory.GlassPanel(parent, new Vector2(0.57f, 0.155f), new Vector2(0.97f, 0.835f));
            if (t == null) return;
            UIFactory.Label(panel, t.trackName.ToUpper(), 30, TextAnchor.UpperLeft,
                new Vector2(0.05f, 0.88f), new Vector2(0.82f, 0.98f), Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(panel, t.trackLocked ? "BLOQUEADO" : "CIRCUITO", 13, TextAnchor.UpperLeft,
                new Vector2(0.05f, 0.83f), new Vector2(0.82f, 0.88f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;
            var nb = UIFactory.Panel(panel, new Vector2(0.88f, 0.85f), new Vector2(0.96f, 0.97f),
                Vector2.zero, Vector2.zero, MarbleUITheme.NeonBlue);
            UIFactory.Label(nb, num.ToString(), 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white).fontStyle = FontStyle.Bold;

            UIFactory.Thumbnail(panel, t.trackId, new Vector2(0.34f, 0.47f), new Vector2(0.66f, 0.82f));

            // 3 indicadores EMPILHADOS (largura cheia) com espaco proprio — sem
            // sobreposicao de label com as bolinhas.
            PreviewStat(panel, "DIFICULDADE", DifficultyDots(t.difficulty), 5, MarbleUITheme.NeonGreen, 0.385f);
            PreviewStat(panel, "ULTRAPASSAGEM", Mathf.RoundToInt(t.overtakeLevel * 8f), 8, MarbleUITheme.NeonCyan, 0.305f);
            PreviewStat(panel, "DESGASTE DE PNEU", Mathf.RoundToInt((t.abrasionLevel - 0.5f) / 1.5f * 8f), 8, MarbleUITheme.NeonOrange, 0.225f);

            UIFactory.Label(panel, Trim(t.description, 160), 16, TextAnchor.UpperLeft,
                new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.2f), UITheme.TextDim);
        }

        private void PreviewStat(Transform parent, string label, int filled, int total, Color color, float yBase)
        {
            UIFactory.Label(parent, label, 15, TextAnchor.LowerLeft,
                new Vector2(0.05f, yBase + 0.03f), new Vector2(0.95f, yBase + 0.075f), UITheme.TextDim).fontStyle = FontStyle.Bold;
            float pipW = 0.9f / total;
            for (int i = 0; i < total; i++)
            {
                float px = 0.05f + i * pipW;
                var pip = UIFactory.Panel(parent, new Vector2(px, yBase), new Vector2(px + pipW - 0.008f, yBase + 0.025f),
                    Vector2.zero, Vector2.zero, i < filled ? color : new Color(0.18f, 0.22f, 0.3f, 0.9f));
                pip.GetComponent<Image>().raycastTarget = false;
            }
        }

        private static int DifficultyDots(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Easy: return 2;
                case Difficulty.Medium: return 3;
                case Difficulty.Hard: return 4;
                default: return 3;
            }
        }

        // ---- Tela: Estrategia pre-corrida (PRD 7.3 / 23.4) --------------

        private void ShowStrategy()
        {
            var canvas = NewCanvas("Strategy");
            var t = _selectedTrack;
            int rainPct = Mathf.RoundToInt(t.rainChance * 100f);

            // ---- Top bar ----
            var top = UIFactory.GlassPanel(canvas.transform, new Vector2(0.02f, 0.885f), new Vector2(0.98f, 0.985f));
            UIFactory.Label(top, $"Estratégia — {t.trackName}", 24, TextAnchor.LowerLeft,
                new Vector2(0.025f, 0.46f), new Vector2(0.7f, 0.95f), Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(top, "ESTRATÉGIA PRÉ-CORRIDA", 11, TextAnchor.UpperLeft,
                new Vector2(0.027f, 0.08f), new Vector2(0.5f, 0.4f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;
            UIFactory.Label(top, $"{(rainPct >= 30 ? "INSTÁVEL" : "SECO")}   ·   chuva {rainPct}%", 18,
                TextAnchor.MiddleRight, new Vector2(0.55f, 0f), new Vector2(0.97f, 1f), UITheme.TextDim);

            // ---- Track blueprint (esquerda) ----
            var bp = UIFactory.GlassPanel(canvas.transform, new Vector2(0.03f, 0.46f), new Vector2(0.47f, 0.88f));
            UIFactory.Label(bp, "MAPA DO CIRCUITO", 16, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.9f), new Vector2(0.7f, 0.99f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;
            UIFactory.Thumbnail(bp, t.trackId, new Vector2(0.30f, 0.13f), new Vector2(0.70f, 0.88f));
            UIFactory.Label(bp, $"{t.difficulty}  ·  {t.recommendedLaps} voltas recomendadas  ·  {Mathf.RoundToInt(t.trackLength)} m",
                13, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.12f), UITheme.TextDim);

            // ---- Weather forecast ----
            var wf = UIFactory.GlassPanel(canvas.transform, new Vector2(0.49f, 0.715f), new Vector2(0.78f, 0.88f));
            UIFactory.Label(wf, "PREVISÃO DO CLIMA", 16, TextAnchor.UpperLeft,
                new Vector2(0.03f, 0.86f), new Vector2(0.7f, 0.99f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;
            string[] times = { "NOW", "+15", "+30", "+45", "+60", "+90" };
            for (int i = 0; i < 6; i++)
            {
                float bx = 0.03f + i * 0.158f;
                var blk = UIFactory.Panel(wf, new Vector2(bx, 0.08f), new Vector2(bx + 0.145f, 0.78f),
                    Vector2.zero, Vector2.zero, MarbleUITheme.PanelDark);
                UIFactory.Label(blk, times[i], 12, TextAnchor.UpperCenter,
                    new Vector2(0f, 0.76f), new Vector2(1f, 0.98f), UITheme.TextDim);
                UIFactory.Icon(blk, "weather", new Vector2(0.28f, 0.4f), new Vector2(0.72f, 0.74f), Color.white);
                UIFactory.Label(blk, $"{24 + (i % 2)}°", 16, TextAnchor.MiddleCenter,
                    new Vector2(0f, 0.2f), new Vector2(1f, 0.42f), Color.white).fontStyle = FontStyle.Bold;
                int rp = Mathf.Clamp(rainPct - 20 + i * 8, 0, 95);
                UIFactory.Label(blk, $"{rp}%", 12, TextAnchor.LowerCenter,
                    new Vector2(0f, 0.02f), new Vector2(1f, 0.2f), rp > 30 ? MarbleUITheme.NeonBlue : UITheme.TextDim);
            }

            // ---- Tyre selection ----
            var ts = UIFactory.GlassPanel(canvas.transform, new Vector2(0.49f, 0.55f), new Vector2(0.78f, 0.705f));
            UIFactory.Label(ts, "PNEUS", 16, TextAnchor.UpperLeft,
                new Vector2(0.03f, 0.82f), new Vector2(0.7f, 0.99f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;
            TyreCard(ts, GripType.Soft, "SOFT", "Mais aderência", 0);
            TyreCard(ts, GripType.Medium, "MEDIUM", "Equilibrado", 1);
            TyreCard(ts, GripType.Hard, "HARD", "Durável", 2);
            TyreCard(ts, GripType.Intermediate, "INTER", "Pista úmida", 3);
            TyreCard(ts, GripType.Rain, "RAIN", "Chuva forte", 4);

            // ---- Drive mode ----
            var dm = UIFactory.GlassPanel(canvas.transform, new Vector2(0.49f, 0.43f), new Vector2(0.78f, 0.54f));
            UIFactory.Label(dm, "MODO DE CORRIDA", 16, TextAnchor.UpperLeft,
                new Vector2(0.03f, 0.78f), new Vector2(0.7f, 0.99f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;
            ModeCard(dm, RaceMode.Save, "SAVE", "Economiza energia", MarbleUITheme.NeonGreen, 0);
            ModeCard(dm, RaceMode.Normal, "NORMAL", "Equilíbrio", MarbleUITheme.NeonBlue, 1);
            ModeCard(dm, RaceMode.Push, "PUSH", "Máximo ritmo", MarbleUITheme.NeonRed, 2);

            // ---- Race duration ----
            var rd = UIFactory.GlassPanel(canvas.transform, new Vector2(0.49f, 0.31f), new Vector2(0.78f, 0.42f));
            UIFactory.Label(rd, "DURAÇÃO DA CORRIDA", 16, TextAnchor.UpperLeft,
                new Vector2(0.03f, 0.78f), new Vector2(0.7f, 0.99f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;
            DurationCard(rd, 5, "RÁPIDA", "5 voltas", 0);
            DurationCard(rd, 12, "NORMAL", "12 voltas", 1);
            DurationCard(rd, 20, "LONGA", "20 voltas", 2);

            // ---- Strategy summary (direita) ----
            var sm = UIFactory.GlassPanel(canvas.transform, new Vector2(0.80f, 0.31f), new Vector2(0.98f, 0.88f),
                null, MarbleUITheme.NeonOrange);
            UIFactory.Label(sm, "RESUMO DA ESTRATÉGIA", 16, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.93f), new Vector2(0.94f, 0.99f), MarbleUITheme.NeonOrange).fontStyle = FontStyle.Bold;
            int stops = _selectedLaps >= 18 ? 2 : 1;
            SummaryRow(sm, "PARADAS PREVISTAS", $"{stops}", "parada(s) no pit", 0.78f, MarbleUITheme.NeonOrange);
            SummaryRow(sm, "COMBUSTÍVEL", "ATENÇÃO", "não chega ao fim sem parar", 0.56f, MarbleUITheme.Warning);
            SummaryRow(sm, "PNEU INICIAL", _grip.ToString().ToUpper(), TyreNote(_grip), 0.34f, MarbleUITheme.NeonCyan);
            var tip = UIFactory.Panel(sm, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.2f),
                Vector2.zero, Vector2.zero, new Color32(22, 20, 8, 220));
            UIFactory.Label(tip, "DICA: macio é mais rápido mas desgasta antes. Planeje o pit!", 12,
                TextAnchor.MiddleCenter, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), UITheme.TextDim);

            // ---- Start race (botao grande) ----
            var start = UIFactory.Button(canvas.transform, "INICIAR CORRIDA", MarbleUITheme.NeonOrange,
                new Vector2(0.30f, 0.07f), new Vector2(0.70f, 0.20f), Vector2.zero, Vector2.zero);
            start.GetComponentInChildren<Text>().fontSize = 30;
            UIFactory.Icon(start.transform, "flag", new Vector2(0.16f, 0.25f), new Vector2(0.26f, 0.75f), Color.white);
            UIFactory.NeonBorder(start.gameObject, MarbleUITheme.NeonGold, 0.9f, 2.5f);
            start.onClick.AddListener(StartRace);

            BackButton(canvas.transform, ShowTrackSelect);
        }

        private void TyreCard(Transform parent, GripType g, string name, string desc, int col)
        {
            bool sel = _grip == g;
            float xMin = 0.03f + col * 0.193f;
            var card = UIFactory.Panel(parent, new Vector2(xMin, 0.06f), new Vector2(xMin + 0.18f, 0.78f),
                Vector2.zero, Vector2.zero, sel ? new Color32(45, 30, 10, 240) : MarbleUITheme.PanelDark);
            UIFactory.NeonBorder(card.gameObject, sel ? MarbleUITheme.NeonOrange : MarbleUITheme.NeonCyan,
                sel ? 0.95f : 0.3f, sel ? 2.2f : 1.1f);
            UIFactory.TyreBadgeSquare(card, g.ToString(), new Vector2(0.5f, 0.69f), 40f);
            UIFactory.Label(card, name, 15, TextAnchor.MiddleCenter, new Vector2(0.02f, 0.26f), new Vector2(0.98f, 0.44f),
                sel ? MarbleUITheme.NeonOrange : Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(card, desc, 10, TextAnchor.MiddleCenter, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.24f),
                UITheme.TextDim);
            var btn = card.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var gg = g; btn.onClick.AddListener(() => { _grip = gg; ShowStrategy(); });
        }

        private void ModeCard(Transform parent, RaceMode m, string name, string desc, Color accent, int col)
        {
            bool sel = _mode == m;
            float xMin = 0.03f + col * 0.323f;
            var card = UIFactory.Panel(parent, new Vector2(xMin, 0.08f), new Vector2(xMin + 0.30f, 0.72f),
                Vector2.zero, Vector2.zero, sel ? new Color(accent.r * 0.2f, accent.g * 0.2f, accent.b * 0.2f, 0.95f) : MarbleUITheme.PanelDark);
            UIFactory.NeonBorder(card.gameObject, sel ? accent : MarbleUITheme.NeonCyan, sel ? 0.95f : 0.3f, sel ? 2f : 1.1f);
            UIFactory.Label(card, name, 16, TextAnchor.MiddleLeft, new Vector2(0.1f, 0.45f), new Vector2(0.98f, 0.95f),
                sel ? accent : Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(card, desc, 11, TextAnchor.MiddleLeft, new Vector2(0.1f, 0.05f), new Vector2(0.98f, 0.45f), UITheme.TextDim);
            var btn = card.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var mm = m; btn.onClick.AddListener(() => { _mode = mm; ShowStrategy(); });
        }

        private void DurationCard(Transform parent, int laps, string name, string desc, int col)
        {
            bool sel = _selectedLaps == laps;
            float xMin = 0.03f + col * 0.323f;
            var card = UIFactory.Panel(parent, new Vector2(xMin, 0.1f), new Vector2(xMin + 0.30f, 0.72f),
                Vector2.zero, Vector2.zero, sel ? new Color32(10, 40, 60, 240) : MarbleUITheme.PanelDark);
            UIFactory.NeonBorder(card.gameObject, sel ? MarbleUITheme.NeonBlue : MarbleUITheme.NeonCyan, sel ? 0.95f : 0.3f, sel ? 2f : 1.1f);
            UIFactory.Label(card, name, 16, TextAnchor.MiddleLeft, new Vector2(0.1f, 0.45f), new Vector2(0.98f, 0.95f),
                sel ? MarbleUITheme.NeonBlue : Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(card, desc, 11, TextAnchor.MiddleLeft, new Vector2(0.1f, 0.05f), new Vector2(0.98f, 0.45f), UITheme.TextDim);
            var btn = card.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var ll = laps; btn.onClick.AddListener(() => { _selectedLaps = ll; ShowStrategy(); });
        }

        private void SummaryRow(Transform parent, string label, string value, string sub, float y, Color color)
        {
            UIFactory.Label(parent, label, 12, TextAnchor.UpperLeft,
                new Vector2(0.06f, y + 0.08f), new Vector2(0.94f, y + 0.14f), UITheme.TextDim);
            UIFactory.Label(parent, value, 24, TextAnchor.UpperLeft,
                new Vector2(0.06f, y), new Vector2(0.94f, y + 0.08f), color).fontStyle = FontStyle.Bold;
            UIFactory.Label(parent, sub, 11, TextAnchor.UpperLeft,
                new Vector2(0.06f, y - 0.04f), new Vector2(0.94f, y), UITheme.TextDim);
        }

        private static string TyreNote(GripType g)
        {
            switch (g)
            {
                case GripType.Soft: return "macio, desgasta antes";
                case GripType.Hard: return "duro, dura mais";
                case GripType.Rain: return "para chuva forte";
                case GripType.Intermediate: return "para pista úmida";
                default: return "equilibrado";
            }
        }

        // ---- Corrida (PRD 8 / 23.5) -------------------------------------

        private void StartRace()
        {
            string playerTeamId = _gm.Database.teams.Count > 0 ? _gm.Database.teams[0].teamId : "";
            var config = QuickRaceBuilder.Build(_gm.Database, _selectedTrack, playerTeamId,
                maxMarbles: 20, defaultGrip: _grip, startEnergy: _startEnergy, startMode: _mode);
            config.laps = _selectedLaps; // duracao escolhida (PRD 3)
            RunRace(config, isChampionship: false);
        }

        /// <summary>Inicia a corrida (compartilhado por Corrida Rapida e Campeonato).</summary>
        private void RunRace(RaceConfig config, bool isChampionship)
        {
            ClearMenuUI();
            ClosePause();
            _gm.CurrentRace = config;
            _gm.RaceIsChampionship = isChampionship;
            _lastConfig = config;
            _lastWasChampionship = isChampionship;

            _raceRoot = new GameObject("RaceRoot");
            var raceGo = new GameObject("RaceManager");
            raceGo.transform.SetParent(_raceRoot.transform, false);
            var race = raceGo.AddComponent<RaceManager>();
            _currentRace = race;
            race.StartRace(config);

            _camera.FrameTrack(race.Track);
            var players = race.PlayerMarbles();
            Transform p1 = players.Count > 0 ? players[0].transform : null;
            Transform p2 = players.Count > 1 ? players[1].transform : null;
            _camera.SetSubjects(p1, p2, () => race.Leader != null ? race.Leader.transform : null);

            var hudGo = new GameObject("RaceHUD");
            hudGo.transform.SetParent(_raceRoot.transform, false);
            var hud = hudGo.AddComponent<RaceHUD>();
            hud.Bind(race, _camera);
            _raceHud = hud;

            // Gravador do replay (PRD extra): registra o movimento + destaques.
            _recorder = _raceRoot.AddComponent<MarbleGP.Race.RaceRecorder>();
            _recorder.Begin(race);

            // Botao de pausa do HUD abre o menu de pausa (PRD 8).
            race.PauseRequested = () => { if (_pausePanel == null) OpenPause(); };

            race.OnRaceFinished += result =>
            {
                if (isChampionship)
                {
                    _gm.Championship?.ApplyResult(result);
                    ShowResults(result, returnToChampionship: true);
                }
                else ShowResults(result, returnToChampionship: false);
            };
        }

        // ---- Tela: Resultado (PRD 23.6) ---------------------------------

        private void ShowResults(RaceResult result, bool returnToChampionship)
        {
            // Registra estatísticas e descobre conquistas novas (PRD 32).
            var newAchievements = AchievementManager.RecordRace(result);

            // Avalia o Desafio do Dia, se esta corrida foi um daily.
            bool dailyDone = false, dailyMet = false;
            if (_dailyActive && _dailyDef != null)
            {
                dailyMet = DailyChallenge.Evaluate(_dailyDef, result, out int dBest, out int dOt, out _);
                UpdateDailyData(_dailyDef, dailyMet, dBest, dOt);
                dailyDone = true;
                _dailyActive = false;
            }

            var canvas = NewCanvas("Results");

            // Titulo + subtitulo (PRD 4).
            var title = UIFactory.Label(canvas.transform, "RESULTADO DA CORRIDA", 52, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.91f), new Vector2(0.95f, 0.99f), UITheme.Gold);
            title.fontStyle = FontStyle.Bold;
            UIFactory.Label(canvas.transform, $"{result.trackName}  ·  {result.laps} voltas", 22,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.872f), new Vector2(0.95f, 0.905f), UITheme.TextDim);

            var winner = result.entries.Count > 0 ? result.entries[0] : null;
            if (winner != null) BuildWinnerBanner(canvas.transform, winner);
            BuildRaceStats(canvas.transform, result);
            BuildResultTable(canvas.transform, result);

            // Faixa central: resultado do Desafio do Dia (prioritario) ou conquista nova.
            if (dailyDone)
            {
                string txt = dailyMet ? "✓  DESAFIO DO DIA CUMPRIDO!"
                                      : "Desafio do dia não cumprido — tente de novo";
                Color col = dailyMet ? MarbleUITheme.NeonGreen : MarbleUITheme.NeonOrange;
                var ban = UIFactory.Panel(canvas.transform, new Vector2(0.24f, 0.615f), new Vector2(0.76f, 0.69f),
                    Vector2.zero, Vector2.zero, new Color(0.05f, 0.12f, 0.06f, 0.96f));
                UIFactory.NeonBorder(ban.gameObject, col, 0.85f, 2f);
                UIFactory.Label(ban, txt, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, col)
                    .fontStyle = FontStyle.Bold;
            }
            else if (newAchievements.Count > 0)
            {
                string txt = newAchievements.Count == 1
                    ? $"★  Nova conquista: {newAchievements[0].title}"
                    : $"★  {newAchievements.Count} novas conquistas desbloqueadas!";
                var ban = UIFactory.Panel(canvas.transform, new Vector2(0.24f, 0.615f), new Vector2(0.76f, 0.69f),
                    Vector2.zero, Vector2.zero, new Color(0.12f, 0.11f, 0.04f, 0.96f));
                UIFactory.NeonBorder(ban.gameObject, MarbleUITheme.NeonGold, 0.85f, 2f);
                UIFactory.Label(ban, txt, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                    MarbleUITheme.NeonGold).fontStyle = FontStyle.Bold;
            }

            // Rodape (PRD 4).
            var menu = UIFactory.Button(canvas.transform, "Voltar ao Menu", UITheme.SecondaryButton,
                new Vector2(0.04f, 0.035f), new Vector2(0.27f, 0.115f), Vector2.zero, Vector2.zero);
            menu.onClick.AddListener(() => { CleanupRace(); ShowMainMenu(); });

            // Ver Replay (se houver gravacao).
            if (_recorder != null && _recorder.Frames.Count >= 2)
            {
                var rep = UIFactory.Button(canvas.transform, "Ver Replay", new Color(0.45f, 0.30f, 0.70f),
                    new Vector2(0.39f, 0.035f), new Vector2(0.61f, 0.115f), Vector2.zero, Vector2.zero);
                rep.onClick.AddListener(StartReplay);
            }

            if (returnToChampionship)
            {
                var next = UIFactory.Button(canvas.transform, "Classificacao / Proxima", UITheme.PrimaryButton,
                    new Vector2(0.73f, 0.035f), new Vector2(0.96f, 0.115f), Vector2.zero, Vector2.zero);
                next.onClick.AddListener(() => { CleanupRace(); ShowChampionshipHub(); });
            }
            else
            {
                var again = UIFactory.Button(canvas.transform, "Correr de Novo", UITheme.PrimaryButton,
                    new Vector2(0.73f, 0.035f), new Vector2(0.96f, 0.115f), Vector2.zero, Vector2.zero);
                again.onClick.AddListener(() => { CleanupRace(); ShowStrategy(); });
            }
        }

        // ---- Replay (PRD extra) -----------------------------------------

        private void StartReplay()
        {
            if (_recorder == null || _recorder.Frames.Count < 2 || _raceRoot == null) return;
            if (_uiRoot != null) _uiRoot.SetActive(false);          // esconde a tela de Resultado
            if (_raceHud != null) _raceHud.gameObject.SetActive(false);

            var go = new GameObject("ReplayPlayer");
            go.transform.SetParent(_raceRoot.transform, false);
            var rp = go.AddComponent<RaceReplayPlayer>();
            rp.Init(_recorder, _camera, () =>
            {
                if (_raceHud != null) _raceHud.gameObject.SetActive(true);
                if (_uiRoot != null) _uiRoot.SetActive(true);       // volta para o Resultado
            });
        }

        private Color TeamColorOf(RaceResultEntry e)
        {
            if (e.isPlayer && _gm.Profile != null) return _gm.Profile.PrimaryColor;
            var t = _gm.Database.GetTeam(e.teamId);
            return t != null ? t.primaryColor : Color.gray;
        }

        private static string FormatTime(float t)
        {
            if (t <= 0f) return "--:--";
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return $"{m}:{s:00.000}";
        }

        private static Color TyreColorByLetter(string s)
        {
            switch (s)
            {
                case "S": return MarbleUITheme.NeonRed;
                case "M": return MarbleUITheme.NeonGold;
                case "H": return new Color32(220, 226, 236, 255);
                case "I": return MarbleUITheme.NeonGreen;
                case "W": case "R": return MarbleUITheme.NeonBlue;
                default: return UITheme.TextDim;
            }
        }

        /// <summary>Banner horizontal do vencedor (estilo transmissao, PRD 4).</summary>
        private void BuildWinnerBanner(Transform canvas, RaceResultEntry w)
        {
            UIFactory.Shadow(canvas, new Vector2(0.22f, 0.71f), new Vector2(0.88f, 0.87f), new Vector2(4f, -6f), 0.35f);
            var card = UIFactory.Panel(canvas, new Vector2(0.22f, 0.71f), new Vector2(0.88f, 0.87f),
                Vector2.zero, Vector2.zero, new Color32(26, 21, 7, 245));
            UIFactory.NeonBorder(card.gameObject, MarbleUITheme.NeonGold, 0.95f, 2.5f);

            UIFactory.Label(card, "1º", 44, TextAnchor.MiddleCenter,
                new Vector2(0.005f, 0.46f), new Vector2(0.1f, 0.96f), MarbleUITheme.NeonGold).fontStyle = FontStyle.Bold;
            UIFactory.Logo(card, w.teamId, new Vector2(0.105f, 0.5f), new Vector2(0.2f, 0.96f));
            var nm = UIFactory.Label(card, w.marbleName, 26, TextAnchor.LowerLeft,
                new Vector2(0.22f, 0.58f), new Vector2(0.8f, 0.97f), Color.white);
            nm.fontStyle = FontStyle.Bold;
            UIFactory.Label(card, w.teamName, 15, TextAnchor.UpperLeft,
                new Vector2(0.22f, 0.46f), new Vector2(0.8f, 0.58f), UITheme.TextDim);

            var wb = UIFactory.Panel(card, new Vector2(0.83f, 0.6f), new Vector2(0.985f, 0.93f),
                Vector2.zero, Vector2.zero, new Color32(44, 35, 9, 240));
            UIFactory.NeonBorder(wb.gameObject, MarbleUITheme.NeonGold, 0.85f, 1.6f);
            UIFactory.Label(wb, "VENCEDOR", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                MarbleUITheme.NeonGold).fontStyle = FontStyle.Bold;

            // Faixa de estatisticas (parte inferior).
            BannerStat(card, "TEMPO", FormatTime(w.totalTime), MarbleUITheme.NeonGold, 0.22f, 0.36f);
            UIFactory.Label(card, "PNEU", 11, TextAnchor.LowerCenter,
                new Vector2(0.37f, 0.04f), new Vector2(0.47f, 0.2f), UITheme.TextDim);
            UIFactory.TyreBadgeSquare(card, w.finalTyre, TyreColorByLetter(w.finalTyre), new Vector2(0.42f, 0.32f), 34f);
            BannerStat(card, "PITS", w.pitStops.ToString(), Color.white, 0.47f, 0.57f);
            BannerStat(card, "COMBUST.", $"{w.finalFuel:0}%", MarbleUITheme.Fuel, 0.58f, 0.7f);
            BannerStat(card, "ENERGIA", $"{w.finalEnergy:0}%", MarbleUITheme.Energy, 0.71f, 0.83f);
            BannerStat(card, "PONTOS", $"+{w.points}", MarbleUITheme.NeonGold, 0.84f, 0.97f);
        }

        private void BannerStat(Transform card, string label, string value, Color color, float xMin, float xMax)
        {
            UIFactory.Label(card, label, 11, TextAnchor.LowerCenter,
                new Vector2(xMin, 0.04f), new Vector2(xMax, 0.2f), UITheme.TextDim);
            UIFactory.Label(card, value, 18, TextAnchor.UpperCenter,
                new Vector2(xMin, 0.2f), new Vector2(xMax, 0.44f), color).fontStyle = FontStyle.Bold;
        }

        /// <summary>Sidebar de estatisticas da corrida (esquerda).</summary>
        private void BuildRaceStats(Transform canvas, RaceResult result)
        {
            var panel = UIFactory.GlassPanel(canvas, new Vector2(0.035f, 0.63f), new Vector2(0.205f, 0.87f));
            UIFactory.Label(panel, "ESTATÍSTICAS", 14, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.99f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;

            var winner = result.entries.Count > 0 ? result.entries[0] : null;
            RaceResultEntry fl = null; float best = float.MaxValue;
            foreach (var e in result.entries)
                if (e.bestLapTime > 0.1f && e.bestLapTime < best) { best = e.bestLapTime; fl = e; }

            StatLine(panel, "TEMPO DE CORRIDA", FormatTime(winner != null ? winner.totalTime : 0f), "", 0.66f, MarbleUITheme.NeonCyan);
            StatLine(panel, "VOLTA MAIS RÁPIDA", fl != null ? FormatTime(best) : "--", fl != null ? fl.marbleName : "", 0.42f, MarbleUITheme.NeonGold);
            StatLine(panel, "CIRCUITO", Trim(result.trackName, 16), $"{result.laps} voltas", 0.18f, Color.white);
        }

        private void StatLine(Transform parent, string label, string value, string sub, float y, Color color)
        {
            UIFactory.Label(parent, label, 11, TextAnchor.UpperLeft,
                new Vector2(0.08f, y + 0.1f), new Vector2(0.94f, y + 0.17f), UITheme.TextDim);
            UIFactory.Label(parent, value, 18, TextAnchor.UpperLeft,
                new Vector2(0.08f, y + 0.01f), new Vector2(0.94f, y + 0.1f), color).fontStyle = FontStyle.Bold;
            if (!string.IsNullOrEmpty(sub))
                UIFactory.Label(parent, sub, 10, TextAnchor.UpperLeft,
                    new Vector2(0.08f, y - 0.04f), new Vector2(0.94f, y + 0.01f), UITheme.TextDim);
        }

        /// <summary>Tabela moderna de classificacao (PRD 4).</summary>
        private void BuildResultTable(Transform canvas, RaceResult result)
        {
            var table = UIFactory.Panel(canvas, new Vector2(0.035f, 0.125f), new Vector2(0.965f, 0.6f),
                Vector2.zero, Vector2.zero, UITheme.BackgroundPanel);
            UIFactory.NeonBorder(table.gameObject, MarbleUITheme.NeonCyan, 0.4f, 1.4f);

            // Cabecalho com colunas alinhadas (Text proprio por coluna, nao
            // depende de fonte monoespacada).
            var header = UIFactory.Panel(table, new Vector2(0.004f, 0.93f), new Vector2(0.996f, 0.996f),
                Vector2.zero, Vector2.zero, UITheme.HeaderPanel);
            Col(header, "P", 15, TextAnchor.MiddleCenter, 0.045f, 0.085f, UITheme.Neon);
            Col(header, "MARBLE", 15, TextAnchor.MiddleLeft, 0.095f, 0.32f, UITheme.Neon);
            Col(header, "EQUIPE", 15, TextAnchor.MiddleLeft, 0.32f, 0.52f, UITheme.Neon);
            Col(header, "PNEU", 15, TextAnchor.MiddleCenter, 0.52f, 0.585f, UITheme.Neon);
            Col(header, "PIT", 15, TextAnchor.MiddleCenter, 0.585f, 0.65f, UITheme.Neon);
            Col(header, "COMB", 15, TextAnchor.MiddleCenter, 0.65f, 0.725f, UITheme.Neon);
            Col(header, "ENER", 15, TextAnchor.MiddleCenter, 0.725f, 0.80f, UITheme.Neon);
            Col(header, "STATUS", 15, TextAnchor.MiddleCenter, 0.80f, 0.92f, UITheme.Neon);
            Col(header, "PTS", 15, TextAnchor.MiddleCenter, 0.92f, 0.985f, UITheme.Neon);

            int rows = result.entries.Count;
            float top = 0.92f, bottom = 0.008f;
            float rowH = (top - bottom) / Mathf.Max(1, rows);
            for (int i = 0; i < rows; i++)
            {
                var e = result.entries[i];
                float yTop = top - i * rowH;
                float yBot = yTop - rowH + 0.003f;

                Color bg = e.position == 1 ? new Color(0.22f, 0.18f, 0.05f, 0.95f)
                         : e.isPlayer ? new Color(0.06f, 0.12f, 0.22f, 0.95f)
                         : (i % 2 == 0 ? new Color(0.11f, 0.12f, 0.16f, 0.92f) : new Color(0.07f, 0.08f, 0.12f, 0.92f));
                var row = UIFactory.Panel(table, new Vector2(0.004f, yBot), new Vector2(0.996f, yTop),
                    Vector2.zero, Vector2.zero, bg);

                // Barra de acento da equipe + logo (se houver).
                var acc = UIFactory.Panel(row, new Vector2(0f, 0.12f), new Vector2(0.006f, 0.88f),
                    Vector2.zero, Vector2.zero, TeamColorOf(e));
                acc.GetComponent<Image>().raycastTarget = false;
                UIFactory.Logo(row, e.teamId, new Vector2(0.010f, 0.1f), new Vector2(0.042f, 0.9f));

                Color txt = e.position == 1 ? UITheme.Gold : (e.isPlayer ? UITheme.PlayerHighlight : Color.white);
                Col(row, e.position.ToString(), 16, TextAnchor.MiddleCenter, 0.045f, 0.085f, txt);
                var nameCol = Col(row, Trim(e.marbleName, 16), 15, TextAnchor.MiddleLeft, 0.095f, 0.32f, txt);
                if (e.position == 1) nameCol.fontStyle = FontStyle.Bold;
                Col(row, Trim(e.teamName, 16), 14, TextAnchor.MiddleLeft, 0.32f, 0.52f, UITheme.TextDim);
                Col(row, e.finalTyre, 15, TextAnchor.MiddleCenter, 0.52f, 0.585f, Color.white);
                Col(row, e.pitStops.ToString(), 15, TextAnchor.MiddleCenter, 0.585f, 0.65f, Color.white);
                Col(row, e.finalFuel.ToString("0"), 14, TextAnchor.MiddleCenter, 0.65f, 0.725f, UITheme.Fuel);
                Col(row, e.finalEnergy.ToString("0"), 14, TextAnchor.MiddleCenter, 0.725f, 0.80f, UITheme.Energy);
                Col(row, Trim(e.statusText, 11), 13, TextAnchor.MiddleCenter, 0.80f, 0.92f, UITheme.TextDim);
                Col(row, e.points.ToString(), 16, TextAnchor.MiddleCenter, 0.92f, 0.985f, txt);
            }
        }

        /// <summary>Coluna de texto da tabela (ancorada por fracao horizontal).</summary>
        private Text Col(Transform parent, string text, int size, TextAnchor anchor, float xMin, float xMax, Color color)
            => UIFactory.Label(parent, text, size, anchor, new Vector2(xMin, 0f), new Vector2(xMax, 1f), color);

        private void CleanupRace()
        {
            ClosePause();
            Time.timeScale = 1f;
            _currentRace = null;
            if (_raceRoot != null) Destroy(_raceRoot);
            _camera.SetOverview();
        }

        // ---- Menu de pausa (PRD 8) --------------------------------------

        private void Update()
        {
            // ESC pausa/despausa durante a corrida (nao na tela de resultado).
            if (_raceRoot != null && _currentRace != null &&
                (_currentRace.State == RaceState.Racing || _currentRace.State == RaceState.Countdown) &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                if (_pausePanel == null) OpenPause(); else ClosePause();
            }
        }

        private void OpenPause()
        {
            Time.timeScale = 0f;
            var canvas = UIFactory.CreateCanvas("PauseMenu");
            _pausePanel = canvas.gameObject;
            // Overlay escuro cobre a tela inteira; o conteudo respeita a safe area.
            UIFactory.Panel(canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.01f, 0.02f, 0.04f, 0.82f));
            var safe = UIFactory.SafeAreaRoot(canvas.transform);

            var box = UIFactory.GlassPanel(safe, new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.84f));
            UIFactory.NeonBorder(box.gameObject, MarbleUITheme.NeonCyan, 0.6f, 2.2f);

            // Coluna esquerda: titulo + botoes.
            UIFactory.Label(box, "PAUSADO", 50, TextAnchor.UpperLeft,
                new Vector2(0.05f, 0.78f), new Vector2(0.5f, 0.96f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;
            UIFactory.Label(box, "Corrida suspensa", 18, TextAnchor.UpperLeft,
                new Vector2(0.055f, 0.71f), new Vector2(0.5f, 0.78f), UITheme.TextDim);

            PauseButton(box, "Continuar", 0, MarbleUITheme.NeonBlue, ClosePause);
            PauseButton(box, "Reiniciar Corrida", 1, UITheme.NeutralButton,
                () => { var c = _lastConfig; bool ch = _lastWasChampionship; CleanupRace(); if (c != null) RunRace(c, ch); });
            PauseButton(box, "Sair para o Menu", 2, UITheme.DangerButton,
                () => { CleanupRace(); ShowMainMenu(); });

            BuildPauseSummary(box);
        }

        private void PauseButton(Transform parent, string label, int index, Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            float yTop = 0.62f - index * 0.135f;
            var btn = UIFactory.Button(parent, label, color,
                new Vector2(0.05f, yTop - 0.1f), new Vector2(0.45f, yTop), Vector2.zero, Vector2.zero);
            btn.GetComponentInChildren<Text>().fontSize = 20;
            btn.onClick.AddListener(onClick);
        }

        /// <summary>Resumo da corrida no lado direito do menu de pausa.</summary>
        private void BuildPauseSummary(Transform box)
        {
            if (_currentRace == null) return;
            var panel = UIFactory.Panel(box, new Vector2(0.5f, 0.06f), new Vector2(0.95f, 0.82f),
                Vector2.zero, Vector2.zero, MarbleUITheme.PanelDark);
            UIFactory.NeonBorder(panel.gameObject, MarbleUITheme.NeonCyan, 0.3f, 1.2f);

            var field = _currentRace.Field;
            int lap = field.Count > 0 ? Mathf.Clamp(field[0].Runtime.completedLaps + 1, 1, _currentRace.TotalLaps) : 1;
            UIFactory.Label(panel, $"VOLTA {lap} / {_currentRace.TotalLaps}", 22, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.88f), new Vector2(0.6f, 0.98f), Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(panel, $"Clima: {_currentRace.WeatherLabelCurrent()}", 15, TextAnchor.UpperRight,
                new Vector2(0.4f, 0.9f), new Vector2(0.94f, 0.98f), UITheme.TextDim);
            UIFactory.Label(panel, _currentRace.SafetyMarbleActive ? "SAFETY MARBLE ATIVO" : "Sem incidentes", 14,
                TextAnchor.UpperRight, new Vector2(0.4f, 0.83f), new Vector2(0.94f, 0.9f),
                _currentRace.SafetyMarbleActive ? MarbleUITheme.Warning : MarbleUITheme.NeonGreen);

            UIFactory.Divider(panel, new Vector2(0.06f, 0.8f), new Vector2(0.94f, 0.805f), MarbleUITheme.NeonCyan);
            UIFactory.Label(panel, "TOP 5", 13, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.72f), new Vector2(0.6f, 0.79f), MarbleUITheme.NeonCyan).fontStyle = FontStyle.Bold;

            int rows = Mathf.Min(5, field.Count);
            for (int i = 0; i < rows; i++)
            {
                var m = field[i].Runtime;
                float yTop = 0.68f - i * 0.12f;
                var row = UIFactory.Panel(panel, new Vector2(0.05f, yTop - 0.1f), new Vector2(0.95f, yTop),
                    Vector2.zero, Vector2.zero, i % 2 == 0 ? MarbleUITheme.PanelSoft : new Color32(8, 16, 28, 200));
                var pcol = UITheme.Medal(i + 1);
                Col(row, (i + 1).ToString(), 16, TextAnchor.MiddleCenter, 0.02f, 0.13f, pcol);
                var lg = UIFactory.Logo(row, m.team != null ? m.team.teamId : "", new Vector2(0.15f, 0.12f), new Vector2(0.27f, 0.88f));
                Col(row, m.driver != null ? m.driver.shortCode : "MAR", 16, TextAnchor.MiddleLeft, 0.3f, 0.62f, Color.white);
                UIFactory.TyreBadgeSquare(row, m.grip != null ? m.grip.gripId.ToString() : "",
                    new Vector2(0.72f, 0.5f), 26f);
                Col(row, $"Combust. {m.fuel:0}%", 13, TextAnchor.MiddleRight, 0.78f, 0.97f, MarbleUITheme.Fuel);
            }
        }

        private void ClosePause()
        {
            if (_pausePanel != null) { Destroy(_pausePanel); _pausePanel = null; }
            if (_raceRoot != null) Time.timeScale = 1f; // so retoma se ainda em corrida
        }

        // ---- Campeonato (PRD 29) ----------------------------------------

        private GameObject _infoPanel;
        private static bool _champTutorialSeen;

        private static string ChampTutorialText()
            => "Você dirige a ESTRATÉGIA da equipe ao longo de várias etapas.\n\n" +
               "•  Cada etapa é uma corrida numa pista diferente do calendário.\n" +
               "•  Pontuação por chegada: P1 = 25, P2 = 18, P3 = 15, ...\n" +
               "•  Há classificação de PILOTOS e de EQUIPES (soma das 2 bolinhas).\n" +
               "•  Correndo você ganha CRÉDITOS para gastar em UPGRADES.\n" +
               "•  Upgrades melhoram pit, energia, desgaste, velocidade e erros.\n" +
               "•  Clique em 'Correr Etapa' para disputar a próxima corrida.\n" +
               "•  O progresso do campeonato é salvo automaticamente após cada etapa.";

        /// <summary>Overlay informativo modal (mini tutorial). Fecha em 'Entendi'.</summary>
        private void ShowInfoOverlay(string title, string body)
        {
            if (_infoPanel != null) { Destroy(_infoPanel); _infoPanel = null; }
            var canvas = UIFactory.CreateCanvas("InfoOverlay");
            _infoPanel = canvas.gameObject;
            var overlay = UIFactory.Panel(canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.82f));
            var safe = UIFactory.SafeAreaRoot(overlay);

            var card = UIFactory.Panel(safe, new Vector2(0.26f, 0.18f), new Vector2(0.74f, 0.82f),
                Vector2.zero, Vector2.zero, UITheme.CardPanel);
            var glow = card.gameObject.AddComponent<Outline>();
            glow.effectColor = UITheme.Neon; glow.effectDistance = new Vector2(2f, 2f);

            UIFactory.Label(card, title, 32, TextAnchor.UpperCenter,
                new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f), Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(card, body, 18, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.17f), new Vector2(0.95f, 0.83f), UITheme.TextDim);

            var ok = UIFactory.Button(card, "Entendi!", UITheme.PrimaryButton,
                new Vector2(0.38f, 0.05f), new Vector2(0.62f, 0.14f), Vector2.zero, Vector2.zero);
            ok.onClick.AddListener(() => { if (_infoPanel != null) { Destroy(_infoPanel); _infoPanel = null; } });
        }

        private void EnsureChampionship()
        {
            if (_gm.Championship == null)
            {
                _gm.Championship = new ChampionshipManager(_gm.Database);
                _gm.Championship.LoadSeason(); // tenta retomar temporada salva
            }
        }

        private void ShowChampionshipHub()
        {
            EnsureChampionship();
            var champ = _gm.Championship;
            var canvas = NewCanvas("Championship");

            UIFactory.Label(canvas.transform, "CAMPEONATO", 44, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), Color.white);

            // Ajuda (mini tutorial do campeonato).
            var champHelp = UIFactory.Button(canvas.transform, "?", new Color(0.22f, 0.45f, 0.85f, 0.95f),
                new Vector2(0.02f, 0.91f), new Vector2(0.06f, 0.975f), Vector2.zero, Vector2.zero);
            champHelp.onClick.AddListener(() => ShowInfoOverlay("CAMPEONATO", ChampTutorialText()));
            if (!_champTutorialSeen) { _champTutorialSeen = true; ShowInfoOverlay("CAMPEONATO", ChampTutorialText()); }

            if (!champ.HasActiveSeason)
            {
                UIFactory.Label(canvas.transform, "Nenhuma temporada em andamento.", 26, TextAnchor.MiddleCenter,
                    new Vector2(0.1f, 0.6f), new Vector2(0.9f, 0.7f), new Color(0.8f, 0.8f, 1f));
                var startBtn = UIFactory.Button(canvas.transform, "Iniciar Temporada", new Color(0.2f, 0.6f, 0.3f),
                    new Vector2(0.35f, 0.45f), new Vector2(0.65f, 0.55f), Vector2.zero, Vector2.zero);
                startBtn.onClick.AddListener(() =>
                {
                    string playerTeamId = _gm.Database.teams.Count > 0 ? _gm.Database.teams[0].teamId : "";
                    champ.StartNewSeason(playerTeamId);
                    AchievementManager.NoteSeasonStart();   // reseta o "claim" do título
                    ShowChampionshipHub();
                });
                BackButton(canvas.transform, ShowMainMenu);
                return;
            }

            // Cabecalho da etapa.
            if (champ.IsSeasonOver)
            {
                var topDriver = champ.DriverStandingsSorted().FirstOrDefault();
                string champName = topDriver != null ? champ.DriverName(topDriver.driverId) : "-";
                UIFactory.Label(canvas.transform, $"Temporada encerrada! Campeao: {champName}", 26,
                    TextAnchor.MiddleCenter, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.9f),
                    new Color(1f, 0.85f, 0.3f));

                // Conquista de campeonato: conta uma vez se o campeao for do jogador.
                string ptid = _gm.Database.teams.Count > 0 ? _gm.Database.teams[0].teamId : "";
                var pdrivers = _gm.Database.GetTeamDrivers(ptid);
                bool playerChamp = topDriver != null && pdrivers != null
                    && pdrivers.Exists(dr => dr.driverId == topDriver.driverId);
                AchievementManager.NoteChampionResult(playerChamp);
            }
            else
            {
                var track = champ.CurrentTrack();
                UIFactory.Label(canvas.transform,
                    $"Etapa {champ.CurrentRound + 1}/{champ.TotalRounds}  -  {track.trackName}", 24,
                    TextAnchor.MiddleCenter, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.9f),
                    new Color(0.8f, 0.9f, 1f));
            }

            // Creditos da equipe (PRD 30).
            UIFactory.Label(canvas.transform, $"Creditos: {champ.Credits}", 22, TextAnchor.MiddleRight,
                new Vector2(0.6f, 0.9f), new Vector2(0.94f, 0.96f), new Color(0.4f, 0.95f, 0.5f));

            BuildStandings(canvas.transform, champ);

            // Botoes.
            var menuBtn = UIFactory.Button(canvas.transform, "Voltar ao Menu", new Color(0.2f, 0.4f, 0.7f),
                new Vector2(0.05f, 0.04f), new Vector2(0.27f, 0.12f), Vector2.zero, Vector2.zero);
            menuBtn.onClick.AddListener(ShowMainMenu);

            var upgBtn = UIFactory.Button(canvas.transform, "Upgrades", new Color(0.45f, 0.35f, 0.7f),
                new Vector2(0.3f, 0.04f), new Vector2(0.52f, 0.12f), Vector2.zero, Vector2.zero);
            upgBtn.onClick.AddListener(ShowUpgrades);

            if (!champ.IsSeasonOver)
            {
                var raceBtn = UIFactory.Button(canvas.transform, "Correr Etapa", new Color(0.85f, 0.4f, 0.2f),
                    new Vector2(0.6f, 0.04f), new Vector2(0.85f, 0.12f), Vector2.zero, Vector2.zero);
                raceBtn.onClick.AddListener(() =>
                {
                    var config = champ.BuildRoundRace();
                    if (config != null) RunRace(config, isChampionship: true);
                });
            }
            else
            {
                var newSeason = UIFactory.Button(canvas.transform, "Nova Temporada", new Color(0.2f, 0.6f, 0.3f),
                    new Vector2(0.6f, 0.04f), new Vector2(0.85f, 0.12f), Vector2.zero, Vector2.zero);
                newSeason.onClick.AddListener(() =>
                {
                    string playerTeamId = _gm.Database.teams.Count > 0 ? _gm.Database.teams[0].teamId : "";
                    champ.StartNewSeason(playerTeamId);
                    AchievementManager.NoteSeasonStart();   // libera contar o proximo titulo
                    ShowChampionshipHub();
                });
            }
        }

        // ---- Tela: Upgrades de equipe (PRD 30) --------------------------

        private void ShowUpgrades()
        {
            EnsureChampionship();
            var champ = _gm.Championship;
            var canvas = NewCanvas("Upgrades");

            UIFactory.Label(canvas.transform, "UPGRADES DA EQUIPE", 40, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), Color.white);
            UIFactory.Label(canvas.transform, $"Creditos: {champ.Credits}", 24, TextAnchor.MiddleRight,
                new Vector2(0.5f, 0.9f), new Vector2(0.94f, 0.97f), new Color(0.4f, 0.95f, 0.5f));

            var types = (UpgradeType[])System.Enum.GetValues(typeof(UpgradeType));
            for (int i = 0; i < types.Length; i++)
                BuildUpgradeRow(canvas.transform, champ, types[i], i);

            BackButton(canvas.transform, ShowChampionshipHub);
        }

        private void BuildUpgradeRow(Transform canvas, ChampionshipManager champ, UpgradeType type, int index)
        {
            float yMax = 0.87f - index * 0.118f;
            var panel = UIFactory.Panel(canvas, new Vector2(0.08f, yMax - 0.105f), new Vector2(0.92f, yMax),
                Vector2.zero, Vector2.zero, UITheme.CardPanel);
            var acc = UIFactory.Panel(panel, new Vector2(0f, 0.14f), new Vector2(0.006f, 0.86f),
                Vector2.zero, Vector2.zero, UITheme.Neon);
            acc.GetComponent<Image>().raycastTarget = false;

            int level = champ.GetLevel(type);
            int max = ChampionshipManager.MaxUpgradeLevel;

            var nameLbl = UIFactory.Label(panel, ChampionshipManager.UpgradeName(type), 22,
                TextAnchor.LowerLeft, new Vector2(0.03f, 0.5f), new Vector2(0.55f, 0.95f), Color.white);
            nameLbl.fontStyle = FontStyle.Bold;
            UIFactory.Label(panel, ChampionshipManager.UpgradeDesc(type), 15,
                TextAnchor.UpperLeft, new Vector2(0.03f, 0.08f), new Vector2(0.62f, 0.48f), UITheme.TextDim);

            // Pips de nivel (0..5).
            UIFactory.Label(panel, $"Nível {level}/{max}", 14, TextAnchor.LowerLeft,
                new Vector2(0.50f, 0.55f), new Vector2(0.69f, 0.92f), UITheme.TextDim);
            BuildLevelPips(panel, level, max, new Vector2(0.50f, 0.2f), new Vector2(0.69f, 0.48f));

            if (champ.IsMaxed(type))
            {
                UIFactory.Label(panel, "MÁX", 24, TextAnchor.MiddleCenter,
                    new Vector2(0.72f, 0.2f), new Vector2(0.97f, 0.8f), UITheme.Success).fontStyle = FontStyle.Bold;
            }
            else
            {
                int cost = champ.UpgradeCost(type);
                bool can = champ.CanUpgrade(type);
                var btn = UIFactory.Button(panel, $"Melhorar ({cost})",
                    can ? UITheme.Success : UITheme.NeutralButton,
                    new Vector2(0.72f, 0.2f), new Vector2(0.97f, 0.8f), Vector2.zero, Vector2.zero);
                btn.interactable = can;
                var captured = type;
                btn.onClick.AddListener(() => { if (champ.BuyUpgrade(captured)) ShowUpgrades(); });
            }
        }

        /// <summary>Pips de nivel (preenchidos = comprados).</summary>
        private void BuildLevelPips(Transform parent, int level, int max, Vector2 aMin, Vector2 aMax)
        {
            float w = (aMax.x - aMin.x) / Mathf.Max(1, max);
            for (int i = 0; i < max; i++)
            {
                float x0 = aMin.x + i * w;
                var pip = UIFactory.Panel(parent, new Vector2(x0 + 0.004f, aMin.y), new Vector2(x0 + w - 0.004f, aMax.y),
                    Vector2.zero, Vector2.zero, i < level ? UITheme.Success : new Color(0.20f, 0.22f, 0.28f, 0.9f));
                pip.GetComponent<Image>().raycastTarget = false;
            }
        }

        private void BuildStandings(Transform canvas, ChampionshipManager champ)
        {
            string playerTeam = champ.Data != null ? champ.Data.playerTeamId : "";

            // ---- Pilotos (esquerda) ----
            var leftPanel = UIFactory.Panel(canvas, new Vector2(0.05f, 0.15f), new Vector2(0.495f, 0.8f),
                Vector2.zero, Vector2.zero, UITheme.BackgroundPanel);
            StandHeader(leftPanel, "PILOTOS");
            var drivers = champ.DriverStandingsSorted();
            float top = 0.9f, bottom = 0.01f;
            float h = (top - bottom) / Mathf.Max(1, drivers.Count);
            for (int i = 0; i < drivers.Count; i++)
            {
                var d = drivers[i];
                StandRow(leftPanel, i + 1, d.teamId, champ.DriverCode(d.driverId), champ.DriverName(d.driverId),
                    d.points, d.wins, top - i * h, top - (i + 1) * h + 0.002f, d.teamId == playerTeam);
            }

            // ---- Equipes (direita) ----
            var rightPanel = UIFactory.Panel(canvas, new Vector2(0.505f, 0.15f), new Vector2(0.95f, 0.8f),
                Vector2.zero, Vector2.zero, UITheme.BackgroundPanel);
            StandHeader(rightPanel, "EQUIPES");
            var teams = champ.TeamStandingsSorted();
            float th = (top - bottom) / Mathf.Max(1, teams.Count);
            for (int i = 0; i < teams.Count; i++)
            {
                var t = teams[i];
                StandRow(rightPanel, i + 1, t.teamId, "", champ.TeamName(t.teamId),
                    t.points, t.wins, top - i * th, top - (i + 1) * th + 0.002f, t.teamId == playerTeam);
            }
        }

        private void StandHeader(Transform panel, string title)
        {
            var header = UIFactory.Panel(panel, new Vector2(0.005f, 0.9f), new Vector2(0.995f, 0.99f),
                Vector2.zero, Vector2.zero, UITheme.HeaderPanel);
            var t = UIFactory.Label(header, title, 18, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0f), new Vector2(0.6f, 1f), UITheme.Neon);
            t.fontStyle = FontStyle.Bold;
            Col(header, "PTS", 13, TextAnchor.MiddleRight, 0.6f, 0.82f, UITheme.Neon);
            Col(header, "V", 13, TextAnchor.MiddleRight, 0.84f, 0.97f, UITheme.Neon);
        }

        private void StandRow(Transform table, int rank, string teamId, string code, string name,
            int points, int wins, float yTop, float yBot, bool highlight)
        {
            Color bg = rank == 1 ? new Color(0.20f, 0.17f, 0.05f, 0.92f)
                     : highlight ? new Color(0.06f, 0.12f, 0.22f, 0.92f)
                     : (rank % 2 == 0 ? new Color(0.10f, 0.11f, 0.15f, 0.85f) : new Color(0.07f, 0.08f, 0.12f, 0.85f));
            var row = UIFactory.Panel(table, new Vector2(0.008f, yBot), new Vector2(0.992f, yTop),
                Vector2.zero, Vector2.zero, bg);

            Color txt = rank == 1 ? UITheme.Gold : (highlight ? UITheme.PlayerHighlight : Color.white);
            Col(row, rank.ToString(), 13, TextAnchor.MiddleCenter, 0.02f, 0.085f, txt);
            UIFactory.Logo(row, teamId, new Vector2(0.10f, 0.1f), new Vector2(0.165f, 0.9f));
            string label = string.IsNullOrEmpty(code) ? name : $"{code}  {name}";
            Col(row, Trim(label, 24), 13, TextAnchor.MiddleLeft, 0.18f, 0.6f, txt);
            Col(row, points.ToString(), 13, TextAnchor.MiddleRight, 0.6f, 0.82f, txt);
            Col(row, $"{wins}V", 12, TextAnchor.MiddleRight, 0.84f, 0.97f, UITheme.TextDim);
        }

        // ---- Garagem (PRD 31) -------------------------------------------

        private static readonly Color[] Palette =
        {
            new Color(0.90f, 0.16f, 0.16f), new Color(0.95f, 0.55f, 0.10f),
            new Color(0.95f, 0.85f, 0.15f), new Color(0.20f, 0.75f, 0.30f),
            new Color(0.15f, 0.55f, 0.90f), new Color(0.55f, 0.30f, 0.85f),
            new Color(0.95f, 0.40f, 0.70f), new Color(0.92f, 0.92f, 0.92f),
            new Color(0.12f, 0.12f, 0.15f)
        };

        private InputField _garageTeamNameField;

        private void ShowGarage()
        {
            var canvas = NewCanvas("Garage");
            var profile = _gm.Profile;

            UIFactory.Label(canvas.transform, "GARAGEM", 44, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), Color.white);

            // Nome da equipe (PRD 31).
            UIFactory.Label(canvas.transform, "Nome da equipe:", 22, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.82f), new Vector2(0.3f, 0.88f), Color.white);
            _garageTeamNameField = InputField(canvas.transform, profile.teamName,
                new Vector2(0.3f, 0.82f), new Vector2(0.62f, 0.88f));
            _garageTeamNameField.text = profile.teamName;

            // Cores da equipe (PRD 31).
            UIFactory.Label(canvas.transform, "Cor primaria:", 20, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.74f), new Vector2(0.24f, 0.8f), Color.white);
            ColorSwatchRow(canvas.transform, new Vector2(0.24f, 0.74f), new Vector2(0.94f, 0.8f),
                profile.PrimaryColor, c => { ApplyTeamName(); profile.PrimaryColor = c; _gm.SaveProfile(); ShowGarage(); });

            UIFactory.Label(canvas.transform, "Cor secundaria:", 20, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.67f), new Vector2(0.24f, 0.73f), Color.white);
            ColorSwatchRow(canvas.transform, new Vector2(0.24f, 0.67f), new Vector2(0.94f, 0.73f),
                profile.SecondaryColor, c => { ApplyTeamName(); profile.SecondaryColor = c; _gm.SaveProfile(); ShowGarage(); });

            // Bolinhas do jogador (PRD 31).
            string playerTeamId = _gm.Database.teams.Count > 0 ? _gm.Database.teams[0].teamId : "";
            var drivers = _gm.Database.GetTeamDrivers(playerTeamId);
            for (int i = 0; i < drivers.Count && i < 2; i++)
            {
                float yMax = 0.6f - i * 0.26f;
                BuildMarbleCard(canvas.transform, drivers[i], i, yMax, profile);
            }

            // Botoes.
            var save = UIFactory.Button(canvas.transform, "Salvar e Voltar", new Color(0.2f, 0.6f, 0.3f),
                new Vector2(0.6f, 0.03f), new Vector2(0.85f, 0.1f), Vector2.zero, Vector2.zero);
            save.onClick.AddListener(() => { ApplyTeamName(); _gm.SaveProfile(); ShowMainMenu(); });
            BackButton(canvas.transform, () => { ApplyTeamName(); _gm.SaveProfile(); ShowMainMenu(); });
        }

        private void ApplyTeamName()
        {
            if (_garageTeamNameField != null && !string.IsNullOrWhiteSpace(_garageTeamNameField.text))
                _gm.Profile.teamName = _garageTeamNameField.text;
        }

        private void BuildMarbleCard(Transform canvas, Data.MarbleDriverSO driver, int index,
            float yMax, Save.PlayerProfile profile)
        {
            var panel = UIFactory.Panel(canvas, new Vector2(0.06f, yMax - 0.24f), new Vector2(0.94f, yMax),
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f));

            Color current = profile.GetMarbleColor(index) ?? profile.PrimaryColor;

            // Preview da cor da bolinha.
            var preview = UIFactory.Panel(panel, new Vector2(0.025f, 0.5f), new Vector2(0.1f, 0.92f),
                Vector2.zero, Vector2.zero, current);
            if (UIFactory.CircleSprite != null) preview.GetComponent<Image>().sprite = UIFactory.CircleSprite;

            var nameLbl = UIFactory.Label(panel, $"#{driver.number}  {driver.marbleName}  ({driver.shortCode})", 22,
                TextAnchor.MiddleLeft, new Vector2(0.12f, 0.55f), new Vector2(0.95f, 0.92f), Color.white);
            nameLbl.fontStyle = FontStyle.Bold;
            UIFactory.Label(panel, "Escolha a cor da bolinha:", 15,
                TextAnchor.UpperLeft, new Vector2(0.12f, 0.42f), new Vector2(0.95f, 0.55f), UITheme.TextDim);

            int captured = index;
            ColorSwatchRow(panel, new Vector2(0.12f, 0.08f), new Vector2(0.95f, 0.40f),
                current, c => { ApplyTeamName(); profile.SetMarbleColor(captured, c); _gm.SaveProfile(); ShowGarage(); });
        }

        private void ColorSwatchRow(Transform parent, Vector2 min, Vector2 max, Color current,
            System.Action<Color> onPick)
        {
            int n = Palette.Length;
            float w = (max.x - min.x) / n;
            for (int i = 0; i < n; i++)
            {
                float x0 = min.x + i * w;
                var col = Palette[i];
                var btn = UIFactory.Button(parent, "", col,
                    new Vector2(x0 + 0.004f, min.y), new Vector2(x0 + w - 0.004f, max.y),
                    Vector2.zero, Vector2.zero);
                // Marca a cor selecionada com um check.
                if (ApproxColor(col, current))
                    UIFactory.Label(btn.transform, "✓", 22, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.one, Color.black);
                var captured = col;
                btn.onClick.AddListener(() => onPick(captured));
            }
        }

        private static bool ApproxColor(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;

        // ---- Helpers de UI ----------------------------------------------

        private void BackButton(Transform parent, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIFactory.Button(parent, "Voltar", new Color(0.4f, 0.4f, 0.4f),
                new Vector2(0.02f, 0.02f), new Vector2(0.12f, 0.08f), Vector2.zero, Vector2.zero);
            btn.onClick.AddListener(onClick);
        }

        private InputField InputField(Transform parent, string placeholder, Vector2 min, Vector2 max)
        {
            var panel = UIFactory.Panel(parent, min, max, Vector2.zero, Vector2.zero, Color.white);
            var go = panel.gameObject;
            var input = go.AddComponent<InputField>();
            var text = UIFactory.Label(panel, "", 24, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0f), new Vector2(1f, 1f), Color.black);
            var ph = UIFactory.Label(panel, placeholder, 24, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0f), new Vector2(1f, 1f), new Color(0.5f, 0.5f, 0.5f));
            input.textComponent = text;
            input.placeholder = ph;
            return input;
        }

        private void ShowFatal(string msg)
        {
            var canvas = NewCanvas("Fatal");
            UIFactory.Label(canvas.transform, msg, 28, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f), new Color(1f, 0.6f, 0.6f));
        }

        private static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n));
    }
}
