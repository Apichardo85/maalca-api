# Spec — Espacio v2 (maalca-api / backend .NET)

**Repo:** `maalca-api` · **Branch de trabajo:** `develop` → QA de Ciri → merge a `main`
**Alcance de este documento:** SOLO backend (.NET/ASP.NET Core, Railway, PostgreSQL). No incluye ningún cambio de frontend — ver documento hermano `spec-maalca-web-espacio-v2.md`.

---

## Fase 0 — Corrección de datos de producción (CERRADA ✅)

**Causa raíz confirmada:** el seed en `Program.cs` (bloque `if (!db.Set<Affiliate>().Any())`, líneas ~841–846) nunca asignaba `Plan` al crear los afiliados reales, por lo que heredaban el default `Plan.Free` de la entidad (`src/Maalca.Domain/Entities/User.cs`). El endpoint `/api/space/{slug}` y `PlanLimitService.GetMaxItems()` leían ese mismo campo correctamente — la lógica de negocio nunca estuvo rota, el dato sí.

**Acción ya ejecutada por Ciri en producción (Railway):**
```sql
UPDATE "Affiliates"
SET "Plan" = 1,
    "PlanStatus" = 0,
    "PlanStartedAt" = NOW()
WHERE "Slug" IN ('the-little-dominicana', 'pegote-barber', 'britocolor');
```
Estado verificado: **activados** (Plan=1/Entrepreneur, PlanStatus=0/Active) para los tres afiliados reales.

**Pendiente — tarea de código para esta fase (previene que el bug regrese en staging):**

- [ ] En el bloque de seed de `Program.cs`, agregar `Plan = Plan.Entrepreneur, PlanStatus = PlanStatus.Active, PlanStartedAt = DateTime.UtcNow` explícito a las filas de `Pegote Barbershop`, `BritoColor` y `Little Dominicana Restaurant` (los tres casos reales/pagos). El resto de afiliados demo (`Dr. Pichardo`, `Masa Tina`, `MaalCa LLC`) se quedan en el default `Free` salvo indicación contraria.
- [ ] No requiere migración nueva — es edición del seed existente. No afecta producción (el seed solo corre si la tabla está vacía), solo protege futuras instancias de staging/desarrollo limpio.

**Criterio de aceptación:** al levantar una base nueva desde cero (staging), los tres afiliados reales nacen ya con `Plan=Entrepreneur` sin necesidad de UPDATE manual.

---

## Confirmación de arquitectura de tenant (hallazgo de esta investigación)

Para que quede documentado y no se vuelva a investigar: **`business.id` (usado en `/space/[slug]`) y `affiliate.id` (usado en `/dashboard/[affiliateId]` y en `/api/affiliates/by-slug/:slug`) son el mismo GUID.** No existe una tabla `businesses` separada — `/api/space/{slug}` arma un `BusinessDto` proyectando directamente desde `db.Affiliates`. Es una única fuente de verdad (tabla `Affiliates`). No hay migración a medio terminar ni unificación de tenant pendiente a nivel de datos.

Lo que sí sigue siendo distinto y debe mantenerse así: `/dashboard/[affiliateId]` resuelve por una **key estática hardcodeada** en el repo de frontend (`affiliatesConfig`), mientras que `/space/[slug]` resuelve contra la base real. Ese es un tema de frontend (routing/config), documentado en el spec hermano — no requiere cambios de backend.

---

## Fase A — Contrato de datos `Canal` (Nivel 1)

**Contexto de producto:** los afiliados necesitan publicar formas de contacto (WhatsApp, correo, teléfono) sin conocimientos técnicos. Se definió un modelo en 3 niveles de madurez; **solo se implementa Nivel 1 ahora** (manual). Los niveles 2 (enlace validado/oEmbed) y 3 (OAuth oficial con Meta/WhatsApp Cloud API) quedan en backlog, pero el modelo de datos se diseña extensible desde ya para que monten sin migración futura.

### Entidad `Canal`

| Campo | Tipo | Notas |
|---|---|---|
| `Id` | `Guid` | PK |
| `AffiliateId` | `Guid` | FK a `Affiliates` |
| `Tipo` | `enum CanalTipo` | Ver abajo |
| `Metodo` | `enum CanalMetodo` | Ver abajo |
| `ValorCrudo` | `string` | Lo que el usuario escribió (número, email) |
| `EnlaceGenerado` | `string` | `wa.me/...`, `mailto:...`, `tel:...` |
| `NombreVisible` | `string?` | Label opcional para mostrar en la página pública |
| `Verificado` | `bool` | Default `false`. Sin uso funcional en Nivel 1 (reservado para N2/N3) |
| `OauthRef` | `string?` | Nullable. Reservado para Nivel 3, sin lógica ahora |
| `Orden` | `int` | Orden de despliegue en la página pública |
| `Activo` | `bool` | Default `true` |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Estándar `AuditableEntity`/`BaseEntity` del proyecto |

