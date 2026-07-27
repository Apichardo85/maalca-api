// src/app/servicios/page.tsx
'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { track } from '@/lib/analytics';

export default function ServiciosPage() {
  const router = useRouter();

  const handleStartFree = () => {
    track('click_start_free', { source: 'landing_hero' });
    router.push('/login');
  };

  return (
    <div className="min-h-screen bg-white text-neutral-900">
      {/* Nav */}
      <nav className="border-b border-neutral-100">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <Link href="/" className="text-lg font-semibold tracking-tight">
            MaalCa
          </Link>
          <div className="flex items-center gap-3">
            <Link
              href="/login"
              className="text-sm text-neutral-600 hover:text-neutral-900"
            >
              Entrar
            </Link>
            <button
              onClick={handleStartFree}
              className="rounded-full bg-[#C8102E] px-4 py-2 text-sm font-medium text-white transition hover:bg-[#A00D26]"
            >
              Empezar gratis
            </button>
          </div>
        </div>
      </nav>

      {/* Hero */}
      <section className="mx-auto max-w-4xl px-6 pt-20 pb-16 text-center sm:pt-32">
        <h1 className="text-4xl font-bold tracking-tight sm:text-6xl">
          Tu negocio en línea
          <br />
          <span className="text-[#C8102E]">en minutos.</span>
        </h1>
        <p className="mx-auto mt-6 max-w-2xl text-lg text-neutral-600 sm:text-xl">
          Página web, menú, pedidos y clientes — sin dolor de cabeza.
        </p>
        <div className="mt-10 flex flex-col items-center gap-3">
          <button
            onClick={handleStartFree}
            className="rounded-full bg-[#C8102E] px-8 py-4 text-base font-medium text-white shadow-lg shadow-[#C8102E]/20 transition hover:bg-[#A00D26]"
          >
            Empezar gratis
          </button>
          <p className="text-sm text-neutral-500">Toma menos de 2 minutos</p>
        </div>
      </section>

      {/* Free plan card */}
      <section className="mx-auto max-w-md px-6 pb-20">
        <div className="rounded-2xl border border-neutral-200 bg-white p-8 shadow-sm">
          <div className="flex items-baseline justify-between">
            <h2 className="text-xl font-semibold">Plan Gratis</h2>
            <div>
              <span className="text-3xl font-bold">$0</span>
              <span className="text-neutral-500">/mes</span>
            </div>
          </div>
          <ul className="mt-6 space-y-3 text-sm">
            {[
              'Página web básica',
              'Menú o servicios',
              'Código QR',
              'Integración con WhatsApp',
              'Hasta 10 productos',
            ].map((feature) => (
              <li key={feature} className="flex items-start gap-2">
                <Check />
                <span className="text-neutral-700">{feature}</span>
              </li>
            ))}
          </ul>
          <button
            onClick={handleStartFree}
            className="mt-8 w-full rounded-full bg-neutral-900 py-3 text-sm font-medium text-white transition hover:bg-neutral-800"
          >
            Empezar gratis
          </button>
          <p className="mt-3 text-center text-xs text-neutral-500">
            Sin tarjeta de crédito · Sin contratos
          </p>
        </div>
      </section>

      {/* Trust */}
      <section className="border-t border-neutral-100 bg-neutral-50 py-12">
        <div className="mx-auto max-w-4xl px-6 text-center">
          <p className="text-sm uppercase tracking-wider text-neutral-500">
            Negocios que ya están en MaalCa
          </p>
          <div className="mt-6 flex flex-wrap items-center justify-center gap-x-12 gap-y-4 text-neutral-400">
            <span className="text-lg font-medium">The Little Dominican</span>
            <span className="text-lg font-medium">Pegote</span>
            <span className="text-lg font-medium">+ más</span>
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t border-neutral-100 py-8 text-center text-sm text-neutral-500">
        © {new Date().getFullYear()} MaalCa · Santo Domingo, DR
      </footer>
    </div>
  );
}

function Check() {
  return (
    <svg
      className="mt-0.5 h-4 w-4 flex-shrink-0 text-[#C8102E]"
      fill="none"
      stroke="currentColor"
      viewBox="0 0 24 24"
    >
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={3} d="M5 13l4 4L19 7" />
    </svg>
  );
}
