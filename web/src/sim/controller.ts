// =====================================================================
// Movimento guiado + cérebro da bolinha — port de AI/{MarbleController,
// MarbleAI}.cs. Sem Rigidbody: integração cinemática 2D (a velocidade
// converge para a desejada; separação suave entre bolinhas).
// =====================================================================

import type { Personality, Vec2, Weather } from '../core/types';
import { driverMult } from '../core/types';
import { Balance } from '../data/balance';
import { errorChance, finalSpeed } from './formulas';
import type { MarbleRuntime } from './runtime';
import type { RacingLine, Track } from './track';

/** Condições vivas da corrida (Core/IRaceConditions.cs). */
export interface RaceConditions {
  currentWeather: Weather;
  trackSpeedMod: number;
  trackErrorAdd: number;
  safetyMarbleActive: boolean;
}

const MARBLE_RADIUS = 0.5;

/** Ruído 1D suave e determinístico (substitui Mathf.PerlinNoise). */
function smoothNoise(t: number, seed: number): number {
  const x = t + seed;
  const i = Math.floor(x);
  const f = x - i;
  const u = f * f * (3 - 2 * f);
  const h = (k: number) => {
    const s = Math.sin(k * 127.1 + seed * 311.7) * 43758.5453;
    return s - Math.floor(s);
  };
  return h(i) * (1 - u) + h(i + 1) * u; // 0..1
}

export class MarbleActor {
  readonly m: MarbleRuntime;
  private track: Track;

  // Definidos pela IA a cada passo:
  desiredSpeed = 0;
  line: RacingLine = 'Ideal';
  externalTarget: Vec2 | null = null; // usado no pit (override do alvo)

  // Velocidade atual (vetor)
  vx = 0;
  vy = 0;
  distanceLastStep = 0;

  onContact: ((self: MarbleActor, other: MarbleActor, impact: number) => void) | null = null;

  private stuckTimer = 0;
  private errorRecoverTimer = 0;
  private attackSide: RacingLine = 'Ideal';
  private attackTimer = 0;
  private paceSeed: number;
  private simTime = 0;
  private rand: () => number;

  /** Reação de largada (0.85..1.06): melhor aceleração/consistência = arranca melhor. */
  readonly launchReaction: number;

  constructor(m: MarbleRuntime, track: Track, rand: () => number = Math.random) {
    this.m = m;
    this.track = track;
    this.rand = rand;
    this.paceSeed = rand() * 100;
    const skill = (m.driver.acceleration * 0.7 + m.driver.consistency * 0.3) / 100;
    this.launchReaction = 0.85 + skill * 0.18 + (rand() - 0.5) * 0.05;
  }

  get currentSpeed(): number { return Math.hypot(this.vx, this.vy); }

  // Objeto reaproveitado para `pos`: evita alocar um Vec2 por acesso no loop
  // quente (~4×/frame/bolinha). Todos os chamadores consomem o valor na hora,
  // sem reter a referência entre dois acessos, então o compartilhamento é seguro.
  private _pos: Vec2 = { x: 0, y: 0 };
  get pos(): Vec2 { this._pos.x = this.m.x; this._pos.y = this.m.y; return this._pos; }

  // ------------------------------------------------------------------
  // Física guiada (MarbleController.PhysicsStep)
  // ------------------------------------------------------------------
  physicsStep(dt: number, field: MarbleActor[]): void {
    this.simTime += dt;
    const m = this.m;
    const pos = this.pos;

    const inPitFlow = m.state === 'EnteringPit' || m.state === 'InPit' || m.state === 'ExitingPit';

    // 1) Alvo de direção.
    const target = this.externalTarget
      ?? this.track.getSteerTarget(this.line, pos, Math.round(Balance.cornerLookAhead));

    let dx = target.x - pos.x, dy = target.y - pos.y;
    const dl = Math.hypot(dx, dy);
    if (dl > 1e-2) { dx /= dl; dy /= dl; }

    // 2) Steering: em pista, separação suave; no pit, on-rails.
    let sx = dx, sy = dy;
    if (!inPitFlow) {
      const sep = this.computeSeparation(pos, field);
      sx = dx + sep.x * 0.6;
      sy = dy + sep.y * 0.6;
      const sl = Math.hypot(sx, sy) || 1;
      sx /= sl; sy /= sl;
    }

    // 3) Velocidade converge para a desejada.
    const desVx = sx * this.desiredSpeed, desVy = sy * this.desiredSpeed;
    const accel = Balance.baseSpeed * 3;
    const dvx = desVx - this.vx, dvy = desVy - this.vy;
    const dvLen = Math.hypot(dvx, dvy);
    const maxStep = accel * dt;
    if (dvLen <= maxStep || dvLen < 1e-6) {
      this.vx = desVx; this.vy = desVy;
    } else {
      this.vx += (dvx / dvLen) * maxStep;
      this.vy += (dvy / dvLen) * maxStep;
    }

    // 4) Integra posição.
    m.x += this.vx * dt;
    m.y += this.vy * dt;
    const sp = this.currentSpeed;
    if (sp > 0.01) m.heading = Math.atan2(this.vy, this.vx);
    m.speed = sp;
    this.distanceLastStep = sp * dt;

    // 5) Contato entre bolinhas (substitui OnCollisionEnter): impacto
    //    aproximado pela velocidade relativa quando muito próximas.
    if (!inPitFlow) {
      for (const other of field) {
        if (other === this) continue;
        const om = other.m;
        if (om.state === 'InPit' || om.state === 'EnteringPit' || om.state === 'ExitingPit') continue;
        const ddx = om.x - m.x, ddy = om.y - m.y;
        const d = Math.hypot(ddx, ddy);
        if (d < MARBLE_RADIUS * 2 && d > 1e-4) {
          const rvx = this.vx - other.vx, rvy = this.vy - other.vy;
          const impact = Math.hypot(rvx, rvy);
          // Empurra para fora (resolução simples de sobreposição).
          const push = (MARBLE_RADIUS * 2 - d) * 0.5;
          m.x -= (ddx / d) * push;
          m.y -= (ddy / d) * push;
          if (impact >= 2.0) this.onContact?.(this, other, impact);
        }
      }
    }
  }

