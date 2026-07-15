// =====================================================================
// Orquestrador da corrida — port de Race/RaceManager.cs (PRD 24.2)
// Monta pista e bolinhas, countdown, loop de simulação (IA + física +
// sistemas), voltas/posições/pit, eventos, rádio e resultado final.
// Passo fixo de 0.02s (equivalente ao FixedUpdate do Unity).
// =====================================================================

import type { Difficulty, GripType, RaceMode, Weather } from '../core/types';
import { gripDisplayLetter } from '../core/types';
import { Balance, pointsForPosition } from '../data/balance';
import { GRIPS, SURFACES, DEFAULT_SURFACE } from '../data/grips';
import { driverById } from '../data/drivers';
import { teamById } from '../data/teams';
import { trackById } from '../data/circuits';
import { MarbleActor, type RaceConditions } from './controller';
import { RacePositionSystem } from './positions';
import { PitStopManager } from './pit';
import { AIStrategyManager } from './strategy';
import { TeamRadioSystem, type RadioDecision, type RadioOption } from './radio';
import { MarbleRuntime, type RaceSetup } from './runtime';
import {
  EnergySystem, FuelSystem, RaceEventSystem, TireWearSystem, WeatherSystem,
  type RaceResult, type RaceResultEntry,
} from './systems';
import { Track } from './track';

export const FIXED_DT = 0.02; // passo de simulação (50 Hz, como o Unity)

export type RaceStateKind = 'PreRace' | 'Countdown' | 'Racing' | 'Paused' | 'Finished';

/** Efeitos agregados dos upgrades da equipe (Core/TeamUpgradeEffects.cs). */
export interface TeamUpgradeEffects {
  pitTimeReduction: number;
  energyFactor: number;
  wearFactor: number;
  speedFactor: number;
  controlFactor: number;
  errorFactor: number;
}

export const NEUTRAL_UPGRADES: TeamUpgradeEffects = {
  pitTimeReduction: 0, energyFactor: 1, wearFactor: 1,
  speedFactor: 1, controlFactor: 1, errorFactor: 1,
};

export interface RaceManagerOptions {
  /** Dificuldade da IA: 0 fácil, 1 médio, 2 difícil. */
  aiDifficulty?: number;
  /** Upgrades aplicados às bolinhas do jogador. */
  upgrades?: TeamUpgradeEffects;
  /** Garagem: nome da equipe e cor secundária (a primária é fixa da equipe). */
  playerTeamName?: string;
  playerSecondaryColor?: string;
}

export class RaceManager implements RaceConditions {
  state: RaceStateKind = 'PreRace';
  readonly setup: RaceSetup;
  readonly track: Track;
  readonly field: MarbleActor[] = [];
  readonly players: MarbleActor[] = [];
  readonly totalLaps: number;
  result: RaceResult | null = null;

  // Eventos para a UI.
  onCountdown: ((v: number) => void) | null = null;      // 3,2,1,0(=GO)
  onRaceStarted: (() => void) | null = null;
  onRaceEvent: ((msg: string) => void) | null = null;
  onRaceFinished: ((r: RaceResult) => void) | null = null;
  onRadioDecision: ((d: RadioDecision) => void) | null = null;
  /** Contato entre bolinhas (para faíscas): posição do mundo + intensidade. */
  onContact: ((x: number, y: number, strength: number) => void) | null = null;

  private ais = new Map<MarbleActor, boolean>(); // presença (o cérebro vive no actor)
  private teamRadio: TeamRadioSystem;
  private tireSystem: TireWearSystem;
  private energySystem: EnergySystem;
  private fuelSystem: FuelSystem;
  private positionSystem: RacePositionSystem;
  private pitManager: PitStopManager;
  private weatherSystem: WeatherSystem;
  private eventSystem: RaceEventSystem;
  private aiStrategy: AIStrategyManager;
  private rand: () => number;

  // Eventos de corrida ativos.
  private safetyActive = false;
  private safetyTimer = 0;
  private dirtyTimer = 0;
  private lastSafetyLap = -10;
  private lastLeaderLap = 0;

