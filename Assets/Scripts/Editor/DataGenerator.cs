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
            // id, nome, speed, grip, wear, wet, dry, energyMult, fuelMult, wearMult, desc (PRD 5.2/13)
            list.Add(MakeGrip(GripType.Soft, "Soft Grip", 1.08f, 1.06f, 18f, 0.6f, 1.0f, 1.15f, 1.10f, 1.50f,
                "Rapido e aderente, mas gasta mais energia/combustivel e desgasta rapido."));
            list.Add(MakeGrip(GripType.Medium, "Medium Grip", 1.00f, 1.00f, 11f, 0.7f, 1.0f, 1.00f, 1.00f, 1.00f,
                "Equilibrado em tudo."));
            list.Add(MakeGrip(GripType.Hard, "Hard Grip", 0.96f, 0.97f, 7f, 0.7f, 1.0f, 0.90f, 0.92f, 0.65f,
                "Mais lento, porem economico (energia/combustivel) e duravel."));
            list.Add(MakeGrip(GripType.Intermediate, "Intermediate Grip", 0.98f, 1.00f, 12f, 1.0f, 0.92f, 1.00f, 1.05f, 1.20f,
                "Para pista umida. Ruim no seco e na chuva muito forte."));
            list.Add(MakeGrip(GripType.Rain, "Rain Grip", 0.95f, 1.05f, 10f, 1.08f, 0.85f, 1.00f, 1.15f, 1.10f,
                "Para chuva forte. Lento e gasta muito no seco."));
            return list;
        }

        private static GripRingSO MakeGrip(GripType id, string name, float spd, float grip,
            float wear, float wet, float dry, float energyMult, float fuelMult, float wearMult, string desc)
        {
            var so = CreateOrLoad<GripRingSO>($"{Root}/GripRings/Grip_{id}.asset");
            so.gripId = id; so.compoundName = name;
            so.speedMultiplier = spd; so.gripMultiplier = grip; so.wearRate = wear;
            so.wetPerformance = wet; so.dryPerformance = dry;
            so.energyMultiplier = energyMult; so.fuelMultiplier = fuelMult; so.wearMultiplier = wearMult;
            so.description = desc;
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
            // Equipes restantes (PRD 11) para grid de 20.
            list.Add(MakeTeam("solar_spin", "Solar Spin", "#F2C200", "#F07000",
                TeamStyle.Fast, TeamBonusType.Acceleration, 55, 52, "Veloz, forte aceleracao."));
            list.Add(MakeTeam("frostline", "Frostline Racing", "#7FD0F0", "#C0C8D0",
                TeamStyle.Precise, TeamBonusType.WetWeather, 58, 55, "Precisa, otima na chuva."));
            list.Add(MakeTeam("iron_sphere", "Iron Sphere", "#8A8A8A", "#C0241F",
                TeamStyle.Resistant, TeamBonusType.LowDamage, 60, 50, "Resistente, baixo dano."));
            list.Add(MakeTeam("neon_pulse", "Neon Pulse", "#FF4FA3", "#20E0E0",
                TeamStyle.Unpredictable, TeamBonusType.Overtaking, 54, 53, "Imprevisivel, boa em ultrapassagem."));
            list.Add(MakeTeam("jungle_curve", "Jungle Curve", "#2E6B2E", "#6B4A2A",
                TeamStyle.Technical, TeamBonusType.Cornering, 53, 56, "Tecnica em curvas lentas."));
            list.Add(MakeTeam("royal_club", "Royal Marble Club", "#6A2FB0", "#D4AF37",
                TeamStyle.Premium, TeamBonusType.Development, 64, 66, "Premium, forte desenvolvimento."));
            // 10 equipes x 2 bolinhas = grid de 20 (PRD 21.1).
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

            // ---- Equipes restantes (16 bolinhas) para grid de 20 ----
            list.Add(MakeDriver("solar_one", "Solar One", "SL1", 9, "solar_spin",
                78, 86, 64, 66, 60, 64, 55, 58, 58, 55, Personality.Aggressive));
            list.Add(MakeDriver("solar_two", "Solar Two", "SL2", 10, "solar_spin",
                76, 84, 62, 64, 60, 66, 56, 60, 58, 55, Personality.RiskTaker));
            list.Add(MakeDriver("frost_one", "Frost One", "FR1", 11, "frostline",
                70, 68, 78, 54, 70, 78, 70, 70, 64, 88, Personality.Smooth));
            list.Add(MakeDriver("frost_two", "Frost Two", "FR2", 12, "frostline",
                68, 66, 76, 52, 72, 80, 72, 70, 64, 85, Personality.Conservative));
            list.Add(MakeDriver("iron_one", "Iron One", "IR1", 13, "iron_sphere",
                66, 64, 72, 56, 82, 78, 80, 74, 62, 60, Personality.Defensive));
            list.Add(MakeDriver("iron_two", "Iron Two", "IR2", 14, "iron_sphere",
                64, 62, 70, 54, 84, 80, 82, 76, 62, 60, Personality.Veteran));
            list.Add(MakeDriver("neon_one", "Neon One", "NP1", 15, "neon_pulse",
                80, 78, 66, 84, 58, 56, 52, 56, 58, 58, Personality.RiskTaker));
            list.Add(MakeDriver("neon_two", "Neon Two", "NP2", 16, "neon_pulse",
                78, 76, 64, 86, 56, 54, 50, 56, 58, 58, Personality.Aggressive));
            list.Add(MakeDriver("jungle_one", "Jungle One", "JG1", 17, "jungle_curve",
                68, 66, 86, 56, 66, 72, 70, 64, 60, 70, Personality.Smooth));
            list.Add(MakeDriver("jungle_two", "Jungle Two", "JG2", 18, "jungle_curve",
                66, 64, 84, 54, 66, 74, 72, 64, 60, 70, Personality.Conservative));
            list.Add(MakeDriver("royal_one", "Royal One", "RY1", 19, "royal_club",
                80, 76, 78, 64, 70, 80, 70, 72, 70, 68, Personality.Veteran));
            list.Add(MakeDriver("royal_two", "Royal Two", "RY2", 20, "royal_club",
                78, 74, 76, 70, 66, 72, 66, 70, 70, 66, Personality.Balanced));
            // Grid de 20 (10 equipes x 2). Volcano/Aqua removidos para manter 20.
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

            list.Add(MakeTrack("marble_park", "Marble Park", Difficulty.Easy, 5, 1.0f, 0.5f, 0.10f, 8f, false,
                "Circuito inicial, equilibrado.", OvalPoints()));
            list.Add(MakeTrack("neon_harbor", "Neon Harbor", Difficulty.Medium, 6, 1.1f, 0.8f, 0.35f, 8f, false,
                "Urbano costeiro, retas longas e curvas de 90 graus.", HarborPoints()));
            list.Add(MakeTrack("spiral_canyon", "Spiral Canyon", Difficulty.Hard, 5, 1.4f, 0.4f, 0.25f, 7f, false,
                "Muitas curvas, desgaste alto.", SpiralPoints()));

            // Campeonato completo — 12 circuitos com geometria propria (PRD 21.2).
            // Cada traçado usa Loop() (curva radial estrela => nunca se auto-cruza).
            list.Add(MakeTrack("sakura_speedway", "Sakura Speedway", Difficulty.Medium, 6, 1.05f, 0.70f, 0.25f, 8f, false,
                "Curvas fluidas sob as cerejeiras; ritmo constante e médio desgaste.",
                Loop(14, 40f, 26f, 0.14f, 3, 0.40f, 0.10f)));
            list.Add(MakeTrack("desert_loop", "Desert Loop", Difficulty.Easy, 6, 1.30f, 0.85f, 0.05f, 8f, false,
                "Retas longuíssimas no deserto; vácuo e ultrapassagens fáceis, asfalto abrasivo.",
                Loop(14, 50f, 20f, 0.0f, 0, 0f, 0f)));
            list.Add(MakeTrack("ice_bowl", "Ice Bowl", Difficulty.Medium, 5, 0.70f, 0.55f, 0.40f, 9f, false,
                "Tigela de gelo larga e escorregadia; baixo desgaste, mas pouca aderência.",
                Loop(14, 34f, 32f, 0.16f, 5, 0f, 0f)));
            list.Add(MakeTrack("volcano_ring", "Volcano Ring", Difficulty.Hard, 5, 1.50f, 0.45f, 0.15f, 7f, false,
                "Anel vulcânico estreito e quente; desgaste altíssimo, ultrapassar é difícil.",
                Loop(14, 30f, 30f, 0.22f, 2, 1.57f, 0f)));
            list.Add(MakeTrack("rainforest_gp", "Rainforest GP", Difficulty.Hard, 5, 1.20f, 0.50f, 0.60f, 7f, false,
                "Traçado sinuoso e muito úmido; o pneu de chuva costuma decidir a corrida.",
                Loop(14, 42f, 22f, 0.28f, 2, 0f, 0f)));
            list.Add(MakeTrack("metro_marble", "Metro Marble Circuit", Difficulty.Medium, 6, 1.10f, 0.60f, 0.30f, 7f, false,
                "Circuito urbano de cantos retos; muros próximos exigem precisão.",
                Loop(14, 34f, 32f, 0.16f, 4, 0f, 0.785f)));
            list.Add(MakeTrack("royal_garden", "Royal Garden", Difficulty.Medium, 6, 1.00f, 0.55f, 0.25f, 7f, false,
                "Jardim real técnico e elegante; três grandes setores de curvas.",
                Loop(14, 32f, 30f, 0.20f, 3, 1.57f, 0f)));
            list.Add(MakeTrack("skybridge", "Skybridge Circuit", Difficulty.Medium, 6, 1.00f, 0.85f, 0.20f, 8f, false,
                "Pontes suspensas rápidas; curvas amplas e muita ultrapassagem.",
                Loop(14, 46f, 22f, 0.10f, 4, 0.30f, 0f)));
            list.Add(MakeTrack("factory_run", "Factory Run", Difficulty.Hard, 5, 1.30f, 0.50f, 0.20f, 7f, false,
                "Linha de montagem apertada; muitas curvas e desgaste elevado.",
                Loop(14, 36f, 28f, 0.18f, 6, 0f, 0f)));
            list.Add(MakeTrack("moonbase_gp", "Moonbase GP", Difficulty.Medium, 6, 0.90f, 0.70f, 0.0f, 8f, false,
                "Base lunar de baixa gravidade; pista limpa, ampla e sem chuva.",
                Loop(14, 38f, 36f, 0.0f, 0, 0f, 0f)));
            list.Add(MakeTrack("atlantis_drift", "Atlantis Drift", Difficulty.Hard, 5, 1.10f, 0.60f, 0.50f, 8f, false,
                "Cidade submersa ondulante; aderência variável e clima instável.",
                Loop(14, 42f, 24f, 0.13f, 5, 0.60f, 0f)));
            list.Add(MakeTrack("final_orbit", "Final Orbit", Difficulty.Hard, 7, 1.40f, 0.75f, 0.30f, 8f, false,
                "Palco final do campeonato; longa, veloz e impiedosa com os pneus.",
                Loop(14, 46f, 28f, 0.16f, 3, 0.30f, 0.20f)));
            return list;
        }

        private static TrackDataSO MakeTrack(string id, string name, Difficulty diff, int laps,
            float abrasion, float overtake, float rainChance, float width, bool locked, string desc, List<Vector2> points)
        {
            var so = CreateOrLoad<TrackDataSO>($"{Root}/Tracks/Track_{id}.asset");
            so.trackId = id; so.trackName = name; so.difficulty = diff;
            so.recommendedLaps = laps; so.abrasionLevel = abrasion; so.overtakeLevel = overtake;
            so.rainChance = rainChance; so.pitLaneTimeLoss = 4f; so.trackLocked = locked;
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

        /// <summary>
        /// Gera um traçado fechado a partir de uma curva radial r(θ)=1+amp·sin(lobes·θ+phase)
        /// escalada por (rx, ry) e girada por rot. Como o ângulo cresce de forma monótona
        /// e r>0 (amp&lt;1), a curva é "estrela" e NUNCA se auto-intersecta — seguro mesmo
        /// sem teste visual. lobes controla o nº de "ondas"/cantos do circuito.
        /// </summary>
        private static List<Vector2> Loop(int n, float rx, float ry, float amp, int lobes, float phase, float rot)
        {
            var pts = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float t = (i / (float)n) * Mathf.PI * 2f;
                float r = 1f + amp * Mathf.Sin(lobes * t + phase);
                float a = t + rot;
                pts.Add(new Vector2(Mathf.Cos(a) * rx * r, Mathf.Sin(a) * ry * r));
            }
            return pts;
        }

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
