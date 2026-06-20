using UnityEngine;

namespace MarbleGP.Race
{
    public enum RaceEventKind { None, SafetyMarble, DirtyTrack }

    /// <summary>
    /// Sorteia eventos de corrida ao longo da prova (PRD 20). Encapsula apenas a
    /// decisao/temporizacao; o RaceManager aplica os efeitos. No MVP cobre
    /// Safety Marble e Pista Suja, alem dos alertas de desgaste/energia/pit
    /// (esses ultimos vem dos sistemas de pneu/energia/pit).
    /// </summary>
    public class RaceEventSystem
    {
        private float _timer;

        public RaceEventSystem()
        {
            _timer = NextInterval();
        }

        public RaceEventKind Tick(float dt)
        {
            _timer -= dt;
            if (_timer > 0f) return RaceEventKind.None;
            _timer = NextInterval();

            float r = Random.value;
            if (r < 0.40f) return RaceEventKind.SafetyMarble;
            if (r < 0.80f) return RaceEventKind.DirtyTrack;
            return RaceEventKind.None;
        }

        private static float NextInterval() => Random.Range(18f, 32f);
    }
}
