# MaalCa SaaS — Plan v2: Conectar `maalca-web` con `maalca-api`

**Documento de diseño previo a implementación — versión 2.**
Sustituye partes de `MODEL_PLAN.md` (v1) que asumían arquitectura monolítica Next.js + Supabase. La arquitectura real es:

- **`maalca-web`** (Next.js 15) — UI pública + dashboard de afiliados
- **`maalca-api`** (.NET 8 + Postgres) — Backend con multi-tenancy ya implementado
- **`maalca-cms`** (Umbraco) — Fuera de alcance del SaaS

Este documento describe **qué hay que construir nuevo, qué hay que conectar, y qué se conserva intacto.**

---

## 0. Decisiones cerradas (lock-in)

| Decisión | Choice | Justificación |
|---|---|---|
| Base de datos | PostgreSQL (existente) | Ya funciona, migrar es trabajo gratis sin upside |
| Arquitectura backend | Service Layer + Minimal APIs (existente) | Respetar el patrón actual, no meter CQRS forzado |
| Auth afiliados | Supabase Auth + bridge a `maalca-api` | Frontend ya lo usa, bridge = ~50 líneas en .NET |
| Auth admin interno | JWT propio (existente) | Ya existe, sirve para usuarios admin tuyos |
| Entidad central | `Affiliate` en API, alias `Business` en frontend | Evita renombre masivo, mantiene seed existente |
| Multi-tenancy | `AffiliateId` discriminador (existente) | Ya implementado, todas las entidades filtran por él |
| Catálogo público | Proyección sobre `Product`/`Service`/`InventoryItem` existentes | NO crear tabla nueva, reusar lo que ya hay |
| Plantillas frontend | Reusar las 4 plantillas ya hechas en `maalca-web` | Cambian las URLs que consumen, no los componentes |
| Facturación e-CF | Fuera de alcance v2 | Capítulo aparte, depende de DGII + certificado |
| Pagos en línea | Stripe Checkout, webhook a `maalca-api` | Sin cambio respecto a v1 |

---

## 1. Estado actual: qué hay y qué falta

### 1.1 Lo que `maalca-api` YA tiene

| Componente | Estado | Comentario |
|---|---|---|
| `Affiliate` aggregate root | ✅ | Tenant central, multi-tenancy implementado |
| `Customer` (CRM básico) | ✅ | Por afiliado |
| `Appointment`, `Service`, `TeamMember` | ✅ | Agenda con personal |
| `QueueEntry` + SignalR | ✅ | Cola en tiempo real |
| `InventoryItem`, `InventoryMovement` | ✅ | Inventario con movimientos |
| `Product`, `Invoice`, `InvoiceItem`, `GiftCard` | ✅ | Facturación interna (sin DGII) |
| `Campaign`, `Lead` | ✅ | Marketing básico |
| `AgentExecution` | ✅ | Telemetría de agentes IA |
| Auth JWT propio + BCrypt + refresh | ✅ | Sirve para admins internos |
| Seed de 6 afiliados (Pegote, TLD, MaalCa LLC, etc.) | ✅ | Data realista |
| EF Core 8 + Npgsql | ✅ | Migrations existen |
| Deployed en Railway | ✅ | Producción funciona |

### 1.2 Lo que `maalca-web` YA tiene (frontend)

| Componente | Estado | Comentario |
|---|---|---|
| Plantillas públicas (Restaurant, Barber, Service, Retail) | ✅ | Esperan props `{ business, items, categories, capabilities }` |
| `BusinessSwitcher` + layout `/space` | ✅ | Multi-negocio UI ready |
| `capabilities.ts`, `plan-limits.ts` | ✅ | Lógica de plan en frontend |
| Demo badges + checklist en onboarding | ✅ | Visual ya hecho |
| Login Google via Supabase | ✅ | OAuth funciona |
| Dashboard `/space/[slug]` | ✅ | UI armada |

### 1.3 Lo que FALTA construir (objetivo del v2)

**En `maalca-api`:**

1. Concepto de `Plan` (free/entrepreneur) en `Affiliate`
2. Tabla `UserAffiliateMap` que vincula `SupabaseUserId` → `AffiliateId` con rol
3. Middleware que valida JWT de Supabase y mapea a afiliado(s)
4. Endpoint público `GET /api/public/affiliates/{slug}` con plantilla data
5. Endpoint público `GET /api/public/affiliates/{slug}/catalog` que proyecta `Service`/`Product`/`InventoryItem` según tipo
6. Endpoint `POST /api/onboarding` para self-service de afiliados nuevos
7. Endpoint `POST /api/affiliates` (entrepreneur multi-negocio)
8. Webhook Stripe + idempotencia
9. Concepto de `BusinessType` en `Affiliate` (restaurant/barber/service/retail) — clave para que las plantillas decidan qué renderizar

