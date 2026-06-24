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
            _checkpointRadius = Mathf.Max(2.2f, track.Data.trackWidth * 0.5f);
            // Tempo de volta de referencia para estimar gaps (timing tower).
            _refLapTime = track.Data.trackLength / Mathf.Max(1f, baseSpeed);
        }

        public void Register(MarbleRuntime m) => _nextCheckpoint[m] = 1 % Mathf.Max(1, _track.CheckpointCount);

        /// <summary>
        /// Resincroniza o proximo checkpoint apos um pit (a bolinha pulou alguns
        /// checkpoints no pit lane). Ajusta para o checkpoint logo a frente da
        /// posicao atual, evitando perder uma volta inteira.
        /// </summary>
        public void ResyncCheckpoint(MarbleController ctrl)
        {
            if (_track.CheckpointCount == 0) return;
            int n = _track.CheckpointCount;
            float arc = _track.ArcFraction(ctrl.transform.position);
            int idx = Mathf.RoundToInt(arc * n) % n;       // checkpoint logo a frente
            _nextCheckpoint[ctrl.Runtime] = idx;
            // Mantem currentCheckpoint adjacente ao proximo (progresso coerente
            // apos o pit, que usa cp + fracao ate o proximo).
            ctrl.Runtime.currentCheckpoint = (idx - 1 + n) % n;
        }

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

            if (!_nextCheckpoint.TryGetValue(m, out int next)) return false;
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
            if (_track.CheckpointCount == 0) return; // sem checkpoints, nada a ordenar
            int n = _track.CheckpointCount;
            foreach (var c in field)
            {
                var m = c.Runtime;

                // Progresso baseado em CHECKPOINTS (robusto). Os checkpoints sao
                // validados em ordem estrita por proximidade, entao isto e imune
                // ao salto de projecao em pistas que passam perto de si mesmas e
                // ao wrap da linha de largada. Ordem grosseira = nº de checkpoints
                // ja passados; ordem fina = quao perto esta do proximo checkpoint.
                int cp = Mathf.Clamp(m.currentCheckpoint, 0, n - 1);
                int next = _nextCheckpoint.TryGetValue(m, out var nx) ? Mathf.Clamp(nx, 0, n - 1) : (cp + 1) % n;

                Vector3 nextPos = _track.Checkpoints[next];
                Vector3 cpPos = _track.Checkpoints[cp];
                Vector3 pos = c.transform.position; pos.y = nextPos.y; cpPos.y = nextPos.y;

                float segLen = Vector3.Distance(cpPos, nextPos);
                float distToNext = Vector3.Distance(pos, nextPos);
                // Pode ser <0 (atras do checkpoint atual, ex.: grid) — mantem a
                // ordem do grid; e continuo ao cruzar a linha (sem hack).
                float frac = segLen > 0.01f ? 1f - distToNext / segLen : 0f;

                m.raceProgress = m.completedLaps + (cp + frac) / n;
            }

            field.Sort((a, b) =>
            {
                // Quem terminou primeiro fica a frente; depois por progresso.
                var ma = a.Runtime; var mb = b.Runtime;
                bool fa = ma.state == MarbleRaceState.Finished;
                bool fb = mb.state == MarbleRaceState.Finished;
                if (fa && fb)
                {
                    // Mais voltas na frente (retardatarios que tomaram a bandeira
                    // ficam atras); empate de voltas decide por tempo total.
                    if (ma.completedLaps != mb.completedLaps)
                        return mb.completedLaps.CompareTo(ma.completedLaps);
                    return ma.totalTime.CompareTo(mb.totalTime);
                }
                if (fa != fb) return fa ? -1 : 1;
                return mb.raceProgress.CompareTo(ma.raceProgress);
            });

            // Estabilizacao anti-flicker (PRD 1): bolinhas lado a lado (em linhas
            // de corrida diferentes) tem arco quase igual e a ordem "piscava",
            // gerando ultrapassagens falsas. Mantemos a ordem anterior enquanto a
            // diferenca de progresso for menor que a histerese; uma bolinha so
            // toma a posicao quando esta CLARAMENTE a frente.
            const float hysteresis = 0.0035f;
            for (int pass = 0; pass < field.Count; pass++)
            {
                bool swapped = false;
                for (int i = 0; i + 1 < field.Count; i++)
                {
                    var ma = field[i].Runtime;
                    var mb = field[i + 1].Runtime;
                    if (ma.state == MarbleRaceState.Finished || mb.state == MarbleRaceState.Finished) continue;
                    // Empate tecnico + ordem anterior invertida -> restaura anterior.
                    if (mb.position != 0 && ma.position > mb.position
                        && Mathf.Abs(ma.raceProgress - mb.raceProgress) < hysteresis)
                    {
                        (field[i], field[i + 1]) = (field[i + 1], field[i]);
                        swapped = true;
                    }
                }
                if (!swapped) break;
            }

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
