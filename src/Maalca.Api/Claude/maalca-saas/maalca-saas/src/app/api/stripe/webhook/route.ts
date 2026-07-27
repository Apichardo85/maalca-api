// src/app/api/stripe/webhook/route.ts
// IMPORTANT: This route uses raw body for signature verification.
// Test locally with: stripe listen --forward-to localhost:3000/api/stripe/webhook

import { NextResponse } from 'next/server';
import { createClient } from '@supabase/supabase-js';
import { stripe, STRIPE_CONFIG } from '@/lib/stripe';
import type Stripe from 'stripe';

// Service-role client (bypasses RLS — required for webhook updates)
function adminClient() {
  return createClient(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.SUPABASE_SERVICE_ROLE_KEY!,
    { auth: { persistSession: false } }
  );
}

export async function POST(req: Request) {
  const sig = req.headers.get('stripe-signature');
  if (!sig) {
    return NextResponse.json({ error: 'missing_signature' }, { status: 400 });
  }

  const body = await req.text();

  let event: Stripe.Event;
  try {
    event = stripe.webhooks.constructEvent(body, sig, STRIPE_CONFIG.webhookSecret);
  } catch (err) {
    console.error('[webhook] signature verification failed', err);
    return NextResponse.json({ error: 'invalid_signature' }, { status: 400 });
  }

  const supabase = adminClient();

  try {
    switch (event.type) {
      case 'checkout.session.completed': {
        const session = event.data.object as Stripe.Checkout.Session;
        const businessId = session.metadata?.business_id;
        const userId = session.metadata?.user_id;
        const customerId = session.customer as string | null;
        const subscriptionId = session.subscription as string | null;

        if (!businessId) {
          console.warn('[webhook] checkout.session.completed without business_id');
          break;
        }

        await supabase
          .from('businesses')
          .update({
            plan: 'entrepreneur',
            plan_status: 'active',
            stripe_customer_id: customerId,
            stripe_subscription_id: subscriptionId,
          })
          .eq('id', businessId);

        // Server-side analytics
        await supabase.from('analytics_events').insert({
          user_id: userId ?? null,
          business_id: businessId,
          event_name: 'upgrade_completed',
          properties: {
            session_id: session.id,
            amount_total: session.amount_total,
          },
        });
        break;
      }

      case 'customer.subscription.updated': {
        const sub = event.data.object as Stripe.Subscription;
        const businessId = sub.metadata?.business_id;
        if (!businessId) break;

        const status = mapStripeStatus(sub.status);
        const plan = sub.status === 'active' || sub.status === 'trialing' ? 'entrepreneur' : 'free';

        await supabase
          .from('businesses')
          .update({ plan, plan_status: status })
          .eq('id', businessId);
        break;
      }

      case 'customer.subscription.deleted': {
        const sub = event.data.object as Stripe.Subscription;
        const businessId = sub.metadata?.business_id;
        if (!businessId) break;

        await supabase
          .from('businesses')
          .update({ plan: 'free', plan_status: 'canceled' })
          .eq('id', businessId);
        break;
      }

      default:
        // Ignore other events; log for visibility
        console.log('[webhook] unhandled event', event.type);
    }
  } catch (err) {
    console.error('[webhook] handler error', err);
    return NextResponse.json({ error: 'handler_failed' }, { status: 500 });
  }

  return NextResponse.json({ received: true });
}

function mapStripeStatus(s: Stripe.Subscription.Status): 'active' | 'past_due' | 'canceled' {
  if (s === 'active' || s === 'trialing') return 'active';
  if (s === 'past_due' || s === 'unpaid') return 'past_due';
  return 'canceled';
}