**En `maalca-web`:**

1. Cliente HTTP centralizado (`lib/api-client.ts`) que apunta a `maalca-api`
2. Wrapper de auth: extraer JWT de Supabase y enviarlo en `Authorization: Bearer`
3. Reescribir las llamadas que el CLI iba a hacer como Next.js API routes para que vayan a `maalca-api`
4. SSR de página pública `/[slug]`: server-side fetch a `maalca-api`
5. Tipos TypeScript que reflejen DTOs de `maalca-api` (no inventarlos)

**Lo que NO hay que hacer:**

- ❌ Migrar Postgres a SQL Server
- ❌ Renombrar `Affiliate` a `Business` en backend
- ❌ Construir inventario, agenda, facturación interna (ya existe)
- ❌ Crear tablas `catalog_items`, `menu_items`, `service_items`, `retail_items` del v1 — se reusan las que ya hay
- ❌ Implementar CQRS/MediatR
- ❌ Reescribir auth desde cero
- ❌ Borrar el seed de 6 afiliados existentes

---

## 2. Modelo de dominio: cómo encaja el SaaS público en lo existente

### 2.1 Cambios mínimos al `Affiliate` existente

**No reemplazar la entidad. Agregar campos:**

```csharp
// Maalca.Domain/Entities/Affiliate.cs
public class Affiliate
{
    // ... campos existentes ...

    // NUEVO — para el SaaS público
    public string Slug { get; set; }                    // unique, lowercase, public URL: /[slug]
    public BusinessType BusinessType { get; set; }      // restaurant | barber | service | retail
    public Plan Plan { get; set; }                      // free | entrepreneur
    public PlanStatus PlanStatus { get; set; }          // active | past_due | canceled | downgraded_locked
    public bool Published { get; set; }                 // puede aparecer en /[slug]
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public BrandingConfig Branding { get; set; }        // primary_color, logo_url, etc. (Owned Entity)
    public ContactInfo Contact { get; set; }            // whatsapp, email, address (Owned Entity)
    public DateTime? PlanStartedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum BusinessType { Restaurant, Barber, Service, Retail }
public enum Plan { Free, Entrepreneur }
public enum PlanStatus { Active, PastDue, Canceled, DowngradedLocked }
```

**Migración EF Core:** `AddSaasFieldsToAffiliate` — agrega columnas, índice único en `Slug`, default `BusinessType='Service'` para los 6 afiliados existentes (vos los reasignás manualmente después).

### 2.2 Nueva entidad: `UserAffiliateMap`

```csharp
public class UserAffiliateMap
{
    public Guid Id { get; set; }
    public string SupabaseUserId { get; set; }         // sub claim del JWT de Supabase
    public string Email { get; set; }                  // denormalizado para búsquedas
    public Guid AffiliateId { get; set; }
    public Affiliate Affiliate { get; set; }
    public AffiliateRole Role { get; set; }            // Owner | Manager | Staff
    public DateTime CreatedAt { get; set; }
}

public enum AffiliateRole { Owner, Manager, Staff }
```

**Reglas:**
- Un `SupabaseUserId` puede tener múltiples filas (multi-negocio en plan entrepreneur)
- Un `AffiliateId` puede tener múltiples filas (varios usuarios administran un afiliado)
- Constraint único: `(SupabaseUserId, AffiliateId)` — un usuario no puede tener dos roles en el mismo afiliado
- En onboarding self-service, se crea con `Role=Owner`

**Migración:** `AddUserAffiliateMap` — tabla nueva con FK a `Affiliates`.

### 2.3 Catálogo público: proyección, NO tabla nueva

El frontend espera renderizar `/[slug]` con items por categoría. **No creamos tablas nuevas** — proyectamos las existentes según `BusinessType`:

| BusinessType | Fuente del catálogo público |
|---|---|
| `Restaurant` | `Product` (los que tienen flag `IsPubliclyVisible=true`) — agrupados por `Category` |
| `Barber` | `Service` filtrado por afiliado |
| `Service` | `Service` filtrado por afiliado |
| `Retail` | `InventoryItem` (los que tienen `IsPubliclyVisible=true` y `Stock > 0` o sin tracking) |

**Cambios mínimos a las entidades existentes:**