  // Fim de corrida.
  private winnerDeclared = false;
  private finishTimer = 0;
  private static readonly FINISH_TIMEOUT = 25;
  private static readonly FINISH_COAST_TIME = 2.6;

  private countdownTimer = 4; // 3..2..1..GO
  private lastCountValue = -1;
  private raceClock = 0;

  // Detecção robusta de ultrapassagem.
  private otPrevPos = new Map<MarbleActor, number>();
  private lapChangeTime = new Map<MarbleActor, number>();
  private pairCooldown = new Map<string, number>();
  private contactCooldown = new Map<string, number>();
  private otSnapTimer = 0.5;

  // ---- IRaceConditions ----------------------------------------------
  get currentWeather(): Weather { return this.weatherSystem.current; }
  get trackSpeedMod(): number { return this.dirtyTimer > 0 ? 0.95 : 1; }
  get trackErrorAdd(): number { return this.dirtyTimer > 0 ? 0.04 : 0; }
  get safetyMarbleActive(): boolean { return this.safetyActive; }

  get leader(): MarbleActor | null { return this.field[0] ?? null; }

  constructor(setup: RaceSetup, opts: RaceManagerOptions = {}, rand: () => number = Math.random) {
    this.setup = setup;
    this.rand = rand;
    this.totalLaps = setup.laps;

    const trackData = trackById(setup.trackId);
    this.track = new Track(trackData, setup.entries.length);

    // Clima dinâmico: começa do setup ou sorteia pela chance de chuva.
    const start: Weather = setup.weather !== 'Dry'
      ? setup.weather
      : WeatherSystem.initialFor(trackData.rainChance, rand);
    this.weatherSystem = new WeatherSystem(start, trackData.rainChance, this.totalLaps, rand);
    this.weatherSystem.onChanged = w => this.log(`🌦 Clima mudou: ${weatherLabel(w)}`);
    this.eventSystem = new RaceEventSystem(rand);

    this.tireSystem = new TireWearSystem(trackData, this.totalLaps);
    this.energySystem = new EnergySystem(trackData);
    this.fuelSystem = new FuelSystem(trackData, this.totalLaps);
    this.positionSystem = new RacePositionSystem(this.track, Balance.baseSpeed);
    this.pitManager = new PitStopManager(this.track, this.tireSystem, this.energySystem, this.fuelSystem, rand);
    this.aiStrategy = new AIStrategyManager(this.fuelSystem, this, this.totalLaps);
    this.teamRadio = new TeamRadioSystem(rand);

    this.tireSystem.onHighWearAlert = m => this.log(`⚠ ${m.displayName}: desgaste alto!`);
    this.energySystem.onLowEnergyAlert = m => this.log(`⚠ ${m.displayName}: energia baixa!`);
    this.fuelSystem.onFuelEmpty = m => this.log(`⛽ ${m.displayName} está sem combustível!`);
    this.pitManager.onPitCompleted = m => this.log(`🔧 ${m.displayName} saiu do pit com ${m.grip.gripId}.`);
    this.pitManager.onPitExit = a => this.positionSystem.resyncCheckpoint(a);

    this.spawnField(setup, opts);

    this.state = 'Countdown';
    this.countdownTimer = 4;
    this.lastCountValue = -1;
  }

