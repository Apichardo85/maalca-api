# maalca-api Implementation Plan

> **Project:** maalca-api (.NET 8 Backend)
> **Purpose:** Implement API contracts required by maalca-web frontend
> **Based on:** maalca-integration-master-backlog.md

> **Estado (Julio 2026):** Phases 1-4 de este documento están **implementadas y en producción** — todas las interfaces/servicios (`IAuthService`, `IAffiliateService`, `ICustomerService`, `IAppointmentService`, `IServiceService`, `IInventoryService`, `IQueueService`, `ITeamService`, `IProductService`, `IInvoiceService`, `IGiftCardService`, `ICampaignService`, `IMetricsService`, `ILeadService`) están registradas y con endpoints activos en `Program.cs`. Este documento queda como referencia de contrato original.
> Para el trabajo nuevo (Espacio v2, Julio 2026 en adelante) ver **[`spec-maalca-api-espacio-v2.md`](./spec-maalca-api-espacio-v2.md)** y la sección **Phase 5** al final de este archivo.

---

## 📋 Implementation Phases

### Phase 1: Foundation (Unblock Frontend) — ✅ Implementado
1. **Authentication Module** - JWT-based auth with refresh tokens
2. **Multi-Tenant Configuration** - Affiliate/branding settings
3. **Database Setup** - EF Core with code-first migrations

### Phase 2: Core Business Modules — ✅ Implementado
4. **Customers (CRM)** - Full CRUD with pagination
5. **Appointments** - Scheduling with conflict detection
6. **Services** - Service catalog management
7. **Inventory** - Stock tracking with movements
8. **Metrics** - KPIs and analytics

### Phase 3: Advanced Features — ✅ Implementado
9. **Virtual Queue** - Real-time with SignalR
10. **Team Management** - Employee CRUD
11. **Products** - Store catalog
12. **Invoicing** - Billing system
13. **Gift Cards** - Digital gift cards
14. **Campaigns** - Marketing campaigns

### Phase 4: Public Endpoints — ✅ Implementado
15. **Leads** - Property and CiriSonic lead capture

### Phase 5: Espacio v2 — ✅ Implementado (Fases 0-D), ver detalle abajo
16. **Canal** (Nivel 1) - Contacto WhatsApp/Email/Teléfono con enlace generado server-side
17. **ModulosActivos** - Whitelist de módulos con endpoint real para el dashboard compositivo
18. **EventoInteraccion** - Tabla y endpoint de tracking construidos, **huérfanos** (0 filas, nadie los invoca — confirmado por auditoría en Phase 6)
19. **Onboarding extendido** - PrimaryColor/LogoUrl + creación automática de Canal WhatsApp

### Phase 6: Agregación de KPIs — ⚠️ Parcial (1 de 4), ver detalle abajo
20. **`kpis` en `GET /api/space/{slug}`** - Contrato `{valor, disponible}` por KPI; solo `itemsPublicados` tiene dato real hoy

---

## 🏗️ Architecture Overview

```mermaid
graph TB
    subgraph "maalca-api"
        API[API Layer<br/>Minimal APIs]
        CTRL[Controllers<br/>(if needed)]
        SW[SignalR Hub]
        
        subgraph "Application Layer"
            AUTH[Auth Service]
            AFF[Affiliate Service]
            CRM[Customer Service]
            APPT[Appointment Service]
            INV[Inventory Service]
            QUEUE[Queue Service]
            TEAM[Team Service]
            PROD[Product Service]
            INVoice[Invoice Service]
            GC[GiftCard Service]
            CAMP[Campaign Service]
            LEAD[Lead Service]
        end
        
        subgraph "Infrastructure"
            DB[(EF Core<br/>SQL Server)]
            JWT[JWT Handler]
            EMAIL[Email Service]
        end
    end
    
    WEB[maalca-web<br/>Next.js] --> API
    WEB --> SW
```

---

## 📦 Project Structure

```
src/
├── Maalca.Api/
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Controllers/
│   │   └── (minimal APIs in modules)
│   ├── Hubs/
│   │   └── QueueHub.cs
│   └── Middleware/
│       └── JwtMiddleware.cs
│
├── Maalca.Application/
│   ├── Common/
│   │   ├── Interfaces/
│   │   ├── DTOs/
│   │   └── Behaviors/
│   └── Dependencies.cs
│
├── Maalca.Domain/
│   ├── Common/
│   │   ├── BaseEntity.cs
│   │   └── AuditableEntity.cs
│   ├── Entities/
│   │   └── (shared entities)
│   └── Enums/
│
├── Maalca.Infrastructure/
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   ├── Services/
│   │   └── EmailService.cs
│   └── Identity/
│       └── JwtSettings.cs
│
└── Modules/
    ├── Auth/
    ├── Affiliates/
    ├── Customers/
    ├── Appointments/
    ├── Services/
    ├── Inventory/
    ├── Queue/
    ├── Team/
    ├── Products/
    ├── Invoices/
    ├── GiftCards/
    ├── Campaigns/
    └── Leads/
```

