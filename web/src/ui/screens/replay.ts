// Tela: Replay — port de UI/RaceReplayPlayer.cs
// Reproduz os keyframes gravados com câmera cinemática, play/pause,
// velocidade e chips de destaques.

import { RaceRecorder } from '../../sim/recorder';
import { RaceRenderer } from '../../render/renderer';
import { btn, div, el, mount } from '../dom';
import { show } from '../router';

const SPEEDS = [0.5, 1, 2, 4];

export function replayScreen(recorder: RaceRecorder, onClose: () => void): void {
  show('replay', root => {
    const race = recorder.race;
    const stage = div('stage');
    stage.style.display = 'block';
    root.appendChild(stage);
    const canvas = el('canvas');
    stage.appendChild(canvas);

    const renderer = new RaceRenderer(canvas, race.track);
    renderer.cam.follow = false;

    // Controles.
    const ctl = div('panel replay-ctl');
    const playB = el('button', 'iconbtn');
    playB.textContent = '⏸';
    let playing = true;
    playB.addEventListener('click', () => {
      playing = !playing;
      playB.textContent = playing ? '⏸' : '▶';
    });

    let speedIdx = 1;
    const speedB = el('button', 'iconbtn');
    speedB.textContent = '1×';
    speedB.addEventListener('click', () => {
      speedIdx = (speedIdx + 1) % SPEEDS.length;
      speedB.textContent = `${SPEEDS[speedIdx]}×`;
    });

    const seek = el('input', '', { type: 'range', min: 0, max: 1000, value: 0 });
    seek.style.width = '160px';

    const closeB = btn('Fechar', 'ghost', onClose);
    closeB.style.padding = '6px 14px';
    mount(ctl, playB, speedB, seek, closeB);
    root.appendChild(ctl);

    // Chips de destaques.
    let focusIndex = -1; // -1 = líder/geral
    if (recorder.highlights.length > 0) {
      const chips = div('');
      chips.style.cssText =
        `position:absolute;top:calc(var(--safe-t) + 12px);left:50%;transform:translateX(-50%);` +
        `display:flex;gap:6px;flex-wrap:wrap;justify-content:center;z-index:5;max-width:86%`;
      for (const h of recorder.highlights.slice(0, 10)) {
        const chip = el('button', 'replay-chip');
        const icon = h.kind === 'Overtake' ? '🔼' : h.kind === 'Crash' ? '💥' : h.kind === 'Finish' ? '🏁' : '🚨';
        chip.textContent = `${icon} ${Math.floor(h.time / 60)}:${String(Math.floor(h.time % 60)).padStart(2, '0')}`;
        chip.title = h.label;
        chip.addEventListener('click', () => {
          t = Math.max(0, h.time - 3);
          focusIndex = h.focusIndex;
          playing = true;
          playB.textContent = '⏸';
          renderer.cam.follow = true;
          renderer.cam.tz = Math.max(renderer.cam.tz, 2.4);
        });
        chips.appendChild(chip);
      }
      root.appendChild(chips);
    }

    // Playback.
    let t = 0;
    let alive = true;
    let last = performance.now();
    const interval = RaceRecorder.INTERVAL;
    const marbles = recorder.marbles;

    function applyFrame(time: number): void {
      const frames = recorder.frames;
      if (frames.length < 2) return;
      const f = Math.min(frames.length - 1.001, Math.max(0, time / interval));
      const i0 = Math.floor(f);
      const alpha = f - i0;
      const a = frames[i0]!, b = frames[Math.min(frames.length - 1, i0 + 1)]!;
      for (let i = 0; i < marbles.length; i++) {
        const m = marbles[i]!.m;
        const pa = a.pos[i]!, pb = b.pos[i]!;
        const nx = pa.x + (pb.x - pa.x) * alpha;
        const ny = pa.y + (pb.y - pa.y) * alpha;
        m.x = nx; m.y = ny;
        m.trail.push({ x: nx, y: ny });
        if (m.trail.length > 9) m.trail.shift();
      }
    }

    seek.addEventListener('input', () => {
      t = (Number(seek.value) / 1000) * recorder.duration;
    });

    function frame(now: number): void {
      if (!alive) return;
      const dt = Math.min(0.1, (now - last) / 1000);
      last = now;

      if (playing) {
        t += dt * SPEEDS[speedIdx]!;
        if (t >= recorder.duration) { t = recorder.duration; playing = false; playB.textContent = '▶'; }
        seek.value = String(Math.round((t / Math.max(0.01, recorder.duration)) * 1000));
      }
      applyFrame(t);

      const focus = focusIndex >= 0 && focusIndex < marbles.length ? marbles[focusIndex]! : null;
      renderer.updateCamera(dt, focus ? { x: focus.m.x, y: focus.m.y } : null);
      renderer.render(marbles, 'Dry', now);

      requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);

    const onResize = () => renderer.resize();
    window.addEventListener('resize', onResize);
    return () => {
      alive = false;
      window.removeEventListener('resize', onResize);
    };
  });
}
