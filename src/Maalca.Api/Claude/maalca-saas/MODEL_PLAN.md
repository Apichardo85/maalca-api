# MaalCa SaaS — Modelo de Dominio y Plan de Onboarding (v2)

**Documento de diseño previo a implementación.** Léelo entero antes de tocar código. Este documento sustituye/complementa `IMPLEMENTATION.md` en las partes que tocan: modelo de datos, multi-negocio, catálogo por tipo, y diferenciación de plantillas por plan.

---

## 0. Decisiones tomadas (lock-in)

| Decisión | Choice | Impacto |
|---|---|---|
| Modelo de catálogo | Tabla base `catalog_items` + tablas hijas por tipo | Refactor completo de la capa de datos del blueprint actual |
| Multi-negocio | Permitido solo en plan `entrepreneur` | Cambia onboarding (selector de negocio activo), políticas de downgrade |
| Plantillas por plan | Misma plantilla por tipo, secciones condicionales según plan | Plantillas son `(business_type, plan) → secciones activas`, no 8 archivos |

Estas tres decisiones **no estaban contempladas en el blueprint original** y son las que justifican este documento.

---

## 1. Modelo de dominio

### 1.1 Entidades

```
auth.users (Supabase)
    │
    │ 1:N (entrepreneur) | 1:1 (free)
    ▼
businesses
    ├── business_type:  restaurant | barber | service | retail
    ├── plan:           free | entrepreneur
    ├── plan_status:    active | past_due | canceled
    └── slug, name, branding, contact, …
        │
        │ 1:N
        ▼
    catalog_items                         ← tabla base, polimórfica
        ├── id, business_id, name, description, price, image_url
        ├── category, sort_order, active
        ├── item_type: 'menu' | 'service' | 'retail' | 'generic'
        └── created_at, updated_at
              │
              │ 1:1 (según item_type)
              ▼
    ┌─────────────┴─────────────┬────────────────┬──────────────┐
    ▼                           ▼                ▼              ▼
menu_items              service_items     retail_items     (generic = none)
├─ catalog_item_id      ├─ catalog_item_id ├─ catalog_item_id
├─ modifiers (JSONB)    ├─ duration_min   ├─ stock
├─ allergens (TEXT[])   ├─ buffer_min     ├─ sku
├─ spicy_level INT      ├─ requires_book  ├─ variants (JSONB)
└─ available_from/to    └─ staff_required └─ weight_grams
```

**Justificación de tabla base + hijas (vs JSONB único):**
- Queries públicas (`/[slug]`) leen siempre `catalog_items` + LEFT JOIN a la tabla hija que aplique → un solo path de datos por tipo, indexable.
- Validación a nivel DB: un `menu_item` no puede tener `stock` por error de código.
- Migraciones por tipo no afectan a otros tipos. Si mañana retail necesita "ofertas de temporada", agregás columna en `retail_items` sin tocar nada más.
- Costo: 4 INSERTs en cascada en lugar de 1, pero lo manejamos con transacción en el API.

### 1.2 Cambios sobre el schema actual

El blueprint tiene una tabla `products` única (`migrations/001_maalca_saas.sql` líneas 48-67). **No la borramos** — la renombramos y la convertimos en la tabla base. Esto se hace en una migración nueva (`003_catalog_split.sql`) que:

1. `ALTER TABLE products RENAME TO catalog_items`
2. Agrega columna `item_type TEXT NOT NULL DEFAULT 'generic'`
3. Crea las 3 tablas hijas (`menu_items`, `service_items`, `retail_items`)
4. **Backfill:** para cada `business`, mira su `business_type` y crea las filas hijas correspondientes a sus items existentes
5. Agrega CHECK constraint: `item_type` debe coincidir con `business_type` del negocio padre (vía trigger, no FK)

**Backward compat:** la tabla `catalog_items` mantiene los mismos campos que `products` tenía. Los APIs viejos siguen funcionando hasta migrar.

### 1.3 Multi-negocio: regla de plan

```
free:          1 negocio, hard cap.   Intentar crear el #2 → 402 + UpgradeModal
entrepreneur:  N negocios ilimitado.  Selector "negocio activo" en /space layout
```

**Política de downgrade** (entrepreneur → free, por cancelación o impago):
- El usuario tiene M negocios. Al downgrade:
  - Mantenemos todos los registros (no borramos).
  - **Marcamos M-1 como `published=false` y `plan_status='downgraded_locked'`**.
  - El primero por `created_at ASC` se queda activo en plan `free`.
  - El usuario ve un banner: "Tienes N-1 negocios bloqueados. Reactiva Emprendedor para recuperarlos."
  - Las páginas públicas de los bloqueados devuelven 404 (no 410, para no penalizar SEO si vuelve).

