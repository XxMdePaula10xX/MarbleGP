using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MarbleGP.Data;
using MarbleGP.Race;
using MarbleGP.Save;

namespace MarbleGP.Core
{
    /// <summary>
    /// Gerencia o campeonato (PRD 24.2 / 29): calendario, pontos, classificacao
    /// de pilotos e equipes, historico e progressao entre etapas. Persistido em
    /// JSON via SaveManager. Independente de cena/UI.
    /// </summary>
    public class ChampionshipManager
    {
        private readonly GameDatabase _db;
        public ChampionshipData Data { get; private set; }

        public ChampionshipManager(GameDatabase db)
        {
            _db = db;
        }

        public bool HasActiveSeason => Data != null && Data.active;
        public bool IsSeasonOver => Data != null && Data.currentRound >= Data.calendarTrackIds.Count;
        public int CurrentRound => Data != null ? Data.currentRound : 0;
        public int TotalRounds => Data != null ? Data.calendarTrackIds.Count : 0;

        // ---- Ciclo de vida ----------------------------------------------

        /// <summary>Inicia uma nova temporada com as pistas disponiveis (PRD 29.1).</summary>
        public void StartNewSeason(string playerTeamId)
        {
            Data = new ChampionshipData { playerTeamId = playerTeamId, active = true, currentRound = 0 };

            // Calendario = pistas nao bloqueadas, na ordem do banco (PRD 21.2: 3 no MVP).
            Data.calendarTrackIds = _db.AvailableTracks().Select(t => t.trackId).ToList();

            foreach (var d in _db.drivers)
                Data.driverStandings.Add(new DriverStanding { driverId = d.driverId, teamId = d.teamId });
            foreach (var t in _db.teams)
                Data.teamStandings.Add(new TeamStanding { teamId = t.teamId });

            Save();
        }

        public bool LoadSeason()
        {
            string json = SaveManager.LoadChampionship();
            if (string.IsNullOrEmpty(json)) return false;
            Data = JsonUtility.FromJson<ChampionshipData>(json);
            return Data != null && Data.active;
        }

        public void Save()
        {
            if (Data != null) SaveManager.SaveChampionship(JsonUtility.ToJson(Data, true));
        }

        public TrackDataSO CurrentTrack()
        {
            if (IsSeasonOver) return null;
            return _db.GetTrack(Data.calendarTrackIds[Data.currentRound]);
        }

        // ---- Construcao da etapa ----------------------------------------

        /// <summary>Monta a RaceConfig da etapa atual com o grid completo (PRD 29).</summary>
        public RaceConfig BuildRoundRace()
        {
            var track = CurrentTrack();
            if (track == null) return null;

            // MVP: usa todas as bolinhas disponiveis (8). Arquitetura suporta 24.
            return QuickRaceBuilder.Build(_db, track, Data.playerTeamId,
                maxMarbles: _db.drivers.Count);
        }

        // ---- Aplicacao de resultado (PRD 10 / 29.2) ---------------------

        public void ApplyResult(RaceResult result)
        {
            if (Data == null || result == null) return;

            foreach (var e in result.entries)
            {
                var ds = Data.driverStandings.FirstOrDefault(x => x.driverId == e.driverId);
                if (ds != null)
                {
                    ds.points += e.points;
                    if (e.position == 1) ds.wins++;
                    if (e.position <= 3) ds.podiums++;
                    if (e.fastestLap) ds.fastestLaps++;
                }

                var ts = Data.teamStandings.FirstOrDefault(x => x.teamId == e.teamId);
                if (ts != null)
                {
                    ts.points += e.points;       // soma das 2 bolinhas (PRD 29.2)
                    if (e.position == 1) ts.wins++;
                    if (e.position <= 3) ts.podiums++;
                }
            }

            var winner = result.entries.FirstOrDefault(x => x.position == 1);
            Data.history.Add(new RoundResultSummary
            {
                trackId = CurrentTrack()?.trackId,
                winnerDriverId = winner?.driverId,
                winnerTeamId = winner?.teamId
            });

            Data.currentRound++;
            if (Data.currentRound >= Data.calendarTrackIds.Count) Data.active = true; // mantem para exibir final
            Save();
        }

        // ---- Classificacoes ordenadas (PRD 29.2) ------------------------

        public List<DriverStanding> DriverStandingsSorted()
            => Data.driverStandings.OrderByDescending(d => d.points)
                                   .ThenByDescending(d => d.wins).ToList();

        public List<TeamStanding> TeamStandingsSorted()
            => Data.teamStandings.OrderByDescending(t => t.points)
                                 .ThenByDescending(t => t.wins).ToList();

        public string DriverName(string driverId)
            => _db.drivers.FirstOrDefault(d => d.driverId == driverId)?.marbleName ?? driverId;

        public string DriverCode(string driverId)
            => _db.drivers.FirstOrDefault(d => d.driverId == driverId)?.shortCode ?? "MAR";

        public string TeamName(string teamId)
            => _db.teams.FirstOrDefault(t => t.teamId == teamId)?.teamName ?? teamId;
    }
}
