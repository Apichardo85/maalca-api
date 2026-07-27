# MaalCa SaaS — Implementation Plan

**Goal:** DISCOVER → TRUST → ENTER → CREATE → EXPERIENCE VALUE → NEED MORE → PAY

This document is the **single source of truth** for implementing the MaalCa onboarding + monetization flow. Follow phases in order. Do not skip checkpoints.

---

## Decisions locked in

| Decision | Choice |
|---|---|
| Payments | Stripe Checkout + Subscription webhooks |
| Public page templates | Per `business_type` (restaurant, barber, service, retail) |
| Analytics | GA4 (client) + Supabase `analytics_events` (server, source of truth) |
| Slug strategy | Path-based: `maalca.com/[slug]` |
| Existing affiliates | Migrated to `businesses` with `plan='entrepreneur'` |
| Free plan limit | 10 products, warning banner from 7 onward |

---

## Pre-flight checklist (do BEFORE coding)

### Stripe Dashboard
1. Create Product: "MaalCa Emprendedor"
2. Create Price: $38.00 USD recurring monthly → copy `price_xxx` ID
3. Configure webhook endpoint: `https://maalca.com/api/stripe/webhook`
   - Events: `checkout.session.completed`, `customer.subscription.updated`, `customer.subscription.deleted`
   - Copy `whsec_xxx` signing secret
4. Save in `.env.local`:
   ```
   STRIPE_SECRET_KEY=sk_live_...
   STRIPE_WEBHOOK_SECRET=whsec_...
   STRIPE_PRICE_ENTREPRENEUR=price_...
   NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=pk_live_...
   ```

### GA4
1. Create GA4 property for `maalca.com`
2. Copy Measurement ID (`G-XXXXXXXXXX`)
3. Save: `NEXT_PUBLIC_GA4_MEASUREMENT_ID=G-XXXXXXXXXX`

### NPM packages
```bash
npm install stripe @stripe/stripe-js qrcode
npm install -D @types/qrcode
```

---

## Phase 1 — Database schema

**File:** `migrations/001_maalca_saas.sql`

Run via Supabase SQL editor or `supabase db push`.

**Checkpoint after Phase 1:**
- [ ] `businesses` table has `business_type`, `plan`, `stripe_*` columns
- [ ] `products`, `onboarding_progress`, `analytics_events` tables exist
- [ ] RLS policies active on all tables
- [ ] Existing affiliates (TLD, Pegote, MaalCa admin) inserted with `plan='entrepreneur'`

---

## Phase 2 — Shared utilities

Order: build these first, all pages depend on them.

| File | Purpose |
|---|---|
| `src/lib/plan-limits.ts` | Plan config, limit checking |
| `src/lib/analytics.ts` | Dual-track GA4 + Supabase |
| `src/lib/stripe.ts` | Stripe SDK init |
| `src/lib/qr.ts` | QR PNG generator |
| `src/lib/templates/registry.ts` | Business type → template component mapping |
| `src/lib/slug.ts` | Slug generation + uniqueness check |

**Checkpoint after Phase 2:**
- [ ] `track('test_event')` writes a row to `analytics_events`
- [ ] `getPlanLimits('free').products === 10`
- [ ] TypeScript compiles clean

---

## Phase 3 — Landing page (`/servicios`)

No auth dependency. Build first to validate design.

**Components:**
- `Hero` — title, subtitle, primary CTA
- `FreePlanCard` — features list, "Start Free" button
- `TrustBar` — logos of existing businesses (TLD, Pegote, etc.)
- `Footer`

**Analytics:** `click_start_free` on CTA click → redirect `/login`.

**Checkpoint:**
- [ ] Mobile-first responsive
- [ ] Clicking CTA fires GA4 event AND writes to Supabase
- [ ] No dead links

---

## Phase 4 — Auth (`/login`)

**Primary:** "Continue with Google" (Supabase OAuth)
**Secondary (less prominent):** Email/password collapsed in `<details>`

**Callback already works** (`/auth/callback/route.ts`). Just need login UI.

**Analytics:** `login_google_success` fired in `/auth/callback` after successful exchange.

**Checkpoint:**
- [ ] Google button works end-to-end
- [ ] No GitHub/Apple buttons (don't show what's not implemented)
- [ ] After login: new user → `/onboarding`, existing → `/space/[slug]`

---

## Phase 5 — Onboarding (`/onboarding`)

**Already exists** but needs `business_type` field.

**Form:**
- Business name (text)
- Business type (select: Restaurante / Barbería / Servicios / Retail)

**Server action:** `createBusiness()` →
1. Generate unique slug from name
2. INSERT into `businesses` (plan='free', published=true)
3. INSERT into `onboarding_progress` (all false)
4. Track `onboarding_completed`
5. Redirect to `/space/[slug]?new=1`

**Checkpoint:**
- [ ] Slug collisions handled (append `-2`, `-3`, etc.)
- [ ] `business_type` persisted correctly
- [ ] Redirect happens within 1.5s

---

## Phase 6 — Dashboard (`/space/[slug]`)