```csharp
public enum CanalTipo { WhatsApp, Email, Telefono, Facebook, Instagram, TikTok }
public enum CanalMetodo { Manual, Enlace, Oauth }
```

**Importante — alcance de esta fase:** los únicos valores que el backend debe **validar y aceptar** ahora son `Tipo ∈ {WhatsApp, Email, Telefono}` y `Metodo = Manual`. `Facebook/Instagram/TikTok` y `Metodo ∈ {Enlace, Oauth}` quedan en el enum (para no migrar después) pero **cualquier request que los use debe devolver 400** hasta que se construya Nivel 2/3. No implementar lógica de oEmbed ni OAuth en esta fase.

### Migración requerida
- [ ] Nueva tabla `Canales` con las columnas de arriba, FK a `Affiliates.Id`, índice por `AffiliateId`.
- [ ] Agregar `DbSet<Canal> Canales` a `AppDbContext`.

### Endpoints nuevos

```
GET    /api/affiliates/{id}/canales
POST   /api/affiliates/{id}/canales
PATCH  /api/affiliates/{id}/canales/{canalId}
DELETE /api/affiliates/{id}/canales/{canalId}
```

- Seguir el mismo patrón de autorización ya usado en `catalog-items`: verificar `active_affiliate_id` del JWT contra `{id}` de la ruta.
- **Generación server-side del enlace** (no confiar en el frontend): al recibir `Tipo=WhatsApp` + `ValorCrudo`, el backend limpia el número (solo dígitos, valida longitud mínima) y arma `https://wa.me/<numero>`. Para `Email` → validar formato → `mailto:<email>`. Para `Telefono` → `tel:<numero>`.
- Validación de request: rechazar (400) si `Tipo` o `Metodo` están fuera del subconjunto permitido en esta fase (ver arriba).

### Exponer canales en la página pública y en el aggregator

- [ ] Agregar `List<CanalDto>` a `BusinessDto` (respuesta de `GET /api/space/{slug}`) para que el dashboard del afiliado los muestre.
- [ ] Agregar `List<CanalDto>` a la respuesta de `GET /api/public/affiliates/{slug}` para que la página pública renderice los botones de contacto.

**Criterio de aceptación de Fase A:** un afiliado puede guardar WhatsApp/Email/Teléfono vía API, el enlace se genera correctamente en el backend, y ambos endpoints de lectura (privado y público) devuelven la lista de canales activos ordenada por `Orden`.

---

## Fase B — Soporte al Dashboard compositivo (datos, no UI)

El frontend necesita saber **qué módulos están realmente activos y con datos** para armar el dashboard "base + una tarjeta por módulo activo". Hoy `Affiliate.Modules` es un string separado por comas sin validar contra lo que el backend puede servir de verdad.

**Hallazgo de esta investigación — módulos con endpoint real hoy:**
- Catálogo (`/api/affiliates/{id}/catalog-items`, sirve Products/Services/InventoryItems según `BusinessType`)
- Página/Espacio (`/api/space/{slug}`, `/api/public/affiliates/{slug}`)
- Métricas (`/api/metrics/overview`)

**Existen como tabla pero SIN ningún endpoint que las exponga hoy** (no ofrecer como "activables" en frontend hasta construirlas): `Appointments` (Citas), `Invoices` (Facturación), `Campaigns`, `GiftCards` (Cupones), `TeamMembers` (Equipo), `Customers` (CRM), `QueueEntries`, `InventoryMovements`.

### Tareas
- [ ] Endpoint (puede ser un campo agregado a `/api/space/{slug}`) que devuelva `ModulosActivos: string[]`, calculado a partir de `Affiliate.Modules` **filtrado contra una whitelist de módulos con endpoint real** (Catálogo, Página, Métricas). No devolver módulos del string crudo que no tengan soporte de API — evita que el frontend intente pintar una tarjeta de datos inexistentes.
- [ ] `GET /api/metrics/overview` — confirmar que devuelve datos suficientes para las 4 tarjetas KPI base propuestas en el dashboard (Visitas, Items publicados, Escaneos QR, Clics a canales). Si "Escaneos QR" y "Clics a canales" no se están trackeando aún, marcarlo como gap explícito (ver Fase C).

**Criterio de aceptación:** el frontend puede pedir un solo payload (`/api/space/{slug}` extendido) y saber exactamente qué tarjetas de módulo debe pintar, sin lógica de whitelist duplicada en el cliente.

### Actualización de implementación (post-diseño original)

Lo implementado difiere del diseño original de una forma mejor, documentada aquí para que no se pierda:

