using System;
using System.Collections.Generic;
using UnityEngine;
using MarbleGP.AI;
using MarbleGP.Core;
using MarbleGP.Data;
using MarbleGP.Systems;
using MarbleGP.Track;

namespace MarbleGP.Race
{
    /// <summary>
    /// Orquestrador da corrida (PRD 24.2). Monta a pista e as bolinhas, conduz
    /// a contagem regressiva, roda o loop de simulacao (IA + fisica + sistemas),
    /// controla voltas/posicoes/pit e gera o resultado final.
    /// Toda a logica de corrida fica separada da UI (PRD 39.14).
    /// </summary>
    public class RaceManager : MonoBehaviour, IRaceConditions
    {
        [Header("Dados")]
        [SerializeField] private GameDatabase database;

        public RaceState State { get; private set; } = RaceState.PreRace;
        public TrackManager Track { get; private set; }
        public IReadOnlyList<MarbleController> Field => _field;
        public MarbleController Leader => _field.Count > 0 ? _field[0] : null;
        public RaceConfig Config { get; private set; }

        /// <summary>Controllers das bolinhas do jogador (para camera/HUD).</summary>
        public List<MarbleController> PlayerMarbles()
        {
            var list = new List<MarbleController>();
            foreach (var c in _field) if (c.Runtime.isPlayer) list.Add(c);
            return list;
        }
        public int TotalLaps { get; private set; }
        public RaceResult Result { get; private set; }

        // Eventos para a UI (PRD 23.5).
        public event Action<int> OnCountdown;          // 3,2,1,0(=GO)
        public event Action OnRaceStarted;
        public event Action<string> OnRaceEvent;       // log de eventos
        public event Action<RaceResult> OnRaceFinished;

        private readonly List<MarbleController> _field = new();
        private readonly Dictionary<MarbleController, MarbleAI> _ais = new();

        private GameBalance _bal;
        private TireWearSystem _tireSystem;
        private EnergySystem _energySystem;
        private RacePositionSystem _positionSystem;
        private PitStopManager _pitManager;
        private WeatherSystem _weatherSystem;
        private RaceEventSystem _eventSystem;

        // Eventos de corrida ativos (PRD 20).
        private bool _safetyActive;
        private float _safetyTimer;
        private float _dirtyTimer;

        private float _countdownTimer;
        private int _lastCountValue = -1;
        private float _raceClock;

        // ---- IRaceConditions (clima dinamico + eventos) ------------------
        public Weather CurrentWeather => _weatherSystem != null ? _weatherSystem.Current : Weather.Dry;
        public float TrackSpeedMod => _dirtyTimer > 0f ? 0.95f : 1f;
        public float TrackErrorAdd => _dirtyTimer > 0f ? 0.04f : 0f;
        public bool SafetyMarbleActive => _safetyActive;

        private void Awake()
        {
            if (database == null && GameManager.Instance != null)
                database = GameManager.Instance.Database;
        }

        /// <summary>Inicia uma corrida a partir de uma configuracao (PRD 7.3).</summary>
        public void StartRace(RaceConfig config)
        {
            Config = config;
            _bal = database.balance;
            TotalLaps = config.laps;

            BuildGround();
            Track = TrackBuilder.Build(config.track, config.MarbleCount, BuildTeamColors(config), transform);

            // Clima dinamico (PRD 19): comeca do config ou sorteia pela chance de chuva.
            Weather start = config.weather != Weather.Dry
                ? config.weather
                : WeatherSystem.InitialFor(config.track.rainChance);
            _weatherSystem = new WeatherSystem(start, config.track.rainChance);
            _weatherSystem.OnChanged += w => Log($"🌦 Clima mudou: {WeatherLabel(w)}");
            _eventSystem = new RaceEventSystem();

            // Sistemas (PRD 24.2).
            _tireSystem = new TireWearSystem(_bal, config.track);
            _energySystem = new EnergySystem(_bal, config.track);
            _positionSystem = new RacePositionSystem(Track, _bal.baseSpeed);
            _pitManager = new PitStopManager(Track, _bal, database, _tireSystem, _energySystem);

            _tireSystem.OnHighWearAlert += m => Log($"⚠ {m.DisplayName}: desgaste alto!");
            _energySystem.OnLowEnergyAlert += m => Log($"⚠ {m.DisplayName}: energia baixa!");
            _pitManager.OnPitCompleted += m => Log($"🔧 {m.DisplayName} saiu do pit com {m.grip.gripId}.");
            _pitManager.OnPitExit += c => _positionSystem.ResyncCheckpoint(c);

            SpawnField(config);

            State = RaceState.Countdown;
            _countdownTimer = 4f; // 3..2..1..GO
            _lastCountValue = -1;
        }

