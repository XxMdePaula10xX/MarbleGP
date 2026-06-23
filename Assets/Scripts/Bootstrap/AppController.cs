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

        // Selecoes correntes do fluxo.
        private TrackDataSO _selectedTrack;
        private GripType _grip = GripType.Medium;
        private RaceMode _mode = RaceMode.Normal;
        private float _startEnergy = 100f;
        private int _selectedLaps = 5; // Rapido/Normal/Longo (PRD 3)

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

        private Canvas NewCanvas(string name)
        {
            ClearMenuUI();
            var canvas = UIFactory.CreateCanvas(name);
            _uiRoot = canvas.gameObject;
            // Fundo da tela (imagem opcional em Resources/Backgrounds/<key> + fallback escuro).
            UIFactory.Background(canvas.transform, BackgroundKey(name), new Color(0.06f, 0.07f, 0.11f, 1f));
            return canvas;
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
            var title = UIFactory.Label(canvas.transform, "MARBLE GP MANAGER", 66, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.96f), Color.white);
            title.fontStyle = FontStyle.Bold;
            UIFactory.Label(canvas.transform, "STRATEGY RACING CHAMPIONSHIP", 24, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.84f), new Color(0.55f, 0.8f, 1f));
            UIFactory.Label(canvas.transform, $"Equipe: {_gm.Profile.teamName}", 22, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.73f), new Vector2(0.9f, 0.78f), new Color(0.85f, 0.85f, 0.95f));

            MenuButton(canvas.transform, "Corrida Rapida", 0, () => ShowTrackSelect());
            MenuButton(canvas.transform, "Campeonato", 1, () => ShowChampionshipHub());
            MenuButton(canvas.transform, "Garagem", 2, () => ShowGarage());
            MenuButton(canvas.transform, "Sair", 3, () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private void MenuButton(Transform parent, string label, int index, UnityEngine.Events.UnityAction onClick, bool disabled = false)
        {
            float yMax = 0.66f - index * 0.1f;
            var color = disabled ? new Color(0.3f, 0.3f, 0.3f, 0.6f) : new Color(0.2f, 0.4f, 0.7f, 0.95f);
            var btn = UIFactory.Button(parent, label, color,
                new Vector2(0.32f, yMax - 0.08f), new Vector2(0.68f, yMax), Vector2.zero, Vector2.zero);
            btn.interactable = !disabled;
            if (onClick != null) btn.onClick.AddListener(onClick);
        }

        // ---- Tela: Selecao de pista (PRD 7.3 / 23.3) --------------------

        private void ShowTrackSelect()
        {
            var canvas = NewCanvas("TrackSelect");
            UIFactory.Label(canvas.transform, "Escolha o Circuito", 40, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.89f), new Vector2(0.9f, 0.97f), Color.white);

            // Grade de cards (2 colunas) com thumbnail do circuito (PRD 7.2).
            var tracks = _gm.Database.tracks;
            float top = 0.83f, h = 0.135f, gap = 0.022f;
            int i = 0;
            foreach (var t in tracks)
            {
                int col = i % 2;
                int rowIdx = i / 2;
                float yTop = top - rowIdx * (h + gap);
                float yBot = yTop - h;
                if (yBot < 0.05f) break; // nao desenha alem da tela (campeonato tem muitos)
                float xMin = col == 0 ? 0.075f : 0.515f;
                float xMax = col == 0 ? 0.485f : 0.925f;

                bool locked = t.trackLocked;
                var card = UIFactory.Button(canvas.transform, "",
                    locked ? new Color(0.15f, 0.16f, 0.20f, 0.92f) : new Color(0.13f, 0.22f, 0.32f, 0.95f),
                    new Vector2(xMin, yBot), new Vector2(xMax, yTop), Vector2.zero, Vector2.zero);
                card.interactable = !locked;
                var captured = t;
                if (!locked) card.onClick.AddListener(() => { _selectedTrack = captured; ShowStrategy(); });

                UIFactory.Thumbnail(card.transform, t.trackId, new Vector2(0.02f, 0.12f), new Vector2(0.26f, 0.88f));

                var title = UIFactory.Label(card.transform, t.trackName, 22, TextAnchor.LowerLeft,
                    new Vector2(0.30f, 0.46f), new Vector2(0.98f, 0.9f), locked ? UITheme.TextDim : Color.white);
                title.fontStyle = FontStyle.Bold;
                title.raycastTarget = false;

                string info = locked
                    ? "Bloqueado · vence o campeonato"
                    : $"{t.difficulty} · {t.recommendedLaps}v · chuva {Mathf.RoundToInt(t.rainChance * 100f)}%";
                var lab = UIFactory.Label(card.transform, info, 16, TextAnchor.UpperLeft,
                    new Vector2(0.30f, 0.12f), new Vector2(0.98f, 0.46f),
                    locked ? new Color(0.6f, 0.62f, 0.7f) : UITheme.TextDim);
                lab.raycastTarget = false;
                i++;
            }

            BackButton(canvas.transform, ShowMainMenu);
        }

        // ---- Tela: Estrategia pre-corrida (PRD 7.3 / 23.4) --------------

        private void ShowStrategy()
        {
            var canvas = NewCanvas("Strategy");
            UIFactory.Label(canvas.transform, $"Estrategia - {_selectedTrack.trackName}", 36, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.95f), Color.white);

            // Previsao do tempo (PRD 10/19): chance de chuva + mudancas previstas.
            int rainPct = Mathf.RoundToInt(_selectedTrack.rainChance * 100f);
            int maxChanges = _selectedLaps <= 6 ? 1 : (_selectedLaps <= 15 ? 2 : 3);
            string confianca = rainPct >= 40 ? "instavel" : rainPct >= 15 ? "moderada" : "estavel";
            UIFactory.Label(canvas.transform,
                $"Previsao: chuva {rainPct}%  ·  ate {maxChanges} mudanca(s) de clima  ·  {confianca}",
                22, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.85f),
                rainPct >= 30 ? new Color(0.5f, 0.7f, 1f) : new Color(0.8f, 0.85f, 0.9f));

            UIFactory.Icon(canvas.transform, "tyre", new Vector2(0.045f, 0.72f), new Vector2(0.075f, 0.78f), Color.white);
            UIFactory.Label(canvas.transform, "Anel de aderencia:", 24, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.72f), new Vector2(0.4f, 0.78f), Color.white);
            GripButton(canvas.transform, GripType.Soft, "Soft", 0);
            GripButton(canvas.transform, GripType.Medium, "Medium", 1);
            GripButton(canvas.transform, GripType.Hard, "Hard", 2);
            GripButton(canvas.transform, GripType.Intermediate, "Inter", 3);
            GripButton(canvas.transform, GripType.Rain, "Rain", 4);

            UIFactory.Icon(canvas.transform, "mode", new Vector2(0.045f, 0.56f), new Vector2(0.075f, 0.62f), Color.white);
            UIFactory.Label(canvas.transform, "Modo inicial:", 24, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.56f), new Vector2(0.4f, 0.62f), Color.white);
            ModeButton(canvas.transform, RaceMode.Save, "Save", 0);
            ModeButton(canvas.transform, RaceMode.Normal, "Normal", 1);
            ModeButton(canvas.transform, RaceMode.Push, "Push", 2);

            // Duracao da corrida (PRD 3 / 13).
            UIFactory.Icon(canvas.transform, "laps", new Vector2(0.045f, 0.46f), new Vector2(0.075f, 0.52f), Color.white);
            UIFactory.Label(canvas.transform, "Duracao:", 24, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.46f), new Vector2(0.4f, 0.52f), Color.white);
            LapButton(canvas.transform, 5, "Rapido (5)", 0);
            LapButton(canvas.transform, 12, "Normal (12)", 1);
            LapButton(canvas.transform, 20, "Longo (20)", 2);

            // Resumo da estrategia.
            int stops = _selectedLaps >= 18 ? 2 : 1;
            string summary =
                $"Circuito: {_selectedTrack.trackName}    Voltas: {_selectedLaps}    Clima inicial: Seco\n" +
                $"Pneu: {_grip}    Modo: {_mode}    Combustivel: 100    Energia: 100\n" +
                $"Paradas previstas: ~{stops}   (combustivel nao chega ao fim sem parar)";
            var sumPanel = UIFactory.Panel(canvas.transform, new Vector2(0.2f, 0.24f), new Vector2(0.8f, 0.38f),
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.5f));
            UIFactory.Label(sumPanel, summary, 18, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), new Color(0.85f, 0.9f, 1f));

            var start = UIFactory.Button(canvas.transform, "INICIAR CORRIDA", new Color(0.9f, 0.45f, 0.15f),
                new Vector2(0.34f, 0.08f), new Vector2(0.66f, 0.19f), Vector2.zero, Vector2.zero);
            start.onClick.AddListener(StartRace);

            BackButton(canvas.transform, ShowTrackSelect);
        }

        private void LapButton(Transform parent, int laps, string label, int col)
        {
            float xMin = 0.42f + col * 0.16f;
            var btn = UIFactory.Button(parent, label,
                _selectedLaps == laps ? new Color(0.2f, 0.6f, 0.85f) : new Color(0.3f, 0.3f, 0.4f),
                new Vector2(xMin, 0.45f), new Vector2(xMin + 0.14f, 0.53f), Vector2.zero, Vector2.zero);
            btn.onClick.AddListener(() => { _selectedLaps = laps; ShowStrategy(); });
        }

        private void GripButton(Transform parent, GripType g, string label, int col)
        {
            float xMin = 0.42f + col * 0.115f;
            var btn = UIFactory.Button(parent, label,
                _grip == g ? new Color(0.9f, 0.6f, 0.2f) : new Color(0.3f, 0.3f, 0.4f),
                new Vector2(xMin, 0.71f), new Vector2(xMin + 0.105f, 0.79f), Vector2.zero, Vector2.zero);
            btn.onClick.AddListener(() => { _grip = g; ShowStrategy(); });
        }

        private void ModeButton(Transform parent, RaceMode m, string label, int col)
        {
            float xMin = 0.42f + col * 0.16f;
            var btn = UIFactory.Button(parent, label,
                _mode == m ? new Color(0.2f, 0.7f, 0.4f) : new Color(0.3f, 0.3f, 0.4f),
                new Vector2(xMin, 0.55f), new Vector2(xMin + 0.14f, 0.63f), Vector2.zero, Vector2.zero);
            btn.onClick.AddListener(() => { _mode = m; ShowStrategy(); });
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
            var canvas = NewCanvas("Results");

            // Titulo + subtitulo (PRD 4).
            var title = UIFactory.Label(canvas.transform, "RESULTADO DA CORRIDA", 52, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.91f), new Vector2(0.95f, 0.99f), Color.white);
            title.fontStyle = FontStyle.Bold;
            UIFactory.Label(canvas.transform, $"{result.trackName}  ·  {result.laps} voltas", 24,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.865f), new Vector2(0.95f, 0.905f), UITheme.TextDim);

            var winner = result.entries.Count > 0 ? result.entries[0] : null;
            if (winner != null) BuildWinnerCard(canvas.transform, winner);

            BuildResultTable(canvas.transform, result);

            // Rodape (PRD 4).
            var menu = UIFactory.Button(canvas.transform, "Voltar ao Menu", UITheme.SecondaryButton,
                new Vector2(0.18f, 0.035f), new Vector2(0.42f, 0.115f), Vector2.zero, Vector2.zero);
            menu.onClick.AddListener(() => { CleanupRace(); ShowMainMenu(); });

            if (returnToChampionship)
            {
                var next = UIFactory.Button(canvas.transform, "Classificacao / Proxima", UITheme.PrimaryButton,
                    new Vector2(0.58f, 0.035f), new Vector2(0.82f, 0.115f), Vector2.zero, Vector2.zero);
                next.onClick.AddListener(() => { CleanupRace(); ShowChampionshipHub(); });
            }
            else
            {
                var again = UIFactory.Button(canvas.transform, "Correr de Novo", UITheme.PrimaryButton,
                    new Vector2(0.58f, 0.035f), new Vector2(0.82f, 0.115f), Vector2.zero, Vector2.zero);
                again.onClick.AddListener(() => { CleanupRace(); ShowStrategy(); });
            }
        }

        private Color TeamColorOf(RaceResultEntry e)
        {
            if (e.isPlayer && _gm.Profile != null) return _gm.Profile.PrimaryColor;
            var t = _gm.Database.GetTeam(e.teamId);
            return t != null ? t.primaryColor : Color.gray;
        }

        /// <summary>Card do vencedor em destaque dourado (PRD 4).</summary>
        private void BuildWinnerCard(Transform canvas, RaceResultEntry w)
        {
            var card = UIFactory.Panel(canvas, new Vector2(0.24f, 0.69f), new Vector2(0.76f, 0.85f),
                Vector2.zero, Vector2.zero, new Color(0.12f, 0.10f, 0.04f, 0.95f));
            var glow = card.gameObject.AddComponent<Outline>();
            glow.effectColor = UITheme.Gold; glow.effectDistance = new Vector2(3f, 3f);

            // Barra lateral na cor da equipe.
            var bar = UIFactory.Panel(card, new Vector2(0f, 0f), new Vector2(0.02f, 1f),
                Vector2.zero, Vector2.zero, TeamColorOf(w));

            UIFactory.Label(card, "★", 60, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.1f), new Vector2(0.16f, 0.9f), UITheme.Gold);

            // Logo da equipe (Resources/Logos/{teamId}); nada se nao existir.
            UIFactory.Logo(card, w.teamId, new Vector2(0.80f, 0.50f), new Vector2(0.96f, 0.92f));

            var name = UIFactory.Label(card, $"P1  {w.marbleName}", 30, TextAnchor.LowerLeft,
                new Vector2(0.18f, 0.5f), new Vector2(0.98f, 0.95f), UITheme.Gold);
            name.fontStyle = FontStyle.Bold;
            UIFactory.Label(card, w.teamName, 20, TextAnchor.UpperLeft,
                new Vector2(0.18f, 0.32f), new Vector2(0.7f, 0.55f), UITheme.TextDim);

            string stats = $"Pneu {w.finalTyre}   ·   Pits {w.pitStops}   ·   " +
                           $"Comb {w.finalFuel:0}   ·   Energia {w.finalEnergy:0}   ·   {w.points} pts";
            UIFactory.Label(card, stats, 18, TextAnchor.UpperLeft,
                new Vector2(0.18f, 0.05f), new Vector2(0.98f, 0.32f), Color.white);
        }

        /// <summary>Tabela moderna de classificacao (PRD 4).</summary>
        private void BuildResultTable(Transform canvas, RaceResult result)
        {
            var table = UIFactory.Panel(canvas, new Vector2(0.06f, 0.135f), new Vector2(0.94f, 0.66f),
                Vector2.zero, Vector2.zero, UITheme.BackgroundPanel);

            // Cabecalho fixo no topo.
            UIFactory.Label(table, $" {"P",-3}{"Marble",-9}{"Equipe",-15}{"Tyre",-5}{"Pit",-4}{"Fuel",-6}{"Ener",-6}{"Status",-11}{"Pts",-4}",
                17, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.92f), new Vector2(0.99f, 1f), UITheme.Neon);

            int rows = result.entries.Count;
            float area = 0.9f;                       // area abaixo do cabecalho (0..0.9)
            float rowH = area / Mathf.Max(1, rows);
            for (int i = 0; i < rows; i++)
            {
                var e = result.entries[i];
                float yTop = 0.9f - i * rowH;
                float yBot = yTop - rowH + 0.002f;

                Color bg = e.position == 1 ? new Color(0.22f, 0.18f, 0.05f, 0.95f)
                         : e.isPlayer ? new Color(0.06f, 0.12f, 0.22f, 0.95f)
                         : (i % 2 == 0 ? new Color(0.10f, 0.11f, 0.15f, 0.6f) : new Color(0.07f, 0.08f, 0.12f, 0.6f));
                var row = UIFactory.Panel(table, new Vector2(0.005f, yBot), new Vector2(0.995f, yTop),
                    Vector2.zero, Vector2.zero, bg);

                // Barra lateral cor da equipe.
                UIFactory.Panel(row, new Vector2(0f, 0.1f), new Vector2(0.012f, 0.9f),
                    Vector2.zero, Vector2.zero, TeamColorOf(e));

                Color txt = e.position == 1 ? UITheme.Gold : (e.isPlayer ? UITheme.PlayerHighlight : Color.white);
                string line = $" {e.position,-3}{Trim(e.marbleName, 8),-9}{Trim(e.teamName, 14),-15}" +
                              $"{e.finalTyre,-5}{e.pitStops,-4}{e.finalFuel,4:0}  {e.finalEnergy,4:0}  " +
                              $"{Trim(e.statusText, 10),-11}{e.points,-4}";
                UIFactory.Label(row, line, 15, TextAnchor.MiddleLeft,
                    new Vector2(0.02f, 0f), new Vector2(0.99f, 1f), txt);
            }
        }

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
            UIFactory.Panel(canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.7f));

            UIFactory.Label(canvas.transform, "PAUSA", 56, TextAnchor.MiddleCenter,
                new Vector2(0.3f, 0.74f), new Vector2(0.7f, 0.86f), Color.white).fontStyle = FontStyle.Bold;

            PauseButton(canvas.transform, "Continuar", 0, new Color(0.2f, 0.6f, 0.3f), ClosePause);
            PauseButton(canvas.transform, "Reiniciar Corrida", 1, new Color(0.2f, 0.45f, 0.8f),
                () => { var c = _lastConfig; bool ch = _lastWasChampionship; CleanupRace(); if (c != null) RunRace(c, ch); });
            PauseButton(canvas.transform, "Voltar ao Menu", 2, new Color(0.5f, 0.4f, 0.7f),
                () => { CleanupRace(); ShowMainMenu(); });
            PauseButton(canvas.transform, "Sair", 3, new Color(0.7f, 0.25f, 0.2f), () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private void PauseButton(Transform parent, string label, int index, Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            float yMax = 0.62f - index * 0.11f;
            var btn = UIFactory.Button(parent, label, color,
                new Vector2(0.36f, yMax - 0.09f), new Vector2(0.64f, yMax), Vector2.zero, Vector2.zero);
            btn.onClick.AddListener(onClick);
        }

        private void ClosePause()
        {
            if (_pausePanel != null) { Destroy(_pausePanel); _pausePanel = null; }
            if (_raceRoot != null) Time.timeScale = 1f; // so retoma se ainda em corrida
        }

        // ---- Campeonato (PRD 29) ----------------------------------------

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
            float yMax = 0.86f - index * 0.11f;
            var panel = UIFactory.Panel(canvas, new Vector2(0.08f, yMax - 0.1f), new Vector2(0.92f, yMax),
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f));

            int level = champ.GetLevel(type);

            UIFactory.Label(panel,
                $"{ChampionshipManager.UpgradeName(type)}   Nivel {level}/{ChampionshipManager.MaxUpgradeLevel}", 22,
                TextAnchor.MiddleLeft, new Vector2(0.02f, 0.5f), new Vector2(0.7f, 1f), Color.white);
            UIFactory.Label(panel, ChampionshipManager.UpgradeDesc(type), 16,
                TextAnchor.MiddleLeft, new Vector2(0.02f, 0f), new Vector2(0.7f, 0.5f),
                new Color(0.8f, 0.85f, 0.95f));

            if (champ.IsMaxed(type))
            {
                UIFactory.Label(panel, "MAX", 24, TextAnchor.MiddleCenter,
                    new Vector2(0.72f, 0f), new Vector2(0.98f, 1f), new Color(0.5f, 0.9f, 0.6f));
            }
            else
            {
                int cost = champ.UpgradeCost(type);
                bool can = champ.CanUpgrade(type);
                var btn = UIFactory.Button(panel,
                    $"Melhorar ({cost})",
                    can ? new Color(0.2f, 0.6f, 0.3f) : new Color(0.3f, 0.3f, 0.35f),
                    new Vector2(0.72f, 0.2f), new Vector2(0.98f, 0.8f), Vector2.zero, Vector2.zero);
                btn.interactable = can;
                var captured = type;
                btn.onClick.AddListener(() => { if (champ.BuyUpgrade(captured)) ShowUpgrades(); });
            }
        }

        private void BuildStandings(Transform canvas, ChampionshipManager champ)
        {
            // Pilotos (esquerda).
            var leftPanel = UIFactory.Panel(canvas, new Vector2(0.06f, 0.16f), new Vector2(0.5f, 0.8f),
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
            var sbD = new StringBuilder("PILOTOS\n");
            int rank = 1;
            foreach (var d in champ.DriverStandingsSorted())
                sbD.AppendLine($"{rank++,2}. {champ.DriverCode(d.driverId)}  {Trim(champ.TeamName(d.teamId), 14),-14} {d.points,3} pts  ({d.wins}V)");
            UIFactory.Label(leftPanel, sbD.ToString(), 19, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0f), new Vector2(1f, 0.98f), Color.white);

            // Equipes (direita).
            var rightPanel = UIFactory.Panel(canvas, new Vector2(0.52f, 0.16f), new Vector2(0.94f, 0.8f),
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
            var sbT = new StringBuilder("EQUIPES\n");
            rank = 1;
            foreach (var t in champ.TeamStandingsSorted())
                sbT.AppendLine($"{rank++,2}. {Trim(champ.TeamName(t.teamId), 18),-18} {t.points,3} pts  ({t.wins}V)");
            UIFactory.Label(rightPanel, sbT.ToString(), 19, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0f), new Vector2(1f, 0.98f), Color.white);
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
            var preview = UIFactory.Panel(panel, new Vector2(0.02f, 0.55f), new Vector2(0.1f, 0.95f),
                Vector2.zero, Vector2.zero, current);

            UIFactory.Label(panel, $"#{driver.number}  {driver.marbleName}  ({driver.shortCode})", 22,
                TextAnchor.MiddleLeft, new Vector2(0.12f, 0.6f), new Vector2(0.95f, 0.95f), Color.white);

            UIFactory.Label(panel,
                $"VEL {driver.speed}  ACE {driver.acceleration}  CTR {driver.control}  " +
                $"AGR {driver.aggression}  DEF {driver.defense}  CON {driver.consistency}\n" +
                $"PNE {driver.tireManagement}  ENE {driver.energyManagement}  PIT {driver.pitSkill}  " +
                $"Personalidade: {driver.personality}", 16,
                TextAnchor.UpperLeft, new Vector2(0.12f, 0.32f), new Vector2(0.95f, 0.62f),
                new Color(0.8f, 0.85f, 0.95f));

            int captured = index;
            ColorSwatchRow(panel, new Vector2(0.12f, 0.05f), new Vector2(0.95f, 0.28f),
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