- **No se reemplazó `Affiliate.Modules`.** Se agregó una columna nueva `Affiliate.ModulosActivos` (migración `AddAffiliateModulosActivos`, solo `AddColumn`, sin lógica de mapeo). Motivo: `Modules` (legacy) sigue siendo leído por `GET /api/affiliates/{id}` (`AffiliateService.GetAffiliateAsync`), que alimenta el dashboard viejo `/dashboard/[affiliateId]` todavía vivo en producción. Sobrescribir `Modules` habría roto secciones de UI del dashboard viejo para los afiliados reales. Los dos campos **coexisten**: `Modules` para el dashboard viejo, `ModulosActivos` como fuente de verdad canónica para todo lo nuevo (Fase B en adelante).
- `/api/space/{slug}` fue actualizado para leer `ModulosActivos` (no `Modules`) al armar el `BusinessDto`.
- **Convención hacia adelante:** cualquier código nuevo (onboarding, futuro admin panel) debe escribir tokens canónicos directamente en `ModulosActivos` — no crear lógica de traducción legacy→canónico en tiempo de request. Documentado en comentarios en `User.cs` (entidad) y `ModuleCatalog.cs`.

**Backfill ejecutado (producción, confirmado):**
```sql
UPDATE "Affiliates"
SET "ModulosActivos" = 'catalog,page,metrics'
WHERE "Slug" IN ('pegote-barber', 'britocolor', 'the-little-dominicana', 'dr-pichardo', 'masa-tina', 'maalca');
```
Nota: el slug real de MaalCa LLC en producción es `maalca`, no `maalca-llc` como asumía el seed de staging usado de referencia — corregido antes de ejecutar contra producción.

**Gap conocido, fuera de alcance por decisión explícita:** producción tiene 3 negocios reales y publicados descubiertos durante este backfill que no estaban en la lista original de 6: `kenia-bbq` (Kenia BBQ), `lisa-cocina` (lisa cocina), `reina-style` (Reina Style). Todos con `Published=true`. Necesitan el mismo backfill de `ModulosActivos='catalog,page,metrics'` antes de poder usar el Dashboard v2, pero quedan deliberadamente fuera de este Paso 2 hasta que se decida incluirlos.

---

## Fase C — Tracking mínimo para KPIs del dashboard (si no existe)

Verificar antes de construir (no asumir): revisar si hay algún registro de eventos de escaneo de QR o clic en canal. Si no existe:

- [ ] Tabla simple `EventoInteraccion` (`AffiliateId`, `Tipo` [`QrScan`, `CanalClick`, `PageView`], `CanalId` nullable, `Timestamp`). Reutilizar si ya existe algo similar en `AgentExecution` o eventos — **verificar primero**, no crear tabla duplicada.
- [ ] Endpoint `POST /api/affiliates/{id}/events` — **ya existe** (`Program.cs`, ruta confirmada). Verificar su payload actual y si ya cubre estos tipos antes de extenderlo.

**Nota:** esta fase depende de qué tan completo esté `POST /api/affiliates/{id}/events` hoy. Investigar su implementación real antes de escribir código nuevo — es candidato a ya estar resuelto parcialmente.

---

## Fase D — Extensión de Onboarding (soporte a "Onboarding rápido")

`OnboardingRequest` hoy captura: `Name`, `BusinessType`, `WhatsApp?`, `Description?`. El flujo rápido de v2 (nombre, categoría, color, logo, 1 canal principal) necesita más campos.

- [ ] Extender `OnboardingRequest` con `PrimaryColor?: string` y `LogoUrl?: string`.
- [ ] `OnboardingService.OnboardAsync` debe persistir esos campos en la fila de `Affiliate` recién creada (ya existen las columnas `PrimaryColor` y `LogoUrl` en la entidad — no requiere migración).
- [ ] El canal principal capturado en onboarding (ej. WhatsApp) debe crear automáticamente una fila en `Canales` (Fase A) en vez de solo llenar `Affiliate.WhatsApp` — para que desde el día uno el afiliado tenga su primer canal ya estructurado en el modelo nuevo.

**Criterio de aceptación:** al completar el onboarding rápido, la fila de `Affiliate` queda con color/logo, y existe al menos 1 fila en `Canales` asociada.

---

## No incluido en este documento (ver spec de frontend)

- Shell visual, navegación, composición de tarjetas del dashboard.
- Editor "Diseñar mi Espacio" (UI, preview instantáneo vs bajo demanda).
- Layout por categoría de negocio, selector de imagen destacada por item.
- Pantalla de Módulos (marketplace) y sección "Próximamente".

## Nota sobre endpoints ya existentes que el frontend puede reusar sin más trabajo de backend

`PATCH /api/affiliates/{id}/profile` ya acepta `Name`, `Description`, `WhatsApp`, `LogoUrl`, `CoverImageUrl`, `ContactEmail`, `Address`, `Website`, `PrimaryColor`. El editor de Fase 3 (frontend) puede consumir este endpoint tal cual para todo excepto Canales — no requiere endpoint nuevo para esa parte del editor.
