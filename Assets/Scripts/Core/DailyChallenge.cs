using System;
using System.Collections.Generic;
using MarbleGP.Data;
using MarbleGP.Race;

namespace MarbleGP.Core
{
    public enum DailyObjective { Win, Podium, Top5, Overtakes, FastestLap }

    /// <summary>Definição do desafio do dia (mesma para todos, derivada da data).</summary>
    public class DailyChallengeDef
    {
        public string dateKey;       // "yyyyMMdd"
        public int seed;
        public TrackDataSO track;
        public Weather weather;
        public int laps;
        public GripType startGrip;
        public RaceMode startMode;
        public DailyObjective objective;
        public int target;           // usado por Overtakes
        public string title;
        public string description;
    }

    /// <summary>
    /// Gera o Desafio Diário a partir da data atual: um cenário fixo (pista, clima,
    /// pneu, modo) + um objetivo. Como a corrida usa Random.InitState(seed), o
    /// desafio é reproduzível e igual para todos no mesmo dia. Sem servidor: o
    /// "placar" é o seu melhor resultado local + a sequência de dias.
    /// </summary>
    public static class DailyChallenge
    {
        public static string TodayKey() => DateTime.Now.ToString("yyyyMMdd");

        public static DailyChallengeDef Today(GameDatabase db)
        {
            string key = TodayKey();
            int seed = SeedFromKey(key);
            var rng = new System.Random(seed);

            var tracks = db.AvailableTracks();
            var track = tracks.Count > 0 ? tracks[rng.Next(tracks.Count)] : (db.tracks.Count > 0 ? db.tracks[0] : null);

            var weathers = new[] { Weather.Dry, Weather.Dry, Weather.Cloudy, Weather.Damp, Weather.LightRain };
            var weather = weathers[rng.Next(weathers.Length)];
            bool wet = weather == Weather.Damp || weather == Weather.LightRain;

            int laps = 4 + rng.Next(3); // 4..6
            var grip = wet ? GripType.Intermediate
                           : new[] { GripType.Soft, GripType.Medium, GripType.Hard }[rng.Next(3)];
            var mode = new[] { RaceMode.Normal, RaceMode.Push, RaceMode.Save }[rng.Next(3)];

            var objective = (DailyObjective)rng.Next(5);
            int target = 0;
            string desc;
            switch (objective)
            {
                case DailyObjective.Win: desc = "Vença a corrida."; break;
                case DailyObjective.Podium: desc = "Termine no pódio (top 3)."; break;
                case DailyObjective.Top5: desc = "Termine entre os 5 primeiros."; break;
                case DailyObjective.FastestLap: desc = "Marque a volta mais rápida da corrida."; break;
                default:
                    target = 3 + rng.Next(4); // 3..6
                    desc = $"Faça pelo menos {target} ultrapassagens.";
                    break;
            }

            return new DailyChallengeDef
            {
                dateKey = key,
                seed = seed,
                track = track,
                weather = weather,
                laps = laps,
                startGrip = grip,
                startMode = mode,
                objective = objective,
                target = target,
                title = "DESAFIO DO DIA",
                description = desc,
            };
        }

        /// <summary>Avalia o resultado do jogador contra o objetivo do dia.</summary>
        public static bool Evaluate(DailyChallengeDef def, RaceResult result,
            out int playerBestPos, out int playerOvertakes, out bool playerFastest)
        {
            playerBestPos = int.MaxValue;
            playerOvertakes = 0;
            playerFastest = false;
            if (result == null) { playerBestPos = 0; return false; }

            foreach (var e in result.entries)
            {
                if (!e.isPlayer) continue;
                if (e.position < playerBestPos) playerBestPos = e.position;
                playerOvertakes += e.overtakes;
                if (e.fastestLap) playerFastest = true;
            }
            if (playerBestPos == int.MaxValue) { playerBestPos = 0; return false; }

            switch (def.objective)
            {
                case DailyObjective.Win: return playerBestPos == 1;
                case DailyObjective.Podium: return playerBestPos <= 3;
                case DailyObjective.Top5: return playerBestPos <= 5;
                case DailyObjective.FastestLap: return playerFastest;
                case DailyObjective.Overtakes: return playerOvertakes >= def.target;
                default: return false;
            }
        }

        private static int SeedFromKey(string key)
        {
            // Hash estavel da data (nao usa GetHashCode, que varia entre execucoes).
            unchecked
            {
                int h = 17;
                foreach (char c in key) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }
    }
}
