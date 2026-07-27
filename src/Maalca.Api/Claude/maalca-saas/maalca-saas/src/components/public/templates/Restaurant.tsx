// src/components/public/templates/Restaurant.tsx
import Link from 'next/link';
import type { PublicTemplateProps } from '@/lib/templates/registry';

export function RestaurantTemplate({ business, products }: PublicTemplateProps) {
  const grouped = groupByCategory(products);
  const accentColor = business.primary_color ?? '#C8102E';

  return (
    <div className="min-h-screen bg-neutral-50">
      <Header business={business} accentColor={accentColor} />

      <main className="mx-auto max-w-2xl px-4 py-8">
        {products.length === 0 ? (
          <EmptyState />
        ) : (
          <div className="space-y-10">
            {Object.entries(grouped).map(([category, items]) => (
              <section key={category}>
                <h2 className="text-xs font-semibold uppercase tracking-widest text-neutral-500">
                  {category}
                </h2>
                <div className="mt-3 divide-y divide-neutral-100 rounded-2xl bg-white shadow-sm">
                  {items.map((p) => (
                    <ProductRow
                      key={p.id}
                      product={p}
                      whatsapp={business.whatsapp}
                      businessName={business.name}
                      accentColor={accentColor}
                    />
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </main>

      <PoweredBy />
      {business.whatsapp && <WhatsAppFAB whatsapp={business.whatsapp} accentColor={accentColor} />}
    </div>
  );
}

function Header({ business, accentColor }: { business: PublicTemplateProps['business']; accentColor: string }) {
  return (
    <header className="px-4 pt-12 pb-8 text-center">
      {business.logo_url && (
        <img
          src={business.logo_url}
          alt={business.name}
          className="mx-auto h-20 w-20 rounded-full object-cover shadow-md"
        />
      )}
      <h1 className="mt-4 text-3xl font-bold tracking-tight">{business.name}</h1>
      {business.description && (
        <p className="mx-auto mt-2 max-w-md text-sm text-neutral-600">{business.description}</p>
      )}
      <div
        className="mx-auto mt-4 h-1 w-12 rounded-full"
        style={{ backgroundColor: accentColor }}
      />
    </header>
  );
}

function ProductRow({
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
  const orderUrl = whatsapp ? buildWhatsAppUrl(whatsapp, businessName, product.name) : null;

  return (
    <div className="flex items-center gap-4 p-4">
      {product.image_url && (
        <img
          src={product.image_url}
          alt={product.name}
          className="h-16 w-16 flex-shrink-0 rounded-lg object-cover"
        />
      )}
      <div className="flex-1 min-w-0">
        <p className="font-medium">{product.name}</p>
        {product.description && (
          <p className="mt-0.5 text-sm text-neutral-500 line-clamp-2">{product.description}</p>
        )}
      </div>
      <div className="flex flex-col items-end gap-2">
        {product.price != null && (
          <span className="text-sm font-semibold">
            ${product.price.toFixed(2)}
          </span>
        )}
        {orderUrl && (
          <a
            href={orderUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="rounded-full px-3 py-1 text-xs font-medium text-white"
            style={{ backgroundColor: accentColor }}
          >
            Pedir
          </a>
        )}
      </div>
    </div>
  );
}

function EmptyState() {
  return (
    <div className="rounded-2xl bg-white p-12 text-center">
      <p className="text-4xl">🍽️</p>
      <p className="mt-4 text-sm text-neutral-500">El menú estará disponible pronto.</p>
    </div>
  );
}

function WhatsAppFAB({ whatsapp, accentColor }: { whatsapp: string; accentColor: string }) {
  return (
    <a
      href={`https://wa.me/${whatsapp.replace(/\D/g, '')}`}
      target="_blank"
      rel="noopener noreferrer"
      className="fixed bottom-6 right-6 flex h-14 w-14 items-center justify-center rounded-full text-white shadow-lg"
      style={{ backgroundColor: '#25D366' }}
      aria-label="WhatsApp"
    >
      <svg className="h-7 w-7" viewBox="0 0 24 24" fill="currentColor">
        <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413z" />
      </svg>
    </a>
  );
}

function PoweredBy() {
  return (
    <footer className="py-8 text-center">
      <Link href="/servicios" className="text-xs text-neutral-400 hover:text-neutral-600">
        Powered by <span className="font-semibold">MaalCa</span>
      </Link>
    </footer>
  );
}

// Helpers
function groupByCategory(products: PublicTemplateProps['products']) {
  return products.reduce<Record<string, PublicTemplateProps['products']>>((acc, p) => {
    const key = p.category || 'Menú';
    if (!acc[key]) acc[key] = [];
    acc[key].push(p);
    return acc;
  }, {});
}

function buildWhatsAppUrl(phone: string, businessName: string, productName: string): string {
  const cleaned = phone.replace(/\D/g, '');
  const msg = encodeURIComponent(`Hola ${businessName}, quiero pedir: ${productName}`);
  return `https://wa.me/${cleaned}?text=${msg}`;
}