```csharp
// Product.cs — agregar:
public bool IsPubliclyVisible { get; set; } = false;
public string? Category { get; set; }
public string? ImageUrl { get; set; }
public int SortOrder { get; set; } = 0;
public bool IsDemo { get; set; } = false;

// Service.cs — agregar:
public bool IsPubliclyVisible { get; set; } = true;  // servicios suelen ser públicos
public string? Category { get; set; }
public string? ImageUrl { get; set; }
public int SortOrder { get; set; } = 0;
public bool IsDemo { get; set; } = false;

// InventoryItem.cs — agregar:
public bool IsPubliclyVisible { get; set; } = false;
public string? Category { get; set; }
public string? ImageUrl { get; set; }
public int SortOrder { get; set; } = 0;
public bool IsDemo { get; set; } = false;
```

**¿Por qué `IsDemo`?** Igual que en v1: items semilla creados durante onboarding no cuentan contra el límite del plan free hasta que el dueño los edite por primera vez.

**¿Por qué no JOIN/UNION en una vista SQL?** Porque las tres entidades tienen campos distintos (`InventoryItem` tiene stock, `Service` tiene duración) y queremos que el endpoint devuelva el shape correcto al frontend según `BusinessType`. Es lógica de aplicación, no de DB.

### 2.4 DTO de respuesta del catálogo público

```csharp
public record PublicCatalogResponse(
    AffiliateInfoDto Affiliate,
    List<CatalogCategoryDto> Categories,
    List<CatalogItemDto> Items,
    PlanCapabilitiesDto Capabilities
);

public record CatalogItemDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    string? Category,
    string? ImageUrl,
    bool IsDemo,
    // Campos opcionales según tipo (null si no aplica)
    int? DurationMinutes,           // service
    bool? RequiresBooking,          // service
    int? Stock,                     // retail (null = sin tracking)
    string[]? Allergens,            // restaurant (futuro)
    object? Modifiers               // restaurant entrepreneur (futuro)
);

public record PlanCapabilitiesDto(
    bool OnlinePayments,
    bool BookingCalendar,
    bool MenuModifiers,
    bool RealtimeStock,
    bool BrandingFull,
    bool HidePoweredBy,
    bool CustomDomain
);
```

El frontend YA tiene un tipo similar (lo que el CLI marcó ✅ en `capabilities.ts`). Se ajustan los campos para que matcheen exactamente.

### 2.5 Reglas de plan revisadas (sin cambio respecto al v1)

| Feature | Free | Entrepreneur |
|---|---|---|
| Afiliados por usuario | 1 | Ilimitado |
| Items publicables (por afiliado) | 10 | Ilimitado |
| Reservas con calendario | ❌ | ✅ |
| Pagos en línea (Stripe Connect) | ❌ | ✅ |
| Branding custom | Color limitado | Full |
| "Powered by MaalCa" footer | Forzado | Quitable |
| Dominio custom | ❌ | ✅ |

Implementación: enum `Plan` + servicio `PlanLimitService` que valida cada acción que requiera plan check.

---

## 3. Auth bridge: cómo Supabase y `maalca-api` conviven

### 3.1 Flujo completo

```
[Browser]
  │
  │ 1. Click "Login con Google"
  ▼
[Supabase Auth]
  │
  │ 2. OAuth dance con Google
  │ 3. Supabase emite JWT firmado con su llave privada
  │    - sub = supabase_user_id
  │    - email
  │    - exp, iat
  ▼
[maalca-web frontend]
  │
  │ 4. Guarda JWT en cookie httpOnly
  │ 5. En cada request a maalca-api agrega:
  │    Authorization: Bearer <supabase_jwt>
  ▼
[maalca-api]
  │
  │ 6. Middleware SupabaseAuthMiddleware:
  │    a. Extrae Bearer token
  │    b. Valida firma contra JWKS público de Supabase
  │       (cached: GET https://<project>.supabase.co/auth/v1/keys)
  │    c. Verifica exp, iss
  │    d. Extrae sub (supabase_user_id) y email
  │    e. Query: SELECT * FROM UserAffiliateMaps WHERE SupabaseUserId = sub
  │    f. Si NO existe → respuesta 401 con header X-Onboarding-Required: true
  │       (frontend redirige a /onboarding)
  │    g. Si existe → set HttpContext.User con claims:
  │       - sub
  │       - email
  │       - affiliate_ids: [guid1, guid2, ...]
  │       - active_affiliate_id: guid (header X-Affiliate-Id si vino, sino el primero)
  │       - role: Owner|Manager|Staff
  ▼
[Endpoint protegido]
  │
  │ 7. Lee HttpContext.User.GetActiveAffiliateId()
  │ 8. Filtra queries por AffiliateId
  │ 9. Verifica role para acciones sensibles
```

