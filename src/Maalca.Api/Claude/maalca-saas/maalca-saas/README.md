# MaalCa SaaS — Cómo aplicar este plan sin que CLI lo arruine

Este folder contiene el blueprint completo. Léelo todo antes de tocar código.

## Archivos

```
IMPLEMENTATION.md                        ← Plan maestro, fases, checkpoints
README.md                                ← Este archivo (orden de comandos)
.env.example                             ← Variables de entorno

migrations/
  001_maalca_saas.sql                    ← Schema completo
  002_migrate_affiliates.sql             ← Migra TLD, MaalCa admin

src/
  app/
    servicios/page.tsx                   ← Landing
    login/page.tsx                       ← Auth
    onboarding/page.tsx                  ← Form business_name + type
    space/[slug]/page.tsx                ← Dashboard guiado
    space/[slug]/products/new/page.tsx   ← Add product (con captura de 402)
    [slug]/page.tsx                      ← 🌟 PÁGINA PÚBLICA
    auth/callback/route.ts               ← Routing inteligente
    api/onboarding/create/route.ts       ← Crear business
    api/products/route.ts                ← CRUD con límite de 10
    api/checkout/route.ts                ← Stripe Checkout session
    api/stripe/webhook/route.ts          ← Activa plan al pagar
    api/analytics/track/route.ts         ← Eventos a Supabase
    api/qr/[slug]/route.ts               ← QR PNG
    layout.tsx                           ← Snippet GA4 (mergear con tu layout actual)

  components/
    space/
      SpaceDashboard.tsx                 ← Dashboard con instant value
      CreatingSpaceAnimation.tsx         ← Animación 2.4s
      UpgradeModal.tsx                   ← Modal Stripe
    public/templates/
      Restaurant.tsx                     ← Plantilla restaurante
      Barber.tsx                         ← Plantilla barbería
      Service.tsx                        ← Plantilla servicios
      Retail.tsx                         ← Plantilla tienda

  lib/
    plan-limits.ts                       ← Config de planes
    analytics.ts                         ← Track GA4 + Supabase
    stripe.ts                            ← SDK init
    qr.ts                                ← Generación PNG
    slug.ts                              ← Slug único
    templates/registry.ts                ← Mapeo type → component
```

## Orden EXACTO de aplicación

### Paso 1 — Pre-flight (manual, 15 min)

1. **Stripe Dashboard:**
   - Crea producto "MaalCa Emprendedor"
   - Crea price recurring $38 USD/mes → copia `price_xxx`
   - Configura webhook `https://maalca.com/api/stripe/webhook` con eventos:
     `checkout.session.completed`, `customer.subscription.updated`, `customer.subscription.deleted`
   - Copia `whsec_xxx`

2. **GA4:** crea propiedad, copia Measurement ID `G-XXXXXXXXXX`

3. **`.env.local`:** copia `.env.example` y llena todos los valores

4. **Instala deps:**
   ```bash
   npm install stripe @stripe/stripe-js qrcode @supabase/ssr
   npm install -D @types/qrcode
   ```

### Paso 2 — Schema (5 min)

```bash
# En Supabase SQL Editor, copia/pega y ejecuta:
# 1. migrations/001_maalca_saas.sql
# 2. (después de validar con SELECT) migrations/002_migrate_affiliates.sql
```

Verifica:
```sql
SELECT slug, name, plan FROM businesses;
-- Debe mostrar: maalca | The Little Dominican (plan='entrepreneur')
SELECT slug FROM reserved_slugs ORDER BY slug;
```

### Paso 3 — Copiar código a tu repo

Copia los archivos en este orden (compila después de cada bloque):

**Bloque A — Utils (no rompen nada):**
```
src/lib/plan-limits.ts
src/lib/analytics.ts
src/lib/stripe.ts
src/lib/qr.ts
src/lib/slug.ts
src/lib/templates/registry.ts
```

**Bloque B — API routes:**
```
src/app/api/onboarding/create/route.ts
src/app/api/products/route.ts
src/app/api/checkout/route.ts
src/app/api/stripe/webhook/route.ts
src/app/api/analytics/track/route.ts
src/app/api/qr/[slug]/route.ts
src/app/auth/callback/route.ts  ← reemplaza el actual
```

**Bloque C — Templates públicas:**
```
src/components/public/templates/Restaurant.tsx
src/components/public/templates/Barber.tsx
src/components/public/templates/Service.tsx
src/components/public/templates/Retail.tsx
```

**Bloque D — Componentes dashboard:**
```
src/components/space/SpaceDashboard.tsx
src/components/space/CreatingSpaceAnimation.tsx
src/components/space/UpgradeModal.tsx
```

