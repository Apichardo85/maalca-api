// src/app/auth/callback/route.ts
// Updated callback: simplified routing — businesses table is the source of truth.
// Hardcoded affiliates have been migrated (002_migrate_affiliates.sql).
// Remove the KNOWN_AFFILIATES fallback after one deploy cycle of verification.

import { NextRequest, NextResponse } from 'next/server';
import { supabaseServer } from '@/lib/supabase/server';

// Fallback only — remove after verifying migration worked
const KNOWN_AFFILIATES: Record<string, { affiliate_id: string; role: string }> = {
  'alejandropichardo85@gmail.com': { affiliate_id: 'maalca', role: 'admin' },
  'littledominicanarestaurant@gmail.com': { affiliate_id: 'the-little-dominican', role: 'owner' },
};

export async function GET(request: NextRequest) {
  const { searchParams, origin } = new URL(request.url);
  const code = searchParams.get('code');

  if (!code) {
    return NextResponse.redirect(`${origin}/login?error=no_code`);
  }

  const supabase = supabaseServer();
  const { data, error } = await supabase.auth.exchangeCodeForSession(code);

  if (error || !data.user) {
    return NextResponse.redirect(`${origin}/login?error=exchange_failed`);
  }

  // Upsert into your existing users table (if any) — keep your existing logic
  // await supabase.from('users').upsert(...);

  // Track login_google_success server-side (we know it's google because of OAuth provider)
  const provider = data.user.app_metadata?.provider;
  if (provider === 'google') {
    await supabase.from('analytics_events').insert({
      user_id: data.user.id,
      event_name: 'login_google_success',
      properties: { provider },
    });
  }

  // ── Routing logic ────────────────────────────────────────────────────────
  // 1. User has a business in `businesses` table → /space/[slug]
  // 2. Email in fallback affiliate map → /dashboard/[affiliate_id] (legacy)
  // 3. New user → /onboarding

  const { data: businesses } = await supabase
    .from('businesses')
    .select('slug')
    .eq('owner_id', data.user.id)
    .order('created_at', { ascending: true })
    .limit(1);

  let redirectPath: string;

  if (businesses && businesses.length > 0) {
    // Primary path: user has a business
    redirectPath = `/space/${businesses[0].slug}`;
  } else if (data.user.email && KNOWN_AFFILIATES[data.user.email]) {
    // Legacy fallback (should not trigger after migration 002 runs)
    redirectPath = `/dashboard/${KNOWN_AFFILIATES[data.user.email].affiliate_id}`;
  } else {
    // New user
    redirectPath = '/onboarding';
  }

  return NextResponse.redirect(`${origin}${redirectPath}`);
}