### 3.2 Código del middleware (esqueleto)

```csharp
// Maalca.Api/Middleware/SupabaseAuthMiddleware.cs
public class SupabaseAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SupabaseJwksCache _jwksCache;
    private readonly IAffiliateMapService _mapService;

    public async Task InvokeAsync(HttpContext context)
    {
        var token = ExtractBearerToken(context);
        if (token == null) { await _next(context); return; }

        // Si el token es JWT propio (admin), saltar al middleware de auth interno
        if (IsInternalJwt(token)) { await _next(context); return; }

        // Validar JWT de Supabase
        var principal = await ValidateSupabaseJwt(token);
        if (principal == null) { context.Response.StatusCode = 401; return; }

        var supabaseUserId = principal.FindFirst("sub")?.Value;
        var email = principal.FindFirst("email")?.Value;

        // Mapear a afiliados
        var maps = await _mapService.GetMapsForUser(supabaseUserId);
        if (maps.Count == 0)
        {
            context.Response.Headers["X-Onboarding-Required"] = "true";
            context.Response.StatusCode = 401;
            return;
        }

        // Resolver afiliado activo (header X-Affiliate-Id, o el primero)
        var requestedAffiliateId = context.Request.Headers["X-Affiliate-Id"].FirstOrDefault();
        var activeMap = maps.FirstOrDefault(m => m.AffiliateId.ToString() == requestedAffiliateId)
                        ?? maps.OrderBy(m => m.CreatedAt).First();

        // Construir ClaimsIdentity
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", supabaseUserId),
            new Claim("email", email),
            new Claim("active_affiliate_id", activeMap.AffiliateId.ToString()),
            new Claim("role", activeMap.Role.ToString()),
            new Claim("affiliate_ids", string.Join(",", maps.Select(m => m.AffiliateId)))
        }, "supabase");

        context.User = new ClaimsPrincipal(identity);
        await _next(context);
    }
}
```

**Notas:**
- `JwksCache` cachea las claves públicas de Supabase por 24h (rotan poco)
- `IsInternalJwt(token)` detecta si el token fue emitido por la API misma (issuer = `maalca-api`) vs Supabase (issuer = `https://<project>.supabase.co/auth/v1`)
- Soporta dual auth sin conflicto: tokens internos van por el pipeline existente, tokens Supabase van por este

### 3.3 Diferencias entre auth admin vs auth afiliado

| Aspecto | Admin interno (JWT propio) | Afiliado (Supabase) |
|---|---|---|
| Login | `POST /auth/login` (email + password) | OAuth Google via Supabase |
| Issuer | `maalca-api` | `https://<project>.supabase.co/auth/v1` |
| Claims | `sub` (user id de tu DB), `roles` | `sub` (supabase_user_id), `email` |
| Mapeo a afiliado | No aplica — admin ve todos | Via `UserAffiliateMap` |
| Refresh | Refresh token propio | Lo maneja Supabase SDK en frontend |
| Dónde se usa | Admin panel interno (futuro) | Dashboard `/space`, página pública con write |

---

## 4. Endpoints nuevos en `maalca-api`

### 4.1 Endpoints públicos (sin auth)

```
GET /api/public/affiliates/{slug}
  → AffiliateInfoDto (nombre, descripción, branding, contact, businessType, plan)
  → 404 si no existe o Published=false o PlanStatus=DowngradedLocked

GET /api/public/affiliates/{slug}/catalog
  → PublicCatalogResponse (incluye items proyectados según BusinessType)
  → cacheable: Cache-Control: public, max-age=60, stale-while-revalidate=300
```

### 4.2 Endpoints autenticados (Supabase auth)

```
POST /api/onboarding
  Body: { name, businessType }
  → Crea Affiliate + UserAffiliateMap (Owner) + categorías default + 3 demo items
  → Devuelve { affiliateId, slug }
  → 409 si ya tiene un afiliado y plan != entrepreneur
  → Todo en transacción (BeginTransaction de EF Core)

POST /api/affiliates                 (entrepreneur multi-negocio)
  Body: { name, businessType }
  → Igual que onboarding pero requiere plan=entrepreneur del usuario
  → 402 Payment Required si plan=free

GET /api/me/affiliates
  → Lista de afiliados del usuario actual
  → Para BusinessSwitcher

GET /api/affiliates/{id}             (existente, sin cambio)
PATCH /api/affiliates/{id}           (existente, validar role=Owner|Manager)

GET /api/affiliates/{id}/catalog-items
POST /api/affiliates/{id}/catalog-items
  Body: { name, description, price, category, imageUrl, type-specific fields }
  → Decide qué tabla insertar según Affiliate.BusinessType:
    - restaurant → INSERT en Products
    - barber, service → INSERT en Services
    - retail → INSERT en InventoryItems
  → Valida límite de plan (10 items reales en free)

PATCH /api/affiliates/{id}/catalog-items/{itemId}
DELETE /api/affiliates/{id}/catalog-items/{itemId}

GET /api/affiliates/{id}/categories
POST /api/affiliates/{id}/categories
  → Free: máx N por tipo (DEFAULT_CATEGORIES count)
  → Entrepreneur: ilimitado
```

