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
        private FuelSystem _fuelSystem;
        private RacePositionSystem _positionSystem;
        private PitStopManager _pitManager;
        private WeatherSystem _weatherSystem;
        private RaceEventSystem _eventSystem;
        private AIStrategyManager _aiStrategy;

        // Eventos de corrida ativos (PRD 20).
        private bool _safetyActive;
        private float _safetyTimer;
        private float _dirtyTimer;
        private int _lastSafetyLap = -10;   // cooldown de Safety Marble (PRD 9)
        private int _lastLeaderLap = 0;

        // Fim de corrida (PRD 2).
        private bool _winnerDeclared;
        private float _finishTimer;
        private const float FinishTimeout = 25f;

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
            _weatherSystem = new WeatherSystem(start, config.track.rainChance, TotalLaps);
            _weatherSystem.OnChanged += w => Log($"🌦 Clima mudou: {WeatherLabel(w)}");
            _eventSystem = new RaceEventSystem();

            // Sistemas (PRD 24.2).
            _tireSystem = new TireWearSystem(_bal, config.track, TotalLaps);
            _energySystem = new EnergySystem(_bal, config.track);
            _fuelSystem = new FuelSystem(config.track, TotalLaps);
            _positionSystem = new RacePositionSystem(Track, _bal.baseSpeed);
            _pitManager = new PitStopManager(Track, _bal, database, _tireSystem, _energySystem, _fuelSystem);
            _aiStrategy = new AIStrategyManager(database, _fuelSystem, this, TotalLaps);

            _tireSystem.OnHighWearAlert += m => Log($"⚠ {m.DisplayName}: desgaste alto!");
            _energySystem.OnLowEnergyAlert += m => Log($"⚠ {m.DisplayName}: energia baixa!");
            _fuelSystem.OnFuelEmpty += m => Log($"⛽ {m.DisplayName} esta sem combustivel!");
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
                    energy = 100f,   // bateria cheia (PRD 4.2)
                    fuel = 100f,     // tanque cheio (PRD 4.1)
                    pitTargetGrip = strat.grip,
                    pitRefillAmount = 100f,
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
                else
                {
                    // Dificuldade da IA (PRD 12): velocidade, erro e qualidade de pit.
                    int diff = profile != null ? profile.difficulty : 1;
                    switch (diff)
                    {
                        case 0: runtime.aiSpeedMult = 0.96f; runtime.aiErrorMult = 1.25f; runtime.aiPitQuality = 0.70f; break;
                        case 2: runtime.aiSpeedMult = 1.04f; runtime.aiErrorMult = 0.75f; runtime.aiPitQuality = 1.30f; break;
                        default: runtime.aiSpeedMult = 1.00f; runtime.aiErrorMult = 1.00f; runtime.aiPitQuality = 1.00f; break;
                    }
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
                ctrl.Contact += OnMarbleContact; // eventos de batida (PRD 10)
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
                    // Estrategia da IA (combustivel/desgaste/clima): pode pedir pit.
                    _aiStrategy.Evaluate(ctrl);

                    _ais[ctrl].Think(_field, dt);

                    // Safety Marble: neutraliza velocidade e ultrapassagens (PRD 20).
                    if (_safetyActive)
                    {
                        ctrl.DesiredSpeed = Mathf.Min(ctrl.DesiredSpeed, safetySpeed);
                        ctrl.Line = RacingLine.Ideal;
                    }

                    // Recuperacao apos batida forte: anda muito devagar (PRD 10).
                    if (m.state == MarbleRaceState.Recovering)
                    {
                        m.recoverTimer -= dt;
                        ctrl.DesiredSpeed = Mathf.Min(ctrl.DesiredSpeed, _bal.baseSpeed * 0.25f);
                        ctrl.Line = RacingLine.Ideal;
                        if (m.recoverTimer <= 0f) m.state = MarbleRaceState.Racing;
                    }

                    // Falha de nucleo: dreno extra de energia (PRD 10).
                    if (m.coreFailTimer > 0f)
                    {
                        m.coreFailTimer -= dt;
                        m.energy = Mathf.Max(0f, m.energy - 14f * dt);
                    }

                    ctrl.PhysicsStep(dt);
                    ctrl.HandleStuckRecovery(dt);

                    // Desgaste, energia e combustivel acoplados a distancia (PRD 4/14/15).
                    _tireSystem.Apply(m, ctrl.DistanceLastStep, CurrentWeather);
                    _energySystem.Apply(m, ctrl.DistanceLastStep);
                    _fuelSystem.Apply(m, ctrl.DistanceLastStep);

                    // Voltas (PRD 9.3 / 26).
                    bool lapDone = _positionSystem.UpdateLap(ctrl, TotalLaps);
                    if (lapDone)
                    {
                        _lapChangeTime[ctrl] = _raceClock; // janela anti-falsa-ultrapassagem
                        RollMarbleLapEvents(ctrl, m);
                        if (m.pitRequested)
                        {
                            _pitManager.BeginEntry(ctrl);
                            Log($"🔧 {m.DisplayName} entrou no pit.");
                        }
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
            HandleSafetyMarbleRoll();
            HandleFinish(dt);
        }

        // ---- Deteccao robusta de ultrapassagem (PRD 1) -------------------

        private readonly Dictionary<MarbleController, int> _otPrevPos = new();
        private readonly Dictionary<MarbleController, float> _lapChangeTime = new();
        private readonly Dictionary<string, float> _pairCooldown = new();
        private float _otSnapTimer = 0.5f;

        /// <summary>
        /// So registra ultrapassagem quando a troca de posicao PERSISTE entre dois
        /// snapshots (~0.5s), ambas em Racing, fora da janela de cruzamento de
        /// volta e com cooldown por par (2s). Evita falsos positivos da reordenacao
        /// momentanea ao cruzar a linha/atualizar checkpoint (PRD 1).
        /// </summary>
        private void DetectOvertakes(float dt)
        {
            // Cooldowns por par decrementam todo frame (ultrapassagem e contato).
            if (_pairCooldown.Count > 0)
            {
                var keys = new List<string>(_pairCooldown.Keys);
                foreach (var k in keys) _pairCooldown[k] = Mathf.Max(0f, _pairCooldown[k] - dt);
            }
            if (_contactCooldown.Count > 0)
            {
                var ckeys = new List<string>(_contactCooldown.Keys);
                foreach (var k in ckeys) _contactCooldown[k] = Mathf.Max(0f, _contactCooldown[k] - dt);
            }

            _otSnapTimer -= dt;
            if (_otSnapTimer > 0f) return;
            _otSnapTimer = 0.5f;

            // Sem ultrapassagens durante Safety Marble ou nos 3s iniciais (PRD 1).
            if (_raceClock < 3f || _safetyActive) { SnapshotPositions(); return; }

            for (int i = 0; i + 1 < _field.Count; i++)
            {
                var a = _field[i];      // a frente agora
                var b = _field[i + 1];  // logo atras agora
                var mA = a.Runtime; var mB = b.Runtime;

                if (mA.state != MarbleRaceState.Racing || mB.state != MarbleRaceState.Racing) continue;
                if (!_otPrevPos.TryGetValue(a, out int prevA) || !_otPrevPos.TryGetValue(b, out int prevB)) continue;

                // A estava ATRAS de B no snapshot anterior e agora esta a frente?
                if (prevA <= prevB) continue;
                // Ignora churn de cruzamento de volta.
                if (RecentLap(a) || RecentLap(b)) continue;
                // Precisa estar realmente a frente (nao empate piscando num pelotao).
                if (mA.raceProgress - mB.raceProgress < 0.004f) continue;

                string key = mA.DisplayName + ">" + mB.DisplayName;
                if (_pairCooldown.TryGetValue(key, out float cd) && cd > 0f) continue;

                Log($"🔼 {mA.DisplayName} ultrapassou {mB.DisplayName}.");
                _pairCooldown[key] = 2f;
            }

            SnapshotPositions();
        }

        private void SnapshotPositions()
        {
            foreach (var c in _field) _otPrevPos[c] = c.Runtime.position;
        }

        private bool RecentLap(MarbleController c)
            => _lapChangeTime.TryGetValue(c, out float t) && (_raceClock - t) < 0.6f;

        // ---- Fim de corrida + timeout (PRD 2) ----------------------------

        private void HandleFinish(float dt)
        {
            if (!_winnerDeclared)
            {
                foreach (var c in _field)
                {
                    if (c.Runtime.state == MarbleRaceState.Finished)
                    {
                        _winnerDeclared = true;
                        _finishTimer = 0f;
                        Log($"🏁 {c.Runtime.DisplayName} cruzou a linha em 1o! Bandeirada!");
                        break;
                    }
                }
            }

            if (!_winnerDeclared) return;

            _finishTimer += dt;
            if (AllFinished() || _finishTimer >= FinishTimeout)
            {
                ClassifyRemaining();
                FinishRace();
            }
        }

        /// <summary>Classifica por progresso quem nao cruzou a linha (timeout).</summary>
        private void ClassifyRemaining()
        {
            foreach (var c in _field)
            {
                if (c.Runtime.state != MarbleRaceState.Finished &&
                    c.Runtime.state != MarbleRaceState.Retired)
                {
                    c.Runtime.state = MarbleRaceState.Finished;
                    c.Freeze();
                }
            }
        }

        // ---- Safety Marble aleatorio por volta (PRD 9) -------------------

        private void HandleSafetyMarbleRoll()
        {
            if (_field.Count == 0) return;
            int leaderLap = _field[0].Runtime.completedLaps;
            if (leaderLap <= _lastLeaderLap) return;
            _lastLeaderLap = leaderLap;

            // Clima e avaliado uma vez por volta do lider (PRD 10).
            _weatherSystem.EvaluateLap(leaderLap);

            if (_safetyActive) return;
            if (leaderLap < 1 || leaderLap >= TotalLaps) return;     // nem 1a nem ultima
            if (leaderLap - _lastSafetyLap < 2) return;              // cooldown 2 voltas

            float chance = 0.08f;
            if (CurrentWeather == Weather.HeavyRain) chance *= 1.6f;
            else if (CurrentWeather == Weather.LightRain) chance *= 1.3f;
            if (Config.track.difficulty == Difficulty.Hard) chance *= 1.3f;

            if (UnityEngine.Random.value < chance)
            {
                _lastSafetyLap = leaderLap;
                StartSafetyMarble(UnityEngine.Random.Range(14f, 22f));
            }
        }

        private void StartSafetyMarble(float duration)
        {
            _safetyActive = true;
            _safetyTimer = duration;
            Log("🚨 Safety Marble na pista! Velocidade neutralizada.");
        }

        // ---- Eventos de corrida: batida, contato, falha de nucleo (PRD 10) --

        private readonly Dictionary<string, float> _contactCooldown = new();

        private void OnMarbleContact(MarbleController a, MarbleController b, float impact)
        {
            var ma = a.Runtime; var mb = b.Runtime;
            if (ma.state == MarbleRaceState.Finished || mb.state == MarbleRaceState.Finished) return;
            // No pit as bolinhas se sobrepoem: nenhum contato conta ali.
            if (InPitFlow(ma) || InPitFlow(mb)) return;

            // Faiscas sempre (feedback visual de contato).
            a.GetComponent<MarbleVisual>()?.PlayContactSpark();
            b.GetComponent<MarbleVisual>()?.PlayContactSpark();

            string key = ma.DisplayName.CompareTo(mb.DisplayName) < 0
                ? ma.DisplayName + "|" + mb.DisplayName
                : mb.DisplayName + "|" + ma.DisplayName;
            if (_contactCooldown.TryGetValue(key, out float cd) && cd > 0f) return;
            _contactCooldown[key] = 1.5f;

            if (impact > 7f && UnityEngine.Random.value < 0.35f)
            {
                // Batida forte: a bolinha mais lenta (atingida) se complica.
                var victim = a.CurrentSpeed <= b.CurrentSpeed ? a : b;
                TriggerCrash(victim, "contato forte");
            }
            else if (impact > 4f)
            {
                Log($"💥 {ma.DisplayName} e {mb.DisplayName} se tocaram.");
            }
        }

        private static bool InPitFlow(MarbleRuntime m)
            => m.state == MarbleRaceState.EnteringPit
            || m.state == MarbleRaceState.InPit
            || m.state == MarbleRaceState.ExitingPit;

        private void RollMarbleLapEvents(MarbleController ctrl, MarbleRuntime m)
        {
            if (m.state != MarbleRaceState.Racing) return;
            float risk = EventRisk(m);

            if (UnityEngine.Random.value < 0.02f * risk) { TriggerCrash(ctrl, "erro grave"); return; }
            if (m.coreFailTimer <= 0f && UnityEngine.Random.value < 0.01f * risk) TriggerCoreFailure(ctrl);
        }

        private float EventRisk(MarbleRuntime m)
        {
            float r = 1f;
            if (m.mode == RaceMode.Push) r *= 1.4f;
            if (m.mode == RaceMode.Save) r *= 0.7f;
            if (m.wear > 75f) r *= 1.5f;
            if (m.energy < 15f) r *= 1.3f;
            if (IsWetWeather(CurrentWeather)) r *= 1.4f;
            switch (m.driver.personality)
            {
                case Personality.Aggressive:
                case Personality.RiskTaker: r *= 1.3f; break;
                case Personality.Veteran:
                case Personality.Smooth: r *= 0.7f; break;
            }
            r *= 1f - m.driver.control / 300f; // alto controle reduz risco
            if (Config.track.difficulty == Difficulty.Hard) r *= 1.2f;
            return r;
        }

        private void TriggerCrash(MarbleController ctrl, string reason)
        {
            var m = ctrl.Runtime;
            if (m.state != MarbleRaceState.Racing) return;
            m.state = MarbleRaceState.Recovering;
            m.recoverTimer = UnityEngine.Random.Range(2f, 4f);
            ctrl.GetComponent<MarbleVisual>()?.PlayCrash();
            Log($"💥 {m.DisplayName} bateu forte ({reason})!");

            // Chance de acionar Safety Marble.
            if (!_safetyActive && _lastLeaderLap >= 1 && _lastLeaderLap < TotalLaps
                && (_lastLeaderLap - _lastSafetyLap) >= 2 && UnityEngine.Random.value < 0.3f)
            {
                _lastSafetyLap = _lastLeaderLap;
                StartSafetyMarble(UnityEngine.Random.Range(14f, 20f));
            }
        }

        private void TriggerCoreFailure(MarbleController ctrl)
        {
            var m = ctrl.Runtime;
            m.coreFailTimer = UnityEngine.Random.Range(6f, 10f);
            ctrl.GetComponent<MarbleVisual>()?.PlayCoreFailure();
            Log($"⚡ {m.DisplayName}: falha de nucleo! Precisa do pit.");
        }

        private static bool IsWetWeather(Weather w)
            => w == Weather.Damp || w == Weather.LightRain || w == Weather.HeavyRain;

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
            if (State == RaceState.Finished) return; // evita finalizar duas vezes
            State = RaceState.Finished;
            foreach (var c in _field) c.Freeze();

            // Usa a ordem ao vivo (ja classificada por progresso/tempo), correta
            // tambem para quem foi classificado por timeout (PRD 2).
            var ordered = new List<MarbleController>(_field);

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
                    finalFuel = m.fuel,
                    finalTyre = m.grip != null ? m.grip.DisplayLetter : "M",
                    statusText = m.completedLaps >= TotalLaps ? "Finished" : "Classificado",
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
            // Clima agora e avaliado por volta (HandleSafetyMarbleRoll), nao por tempo.

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

            // Pista suja (time-based). Safety Marble e tratado por volta (HandleSafetyMarbleRoll).
            if (_safetyActive || _dirtyTimer > 0f) return;
            if (_eventSystem.Tick(dt) == RaceEventKind.DirtyTrack)
            {
                _dirtyTimer = 10f;
                Log("⚠ Pista suja! Menos aderencia e mais risco de erro.");
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
