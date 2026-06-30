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
    /// Gerencia o fluxo de pit stop (PRD 18 / 3) seguindo o pit lane REAL
    /// (caminho finito tm.PitPath): entrada -> box -> servico -> saida -> pista.
    /// A bolinha segue os waypoints do pit (nao corta pela grama).
    /// </summary>
    public class PitStopManager
    {
        private enum Phase { DrivingIn, Servicing, DrivingOut }

        private class PitJob
        {
            public MarbleController ctrl;
            public Phase phase;
            public List<Vector3> path;  // null => servico no lugar (fallback)
            public int idx;             // waypoint alvo atual no path
            public int boxIdx;          // indice onde parar
            public Vector3 box;
        }

        private readonly TrackManager _track;
        private readonly GameBalance _bal;
        private readonly GameDatabase _db;
        private readonly TireWearSystem _tires;
        private readonly EnergySystem _energy;
        private readonly FuelSystem _fuel;

        private readonly Dictionary<MarbleController, PitJob> _active = new();
        private int _boxCursor;

        /// <summary>Disparado quando um servico de pit termina (PRD 20 MVP).</summary>
        public event Action<MarbleRuntime> OnPitCompleted;
        /// <summary>Disparado quando a bolinha volta a pista vinda do pit (resync de checkpoint).</summary>
        public event Action<MarbleController> OnPitExit;

        public PitStopManager(TrackManager track, GameBalance bal, GameDatabase db,
            TireWearSystem tires, EnergySystem energy, FuelSystem fuel)
        {
            _track = track;
            _bal = bal;
            _db = db;
            _tires = tires;
            _energy = energy;
            _fuel = fuel;
        }

        public bool IsPitting(MarbleController ctrl) => _active.ContainsKey(ctrl);

        /// <summary>
        /// Chamado pelo RaceManager quando a bolinha completa a volta e tinha pit
        /// solicitado (PRD 18.1: termina a volta atual e entra no pit).
        /// </summary>
        public void BeginEntry(MarbleController ctrl)
        {
            if (_active.ContainsKey(ctrl)) return;
            var m = ctrl.Runtime;
            // Bolinha que ja terminou a corrida nunca entra no pit (defesa extra).
            if (m.state == MarbleRaceState.Finished) { m.pitRequested = false; return; }
            m.pitRequested = false;
            m.state = MarbleRaceState.EnteringPit;
            m.pitStops++;

            var path = _track.PitPath;
            ctrl.UseExternalTarget = true;

            if (path == null || path.Count < 3)
            {
                // Fallback: servico no lugar (pista sem pit lane definido).
                StartService(m);
                _active[ctrl] = new PitJob { ctrl = ctrl, phase = Phase.Servicing, path = null };
                ctrl.Freeze();
                return;
            }

            int slot = _track.PitBoxes.Count > 0 ? (_boxCursor++ % _track.PitBoxes.Count) : 0;
            int boxIdx = (_track.PitBoxPathIndex != null && slot < _track.PitBoxPathIndex.Count)
                ? _track.PitBoxPathIndex[slot] : path.Count / 2;
            Vector3 box = (slot < _track.PitBoxes.Count) ? _track.PitBoxes[slot] : path[boxIdx];

            _active[ctrl] = new PitJob
            {
                ctrl = ctrl, phase = Phase.DrivingIn, path = path, idx = 0, boxIdx = boxIdx, box = box
            };
        }

        /// <summary>Processa todas as paradas em andamento. Chamado no FixedUpdate.</summary>
        public void Tick(float dt)
        {
            if (_active.Count == 0) return;
            var done = new List<MarbleController>();

            foreach (var kv in _active)
            {
                var job = kv.Value;
                var ctrl = job.ctrl;
                var m = ctrl.Runtime;

                switch (job.phase)
                {
                    case Phase.DrivingIn:
                        DriveIn(job, ctrl, m);
                        break;

                    case Phase.Servicing:
                        ctrl.DesiredSpeed = 0f;
                        ctrl.Freeze();
                        m.pitTimer -= dt;
                        if (m.pitTimer <= 0f)
                        {
                            OnPitCompleted?.Invoke(m);
                            m.state = MarbleRaceState.ExitingPit;
                            if (job.path == null)
                            {
                                ctrl.UseExternalTarget = false;
                                m.state = MarbleRaceState.Racing;
                                done.Add(ctrl);
                                OnPitExit?.Invoke(ctrl);
                            }
                            else
                            {
                                job.phase = Phase.DrivingOut;
                                job.idx = job.boxIdx; // continua do box ate o fim do pit lane
                            }
                        }
                        break;

                    case Phase.DrivingOut:
                        if (DriveOut(job, ctrl, m)) { done.Add(ctrl); OnPitExit?.Invoke(ctrl); }
                        break;
                }
            }

            foreach (var c in done) _active.Remove(c);
        }

        private void DriveIn(PitJob job, MarbleController ctrl, MarbleRuntime m)
        {
            int t = Mathf.Min(job.idx, job.boxIdx);
            ctrl.ExternalTarget = job.path[t];
            ctrl.DesiredSpeed = _bal.pitLaneSpeed;

            if (job.idx < job.boxIdx)
            {
                if (Flat(ctrl.transform.position, job.path[job.idx]) < 1.4f) job.idx++;
            }
            else
            {
                // Chegou na regiao do box: mira o ponto exato e para.
                ctrl.ExternalTarget = job.box;
                ctrl.DesiredSpeed = _bal.pitLaneSpeed * 0.7f;
                if (Flat(ctrl.transform.position, job.box) < 1.0f)
                {
                    StartService(m);
                    job.phase = Phase.Servicing;
                    ctrl.Freeze();
                }
            }
        }

        private bool DriveOut(PitJob job, MarbleController ctrl, MarbleRuntime m)
        {
            if (job.idx < job.path.Count)
            {
                ctrl.ExternalTarget = job.path[job.idx];
                ctrl.DesiredSpeed = _bal.pitLaneSpeed * 1.15f;
                if (Flat(ctrl.transform.position, job.path[job.idx]) < 1.4f) job.idx++;
            }

            if (job.idx >= job.path.Count)
            {
                ctrl.UseExternalTarget = false;
                m.state = MarbleRaceState.Racing;
                return true;
            }
            return false;
        }

        private void StartService(MarbleRuntime m)
        {
            m.state = MarbleRaceState.InPit;

            // Servico 1: troca de anel (reseta desgaste, PRD 18.2 / 8).
            if (m.pitChangeTires)
            {
                var newGrip = _db.GetGrip(m.pitTargetGrip);
                if (newGrip != null) _tires.FitNewGrip(m, newGrip);
            }

            // Servico 2: reabastece combustivel e restaura energia para 100 (PRD 8).
            float energyRefilled = 100f - m.energy;
            _energy.Refill(m, 100f);
            _fuel.Refill(m);

            // Tempo total do pit (PRD 18.3 / 41).
            m.pitTimer = RaceFormulas.PitTime(m, _bal, m.pitChangeTires, energyRefilled);
            m.pitTotalTime = Mathf.Max(0.1f, m.pitTimer);
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