### 4.3 Endpoints de billing (Stripe)

```
POST /api/billing/checkout-session
  → Crea Stripe Checkout Session, devuelve URL
  → Cliente redirige

POST /api/webhooks/stripe
  → Handler con verificación de signature
  → Eventos: checkout.session.completed, customer.subscription.updated, customer.subscription.deleted
  → Idempotencia: tabla StripeProcessedEvents (event_id PRIMARY KEY)
  → Lógica de downgrade: marca M-1 afiliados del usuario como DowngradedLocked, deja el más viejo activo
```

---

## 5. Cambios en `maalca-web` (frontend)

### 5.1 Cliente HTTP centralizado

```typescript
// src/lib/api-client.ts
import { createClient } from "@/lib/supabase/client";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL!;
// dev: http://localhost:5000
// prod: https://maalca-api.up.railway.app (o lo que sea)

export async function apiFetch<T>(
  path: string,
  init: RequestInit = {},
  options: { affiliateId?: string; skipAuth?: boolean } = {}
): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Content-Type", "application/json");

  if (!options.skipAuth) {
    const supabase = createClient();
    const { data: { session } } = await supabase.auth.getSession();
    if (session?.access_token) {
      headers.set("Authorization", `Bearer ${session.access_token}`);
    }
  }

  if (options.affiliateId) {
    headers.set("X-Affiliate-Id", options.affiliateId);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers });

  if (response.status === 401) {
    const onboarding = response.headers.get("X-Onboarding-Required");
    if (onboarding === "true") {
      window.location.href = "/onboarding";
      return Promise.reject(new Error("Onboarding required"));
    }
    throw new ApiError(401, "Unauthorized");
  }

  if (!response.ok) {
    const body = await response.text();
    throw new ApiError(response.status, body);
  }

  return response.json();
}
```

### 5.2 SSR de página pública

```typescript
// src/app/[slug]/page.tsx
import { notFound } from "next/navigation";

export const revalidate = 60; // ISR cada 60s

async function getCatalogData(slug: string) {
  try {
    const response = await fetch(
      `${process.env.API_BASE_URL}/api/public/affiliates/${slug}/catalog`,
      { next: { revalidate: 60, tags: [`affiliate:${slug}`] } }
    );
    if (response.status === 404) return null;
    if (!response.ok) throw new Error("API error");
    return response.json();
  } catch (e) {
    console.error(e);
    return null;
  }
}

export default async function PublicAffiliatePage({ params }: { params: { slug: string } }) {
  const data = await getCatalogData(params.slug);
  if (!data) notFound();

  const { affiliate, categories, items, capabilities } = data;
  const Template = TEMPLATES[affiliate.businessType];

  return <Template
    business={affiliate}
    categories={categories}
    items={items}
    capabilities={capabilities}
  />;
}
```

### 5.3 Tipos TypeScript desde API

**Importante: NO inventar tipos.** Usar uno de estos enfoques:

**Opción 1 (recomendada):** Generar tipos automáticamente desde OpenAPI/Swagger de `maalca-api`. Comando:
```bash
npx openapi-typescript https://api.maalca.com/swagger/v1/swagger.json -o src/lib/api-types.ts
```
Correr en CI cada vez que cambia la API.

**Opción 2:** Mantener tipos manualmente en `src/lib/api-types.ts` con disciplina de actualizar cuando cambia el backend. Riesgoso pero válido para empezar.

---

## 6. Plan de implementación por fases

### Fase A — Auth bridge (prerequisito de TODO)

Sin esto, ningún endpoint autenticado funciona.

