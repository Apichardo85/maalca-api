// src/app/api/qr/[slug]/route.ts
import { NextResponse } from 'next/server';
import { supabaseServer } from '@/lib/supabase/server';
import { generateQRPng } from '@/lib/qr';

interface RouteContext {
  params: Promise<{ slug: string }>;
}

export async function GET(_req: Request, ctx: RouteContext) {
  const { slug } = await ctx.params;

  const supabase = supabaseServer();
  const { data: business } = await supabase
    .from('businesses')
    .select('slug')
    .eq('slug', slug)
    .eq('published', true)
    .maybeSingle();

  if (!business) {
    return NextResponse.json({ error: 'not_found' }, { status: 404 });
  }

  const baseUrl = process.env.NEXT_PUBLIC_APP_URL ?? 'https://maalca.com';
  const url = `${baseUrl}/${slug}`;
  const png = await generateQRPng(url, 512);

  return new NextResponse(png, {
    status: 200,
    headers: {
      'Content-Type': 'image/png',
      'Cache-Control': 'public, max-age=86400, immutable',
    },
  });
}
