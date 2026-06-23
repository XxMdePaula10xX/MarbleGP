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
            UIFactory.Label(canvas.transform, $"Estratégia — {_selectedTrack.trackName}", 36, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.95f), Color.white);

            // Previsao do tempo (PRD 10/19): chance de chuva + mudancas previstas.
            int rainPct = Mathf.RoundToInt(_selectedTrack.rainChance * 100f);
            int maxChanges = _selectedLaps <= 6 ? 1 : (_selectedLaps <= 15 ? 2 : 3);
            string confianca = rainPct >= 40 ? "instável" : rainPct >= 15 ? "moderada" : "estável";
            UIFactory.Label(canvas.transform,
                $"Previsão: chuva {rainPct}%   ·   até {maxChanges} mudança(s) de clima   ·   {confianca}",
                22, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.85f),
                rainPct >= 30 ? new Color(0.5f, 0.7f, 1f) : new Color(0.8f, 0.85f, 0.9f));

            UIFactory.Icon(canvas.transform, "tyre", new Vector2(0.075f, 0.715f), new Vector2(0.115f, 0.785f), Color.white);
            var lblGrip = UIFactory.Label(canvas.transform, "Anel de aderência:", 22, TextAnchor.MiddleLeft,
                new Vector2(0.125f, 0.71f), new Vector2(0.255f, 0.79f), Color.white);
            lblGrip.fontStyle = FontStyle.Bold;
            GripButton(canvas.transform, GripType.Soft, "Soft", 0);
            GripButton(canvas.transform, GripType.Medium, "Medium", 1);
            GripButton(canvas.transform, GripType.Hard, "Hard", 2);
            GripButton(canvas.transform, GripType.Intermediate, "Inter", 3);
            GripButton(canvas.transform, GripType.Rain, "Rain", 4);

            UIFactory.Icon(canvas.transform, "mode", new Vector2(0.075f, 0.555f), new Vector2(0.115f, 0.625f), Color.white);
            var lblMode = UIFactory.Label(canvas.transform, "Modo inicial:", 22, TextAnchor.MiddleLeft,
                new Vector2(0.125f, 0.55f), new Vector2(0.255f, 0.63f), Color.white);
            lblMode.fontStyle = FontStyle.Bold;
            ModeButton(canvas.transform, RaceMode.Save, "Save", 0);
            ModeButton(canvas.transform, RaceMode.Normal, "Normal", 1);
            ModeButton(canvas.transform, RaceMode.Push, "Push", 2);

            // Duracao da corrida (PRD 3 / 13).
            UIFactory.Icon(canvas.transform, "laps", new Vector2(0.075f, 0.455f), new Vector2(0.115f, 0.525f), Color.white);
            var lblDur = UIFactory.Label(canvas.transform, "Duração:", 22, TextAnchor.MiddleLeft,
                new Vector2(0.125f, 0.45f), new Vector2(0.255f, 0.53f), Color.white);
            lblDur.fontStyle = FontStyle.Bold;
            LapButton(canvas.transform, 5, "Rapido (5)", 0);
            LapButton(canvas.transform, 12, "Normal (12)", 1);
            LapButton(canvas.transform, 20, "Longo (20)", 2);

            // Resumo da estrategia.
            int stops = _selectedLaps >= 18 ? 2 : 1;
            string summary =
                $"Circuito: {_selectedTrack.trackName}    Voltas: {_selectedLaps}    Clima inicial: Seco\n" +
                $"Pneu: {_grip}    Modo: {_mode}    Combustível: 100    Energia: 100\n" +
                $"Paradas previstas: ~{stops}   (combustível não chega ao fim sem parar)";
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
            float xMin = 0.26f + col * 0.16f;
            var btn = UIFactory.Button(parent, label,
                _selectedLaps == laps ? new Color(0.2f, 0.6f, 0.85f) : new Color(0.3f, 0.3f, 0.4f),
                new Vector2(xMin, 0.45f), new Vector2(xMin + 0.14f, 0.53f), Vector2.zero, Vector2.zero);
            btn.onClick.AddListener(() => { _selectedLaps = laps; ShowStrategy(); });
        }

        private void GripButton(Transform parent, GripType g, string label, int col)
        {
            float xMin = 0.26f + col * 0.135f;
            var btn = UIFactory.Button(parent, label,
                _grip == g ? new Color(0.9f, 0.6f, 0.2f) : new Color(0.3f, 0.3f, 0.4f),
                new Vector2(xMin, 0.71f), new Vector2(xMin + 0.105f, 0.79f), Vector2.zero, Vector2.zero);
            btn.onClick.AddListener(() => { _grip = g; ShowStrategy(); });
        }

        private void ModeButton(Transform parent, RaceMode m, string label, int col)
        {
            float xMin = 0.26f + col * 0.16f;
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
            UIFactory.Panel(canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.7f));

            UIFactory.Label(canvas.transform, "PAUSA", 56, TextAnchor.MiddleCenter,
                new Vector2(0.3f, 0.74f), new Vector2(0.7f, 0.86f), Color.white).fontStyle = FontStyle.Bold;

            PauseButton(canvas.transform, "Continuar", 0, new Color(0.2f, 0.6f, 0.3f), ClosePause);
            PauseButton(canvas.transform, "Reiniciar Corrida", 1, new Color(0.2f, 0.45f, 0.8f),
                () => { var c = _lastConfig; bool ch = _lastWasChampionship; CleanupRace(); if (c != null) RunRace(c, ch); });
            // "Sair" da corrida volta ao menu (nao fecha o jogo). O progresso da
            // corrida nao e salvo (apenas o campeonato, ao fim de cada etapa).
            PauseButton(canvas.transform, "Sair para o Menu", 2, new Color(0.7f, 0.3f, 0.25f),
                () => { CleanupRace(); ShowMainMenu(); });
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

            var card = UIFactory.Panel(overlay, new Vector2(0.26f, 0.18f), new Vector2(0.74f, 0.82f),
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
