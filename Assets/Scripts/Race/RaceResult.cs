using System.Collections.Generic;

namespace MarbleGP.Race
{
    /// <summary>Resultado de uma bolinha ao fim da corrida (PRD 23.6).</summary>
    [System.Serializable]
    public class RaceResultEntry
    {
        public int position;
        public string marbleName;
        public string teamName;
        public string teamId;
        public string driverId;
        public float totalTime;
        public int pitStops;
        public float bestLapTime;
        public int overtakes;
        public float finalWear;
        public float finalEnergy;
        public string finalTyre = "M";
        public int points;
        public bool isPlayer;
        public bool fastestLap;
    }

    /// <summary>Resultado completo da corrida, consumido pela tela de Results.</summary>
    [System.Serializable]
    public class RaceResult
    {
        public string trackName;
        public int laps;
        public List<RaceResultEntry> entries = new List<RaceResultEntry>();
    }
}
