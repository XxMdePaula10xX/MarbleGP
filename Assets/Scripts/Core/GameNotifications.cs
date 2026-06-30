using System;
using System.Collections;
using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

namespace MarbleGP.Core
{
    /// <summary>
    /// Lembretes locais ("volte a jogar") + badge no ícone que some ao abrir o app.
    /// Usa o pacote com.unity.mobile.notifications (iOS + Android). Em outras
    /// plataformas (editor desktop) os métodos viram no-op, então o jogo compila
    /// e roda normalmente sem o pacote.
    /// Chamado pelo GameManager no ciclo de vida do app.
    /// </summary>
    public static class GameNotifications
    {
        private const string AndroidChannel = "marblegp_reminders";

        // Lembretes escalonados para quem se ausenta: (título, corpo, dias até disparar).
        private static readonly (string title, string body, int days)[] Reminders =
        {
            ("🏁 As pistas chamam!",         "Sua equipe está pronta. Bora competir?",         1),
            ("🏆 Supere seu recorde",        "Será que hoje você bate seu melhor resultado?",  2),
            ("🏎️ A grid sente sua falta",    "Volte e brigue pelo pódio.",                     4),
            ("📣 O campeonato não para",     "Já faz uma semana! Acelere de volta.",           7),
            ("🔥 Sua equipe precisa de você", "Duas semanas fora... hora de voltar às pistas!", 14),
        };

        /// <summary>
        /// Pede permissão (iOS / Android 13+) e registra o canal (Android).
        /// Rode via StartCoroutine no boot do jogo.
        /// </summary>
        public static IEnumerator Setup()
        {
#if UNITY_IOS
            var opt = AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound;
            using (var req = new AuthorizationRequest(opt, true))
                while (!req.IsFinished) yield return null;
#elif UNITY_ANDROID
            var channel = new AndroidNotificationChannel
            {
                Id = AndroidChannel,
                Name = "Lembretes de corrida",
                Importance = Importance.Default,
                Description = "Lembra você de voltar a correr no Marble GP."
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
            // Android 13+ exige permissão POST_NOTIFICATIONS (ignora retorno).
            AndroidNotificationCenter.RequestNotificationPermission();
            yield break;
#else
            yield break;
#endif
        }

        /// <summary>
        /// (Re)agenda os lembretes a partir de agora. Chamado quando o app vai
        /// para segundo plano / fecha, para lembrar relativo à última sessão.
        /// </summary>
        public static void ScheduleReminders()
        {
#if UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            for (int i = 0; i < Reminders.Length; i++)
            {
                var r = Reminders[i];
                var n = new iOSNotification
                {
                    Identifier = "marblegp_reminder_" + i,
                    Title = r.title,
                    Body = r.body,
                    ShowInForeground = false,
                    Badge = i + 1,
                    Trigger = new iOSNotificationTimeIntervalTrigger
                    {
                        TimeInterval = TimeSpan.FromDays(r.days),
                        Repeats = false
                    }
                };
                iOSNotificationCenter.ScheduleNotification(n);
            }
            // Recorrente de manhã (10h): novo Desafio do Dia disponível.
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = "marblegp_daily_challenge",
                Title = "🏁 Novo Desafio do Dia!",
                Body = "Um novo desafio te espera. Encare e aumente sua sequência!",
                ShowInForeground = false,
                Badge = 1,
                Trigger = new iOSNotificationCalendarTrigger { Hour = 10, Minute = 0, Repeats = true }
            });
            // Recorrente à noite (19h): hora da corrida.
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = "marblegp_daily",
                Title = "🏎️ Hora da corrida!",
                Body = "Bora dar umas voltas no Marble GP?",
                ShowInForeground = false,
                Badge = 1,
                Trigger = new iOSNotificationCalendarTrigger { Hour = 19, Minute = 0, Repeats = true }
            });
#elif UNITY_ANDROID
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            for (int i = 0; i < Reminders.Length; i++)
            {
                var r = Reminders[i];
                var n = new AndroidNotification
                {
                    Title = r.title,
                    Text = r.body,
                    FireTime = DateTime.Now.AddDays(r.days),
                    Number = i + 1,            // badge/contador no ícone
                    ShouldAutoCancel = true
                };
                AndroidNotificationCenter.SendNotification(n, AndroidChannel);
            }
            // Recorrente de manhã (10h): novo Desafio do Dia.
            var morning = DateTime.Now.Date.AddHours(10);
            if (morning < DateTime.Now) morning = morning.AddDays(1);
            AndroidNotificationCenter.SendNotification(new AndroidNotification
            {
                Title = "🏁 Novo Desafio do Dia!",
                Text = "Um novo desafio te espera. Encare e aumente sua sequência!",
                FireTime = morning,
                RepeatInterval = TimeSpan.FromDays(1),
                Number = 1,
                ShouldAutoCancel = true
            }, AndroidChannel);
            // Recorrente à noite (19h): hora da corrida.
            var fire = DateTime.Now.Date.AddHours(19);
            if (fire < DateTime.Now) fire = fire.AddDays(1);
            AndroidNotificationCenter.SendNotification(new AndroidNotification
            {
                Title = "🏎️ Hora da corrida!",
                Text = "Bora dar umas voltas no Marble GP?",
                FireTime = fire,
                RepeatInterval = TimeSpan.FromDays(1),
                Number = 1,
                ShouldAutoCancel = true
            }, AndroidChannel);
#endif
        }

        /// <summary>
        /// Zera o badge do ícone e limpa o que já foi entregue. Chamado ao abrir /
        /// voltar para o app (badge "some ao entrar").
        /// </summary>
        public static void ClearBadgeAndDelivered()
        {
#if UNITY_IOS
            iOSNotificationCenter.ApplicationBadge = 0;
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
#elif UNITY_ANDROID
            AndroidNotificationCenter.CancelAllDisplayedNotifications();
#endif
        }
    }
}
