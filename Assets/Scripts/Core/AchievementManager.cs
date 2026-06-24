using System;
using System.Collections.Generic;
using UnityEngine;
using MarbleGP.Race;
using MarbleGP.Save;

namespace MarbleGP.Core
{
    /// <summary>Definição de uma conquista; o progresso é derivado dos contadores.</summary>
    public class Achievement
    {
        public readonly string id, category, title, desc;
        public readonly int target;
        private readonly Func<AchievementData, int> _value;

        public Achievement(string id, string category, string title, string desc,
            int target, Func<AchievementData, int> value)
        {
            this.id = id; this.category = category; this.title = title; this.desc = desc;
            this.target = target; _value = value;
        }

        public int Current(AchievementData d) => Mathf.Clamp(_value(d), 0, target);
        public bool IsUnlocked(AchievementData d) => _value(d) >= target;
        public float Progress(AchievementData d) => target <= 0 ? 1f : Mathf.Clamp01((float)_value(d) / target);
    }

    /// <summary>
    /// Conquistas (PRD 32). Acumula estatísticas a cada corrida/campeonato e
    /// deriva 32 conquistas de um catálogo. Persiste via SaveManager.
    /// </summary>
    public static class AchievementManager
    {
        private static AchievementData _data;
        public static AchievementData Data => _data ??= SaveManager.LoadAchievements();

        public static void Save() => SaveManager.SaveAchievements(Data);

        // ---- Catálogo (32) ----------------------------------------------
        private static List<Achievement> _catalog;
        public static IReadOnlyList<Achievement> Catalog => _catalog ??= Build();

        public static int UnlockedCount()
        {
            int n = 0;
            foreach (var a in Catalog) if (a.IsUnlocked(Data)) n++;
            return n;
        }

        private static List<Achievement> Build()
        {
            int B(bool b) => b ? 1 : 0;
            return new List<Achievement>
            {
                // Vitórias
                new Achievement("win_1","Vitórias","Primeira Vitória","Vença a sua primeira corrida.",1,d=>d.racesWon),
                new Achievement("win_3","Vitórias","Tri de Vitórias","Vença 3 corridas.",3,d=>d.racesWon),
                new Achievement("win_5","Vitórias","Pé no Acelerador","Vença 5 corridas.",5,d=>d.racesWon),
                new Achievement("win_10","Vitórias","Vencedor Nato","Vença 10 corridas.",10,d=>d.racesWon),
                new Achievement("win_25","Vitórias","Dominante","Vença 25 corridas.",25,d=>d.racesWon),
                new Achievement("win_50","Vitórias","Lenda das Pistas","Vença 50 corridas.",50,d=>d.racesWon),
                // Sequências
                new Achievement("streak_3","Sequências","Embalado","Vença 3 corridas seguidas.",3,d=>d.bestWinStreak),
                new Achievement("streak_5","Sequências","Imparável","Vença 5 corridas seguidas.",5,d=>d.bestWinStreak),
                // Pódios
                new Achievement("pod_1","Pódios","Ao Pódio","Termine entre os 3 primeiros.",1,d=>d.podiums),
                new Achievement("pod_10","Pódios","Frequentador do Pódio","Suba ao pódio 10 vezes.",10,d=>d.podiums),
                new Achievement("pod_25","Pódios","Habitué do Champanhe","Suba ao pódio 25 vezes.",25,d=>d.podiums),
                // Corridas disputadas
                new Achievement("play_1","Carreira","Estreia","Dispute a sua primeira corrida.",1,d=>d.racesPlayed),
                new Achievement("play_10","Carreira","Veterano","Dispute 10 corridas.",10,d=>d.racesPlayed),
                new Achievement("play_50","Carreira","Profissional","Dispute 50 corridas.",50,d=>d.racesPlayed),
                new Achievement("play_100","Carreira","Centurião","Dispute 100 corridas.",100,d=>d.racesPlayed),
                // Campeonatos
                new Achievement("champ_1","Campeonatos","Campeão","Vença um campeonato.",1,d=>d.championshipsWon),
                new Achievement("champ_3","Campeonatos","Dinastia","Vença 3 campeonatos.",3,d=>d.championshipsWon),
                // Ultrapassagens
                new Achievement("ot_10","Ultrapassagens","Atacante","Faça 10 ultrapassagens no total.",10,d=>d.totalOvertakes),
                new Achievement("ot_50","Ultrapassagens","Afiado","Faça 50 ultrapassagens no total.",50,d=>d.totalOvertakes),
                new Achievement("ot_100","Ultrapassagens","Ferocidade","Faça 100 ultrapassagens no total.",100,d=>d.totalOvertakes),
                new Achievement("ot_500","Ultrapassagens","Furacão","Faça 500 ultrapassagens no total.",500,d=>d.totalOvertakes),
                new Achievement("ot_race_5","Ultrapassagens","Show de Bola","Faça 5 ultrapassagens numa única corrida.",5,d=>d.maxOvertakesInRace),
                // Voltas mais rápidas
                new Achievement("fl_1","Voltas Rápidas","Volta Voadora","Marque a volta mais rápida de uma corrida.",1,d=>d.fastestLaps),
                new Achievement("fl_10","Voltas Rápidas","Cronômetro","Marque 10 voltas mais rápidas.",10,d=>d.fastestLaps),
                new Achievement("fl_25","Voltas Rápidas","Relâmpago","Marque 25 voltas mais rápidas.",25,d=>d.fastestLaps),
                // Pit stops
                new Achievement("pit_10","Estratégia","Box, Box!","Realize 10 pit stops no total.",10,d=>d.totalPitStops),
                new Achievement("pit_50","Estratégia","Mestre dos Boxes","Realize 50 pit stops no total.",50,d=>d.totalPitStops),
                // Circuitos
                new Achievement("trk_5","Circuitos","Viajante","Vença em 5 circuitos diferentes.",5,d=>d.tracksWon.Count),
                new Achievement("trk_all","Circuitos","Conquistador Global","Vença em todos os 15 circuitos.",15,d=>d.tracksWon.Count),
                // Pneus
                new Achievement("tyre_all","Pneus","Mestre dos Compostos","Vença usando os 5 tipos de pneu.",5,d=>d.tyresWon.Count),
                // Especiais
                new Achievement("fuel_spare","Especiais","Sobrou Tanque","Vença com mais de 40% de combustível.",1,d=>B(d.wonWithFuelToSpare)),
                new Achievement("both_pod","Especiais","Dobradinha","Coloque as suas 2 bolinhas no pódio na mesma corrida.",1,d=>B(d.wonBothPodium)),
            };
        }