**Bloque E — Páginas:**
```
src/app/servicios/page.tsx
src/app/login/page.tsx
src/app/onboarding/page.tsx
src/app/space/[slug]/page.tsx
src/app/space/[slug]/products/new/page.tsx
src/app/[slug]/page.tsx
```

**Bloque F — Layout:**
- Mergea `src/app/layout.tsx` con tu layout actual (solo agregar Script tags GA4)

### Paso 4 — Compilar y validar

```bash
npx tsc --noEmit
# Debe compilar sin errores en archivos nuevos
# (errores pre-existentes en otros archivos están bien)

npm run dev
```

### Paso 5 — Test manual del flujo completo

Con un usuario Google nuevo (NO `alejandropichardo85@gmail.com`):

1. Ve a `/servicios` → click "Empezar gratis"
2. Login con Google
3. **Verifica:** redirige a `/onboarding`
4. Llena formulario (ej: "Test Restaurant" + Restaurante)
5. **Verifica:** ves la animación 2.4s "Creando tu espacio..."
6. **Verifica:** aterrizas en `/space/test-restaurant?new=1` con confeti
7. Click "Copiar" link → verifica `link_copied` en Supabase y GA4
8. Click "Agregar producto" → crea uno
9. Click "Ver mi página" → abre `/test-restaurant` con la plantilla restaurant
10. Crea 9 productos más manualmente (loop POST `/api/products`) hasta 10
11. Intenta crear el #11 → debe abrir modal upgrade
12. Click "Activar Emprendedor" → redirige a Stripe Checkout
13. Usa tarjeta test `4242 4242 4242 4242` → completa pago
14. **Verifica:** redirige a `/space/test-restaurant?upgraded=1`
15. **Verifica en DB:** `SELECT plan FROM businesses WHERE slug='test-restaurant'` → `entrepreneur`

### Paso 6 — Test webhook localmente

En otra terminal:
```bash
stripe login
stripe listen --forward-to localhost:3000/api/stripe/webhook
# Copia el whsec_xxx que te muestra y úsalo en .env.local
```

Dispara evento de prueba:
```bash
stripe trigger checkout.session.completed
```

### Paso 7 — Verifica analytics

```sql
SELECT event_name, COUNT(*), MAX(created_at) AS latest
FROM analytics_events
GROUP BY event_name
ORDER BY latest DESC;
```

Deberías ver al menos: `click_start_free`, `login_google_success`, `onboarding_completed`, `first_product_created`, `link_copied`, `upgrade_clicked`, `upgrade_completed`.

En GA4 → DebugView, los mismos eventos deberían aparecer.

## Reglas anti-improvisación para CLI

Cuando le pidas a CLI que continúe trabajo, pégale este bloque:

> **Lee `IMPLEMENTATION.md` antes de cualquier cambio.**
> No agregues archivos fuera del árbol del README.
> No cambies el schema sin agregar una migración nueva (003+).
> No cambies los nombres de los 7 eventos de analytics.
> No agregues OAuth providers extra (solo Google).
> No quites el límite de 10 productos sin coordinarlo.
> No toques `/dashboard/[affiliate_id]` viejo hasta confirmar que la migración funcionó.
> Después de cada fase, corre `npx tsc --noEmit` y verifica el checkpoint.

## Qué hacer cuando algo falle

| Síntoma | Causa probable | Fix |
|---|---|---|
| Webhook 400 invalid_signature | `STRIPE_WEBHOOK_SECRET` mal | Recopia el `whsec_` correcto |
| `/[slug]` rompe rutas existentes | Conflict con ruta hardcoded | Agrega slug a `RESERVED` set en `[slug]/page.tsx` y a `reserved_slugs` table |
| Onboarding 409 business_already_exists | Usuario ya tiene business | Es esperado, redirige a `/space/[slug]` |
| Plan sigue 'free' después de pagar | Webhook no llegó | Verifica con `stripe events list`, reenvía con `stripe events resend evt_xxx` |
| 402 al crear producto pero plan es entrepreneur | Cache stale | El handler relee plan en cada POST, no hay cache. Verifica DB directo |

## Definición de done

Antes de mergear a main:

- [ ] Usuario nuevo: `/servicios` → página pública en menos de 2 minutos
- [ ] 7 eventos visibles en `analytics_events` table
- [ ] 7 eventos visibles en GA4 DebugView
- [ ] Producto #11 dispara modal upgrade (no error genérico)
- [ ] Tarjeta test `4242...` upgradea plan vía webhook
- [ ] `/the-little-dominican` renderiza con plantilla restaurant
- [ ] Mobile (375px iPhone SE) sin scroll horizontal en ninguna página
- [ ] No hay botones "Coming soon" o links rotos
- [ ] `npx tsc --noEmit` no agrega errores nuevos