Esto se enforce en el webhook de Stripe (`customer.subscription.deleted`).

### 1.4 Reglas de catálogo por tipo

| Tipo | Tabla hija | Campos clave | Categorías sugeridas | Carrito/Reserva |
|---|---|---|---|---|
| `restaurant` | `menu_items` | `modifiers`, `allergens`, `spicy_level`, `available_from/to` | Entradas / Principales / Bebidas / Postres | "Pedir por WhatsApp" (free) / Pedido en línea (entrepreneur) |
| `barber` | `service_items` | `duration_min`, `buffer_min`, `requires_booking`, `staff_required` | Cortes / Barba / Tratamientos / Combos | "Reservar por WhatsApp" (free) / Calendario de reservas (entrepreneur) |
| `service` | `service_items` | `duration_min`, `requires_booking` (false por default) | Configurable por el dueño | "Cotizar por WhatsApp" (free) / Formulario de cotización (entrepreneur) |
| `retail` | `retail_items` | `stock`, `sku`, `variants`, `weight_grams` | Configurable por el dueño | "Pedir por WhatsApp" (free) / Checkout con Stripe (entrepreneur) |

**Categorías:** las "sugeridas" se siembran al crear el negocio (ver §3). El dueño puede editar, agregar, eliminar.

### 1.5 Límites por plan (revisado)

| Feature | Free | Entrepreneur |
|---|---|---|
| Negocios | 1 | Ilimitado |
| Items de catálogo (por negocio) | 10 | Ilimitado |
| Categorías | 4 fijas según tipo | Ilimitadas + custom |
| Imágenes por item | 1 | 5 |
| WhatsApp ordering | ✅ | ✅ |
| Pago en línea (Stripe Connect) | ❌ | ✅ |
| Reservas con calendario | ❌ | ✅ (barber, service) |
| Stock tracking real-time | ❌ | ✅ (retail) |
| Modificadores en menú | ❌ (solo nombre+precio) | ✅ (restaurant) |
| Branding custom (color, logo) | Color limitado | Full |
| "Powered by MaalCa" en footer | Sí (link) | Quitable |
| Dominio custom | ❌ | ✅ |
| Analytics dashboard | Conteo básico | Eventos detallados |

Esto reemplaza `src/lib/plan-limits.ts` con una estructura más rica:

```ts
export interface PlanLimits {
  businesses: number;
  itemsPerBusiness: number;
  imagesPerItem: number;
  customCategories: boolean;
  onlinePayments: boolean;
  bookingCalendar: boolean;
  realtimeStock: boolean;
  menuModifiers: boolean;
  brandingFull: boolean;
  customDomain: boolean;
  hidePoweredBy: boolean;
  warningThresholdItems: number;
}
```

---

## 2. Matriz Tipo × Plan × Capacidades de plantilla

Las plantillas públicas (`/[slug]`) se definen como **una plantilla por tipo** con secciones condicionales activadas según plan. No hay 8 archivos; hay 4 archivos con bloques `{plan === 'entrepreneur' && <Section />}`.

| Sección | Restaurant | Barber | Service | Retail |
|---|---|---|---|---|
| Header (logo, nombre, descripción) | F+E | F+E | F+E | F+E |
| Catálogo agrupado por categoría | F+E | F+E | F+E | F+E |
| Botón "Pedir/Reservar por WhatsApp" | F+E | F+E | F+E | F+E |
| Footer "Powered by MaalCa" | F (forzado) | F (forzado) | F (forzado) | F (forzado) |
| **Carrito + Checkout Stripe** | E | — | — | E |
| **Calendario de reservas** | — | E | E (opcional) | — |
| **Modificadores en menú** | E | — | — | — |
| **Stock visible / "Quedan X"** | — | — | — | E |
| **Galería de imágenes (5 por item)** | E | E | E | E |
| **Schema.org / Rich snippets** | E | E | E | E |
| **OG image custom por item** | E | E | E | E |
| **Branding full (color, fonts)** | E | E | E | E |

**F** = Free, **E** = Entrepreneur, **F+E** = Ambos.

**Implementación:** cada plantilla recibe `{ business, items, plan, capabilities }` y usa `capabilities.bookingCalendar`, etc., para renderizar condicionalmente. Esto es más mantenible que duplicar archivos.

---

## 3. Flujo de onboarding completo

### 3.1 Diagrama del happy path