| # | Tarea | Checkpoint | Repo |
|---|---|---|---|
| A-1 | Crear migration `AddUserAffiliateMap` | Tabla creada en dev | maalca-api |
| A-2 | Crear `IAffiliateMapService` y su implementación | Test unitario: GetMapsForUser devuelve correcto | maalca-api |
| A-3 | Implementar `SupabaseJwksCache` (HTTP client + cache 24h) | Llama a `/auth/v1/keys`, cachea | maalca-api |
| A-4 | Implementar `SupabaseAuthMiddleware` | Token Supabase válido → User poblado | maalca-api |
| A-5 | Registrar middleware en `Program.cs` antes de auth interno | Token interno sigue funcionando | maalca-api |
| A-6 | Endpoint `GET /api/me/affiliates` | Con token Supabase devuelve lista (vacía si onboarding pendiente) | maalca-api |
| A-7 | En `maalca-web`, crear `lib/api-client.ts` | Llamada de prueba a `/api/me/affiliates` funciona desde browser autenticado | maalca-web |

**Checkpoint global Fase A:** un usuario logueado en frontend puede llamar a `/api/me/affiliates` y la API responde correctamente con su lista (o 401 + header X-Onboarding-Required si no tiene mapping).

### Fase B — SaaS fields en Affiliate

| # | Tarea | Checkpoint | Repo |
|---|---|---|---|
| B-1 | Migration `AddSaasFieldsToAffiliate` (Slug, BusinessType, Plan, etc.) | Schema actualizado | maalca-api |
| B-2 | Asignar manualmente BusinessType a los 6 afiliados existentes via SQL UPDATE | TLD=restaurant, Pegote=barber, etc. | maalca-api (SQL manual) |
| B-3 | Generar slugs únicos para los 6 existentes | Cada afiliado tiene Slug único | maalca-api |
| B-4 | Endpoint `GET /api/public/affiliates/{slug}` | Retorna info, 404 si no existe o no Published | maalca-api |

**Checkpoint:** llamar `GET /api/public/affiliates/the-little-dominican` devuelve los datos del afiliado.

### Fase C — Catálogo público (proyección)

| # | Tarea | Checkpoint | Repo |
|---|---|---|---|
| C-1 | Migration `AddPublicCatalogFields` — agrega `IsPubliclyVisible`, `Category`, `ImageUrl`, `SortOrder`, `IsDemo` a `Product`, `Service`, `InventoryItem` | Migración aplicada | maalca-api |
| C-2 | Crear `IPublicCatalogService` con método `GetCatalog(slug)` | Devuelve `PublicCatalogResponse` según BusinessType | maalca-api |
| C-3 | Endpoint `GET /api/public/affiliates/{slug}/catalog` | Devuelve catálogo correcto para cada tipo | maalca-api |
| C-4 | En frontend, conectar `app/[slug]/page.tsx` para llamar a este endpoint via SSR | Página pública renderiza con data real | maalca-web |
| C-5 | Verificar que las 4 plantillas reciben las props correctas | Restaurant, Barber, Service, Retail renderizan | maalca-web |

**Checkpoint:** abrir `https://maalca.com/the-little-dominican` muestra el menú real desde la API.

### Fase D — Onboarding self-service

| # | Tarea | Checkpoint | Repo |
|---|---|---|---|
| D-1 | Crear `IOnboardingService.OnboardNewAffiliate(userId, email, name, businessType)` | Test: crea affiliate + map + categorías + 3 demos en transacción | maalca-api |
| D-2 | Endpoint `POST /api/onboarding` | Funciona con auth Supabase | maalca-api |
| D-3 | Conectar frontend `/onboarding` con endpoint | Form crea afiliado, redirige a `/space/[slug]?new=1` | maalca-web |
| D-4 | Verificar que aparecen los 3 demo items con badge | Visualmente correcto | maalca-web |

**Checkpoint:** un usuario nuevo se registra con Google → onboarding form → afiliado creado → página pública poblada con 3 demos.

### Fase E — CRUD de catálogo desde dashboard

| # | Tarea | Checkpoint | Repo |
|---|---|---|---|
| E-1 | Endpoints `GET/POST/PATCH/DELETE /api/affiliates/{id}/catalog-items` | Decide tabla según BusinessType | maalca-api |
| E-2 | Implementar `PlanLimitService.CanAddItem(affiliateId)` | Bloquea con 402 si free + 10 reales | maalca-api |
| E-3 | Conectar frontend forms de catálogo | Crear, editar, eliminar funciona | maalca-web |
| E-4 | Convertir `IsDemo=false` al primer edit | Test: demo + edit → cuenta hacia el límite | maalca-api |

### Fase F — Categorías

| # | Tarea | Checkpoint | Repo |
|---|---|---|---|
| F-1 | Crear entidad `Category` + migration | Tabla creada | maalca-api |
| F-2 | Endpoints `GET/POST/PATCH/DELETE /api/affiliates/{id}/categories` | Free bloquea agregar custom | maalca-api |
| F-3 | Backfill: crear categorías default para los 6 afiliados existentes | TLD tiene "Entradas", "Principales", etc. | maalca-api |
| F-4 | Conectar frontend de gestión de categorías | UI funciona | maalca-web |

