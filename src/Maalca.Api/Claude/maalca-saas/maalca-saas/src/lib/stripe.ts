// src/lib/stripe.ts
import Stripe from 'stripe';

if (!process.env.STRIPE_SECRET_KEY) {
  throw new Error('STRIPE_SECRET_KEY is not set');
}

export const stripe = new Stripe(process.env.STRIPE_SECRET_KEY, {
  // Pin API version to avoid surprise breakage on Stripe upgrades.
  apiVersion: '2024-12-18.acacia',
  typescript: true,
});

export const STRIPE_CONFIG = {
  priceEntrepreneur: process.env.STRIPE_PRICE_ENTREPRENEUR!,
  webhookSecret: process.env.STRIPE_WEBHOOK_SECRET!,
  publishableKey: process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY!,
} as const;

if (!STRIPE_CONFIG.priceEntrepreneur) {
  console.warn('[stripe] STRIPE_PRICE_ENTREPRENEUR not set — checkout will fail');
}
