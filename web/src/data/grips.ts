// =====================================================================
// Compostos de grip e superficies — port de Editor/DataGenerator.cs
// (CreateGrips / CreateSurfaces) + GripRingSO/SurfaceProfileSO
// =====================================================================

import type { GripRing, GripType, SurfaceProfile, SurfaceType } from '../core/types';

export const GRIPS: Record<GripType, GripRing> = {
  Soft: {
    gripId: 'Soft', compoundName: 'Soft Grip',
    speedMultiplier: 1.08, gripMultiplier: 1.06, wearRate: 18,
    wetPerformance: 0.6, dryPerformance: 1.0,
    energyMultiplier: 1.15, fuelMultiplier: 1.10, wearMultiplier: 1.50,
    description: 'Rápido e aderente, mas gasta mais energia/combustível e desgasta rápido.',
  },
  Medium: {
    gripId: 'Medium', compoundName: 'Medium Grip',
    speedMultiplier: 1.00, gripMultiplier: 1.00, wearRate: 11,
    wetPerformance: 0.7, dryPerformance: 1.0,
    energyMultiplier: 1.00, fuelMultiplier: 1.00, wearMultiplier: 1.00,
    description: 'Equilibrado em tudo.',
  },
  Hard: {
    gripId: 'Hard', compoundName: 'Hard Grip',
    speedMultiplier: 0.96, gripMultiplier: 0.97, wearRate: 7,
    wetPerformance: 0.7, dryPerformance: 1.0,
    energyMultiplier: 0.90, fuelMultiplier: 0.92, wearMultiplier: 0.65,
    description: 'Mais lento, porém econômico (energia/combustível) e durável.',
  },
  Intermediate: {
    gripId: 'Intermediate', compoundName: 'Intermediate Grip',
    speedMultiplier: 0.98, gripMultiplier: 1.00, wearRate: 12,
    wetPerformance: 1.0, dryPerformance: 0.92,
    energyMultiplier: 1.00, fuelMultiplier: 1.05, wearMultiplier: 1.20,
    description: 'Para pista úmida. Ruim no seco e na chuva muito forte.',
  },
  Rain: {
    gripId: 'Rain', compoundName: 'Rain Grip',
    speedMultiplier: 0.95, gripMultiplier: 1.05, wearRate: 10,
    wetPerformance: 1.08, dryPerformance: 0.85,
    energyMultiplier: 1.00, fuelMultiplier: 1.15, wearMultiplier: 1.10,
    description: 'Para chuva forte. Lento e gasta muito no seco.',
  },
};

export const SURFACES: Record<Extract<SurfaceType, 'Polished' | 'MicroGrooved' | 'Textured'>, SurfaceProfile> = {
  Polished: {
    surfaceId: 'Polished', surfaceName: 'Polished',
    speedModifier: 1.05, controlModifier: 0.92, wearModifier: 1.10,
    energyModifier: 1.0, wetModifier: 0.9,
    description: 'Mais rápida em retas, menos controle em curvas, desgaste maior.',
  },
  MicroGrooved: {
    surfaceId: 'MicroGrooved', surfaceName: 'Micro-Grooved',
    speedModifier: 1.00, controlModifier: 1.00, wearModifier: 1.00,
    energyModifier: 1.0, wetModifier: 1.0,
    description: 'Equilibrada, boa para a maioria dos circuitos.',
  },
  Textured: {
    surfaceId: 'Textured', surfaceName: 'Textured',
    speedModifier: 0.95, controlModifier: 1.08, wearModifier: 0.95,
    energyModifier: 1.0, wetModifier: 1.1,
    description: 'Melhor aderência, mais lenta em retas.',
  },
};

export const DEFAULT_SURFACE = SURFACES.MicroGrooved;
