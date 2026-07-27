// src/lib/plan-limits.ts
// Single source of truth for plan limits. Used by API + UI.

export type Plan = 'free' | 'entrepreneur';

export interface PlanLimits {
  products: number;        // hard cap
  warningThreshold: number; // show warning banner from this count
  customDomain: boolean;
  onlinePayments: boolean;
  analytics: boolean;
}

const LIMITS: Record<Plan, PlanLimits> = {
  free: {
    products: 10,
    warningThreshold: 7,
    customDomain: false,
    onlinePayments: false,
    analytics: false,
  },
  entrepreneur: {
    products: Infinity,
    warningThreshold: Infinity,
    customDomain: true,
    onlinePayments: true,
    analytics: true,
  },
};

export function getPlanLimits(plan: Plan): PlanLimits {
  return LIMITS[plan];
}

export function canAddProduct(plan: Plan, currentCount: number): boolean {
  return currentCount < getPlanLimits(plan).products;
}

export function shouldWarnNearLimit(plan: Plan, currentCount: number): boolean {
  const limits = getPlanLimits(plan);
  return currentCount >= limits.warningThreshold && currentCount < limits.products;
}

export function remainingProducts(plan: Plan, currentCount: number): number {
  const max = getPlanLimits(plan).products;
  if (max === Infinity) return Infinity;
  return Math.max(0, max - currentCount);
}

export const ENTREPRENEUR_PRICE_USD = 38;