```
[/servicios]
   │ click "Empezar gratis"
   │ track('click_start_free')
   ▼
[/login]  (Google OAuth)
   │ track('login_google_success')
   ▼
[/auth/callback]
   ├── Si usuario tiene businesses → /space/[primer_slug]
   └── Si no → /onboarding
                 │
                 ▼
              [/onboarding] — Paso 1: nombre + tipo
                 │  POST /api/onboarding/create
                 │  ├─ Genera slug único
                 │  ├─ INSERT business (plan='free', published=true)
                 │  ├─ INSERT onboarding_progress
                 │  ├─ INSERT default_categories(business_type)   ← NUEVO
                 │  └─ INSERT seed_catalog_items(business_type)   ← NUEVO (3 items demo)
                 │  track('onboarding_completed')
                 ▼
           [/space/[slug]?new=1]
              ├─ Animación 2.4s
              ├─ Confeti
              ├─ Hero: "Tu negocio ya está en línea"
              ├─ URL pública + copy + QR
              ├─ Checklist (4 items)
              └─ Catálogo con 3 items demo + CTA "Edita o agrega"
                 │
                 │ click "Ver mi página"
                 ▼
           [/[slug]]  (página pública con items demo)
                 │
                 │ user vuelve, edita, agrega items
                 ▼
              ... uso normal ...
                 │
                 │ llega al item #11 (free)
                 ▼
           [UpgradeModal]
                 │ click "Activar Emprendedor"
                 │ track('upgrade_clicked')
                 ▼
           [Stripe Checkout]
                 │ paga
                 ▼
           [/api/stripe/webhook]
                 │ UPDATE plan='entrepreneur'
                 │ track('upgrade_completed')
                 ▼
           [/space/[slug]?upgraded=1]
                 │ confeti + "¡Bienvenido a Emprendedor!"
                 │ NUEVO: aparece selector "+ Crear otro negocio"
                 ▼
              ... uso multi-negocio ...
```

### 3.2 Datos que se piden / inyectan en cada paso

**Paso 1 — Onboarding form (sin cambio del actual, reformulamos defaults):**
- `name` (text, 2-50 chars)
- `business_type` (select de 4 opciones)
- *No pedimos más.* Todo lo demás (descripción, color, WhatsApp, logo) se hace después en el dashboard. La fricción es enemiga.

**Paso 2 — Backend al crear el business (atomizar en transacción):**
1. `generateUniqueSlug(name)`
2. INSERT `businesses { owner_id, slug, name, business_type, plan='free', plan_status='active', published=true, primary_color='#C8102E' }`
3. INSERT `onboarding_progress { business_id, all flags=false }`
4. INSERT `categories[]` según `DEFAULT_CATEGORIES[business_type]` (ver §3.4)
5. INSERT `catalog_items[]` con 3 items demo según `SEED_ITEMS[business_type]` (ver §3.5) — todos con `active=true` para que la página pública no se vea vacía
6. INSERT `analytics_events { event_name: 'onboarding_completed' }`

**Si cualquier paso falla → rollback completo.** No quiero un negocio sin progress, ni con categorías rotas.

**Paso 3 — Dashboard (`/space/[slug]?new=1`):**
- Muestra los 3 items demo cargados con un badge "Demo — edita o elimina"
- Checklist con 4 ítems:
  - ✅ Crea tu espacio (auto)
  - ⬜ Edita o agrega tu primer item real
  - ⬜ Configura WhatsApp
  - ⬜ Comparte tu link

### 3.3 Por qué seed items en lugar de empty state

El blueprint actual deja la página pública vacía hasta que el dueño agrega el primer producto. Esto rompe el momento "WOW" — el dueño copia el link, lo abre, y ve un placeholder genérico. **Seedeando 3 items demo:**
- La página pública se ve poblada inmediatamente
- El dueño tiene un punto de partida para editar (más fácil que crear desde cero)
- El badge "Demo" + un CTA claro empujan a editar

Costo: 3 INSERTs extra al onboarding. Trivial.

### 3.4 Default categories por tipo

```ts
const DEFAULT_CATEGORIES: Record<BusinessType, string[]> = {
  restaurant: ['Entradas', 'Principales', 'Bebidas', 'Postres'],
  barber:     ['Cortes', 'Barba', 'Tratamientos', 'Combos'],
  service:    ['Servicios', 'Paquetes'],   // genérico, el dueño renombra
  retail:     ['Destacados', 'Nuevos'],     // genérico
};
```

