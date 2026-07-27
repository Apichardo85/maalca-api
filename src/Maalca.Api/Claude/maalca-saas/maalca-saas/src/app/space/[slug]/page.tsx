// src/app/space/[slug]/page.tsx
import { notFound, redirect } from 'next/navigation';
import { supabaseServer } from '@/lib/supabase/server';
import { SpaceDashboard } from '@/components/space/SpaceDashboard';

interface PageProps {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ new?: string; upgraded?: string }>;
}

export default async function SpacePage({ params, searchParams }: PageProps) {
  const { slug } = await params;
  const sp = await searchParams;
  const supabase = supabaseServer();

  const { data: { user } } = await supabase.auth.getUser();
  if (!user) redirect('/login');

  const { data: business } = await supabase
    .from('businesses')
    .select('id, slug, name, business_type, plan, plan_status, whatsapp, primary_color')
    .eq('slug', slug)
    .eq('owner_id', user.id)
    .maybeSingle();

  if (!business) notFound();

  const [{ data: products }, { data: progress }] = await Promise.all([
    supabase
      .from('products')
      .select('id, name, active')
      .eq('business_id', business.id),
    supabase
      .from('onboarding_progress')
      .select('first_product_added, whatsapp_configured, link_shared')
      .eq('business_id', business.id)
      .maybeSingle(),
  ]);

  const productCount = products?.length ?? 0;

  return (
    <SpaceDashboard
      business={business}
      productCount={productCount}
      progress={progress ?? {
        first_product_added: false,
        whatsapp_configured: false,
        link_shared: false,
      }}
      isNew={sp.new === '1'}
      justUpgraded={sp.upgraded === '1'}
      publicUrl={`${process.env.NEXT_PUBLIC_APP_URL ?? ''}/${business.slug}`}
    />
  );
}
