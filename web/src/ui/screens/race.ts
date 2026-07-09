// =====================================================================
// Tela de corrida — HUD do protótipo aprovado (torre, cards do jogador,
// rádio, log, banner, countdown, zoom) dirigido pelo RaceManager real.
// DOM atualizado a ~15 Hz; canvas a 60 fps; medidor de FPS ao vivo.
// =====================================================================

import type { GripType, RaceMode } from '../../core/types';
import { gripDisplayColor, gripDisplayLetter } from '../../core/types';
import { Game } from '../../game/state';
import { Haptics } from '../../game/haptics';
import { RaceManager, weatherLabel, NEUTRAL_UPGRADES } from '../../sim/race';
import { RaceRecorder } from '../../sim/recorder';
import type { MarbleActor } from '../../sim/controller';
import type { RadioDecision } from '../../sim/radio';
import type { RaceSetup } from '../../sim/runtime';
import { RaceRenderer } from '../../render/renderer';
import { btn, div, el, mount } from '../dom';
import { show } from '../router';
import { goMenu, goResults, type RaceContext } from '../flow';

const NEXT_TYRE: Record<GripType, GripType> = {
  Soft: 'Medium', Medium: 'Hard', Hard: 'Intermediate', Intermediate: 'Rain', Rain: 'Soft',
};
const NEXT_MODE: Record<string, RaceMode> = { Normal: 'Push', Push: 'Save', Save: 'Normal' };