        /// <summary>Cores das equipes (por bolinha) para colorir os pit boxes.</summary>
        private List<Color> BuildTeamColors(RaceConfig config)
        {
            var colors = new List<Color>();
            var profile = GameManager.Instance != null ? GameManager.Instance.Profile : null;
            foreach (var e in config.entries)
            {
                if (e.isPlayerControlled && profile != null) { colors.Add(profile.PrimaryColor); continue; }
                var team = database.GetTeam(e.driver.teamId);
                colors.Add(team != null ? team.primaryColor : Color.gray);
            }
            return colors;
        }

        private void BuildGround()
        {
            var ground = new GameObject("Ground");
            ground.transform.SetParent(transform, false);
            var col = ground.AddComponent<BoxCollider>();
            col.size = new Vector3(500f, 1f, 500f);
            col.center = new Vector3(0f, -0.5f, 0f);
        }

        private void SpawnField(RaceConfig config)
        {
            var profile = GameManager.Instance != null ? GameManager.Instance.Profile : null;
            int playerMarbleIndex = 0;

            // Efeitos dos upgrades de equipe (PRD 30), se houver temporada ativa.
            var champ = GameManager.Instance != null ? GameManager.Instance.Championship : null;
            var upgrades = (champ != null && champ.HasActiveSeason)
                ? champ.Effects() : TeamUpgradeEffects.Neutral;

            for (int i = 0; i < config.entries.Count; i++)
            {
                var strat = config.entries[i];
                var runtime = new MarbleRuntime
                {
                    driver = strat.driver,
                    team = database.GetTeam(strat.driver.teamId),
                    strategy = strat,
                    isPlayer = strat.isPlayerControlled,
                    grip = database.GetGrip(strat.grip),
                    surface = database.GetSurface(strat.surface),
                    mode = strat.startMode,
                    energy = strat.startEnergy,
                    pitTargetGrip = strat.grip,
                    pitRefillAmount = 60f,
                    GameBalanceRef = _bal,
                    state = MarbleRaceState.OnGrid
                };

                // Aplica customizacoes da garagem nas bolinhas do jogador (PRD 31).
                if (runtime.isPlayer && profile != null)
                {
                    runtime.teamNameOverride = profile.teamName;
                    runtime.teamPrimaryOverride = profile.PrimaryColor;
                    runtime.teamSecondaryOverride = profile.SecondaryColor;
                    runtime.marbleColorOverride = profile.GetMarbleColor(playerMarbleIndex);
                    playerMarbleIndex++;

                    runtime.upgPitReduction = upgrades.pitTimeReduction;
                    runtime.upgEnergyFactor = upgrades.energyFactor;
                    runtime.upgWearFactor = upgrades.wearFactor;
                    runtime.upgSpeedFactor = upgrades.speedFactor;
                    runtime.upgControlFactor = upgrades.controlFactor;
                    runtime.upgErrorFactor = upgrades.errorFactor;
                }

                Vector3 grid = i < Track.GridPositions.Count
                    ? Track.GridPositions[i]
                    : Vector3.right * i;

                var ctrl = MarbleFactory.Spawn(runtime, grid, Track, _bal, transform);
                // Orienta para a frente da pista.
                Vector3 fwd = Track.GetSteerTarget(RacingLine.Ideal, grid, 2) - grid;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.01f) ctrl.transform.rotation = Quaternion.LookRotation(fwd);

                _field.Add(ctrl);
                _ais[ctrl] = new MarbleAI(ctrl, Track, _bal, this);
                _positionSystem.Register(runtime);
            }
        }

        private void Update()
        {
            if (State != RaceState.Countdown) return;
            _countdownTimer -= Time.deltaTime;
            int v = Mathf.CeilToInt(_countdownTimer);
            if (v != _lastCountValue)
            {
                _lastCountValue = v;
                OnCountdown?.Invoke(Mathf.Max(0, v)); // 3,2,1,0
            }
            if (_countdownTimer <= 0f) BeginRacing();
        }