---

## 🔐 Phase 1: Authentication (API-REQ-001, 001b)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/login` | Authenticate user, return JWT |
| POST | `/api/auth/refresh` | Refresh JWT token |

### Request/Response Models

**POST /api/auth/login**
```json
// Request
{
  "email": "string",
  "password": "string"
}

// Response
{
  "token": "string",
  "refreshToken": "string",
  "user": {
    "id": "guid",
    "email": "string",
    "affiliateId": "string",
    "role": "string"
  }
}
```

**POST /api/auth/refresh**
```json
// Request
{
  "token": "string",
  "refreshToken": "string"
}

// Response
{
  "token": "string",
  "refreshToken": "string"
}
```

### Implementation Tasks
- [x] Install NuGet: Microsoft.AspNetCore.Authentication.JwtBearer
- [x] Configure JWT settings in appsettings.json
- [x] Create JWT generation service
- [x] Create AuthController with login/refresh endpoints
- [x] Implement password hashing (bcrypt)
- [x] Create user entity and repository

---

## 🏢 Phase 1: Multi-Tenant Config (API-REQ-002)

### Endpoint Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}` | Get affiliate configuration |

### Response Model

```json
{
  "id": "string",
  "branding": {
    "logo": "string",
    "primaryColor": "#hex",
    "secondaryColor": "#hex",
    "heroImage": "string"
  },
  "modules": ["string"],
  "features": {
    "enableQueue": true,
    "enableInventory": true
  },
  "settings": {}
}
```

### Implementation Tasks
- [x] Create Affiliate entity
- [x] Create Affiliate repository
- [x] Implement GET endpoint with tenant isolation

---

## 👥 Phase 2: Customers/CRM (API-REQ-003)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/customers` | List with pagination |
| POST | `/api/affiliates/{affiliateId}/customers` | Create customer |
| PUT | `/api/affiliates/{affiliateId}/customers/{id}` | Update customer |
| DELETE | `/api/affiliates/{affiliateId}/customers/{id}` | Delete customer |

### Query Parameters
- `page` (default: 1)
- `limit` (default: 20)
- `search` (optional)
- `status` (optional)

### Response Model (Paginated)
```json
{
  "data": [],
  "total": 0,
  "page": 1,
  "totalPages": 1
}
```

---

## 📅 Phase 2: Appointments (API-REQ-004, 004b)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/appointments` | List appointments |
| POST | `/api/affiliates/{affiliateId}/appointments` | Create appointment |
| PATCH | `/api/affiliates/{affiliateId}/appointments/{id}` | Update status |
| GET | `/api/affiliates/{affiliateId}/services` | List services |
| POST | `/api/affiliates/{affiliateId}/services` | Create service |
| PUT | `/api/affiliates/{affiliateId}/services/{id}` | Update service |
| DELETE | `/api/affiliates/{affiliateId}/services/{id}` | Delete service |

### Features Required
- Conflict detection (double-booking prevention)
- Status workflow: scheduled → confirmed → in-progress → completed/cancelled

---

## 📦 Phase 2: Inventory (API-REQ-005)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/inventory` | List inventory |
| POST | `/api/affiliates/{affiliateId}/inventory/movements` | Register movement |

### Movement Types
- `in` - Stock addition
- `out` - Stock reduction

---

## 🔄 Phase 3: Virtual Queue (API-REQ-006)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/queue` | Get queue state |
| POST | `/api/affiliates/{affiliateId}/queue` | Add to queue |
| PATCH | `/api/affiliates/{affiliateId}/queue/{id}` | Update entry status |

### SignalR Hub
- Hub URL: `/hubs/queue?affiliateId={id}`
- Events: `QueueUpdated`, `PositionChanged`, `Called`

### Status Values
- `waiting` → `in_service` → `completed` | `no_show`

---

## 👨‍💼 Phase 3: Team Management (API-REQ-007)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/team` | List team members |
| POST | `/api/affiliates/{affiliateId}/team` | Add team member |
| PUT | `/api/affiliates/{affiliateId}/team/{id}` | Update member |
| DELETE | `/api/affiliates/{affiliateId}/team/{id}` | Remove member |

---

