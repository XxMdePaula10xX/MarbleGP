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

        public const int MaxUpgradeLevel = 5;
        private const int UpgradeBaseCost = 120;

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

            // Inicializa todos os upgrades em nivel 0 (PRD 30).
            Data.credits = 0;
            Data.upgrades.Clear();
            foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
                Data.upgrades.Add(new UpgradeState { type = type, level = 0 });

            Save();
        }

        public bool LoadSeason()
        {
            string json = SaveManager.LoadChampionship();
            if (string.IsNullOrEmpty(json)) return false;
            Data = JsonUtility.FromJson<ChampionshipData>(json);
            if (Data != null)
            {
                // Robustez contra saves antigos/corrompidos (evita NRE em IsSeasonOver/TotalRounds).
                if (Data.calendarTrackIds == null) Data.calendarTrackIds = new List<string>();
                if (Data.history == null) Data.history = new List<RoundResultSummary>();
                EnsureUpgrades();
                EnsureStandings();
            }
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
                maxMarbles: Mathf.Min(20, _db.drivers.Count));
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

            // Creditos da equipe do jogador (PRD 30): participacao + desempenho.
            int earned = 50;
            foreach (var e in result.entries.Where(x => x.isPlayer))
                earned += e.points * 10 + (e.position <= 3 ? 40 : 0);
            Data.credits += earned;

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

        // ---- Upgrades de equipe (PRD 30) --------------------------------

        public int Credits => Data != null ? Data.credits : 0;

        /// <summary>Garante que todos os tipos de upgrade existam na lista (saves antigos).</summary>
        private void EnsureUpgrades()
        {
            if (Data.upgrades == null) Data.upgrades = new System.Collections.Generic.List<UpgradeState>();
            foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
                if (!Data.upgrades.Any(x => x.type == type))
                    Data.upgrades.Add(new UpgradeState { type = type, level = 0 });
        }

        /// <summary>Garante que todos os pilotos/equipes do banco estejam na
        /// classificacao (saves antigos, criados com menos pilotos). PRD 29.2.</summary>
        private void EnsureStandings()
        {
            if (Data.driverStandings == null) Data.driverStandings = new List<DriverStanding>();
            if (Data.teamStandings == null) Data.teamStandings = new List<TeamStanding>();
            foreach (var d in _db.drivers)
                if (!Data.driverStandings.Any(x => x.driverId == d.driverId))
                    Data.driverStandings.Add(new DriverStanding { driverId = d.driverId, teamId = d.teamId });
            foreach (var t in _db.teams)
                if (!Data.teamStandings.Any(x => x.teamId == t.teamId))
                    Data.teamStandings.Add(new TeamStanding { teamId = t.teamId });
        }

        public int GetLevel(UpgradeType type)
        {
            var u = Data?.upgrades.FirstOrDefault(x => x.type == type);
            return u != null ? u.level : 0;
        }

        public int UpgradeCost(UpgradeType type) => UpgradeBaseCost * (GetLevel(type) + 1);

        public bool IsMaxed(UpgradeType type) => GetLevel(type) >= MaxUpgradeLevel;

        public bool CanUpgrade(UpgradeType type)
            => Data != null && !IsMaxed(type) && Data.credits >= UpgradeCost(type);

        public bool BuyUpgrade(UpgradeType type)
        {
            if (!CanUpgrade(type)) return false;
            Data.credits -= UpgradeCost(type);
            var u = Data.upgrades.First(x => x.type == type);
            u.level++;
            Save();
            return true;
        }

        /// <summary>Efeitos agregados aplicados as bolinhas do jogador (PRD 30).</summary>
        public TeamUpgradeEffects Effects()
        {
            var e = TeamUpgradeEffects.Neutral;
            if (Data == null) return e;
            foreach (var u in Data.upgrades)
            {
                switch (u.type)
                {
                    case UpgradeType.PitCrew: e.pitTimeReduction += 0.3f * u.level; break;
                    case UpgradeType.EnergyCoreLab: e.energyFactor -= 0.04f * u.level; break;
                    case UpgradeType.GripResearch: e.wearFactor -= 0.05f * u.level; break;
                    case UpgradeType.MarbleMaterial: e.speedFactor += 0.015f * u.level; break;
                    case UpgradeType.SurfaceLab: e.controlFactor += 0.02f * u.level; break;
                    case UpgradeType.StrategyCenter: e.errorFactor *= Mathf.Pow(0.94f, u.level); break;
                    case UpgradeType.AICoaching: e.errorFactor *= Mathf.Pow(0.92f, u.level); break;
                }
            }
            e.energyFactor = Mathf.Max(0.6f, e.energyFactor);
            e.wearFactor = Mathf.Max(0.6f, e.wearFactor);
            return e;
        }

        public static string UpgradeName(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.PitCrew: return "Pit Crew";
                case UpgradeType.EnergyCoreLab: return "Energy Core Lab";
                case UpgradeType.GripResearch: return "Grip Research";
                case UpgradeType.SurfaceLab: return "Surface Lab";
                case UpgradeType.StrategyCenter: return "Strategy Center";
                case UpgradeType.MarbleMaterial: return "Marble Material";
                case UpgradeType.AICoaching: return "AI Coaching";
                default: return type.ToString();
            }
        }

        public static string UpgradeDesc(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.PitCrew: return "Reduz o tempo de pit stop (-0.3s/nivel).";
                case UpgradeType.EnergyCoreLab: return "Reduz o consumo de energia (-4%/nivel).";
                case UpgradeType.GripResearch: return "Reduz o desgaste dos aneis (-5%/nivel).";
                case UpgradeType.SurfaceLab: return "Melhora o controle em curva (+2%/nivel).";
                case UpgradeType.StrategyCenter: return "Reduz erros sob pressao (-6%/nivel).";
                case UpgradeType.MarbleMaterial: return "Aumenta a velocidade (+1.5%/nivel).";
                case UpgradeType.AICoaching: return "Reduz erros das bolinhas (-8%/nivel).";
                default: return "";
            }
        }

        public string DriverName(string driverId)
            => _db.drivers.FirstOrDefault(d => d.driverId == driverId)?.marbleName ?? driverId;

        public string DriverCode(string driverId)
            => _db.drivers.FirstOrDefault(d => d.driverId == driverId)?.shortCode ?? "MAR";

        public string TeamName(string teamId)
            => _db.teams.FirstOrDefault(t => t.teamId == teamId)?.teamName ?? teamId;
    }
}