Estas se insertan en una nueva tabla `categories` (ver §4.2). En plan `free` el dueño puede renombrar/reordenar pero no agregar más allá del default. En `entrepreneur` puede agregar ilimitadas.

### 3.5 Seed items demo por tipo

```ts
const SEED_ITEMS: Record<BusinessType, Array<Partial<CatalogItem>>> = {
  restaurant: [
    { name: 'Mofongo de cerdo',     category: 'Principales', price: 350, description: 'Plátano verde, chicharrón…', is_demo: true },
    { name: 'Morir Soñando',        category: 'Bebidas',     price: 120, is_demo: true },
    { name: 'Tres Leches',          category: 'Postres',     price: 180, is_demo: true },
  ],
  barber: [
    { name: 'Corte clásico',         category: 'Cortes',     price: 500, duration_min: 30, is_demo: true },
    { name: 'Corte + barba',         category: 'Combos',     price: 800, duration_min: 60, is_demo: true },
    { name: 'Diseño de barba',       category: 'Barba',      price: 400, duration_min: 25, is_demo: true },
  ],
  service: [
    { name: 'Servicio básico',       category: 'Servicios',  price: 1000, is_demo: true },
    { name: 'Servicio premium',      category: 'Servicios',  price: 2500, is_demo: true },
    { name: 'Paquete mensual',       category: 'Paquetes',   price: 5000, is_demo: true },
  ],
  retail: [
    { name: 'Producto destacado',    category: 'Destacados', price: 750,  is_demo: true },
    { name: 'Nuevo arrival',         category: 'Nuevos',     price: 1200, is_demo: true },
    { name: 'Best seller',           category: 'Destacados', price: 950,  is_demo: true },
  ],
};
```

Agregamos columna `is_demo BOOLEAN NOT NULL DEFAULT false` en `catalog_items`. Estos items NO cuentan contra el límite de 10 mientras `is_demo=true`. Al primer edit del dueño se vuelve `is_demo=false` y empieza a contar.

### 3.6 Cuándo se desbloquea multi-negocio

Trigger: `business.plan === 'entrepreneur' AND plan_status === 'active'`.

UI:
- Antes del upgrade: el dashboard solo muestra UN negocio (el del usuario). No hay selector.
- Tras el upgrade: aparece un selector arriba a la izquierda en `/space/[slug]` con dropdown de negocios + opción "+ Crear nuevo negocio". Click ahí → `/onboarding?multi=1` (mismo form, sin redirect a login).
- Política: si el usuario crea un segundo negocio y luego cancela Stripe, ver §1.3 (downgrade).

---

## 4. Schema — migraciones nuevas

### 4.1 `003_catalog_split.sql`

