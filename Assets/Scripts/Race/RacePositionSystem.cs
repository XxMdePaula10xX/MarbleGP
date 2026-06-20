using System.Collections.Generic;
using UnityEngine;
using MarbleGP.AI;
using MarbleGP.Core;
using MarbleGP.Systems;
using MarbleGP.Track;

namespace MarbleGP.Race
{
    /// <summary>
    /// Calcula posicao em tempo real e valida voltas por checkpoints (PRD 26 / 9.3).
    /// Ordenacao por progresso de arco ao longo da pista (nao por distancia ate a
    /// linha de chegada), evitando o bug classico de circuitos fechados.
    /// </summary>
    public class RacePositionSystem
    {
        private readonly TrackManager _track;
        private readonly float _checkpointRadius;
        private readonly float _refLapTime;

        // Estado de validacao por bolinha (PRD 9.3: ordem correta de checkpoints).
        private readonly Dictionary<MarbleRuntime, int> _nextCheckpoint = new();

        public RacePositionSystem(TrackManager track, float baseSpeed)
        {
            _track = track;
            _checkpointRadius = Mathf.Max(2.5f, track.Data.trackWidth * 0.8f);
            // Tempo de volta de referencia para estimar gaps (timing tower).
            _refLapTime = track.Data.trackLength / Mathf.Max(1f, baseSpeed);
        }

        public void Register(MarbleRuntime m) => _nextCheckpoint[m] = 1 % Mathf.Max(1, _track.CheckpointCount);

        /// <summary>
        /// Atualiza contagem de voltas via sequencia de checkpoints. Uma volta so
        /// conta ao cruzar todos os checkpoints na ordem e voltar ao checkpoint 0.
        /// </summary>
        /// <returns>true se a bolinha COMPLETOU uma volta neste passo.</returns>
        public bool UpdateLap(MarbleController ctrl, int totalLaps)
        {
            var m = ctrl.Runtime;
            if (_track.CheckpointCount == 0) return false;
            if (m.state == MarbleRaceState.Finished) return false;

            int next = _nextCheckpoint[m];
            Vector3 cpPos = _track.Checkpoints[next];
            Vector3 pos = ctrl.transform.position;
            pos.y = cpPos.y;

            if (Vector3.Distance(pos, cpPos) <= _checkpointRadius)
            {
                m.currentCheckpoint = next;
                _nextCheckpoint[m] = (next + 1) % _track.CheckpointCount;

                // Validar o checkpoint 0 (linha de largada) fecha uma volta (PRD 9.3).
                // Como o registro inicia apontando para o checkpoint 1, a sequencia
                // 1,2,...,N-1,0 corresponde exatamente a uma volta completa.
                if (next == 0 && m.completedLaps < totalLaps && m.totalTime > 1f)
                    return CompleteLap(m, totalLaps);
            }
            return false;
        }

        private bool CompleteLap(MarbleRuntime m, int totalLaps)
        {
            m.completedLaps++;
            if (m.currentLapTime < m.bestLapTime) m.bestLapTime = m.currentLapTime;
            m.currentLapTime = 0f;
            if (m.completedLaps >= totalLaps)
            {
                m.state = MarbleRaceState.Finished;
            }
            return true;
        }

        /// <summary>Recalcula raceProgress e ordena o campo, setando position (1-based).</summary>
        public void UpdatePositions(List<MarbleController> field)
        {
            foreach (var c in field)
            {
                var m = c.Runtime;
                float arc = _track.ArcFraction(c.transform.position); // 0..1 ao longo da pista
                // Progresso monotonico: voltas + fracao do arco (PRD 26).
                m.raceProgress = m.completedLaps + arc;
            }

            field.Sort((a, b) =>
            {
                // Quem terminou primeiro fica a frente; depois por progresso.
                var ma = a.Runtime; var mb = b.Runtime;
                bool fa = ma.state == MarbleRaceState.Finished;
                bool fb = mb.state == MarbleRaceState.Finished;
                if (fa && fb) return ma.totalTime.CompareTo(mb.totalTime);
                if (fa != fb) return fa ? -1 : 1;
                return mb.raceProgress.CompareTo(ma.raceProgress);
            });

            var leader = field.Count > 0 ? field[0].Runtime : null;
            for (int i = 0; i < field.Count; i++)
            {
                int newPos = i + 1;
                var m = field[i].Runtime;
                // Conta ultrapassagem quando ganha posicao em pista (PRD 8 estatisticas).
                if (m.position != 0 && newPos < m.position) m.overtakes++;
                m.position = newPos;

                // Gap para o lider em segundos (timing tower, PRD 23.5).
                if (leader == null || m == leader) m.gapToLeader = 0f;
                else if (m.state == MarbleRaceState.Finished && leader.state == MarbleRaceState.Finished)
                    m.gapToLeader = m.totalTime - leader.totalTime;
                else
                    m.gapToLeader = Mathf.Max(0f, (leader.raceProgress - m.raceProgress) * _refLapTime);
            }
        }
    }
}
