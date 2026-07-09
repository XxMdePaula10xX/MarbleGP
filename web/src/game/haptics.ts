// =====================================================================
// Háptica (Capacitor Haptics). No-op silencioso no navegador; vibra no
// iOS/Android nos momentos-chave (pit, ultrapassagem, rádio, largada).
// =====================================================================

import { Capacitor } from '@capacitor/core';

type Style = 'light' | 'medium' | 'heavy';

function native(): boolean { return Capacitor.isNativePlatform(); }

async function impact(style: Style): Promise<void> {
  if (!native()) return;
  try {
    const mod = await import('@capacitor/haptics');
    const map = { light: mod.ImpactStyle.Light, medium: mod.ImpactStyle.Medium, heavy: mod.ImpactStyle.Heavy };
    await mod.Haptics.impact({ style: map[style] });
  } catch { /* plugin ausente: ignora */ }
}

export const Haptics = {
  /** Toque leve — botões, seleções. */
  tap(): void { void impact('light'); },
  /** Toque médio — pit solicitado, decisão do rádio, troca de modo. */
  medium(): void { void impact('medium'); },
  /** Toque forte — largada (lights out), vitória, batida. */
  heavy(): void { void impact('heavy'); },
  /** Sucesso — objetivo cumprido, conquista. */
  async success(): Promise<void> {
    if (!native()) return;
    try {
      const mod = await import('@capacitor/haptics');
      await mod.Haptics.notification({ type: mod.NotificationType.Success });
    } catch { /* ignora */ }
  },
};
