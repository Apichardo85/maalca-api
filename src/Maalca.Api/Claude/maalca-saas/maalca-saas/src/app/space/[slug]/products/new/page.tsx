// src/app/space/[slug]/products/new/page.tsx
'use client';

import { useState, useEffect, useTransition } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { createBrowserClient } from '@supabase/ssr';
import { UpgradeModal } from '@/components/space/UpgradeModal';

export default function NewProductPage() {
  const params = useParams<{ slug: string }>();
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  const [businessId, setBusinessId] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [price, setPrice] = useState('');
  const [category, setCategory] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [showUpgrade, setShowUpgrade] = useState(false);

  // Fetch business id on mount
  useEffect(() => {
    const supabase = createBrowserClient(
      process.env.NEXT_PUBLIC_SUPABASE_URL!,
      process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!
    );
    supabase
      .from('businesses')
      .select('id')
      .eq('slug', params.slug)
      .maybeSingle()
      .then(({ data }) => {
        if (data) setBusinessId(data.id);
      });
  }, [params.slug]);

  const submit = () => {
    if (!businessId || !name.trim()) return;
    setError(null);

    startTransition(async () => {
      const res = await fetch('/api/products', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          business_id: businessId,
          name: name.trim(),
          description: description.trim() || undefined,
          price: price ? parseFloat(price) : undefined,
          category: category.trim() || undefined,
        }),
      });

      const json = await res.json();

      if (res.status === 402 && json.upgrade_required) {
        setShowUpgrade(true);
        return;
      }

      if (!res.ok) {
        setError(json.error || 'Algo salió mal.');
        return;
      }

      router.push(`/space/${params.slug}`);
      router.refresh();
    });
  };

  return (
    <div className="min-h-screen bg-neutral-50">
      <nav className="border-b border-neutral-200 bg-white">
        <div className="mx-auto flex max-w-3xl items-center px-6 py-4">
          <button
            onClick={() => router.back()}
            className="text-sm text-neutral-600 hover:text-neutral-900"
          >
            ← Volver
          </button>
        </div>
      </nav>

      <main className="mx-auto max-w-md px-6 py-8">
        <h1 className="text-2xl font-bold tracking-tight">Nuevo producto</h1>

        <div className="mt-6 space-y-4">
          <Field label="Nombre *">
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              maxLength={100}
              className="w-full rounded-lg border border-neutral-200 px-3 py-2.5 text-sm focus:border-neutral-400 focus:outline-none"
            />
          </Field>

          <Field label="Descripción">
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              maxLength={300}
              rows={3}
              className="w-full rounded-lg border border-neutral-200 px-3 py-2.5 text-sm focus:border-neutral-400 focus:outline-none"
            />
          </Field>

          <Field label="Precio (USD)">
            <input
              type="number"
              step="0.01"
              min="0"
              value={price}
              onChange={(e) => setPrice(e.target.value)}
              className="w-full rounded-lg border border-neutral-200 px-3 py-2.5 text-sm focus:border-neutral-400 focus:outline-none"
            />
          </Field>

          <Field label="Categoría">
            <input
              type="text"
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              placeholder="Ej. Entradas, Cortes, Servicios"
              maxLength={50}
              className="w-full rounded-lg border border-neutral-200 px-3 py-2.5 text-sm focus:border-neutral-400 focus:outline-none"
            />
          </Field>
        </div>

        {error && (
          <p className="mt-4 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>
        )}

        <button
          onClick={submit}
          disabled={!name.trim() || pending}
          className="mt-8 w-full rounded-full bg-[#C8102E] py-3 text-sm font-medium text-white transition hover:bg-[#A00D26] disabled:opacity-50"
        >
          {pending ? 'Guardando...' : 'Agregar producto'}
        </button>
      </main>

      {showUpgrade && businessId && (
        <UpgradeModal
          businessId={businessId}
          businessSlug={params.slug}
          onClose={() => setShowUpgrade(false)}
        />
      )}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="block text-sm font-medium text-neutral-700">{label}</label>
      <div className="mt-1.5">{children}</div>
    </div>
  );
}