This is the **"instant value"** moment. Critical UX.

**Layout (when `?new=1`):**
1. 2-second animation: "Creando tu espacio..." with sequential checkmarks
2. Confetti
3. Hero card: "🚀 Tu negocio ya está en línea"
4. Public URL with copy button
5. QR code preview
6. Activation checklist (persisted in `onboarding_progress`)
7. Plan badge: "Plan Gratis · 0 de 10 productos"

**Layout (returning user):**
- Skip animation
- Show same dashboard with current state
- If `products.count >= 7` → warning banner
- If `products.count >= 10` → upgrade CTA prominent

**Checklist items:**
- [x] Crea tu espacio (auto-checked)
- [ ] Agrega tu primer producto/servicio
- [ ] Configura WhatsApp
- [ ] Comparte tu link

**Checkpoint:**
- [ ] Copy link button fires `link_copied` event
- [ ] Checklist persists across reloads
- [ ] "Ver mi página" opens `/[slug]` in new tab

---

## Phase 7 — Products CRUD

**Files:**
- `/space/[slug]/products/page.tsx` — list
- `/space/[slug]/products/new/page.tsx` — form
- `/api/products/route.ts` — POST/GET
- `/api/products/[id]/route.ts` — PATCH/DELETE

**Critical: limit enforcement on POST**
```ts
if (plan === 'free' && currentCount >= 10) {
  return Response.json(
    { error: 'plan_limit_reached', upgrade_required: true },
    { status: 402 }
  );
}
```

**Frontend captures 402** → opens `<UpgradeModal />`.

**Warning at 7+:** banner above product list:
> "Estás cerca del límite. Te quedan X productos en el plan gratis."

**Analytics:** `first_product_created` fires only on the very first product.

**Checkpoint:**
- [ ] Cannot create 11th product on free plan
- [ ] Warning shows from 7 onward
- [ ] Public `/[slug]` updates immediately after product create

---

## Phase 8 — Public business page (`/[slug]`)

**THE moment of truth.** This is what makes the user feel "I have something real."

**Route:** `src/app/[slug]/page.tsx` (catch-all at root level — be careful with conflicts)

**Logic:**
1. Fetch `businesses` by slug, check `published=true`
2. Fetch `products` where `active=true`, ordered by `sort_order`
3. Look up template via `TEMPLATES[business.business_type]`
4. Render template with `{ business, products }`

**Templates (one file each):**
- `RestaurantTemplate` — categories grid (entradas/principales/bebidas/postres), prices, "Pedir por WhatsApp"
- `BarberTemplate` — service list with duration + price, "Reservar por WhatsApp"
- `ServiceTemplate` — service cards with descriptions
- `RetailTemplate` — product grid with images

**All templates include:**
- Business name + logo
- WhatsApp floating button (if configured)
- Footer: "Powered by MaalCa" link → `/servicios` (growth loop)

**SEO:**
- Dynamic `<title>{business.name} | MaalCa</title>`
- OG image generated via `/api/og/[slug]` (Phase 11, optional)

**Conflict prevention:** make sure these slugs are reserved and rejected at signup:
`servicios`, `login`, `onboarding`, `space`, `dashboard`, `api`, `auth`, `admin`, `_next`, `static`

**Checkpoint:**
- [ ] `/the-little-dominican` renders restaurant template
- [ ] Reserved slugs rejected at onboarding
- [ ] Unpublished business returns 404

---

## Phase 9 — Monetization

**Files:**
- `src/components/space/UpgradeModal.tsx`
- `src/app/space/[slug]/upgrade/page.tsx` (full-page version)
- `src/app/api/checkout/route.ts`
- `src/app/api/stripe/webhook/route.ts`

**Trigger points:**
1. POST `/api/products` returns 402 → modal opens
2. User clicks "Upgrade" badge in dashboard header
3. Warning banner CTA at 7+ products

**Modal copy:**
> **Estás creciendo 🔥**
>
> Desbloquea con Emprendedor:
> - Productos ilimitados
> - Pedidos en línea
> - Pagos integrados
> - Analytics avanzado
>
> [Activar Emprendedor — $38/mes]

**Checkout flow:**
```
Click CTA
  → POST /api/checkout { business_id }
  → Server: stripe.checkout.sessions.create({...})
  → Track upgrade_clicked
  → Redirect to Stripe Checkout
  → User pays
  → Stripe webhook → UPDATE businesses SET plan='entrepreneur'
  → Stripe redirects to /space/[slug]?upgraded=1
  → Show confetti + "¡Bienvenido a Emprendedor!"
  → Track upgrade_completed (server-side from webhook)
```

**Webhook handling (critical, often broken):**
```ts
const sig = req.headers.get('stripe-signature');
const event = stripe.webhooks.constructEvent(body, sig, WEBHOOK_SECRET);
// handle by event.type, idempotent (check if already processed)
```

**Checkpoint:**
- [ ] Stripe test mode: card `4242 4242 4242 4242` upgrades plan
- [ ] Webhook signature verification works
- [ ] `plan='entrepreneur'` after successful payment
- [ ] Cancellation downgrades to `plan='free'`