  private spawnField(setup: RaceSetup, opts: RaceManagerOptions): void {
    const upgrades = opts.upgrades ?? NEUTRAL_UPGRADES;

    for (let i = 0; i < setup.entries.length; i++) {
      const strat = setup.entries[i]!;
      const driver = driverById(strat.driverId);
      const team = teamById(driver.teamId);
      const surface = SURFACES[strat.surface as keyof typeof SURFACES] ?? DEFAULT_SURFACE;

      const m = new MarbleRuntime(driver, team, strat, GRIPS[strat.grip], surface);
      m.energy = strat.startEnergy <= 0 ? 100 : Math.min(Math.max(strat.startEnergy, 0), Balance.maxEnergy);
      m.fuel = 100; // tanque cheio (PRD 4.1)
      m.pitTargetGrip = strat.grip;
      m.pitRefillAmount = 100;
      m.state = 'OnGrid';

      if (m.isPlayer) {
        m.upgPitReduction = upgrades.pitTimeReduction;
        m.upgEnergyFactor = upgrades.energyFactor;
        m.upgWearFactor = upgrades.wearFactor;
        m.upgSpeedFactor = upgrades.speedFactor;
        m.upgControlFactor = upgrades.controlFactor;
        m.upgErrorFactor = upgrades.errorFactor;

        // Garagem: só nome da equipe e cor secundária (primária/bolinha fixas
        // na identidade da equipe, para não confundir na pista).
        if (opts.playerTeamName) m.teamNameOverride = opts.playerTeamName;
        if (opts.playerSecondaryColor) m.teamSecondaryOverride = opts.playerSecondaryColor;
      } else {
        // Dificuldade da IA: velocidade, erro e qualidade de pit.
        switch (opts.aiDifficulty ?? 1) {
          case 0: m.aiSpeedMult = 0.96; m.aiErrorMult = 1.25; m.aiPitQuality = 0.70; break;
          case 2: m.aiSpeedMult = 1.04; m.aiErrorMult = 0.75; m.aiPitQuality = 1.30; break;
          default: m.aiSpeedMult = 1.00; m.aiErrorMult = 1.00; m.aiPitQuality = 1.00; break;
        }
      }

      const grid = this.track.gridPositions[i] ?? { x: i, y: 0 };
      m.x = grid.x;
      m.y = grid.y;

      const actor = new MarbleActor(m, this.track, this.rand);
      // Orienta para a frente da pista.
      const fwd = this.track.getSteerTarget('Ideal', grid, 2);
      m.heading = Math.atan2(fwd.y - grid.y, fwd.x - grid.x);

      this.field.push(actor);
      if (m.isPlayer) this.players.push(actor);
      this.ais.set(actor, true);
      this.positionSystem.register(m);
      actor.onContact = (a, b, impact) => this.onMarbleContact(a, b, impact);
    }
  }

  // ------------------------------------------------------------------
  // Loop principal: chame step(dt) a cada frame; internamente acumula e
  // roda passos fixos de FIXED_DT (determinismo próximo ao Unity).
  // ------------------------------------------------------------------
  private accumulator = 0;

  step(dt: number): void {
    if (this.state === 'Paused' || this.state === 'Finished') return;
    this.accumulator += Math.min(dt, 0.25);
    while (this.accumulator >= FIXED_DT) {
      this.accumulator -= FIXED_DT;
      this.fixedStep(FIXED_DT);
      if ((this.state as RaceStateKind) === 'Finished') break;
    }
  }

  pause(): void { if (this.state === 'Racing') this.state = 'Paused'; }
  resume(): void { if (this.state === 'Paused') this.state = 'Racing'; }