  private computeSeparation(pos: Vec2, field: MarbleActor[]): Vec2 {
    let sumX = 0, sumY = 0;
    const range = MARBLE_RADIUS * 3;
    for (const other of field) {
      if (other === this) continue;
      const om = other.m;
      if (om.state === 'InPit' || om.state === 'EnteringPit' || om.state === 'ExitingPit') continue;
      const ax = pos.x - om.x, ay = pos.y - om.y;
      const d = Math.hypot(ax, ay);
      if (d > 0.01 && d < range) {
        const w = 1 - d / range;
        sumX += (ax / d) * w;
        sumY += (ay / d) * w;
      }
    }
    return { x: sumX, y: sumY };
  }

  /** Detecção de "presa" e recuperação (PRD 13.6). Retorna true se reposicionou. */
  handleStuckRecovery(dt: number): boolean {
    const m = this.m;
    if (m.state === 'InPit' || m.state === 'Finished') return false;

    if (this.currentSpeed < Balance.stuckSpeedThreshold) {
      this.stuckTimer += dt;
      if (this.stuckTimer >= Balance.stuckRecoveryTime) {
        const { nearestIndex } = this.track.idealLine.closestArcFraction(this.pos);
        const wp = this.track.idealLine.point(nearestIndex + 1);
        m.x = wp.x; m.y = wp.y;
        this.vx = 0; this.vy = 0;
        this.stuckTimer = 0;
        m.totalTime += 1.5; // pequena penalidade
        return true;
      }
    } else this.stuckTimer = 0;
    return false;
  }

  freeze(): void {
    this.vx = 0; this.vy = 0;
    this.desiredSpeed = 0;
    this.m.speed = 0;
  }

  // ------------------------------------------------------------------
  // Cérebro (MarbleAI.Think)
  // ------------------------------------------------------------------
  think(field: MarbleActor[], dt: number, cond: RaceConditions): void {
    const m = this.m;
    if (m.state === 'Finished' || m.state === 'Retired') {
      this.desiredSpeed = 0;
      return;
    }

    const weather = cond.currentWeather;

    // 1) Velocidade-base teórica com variação de ritmo por bolinha.
    const maxSpeed = finalSpeed(m, weather, cond.trackSpeedMod) * this.paceNoise();

    // 2) Freia em curva fechada, acelera nas retas.
    const curvature = this.track.curvatureAhead(this.pos, 2);
    const controlFactor = driverMult.control(m.driver) * m.grip.gripMultiplier
      * m.surface.controlModifier * m.upgControlFactor;
    const bravery = this.bravery();
    const cornerT = Math.min(1, Math.max(0, curvature / Math.max(0.5, controlFactor) * (2 - bravery)));
    let corner = maxSpeed + (maxSpeed * 0.34 - maxSpeed) * cornerT;

    // 3) Disputa com compromisso de lado.
    let line: RacingLine = 'Ideal';
    const ahead = this.findAhead(field);
    if (this.attackTimer > 0) {
      this.attackTimer -= dt;
      line = this.attackSide;
      corner *= 1.05; // empurra para concluir a ultrapassagem
    } else if (ahead) {
      const res = this.decideOvertakeOrFollow(ahead, corner);
      line = res.line;
      corner = res.speed;
    } else {
      line = this.defendIfThreatened(field, 'Ideal');
    }

    // 4) Reação a desgaste/energia.
    if (m.wear > Balance.wearCriticalThreshold) corner *= 0.9;
    if (m.energy < Balance.lowChargeThreshold) corner *= 0.95;
    if (m.mode === 'Save') corner *= 0.99;

    // 5) Erros ocasionais.
    if (this.errorRecoverTimer > 0) {
      this.errorRecoverTimer -= dt;
      corner *= 0.65; // perdeu tempo no erro
    } else if (this.rand() < errorChance(m, weather, cond.trackErrorAdd) * dt) {
      this.errorRecoverTimer = 0.3 + this.rand() * 0.7;
      line = 'Outside'; // abriu a curva
    }

    this.desiredSpeed = Math.max(0, corner);
    this.line = line;
  }

