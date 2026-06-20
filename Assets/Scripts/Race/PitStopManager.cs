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
    /// Gerencia o fluxo de pit stop (PRD 18): entrada -> box -> servico
    /// (troca de anel + recarga) -> saida -> volta a pista. Para o MVP usa
    /// "Pit Normal" e os servicos de trocar aneis e recarregar energia.
    /// </summary>
    public class PitStopManager
    {
        private enum Phase { DrivingIn, Servicing, DrivingOut }

        private class PitJob
        {
            public MarbleController ctrl;
            public Phase phase;
            public Vector3 box;
            public Vector3 exitPoint;
        }

        private readonly TrackManager _track;
        private readonly GameBalance _bal;
        private readonly GameDatabase _db;
        private readonly TireWearSystem _tires;
        private readonly EnergySystem _energy;

        private readonly Dictionary<MarbleController, PitJob> _active = new();
        private int _boxCursor;

        /// <summary>Disparado quando um servico de pit termina (PRD 20 MVP).</summary>
        public event Action<MarbleRuntime> OnPitCompleted;

        public PitStopManager(TrackManager track, GameBalance bal, GameDatabase db,
            TireWearSystem tires, EnergySystem energy)
        {
            _track = track;
            _bal = bal;
            _db = db;
            _tires = tires;
            _energy = energy;
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
            m.pitRequested = false;
            m.state = MarbleRaceState.EnteringPit;
            m.pitStops++;

            Vector3 box = _track.PitBoxes.Count > 0
                ? _track.PitBoxes[_boxCursor++ % _track.PitBoxes.Count]
                : ctrl.transform.position;

            // Ponto de saida: retorno a linha ideal proximo ao inicio do setor.
            Vector3 exit = _track.IdealLine.Point(2);

            _active[ctrl] = new PitJob { ctrl = ctrl, phase = Phase.DrivingIn, box = box, exitPoint = exit };
            ctrl.UseExternalTarget = true;
            ctrl.Line = Core.RacingLine.Pit;
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
                        ctrl.ExternalTarget = job.box;
                        ctrl.DesiredSpeed = _bal.pitLaneSpeed;
                        if (Flat(ctrl.transform.position, job.box) < 1.2f)
                        {
                            StartService(m);
                            job.phase = Phase.Servicing;
                            ctrl.Freeze();
                        }
                        break;

                    case Phase.Servicing:
                        ctrl.DesiredSpeed = 0f;
                        ctrl.Freeze();
                        m.pitTimer -= dt;
                        if (m.pitTimer <= 0f)
                        {
                            OnPitCompleted?.Invoke(m);
                            job.phase = Phase.DrivingOut;
                            m.state = MarbleRaceState.ExitingPit;
                        }
                        break;

                    case Phase.DrivingOut:
                        ctrl.ExternalTarget = job.exitPoint;
                        ctrl.DesiredSpeed = _bal.pitLaneSpeed * 1.2f;
                        if (Flat(ctrl.transform.position, job.exitPoint) < 1.5f)
                        {
                            ctrl.UseExternalTarget = false;
                            m.state = MarbleRaceState.Racing;
                            done.Add(ctrl);
                        }
                        break;
                }
            }

            foreach (var c in done) _active.Remove(c);
        }

        private void StartService(MarbleRuntime m)
        {
            m.state = MarbleRaceState.InPit;

            // Servico 1: troca de anel (PRD 18.2).
            if (m.pitChangeTires)
            {
                var newGrip = _db.GetGrip(m.pitTargetGrip);
                if (newGrip != null) _tires.FitNewGrip(m, newGrip);
            }

            // Servico 2: recarga de energia (PRD 18.2).
            float refilled = _energy.Refill(m, m.pitRefillAmount);

            // Tempo total do pit (PRD 18.3 / 41).
            m.pitTimer = RaceFormulas.PitTime(m, _bal, m.pitChangeTires, refilled);
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