  private fixedStep(dt: number): void {
    if (this.state === 'Countdown') {
      this.countdownTimer -= dt;
      const v = Math.ceil(this.countdownTimer);
      if (v !== this.lastCountValue) {
        this.lastCountValue = v;
        this.onCountdown?.(Math.max(0, v));
      }
      if (this.countdownTimer <= 0) this.beginRacing();
      return;
    }
    if (this.state !== 'Racing') return;

    this.raceClock += dt;
    this.updateConditions(dt);
    this.pitManager.tick(dt);

    const safetySpeed = Balance.baseSpeed * 0.5;

    for (const actor of this.field) {
      const m = actor.m;
      if (m.state === 'Finished') {
        // Pós-bandeirada: desacelera suave para cruzeiro (~0.32x) e CONTINUA
        // no traçado; só congela no finishRace().
        if (!m.finishHandled) { m.finishHandled = true; m.finishCoastTimer = RaceManager.FINISH_COAST_TIME; }
        const ct = m.finishCoastTimer > 0
          ? Math.min(1, Math.max(0, m.finishCoastTimer / RaceManager.FINISH_COAST_TIME)) : 0;
        if (m.finishCoastTimer > 0) m.finishCoastTimer -= dt;
        actor.line = 'Ideal';
        actor.externalTarget = null;
        actor.desiredSpeed = Balance.baseSpeed * (0.32 + (0.5 - 0.32) * ct);
        actor.physicsStep(dt, this.field);
        continue;
      }

      const pitting = this.pitManager.isPitting(actor);

      if (pitting) {
        actor.physicsStep(dt, this.field);
      } else {
        this.aiStrategy.evaluate(actor);
        actor.think(this.field, dt, this);

        // Safety Marble: neutraliza velocidade e ultrapassagens.
        if (this.safetyActive) {
          actor.desiredSpeed = Math.min(actor.desiredSpeed, safetySpeed);
          actor.line = 'Ideal';
        }

        // Recuperação após batida forte.
        if (m.state === 'Recovering') {
          m.recoverTimer -= dt;
          actor.desiredSpeed = Math.min(actor.desiredSpeed, Balance.baseSpeed * 0.25);
          actor.line = 'Ideal';
          if (m.recoverTimer <= 0) m.state = 'Racing';
        }

        // Falha de núcleo: dreno extra de energia.
        if (m.coreFailTimer > 0) {
          m.coreFailTimer -= dt;
          m.energy = Math.max(0, m.energy - 14 * dt);
        }

        // Reação de largada: nos ~1.3s iniciais, cada bolinha arranca conforme
        // sua aceleração/consistência (getaways variados, sem alterar o longo prazo).
        if (this.raceClock < 1.3) {
          const t = this.raceClock / 1.3;
          actor.desiredSpeed *= actor.launchReaction + (1 - actor.launchReaction) * t;
        }

        // Rádio do box: modo temporário expira e volta ao anterior.
        if (m.radioActive) {
          m.radioModeTimer -= dt;
          if (m.radioModeTimer <= 0) { m.radioActive = false; m.mode = m.radioPrevMode; }
        }

        actor.physicsStep(dt, this.field);
        actor.handleStuckRecovery(dt);

        // Desgaste, energia e combustível acoplados à distância.
        this.tireSystem.apply(m, actor.distanceLastStep, this.currentWeather);
        this.energySystem.apply(m, actor.distanceLastStep);
        this.fuelSystem.apply(m, actor.distanceLastStep);

        const lapDone = this.positionSystem.updateLap(actor, this.totalLaps);
        if (lapDone) {
          this.lapChangeTime.set(actor, this.raceClock);
          this.rollMarbleLapEvents(actor, m);
          if (this.winnerDeclared && (m.state as string) !== 'Finished') {
            // Após a bandeirada, cada bolinha termina ao CRUZAR a linha.
            m.state = 'Finished';
            this.log(`🏁 ${m.displayName} cruzou a linha.`);
          } else if (m.pitRequested && (m.state as string) !== 'Finished') {
            // Não arrasta para o pit uma bolinha que ACABOU de terminar.
            this.pitManager.beginEntry(actor);
            this.log(`🔧 ${m.displayName} entrou no pit.`);
          }
        }
      }

      if ((m.state as string) !== 'Finished') {
        m.totalTime += dt;
        m.currentLapTime += dt;
      }
    }

    this.positionSystem.updatePositions(this.field);
    this.detectOvertakes(dt);
    this.handleSafetyMarbleRoll();

    // Rádio do box: decisões táticas antes da bandeirada.
    if (!this.winnerDeclared && this.onRadioDecision) {
      const decision = this.teamRadio.tick(dt, this.players, this.totalLaps);
      if (decision) this.onRadioDecision(decision);
    }

    this.handleFinish(dt);
  }

  private beginRacing(): void {
    this.state = 'Racing';
    for (const a of this.field) a.m.state = 'Racing';
    this.log('🏁 GO!');
    this.onRaceStarted?.();
  }

  // ---- Detecção robusta de ultrapassagem ----------------------------

