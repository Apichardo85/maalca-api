// src/lib/analytics.ts
// Dual-track: GA4 (client) + Supabase analytics_events (server, source of truth)

export type AnalyticsEvent =
  | 'click_start_free'
  | 'login_google_success'
  | 'onboarding_completed'
  | 'first_product_created'
  | 'link_copied'
  | 'upgrade_clicked'
  | 'upgrade_completed';

interface TrackProps {
  business_id?: string;
  [key: string]: unknown;
}

declare global {
  interface Window {
    gtag?: (...args: unknown[]) => void;
    dataLayer?: unknown[];
  }
}

/**
 * Client-side track. Fires GA4 + Supabase in parallel, fire-and-forget.
 * Safe to call without await.
 */
export function track(event: AnalyticsEvent, properties: TrackProps = {}): void {
  // 1. GA4
  if (typeof window !== 'undefined' && typeof window.gtag === 'function') {
    window.gtag('event', event, properties);
  }

  // 2. Supabase (via API route, fire-and-forget)
  if (typeof window !== 'undefined') {
    try {
      fetch('/api/analytics/track', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ event, properties }),
        keepalive: true,
      }).catch(() => {
        /* swallow — analytics must never break UX */
      });
    } catch {
      /* swallow */
    }
  }
}

/**
 * Server-side track. Use from server actions / API routes / webhooks.
 * Requires service-role Supabase client passed in by caller.
 */
export async function trackServer(
  supabase: { from: (t: string) => { insert: (data: unknown) => Promise<{ error: unknown }> } },
  event: AnalyticsEvent,
  payload: { user_id?: string; business_id?: string; properties?: TrackProps }
): Promise<void> {
  try {
    await supabase.from('analytics_events').insert({
      event_name: event,
      user_id: payload.user_id ?? null,
      business_id: payload.business_id ?? null,
      properties: payload.properties ?? {},
    });
  } catch {
    /* swallow */
  }
}