## 🛒 Phase 3: Products (API-REQ-008)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/products` | List products |
| POST | `/api/affiliates/{affiliateId}/products` | Create product |
| PUT | `/api/affiliates/{affiliateId}/products/{id}` | Update product |
| DELETE | `/api/affiliates/{affiliateId}/products/{id}` | Delete product |

---

## 🧾 Phase 3: Invoicing (API-REQ-009)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/invoices` | List invoices |
| POST | `/api/affiliates/{affiliateId}/invoices` | Create invoice |

---

## 🎁 Phase 3: Gift Cards (API-REQ-010)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/giftcards` | List gift cards |
| POST | `/api/affiliates/{affiliateId}/giftcards` | Create gift card |

### Features
- Generate unique code
- Track balance

---

## 📊 Phase 3: Metrics (API-REQ-011)

### Endpoint Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/metrics` | Get KPIs |

### Response Model
```json
{
  "revenue": 0,
  "appointments": 0,
  "customers": 0,
  "inventoryValue": 0,
  "queueLength": 0
}
```

---

## 📢 Phase 3: Campaigns (API-REQ-012)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/affiliates/{affiliateId}/campaigns` | List campaigns |
| POST | `/api/affiliates/{affiliateId}/campaigns` | Create campaign |

---

## 📧 Phase 4: Public Endpoints (Leads)

### Endpoints Required

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/metrics/overview` | Homepage stats |
| POST | `/api/leads/properties` | Property lead capture |
| POST | `/api/leads/cirisonic` | CiriSonic lead capture |

---

## ⚙️ Common Infrastructure

### Error Response Format
```json
{
  "error": {
    "code": "string",
    "message": "string",
    "details": {}
  }
}
```

### Pagination Standard
```
?page=1&limit=20
```

### Tenant Isolation
- All affiliate endpoints require `{affiliateId}` in path
- Alternative: `X-Tenant-Id` header

---

## 🚀 Recommended Implementation Order

1. **Week 1**: Foundation
   - Project setup + NuGet packages
   - Database context
   - Authentication (JWT)
   - Affiliate config

2. **Week 2**: Core CRUD
   - Customers
   - Appointments + Services
   - Inventory

3. **Week 3**: Core CRUD cont.
   - Team
   - Products
   - Metrics

4. **Week 4**: Advanced
   - Invoicing
   - Gift Cards
   - Campaigns

5. **Week 5**: Real-time + Public
   - SignalR Queue
   - Lead endpoints

6. **Julio 2026 (Espacio v2)**: ver Phases 5-6 abajo
   - Canal, ModulosActivos, EventoInteraccion (escritura), Onboarding extendido
   - KPIs del dashboard: `itemsPublicados` real; `visitas`/`escaneosQr`/`clicsCanales` bloqueados en decisión de producto (frontend debe empezar a disparar eventos)

---

## 🌐 Phase 5: Espacio v2 (Julio 2026)

> Documento fuente completo con contexto de producto, migraciones y hallazgos de investigación: **[`spec-maalca-api-espacio-v2.md`](./spec-maalca-api-espacio-v2.md)**. Esta sección es un resumen de estado, no reemplaza ese documento.

### Fase 0 — Corrección de datos de producción — ✅ Cerrada
Bug de seed: los 3 afiliados reales (`pegote-barber`, `britocolor`, `the-little-dominicana`) heredaban `Plan.Free` por default en vez de `Plan.Entrepreneur`. Corregido en producción vía `UPDATE` manual, y en el seed de `Program.cs` para que una base nueva no reproduzca el bug.

### Fase A — Entidad `Canal` (contacto Nivel 1) — ✅ Implementado
Nueva tabla `Canales` (migración `AddCanales`). Solo `Tipo ∈ {WhatsApp, Email, Telefono}` + `Metodo=Manual` aceptados hoy (400 para el resto); enum ya incluye Facebook/Instagram/TikTok/Enlace/Oauth para no migrar en Nivel 2/3.

| Method | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/affiliates/{id}/canales` | Lista canales del afiliado |
| POST | `/api/affiliates/{id}/canales` | Crea canal, genera enlace server-side (`wa.me/`, `mailto:`, `tel:`) |
| PATCH | `/api/affiliates/{id}/canales/{canalId}` | Actualiza ValorCrudo/NombreVisible/Orden/Activo |
| DELETE | `/api/affiliates/{id}/canales/{canalId}` | Elimina canal |

Expuesto en `BusinessDto` (`GET /api/space/{slug}`) y `AffiliatePublicDto` (`GET /api/public/affiliates/{slug}`), filtrado a `Activo=true`, ordenado por `Orden`.