  /** Variação suave de ritmo; menos consistente => oscila mais. */
  private paceNoise(): number {
    let amp = (1 - this.m.driver.consistency / 100) * 0.07;
    if (this.m.driver.personality === 'Rookie') amp += 0.03;
    if (this.m.driver.personality === 'Veteran') amp *= 0.6;
    const noise = smoothNoise(this.simTime * 0.35 + this.paceSeed, this.paceSeed) - 0.5;
    return 1 + noise * 2 * amp;
  }

  /** Coragem na curva (RiskTaker entra mais rápido). */
  private bravery(): number {
    switch (this.m.driver.personality as Personality) {
      case 'RiskTaker': return 1.25;
      case 'Aggressive': return 1.12;
      case 'Smooth': return 1.05;
      case 'Conservative': return 0.85;
      case 'Rookie': return 0.9;
      default: return 1;
    }
  }

  private findAhead(field: MarbleActor[]): MarbleActor | null {
    const sp = this.currentSpeed;
    if (sp < 1e-3) return null;
    const fx = this.vx / sp, fy = this.vy / sp;
    let best: MarbleActor | null = null;
    let bestDist: number = Balance.overtakeDetectDistance;
    for (const other of field) {
      if (other === this) continue;
      if (other.m.state !== 'Racing') continue;
      const tx = other.m.x - this.m.x, ty = other.m.y - this.m.y;
      const d = Math.hypot(tx, ty);
      if (d < bestDist && d > 1e-4 && (fx * tx + fy * ty) / d > 0.6) {
        best = other;
        bestDist = d;
      }
    }
    return best;
  }

  private decideOvertakeOrFollow(ahead: MarbleActor, speed: number): { line: RacingLine; speed: number } {
    const m = this.m;
    const faster = this.desiredSpeed >= ahead.currentSpeed * 0.97;
    const hasEnergy = m.energy > Balance.lowChargeThreshold;
    const freshTyre = m.wear < 70;

    let willAttack = m.driver.aggression / 100;
    switch (m.driver.personality) {
      case 'Aggressive': willAttack += 0.2; break;
      case 'RiskTaker': willAttack += 0.28; break;
      case 'Conservative': willAttack -= 0.2; break;
      case 'Defensive': willAttack -= 0.12; break;
      case 'Veteran': willAttack += 0.05; break;
    }
    if (m.mode === 'Push' || m.mode === 'Attack') willAttack += 0.18;
    if (m.mode === 'Save') willAttack -= 0.15;

    // Curva fechada não é bom lugar para atacar.
    const curvature = this.track.curvatureAhead(this.pos, 2);
    const goodZone = curvature < 0.25;

    if (faster && hasEnergy && freshTyre && goodZone && this.rand() < willAttack * 0.6) {
      this.attackSide = this.chooseAttackSide();
      this.attackTimer = 1.5 + this.rand() * 1.1;
      return { line: this.attackSide, speed: speed * 1.05 };
    }

    // Sem ataque: segue atrás, reduzindo um pouco para não bater.
    return { line: 'Ideal', speed: Math.min(speed, ahead.currentSpeed * 0.99) };
  }

  private chooseAttackSide(): RacingLine {
    switch (this.m.driver.personality) {
      case 'Aggressive':
      case 'RiskTaker':
        return this.rand() < 0.7 ? 'Inside' : 'Outside';
      case 'Conservative':
      case 'Smooth':
        return this.rand() < 0.6 ? 'Outside' : 'Inside';
      default:
        return this.rand() < 0.5 ? 'Inside' : 'Outside';
    }
  }

  /** Defensivos/veteranos bloqueiam a linha interna quando ameaçados. */
  private defendIfThreatened(field: MarbleActor[], fallback: RacingLine): RacingLine {
    const p = this.m.driver.personality;
    if (p !== 'Defensive' && p !== 'Veteran') return fallback;

    const sp = this.currentSpeed;
    if (sp < 1e-3) return fallback;
    const bx = -this.vx / sp, by = -this.vy / sp;
    for (const other of field) {
      if (other === this || other.m.state !== 'Racing') continue;
      const tx = other.m.x - this.m.x, ty = other.m.y - this.m.y;
      const d = Math.hypot(tx, ty);
      if (d < Balance.overtakeDetectDistance && d > 1e-4 && (bx * tx + by * ty) / d > 0.6)
        return 'Inside'; // fecha a porta
    }
    return fallback;
  }
}
