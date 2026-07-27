// src/app/api/checkout/route.ts
import { NextResponse } from 'next/server';
import { supabaseServer } from '@/lib/supabase/server';
import { stripe, STRIPE_CONFIG } from '@/lib/stripe';

export async function POST(req: Request) {
  const supabase = supabaseServer();

  const { data: { user } } = await supabase.auth.getUser();
  if (!user) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });

  let body: { business_id?: string; return_slug?: string };
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: 'invalid_json' }, { status: 400 });
  }

  if (!body.business_id) {
    return NextResponse.json({ error: 'missing_business_id' }, { status: 400 });
  }

  // Verify ownership
  const { data: business } = await supabase
    .from('businesses')
    .select('id, slug, name, plan, stripe_customer_id')
    .eq('id', body.business_id)
    .eq('owner_id', user.id)
    .maybeSingle();

  if (!business) {
    return NextResponse.json({ error: 'business_not_found' }, { status: 404 });
  }

  if (business.plan === 'entrepreneur') {
    return NextResponse.json({ error: 'already_subscribed' }, { status: 400 });
  }

  const origin = req.headers.get('origin') ?? process.env.NEXT_PUBLIC_APP_URL ?? '';
  const returnSlug = body.return_slug ?? business.slug;

  try {
    const session = await stripe.checkout.sessions.create({
      mode: 'subscription',
      line_items: [{ price: STRIPE_CONFIG.priceEntrepreneur, quantity: 1 }],
      customer: business.stripe_customer_id ?? undefined,
      customer_email: business.stripe_customer_id ? undefined : user.email,
      client_reference_id: business.id,
      metadata: {
        business_id: business.id,
        user_id: user.id,
      },
      subscription_data: {
        metadata: {
          business_id: business.id,
          user_id: user.id,
        },
      },
      success_url: `${origin}/space/${returnSlug}?upgraded=1`,
      cancel_url: `${origin}/space/${returnSlug}`,
      allow_promotion_codes: true,
    });

    if (!session.url) {
      return NextResponse.json({ error: 'session_url_missing' }, { status: 500 });
    }

    return NextResponse.json({ url: session.url, session_id: session.id });
  } catch (err) {
    console.error('[checkout] stripe error', err);
    return NextResponse.json({ error: 'stripe_error' }, { status: 500 });
  }
}
