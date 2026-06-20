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
    public class RaceManager : MonoBehaviour
    {
        [Header("Dados")]
        [SerializeField] private GameDatabase database;

        public RaceState State { get; private set; } = RaceState.PreRace;
        public TrackManager Track { get; private set; }
        public IReadOnlyList<MarbleController> Field => _field;
        public RaceConfig Config { get; private set; }
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
        private Weather _weather;
        private TireWearSystem _tireSystem;
        private EnergySystem _energySystem;
        private RacePositionSystem _positionSystem;
        private PitStopManager _pitManager;

        private float _countdownTimer;
        private int _lastCountValue = -1;
        private float _raceClock;

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
            _weather = config.weather;
            TotalLaps = config.laps;

            BuildGround();
            Track = TrackBuilder.Build(config.track, config.MarbleCount, transform);

            // Sistemas (PRD 24.2).
            _tireSystem = new TireWearSystem(_bal, config.track, _weather);
            _energySystem = new EnergySystem(_bal, config.track);
            _positionSystem = new RacePositionSystem(Track, _bal.baseSpeed);
            _pitManager = new PitStopManager(Track, _bal, database, _tireSystem, _energySystem);

            _tireSystem.OnHighWearAlert += m => Log($"⚠ {m.DisplayName}: desgaste alto!");
            _energySystem.OnLowEnergyAlert += m => Log($"⚠ {m.DisplayName}: energia baixa!");
            _pitManager.OnPitCompleted += m => Log($"🔧 {m.DisplayName}: pit concluido.");

            SpawnField(config);

            State = RaceState.Countdown;
            _countdownTimer = 4f; // 3..2..1..GO
            _lastCountValue = -1;
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
                _ais[ctrl] = new MarbleAI(ctrl, Track, _bal, _weather);
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

            _pitManager.Tick(dt);

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
                    ctrl.PhysicsStep(dt);
                    ctrl.HandleStuckRecovery(dt);

                    // Desgaste e energia acoplados a distancia percorrida (PRD 14/15).
                    _tireSystem.Apply(m, ctrl.DistanceLastStep);
                    _energySystem.Apply(m, ctrl.DistanceLastStep);

                    // Voltas (PRD 9.3 / 26).
                    bool lapDone = _positionSystem.UpdateLap(ctrl, TotalLaps);
                    if (lapDone && m.pitRequested)
                        _pitManager.BeginEntry(ctrl);
                }

                // Cronometro por bolinha.
                if (m.state != MarbleRaceState.Finished)
                {
                    m.totalTime += dt;
                    m.currentLapTime += dt;
                }
            }

            _positionSystem.UpdatePositions(_field);

            if (AllFinished()) FinishRace();
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
                    points = _bal.PointsForPosition(i + 1),
                    isPlayer = m.isPlayer,
                    fastestLap = ordered[i] == fastest
                });
            }

            Log("🏆 Corrida encerrada!");
            OnRaceFinished?.Invoke(Result);
        }

        private void Log(string msg)
        {
            Debug.Log($"[Race] {msg}");
            OnRaceEvent?.Invoke(msg);
        }
    }
}
