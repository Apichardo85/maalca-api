// src/app/api/analytics/track/route.ts
import { NextResponse } from 'next/server';
import { supabaseServer } from '@/lib/supabase/server';

const VALID_EVENTS = new Set([
  'click_start_free',
  'login_google_success',
  'onboarding_completed',
  'first_product_created',
  'link_copied',
  'upgrade_clicked',
  'upgrade_completed',
]);

export async function POST(req: Request) {
  let body: { event?: string; properties?: Record<string, unknown> };
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: 'invalid_json' }, { status: 400 });
  }

  if (!body.event || !VALID_EVENTS.has(body.event)) {
    return NextResponse.json({ error: 'invalid_event' }, { status: 400 });
  }

  const supabase = supabaseServer();
  const { data: { user } } = await supabase.auth.getUser();

  const { business_id, ...rest } = body.properties ?? {};

  await supabase.from('analytics_events').insert({
    user_id: user?.id ?? null,
    business_id: typeof business_id === 'string' ? business_id : null,
    event_name: body.event,
    properties: rest,
    user_agent: req.headers.get('user-agent') ?? null,
  });

  return NextResponse.json({ ok: true });
}
