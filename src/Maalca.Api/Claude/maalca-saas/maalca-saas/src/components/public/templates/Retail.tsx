// src/components/public/templates/Retail.tsx
import Link from 'next/link';
import type { PublicTemplateProps } from '@/lib/templates/registry';

export function RetailTemplate({ business, products }: PublicTemplateProps) {
  const accent = business.primary_color ?? '#C8102E';

  return (
    <div className="min-h-screen bg-white">
      <header className="border-b border-neutral-100 px-4 pt-12 pb-8 text-center">
        {business.logo_url && (
          <img src={business.logo_url} alt={business.name} className="mx-auto h-20 w-20 rounded-full object-cover" />
        )}
        <h1 className="mt-4 text-3xl font-bold tracking-tight">{business.name}</h1>
        {business.description && (
          <p className="mx-auto mt-2 max-w-md text-sm text-neutral-600">{business.description}</p>
        )}
      </header>

      <main className="mx-auto max-w-5xl px-4 py-10">
        {products.length === 0 ? (
          <div className="rounded-2xl border border-neutral-200 p-12 text-center">
            <p className="text-4xl">🛍️</p>
            <p className="mt-4 text-sm text-neutral-500">Productos disponibles pronto.</p>
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
            {products.map((p) => (
              <div key={p.id} className="group">
                <div className="aspect-square overflow-hidden rounded-xl bg-neutral-100">
                  {p.image_url ? (
                    <img
                      src={p.image_url}
                      alt={p.name}
                      className="h-full w-full object-cover transition group-hover:scale-105"
                    />
                  ) : (
                    <div className="flex h-full items-center justify-center text-3xl text-neutral-300">
                      🛍️
                    </div>
                  )}
                </div>
                <p className="mt-2 text-sm font-medium line-clamp-1">{p.name}</p>
                {p.price != null && (
                  <p className="text-sm text-neutral-600">${p.price.toFixed(2)}</p>
                )}
                {business.whatsapp && (
                  <a
                    href={`https://wa.me/${business.whatsapp.replace(/\D/g, '')}?text=${encodeURIComponent(
                      `Hola ${business.name}, me interesa: ${p.name}`
                    )}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="mt-2 block rounded-full py-1.5 text-center text-xs font-medium text-white"
                    style={{ backgroundColor: accent }}
                  >
                    Comprar
                  </a>
                )}
              </div>
            ))}
          </div>
        )}
      </main>

      <footer className="py-8 text-center">
        <Link href="/servicios" className="text-xs text-neutral-400 hover:text-neutral-600">
          Powered by <span className="font-semibold">MaalCa</span>
        </Link>
      </footer>
    </div>
  );
}