```sql
-- Renombrar products → catalog_items
ALTER TABLE products RENAME TO catalog_items;

-- Nuevo: tipo de item (debe coincidir con business_type del padre)
ALTER TABLE catalog_items ADD COLUMN item_type TEXT NOT NULL DEFAULT 'generic';
ALTER TABLE catalog_items ADD CONSTRAINT catalog_items_type_check
  CHECK (item_type IN ('menu', 'service', 'retail', 'generic'));

-- Nuevo: flag de demo (no cuenta contra el límite del plan)
ALTER TABLE catalog_items ADD COLUMN is_demo BOOLEAN NOT NULL DEFAULT false;

-- Trigger: item_type debe alinearse con business_type
CREATE OR REPLACE FUNCTION enforce_item_type_matches_business()
RETURNS TRIGGER AS $$
DECLARE
  bt TEXT;
BEGIN
  SELECT business_type INTO bt FROM businesses WHERE id = NEW.business_id;
  IF bt = 'restaurant' AND NEW.item_type NOT IN ('menu', 'generic') THEN
    RAISE EXCEPTION 'restaurant business cannot have item_type=%', NEW.item_type;
  ELSIF bt = 'barber' AND NEW.item_type NOT IN ('service', 'generic') THEN
    RAISE EXCEPTION 'barber business cannot have item_type=%', NEW.item_type;
  ELSIF bt = 'retail' AND NEW.item_type NOT IN ('retail', 'generic') THEN
    RAISE EXCEPTION 'retail business cannot have item_type=%', NEW.item_type;
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER catalog_items_type_match
BEFORE INSERT OR UPDATE ON catalog_items
FOR EACH ROW EXECUTE FUNCTION enforce_item_type_matches_business();

-- Tabla hija: menu_items
CREATE TABLE menu_items (
  catalog_item_id  UUID PRIMARY KEY REFERENCES catalog_items(id) ON DELETE CASCADE,
  modifiers        JSONB NOT NULL DEFAULT '[]'::jsonb,  -- [{name, options:[{label,price_delta}]}]
  allergens        TEXT[] NOT NULL DEFAULT '{}',
  spicy_level      INT CHECK (spicy_level BETWEEN 0 AND 3),
  available_from   TIME,
  available_to     TIME
);

-- Tabla hija: service_items (barber + service)
CREATE TABLE service_items (
  catalog_item_id   UUID PRIMARY KEY REFERENCES catalog_items(id) ON DELETE CASCADE,
  duration_min      INT NOT NULL CHECK (duration_min > 0),
  buffer_min        INT NOT NULL DEFAULT 0,
  requires_booking  BOOLEAN NOT NULL DEFAULT false,
  staff_required    INT NOT NULL DEFAULT 1
);

-- Tabla hija: retail_items
CREATE TABLE retail_items (
  catalog_item_id  UUID PRIMARY KEY REFERENCES catalog_items(id) ON DELETE CASCADE,
  stock            INT,                                  -- null = sin tracking
  sku              TEXT,
  variants         JSONB NOT NULL DEFAULT '[]'::jsonb,   -- [{name:'Talla', options:['S','M','L']}]
  weight_grams     INT
);

-- Backfill: migrar duration_min de catalog_items (era de products) a service_items
INSERT INTO service_items (catalog_item_id, duration_min, requires_booking)
SELECT ci.id, COALESCE(ci.duration_min, 30), false
FROM catalog_items ci
JOIN businesses b ON b.id = ci.business_id
WHERE b.business_type IN ('barber', 'service') AND ci.duration_min IS NOT NULL
ON CONFLICT DO NOTHING;

-- Marcar item_type según el negocio padre
UPDATE catalog_items ci SET item_type = CASE
  WHEN b.business_type = 'restaurant' THEN 'menu'
  WHEN b.business_type = 'barber'     THEN 'service'
  WHEN b.business_type = 'service'    THEN 'service'
  WHEN b.business_type = 'retail'     THEN 'retail'
  ELSE 'generic'
END
FROM businesses b
WHERE b.id = ci.business_id;

-- Drop la columna duration_min de catalog_items (ya está en service_items)
ALTER TABLE catalog_items DROP COLUMN duration_min;

-- Renombrar índices viejos para que el nombre refleje la nueva tabla
ALTER INDEX products_business_active_idx RENAME TO catalog_items_business_active_idx;
ALTER INDEX products_business_idx        RENAME TO catalog_items_business_idx;
```

### 4.2 `004_categories.sql`

```sql
CREATE TABLE categories (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  business_id  UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
  name         TEXT NOT NULL,
  sort_order   INT NOT NULL DEFAULT 0,
  is_default   BOOLEAN NOT NULL DEFAULT false,
  created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (business_id, name)
);

CREATE INDEX categories_business_idx ON categories (business_id, sort_order);

-- RLS
ALTER TABLE categories ENABLE ROW LEVEL SECURITY;

CREATE POLICY "categories_owner_all" ON categories
  FOR ALL USING (
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = categories.business_id AND b.owner_id = auth.uid())
  ) WITH CHECK (
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = categories.business_id AND b.owner_id = auth.uid())
  );

CREATE POLICY "categories_public_read" ON categories
  FOR SELECT USING (
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = categories.business_id AND b.published = true)
  );

-- Backfill: crear categorías default para negocios existentes
INSERT INTO categories (business_id, name, sort_order, is_default)
SELECT b.id, cat, ord, true
FROM businesses b
CROSS JOIN LATERAL (
  VALUES
    ('restaurant', 'Entradas', 0),    ('restaurant', 'Principales', 1),
    ('restaurant', 'Bebidas', 2),     ('restaurant', 'Postres', 3),
    ('barber',     'Cortes', 0),      ('barber',     'Barba', 1),
    ('barber',     'Tratamientos', 2),('barber',     'Combos', 3),
    ('service',    'Servicios', 0),   ('service',    'Paquetes', 1),
    ('retail',     'Destacados', 0),  ('retail',     'Nuevos', 1)
) AS defaults(btype, cat, ord)
WHERE b.business_type = defaults.btype
ON CONFLICT (business_id, name) DO NOTHING;

-- catalog_items.category pasa de TEXT libre a FK opcional (manteniendo TEXT por compat)
-- Mantenemos catalog_items.category como TEXT para no romper, pero agregamos category_id
ALTER TABLE catalog_items ADD COLUMN category_id UUID REFERENCES categories(id) ON DELETE SET NULL;
CREATE INDEX catalog_items_category_idx ON catalog_items (category_id) WHERE category_id IS NOT NULL;

-- Backfill: linkear catalog_items.category (texto) → categories.id
UPDATE catalog_items ci SET category_id = c.id
FROM categories c
WHERE c.business_id = ci.business_id AND c.name = ci.category;
```

