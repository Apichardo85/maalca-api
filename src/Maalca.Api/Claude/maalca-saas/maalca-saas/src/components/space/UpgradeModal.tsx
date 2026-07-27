// src/components/space/UpgradeModal.tsx
'use client';

import { useState } from 'react';
import { track } from '@/lib/analytics';
import { ENTREPRENEUR_PRICE_USD } from '@/lib/plan-limits';

interface Props {
  businessId: string;
  businessSlug: string;
  onClose: () => void;
}

const FEATURES = [
  'Productos ilimitados',
  'Pedidos en línea',
  'Pagos integrados',
  'Analytics avanzado',
  'Soporte prioritario',
];

export function UpgradeModal({ businessId, businessSlug, onClose }: Props) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleUpgrade = async () => {
    setLoading(true);
    setError(null);
    track('upgrade_clicked', { source: 'modal_cta', business_id: businessId });

    try {
      const res = await fetch('/api/checkout', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ business_id: businessId, return_slug: businessSlug }),
      });

      const json = await res.json();

      if (!res.ok || !json.url) {
        setError(json.error || 'No se pudo iniciar el checkout.');
        setLoading(false);
        return;
      }

      window.location.href = json.url;
    } catch {
      setError('Error de red. Intenta de nuevo.');
      setLoading(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4"
      onClick={onClose}
    >
      <div
        className="w-full max-w-md rounded-2xl bg-white p-8 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="text-center">
          <p className="text-3xl">🔥</p>
          <h2 className="mt-2 text-2xl font-bold tracking-tight">Estás creciendo</h2>
          <p className="mt-2 text-sm text-neutral-600">
            Desbloquea el plan Emprendedor
          </p>
        </div>

        <ul className="mt-6 space-y-3">
          {FEATURES.map((f) => (
            <li key={f} className="flex items-start gap-2 text-sm">
              <svg className="mt-0.5 h-4 w-4 flex-shrink-0 text-[#C8102E]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={3} d="M5 13l4 4L19 7" />
              </svg>
              <span className="text-neutral-700">{f}</span>
            </li>
          ))}
        </ul>

        <div className="mt-6 rounded-xl bg-neutral-50 p-4 text-center">
          <p className="text-xs uppercase tracking-wider text-neutral-500">Emprendedor</p>
          <p className="mt-1">
            <span className="text-3xl font-bold">${ENTREPRENEUR_PRICE_USD}</span>
            <span className="text-neutral-500">/mes</span>
          </p>
        </div>

        {error && (
          <p className="mt-4 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>
        )}

        <button
          onClick={handleUpgrade}
          disabled={loading}
          className="mt-6 w-full rounded-full bg-[#C8102E] py-3 text-sm font-medium text-white transition hover:bg-[#A00D26] disabled:opacity-50"
        >
          {loading ? 'Redirigiendo...' : `Activar Emprendedor — $${ENTREPRENEUR_PRICE_USD}/mes`}
        </button>

        <button
          onClick={onClose}
          className="mt-3 w-full text-center text-xs text-neutral-500 hover:text-neutral-700"
        >
          Más tarde
        </button>
      </div>
    </div>
  );
}
