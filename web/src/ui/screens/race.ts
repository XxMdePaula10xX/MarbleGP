// =====================================================================
// Tela de corrida — HUD do protótipo aprovado (torre, cards do jogador,
// rádio, log, banner, countdown, zoom) dirigido pelo RaceManager real.
// DOM atualizado a ~15 Hz; canvas a 60 fps; medidor de FPS ao vivo.
// =====================================================================

import type { GripType, RaceMode } from '../../core/types';
import { gripDisplayColor, gripDisplayLetter } from '../../core/types';
import { Game } from '../../game/state';
import { Haptics } from '../../game/haptics';
import { Sound } from '../../game/audio';
import { seededRandom } from '../../game/daily';
import { RaceManager, weatherLabel, NEUTRAL_UPGRADES } from '../../sim/race';
import { RaceRecorder } from '../../sim/recorder';
import type { MarbleActor } from '../../sim/controller';
import type { RadioDecision } from '../../sim/radio';
import type { RaceSetup } from '../../sim/runtime';
import { RaceRenderer } from '../../render/renderer';
import { teamEmblem } from '../emblem';
import { btn, div, el, mount } from '../dom';
import { show } from '../router';
import { goMenu, goResults, type RaceContext } from '../flow';

export function raceScreen(ctx: RaceContext): void {
  show('race', root => {
    // ---- Simulação -------------------------------------------------
    const p = Game.profile;
    const opts = {
      aiDifficulty: p.difficulty,
      upgrades: ctx.isChampionship && Game.championship.hasActiveSeason
        ? Game.championship.effects() : NEUTRAL_UPGRADES,
      // Garagem: nome + cor secundária (primária fixa da equipe).
      playerTeamName: p.teamName,
      playerSecondaryColor: p.secondaryColorHex,
    };
    // Desafio do Dia: reconstrói o RNG A PARTIR DO SEED a cada entrada na
    // tela. Assim "Reiniciar Corrida" reproduz o MESMO cenário oficial, em
    // vez de reusar um seededRand já consumido na tentativa anterior.
    const rand = ctx.isDaily && ctx.dailyDef
      ? seededRandom(ctx.dailyDef.seed)
      : (ctx.seededRand ?? Math.random);
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
    const towerHd = div('rhud-hd tower-hd');
    towerHd.innerHTML = '<span class="k"></span><span class="tt">CLASSIFICAÇÃO</span>';
    // Botão de recolher (modo ultracompacto: só pos, sigla e pneu).
    const collapseBtn = el('button', 'tower-collapse');
    collapseBtn.innerHTML = '⇲';
    collapseBtn.title = 'Recolher classificação';
    collapseBtn.addEventListener('click', () => {
      const compact = tower.classList.toggle('compact');
      collapseBtn.innerHTML = compact ? '⇱' : '⇲';
      Haptics.tap();
    });
    towerHd.appendChild(collapseBtn);
    const rowsBox = div('rows');
    mount(tower, towerHd, rowsBox);
    stage.appendChild(tower);

    interface RowRefs { root: HTMLElement; acc: HTMLElement; pos: HTMLElement; arw: HTMLElement; chip: HTMLElement; code: HTMLElement; gap: HTMLElement; ty: HTMLElement; }
    const rows: RowRefs[] = [];
    for (let i = 0; i < race.field.length; i++) {
      const r = div('trow');
      if (i === 0) r.classList.add('leader');   // linha do líder (dourada)
      if (i >= 10) r.classList.add('rank-lo');  // fora do top 10: mais compacta
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
    const flash = div('start-flash'); // clarão branco no GO
    const showFps = Game.settings.showFps;
    const fps = div('', { id: 'fpsmeter' });
    fps.textContent = '— fps';
    if (!showFps) fps.style.display = 'none';
    mount(mid, banner, lights, flash, fps);

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

    // Popup de escolha (pit/modo) sobre o palco — não pausa a corrida.
    const openChoice = (
      title: string, subtitle: string,
      opts: Array<{ label: string; sub?: string; cls: string; onPick: () => void }>,
    ): void => {
      const pop = div('race-popup');
      const box = div('rp-box');
      const h = div('rp-title'); h.textContent = title;
      const s = div('rp-sub'); s.textContent = subtitle;
      const grid = div('rp-grid');
      for (const o of opts) {
        const b = el('button', `rp-opt ${o.cls}`);
        b.type = 'button';
        b.innerHTML = `<span class="rp-lb">${o.label}</span>${o.sub ? `<span class="rp-sb">${o.sub}</span>` : ''}`;
        b.addEventListener('click', () => { Haptics.medium(); o.onPick(); pop.remove(); });
        grid.appendChild(b);
      }
      const cancel = btn('Cancelar', 'ghost', () => pop.remove());
      cancel.classList.add('rp-cancel');
      mount(box, h, s, grid, cancel);
      pop.appendChild(box);
      pop.addEventListener('click', e => { if (e.target === pop) pop.remove(); });
      stage.appendChild(pop);
    };

    // Cards do jogador.
    const cardsBox = div('pcards');
    stage.appendChild(cardsBox);
    interface CardRefs {
      actor: MarbleActor; pp: HTMLElement; tyreBadge: HTMLElement; modeBadge: HTMLElement;
      bars: Array<{ fill: HTMLElement; val: HTMLElement }>;
      pit: HTMLButtonElement;
    }
    const cards: CardRefs[] = [];

    const TYRES: Array<{ g: GripType; sub: string }> = [
      { g: 'Soft', sub: 'macio' }, { g: 'Medium', sub: 'médio' }, { g: 'Hard', sub: 'duro' },
      { g: 'Intermediate', sub: 'úmido' }, { g: 'Rain', sub: 'chuva' },
    ];
    const MODES: Array<{ m: RaceMode; label: string; sub: string; cls: string }> = [
      { m: 'Save', label: 'SAVE', sub: 'economiza', cls: 'grn' },
      { m: 'Normal', label: 'NORMAL', sub: 'equilíbrio', cls: 'blue' },
      { m: 'Push', label: 'PUSH', sub: 'máximo', cls: 'red' },
    ];

    for (const actor of race.players) {
      const m = actor.m;
      const card = div('pcard');
      const topRow = div('top');
      const side = div('side'); side.style.background = m.teamPrimary;
      const em = teamEmblem(m.team.teamId, 20);
      const nm = div('nm'); nm.textContent = m.displayName;
      const pp = div('pp'); pp.textContent = 'P—';
      mount(topRow, side, em, nm, pp);
      card.appendChild(topRow);

      // Badges: pneu + modo.
      const badges = div('badges');
      const tyreBadge = div('badge tyb');
      const modeBadge = div('badge mdb');
      mount(badges, tyreBadge, modeBadge);
      card.appendChild(badges);

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
        // Popup: escolher pneu e confirmar o pit (fluxo intuitivo — o pneu só é
        // escolhido no momento do pit, como pediu o feedback).
        Haptics.tap();
        openChoice(`Pit de ${m.displayName}`, 'Escolha o pneu para a parada:',
          TYRES.map(t => ({
            label: gripDisplayLetter(t.g), sub: t.sub, cls: `tyre-${t.g}`,
            onPick: () => { race.requestPit(actor, t.g, true, 100); pitB.classList.add('on'); },
          })));
      });
      const modeB = btn('MODO', 'blue', () => {
        Haptics.tap();
        openChoice(`Modo de ${m.displayName}`, 'Como administrar o ritmo?',
          MODES.map(md => ({
            label: md.label, sub: md.sub, cls: md.cls,
            onPick: () => race.setMode(actor, md.m),
          })));
      });
      mount(actions, pitB, modeB);
      card.appendChild(actions);
      cardsBox.appendChild(card);

      cards.push({ actor, pp, tyreBadge, modeBadge, bars, pit: pitB });
    }

    // Log de eventos.
    const log = div('rhud-panel rlog');
    const logHd = div('rhud-hd');
    logHd.innerHTML = '<span class="k"></span>EVENTOS';
    const logLines = div('lines');
    mount(log, logHd, logLines);
    stage.appendChild(log);

    // ---- Arrastar para recolher (torre e log) -----------------------
    // Gesto: arraste o painel na direção da borda para recolher; arraste
    // de volta (ou toque quando recolhido) para reabrir. O dedo acompanha
    // o painel; ao soltar, ele "prende" no estado mais próximo.
    const makeStowable = (
      panel: HTMLElement, axis: 'x' | 'y', stowSign: 1 | -1, keepPx: number,
    ): void => {
      let stowed = false;
      let dragging = false;
      let decided = false;
      let cancelled = false;
      let start = 0;
      let startOther = 0;
      let base = 0;      // posição no início do arrasto
      let curPx = 0;
      const size = () => (axis === 'x' ? panel.offsetWidth : panel.offsetHeight);
      const stowedPx = () => stowSign * (size() - keepPx);
      const applyPx = (px: number) => {
        panel.style.transform = axis === 'x' ? `translateX(${px}px)` : `translateY(${px}px)`;
      };
      const settle = (toStowed: boolean) => {
        stowed = toStowed;
        panel.classList.toggle('stowed', toStowed);
        panel.style.transition = 'transform 0.28s cubic-bezier(0.34,1.2,0.4,1)';
        applyPx(toStowed ? stowedPx() : 0);
      };
      panel.addEventListener('pointerdown', (e) => {
        if ((e.target as HTMLElement).closest('button')) return; // não a partir de botões
        dragging = true; decided = false; cancelled = false;
        start = axis === 'x' ? e.clientX : e.clientY;
        startOther = axis === 'x' ? e.clientY : e.clientX;
        base = stowed ? stowedPx() : 0;
        curPx = base;
        panel.style.transition = 'none';
        try { panel.setPointerCapture(e.pointerId); } catch { /* ok */ }
      });
      panel.addEventListener('pointermove', (e) => {
        if (!dragging || cancelled) return;
        const p = axis === 'x' ? e.clientX : e.clientY;
        const other = axis === 'x' ? e.clientY : e.clientX;
        const d = p - start;
        if (!decided) {
          if (Math.abs(d) < 5 && Math.abs(other - startOther) < 5) return;
          decided = true;
          if (Math.abs(other - startOther) > Math.abs(d)) { cancelled = true; return; } // gesto no outro eixo
        }
        const lo = Math.min(0, stowedPx());
        const hi = Math.max(0, stowedPx());
        curPx = Math.max(lo, Math.min(hi, base + d));
        applyPx(curPx);
        e.preventDefault();
      });
      const end = () => {
        if (!dragging) return;
        dragging = false;
        const moved = Math.abs(curPx - base);
        if (moved < 6) { settle(!stowed); return; }        // toque curto alterna
        const past = Math.abs(curPx) > Math.abs(stowedPx()) * 0.4; // passou de 40% → recolhe
        settle(past);
        Haptics.tap();
      };
      panel.addEventListener('pointerup', end);
      panel.addEventListener('pointercancel', end);
    };
    makeStowable(tower, 'x', -1, 26);   // torre recolhe para a esquerda
    makeStowable(log, 'y', 1, 30);      // log recolhe para baixo (deixa o cabeçalho)
    // Alça de arrasto (dica visual).
    const towerGrip = div('stow-grip');
    towerGrip.innerHTML = '‹';
    tower.appendChild(towerGrip);

    const logItems: Array<{ txt: string; el: HTMLElement }> = [];
    const pushLog = (msg: string, color?: string) => {
      const li = div('li');
      li.textContent = msg;
      if (color) li.style.color = color;
      logItems.unshift({ txt: msg, el: li });
      logLines.prepend(li);
      // Máx 4 visíveis; as antigas escurecem e a última some (fade).
      while (logItems.length > 4) logLines.removeChild(logItems.pop()!.el);
      logItems.forEach((it, i) => {
        it.el.classList.toggle('f', i >= 2);
        it.el.classList.toggle('ff', i >= 3);
      });
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
    // Todos os setTimeout da tela vão para 'timers' e são cancelados no
    // teardown — senão, sair durante a largada deixa beeps/vibração/flash
    // disparando no menu e closures retendo a árvore DOM da corrida.
    const timers: number[] = [];
    const later = (fn: () => void, ms: number): number => {
      const id = window.setTimeout(fn, ms);
      timers.push(id);
      return id;
    };
    race.onCountdown = v => {
      lights.classList.add('show');
      if (!lightsStarted && v > 0) {
        lightsStarted = true;
        const dots = slDots();
        for (let col = 0; col < 5; col++) {
          lightTimers.push(later(() => {
            dots[col * 2]?.classList.add('on');
            dots[col * 2 + 1]?.classList.add('on');
            Haptics.tap();
            Sound.lightOn();
          }, col * 560));
        }
      }
      if (v === 0) {
        for (const t of lightTimers) clearTimeout(t);
        slDots().forEach(d => d.classList.remove('on'));
        lights.classList.add('out');
        Haptics.heavy();
        Sound.go();
        // Clarão branco de largada.
        flash.classList.add('go');
        later(() => flash.classList.remove('go'), 400);
        later(() => { lights.style.display = 'none'; }, 320);
        showBanner('GO!', 'var(--cyan)');
      }
    };
    const playerNames = race.players.map(p => p.m.displayName);
    const involvesPlayer = (msg: string) => playerNames.some(n => msg.includes(n));
    let lastBanner = 0;
    const tryBanner = (txt: string, color: string, throttleMs = 2600) => {
      if (performance.now() - lastBanner < throttleMs) return;
      lastBanner = performance.now();
      showBanner(txt, color);
    };
    race.onRaceEvent = msg => {
      // Cor por tipo de evento (linguagem de transmissão).
      let color = 'var(--dim)';
      if (msg.includes('ultrapassou')) color = 'var(--delta-up)';
      else if (msg.includes('bateu forte')) color = 'var(--red)';
      else if (msg.includes('se tocaram')) color = 'var(--wear)';
      else if (msg.includes('pit')) color = 'var(--orange)';
      else if (msg.includes('Clima')) color = 'var(--energy)';
      else if (msg.includes('Safety')) color = 'var(--gold)';
      else if (msg.includes('GO')) color = 'var(--cyan)';
      pushLog(msg, color);

      // Banners curtos para os eventos importantes.
      if (msg.includes('bateu forte')) { tryBanner('BATIDA FORTE', 'var(--red)'); Sound.alert(); }
      else if (msg.includes('Safety Marble na')) { tryBanner('SAFETY MARBLE', 'var(--gold)'); Sound.alert(); }
      else if (msg.includes('Clima mudou')) tryBanner(msg.replace('🌦 ', '').toUpperCase(), 'var(--energy)');
      else if (msg.includes('Bandeirada')) tryBanner('BANDEIRADA!', 'var(--gold)');
      else if (msg.includes('entrou no pit') && involvesPlayer(msg)) tryBanner('PIT STOP', 'var(--orange)');
      else if (msg.includes('ultrapassou') && involvesPlayer(msg)) tryBanner('ULTRAPASSAGEM', 'var(--delta-up)', 4000);
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
      later(() => {
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
    // Flash one-shot de mudança de posição. Em vez de forçar um reflow por
    // linha alterada (layout thrashing a 15 Hz na largada), acumulamos e
    // fazemos UM único reflow por updateHud (ver flush abaixo).
    const pendingFlash: Array<[HTMLElement, 'up' | 'down']> = [];
    const flashRow = (elm: HTMLElement, cls: 'up' | 'down'): void => {
      elm.classList.remove('up', 'down');
      pendingFlash.push([elm, cls]);
    };

    let lastLapShown = false;
    function updateHud(): void {
      pendingFlash.length = 0;
      const leader = race.leader;
      const lap = Math.min(race.totalLaps, (leader?.m.completedLaps ?? 0) + 1);
      lapEl.textContent = `VOLTA ${lap}/${race.totalLaps}`;
      lapEl.classList.toggle('lastlap', lap >= race.totalLaps && race.totalLaps > 1);
      wxEl.textContent = `Clima: ${race.weatherLabelCurrent()}`;
      if (!lastLapShown && lap >= race.totalLaps && race.totalLaps > 1 && race.state === 'Racing') {
        lastLapShown = true;
        showBanner('ÚLTIMA VOLTA', 'var(--gold)');
      }

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
        // Volta mais rápida: marcador não-cromático (⚡) além da cor roxa.
        const isFl = actor === flActor;
        const gapTxt = i === 0 ? 'Líder' : `+${m.gapToLeader.toFixed(1)}`;
        r.gap.textContent = isFl && i !== 0 ? `⚡${gapTxt}` : gapTxt;
        r.gap.style.color = isFl ? 'var(--delta-best)' : 'var(--dim)';
        const inPit = m.state === 'InPit' || m.state === 'EnteringPit' || m.state === 'ExitingPit';
        r.ty.textContent = inPit ? 'P' : gripDisplayLetter(m.grip.gripId);
        r.ty.style.color = inPit ? 'var(--orange)' : gripDisplayColor(m.grip.gripId);
        r.ty.classList.toggle('pit', inPit);
      }
      // Um único reflow reinicia todas as animações de troca de posição.
      if (pendingFlash.length) {
        void rowsBox.offsetWidth;
        for (const [e, c] of pendingFlash) e.classList.add(c);
      }

      // Cards.
      const critBar = (bar: { fill: HTMLElement; val: HTMLElement }, pct: number, color: string, crit: boolean) => {
        bar.fill.style.width = `${pct}%`;
        bar.fill.style.background = color;
        bar.val.textContent = `${Math.round(pct)}%`;
        bar.fill.parentElement!.classList.toggle('crit', crit); // pulsa quando crítico
      };
      for (const c of cards) {
        const m = c.actor.m;
        c.pp.textContent = `P${m.position || '—'}`;
        // Badge de pneu (letra colorida) + badge de modo.
        c.tyreBadge.innerHTML = `<b style="color:${gripDisplayColor(m.grip.gripId)}">${gripDisplayLetter(m.grip.gripId)}</b> ${m.grip.compoundName.split(' ')[0]}`;
        c.modeBadge.textContent = m.mode;
        c.modeBadge.className = `badge mdb mode-${m.mode}`;
        const [wearB, enB, fuB] = c.bars;
        critBar(wearB!, m.wear, m.wear > 70 ? 'var(--red)' : 'var(--wear)', m.wear > 82);
        critBar(enB!, m.energy, m.energy < 20 ? 'var(--wear)' : 'var(--energy)', m.energy < 12);
        critBar(fuB!, m.fuel, m.fuel < 25 ? 'var(--red)' : 'var(--fuel)', m.fuel < 15);
        const inPit = m.state === 'InPit' || m.state === 'EnteringPit' || m.state === 'ExitingPit';
        c.pit.textContent = inPit ? 'IN PIT' : m.pitRequested ? 'PIT →' : 'PIT';
        c.pit.classList.toggle('on', m.pitRequested || inPit);
      }
    }

    function frame(now: number): void {
      if (!alive) return;
      const rawMs = now - last;
      let dt = rawMs / 1000;
      last = now;
      if (dt > 0.1) dt = 0.1;

      // FPS ao vivo (~2x/s) — só quando o medidor está ligado nas configs.
      if (showFps) {
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
      for (const t of timers) clearTimeout(t);   // luzes/flash/resultado
      window.removeEventListener('resize', onResize);
      window.removeEventListener('keydown', onKey);
    }
    return cleanup;
  });
}
