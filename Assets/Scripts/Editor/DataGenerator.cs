#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MarbleGP.Core;
using MarbleGP.Data;

namespace MarbleGP.EditorTools
{
    /// <summary>
    /// Gera automaticamente todos os ScriptableObjects do MVP (PRD 42),
    /// o GameBalance (PRD 28) e o GameDatabase (em Resources), para que o
    /// jogo rode sem criacao manual de assets. Menu: Tools > Marble GP.
    /// </summary>
    public static class DataGenerator
    {
        private const string Root = "Assets/ScriptableObjects";
        private const string ResRoot = "Assets/Resources";

        [MenuItem("Tools/Marble GP/Gerar Dados do MVP", priority = 0)]
        public static void Generate()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder(Root, "Teams");
            EnsureFolder(Root, "Drivers");
            EnsureFolder(Root, "Tracks");
            EnsureFolder(Root, "GripRings");
            EnsureFolder(Root, "Surfaces");

            var balance = CreateOrLoad<GameBalance>($"{ResRoot}/GameBalance.asset");

            var grips = CreateGrips();
            var surfaces = CreateSurfaces();
            var teams = CreateTeams();
            var drivers = CreateDrivers(teams);
            var tracks = CreateTracks();

            var db = CreateOrLoad<GameDatabase>($"{ResRoot}/GameDatabase.asset");
            db.balance = balance;
            db.gripRings = grips;
            db.surfaces = surfaces;
            db.teams = teams;
            db.drivers = drivers;
            db.tracks = tracks;
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Marble GP] Dados do MVP gerados em Assets/Resources/GameDatabase.asset");
            Selection.activeObject = db;
        }

        // ---- Grips (PRD 14 / 28) ----------------------------------------

        private static List<GripRingSO> CreateGrips()
        {
            var list = new List<GripRingSO>();
            list.Add(MakeGrip(GripType.Soft, "Soft Grip", 1.08f, 1.10f, 18f, 0.6f, 1.0f,
                "Muito rapido, desgasta rapido. Ideal para classificacao ou final de corrida."));
            list.Add(MakeGrip(GripType.Medium, "Medium Grip", 1.00f, 1.00f, 11f, 0.7f, 1.0f,
                "Equilibrado entre velocidade e durabilidade."));
            list.Add(MakeGrip(GripType.Hard, "Hard Grip", 0.94f, 0.95f, 7f, 0.7f, 1.0f,
                "Mais lento, mas dura muito mais."));
            return list;
        }

        private static GripRingSO MakeGrip(GripType id, string name, float spd, float grip,
            float wear, float wet, float dry, string desc)
        {
            var so = CreateOrLoad<GripRingSO>($"{Root}/GripRings/Grip_{id}.asset");
            so.gripId = id; so.compoundName = name;
            so.speedMultiplier = spd; so.gripMultiplier = grip; so.wearRate = wear;
            so.wetPerformance = wet; so.dryPerformance = dry; so.description = desc;
            EditorUtility.SetDirty(so);
            return so;
        }

        // ---- Surfaces (PRD 16) ------------------------------------------

        private static List<SurfaceProfileSO> CreateSurfaces()
        {
            var list = new List<SurfaceProfileSO>();
            list.Add(MakeSurface(SurfaceType.Polished, "Polished", 1.05f, 0.92f, 1.10f, 1.0f, 0.9f,
                "Mais rapida em retas, menos controle em curvas, desgaste maior."));
            list.Add(MakeSurface(SurfaceType.MicroGrooved, "Micro-Grooved", 1.00f, 1.00f, 1.00f, 1.0f, 1.0f,
                "Equilibrada, boa para a maioria dos circuitos."));
            list.Add(MakeSurface(SurfaceType.Textured, "Textured", 0.95f, 1.08f, 0.95f, 1.0f, 1.1f,
                "Melhor aderencia, mais lenta em retas."));
            return list;
        }

        private static SurfaceProfileSO MakeSurface(SurfaceType id, string name, float spd, float ctrl,
            float wear, float energy, float wet, string desc)
        {
            var so = CreateOrLoad<SurfaceProfileSO>($"{Root}/Surfaces/Surface_{id}.asset");
            so.surfaceId = id; so.surfaceName = name;
            so.speedModifier = spd; so.controlModifier = ctrl; so.wearModifier = wear;
            so.energyModifier = energy; so.wetModifier = wet; so.description = desc;
            EditorUtility.SetDirty(so);
            return so;
        }

        // ---- Teams (PRD 42) ---------------------------------------------

        private static List<TeamDataSO> CreateTeams()
        {
            var list = new List<TeamDataSO>();
            list.Add(MakeTeam("red_comet", "Red Comet Racing", "#E62020", "#1A1A1A",
                TeamStyle.Aggressive, TeamBonusType.TopSpeed, 55, 50,
                "Agressiva e rapida. Forte em retas, desgaste alto."));
            list.Add(MakeTeam("blue_orbit", "Blue Orbit GP", "#1E5FE0", "#FFFFFF",
                TeamStyle.Balanced, TeamBonusType.Consistency, 60, 55,
                "Equilibrada e consistente."));
            list.Add(MakeTeam("emerald_rollers", "Emerald Rollers", "#1Fa84a", "#D4AF37",
                TeamStyle.Technical, TeamBonusType.Cornering, 55, 58,
                "Tecnica, forte em curvas."));
            list.Add(MakeTeam("shadow_marble", "Shadow Marble Team", "#202020", "#7A3FB0",
                TeamStyle.Strategic, TeamBonusType.PitStops, 70, 52,
                "Estrategica, excelente nos pit stops."));
            return list;
        }

        private static TeamDataSO MakeTeam(string id, string name, string primary, string secondary,
            TeamStyle style, TeamBonusType bonus, int pit, int dev, string desc)
        {
            var so = CreateOrLoad<TeamDataSO>($"{Root}/Teams/Team_{id}.asset");
            so.teamId = id; so.teamName = name;
            ColorUtility.TryParseHtmlString(primary, out var pc); so.primaryColor = pc;
            ColorUtility.TryParseHtmlString(secondary, out var sc); so.secondaryColor = sc;
            so.teamStyle = style; so.baseBonusType = bonus;
            so.pitCrewRating = pit; so.developmentRating = dev; so.description = desc;
            EditorUtility.SetDirty(so);
            return so;
        }

        // ---- Drivers (PRD 12 / 42) --------------------------------------

        private static List<MarbleDriverSO> CreateDrivers(List<TeamDataSO> teams)
        {
            var list = new List<MarbleDriverSO>();
            // Red Comet: velocidade alta, controle/tire menor.
            list.Add(MakeDriver("comet_one", "Comet One", "CM1", 1, "red_comet",
                88, 82, 60, 78, 60, 62, 45, 55, 55, 50, Personality.Aggressive));
            list.Add(MakeDriver("comet_two", "Comet Two", "CM2", 2, "red_comet",
                84, 80, 58, 70, 58, 60, 48, 55, 55, 50, Personality.RiskTaker));
            // Blue Orbit: consistente.
            list.Add(MakeDriver("orbit_one", "Orbit One", "OB1", 3, "blue_orbit",
                72, 70, 75, 55, 70, 85, 70, 72, 65, 68, Personality.Balanced));
            list.Add(MakeDriver("orbit_two", "Orbit Two", "OB2", 4, "blue_orbit",
                70, 68, 74, 52, 72, 82, 72, 70, 64, 66, Personality.Veteran));
            // Emerald Rollers: curvas/controle.
            list.Add(MakeDriver("emerald_one", "Emerald One", "EM1", 5, "emerald_rollers",
                70, 68, 88, 58, 66, 70, 68, 64, 60, 72, Personality.Smooth));
            list.Add(MakeDriver("emerald_two", "Emerald Two", "EM2", 6, "emerald_rollers",
                68, 66, 85, 55, 64, 72, 70, 62, 60, 74, Personality.Conservative));
            // Shadow Marble: pit/estrategia.
            list.Add(MakeDriver("shadow_one", "Shadow One", "SH1", 7, "shadow_marble",
                74, 66, 72, 60, 74, 75, 72, 70, 90, 65, Personality.Defensive));
            list.Add(MakeDriver("shadow_two", "Shadow Two", "SH2", 8, "shadow_marble",
                72, 64, 70, 58, 72, 76, 74, 72, 88, 64, Personality.Balanced));
            return list;
        }

        private static MarbleDriverSO MakeDriver(string id, string name, string code, int number, string teamId,
            int spd, int acc, int ctrl, int agg, int def, int cons, int tire, int energy, int pit, int wet,
            Personality pers)
        {
            var so = CreateOrLoad<MarbleDriverSO>($"{Root}/Drivers/Driver_{id}.asset");
            so.driverId = id; so.marbleName = name; so.shortCode = code; so.number = number; so.teamId = teamId;
            so.speed = spd; so.acceleration = acc; so.control = ctrl; so.aggression = agg;
            so.defense = def; so.consistency = cons; so.tireManagement = tire;
            so.energyManagement = energy; so.pitSkill = pit; so.wetSkill = wet;
            so.personality = pers;
            EditorUtility.SetDirty(so);
            return so;
        }

        // ---- Tracks (PRD 42 + placeholders 21.2) ------------------------

        private static List<TrackDataSO> CreateTracks()
        {
            var list = new List<TrackDataSO>();

            list.Add(MakeTrack("marble_park", "Marble Park", Difficulty.Easy, 5, 1.0f, 0.5f, 8f, false,
                "Circuito inicial, equilibrado.", OvalPoints()));
            list.Add(MakeTrack("neon_harbor", "Neon Harbor", Difficulty.Medium, 6, 1.1f, 0.8f, 8f, false,
                "Urbano costeiro, retas longas e curvas de 90 graus.", HarborPoints()));
            list.Add(MakeTrack("spiral_canyon", "Spiral Canyon", Difficulty.Hard, 5, 1.4f, 0.4f, 7f, false,
                "Muitas curvas, desgaste alto.", SpiralPoints()));

            // Placeholders bloqueados do campeonato completo (PRD 21.2).
            string[] locked = {
                "sakura_speedway:Sakura Speedway", "desert_loop:Desert Loop", "ice_bowl:Ice Bowl",
                "volcano_ring:Volcano Ring", "rainforest_gp:Rainforest GP", "metro_marble:Metro Marble Circuit",
                "royal_garden:Royal Garden", "skybridge:Skybridge Circuit", "factory_run:Factory Run",
                "moonbase_gp:Moonbase GP", "atlantis_drift:Atlantis Drift", "final_orbit:Final Orbit"
            };
            foreach (var entry in locked)
            {
                var parts = entry.Split(':');
                var t = MakeTrack(parts[0], parts[1], Difficulty.Medium, 5, 1.0f, 0.5f, 8f, true,
                    "Placeholder do campeonato completo.", OvalPoints());
                list.Add(t);
            }
            return list;
        }

        private static TrackDataSO MakeTrack(string id, string name, Difficulty diff, int laps,
            float abrasion, float overtake, float width, bool locked, string desc, List<Vector2> points)
        {
            var so = CreateOrLoad<TrackDataSO>($"{Root}/Tracks/Track_{id}.asset");
            so.trackId = id; so.trackName = name; so.difficulty = diff;
            so.recommendedLaps = laps; so.abrasionLevel = abrasion; so.overtakeLevel = overtake;
            so.rainChance = 0f; so.pitLaneTimeLoss = 4f; so.trackLocked = locked;
            so.description = desc; so.controlPoints = points; so.trackWidth = width;
            so.checkpointEvery = 4;
            so.trackLength = Perimeter(points);
            EditorUtility.SetDirty(so);
            return so;
        }

        private static float Perimeter(List<Vector2> pts)
        {
            float total = 0f;
            for (int i = 0; i < pts.Count; i++)
                total += Vector2.Distance(pts[i], pts[(i + 1) % pts.Count]);
            return total;
        }

        // Layouts de control points (plano XZ).
        private static List<Vector2> OvalPoints() => new List<Vector2>
        {
            new(-30,-18), new(0,-22), new(30,-18), new(38,0),
            new(30,18), new(0,22), new(-30,18), new(-38,0)
        };

        private static List<Vector2> HarborPoints() => new List<Vector2>
        {
            new(-45,-20), new(0,-24), new(45,-20), new(48,-5),
            new(20,-5), new(20,15), new(48,15), new(45,28),
            new(0,30), new(-45,28), new(-48,5), new(-48,-8)
        };

        private static List<Vector2> SpiralPoints() => new List<Vector2>
        {
            new(-32,-20), new(-5,-26), new(25,-22), new(36,-4),
            new(14,2), new(-2,-8), new(-16,4), new(0,16),
            new(24,18), new(34,30), new(0,34), new(-34,26), new(-40,2)
        };

        // ---- Util -------------------------------------------------------

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var so = ScriptableObject.CreateInstance<T>();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(so, path);
            return so;
        }
    }
}
#endif