### 4.3 `005_multi_business_policy.sql`

```sql
-- Asegurar índice por owner para queries frecuentes
CREATE INDEX IF NOT EXISTS businesses_owner_plan_idx ON businesses (owner_id, plan);

-- Función: cuenta cuántos negocios activos tiene un owner
CREATE OR REPLACE FUNCTION owner_business_count(uid UUID)
RETURNS INT AS $$
  SELECT COUNT(*)::INT FROM businesses
  WHERE owner_id = uid AND plan_status != 'downgraded_locked'
$$ LANGUAGE sql STABLE;

-- Extender plan_status para downgrade
ALTER TABLE businesses DROP CONSTRAINT IF EXISTS businesses_plan_status_check;
ALTER TABLE businesses ADD CONSTRAINT businesses_plan_status_check
  CHECK (plan_status IN ('active', 'past_due', 'canceled', 'downgraded_locked'));
```

---

## 5. APIs nuevas / modificadas

| Ruta | Método | Cambio | Comportamiento |
|---|---|---|---|
| `/api/onboarding/create` | POST | **Modificada** | Acepta header `?multi=1` para usuarios entrepreneur. Quita el chequeo "1 business per user" y lo reemplaza por: `if (plan==='free' && existing) return 409`. Agrega siembra de categorías + 3 demo items. Todo en transacción. |
| `/api/catalog` | POST/GET | **Reemplaza** `/api/products` | Acepta `item_type` payload. INSERT en `catalog_items` + tabla hija correspondiente. Cuenta solo items con `is_demo=false` para el límite. |
| `/api/catalog/[id]` | PATCH/DELETE | **Nueva** | Edit set `is_demo=false` automáticamente al primer cambio. Si la edición hace que el count rebase el límite (caso edge tras cambiar items demo a reales), bloquea con 402. |
| `/api/categories` | POST/GET/PATCH/DELETE | **Nueva** | CRUD de categorías. POST bloqueado si plan=free y ya tiene `default count` categorías. |
| `/api/businesses` | POST | **Nueva** | Crear negocio adicional (entrepreneur only). Equivalente al onboarding pero sin redirect a login. 402 si plan=free. |
| `/api/businesses/[id]/publish` | POST | **Nueva** | Toggle published flag. Se usa en downgrade para auto-bloquear. |
| `/api/stripe/webhook` | POST | **Modificada** | En `customer.subscription.deleted`: ejecutar política de downgrade (§1.3). Marca M-1 negocios como `downgraded_locked`, deja el más viejo activo. |

---

## 6. Cambios en el frontend

### 6.1 Layout `/space`

Convertir `/space/[slug]` en un layout con selector de negocio:

```
src/app/space/
  layout.tsx                ← NUEVO. Sidebar con BusinessSwitcher (solo entrepreneur)
  [slug]/
    page.tsx                ← Dashboard (mismo)
    catalog/
      page.tsx              ← Lista de items
      new/page.tsx          ← Form de creación con campos según item_type
      [id]/edit/page.tsx    ← Editar
    categories/
      page.tsx              ← Gestión de categorías (entrepreneur si custom)
    settings/
      page.tsx              ← Branding, WhatsApp, descripción
    upgrade/
      page.tsx              ← Página dedicada (versión completa del modal)
```

### 6.2 Form de catálogo según `item_type`

Mismo componente `CatalogItemForm` con campos condicionales:

```tsx
<CatalogItemForm itemType={business.business_type === 'restaurant' ? 'menu' : ...}>
  {/* Siempre */}
  <NameField /> <DescriptionField /> <CategorySelect /> <PriceField /> <ImageUpload />

  {itemType === 'menu' && (
    <>
      <ModifiersBuilder disabled={plan === 'free'} />     {/* gated */}
      <AllergensMulti />
      <SpicyLevel />
    </>
  )}
  {itemType === 'service' && (
    <>
      <DurationField /> <BookingToggle disabled={plan === 'free'} />
    </>
  )}
  {itemType === 'retail' && (
    <>
      <StockField /> <SKUField />
      <VariantsBuilder disabled={plan === 'free'} />
    </>
  )}
</CatalogItemForm>
```