### Fase G — Multi-negocio (entrepreneur)

| # | Tarea | Checkpoint | Repo |
|---|---|---|---|
| G-1 | Endpoint `POST /api/affiliates` (multi-negocio entrepreneur) | 402 si plan=free | maalca-api |
| G-2 | BusinessSwitcher consume `GET /api/me/affiliates` | Switcher muestra todos los afiliados | maalca-web |
| G-3 | Header `X-Affiliate-Id` cambia según selección | Backend filtra por afiliado activo | ambos |

### Fase H — Stripe + planes

| # | Tarea | Checkpoint | Repo |
|---|---|---|---|
| H-1 | Crear migration `StripeProcessedEvents` (idempotencia) | Tabla creada | maalca-api |
| H-2 | Endpoint `POST /api/billing/checkout-session` | Crea Stripe session | maalca-api |
| H-3 | Endpoint `POST /api/webhooks/stripe` con verificación de signature + idempotencia | Eventos duplicados ignorados | maalca-api |
| H-4 | Lógica de upgrade: `customer.subscription.updated` → `Affiliate.Plan = Entrepreneur` | Test: webhook → DB actualizada | maalca-api |
| H-5 | Lógica de downgrade: `customer.subscription.deleted` → política del MODEL_PLAN §1.3 | Test: 3 afiliados → 1 activo, 2 locked | maalca-api |
| H-6 | Conectar frontend UpgradeModal con checkout | Click "Upgrade" → Stripe → vuelta a `/space/[slug]?upgraded=1` | maalca-web |

---

## 7. Orden de ataque recomendado

```
[A] Auth bridge ──────────────► PRE-REQUISITO TODO
       │
       ▼
[B] SaaS fields en Affiliate
       │
       ▼
[C] Catálogo público (proyección) ──► PRIMER GANANCIA VISIBLE
       │                              (puedo abrir /the-little-dominican
       │                               y ver TLD renderizado)
       ▼
[D] Onboarding self-service ──► UX completa para nuevos usuarios
       │
       ▼
[E] CRUD catálogo ──► Edits desde dashboard
       │
       ▼
[F] Categorías
       │
       ▼
[G] Multi-negocio (entrepreneur)
       │
       ▼
[H] Stripe + planes ──► Monetización real
```

**Tiempo estimado** (con CLI ejecutando, vos revisando):
- A: 1-2 días
- B: 0.5 día
- C: 1-2 días
- D: 1 día
- E: 1 día
- F: 0.5 día
- G: 0.5 día
- H: 1-2 días

**Total: 1-2 semanas reales.** Mucho menos que el v1 estimaba porque casi todo el dominio ya está construido.

---

## 8. Riesgos y huecos

**Lo que este plan no resuelve y vas a topar:**

1. **`Product`, `Service`, `InventoryItem` son tres entidades distintas con DTOs distintos.**
   El frontend espera UN tipo `CatalogItem`. La proyección que diseñamos en §2.4 unifica en el response, pero el CRUD requiere POST diferentes según `BusinessType`. Hay que ser disciplinado: el endpoint `POST /api/affiliates/{id}/catalog-items` es **un solo endpoint** que internamente decide qué entidad crear, NO tres endpoints distintos. Esto evita que el frontend tenga que saber qué tipo es el negocio.

2. **Webhook de Supabase para usuarios eliminados.**
   Si un usuario se borra en Supabase Dashboard, sus filas en `UserAffiliateMap` quedan huérfanas. Esto NO causa security issues (al no tener token válido nunca pasan por el middleware), pero queda data muerta. Solución futura: suscribirse a webhook de Supabase `auth.user.deleted`. **NO MVP.**

3. **Dominio personalizado para Entrepreneur.**
   El plan dice "dominio custom" pero no hay infraestructura para esto: certificados SSL, DNS, routing. Es trabajo serio. Para MVP, "dominio custom" se interpreta como subdomain `<slug>.maalca.com` con wildcard SSL. **Custom apex domain queda para después.**

4. **CORS.**
   `maalca-web` (Vercel/Next.js) y `maalca-api` (Railway) están en dominios distintos. Hay que configurar CORS en `maalca-api` con origen específico. NO usar `*` — usar lista explícita: `https://maalca.com`, `https://*.maalca.com`, y dev.

