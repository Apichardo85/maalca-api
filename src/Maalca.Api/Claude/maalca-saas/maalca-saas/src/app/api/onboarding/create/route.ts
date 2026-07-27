// src/app/api/onboarding/create/route.ts
import { NextResponse } from 'next/server';
import { supabaseServer } from '@/lib/supabase/server';
import { generateUniqueSlug } from '@/lib/slug';

const VALID_TYPES = ['restaurant', 'barber', 'service', 'retail'];

export async function POST(req: Request) {
  const supabase = supabaseServer();

  // Auth
  const { data: { user }, error: authError } = await supabase.auth.getUser();
  if (authError || !user) {
    return NextResponse.json({ error: 'unauthorized' }, { status: 401 });
  }

  // Parse + validate
  let body: { name?: string; business_type?: string };
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: 'invalid_json' }, { status: 400 });
  }

  const name = body.name?.trim();
  const business_type = body.business_type;

  if (!name || name.length < 2 || name.length > 50) {
    return NextResponse.json({ error: 'invalid_name' }, { status: 400 });
  }
  if (!business_type || !VALID_TYPES.includes(business_type)) {
    return NextResponse.json({ error: 'invalid_business_type' }, { status: 400 });
  }

  // One business per user on free plan (for now — relax later if needed)
  const { data: existing } = await supabase
    .from('businesses')
    .select('id, slug')
    .eq('owner_id', user.id)
    .limit(1)
    .maybeSingle();

  if (existing) {
    return NextResponse.json(
      { error: 'business_already_exists', business: existing },
      { status: 409 }
    );
  }

  // Generate slug
  const slug = await generateUniqueSlug(supabase, name);

  // Insert business
  const { data: business, error: insertError } = await supabase
    .from('businesses')
    .insert({
      owner_id: user.id,
      slug,
      name,
      business_type,
      plan: 'free',
      plan_status: 'active',
      published: true,
    })
    .select('id, slug, name, business_type, plan')
    .single();

  if (insertError || !business) {
    console.error('[onboarding] insert failed', insertError);
    return NextResponse.json({ error: 'insert_failed' }, { status: 500 });
  }

  // Init onboarding progress (creating space = first checkbox auto-done)
  await supabase.from('onboarding_progress').insert({
    business_id: business.id,
    first_product_added: false,
    whatsapp_configured: false,
    link_shared: false,
  });

  // Server-side analytics
  await supabase.from('analytics_events').insert({
    user_id: user.id,
    business_id: business.id,
    event_name: 'onboarding_completed',
    properties: { business_type },
  });

  return NextResponse.json({ business });
}
