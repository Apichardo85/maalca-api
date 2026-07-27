// src/app/[slug]/page.tsx
// Public-facing page. NO auth required.
// IMPORTANT: This catch-all sits at root, so reserved slugs must NEVER reach here.
// Reserved-slug enforcement happens at signup (DB CHECK + reserved_slugs table).

import { notFound } from 'next/navigation';
import type { Metadata } from 'next';
import { supabaseServer } from '@/lib/supabase/server';
import { TEMPLATES, type BusinessType } from '@/lib/templates/registry';

interface PageProps {
  params: Promise<{ slug: string }>;
}

// Reserved slugs that should never resolve to a business — fall through to 404.
// This is a defense-in-depth check; the real enforcement is in signup.
const RESERVED = new Set([
  'servicios', 'login', 'signup', 'register', 'onboarding', 'space',
  'dashboard', 'admin', 'api', 'auth', 'app', 'www',
  'about', 'contact', 'contacto', 'pricing', 'terms', 'privacy', 'legal',
  'help', 'blog', 'docs', '_next', 'static', 'public', 'assets', 'images',
  'favicon.ico', 'robots.txt', 'sitemap.xml',
]);

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  if (RESERVED.has(slug)) return { title: 'MaalCa' };

  const supabase = supabaseServer();
  const { data: business } = await supabase
    .from('businesses')
    .select('name, description')
    .eq('slug', slug)
    .eq('published', true)
    .maybeSingle();

  if (!business) return { title: 'MaalCa' };

  return {
    title: `${business.name} | MaalCa`,
    description: business.description ?? `Visita ${business.name} en MaalCa`,
    openGraph: {
      title: business.name,
      description: business.description ?? undefined,
    },
  };
}

export default async function PublicBusinessPage({ params }: PageProps) {
  const { slug } = await params;
  if (RESERVED.has(slug)) notFound();

  const supabase = supabaseServer();

  const { data: business } = await supabase
    .from('businesses')
    .select('id, slug, name, description, business_type, logo_url, primary_color, whatsapp')
    .eq('slug', slug)
    .eq('published', true)
    .maybeSingle();

  if (!business) notFound();

  const { data: products } = await supabase
    .from('products')
    .select('id, name, description, price, category, image_url, duration_min')
    .eq('business_id', business.id)
    .eq('active', true)
    .order('sort_order', { ascending: true })
    .order('created_at', { ascending: true });

  const Template = TEMPLATES[business.business_type as BusinessType];

  if (!Template) {
    // Unknown business_type fallback
    return notFound();
  }

  return (
    <Template
      business={business as any}
      products={products ?? []}
    />
  );
}
