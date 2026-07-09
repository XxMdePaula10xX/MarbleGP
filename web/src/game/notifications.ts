// =====================================================================
// Lembretes locais + badge — port de Core/GameNotifications.cs
// Web: no-op silencioso. iOS/Android (Capacitor): LocalNotifications +
// Badge. Mesmos textos e horários do Unity (10h desafio, 19h corrida,
// lembretes 1/2/4/7/14 dias).
// =====================================================================

import { Capacitor } from '@capacitor/core';

const REMINDERS: Array<{ title: string; body: string; days: number }> = [
  { title: '🏁 As pistas chamam!', body: 'Sua equipe está pronta. Bora competir?', days: 1 },
  { title: '🏆 Supere seu recorde', body: 'Será que hoje você bate seu melhor resultado?', days: 2 },
  { title: '🏎️ A grid sente sua falta', body: 'Volte e brigue pelo pódio.', days: 4 },
  { title: '📣 O campeonato não para', body: 'Já faz uma semana! Acelere de volta.', days: 7 },
  { title: '🔥 Sua equipe precisa de você', body: 'Duas semanas fora... hora de voltar às pistas!', days: 14 },
];

const ID_REMINDER_BASE = 100;      // 100..104
const ID_DAILY_CHALLENGE = 200;    // 10h recorrente
const ID_RACE_TIME = 201;          // 19h recorrente

function isNative(): boolean {
  return Capacitor.isNativePlatform();
}

export const GameNotifications = {
  /** Pede permissão. Chame no boot do app. */
  async setup(): Promise<void> {
    if (!isNative()) return;
    try {
      const { LocalNotifications } = await import('@capacitor/local-notifications');
      await LocalNotifications.requestPermissions();
    } catch (e) {
      console.warn('[Notifications] setup falhou:', e);
    }
  },

  /**
   * (Re)agenda os lembretes a partir de agora. Chamado quando o app vai
   * para segundo plano, para lembrar relativo à última sessão.
   */
  async scheduleReminders(): Promise<void> {
    if (!isNative()) return;
    try {
      const { LocalNotifications } = await import('@capacitor/local-notifications');

      const pending = await LocalNotifications.getPending();
      if (pending.notifications.length > 0) {
        await LocalNotifications.cancel({ notifications: pending.notifications.map(n => ({ id: n.id })) });
      }

      const now = new Date();
      const notifications = REMINDERS.map((r, i) => ({
        id: ID_REMINDER_BASE + i,
        title: r.title,
        body: r.body,
        schedule: { at: new Date(now.getTime() + r.days * 24 * 3600 * 1000) },
      }));

      // Recorrente de manhã (10h): novo Desafio do Dia.
      notifications.push({
        id: ID_DAILY_CHALLENGE,
        title: '🏁 Novo Desafio do Dia!',
        body: 'Um novo desafio te espera. Encare e aumente sua sequência!',
        schedule: { on: { hour: 10, minute: 0 }, allowWhileIdle: true } as never,
      });
      // Recorrente à noite (19h): hora da corrida.
      notifications.push({
        id: ID_RACE_TIME,
        title: '🏎️ Hora da corrida!',
        body: 'Bora dar umas voltas no Marble GP?',
        schedule: { on: { hour: 19, minute: 0 }, allowWhileIdle: true } as never,
      });

      await LocalNotifications.schedule({ notifications });
    } catch (e) {
      console.warn('[Notifications] scheduleReminders falhou:', e);
    }
  },

  /** Zera o badge e limpa notificações entregues. Chamado ao abrir/voltar. */
  async clearBadgeAndDelivered(): Promise<void> {
    if (!isNative()) return;
    try {
      const { Badge } = await import('@capawesome/capacitor-badge');
      await Badge.clear();
    } catch { /* plugin ausente: ignora */ }
    try {
      const { LocalNotifications } = await import('@capacitor/local-notifications');
      await LocalNotifications.removeAllDeliveredNotifications();
    } catch { /* idem */ }
  },
};

/** Liga os hooks de ciclo de vida (pause/resume) — chame uma vez no boot. */
export function bindNotificationLifecycle(): void {
  void GameNotifications.setup();
  void GameNotifications.clearBadgeAndDelivered();

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') {
      void GameNotifications.scheduleReminders();
    } else {
      void GameNotifications.clearBadgeAndDelivered();
    }
  });
}