        // ---- Registro de eventos ----------------------------------------

        /// <summary>Atualiza estatísticas ao fim de uma corrida; devolve conquistas novas.</summary>
        public static List<Achievement> RecordRace(RaceResult result)
        {
            if (result == null) return new List<Achievement>();
            var d = Data;
            d.racesPlayed++;

            int bestPos = int.MaxValue, raceOvertakes = 0, racePits = 0, podiumPlayers = 0;
            bool anyFastest = false;
            foreach (var e in result.entries)
            {
                if (!e.isPlayer) continue;
                if (e.position < bestPos) bestPos = e.position;
                raceOvertakes += e.overtakes;
                racePits += e.pitStops;
                if (e.fastestLap) anyFastest = true;
                if (e.position <= 3) podiumPlayers++;
            }

            if (bestPos == int.MaxValue) { Save(); return new List<Achievement>(); } // sem bolinhas do jogador

            d.totalOvertakes += raceOvertakes;
            d.totalPitStops += racePits;
            d.maxOvertakesInRace = Mathf.Max(d.maxOvertakesInRace, raceOvertakes);
            if (anyFastest) d.fastestLaps++;
            if (bestPos <= 3) d.podiums++;
            if (podiumPlayers >= 2) d.wonBothPodium = true;

            if (bestPos == 1)
            {
                d.racesWon++;
                d.currentWinStreak++;
                d.bestWinStreak = Mathf.Max(d.bestWinStreak, d.currentWinStreak);
                if (!string.IsNullOrEmpty(result.trackName) && !d.tracksWon.Contains(result.trackName))
                    d.tracksWon.Add(result.trackName);

                var win = result.entries.Find(e => e.isPlayer && e.position == 1);
                if (win != null)
                {
                    if (!string.IsNullOrEmpty(win.finalTyre) && !d.tyresWon.Contains(win.finalTyre))
                        d.tyresWon.Add(win.finalTyre);
                    if (win.finalFuel > 40f) d.wonWithFuelToSpare = true;
                }
            }
            else d.currentWinStreak = 0;

            var newly = DetectNew();
            Save();
            return newly;
        }

        /// <summary>Reseta o "claim" do campeonato ao iniciar uma nova temporada.</summary>
        public static void NoteSeasonStart()
        {
            Data.seasonClaimed = false;
            Save();
        }

        /// <summary>Contabiliza o título uma única vez quando o jogador é campeão.</summary>
        public static List<Achievement> NoteChampionResult(bool playerIsChampion)
        {
            if (!playerIsChampion || Data.seasonClaimed) return new List<Achievement>();
            Data.championshipsWon++;
            Data.seasonClaimed = true;
            var newly = DetectNew();
            Save();
            return newly;
        }

        /// <summary>Detecta conquistas recém-desbloqueadas e marca como vistas.</summary>
        private static List<Achievement> DetectNew()
        {
            var newly = new List<Achievement>();
            foreach (var a in Catalog)
            {
                if (a.IsUnlocked(Data) && !Data.unlocked.Contains(a.id))
                {
                    Data.unlocked.Add(a.id);
                    newly.Add(a);
                }
            }
            return newly;
        }
    }
}