        private void BeginRacing()
        {
            State = RaceState.Racing;
            foreach (var c in _field) c.Runtime.state = MarbleRaceState.Racing;
            Log("🏁 GO!");
            OnRaceStarted?.Invoke();
        }

        private void FixedUpdate()
        {
            if (State != RaceState.Racing) return;
            float dt = Time.fixedDeltaTime;
            _raceClock += dt;

            UpdateConditions(dt);
            _pitManager.Tick(dt);

            float safetySpeed = _bal.baseSpeed * 0.5f;

            foreach (var ctrl in _field)
            {
                var m = ctrl.Runtime;
                if (m.state == MarbleRaceState.Finished) continue;

                bool pitting = _pitManager.IsPitting(ctrl);

                if (pitting)
                {
                    // O PitStopManager define alvo/velocidade; so movemos a fisica.
                    ctrl.PhysicsStep(dt);
                }
                else
                {
                    _ais[ctrl].Think(_field, dt);

                    // Safety Marble: neutraliza velocidade e ultrapassagens (PRD 20).
                    if (_safetyActive)
                    {
                        ctrl.DesiredSpeed = Mathf.Min(ctrl.DesiredSpeed, safetySpeed);
                        ctrl.Line = RacingLine.Ideal;
                    }

                    ctrl.PhysicsStep(dt);
                    ctrl.HandleStuckRecovery(dt);

                    // Desgaste (clima vivo) e energia acoplados a distancia (PRD 14/15/19).
                    _tireSystem.Apply(m, ctrl.DistanceLastStep, CurrentWeather);
                    _energySystem.Apply(m, ctrl.DistanceLastStep);

                    // Voltas (PRD 9.3 / 26).
                    bool lapDone = _positionSystem.UpdateLap(ctrl, TotalLaps);
                    if (lapDone && m.pitRequested)
                    {
                        _pitManager.BeginEntry(ctrl);
                        Log($"🔧 {m.DisplayName} entrou no pit.");
                    }
                }

                // Cronometro por bolinha.
                if (m.state != MarbleRaceState.Finished)
                {
                    m.totalTime += dt;
                    m.currentLapTime += dt;
                }
            }

            _positionSystem.UpdatePositions(_field);
            DetectOvertakes(dt);

            if (AllFinished()) FinishRace();
        }

        private readonly Dictionary<MarbleController, int> _lastPos = new();
        private readonly Dictionary<MarbleController, float> _otCooldown = new();

        /// <summary>Loga ultrapassagens (PRD 20: "X ultrapassou Y"), com cooldown anti-spam.</summary>
        private void DetectOvertakes(float dt)
        {
            if (_raceClock < 3f) { foreach (var c in _field) _lastPos[c] = c.Runtime.position; return; }

            for (int i = 0; i < _field.Count; i++)
            {
                var c = _field[i];
                var m = c.Runtime;
                int newPos = i + 1;
                if (_lastPos.TryGetValue(c, out int prev) && newPos == prev - 1 &&
                    m.state == MarbleRaceState.Racing)
                {
                    float cd = _otCooldown.TryGetValue(c, out var t) ? t : 0f;
                    if (cd <= 0f && i + 1 < _field.Count)
                    {
                        var behind = _field[i + 1].Runtime;
                        if (behind.state == MarbleRaceState.Racing)
                        {
                            Log($"🔼 {m.DisplayName} ultrapassou {behind.DisplayName}.");
                            _otCooldown[c] = 2.5f;
                        }
                    }
                }
                _lastPos[c] = newPos;
            }

            var keys = new List<MarbleController>(_otCooldown.Keys);
            foreach (var k in keys) _otCooldown[k] = Mathf.Max(0f, _otCooldown[k] - dt);
        }

        private bool AllFinished()
        {
            foreach (var c in _field)
                if (c.Runtime.state != MarbleRaceState.Finished &&
                    c.Runtime.state != MarbleRaceState.Retired)
                    return false;
            return true;
        }

        // ---- API para a UI (PRD 8: decisoes durante a corrida) -----------

        public void RequestPit(MarbleController ctrl, GripType grip, bool changeTires, float refill)
        {
            var m = ctrl.Runtime;
            if (m.state == MarbleRaceState.Finished) return;
            m.pitTargetGrip = grip;
            m.pitChangeTires = changeTires;
            m.pitRefillAmount = refill;
            m.pitRequested = true;
            Log($"📞 {m.DisplayName}: pit solicitado.");
        }

