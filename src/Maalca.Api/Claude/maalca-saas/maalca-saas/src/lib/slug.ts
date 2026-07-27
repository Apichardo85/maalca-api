// src/lib/slug.ts
import type { SupabaseClient } from '@supabase/supabase-js';

/**
 * Convert a business name into a URL-safe slug.
 * "El Pegote Restaurant!" -> "el-pegote-restaurant"
 */
export function slugify(input: string): string {
  return input
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')      // strip accents
    .replace(/[^a-z0-9\s-]/g, '')          // strip non-alphanumeric
    .trim()
    .replace(/\s+/g, '-')                   // spaces -> dashes
    .replace(/-+/g, '-')                    // collapse dashes
    .replace(/^-|-$/g, '')                  // trim dashes
    .slice(0, 50);
}

/**
 * Generate a unique slug, checking against `businesses` and `reserved_slugs`.
 * Appends -2, -3, ... on collision.
 */
export async function generateUniqueSlug(
  supabase: SupabaseClient,
  baseName: string
): Promise<string> {
  const base = slugify(baseName) || 'mi-negocio';
  let candidate = base;
  let suffix = 1;

  while (suffix < 100) {
    const [reserved, existing] = await Promise.all([
      supabase.from('reserved_slugs').select('slug').eq('slug', candidate).maybeSingle(),
      supabase.from('businesses').select('id').eq('slug', candidate).maybeSingle(),
    ]);

    if (!reserved.data && !existing.data) {
      return candidate;
    }

    suffix += 1;
    candidate = `${base}-${suffix}`;
  }

  // Fallback: timestamp suffix
  return `${base}-${Date.now()}`;
}
