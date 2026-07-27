// src/components/public/templates/Barber.tsx
import Link from 'next/link';
import type { PublicTemplateProps } from '@/lib/templates/registry';

export function BarberTemplate({ business, products }: PublicTemplateProps) {
  const accentColor = business.primary_color ?? '#C8102E';

  return (
    <div className="min-h-screen bg-neutral-900 text-white">
      <header className="px-4 pt-12 pb-8 text-center">
        {business.logo_url && (
          <img
            src={business.logo_url}
            alt={business.name}
            className="mx-auto h-20 w-20 rounded-full object-cover ring-2 ring-white/20"
          />
        )}
        <h1 className="mt-4 text-3xl font-bold tracking-tight">{business.name}</h1>
        {business.description && (
          <p className="mx-auto mt-2 max-w-md text-sm text-neutral-400">{business.description}</p>
        )}
        <div className="mx-auto mt-4 h-1 w-12 rounded-full" style={{ backgroundColor: accentColor }} />
      </header>

      <main className="mx-auto max-w-2xl px-4 pb-16">
        <h2 className="text-xs font-semibold uppercase tracking-widest text-neutral-500">
          Servicios
        </h2>

        {products.length === 0 ? (
          <div className="mt-3 rounded-2xl bg-neutral-800 p-12 text-center">
            <p className="text-4xl">💈</p>
            <p className="mt-4 text-sm text-neutral-400">Servicios disponibles pronto.</p>
          </div>
        ) : (
          <div className="mt-3 space-y-3">
            {products.map((p) => (
              <ServiceCard
                key={p.id}
                product={p}
                whatsapp={business.whatsapp}
                businessName={business.name}
                accentColor={accentColor}
              />
            ))}
          </div>
        )}
      </main>

      <footer className="py-8 text-center">
        <Link href="/servicios" className="text-xs text-neutral-500 hover:text-neutral-300">
          Powered by <span className="font-semibold">MaalCa</span>
        </Link>
      </footer>
    </div>
  );
}

function ServiceCard({
  product,
  whatsapp,
  businessName,
  accentColor,
}: {
  product: PublicTemplateProps['products'][number];
  whatsapp: string | null | undefined;
  businessName: string;
  accentColor: string;
}) {
  const bookUrl = whatsapp
    ? `https://wa.me/${whatsapp.replace(/\D/g, '')}?text=${encodeURIComponent(
        `Hola ${businessName}, quiero reservar: ${product.name}`
      )}`
    : null;

  return (
    <div className="rounded-2xl bg-neutral-800 p-5">
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1">
          <h3 className="font-semibold">{product.name}</h3>
          {product.description && (
            <p className="mt-1 text-sm text-neutral-400">{product.description}</p>
          )}
          {product.duration_min && (
            <p className="mt-2 text-xs text-neutral-500">⏱ {product.duration_min} min</p>
          )}
        </div>
        <div className="text-right">
          {product.price != null && (
            <p className="text-xl font-bold">${product.price.toFixed(2)}</p>
          )}
        </div>
      </div>
      {bookUrl && (
        <a
          href={bookUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="mt-4 block w-full rounded-full py-2.5 text-center text-sm font-medium text-white transition"
          style={{ backgroundColor: accentColor }}
        >
          Reservar por WhatsApp
        </a>
      )}
    </div>
  );
}