        public void SetMode(MarbleController ctrl, RaceMode mode)
        {
            ctrl.Runtime.mode = mode;
            Log($"⚙ {ctrl.Runtime.DisplayName}: modo {mode}.");
        }

        // ---- Fim da corrida (PRD 23.6 / 10) ------------------------------

        private void FinishRace()
        {
            State = RaceState.Finished;
            foreach (var c in _field) c.Freeze();

            var ordered = new List<MarbleController>(_field);
            ordered.Sort((a, b) => a.Runtime.totalTime.CompareTo(b.Runtime.totalTime));

            // Volta mais rapida da corrida (para flag de estatistica).
            MarbleController fastest = null;
            float best = float.MaxValue;
            foreach (var c in ordered)
                if (c.Runtime.bestLapTime < best) { best = c.Runtime.bestLapTime; fastest = c; }

            Result = new RaceResult { trackName = Config.track.trackName, laps = TotalLaps };
            for (int i = 0; i < ordered.Count; i++)
            {
                var m = ordered[i].Runtime;
                Result.entries.Add(new RaceResultEntry
                {
                    position = i + 1,
                    marbleName = m.DisplayName,
                    teamName = m.TeamDisplayName,
                    teamId = m.team != null ? m.team.teamId : "",
                    driverId = m.driver != null ? m.driver.driverId : "",
                    totalTime = m.totalTime,
                    pitStops = m.pitStops,
                    bestLapTime = m.bestLapTime == float.MaxValue ? 0f : m.bestLapTime,
                    overtakes = m.overtakes,
                    finalWear = m.wear,
                    finalEnergy = m.energy,
                    finalTyre = m.grip != null ? m.grip.DisplayLetter : "M",
                    points = _bal.PointsForPosition(i + 1),
                    isPlayer = m.isPlayer,
                    fastestLap = ordered[i] == fastest
                });
            }

            // Destaca o vencedor na pista (PRD 15: chegada).
            if (ordered.Count > 0)
            {
                var winnerVisual = ordered[0].GetComponent<MarbleVisual>();
                if (winnerVisual != null) winnerVisual.SetWinner();
            }

            Log($"🏁 {ordered[0].Runtime.DisplayName} venceu em {Config.track.trackName}!");
            Log("🏆 Corrida encerrada!");
            OnRaceFinished?.Invoke(Result);
        }

        // ---- Clima dinamico + eventos de corrida (PRD 19 / 20) -----------

        private void UpdateConditions(float dt)
        {
            _weatherSystem.Tick(dt);

            // Contagem de eventos ativos.
            if (_safetyTimer > 0f)
            {
                _safetyTimer -= dt;
                if (_safetyTimer <= 0f)
                {
                    _safetyActive = false;
                    Log("🟢 Safety Marble recolhido. Corrida liberada!");
                }
            }
            if (_dirtyTimer > 0f)
            {
                _dirtyTimer -= dt;
                if (_dirtyTimer <= 0f) Log("✨ Pista limpa novamente.");
            }

            // Sorteio de novos eventos (apenas se nenhum em andamento).
            if (_safetyActive || _dirtyTimer > 0f) return;
            switch (_eventSystem.Tick(dt))
            {
                case RaceEventKind.SafetyMarble:
                    _safetyActive = true;
                    _safetyTimer = 8f;
                    Log("🚨 Safety Marble na pista! Velocidade neutralizada.");
                    break;
                case RaceEventKind.DirtyTrack:
                    _dirtyTimer = 10f;
                    Log("⚠ Pista suja! Menos aderencia e mais risco de erro.");
                    break;
            }
        }

        private static string WeatherLabel(Weather w)
        {
            switch (w)
            {
                case Weather.Dry: return "Seco";
                case Weather.Cloudy: return "Nublado";
                case Weather.Damp: return "Umido";
                case Weather.LightRain: return "Chuva leve";
                case Weather.HeavyRain: return "Chuva forte";
                default: return w.ToString();
            }
        }

        public string WeatherLabelCurrent() => WeatherLabel(CurrentWeather);

        private void Log(string msg)
        {
            Debug.Log($"[Race] {msg}");
            OnRaceEvent?.Invoke(msg);
        }
    }
}