  private detectOvertakes(dt: number): void {
    for (const [k, v] of this.pairCooldown) this.pairCooldown.set(k, Math.max(0, v - dt));
    for (const [k, v] of this.contactCooldown) this.contactCooldown.set(k, Math.max(0, v - dt));

    this.otSnapTimer -= dt;
    if (this.otSnapTimer > 0) return;
    this.otSnapTimer = 0.5;

    // Sem ultrapassagens durante Safety Marble ou nos 3s iniciais.
    if (this.raceClock < 3 || this.safetyActive) {
      this.snapshotPositions();
      return;
    }

    for (let i = 0; i + 1 < this.field.length; i++) {
      const a = this.field[i]!;      // à frente agora
      const b = this.field[i + 1]!;  // logo atrás agora
      const ma = a.m, mb = b.m;

      if (ma.state !== 'Racing' || mb.state !== 'Racing') continue;
      const prevA = this.otPrevPos.get(a), prevB = this.otPrevPos.get(b);
      if (prevA === undefined || prevB === undefined) continue;

      if (prevA <= prevB) continue;                                 // não trocaram
      if (this.recentLap(a) || this.recentLap(b)) continue;         // churn de volta
      if (ma.raceProgress - mb.raceProgress < 0.004) continue;      // empate piscando

      const key = `${ma.displayName}>${mb.displayName}`;
      if ((this.pairCooldown.get(key) ?? 0) > 0) continue;

      this.log(`🔼 ${ma.displayName} ultrapassou ${mb.displayName}.`);
      ma.overtakes++;   // estatística real (objetivo diário depende disto)
      this.pairCooldown.set(key, 2);
    }

    this.snapshotPositions();
  }

  private snapshotPositions(): void {
    for (const a of this.field) this.otPrevPos.set(a, a.m.position);
  }

  private recentLap(a: MarbleActor): boolean {
    const t = this.lapChangeTime.get(a);
    return t !== undefined && this.raceClock - t < 0.6;
  }

  // ---- Fim de corrida + timeout --------------------------------------

  private handleFinish(dt: number): void {
    if (!this.winnerDeclared) {
      for (const a of this.field) {
        if (a.m.state === 'Finished') {
          this.winnerDeclared = true;
          this.finishTimer = 0;
          this.log(`🏁 ${a.m.displayName} cruzou a linha em 1º! Bandeirada!`);
          break;
        }
      }
    }
    if (!this.winnerDeclared) return;

    this.finishTimer += dt;
    if ((this.allFinished() && this.allCoastDone()) || this.finishTimer >= RaceManager.FINISH_TIMEOUT) {
      this.classifyRemaining();
      this.finishRace();
    }
  }

  private classifyRemaining(): void {
    for (const a of this.field) {
      if (a.m.state !== 'Finished' && a.m.state !== 'Retired') {
        a.m.state = 'Finished';
        a.freeze();
      }
    }
  }

  private allFinished(): boolean {
    return this.field.every(a => a.m.state === 'Finished' || a.m.state === 'Retired');
  }

  private allCoastDone(): boolean {
    return this.field.every(a =>
      a.m.state !== 'Finished' || (a.m.finishHandled && a.m.finishCoastTimer <= 0));
  }

  // ---- Safety Marble por volta ---------------------------------------

  private handleSafetyMarbleRoll(): void {
    if (this.field.length === 0) return;
    const leaderLap = this.field[0]!.m.completedLaps;
    if (leaderLap <= this.lastLeaderLap) return;
    this.lastLeaderLap = leaderLap;

    // Clima é avaliado uma vez por volta do líder.
    this.weatherSystem.evaluateLap(leaderLap);

    if (this.safetyActive) return;
    if (leaderLap < 1 || leaderLap >= this.totalLaps) return; // nem 1ª nem última
    if (leaderLap - this.lastSafetyLap < 2) return;           // cooldown 2 voltas

    let chance = 0.08;
    if (this.currentWeather === 'HeavyRain') chance *= 1.6;
    else if (this.currentWeather === 'LightRain') chance *= 1.3;
    if (this.track.data.difficulty === ('Hard' as Difficulty)) chance *= 1.3;

    if (this.rand() < chance) {
      this.lastSafetyLap = leaderLap;
      this.startSafetyMarble(14 + this.rand() * 8);
    }
  }

  private startSafetyMarble(duration: number): void {
    this.safetyActive = true;
    this.safetyTimer = duration;
    this.log('🚨 Safety Marble na pista! Velocidade neutralizada.');
  }

