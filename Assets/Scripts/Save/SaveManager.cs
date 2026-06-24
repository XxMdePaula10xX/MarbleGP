using System;
using System.IO;
using UnityEngine;

namespace MarbleGP.Save
{
    /// <summary>
    /// Persistencia local em JSON (PRD 7.1 / 24.2). Sem autenticacao online.
    /// Salva em Application.persistentDataPath. Tambem guarda progresso de
    /// campeonato (estrutura preparada, PRD 29.3).
    /// </summary>
    public static class SaveManager
    {
        private static string ProfilePath => Path.Combine(Application.persistentDataPath, "profile.json");
        private static string ChampionshipPath => Path.Combine(Application.persistentDataPath, "championship.json");
        private static string AchievementsPath => Path.Combine(Application.persistentDataPath, "achievements.json");

        // ---- Perfil -----------------------------------------------------

        public static void SaveProfile(PlayerProfile profile)
        {
            try
            {
                File.WriteAllText(ProfilePath, JsonUtility.ToJson(profile, true));
                Debug.Log($"[SaveManager] Perfil salvo em {ProfilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Falha ao salvar perfil: {e.Message}");
            }
        }

        public static PlayerProfile LoadProfile()
        {
            try
            {
                if (File.Exists(ProfilePath))
                    return JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(ProfilePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Falha ao carregar perfil: {e.Message}");
            }
            return new PlayerProfile();
        }

        public static bool HasProfile() => File.Exists(ProfilePath);

        // ---- Campeonato (estrutura preparada, PRD 29.3) -----------------

        public static void SaveChampionship(string json)
        {
            try { File.WriteAllText(ChampionshipPath, json); }
            catch (Exception e) { Debug.LogError($"[SaveManager] Falha ao salvar campeonato: {e.Message}"); }
        }

        public static string LoadChampionship()
        {
            try { if (File.Exists(ChampionshipPath)) return File.ReadAllText(ChampionshipPath); }
            catch (Exception e) { Debug.LogError($"[SaveManager] Falha ao carregar campeonato: {e.Message}"); }
            return null;
        }

        // ---- Conquistas (PRD 32) ----------------------------------------

        public static void SaveAchievements(AchievementData data)
        {
            try { File.WriteAllText(AchievementsPath, JsonUtility.ToJson(data, true)); }
            catch (Exception e) { Debug.LogError($"[SaveManager] Falha ao salvar conquistas: {e.Message}"); }
        }

        public static AchievementData LoadAchievements()
        {
            try
            {
                if (File.Exists(AchievementsPath))
                    return JsonUtility.FromJson<AchievementData>(File.ReadAllText(AchievementsPath))
                           ?? new AchievementData();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Falha ao carregar conquistas: {e.Message}");
            }
            return new AchievementData();
        }

        public static void DeleteAll()
        {
            if (File.Exists(ProfilePath)) File.Delete(ProfilePath);
            if (File.Exists(ChampionshipPath)) File.Delete(ChampionshipPath);
            if (File.Exists(AchievementsPath)) File.Delete(AchievementsPath);
        }
    }
}