5. **Storage de imágenes.**
   Tu API actual no tiene un sistema de upload de imágenes documentado. Las plantillas frontend esperan `imageUrl`. Hay que decidir:
   - Supabase Storage (gratis, integra bien con auth Supabase)
   - Cloudflare R2 (barato a escala)
   - S3
   Recomendación: Supabase Storage para MVP, ya estás en su ecosistema. **Pero NO está en este plan, es una tarea aparte.**

6. **Tipos TypeScript desincronizados con DTOs C#.**
   Si el backend cambia un campo y el frontend no se entera, breaks silenciosos. Solución: generar tipos desde Swagger en CI. **Recomiendo agregarlo en Fase A-7.**

7. **Migración de datos del seed existente.**
   Los 6 afiliados de seed (Pegote, TLD, etc.) hoy NO tienen `Slug`, `BusinessType`, `Plan`. La migración B-1 los crea con valores default. **Hay que asignarlos manualmente** (Fase B-2). Esto es una tarea de DBA, no de código. Anota en algún lugar qué BusinessType corresponde a cada uno antes de aplicar la migration.

8. **El frontend que el CLI hizo asume Next.js API routes.**
   Hay archivos `src/app/api/...` que el CLI creó pensando que iban a vivir ahí. Esos archivos hay que **eliminarlos o convertirlos en proxies a `maalca-api`**. Auditoría manual obligatoria antes de Fase A.

9. **Plan check duplicado: frontend y backend.**
   `plan-limits.ts` en frontend valida UI (oculta botones, muestra modals). El backend valida en serio (rechaza con 402). **Ambos son necesarios** — frontend por UX, backend por seguridad. La fuente de verdad es backend.

10. **e-CF queda fuera, pero `Invoice` ya existe.**
    Cuando llegue el momento de e-CF, NO hay que crear entidades nuevas — extender `Invoice` con campos fiscales (NCF, XML firmado, estado DGII). El alcance v2 NO toca esto, pero el modelo está preparado.

---

## 9. Definition of Done (este plan)

- [ ] Migration `AddUserAffiliateMap` aplicada
- [ ] Migration `AddSaasFieldsToAffiliate` aplicada, 6 afiliados existentes tienen Slug + BusinessType correctos
- [ ] Migration `AddPublicCatalogFields` aplicada
- [ ] `SupabaseAuthMiddleware` funciona, tokens Supabase válidos pasan
- [ ] Endpoint público `/api/public/affiliates/{slug}/catalog` responde correcto para los 4 tipos
- [ ] Frontend `/[slug]` renderiza data real desde API en SSR
- [ ] Onboarding crea afiliado + map + 3 demos en transacción
- [ ] CRUD de catálogo funciona desde dashboard
- [ ] Plan free bloquea con 402 al intentar el item #11
- [ ] Multi-negocio funciona en entrepreneur
- [ ] Stripe webhook idempotente
- [ ] Downgrade marca M-1 afiliados como locked
- [ ] CORS configurado correctamente
- [ ] OpenAPI/Swagger generado y tipos TypeScript en sincro

---

## 10. Reglas anti-improvisación para CLI (v2)

> **Antes de tocar código, lee este documento Y verifica el estado actual de `maalca-api`.**
>
> NO crear `catalog_items`, `menu_items`, `service_items`, `retail_items` — esas tablas del v1 NO aplican. El catálogo público es proyección sobre `Product`/`Service`/`InventoryItem` ya existentes.
>
> NO renombrar `Affiliate` a `Business` en backend. El alias se hace solo en frontend (`type Business = Affiliate`).
>
> NO migrar a SQL Server. Postgres se queda.
>
> NO implementar CQRS/MediatR. La API usa Service Layer + Minimal APIs — respeta el patrón.
>
> NO romper el seed de los 6 afiliados existentes (Pegote, TLD, MaalCa LLC, BritoColor, Dr. Pichardo, Masa Tina).
>
> NO crear endpoints CRUD separados por tipo (`POST /api/products`, `POST /api/services`). UN solo endpoint `POST /api/affiliates/{id}/catalog-items` que decide internamente.
>
> Tokens Supabase y tokens internos coexisten. El middleware identifica el issuer y rutea.
>
> Header `X-Affiliate-Id` solo es válido si el usuario tiene mapping a ese afiliado. Verificar SIEMPRE.
>
> Eliminar Next.js API routes en `maalca-web` que el CLI v1 haya creado — ya no van a existir, todo va a `maalca-api`.
>
> Después de cada fase, correr `dotnet build` en `maalca-api` y `npm run build` en `maalca-web`. Verificar checkpoint correspondiente antes de pasar a la siguiente fase.