  // ---- Eventos: batida, contato, falha de núcleo ----------------------

  private onMarbleContact(a: MarbleActor, b: MarbleActor, impact: number): void {
    const ma = a.m, mb = b.m;
    if (ma.state === 'Finished' || mb.state === 'Finished') return;
    if (inPitFlow(ma.state) || inPitFlow(mb.state)) return;

    // Faíscas no ponto de contato (throttle leve para não spammar).
    // Sortear SEMPRE (não condicionar ao callback de UI) — senão o headless
    // e o cliente consomem o RNG em passos diferentes e o Desafio do Dia
    // deixa de ser reproduzível.
    const sparkRoll = this.rand() < 0.35;
    if (sparkRoll && this.onContact) {
      this.onContact((ma.x + mb.x) / 2, (ma.y + mb.y) / 2, Math.min(2, 0.5 + impact / 6));
    }

    const key = ma.displayName < mb.displayName
      ? `${ma.displayName}|${mb.displayName}`
      : `${mb.displayName}|${ma.displayName}`;
    if ((this.contactCooldown.get(key) ?? 0) > 0) return;
    this.contactCooldown.set(key, 1.5);

    if (impact > 7 && this.rand() < 0.35) {
      // Batida forte: a bolinha mais lenta (atingida) se complica.
      const victim = a.currentSpeed <= b.currentSpeed ? a : b;
      this.triggerCrash(victim, 'contato forte');
    } else if (impact > 4) {
      this.log(`💥 ${ma.displayName} e ${mb.displayName} se tocaram.`);
    }
  }

  private rollMarbleLapEvents(actor: MarbleActor, m: MarbleRuntime): void {
    if (m.state !== 'Racing') return;
    const risk = this.eventRisk(m);

    if (this.rand() < 0.02 * risk) { this.triggerCrash(actor, 'erro grave'); return; }
    if (m.coreFailTimer <= 0 && this.rand() < 0.01 * risk) this.triggerCoreFailure(actor);
  }

  private eventRisk(m: MarbleRuntime): number {
    let r = 1;
    if (m.mode === 'Push') r *= 1.4;
    if (m.mode === 'Save') r *= 0.7;
    if (m.wear > 75) r *= 1.5;
    if (m.energy < 15) r *= 1.3;
    if (isWetWeather(this.currentWeather)) r *= 1.4;
    switch (m.driver.personality) {
      case 'Aggressive':
      case 'RiskTaker': r *= 1.3; break;
      case 'Veteran':
      case 'Smooth': r *= 0.7; break;
    }
    r *= 1 - m.driver.control / 300; // alto controle reduz risco
    if (this.track.data.difficulty === 'Hard') r *= 1.2;
    return r;
  }

  private triggerCrash(actor: MarbleActor, reason: string): void {
    const m = actor.m;
    if (m.state !== 'Racing') return;
    m.state = 'Recovering';
    m.recoverTimer = 2 + this.rand() * 2;
    this.log(`💥 ${m.displayName} bateu forte (${reason})!`);

    // Chance de acionar Safety Marble.
    if (!this.safetyActive && this.lastLeaderLap >= 1 && this.lastLeaderLap < this.totalLaps
      && this.lastLeaderLap - this.lastSafetyLap >= 2 && this.rand() < 0.3) {
      this.lastSafetyLap = this.lastLeaderLap;
      this.startSafetyMarble(14 + this.rand() * 6);
    }
  }

  private triggerCoreFailure(actor: MarbleActor): void {
    const m = actor.m;
    m.coreFailTimer = 6 + this.rand() * 4;
    this.log(`⚡ ${m.displayName}: falha de núcleo! Precisa do pit.`);
  }

  // ---- API para a UI (decisões durante a corrida) ---------------------

  requestPit(actor: MarbleActor, grip: GripType, changeTires: boolean, refill: number): void {
    const m = actor.m;
    if (m.state === 'Finished') return;
    m.pitTargetGrip = grip;
    m.pitChangeTires = changeTires;
    m.pitRefillAmount = refill;
    m.pitRequested = true;
    this.log(`📞 ${m.displayName}: pit solicitado.`);
  }

