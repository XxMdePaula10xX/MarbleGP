using UnityEngine;
using UnityEngine.SceneManagement;
using MarbleGP.Data;
using MarbleGP.Save;

namespace MarbleGP.Core
{
    /// <summary>
    /// Estado global e ponto de entrada de sistemas (PRD 24.2).
    /// Singleton persistente entre cenas. Guarda o GameDatabase, o perfil
    /// ativo e o RaceConfig corrente que sera consumido pela RaceScene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Dados (arraste o GameDatabase gerado pelo Editor)")]
        [SerializeField] private GameDatabase database;

        public GameDatabase Database => database;
        public GameBalance Balance => database != null ? database.balance : null;

        public PlayerProfile Profile { get; private set; }

        /// <summary>Config da corrida montada pelos menus, lida pela RaceScene.</summary>
        public RaceConfig CurrentRace { get; set; }

        /// <summary>Gerenciador do campeonato em andamento (PRD 29).</summary>
        public ChampionshipManager Championship { get; set; }

        /// <summary>True quando a corrida atual faz parte do campeonato.</summary>
        public bool RaceIsChampionship { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Carrega o banco por Resources caso nao tenha sido atribuido no Inspector.
            if (database == null)
                database = Resources.Load<GameDatabase>("GameDatabase");

            Profile = SaveManager.LoadProfile();

            if (database == null)
                Debug.LogWarning("[GameManager] GameDatabase nao atribuido. " +
                    "Rode Tools > Marble GP > Gerar Dados do MVP e arraste o asset, " +
                    "ou coloque-o em Resources/GameDatabase.");

            // Notificacoes locais (lembretes) + limpa o badge ao abrir o app.
            StartCoroutine(GameNotifications.Setup());
            GameNotifications.ClearBadgeAndDelivered();
        }

        // Ciclo de vida do app: ao minimizar/fechar agenda os lembretes; ao voltar
        // limpa o badge e as notificacoes ja entregues (badge "some ao entrar").
        private void OnApplicationPause(bool paused)
        {
            if (paused) GameNotifications.ScheduleReminders();
            else GameNotifications.ClearBadgeAndDelivered();
        }

        // Reforco: ao reganhar o foco (voltar pro app), limpa o badge tambem.
        private void OnApplicationFocus(bool focus)
        {
            if (focus) GameNotifications.ClearBadgeAndDelivered();
        }

        private void OnApplicationQuit() => GameNotifications.ScheduleReminders();

        public void SaveProfile() => SaveManager.SaveProfile(Profile);

        public void LoadScene(string sceneName) => SceneManager.LoadScene(sceneName);
    }
}
