using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MarbleGP.Core;

namespace MarbleGP.Data
{
    /// <summary>
    /// Catalogo central de todos os dados do jogo (PRD 39.4: nao hardcodar
    /// equipes/pistas/bolinhas nos managers). Um asset default e gerado pelo
    /// DataGenerator do Editor. Acessivel via GameManager.Database.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "MarbleGP/Game Database", order = 1)]
    public class GameDatabase : ScriptableObject
    {
        public GameBalance balance;

        public List<TeamDataSO> teams = new List<TeamDataSO>();
        public List<MarbleDriverSO> drivers = new List<MarbleDriverSO>();
        public List<TrackDataSO> tracks = new List<TrackDataSO>();
        public List<GripRingSO> gripRings = new List<GripRingSO>();
        public List<SurfaceProfileSO> surfaces = new List<SurfaceProfileSO>();

        // ---- Lookups ----------------------------------------------------

        public TeamDataSO GetTeam(string teamId) => teams.FirstOrDefault(t => t.teamId == teamId);
        public TrackDataSO GetTrack(string trackId) => tracks.FirstOrDefault(t => t.trackId == trackId);
        public GripRingSO GetGrip(GripType type) => gripRings.FirstOrDefault(g => g.gripId == type);
        public SurfaceProfileSO GetSurface(SurfaceType type) => surfaces.FirstOrDefault(s => s.surfaceId == type);

        /// <summary>Bolinhas de uma equipe (espera-se 2, PRD 11).</summary>
        public List<MarbleDriverSO> GetTeamDrivers(string teamId)
            => drivers.Where(d => d.teamId == teamId).ToList();

        /// <summary>Pistas disponiveis (nao bloqueadas) para corrida rapida.</summary>
        public List<TrackDataSO> AvailableTracks() => tracks.Where(t => !t.trackLocked).ToList();
    }
}