  setMode(actor: MarbleActor, mode: RaceMode): void {
    actor.m.mode = mode;
    this.log(`⚙ ${actor.m.displayName}: modo ${mode}.`);
  }

  /** Aplica uma decisão do rádio do box (modo temporário). */
  applyRadio(actor: MarbleActor, opt: RadioOption): void {
    const m = actor.m;
    if (opt.hold) { this.log(`📻 ${m.displayName}: mantém o ritmo.`); return; }
    if (m.state !== 'Racing') return;
    if (opt.mode !== m.mode) { m.radioPrevMode = m.mode; m.mode = opt.mode; }
    m.radioActive = true;
    m.radioModeTimer = this.teamRadio.effectSeconds;
    this.log(`📻 ${m.displayName}: ${opt.mode} por ordem do box!`);
  }

  // ---- Fim da corrida --------------------------------------------------

  private finishRace(): void {
    if (this.state === 'Finished') return;
    this.state = 'Finished';
    for (const a of this.field) a.freeze();

    const ordered = [...this.field];

    let fastest: MarbleActor | null = null;
    let best = Number.MAX_VALUE;
    for (const a of ordered) {
      if (a.m.bestLapTime < best) { best = a.m.bestLapTime; fastest = a; }
    }

    const entries: RaceResultEntry[] = ordered.map((a, i) => {
      const m = a.m;
      return {
        position: i + 1,
        marbleName: m.displayName,
        teamName: m.teamName,
        teamId: m.team.teamId,
        driverId: m.driver.driverId,
        totalTime: m.totalTime,
        pitStops: m.pitStops,
        bestLapTime: m.bestLapTime === Number.MAX_VALUE ? 0 : m.bestLapTime,
        overtakes: m.overtakes,
        finalWear: m.wear,
        finalEnergy: m.energy,
        finalFuel: m.fuel,
        finalTyre: gripDisplayLetter(m.grip.gripId),
        statusText: m.completedLaps >= this.totalLaps ? 'Finished' : 'Classificado',
        points: pointsForPosition(i + 1),
        isPlayer: m.isPlayer,
        fastestLap: a === fastest,
      };
    });

    this.result = { trackName: this.track.data.trackName, laps: this.totalLaps, entries };

    if (ordered.length > 0) {
      this.log(`🏁 ${ordered[0]!.m.displayName} venceu em ${this.track.data.trackName}!`);
    }
    this.log('🏆 Corrida encerrada!');
    this.onRaceFinished?.(this.result);
  }

  // ---- Clima dinâmico + eventos ---------------------------------------

  private updateConditions(dt: number): void {
    if (this.safetyTimer > 0) {
      this.safetyTimer -= dt;
      if (this.safetyTimer <= 0) {
        this.safetyActive = false;
        this.log('🟢 Safety Marble recolhido. Corrida liberada!');
      }
    }
    if (this.dirtyTimer > 0) {
      this.dirtyTimer -= dt;
      if (this.dirtyTimer <= 0) this.log('✨ Pista limpa novamente.');
    }

    if (this.safetyActive || this.dirtyTimer > 0) return;
    if (this.eventSystem.tick(dt) === 'DirtyTrack') {
      this.dirtyTimer = 10;
      this.log('⚠ Pista suja! Menos aderência e mais risco de erro.');
    }
  }

  weatherLabelCurrent(): string { return weatherLabel(this.currentWeather); }

  private log(msg: string): void {
    this.onRaceEvent?.(msg);
  }
}

export function weatherLabel(w: Weather): string {
  switch (w) {
    case 'Dry': return 'Seco';
    case 'Cloudy': return 'Nublado';
    case 'Damp': return 'Úmido';
    case 'LightRain': return 'Chuva leve';
    case 'HeavyRain': return 'Chuva forte';
  }
}

function inPitFlow(s: string): boolean {
  return s === 'EnteringPit' || s === 'InPit' || s === 'ExitingPit';
}

function isWetWeather(w: Weather): boolean {
  return w === 'Damp' || w === 'LightRain' || w === 'HeavyRain';
}