---

## Phase 10 — Analytics tracking

**File:** `src/app/api/analytics/track/route.ts`

POST endpoint, validates session, writes to `analytics_events`.

**Layout integration:** add GA4 script in `src/app/layout.tsx`:
```tsx
<Script src={`https://www.googletagmanager.com/gtag/js?id=${GA4_ID}`} />
<Script id="ga4-init">{`
  window.dataLayer = window.dataLayer || [];
  function gtag(){dataLayer.push(arguments);}
  gtag('js', new Date());
  gtag('config', '${GA4_ID}');
`}</Script>
```

**6 events to verify firing:**
| Event | Where |
|---|---|
| `click_start_free` | `/servicios` CTA |
| `login_google_success` | `/auth/callback` |
| `onboarding_completed` | After business created |
| `first_product_created` | POST `/api/products` (only first) |
| `link_copied` | Copy button in `/space/[slug]` |
| `upgrade_clicked` | Upgrade modal CTA |
| `upgrade_completed` | Stripe webhook (bonus, server-side) |

**Checkpoint:**
- [ ] All 6 events visible in GA4 DebugView
- [ ] All 6 events visible in `analytics_events` table

---

## Phase 11 — Migration of existing affiliates

**Script:** `migrations/002_migrate_affiliates.sql`

```sql
-- Insert existing affiliates as businesses with entrepreneur plan
-- Maps: alejandropichardo85@gmail.com → maalca
--       littledominicanarestaurant@gmail.com → the-little-dominican
INSERT INTO businesses (owner_id, slug, name, business_type, plan, published)
SELECT 
  u.id,
  'maalca',
  'MaalCa',
  'service',
  'entrepreneur',
  true
FROM auth.users u
WHERE u.email = 'alejandropichardo85@gmail.com'
ON CONFLICT (slug) DO NOTHING;

INSERT INTO businesses (owner_id, slug, name, business_type, plan, published)
SELECT 
  u.id,
  'the-little-dominican',
  'The Little Dominican',
  'restaurant',
  'entrepreneur',
  true
FROM auth.users u
WHERE u.email = 'littledominicanarestaurant@gmail.com'
ON CONFLICT (slug) DO NOTHING;
```

**Then update `/auth/callback`:** the `KNOWN_AFFILIATES` constant becomes a fallback only. The flow simplifies to:
1. Check `businesses` table for owner_id
2. If found → `/space/[slug]`
3. Else → `/onboarding`

The hardcoded routing to `/dashboard/[affiliate_id]` can be removed once you confirm the migration worked. Keep it as fallback for one deploy cycle.

**Checkpoint:**
- [ ] TLD owner logging in lands at `/space/the-little-dominican`
- [ ] Their existing menu visible at `/the-little-dominican`
- [ ] Plan shows "Emprendedor" not "Free"

---

## File delivery order

When you go back to CLI, paste files in this order — each phase's files must compile before moving to the next:

1. `migrations/001_maalca_saas.sql` → run in Supabase
2. `.env.local` updates
3. `src/lib/*` (utilities)
4. `src/components/**` (shared components)
5. `src/app/servicios/page.tsx` → test landing
6. `src/app/login/page.tsx` → test auth
7. `src/app/onboarding/page.tsx` + action
8. `src/app/space/[slug]/page.tsx` → test instant value
9. `src/app/api/products/**` + `/space/[slug]/products/**`
10. `src/app/[slug]/page.tsx` + 4 templates → test public page
11. `src/app/api/checkout/**` + `/api/stripe/webhook/**` + upgrade modal
12. `src/app/api/analytics/track/route.ts` + layout GA4
13. `migrations/002_migrate_affiliates.sql`

---

## Anti-CLI-improvisation rules

When you give this plan to your CLI tool, include this block verbatim:

> **DO NOT improvise file structure. Follow the paths in this document exactly.**
> **DO NOT add features not listed.** No "while we're at it" additions.
> **DO NOT skip checkpoints.** After each phase, verify the checkpoint passes before moving on.
> **DO NOT create UI for the email/password auth flow.** Only Google OAuth UI in primary view.
> **DO NOT add buttons that don't work.** No "Coming soon" placeholders.
> **DO NOT remove existing affiliate hardcoding** until Phase 11 migration is verified.
> **DO test the 402 limit** by manually creating 10 products and trying an 11th.
> **DO test webhook locally** with `stripe listen --forward-to localhost:3000/api/stripe/webhook`.

---

## Definition of done

Ship when all true:
- [ ] New user from `/servicios` → live business page in under 2 minutes
- [ ] All 6 analytics events fire in GA4 + Supabase
- [ ] 11th product attempt opens upgrade modal
- [ ] Stripe test card upgrades plan, webhook updates DB
- [ ] Existing affiliates (TLD, Pegote) work on new system
- [ ] No dead UI, no broken links, no "Coming soon"
- [ ] Mobile responsive on iPhone SE (375px)