Los campos gated muestran un lock icon + tooltip: "Disponible en Emprendedor".

### 6.3 Plantillas públicas — refactor

Cada plantilla (`Restaurant.tsx`, `Barber.tsx`, etc.) recibe ahora:

```ts
interface PublicTemplateProps {
  business: { ..., plan: Plan };
  items: Array<CatalogItem & { menu?: MenuFields; service?: ServiceFields; retail?: RetailFields }>;
  categories: Category[];
  capabilities: PlanCapabilities;  // derivada de business.plan
}
```

Y dentro:

```tsx
{capabilities.bookingCalendar && <BookingCalendar items={services} />}
{capabilities.onlinePayments && <CartProvider>...</CartProvider>}
{!capabilities.hidePoweredBy && <PoweredByMaalCa />}
```

---

## 7. Plan de implementación por fases

Las fases viejas (1-11 del IMPLEMENTATION.md) **siguen válidas** para lo que ya hiciste. Esta sección agrega las fases 12-16 que cubren lo nuevo.

### Fase 12 — Schema split (catálogo)

1. Aplicar `003_catalog_split.sql` en Supabase
2. Verificar:
   - `SELECT count(*) FROM catalog_items` == count viejo de products
   - `SELECT count(*) FROM service_items` == count de items que tenían `duration_min`
   - Trigger `enforce_item_type_matches_business` rechaza un INSERT inválido
3. Aplicar `004_categories.sql`
4. Verificar:
   - Cada business existente tiene sus categorías default
   - `catalog_items.category_id` está populado para items que tenían category texto

**Checkpoint:** queries del frontend viejo siguen funcionando (la columna `category` TEXT sigue existiendo).

### Fase 13 — APIs de catálogo

1. Crear `/api/catalog/route.ts` (POST/GET) — reemplaza `/api/products`
2. Crear `/api/catalog/[id]/route.ts` (PATCH/DELETE)
3. Crear `/api/categories/route.ts`
4. Mantener `/api/products` como alias durante 1 deploy (delegando a catalog) para no romper UI vieja
5. Test: crear menu_item desde Postman, verificar que aparece en `menu_items` table

**Checkpoint:** límite de 10 funciona, `is_demo=true` no cuenta.

### Fase 14 — Onboarding mejorado (seed + categorías)

1. Modificar `/api/onboarding/create/route.ts` — envolver todo en transacción, agregar siembra
2. Agregar `DEFAULT_CATEGORIES` y `SEED_ITEMS` en `src/lib/seeds.ts`
3. Update dashboard `/space/[slug]?new=1` para mostrar badge "Demo" en items demo

**Checkpoint:** crear cuenta nueva → página pública sale con 3 items demo + 4 categorías + se ve "real".

### Fase 15 — Plantillas con capabilities

1. Refactor `PublicTemplateProps` para incluir `plan` y `capabilities`
2. Actualizar las 4 plantillas con bloques condicionales
3. Hidden por ahora detrás de feature flag los componentes pesados (BookingCalendar, Cart) — son fase 16

**Checkpoint:** un negocio free y uno entrepreneur del mismo tipo se ven distintos en `/[slug]`.

### Fase 16 — Multi-negocio

1. `005_multi_business_policy.sql` aplicado
2. Crear `/api/businesses/route.ts` POST
3. Modificar webhook stripe para política de downgrade
4. Crear `BusinessSwitcher` component en `src/app/space/layout.tsx`
5. Modificar UpgradeModal para mencionar "+ Crea negocios ilimitados"

**Checkpoint:**
- Free + intentar crear segundo negocio → 402 + modal
- Tras upgrade, switcher aparece, "Crear nuevo" funciona
- Cancelar Stripe en cuenta con 3 negocios → 1 queda activo, 2 quedan `downgraded_locked` y devuelven 404 público

### Fase 17 (opcional) — Capabilities reales

Recién aquí construyes:
- BookingCalendar para barber/service entrepreneur
- Cart + Stripe Connect para restaurant/retail entrepreneur
- ModifiersBuilder UI

Esto es post-MVP. El MVP del split es 12-16.

---

## 8. Riesgos y huecos del modelo

Lo que **este plan no resuelve** y vas a topar:

1. **Stripe Connect para pagos online (entrepreneur)** — no es solo activar checkout. Cada negocio del entrepreneur necesita su propio Stripe account conectada. Eso es un onboarding de Stripe Connect Express dentro del onboarding de MaalCa. No está modelado aquí. Para MVP: WhatsApp ordering only.

