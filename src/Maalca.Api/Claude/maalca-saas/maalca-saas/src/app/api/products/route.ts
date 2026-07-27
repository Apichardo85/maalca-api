// src/app/api/products/route.ts
import { NextResponse } from 'next/server';
import { supabaseServer } from '@/lib/supabase/server';
import { canAddProduct, type Plan } from '@/lib/plan-limits';

export async function POST(req: Request) {
  const supabase = supabaseServer();

  const { data: { user } } = await supabase.auth.getUser();
  if (!user) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });

  let body: {
    business_id?: string;
    name?: string;
    description?: string;
    price?: number;
    category?: string;
    image_url?: string;
    duration_min?: number;
  };
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: 'invalid_json' }, { status: 400 });
  }

  if (!body.business_id || !body.name?.trim()) {
    return NextResponse.json({ error: 'missing_fields' }, { status: 400 });
  }

  // Verify ownership + get plan
  const { data: business } = await supabase
    .from('businesses')
    .select('id, plan')
    .eq('id', body.business_id)
    .eq('owner_id', user.id)
    .maybeSingle();

  if (!business) {
    return NextResponse.json({ error: 'business_not_found' }, { status: 404 });
  }

  // Count current products
  const { count, error: countError } = await supabase
    .from('products')
    .select('id', { count: 'exact', head: true })
    .eq('business_id', business.id);

  if (countError) {
    return NextResponse.json({ error: 'count_failed' }, { status: 500 });
  }

  const currentCount = count ?? 0;

  // ENFORCE PLAN LIMIT — this is the conversion trigger
  if (!canAddProduct(business.plan as Plan, currentCount)) {
    return NextResponse.json(
      {
        error: 'plan_limit_reached',
        upgrade_required: true,
        current_count: currentCount,
        limit: 10,
      },
      { status: 402 } // Payment Required
    );
  }

  // Insert
  const { data: product, error: insertError } = await supabase
    .from('products')
    .insert({
      business_id: business.id,
      name: body.name.trim(),
      description: body.description ?? null,
      price: body.price ?? null,
      category: body.category ?? null,
      image_url: body.image_url ?? null,
      duration_min: body.duration_min ?? null,
      sort_order: currentCount,
    })
    .select()
    .single();

  if (insertError || !product) {
    return NextResponse.json({ error: 'insert_failed' }, { status: 500 });
  }

  // Update onboarding progress (mark first_product_added if this is the first)
  if (currentCount === 0) {
    await supabase
      .from('onboarding_progress')
      .update({ first_product_added: true })
      .eq('business_id', business.id);

    // Track first_product_created server-side
    await supabase.from('analytics_events').insert({
      user_id: user.id,
      business_id: business.id,
      event_name: 'first_product_created',
      properties: { product_id: product.id },
    });
  }

  return NextResponse.json({ product });
}

export async function GET(req: Request) {
  const supabase = supabaseServer();
  const { data: { user } } = await supabase.auth.getUser();
  if (!user) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });

  const { searchParams } = new URL(req.url);
  const businessId = searchParams.get('business_id');
  if (!businessId) return NextResponse.json({ error: 'missing_business_id' }, { status: 400 });

  const { data: business } = await supabase
    .from('businesses')
    .select('id')
    .eq('id', businessId)
    .eq('owner_id', user.id)
    .maybeSingle();

  if (!business) return NextResponse.json({ error: 'not_found' }, { status: 404 });

  const { data: products } = await supabase
    .from('products')
    .select('*')
    .eq('business_id', business.id)
    .order('sort_order');

  return NextResponse.json({ products: products ?? [] });
}
