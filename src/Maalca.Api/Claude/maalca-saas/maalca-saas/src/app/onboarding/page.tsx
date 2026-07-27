// src/app/onboarding/page.tsx
'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { track } from '@/lib/analytics';

const BUSINESS_TYPES = [
  { value: 'restaurant', label: 'Restaurante', emoji: '🍽️' },
  { value: 'barber', label: 'Barbería', emoji: '💈' },
  { value: 'service', label: 'Servicios', emoji: '🛠️' },
  { value: 'retail', label: 'Tienda', emoji: '🛍️' },
] as const;

export default function OnboardingPage() {
  const router = useRouter();
  const [name, setName] = useState('');
  const [type, setType] = useState<typeof BUSINESS_TYPES[number]['value'] | ''>('');
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  const submit = () => {
    if (!name.trim() || !type) return;
    setError(null);

    startTransition(async () => {
      const res = await fetch('/api/onboarding/create', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: name.trim(), business_type: type }),
      });

      const json = await res.json();

      if (!res.ok) {
        setError(json.error || 'Algo salió mal. Intenta de nuevo.');
        return;
      }

      track('onboarding_completed', {
        business_id: json.business.id,
        business_type: type,
      });

      router.push(`/space/${json.business.slug}?new=1`);
    });
  };

  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-neutral-50 px-6">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-bold tracking-tight">
            Crea tu espacio
          </h1>
          <p className="mt-2 text-sm text-neutral-500">
            Dos preguntas y estás en línea.
          </p>
        </div>

        <div className="rounded-2xl border border-neutral-200 bg-white p-8 shadow-sm">
          <div>
            <label className="block text-sm font-medium text-neutral-700">
              Nombre del negocio
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Ej. La Casa del Mofongo"
              maxLength={50}
              className="mt-2 w-full rounded-lg border border-neutral-200 px-3 py-2.5 text-sm focus:border-neutral-400 focus:outline-none"
            />
          </div>

          <div className="mt-6">
            <label className="block text-sm font-medium text-neutral-700">
              ¿Qué tipo de negocio?
            </label>
            <div className="mt-2 grid grid-cols-2 gap-2">
              {BUSINESS_TYPES.map((t) => (
                <button
                  key={t.value}
                  type="button"
                  onClick={() => setType(t.value)}
                  className={`flex items-center gap-2 rounded-lg border px-3 py-3 text-sm transition ${
                    type === t.value
                      ? 'border-[#C8102E] bg-[#C8102E]/5 text-[#C8102E]'
                      : 'border-neutral-200 hover:border-neutral-300'
                  }`}
                >
                  <span className="text-lg">{t.emoji}</span>
                  <span className="font-medium">{t.label}</span>
                </button>
              ))}
            </div>
          </div>

          {error && (
            <p className="mt-4 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>
          )}

          <button
            onClick={submit}
            disabled={!name.trim() || !type || pending}
            className="mt-8 w-full rounded-full bg-[#C8102E] py-3 text-sm font-medium text-white transition hover:bg-[#A00D26] disabled:cursor-not-allowed disabled:opacity-50"
          >
            {pending ? 'Creando...' : 'Crear mi espacio'}
          </button>
        </div>
      </div>
    </div>
  );
}
