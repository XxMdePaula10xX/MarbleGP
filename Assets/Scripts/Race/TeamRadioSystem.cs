using System.Collections.Generic;
using UnityEngine;
using MarbleGP.AI;
using MarbleGP.Core;

namespace MarbleGP.Race
{
    /// <summary>Tom visual da opção (a UI mapeia para cor).</summary>
    public enum RadioTone { Attack, Defend, Save, Hold }

    /// <summary>Uma opção de decisão do rádio do box.</summary>
    public class RadioOption
    {
        public string label;
        public RaceMode mode;
        public bool hold;          // mantém o modo atual (sem efeito)
        public RadioTone tone;
    }

    /// <summary>Uma decisão-relâmpago oferecida ao jogador durante a corrida.</summary>
    public class RadioDecision
    {
        public MarbleController ctrl;
        public string prompt;
        public readonly List<RadioOption> options = new List<RadioOption>();
        public float duration = 8f;   // tempo para decidir
    }

    /// <summary>
    /// Rádio do box (PRD extra): de tempos em tempos oferece ao jogador uma
    /// decisão tática rápida (atacar / defender / poupar) para uma das suas
    /// bolinhas, com base no estado da corrida. Converte "assistir" em "decidir".
    /// </summary>
    public class TeamRadioSystem
    {
        private float _cooldown = 28f;          // primeira chamada ~28s após o início
        private const float EffectDuration = 18f;

        public float EffectSeconds => EffectDuration;

        /// <summary>Avalia se é hora de uma decisão; devolve uma ou null.</summary>
        public RadioDecision Tick(float dt, IReadOnlyList<MarbleController> players, int totalLaps)
        {
            _cooldown -= dt;
            if (_cooldown > 0f || players == null || players.Count == 0) return null;

            // Escolhe uma bolinha do jogador correndo e longe do fim.
            MarbleController pick = null;
            for (int i = 0; i < players.Count; i++)
            {
                var m = players[i].Runtime;
                bool nearEnd = m.completedLaps >= totalLaps - 1;
                if (m.state == MarbleRaceState.Racing && !nearEnd) { pick = players[i]; break; }
            }
            if (pick == null) { _cooldown = 12f; return null; }

            _cooldown = Random.Range(45f, 75f);
            return Build(pick);
        }

        private static RadioDecision Build(MarbleController ctrl)
        {
            var m = ctrl.Runtime;
            var d = new RadioDecision { ctrl = ctrl };

            if (m.wear > 65f)
            {
                d.prompt = $"{m.DisplayName}: pneus castigados. Plano?";
                d.options.Add(Opt("FORÇAR", RaceMode.Attack, RadioTone.Attack));
                d.options.Add(Opt("POUPAR", RaceMode.Save, RadioTone.Save));
                d.options.Add(Hold());
            }
            else if (m.fuel < 45f)
            {
                d.prompt = $"{m.DisplayName}: combustível ficando curto.";
                d.options.Add(Opt("ECONOMIZAR", RaceMode.Save, RadioTone.Save));
                d.options.Add(Hold());
            }
            else if (m.position == 1)
            {
                d.prompt = $"{m.DisplayName} LIDERA! Como administrar?";
                d.options.Add(Opt("ABRIR", RaceMode.Push, RadioTone.Attack));
                d.options.Add(Opt("CONTROLAR", RaceMode.Defend, RadioTone.Defend));
                d.options.Add(Hold());
            }
            else
            {
                d.prompt = $"{m.DisplayName} em P{m.position}. Partir pra cima?";
                d.options.Add(Opt("ATACAR", RaceMode.Attack, RadioTone.Attack));
                d.options.Add(Opt("POUPAR", RaceMode.Save, RadioTone.Save));
                d.options.Add(Hold());
            }
            return d;
        }

        private static RadioOption Opt(string label, RaceMode mode, RadioTone tone)
            => new RadioOption { label = label, mode = mode, tone = tone };

        private static RadioOption Hold()
            => new RadioOption { label = "MANTER", hold = true, tone = RadioTone.Hold };
    }
}