### Fase B — Dashboard compositivo — ✅ Implementado (con cambio de diseño documentado)
`ModulosActivos: string[]` agregado a `BusinessDto`, filtrado contra whitelist (`catalog`, `page`, `metrics`). **Cambio respecto al diseño original:** no se reemplazó `Affiliate.Modules` (legacy, todavía usado por el dashboard viejo `/dashboard/[affiliateId]`) — se agregó una columna nueva `Affiliate.ModulosActivos` que coexiste con ella. Detalle completo y justificación en el spec fuente.

**Backfill de producción ejecutado (2026-07-04):** los 6 afiliados reales/demo (`pegote-barber`, `britocolor`, `the-little-dominicana`, `dr-pichardo`, `masa-tina`, `maalca`) tienen `ModulosActivos='catalog,page,metrics'`. Verificado antes/después contra staging y producción.

**Gap abierto:** 3 negocios reales publicados (`kenia-bbq`, `lisa-cocina`, `reina-style`) no estaban en el alcance original y quedaron sin este backfill — pendiente de decisión.

### Fase C — Tracking de interacciones — ✅ Escritura construida, ⚠️ **huérfana** (confirmado por auditoría, ver Phase 6)
Tabla `EventoInteraccion` (`QrScan`, `CanalClick`, `PageView`) + endpoint público `POST /api/public/affiliates/{slug}/events` (anónimo — visitantes de la página pública no tienen JWT, por eso no se extendió el endpoint autenticado existente `POST /api/affiliates/{id}/events`, que sigue siendo solo para `link_shared`).

**Gap confirmado (auditoría Phase 6, 2026-07-04):** nadie invoca este endpoint todavía — ni backend ni frontend. `EventosInteraccion` tiene **0 filas** en staging y producción. El sistema de analytics que sí está activo en `maalca-web` (GA4 vía `useAnalytics.ts`) es un sistema separado para páginas de marketing (ciriwhispers/editorial) — no alimenta esta tabla ni el dashboard del afiliado. Cerrar este gap requiere una decisión de producto (dónde/cuándo dispara el evento la página pública) — no se resuelve solo con backend.

### Fase D — Onboarding extendido — ✅ Implementado
`OnboardingRequest`/`OnboardingResponse` con `PrimaryColor?`/`LogoUrl?`. El WhatsApp de onboarding crea automáticamente su primera fila en `Canales` (reutiliza `ICanalService.CreateAsync` de Fase A), todo dentro de la misma transacción — validado con rollback forzado contra staging (falla mid-transacción → cero filas huérfanas).

### Phase 6 — Agregación de KPIs (Paso 3) — ⚠️ Parcialmente implementado (1 de 4 KPIs con dato real)
`GET /api/space/{slug}` ahora devuelve un campo `kpis` con contrato `{ valor, disponible }` por KPI, para que el frontend distinga dato real de "sin datos todavía" sin adivinar por un `0` ambiguo:

```json
"kpis": {
  "visitas": { "valor": null, "disponible": false },
  "itemsPublicados": { "valor": 66, "disponible": true },
  "escaneosQr": { "valor": null, "disponible": false },
  "clicsCanales": { "valor": null, "disponible": false }
}
```

| KPI | `disponible` | Fuente |
|---|---|---|
| `itemsPublicados` | `true` | Derivado del catálogo existente (`ProductCount`/`realCount`), sin evento nuevo |
| `visitas` | `false` | `EventoInteraccion` tipo `PageView` — endpoint existe, **0 filas**, nadie lo invoca |
| `escaneosQr` | `false` | `EventoInteraccion` tipo `QrScan` — endpoint existe, **0 filas**, nadie lo invoca |
| `clicsCanales` | `false` | `EventoInteraccion` tipo `CanalClick` — endpoint existe, **0 filas**, nadie lo invoca |

**No se agregó tracking nuevo por decisión explícita del producto** (no le corresponde a esta sesión decidir dónde/cómo se dispara un evento desde la página pública). Para cerrar `visitas`/`escaneosQr`/`clicsCanales` falta, específicamente: que la página pública (`/space/[slug]`, visitante anónimo) llame a `POST /api/public/affiliates/{slug}/events` con `type: "page_view"` al cargar, `type: "qr_scan"` cuando corresponda, y `type: "canal_click"` (+ `canalId`) al hacer clic en un botón de contacto — trabajo de frontend, probablemente parte de retomar Fase 2/3 de `maalca-web`.

---

*Generated: Marzo 2026*
*Actualizado: Julio 2026 — Phase 5 (Espacio v2, Fases 0-D) y Phase 6 (agregación de KPIs, parcial)*