2. **Disponibilidad de horarios para reservas (barber)** — el modelo `service_items` tiene `duration_min` y `requires_booking`, pero no hay tabla de `business_hours` ni `staff_schedule` ni `bookings`. Esto es una sub-feature grande. Recomiendo no prometerlo en plantilla hasta tener `006_bookings.sql` diseñado.

3. **Imágenes — almacenamiento.** Hoy `image_url` es un TEXT. Necesitas Supabase Storage bucket configurado, política de tamaño, posiblemente CDN. Y para entrepreneur "5 imágenes por item" necesitas tabla `catalog_item_images` o un TEXT[].

4. **SEO y rendering.** `/[slug]/page.tsx` debe ser SSR con `revalidate` controlado. Si un dueño edita su menú, ¿cuándo se invalida el cache? Recomiendo `revalidateTag(`business:${slug}`)` en el PATCH de catalog.

5. **Conflicto de slugs reservados.** El blueprint reserva 30 slugs pero olvida `space`, `settings`, `catalog`, `categories`, `upgrade` que ahora son rutas de `/space`. **Hay que agregarlos** a `reserved_slugs` antes del Fase 12.

6. **Multi-negocio + analytics.** El `business_id` en `analytics_events` ya existe, pero el dashboard de analytics no filtra por negocio activo. Si tienes 3 negocios, vas a ver eventos mezclados. Hay que agregar un filtro en el query.

7. **Migración de afiliados existentes.** El `migrations/002_migrate_affiliates.sql` mete TLD con `business_type='restaurant'` pero sus items existentes en la base vieja pueden no tener `category` consistente con `DEFAULT_CATEGORIES`. Hay que revisar manualmente qué tiene TLD ahora y mapear sus categorías a las nuevas.

8. **Webhooks idempotency.** El webhook de Stripe en el blueprint no tiene tabla de `processed_events`. Si Stripe reenvía un evento (lo hace), vas a procesar el upgrade dos veces. Falta `CREATE TABLE stripe_processed_events (event_id PRIMARY KEY)` y check antes de procesar.

9. **`is_demo` y conteo del límite — caso edge.** Si el dueño tiene 10 items reales y abre un demo (heredado de algún momento), al editarlo `is_demo` pasa a false y queda con 11 reales. La validación debe ser **al editar un demo**, no al crearlo: chequear si tras pasar a real, el count rebasa el límite, y bloquear.

---

## 9. Definition of done (este plan)

- [ ] `003_catalog_split.sql` aplicado, 0 datos perdidos vs `products` original
- [ ] `004_categories.sql` aplicado, todos los businesses tienen sus categorías default
- [ ] Onboarding crea negocio + 4 categorías + 3 items demo en una sola transacción
- [ ] `/[slug]` de un negocio recién creado se ve poblado (no empty state)
- [ ] Items demo tienen badge "Demo" en `/space/[slug]/catalog`
- [ ] Editar un item demo lo convierte en real y empieza a contar
- [ ] Free intentando crear 2do negocio → 402 + modal
- [ ] Entrepreneur ve switcher de negocios
- [ ] Downgrade marca M-1 negocios como locked, devuelven 404 público
- [ ] Plantilla restaurant entrepreneur muestra modificadores; free no
- [ ] Plantilla barber entrepreneur muestra calendar (aunque sea placeholder); free solo WhatsApp
- [ ] Webhook Stripe es idempotente (no procesa el mismo evento 2 veces)
- [ ] Slugs reservados incluyen `space`, `settings`, `catalog`, `categories`, `upgrade`

---

## 10. Reglas anti-improvisación para CLI (segunda ronda)

> **Lee este documento Y `IMPLEMENTATION.md` antes de cualquier cambio.**
> No modifiques `catalog_items` sin migración nueva (006+).
> No agregues columnas a `menu_items`/`service_items`/`retail_items` sin actualizar el form correspondiente Y la plantilla pública.
> No hagas que un item demo cuente contra el límite del plan — la regla es `WHERE is_demo = false`.
> No permitas crear negocio adicional sin verificar `plan='entrepreneur' AND plan_status='active'`.
> El `BusinessSwitcher` no se renderiza si el usuario tiene 1 solo negocio (aunque sea entrepreneur).
> No quites el footer "Powered by MaalCa" de plantillas free, solo en entrepreneur con `capabilities.hidePoweredBy=true`.
> En la política de downgrade, el negocio que se queda activo es el más antiguo por `created_at ASC` — no el del slug "más bonito" ni nada raro. Determinístico.
> Después de cada fase, corre `npx tsc --noEmit` y verifica el checkpoint correspondiente.