export function raceScreen(ctx: RaceContext): void {
  show('race', root => {
    // ---- Simulação -------------------------------------------------
    const p = Game.profile;
    const opts = {
      aiDifficulty: p.difficulty,
      upgrades: ctx.isChampionship && Game.championship.hasActiveSeason
        ? Game.championship.effects() : NEUTRAL_UPGRADES,
      // Identidade personalizada da garagem aplicada às bolinhas do jogador.
      playerTeamName: p.teamName,
      playerPrimaryColor: p.primaryColorHex,
      playerSecondaryColor: p.secondaryColorHex,
      playerMarbleColors: p.marbleColorHex,
    };
    const rand = ctx.seededRand ?? Math.random;
    const race = new RaceManager(ctx.setup, opts, rand);
    const recorder = new RaceRecorder(race);

    // ---- Estrutura DOM ----------------------------------------------
    const stage = div('stage');
    root.appendChild(stage);

    const canvas = el('canvas');
    stage.appendChild(canvas);

    // Top bar.
    const top = div('rhud-panel rtop');
    const circEl = div('circ');
    circEl.textContent = race.track.data.trackName;
    const wxEl = div('wx');
    wxEl.textContent = `Clima: ${weatherLabel(race.currentWeather)}`;
    const lapEl = div('lap');
    lapEl.textContent = `VOLTA 1/${race.totalLaps}`;
    const pauseBtn = el('button', 'iconbtn');
    pauseBtn.textContent = '⏸ Pausa';
    mount(top, circEl, wxEl, lapEl, pauseBtn);
    stage.appendChild(top);

    // Torre.
    const tower = div('tower');
    const towerHd = div('rhud-hd');
    towerHd.innerHTML = '<span class="k"></span>CLASSIFICAÇÃO';
    const rowsBox = div('rows');
    mount(tower, towerHd, rowsBox);
    stage.appendChild(tower);

    interface RowRefs { root: HTMLElement; acc: HTMLElement; pos: HTMLElement; arw: HTMLElement; chip: HTMLElement; code: HTMLElement; gap: HTMLElement; ty: HTMLElement; }
    const rows: RowRefs[] = [];
    for (let i = 0; i < race.field.length; i++) {
      const r = div('trow');
      const acc = div('acc'); const pos = div('pos'); const arw = div('arw');
      const chip = div('chip'); const code = div('code'); const gap = div('gap'); const ty = div('ty');
      mount(r, acc, pos, arw, chip, code, gap, ty);
      rowsBox.appendChild(r);
      rows.push({ root: r, acc, pos, arw, chip, code, gap, ty });
    }

    // Centro: overlays.
    const mid = div('rmid');
    stage.appendChild(mid);
    const banner = div('rbanner');
    // Largada estilo F1: 5 colunas de luzes.
    const lights = div('startlights');
    for (let i = 0; i < 5; i++) {
      const col = div('col');
      col.innerHTML = '<span class="sl"></span><span class="sl"></span>';
      lights.appendChild(col);
    }
    const slDots = () => Array.from(lights.querySelectorAll<HTMLElement>('.sl'));
    const fps = div('', { id: 'fpsmeter' });
    fps.textContent = '— fps';
    mount(mid, banner, lights, fps);

    const zoomCtl = div('zoomctl');
    const zIn = el('button', 'zbtn'); zIn.textContent = '+';
    const zOut = el('button', 'zbtn'); zOut.textContent = '−';
    const zHome = el('button', 'zbtn'); zHome.textContent = '⌂';
    const zFollow = el('button', 'zbtn'); zFollow.textContent = '◎';
    mount(zoomCtl, zIn, zOut, zHome, zFollow);
    mid.appendChild(zoomCtl);

    // Rádio do box.
    const radio = div('radio');
    radio.innerHTML = `<div class="rhd">RÁDIO DO BOX</div><div class="msg"></div>
      <div class="opts"></div><div class="rbar"><i style="width:100%"></i></div>`;
    mid.appendChild(radio);
    const radioMsg = radio.querySelector<HTMLElement>('.msg')!;
    const radioOpts = radio.querySelector<HTMLElement>('.opts')!;
    const radioBar = radio.querySelector<HTMLElement>('.rbar i')!;
    let radioTimer = 0;
    let radioDuration = 8;

    // Cards do jogador.
    const cardsBox = div('pcards');
    stage.appendChild(cardsBox);
    interface CardRefs {
      actor: MarbleActor; pp: HTMLElement; cur: HTMLElement; next: HTMLElement; md: HTMLElement;
      bars: Array<{ fill: HTMLElement; val: HTMLElement }>;
      pit: HTMLButtonElement; mode: HTMLButtonElement; tyre: HTMLButtonElement;
      nextTyre: GripType;
    }
    const cards: CardRefs[] = [];

    for (const actor of race.players) {
      const m = actor.m;
      const card = div('pcard');
      const topRow = div('top');
      const side = div('side'); side.style.background = m.teamPrimary;
      const nm = div('nm'); nm.textContent = m.displayName;
      const pp = div('pp'); pp.textContent = 'P—';
      mount(topRow, side, nm, pp);
      card.appendChild(topRow);

      const tyreRow = div('tyre');
      const cur = el('span'); const next = el('span'); const md = el('span');
      tyreRow.append(cur, md, next);
      card.appendChild(tyreRow);

      const bars: Array<{ fill: HTMLElement; val: HTMLElement }> = [];
      const mkBar = (lbTxt: string) => {
        const bar = div('bar');
        const lb = div('lb'); lb.textContent = lbTxt;
        const track = div('track'); const fill = div('fill'); track.appendChild(fill);
        const vl = div('vl'); vl.textContent = '—';
        mount(bar, lb, track, vl);
        card.appendChild(bar);
        bars.push({ fill, val: vl });
      };
      mkBar('Desgaste'); mkBar('Energia'); mkBar('Combust.');

      const actions = div('actions');
      const pitB = btn('PIT', 'orange', () => {
        const c = cards.find(x => x.actor === actor)!;
        race.requestPit(actor, c.nextTyre, true, 100);
        pitB.classList.add('on');
        Haptics.medium();
      });
      const modeB = btn('MODO', 'blue', () => {
        race.setMode(actor, NEXT_MODE[m.mode] ?? 'Normal');
        Haptics.tap();
      });
      const tyreB = btn('PNEU', 'purple', () => {
        const c = cards.find(x => x.actor === actor)!;
        c.nextTyre = NEXT_TYRE[c.nextTyre];
        Haptics.tap();
      });
      mount(actions, pitB, modeB, tyreB);
      card.appendChild(actions);
      cardsBox.appendChild(card);

      cards.push({ actor, pp, cur, next, md, bars, pit: pitB, mode: modeB, tyre: tyreB, nextTyre: m.grip.gripId });
    }

    // Log de eventos.
    const log = div('rhud-panel rlog');
    const logHd = div('rhud-hd');
    logHd.innerHTML = '<span class="k"></span>EVENTOS';
    const logLines = div('lines');
    mount(log, logHd, logLines);
    stage.appendChild(log);

    const logItems: Array<{ txt: string; el: HTMLElement }> = [];
    const pushLog = (msg: string) => {
      const li = div('li');
      li.textContent = msg;
      logItems.unshift({ txt: msg, el: li });
      logLines.prepend(li);
      while (logItems.length > 4) logLines.removeChild(logItems.pop()!.el);
      logItems.forEach((it, i) => it.el.classList.toggle('f', i >= 2));
    };

    // ---- Renderer + câmera -------------------------------------------
    const renderer = new RaceRenderer(canvas, race.track);
    // Faíscas de contato no ponto do mundo (batida forte vibra o aparelho).
    race.onContact = (x, y, strength) => {
      renderer.spark(x, y, strength);
      if (strength > 1.4) Haptics.medium();
    };

    zIn.addEventListener('click', () => renderer.zoomBy(1.35));
    zOut.addEventListener('click', () => renderer.zoomBy(1 / 1.35));
    zHome.addEventListener('click', () => { renderer.resetCamera(); zFollow.classList.remove('on'); });
    zFollow.addEventListener('click', () => {
      renderer.cam.follow = !renderer.cam.follow;
      zFollow.classList.toggle('on', renderer.cam.follow);
    });

    canvas.addEventListener('wheel', e => {
      e.preventDefault();
      const rect = canvas.getBoundingClientRect();
      const pivot = renderer.screenToWorld(e.clientX - rect.left, e.clientY - rect.top);
      renderer.zoomBy(e.deltaY < 0 ? 1.18 : 1 / 1.18, pivot);
    }, { passive: false });

    // Arrasto + pinch.
    const pointers = new Map<number, { x: number; y: number }>();
    let pinchDist = 0;
    canvas.addEventListener('pointerdown', e => {
      canvas.setPointerCapture(e.pointerId);
      pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (pointers.size === 2) {
        const [a, b] = [...pointers.values()];
        pinchDist = Math.hypot(a!.x - b!.x, a!.y - b!.y);
      }
    });
    canvas.addEventListener('pointermove', e => {
      const prev = pointers.get(e.pointerId);
      if (!prev) return;
      if (pointers.size === 1) {
        renderer.panBy(e.clientX - prev.x, e.clientY - prev.y);
      }
      pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (pointers.size === 2) {
        const [a, b] = [...pointers.values()];
        const d = Math.hypot(a!.x - b!.x, a!.y - b!.y);
        if (pinchDist > 0) renderer.zoomBy(d / pinchDist);
        pinchDist = d;
      }
    });
    const endPointer = (e: PointerEvent) => { pointers.delete(e.pointerId); pinchDist = 0; };
    canvas.addEventListener('pointerup', endPointer);
    canvas.addEventListener('pointercancel', endPointer);

    const onResize = () => renderer.resize();
    window.addEventListener('resize', onResize);

    // ---- Eventos da corrida ------------------------------------------
    let bannerTimer = 0;
    const showBanner = (txt: string, color: string) => {
      banner.textContent = txt;
      banner.style.color = color;
      banner.classList.add('show');
      bannerTimer = 2.6;
    };

    // Largada estilo F1: as luzes acendem coluna a coluna; no GO, apagam
    // todas de uma vez (lights out) com um toque háptico forte.
    let lightsStarted = false;
    const lightTimers: number[] = [];
    race.onCountdown = v => {
      lights.classList.add('show');
      if (!lightsStarted && v > 0) {
        lightsStarted = true;
        const dots = slDots();
        for (let col = 0; col < 5; col++) {
          lightTimers.push(window.setTimeout(() => {
            dots[col * 2]?.classList.add('on');
            dots[col * 2 + 1]?.classList.add('on');
            Haptics.tap();
          }, col * 560));
        }
      }
      if (v === 0) {
        for (const t of lightTimers) clearTimeout(t);
        slDots().forEach(d => d.classList.remove('on'));
        lights.classList.add('out');
        Haptics.heavy();
        window.setTimeout(() => { lights.style.display = 'none'; }, 320);
        showBanner('GO!', 'var(--cyan)');
      }
    };
    race.onRaceEvent = msg => {
      pushLog(msg);
      if (msg.includes('Clima mudou')) showBanner(msg.replace('🌦 ', '').toUpperCase(), 'var(--blue)');
      if (msg.includes('Safety Marble na')) showBanner('SAFETY MARBLE', 'var(--gold)');
      if (msg.includes('Bandeirada')) showBanner('BANDEIRADA!', 'var(--gold)');
    };

    let pendingRadio: RadioDecision | null = null;
    race.onRadioDecision = d => {
      pendingRadio = d;
      radioMsg.textContent = d.prompt;
      radioOpts.innerHTML = '';
      for (const opt of d.options) {
        const cls = opt.tone === 'Attack' ? 'red' : opt.tone === 'Save' ? 'grn' : opt.tone === 'Defend' ? 'blue' : 'ghost';
        radioOpts.appendChild(btn(opt.label, cls, () => {
          race.applyRadio(d.actor, opt);
          radio.classList.remove('show');
          pendingRadio = null;
          Haptics.medium();
        }));
      }
      radioTimer = d.duration;
      radioDuration = d.duration;
      radio.classList.add('show');
      Haptics.tap(); // chega uma decisão
    };

    let finished = false;
    race.onRaceFinished = result => {
      if (finished) return;
      finished = true;
      // Pequena pausa para o jogador ver a bandeirada antes do resultado.
      setTimeout(() => {
        if (!alive) return;
        goResults(result, ctx, recorder);
      }, 1400);
    };

    // ---- Pausa --------------------------------------------------------
    let paused = false;
    let pauseEl: HTMLElement | null = null;
    const openPause = () => {
      if (paused || race.state === 'Finished') return;
      paused = true;
      race.pause();
      pauseEl = div('pause-overlay');
      const box = div('panel pause-box');
      const h = el('h2'); h.textContent = 'PAUSADO';
      const sub = div('sub'); sub.textContent = `Volta ${Math.min(race.totalLaps, (race.leader?.m.completedLaps ?? 0) + 1)}/${race.totalLaps} · Clima: ${race.weatherLabelCurrent()}`;
      mount(box, h, sub,
        btn('Continuar', 'primary', closePause),
        btn('Reiniciar Corrida', 'ghost', () => { cleanup(); raceScreen(ctx); }),
        btn('Sair para o Menu', 'red', () => { cleanup(); goMenu(); }),
      );
      pauseEl.appendChild(box);
      stage.appendChild(pauseEl);
    };
    const closePause = () => {
      paused = false;
      race.resume();
      pauseEl?.remove();
      pauseEl = null;
    };
    pauseBtn.addEventListener('click', openPause);
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') paused ? closePause() : openPause(); };
    window.addEventListener('keydown', onKey);

    // ---- Loop principal ----------------------------------------------
    let alive = true;
    let last = performance.now();
    let hudAccum = 0;
    let fpsAcc = 0, fpsFrames = 0, fpsWorst = 0;

    const medal = (p: number) => (p === 1 ? 'var(--gold)' : p === 2 ? '#cdd5e3' : p === 3 ? '#d9975a' : '#e6eeff');

    // Posição anterior POR BOLINHA (não por linha da torre) para a seta ▲/▼.
    const prevPos = new Map<MarbleActor, number>();
    // Flash one-shot de mudança de posição (reflow força o replay da animação).
    const flashRow = (elm: HTMLElement, cls: 'up' | 'down'): void => {
      elm.classList.remove('up', 'down');
      void elm.offsetWidth;
      elm.classList.add(cls);
    };

    function updateHud(): void {
      const leader = race.leader;
      const lap = Math.min(race.totalLaps, (leader?.m.completedLaps ?? 0) + 1);
      lapEl.textContent = `VOLTA ${lap}/${race.totalLaps}`;
      wxEl.textContent = `Clima: ${race.weatherLabelCurrent()}`;

      // Detentor da volta mais rápida (gap em roxo, à la transmissão).
      let flActor: MarbleActor | null = null;
      let flBest = Number.MAX_VALUE;
      for (const a of race.field) {
        if (a.m.bestLapTime < flBest) { flBest = a.m.bestLapTime; flActor = a; }
      }

      // Torre.
      for (let i = 0; i < race.field.length && i < rows.length; i++) {
        const actor = race.field[i]!;
        const m = actor.m;
        const r = rows[i]!;
        r.root.classList.toggle('me', m.isPlayer);
        r.acc.style.background = m.teamPrimary;
        r.pos.textContent = String(i + 1);
        r.pos.style.color = medal(i + 1);
        // Compara a posição da BOLINHA com a dela própria no update anterior.
        const cur = i + 1;
        const prev = prevPos.get(actor) ?? cur;
        if (cur < prev) {
          r.arw.textContent = '▲'; r.arw.style.color = 'var(--delta-up)';
          flashRow(r.root, 'up');
          if (m.isPlayer) Haptics.tap(); // ultrapassagem do jogador
        } else if (cur > prev) {
          r.arw.textContent = '▼'; r.arw.style.color = 'var(--delta-down)';
          flashRow(r.root, 'down');
        } else {
          r.arw.textContent = '';
        }
        prevPos.set(actor, cur);
        r.chip.textContent = m.driver.shortCode.slice(-1);
        r.chip.style.background = m.teamPrimary;
        r.chip.style.color = m.teamSecondary;
        r.code.textContent = m.driver.shortCode;
        r.code.style.color = i === 0 ? 'var(--gold)' : '#e6eeff';
        r.gap.textContent = i === 0 ? 'Líder' : `+${m.gapToLeader.toFixed(1)}`;
        r.gap.style.color = actor === flActor ? 'var(--delta-best)' : 'var(--dim)';
        const inPit = m.state === 'InPit' || m.state === 'EnteringPit' || m.state === 'ExitingPit';
        r.ty.textContent = inPit ? 'P' : gripDisplayLetter(m.grip.gripId);
        r.ty.style.color = inPit ? 'var(--orange)' : gripDisplayColor(m.grip.gripId);
        r.ty.classList.toggle('pit', inPit);
      }

      // Cards.
      for (const c of cards) {
        const m = c.actor.m;
        c.pp.textContent = `P${m.position || '—'}`;
        c.cur.textContent = `Pneu: ${gripDisplayLetter(m.grip.gripId)}`;
        c.md.textContent = `Modo: ${m.mode}`;
        c.next.textContent = `Próx: ${gripDisplayLetter(c.nextTyre)}`;
        const [wearB, enB, fuB] = c.bars;
        wearB!.fill.style.width = `${m.wear}%`;
        wearB!.fill.style.background = m.wear > 70 ? 'var(--red)' : 'var(--wear)';
        wearB!.val.textContent = `${Math.round(m.wear)}%`;
        enB!.fill.style.width = `${m.energy}%`;
        enB!.fill.style.background = m.energy < 20 ? 'var(--wear)' : 'var(--energy)';
        enB!.val.textContent = `${Math.round(m.energy)}%`;
        fuB!.fill.style.width = `${m.fuel}%`;
        fuB!.fill.style.background = m.fuel < 25 ? 'var(--red)' : 'var(--fuel)';
        fuB!.val.textContent = `${Math.round(m.fuel)}%`;
        const inPit = m.state === 'InPit' || m.state === 'EnteringPit' || m.state === 'ExitingPit';
        c.pit.textContent = inPit ? 'IN PIT' : 'PIT';
        c.pit.classList.toggle('on', m.pitRequested || inPit);
        if (!m.pitRequested && !inPit) c.pit.classList.remove('on');
      }
    }

    function frame(now: number): void {
      if (!alive) return;
      const rawMs = now - last;
      let dt = rawMs / 1000;
      last = now;
      if (dt > 0.1) dt = 0.1;

      // FPS ao vivo (~2x/s).
      if (rawMs > 0 && rawMs < 500) { fpsAcc += rawMs; fpsFrames++; if (rawMs > fpsWorst) fpsWorst = rawMs; }
      if (fpsAcc >= 500 && fpsFrames > 0) {
        const avg = fpsAcc / fpsFrames;
        const f = Math.round(1000 / avg);
        const col = f >= 55 ? 'var(--green)' : f >= 40 ? 'var(--wear)' : 'var(--red)';
        fps.style.color = col;
        fps.style.borderColor = col;
        fps.textContent = `${f} fps · ${avg.toFixed(1)}ms`;
        fpsAcc = 0; fpsFrames = 0; fpsWorst = 0;
      }

      if (!paused) {
        race.step(dt);
        recorder.tick(dt);
        // Trilhas (para o renderer).
        for (const a of race.field) {
          const m = a.m;
          m.trail.push({ x: m.x, y: m.y });
          if (m.trail.length > 9) m.trail.shift();
        }
      }

      // Rádio: countdown.
      if (pendingRadio && radioTimer > 0 && !paused) {
        radioTimer -= dt;
        radioBar.style.width = `${Math.max(0, (radioTimer / radioDuration) * 100)}%`;
        if (radioTimer <= 0) {
          radio.classList.remove('show');
          pendingRadio = null;
        }
      }

      if (bannerTimer > 0) {
        bannerTimer -= dt;
        if (bannerTimer <= 0) banner.classList.remove('show');
      }

      // Câmera segue o líder quando em modo follow.
      renderer.updateCamera(dt, race.leader ? { x: race.leader.m.x, y: race.leader.m.y } : null);
      renderer.render(race.field, race.currentWeather, now);

      // HUD DOM a ~15 Hz (evita jank de layout no mobile).
      hudAccum += dt;
      if (hudAccum >= 1 / 15) {
        hudAccum = 0;
        updateHud();
      }

      requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);
    updateHud();

    // Teardown ao trocar de tela.
    function cleanup(): void {
      alive = false;
      window.removeEventListener('resize', onResize);
      window.removeEventListener('keydown', onKey);
    }
    return cleanup;
  });
}
