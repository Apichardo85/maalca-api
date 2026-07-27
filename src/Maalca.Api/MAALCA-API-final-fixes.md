# Final Fixes for maalca-api — Closing the Free Plan Epic

> **Status**: Ready to implement — closes the last gaps before E2E smoke test
> **Scope**: 3 small changes, ~45 min total
> **Owner**: API team
> **Context**: Correction #2 (`AffiliateMilestones`) is deployed and working. These are the last polish items.

---

## What works today (validated)

- ✅ `MilestoneService` persists to `AffiliateMilestones` with `UNIQUE INDEX (AffiliateId, Key)`
- ✅ `link_shared` fires via `POST /api/affiliates/{id}/events` (whitelisted, idempotent)
- ✅ `first_product_added` fires via `PATCH /api/affiliates/{id}/catalog-items/{itemId}` when `wasDemo == true`, with `source:"demo_edited"` metadata
- ✅ `whats_app_configured` fires via `PATCH /api/affiliates/{id}/profile` when `result.WhatsApp` is not empty
- ✅ `GET /api/space/{slug}` reads progress from `MilestoneService.GetCompletedKeysAsync`

---

## Fix #1: Fire `first_product_added` on `POST /api/affiliates/{id}/catalog-items`

**Effort**: 5 min
**Why it matters**: `SpaceDashboard` has a button labeled "+ Agregar mi primer item real" that routes to `/space/{slug}/catalog/new`. That form submits via POST, not PATCH. Without this fix, users who add an item from scratch (rather than editing a demo) never see `first_product_added` tick off. The button copy literally describes the milestone — but the milestone doesn't fire.

**File**: `Maalca.Api/Program.cs`, the `MapPost("/api/affiliates/{id}/catalog-items", ...)` handler.

**Current**:

```csharp
app.MapPost("/api/affiliates/{id}/catalog-items", async (
    HttpContext ctx, ICatalogCrudService catalogCrud, Guid id,
    CreateCatalogItemRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();
    try
    {
        var item = await catalogCrud.CreateItemAsync(id, request);
        return Results.Created($"/api/affiliates/{id}/catalog-items/{item.Id}", item);
    }
    catch (InvalidOperationException ex) when (ex.Message.StartsWith("Plan limit"))
    {
        return Results.Problem(statusCode: 402, title: "Payment Required", detail: ex.Message);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
});
```

**Required change**: inject `IMilestoneService` and fire after successful creation. Idempotency is handled by the unique index, so no need to check state — just fire it every time. The `UNIQUE INDEX (AffiliateId, Key)` makes repeat calls no-ops.

```csharp
app.MapPost("/api/affiliates/{id}/catalog-items", async (
    HttpContext ctx, ICatalogCrudService catalogCrud, IMilestoneService milestones,
    Guid id, CreateCatalogItemRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();
    try
    {
        var item = await catalogCrud.CreateItemAsync(id, request);

        // Items created via POST are always non-demo (demos are seeded during onboarding only).
        // Fire idempotently — the unique index makes subsequent calls no-ops.
        await milestones.MarkAsync(id, MilestoneKeys.FirstProductAdded,
            metadata: $$$"""{"itemId":"{{{item.Id}}}","source":"created"}""");

        return Results.Created($"/api/affiliates/{id}/catalog-items/{item.Id}", item);
    }
    catch (InvalidOperationException ex) when (ex.Message.StartsWith("Plan limit"))
    {
        return Results.Problem(statusCode: 402, title: "Payment Required", detail: ex.Message);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
});
```

**Acceptance**:
- Creating a catalog item from `/space/{slug}/catalog/new` returns 201 and fires `first_product_added`
- Subsequent item creates don't error (idempotent via unique index)
- Metadata distinguishes this from the demo-edit path: `source:"created"` vs `source:"demo_edited"`

---

## Fix #2: `whats_app_configured` — transition-only firing (optional polish)

**Effort**: 10 min
**Why it matters**: This is a polish item, not a bug. The end-user experience is correct today. But the current implementation re-fires the milestone on every `PATCH /profile`, even when WhatsApp was already configured. The unique index swallows the duplicate, but it generates noise in logs (one `DbUpdateException` per PATCH after the first).

**Current** (in `Program.cs` around line 597):

```csharp
if (!string.IsNullOrWhiteSpace(result.WhatsApp))
    await milestones.MarkAsync(id, MilestoneKeys.WhatsAppConfigured);
```

This fires whenever the PATCH result has WhatsApp, regardless of whether WhatsApp was already set before.

**Required change**: only fire on the null/empty → value transition. The cleanest way is to have `AffiliateService.UpdateProfileAsync` return both the updated profile AND a flag indicating whether WhatsApp just became configured.

**File 1**: `Maalca.Application/Common/DTOs/AffiliateDtos.cs` (or wherever `UpdateAffiliateProfileResult` lives).

Add a wrapper result type:

```csharp
public record UpdateProfileResult(
    AffiliatePublicProfileDto Profile,
    bool WhatsAppWasJustConfigured
);
```

**File 2**: `Maalca.Application/Services/AffiliateService.cs`, `UpdateProfileAsync` method.

```csharp
public async Task<UpdateProfileResult?> UpdateProfileAsync(
    Guid affiliateId, UpdateAffiliateProfileRequest request)
{
    var affiliate = await _db.Affiliates.FirstOrDefaultAsync(a => a.Id == affiliateId);
    if (affiliate is null) return null;

    var hadWhatsApp = !string.IsNullOrWhiteSpace(affiliate.WhatsApp);

    // ... apply patches as before ...
    if (request.WhatsApp is not null) affiliate.WhatsApp = request.WhatsApp;
    // ... other fields ...

    await _db.SaveChangesAsync();

    var nowHasWhatsApp = !string.IsNullOrWhiteSpace(affiliate.WhatsApp);
    var whatsAppWasJustConfigured = !hadWhatsApp && nowHasWhatsApp;

    return new UpdateProfileResult(
        Profile: MapToProfileDto(affiliate),
        WhatsAppWasJustConfigured: whatsAppWasJustConfigured
    );
}
```

**File 3**: `Maalca.Api/Program.cs`, the `PATCH /api/affiliates/{id}/profile` handler.

```csharp
var result = await affiliateService.UpdateProfileAsync(id, request);
if (result is null)
    return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });

if (result.WhatsAppWasJustConfigured)
    await milestones.MarkAsync(id, MilestoneKeys.WhatsAppConfigured);

return Results.Ok(result.Profile);
```

**Acceptance**:
- First time WhatsApp is set: milestone fires, row inserted, 200 response
- Subsequent PATCHes that don't change WhatsApp: no milestone call, no DbUpdateException in logs
- PATCH that clears WhatsApp (sets to null/empty): no milestone fire (correctly — we're not regressing it)
- PATCH that goes empty → value (re-configures after clearing): milestone is already in the table from before, no double-insert, no error

**Skip this fix if**: you'd rather close the epic now and treat this as tech debt. The user experience is correct today; only operational logs are noisy.

---

## Fix #3: Correction #1 — Demo items publicly visible + 5 per type

**Effort**: 30 min (mostly copy curation)
**Why it matters**: The original epic specified 5 demo items per business type, all `IsPubliclyVisible = true`. Current state is 3 items, `IsPubliclyVisible = false`. This means:

1. Public pages `/{slug}` show **zero items** until owner manually activates each one — breaks the "shareable link in 60 seconds" promise of the epic.
2. `SpaceDashboard` shows 3 items in the demo banner instead of the curated 5.

**File 1**: `Maalca.Application/Services/DemoItemTemplates.cs` (or wherever the templates live).

Extend each `BusinessType` branch from 3 to 5 items. Curated copy below.

```csharp
public static IReadOnlyList<DemoItem> ForType(BusinessType type) => type switch
{
    BusinessType.Restaurant =>
    [
        new("Mofongo de Cerdo", "Plátano verde majado con chicharrón crocante", "Platos fuertes", 14.99m),
        new("Pollo Guisado", "Pollo en salsa criolla con sazón casera", "Platos fuertes", 12.99m),
        new("Sancocho Dominicano", "Siete carnes, viandas, plátano y aguacate", "Sopas", 16.99m),
        new("Tres Golpes", "Mangú, huevo, queso frito y salami", "Desayunos", 10.99m),
        new("Tostones con Queso", "Plátano verde frito con queso derretido", "Acompañantes", 6.99m),
    ],
    BusinessType.Barber =>
    [
        new("Corte Clásico", "Fade o degradado a elección", "Cortes", 25m),
        new("Corte + Barba", "Servicio completo, toalla caliente incluida", "Combos", 40m),
        new("Solo Barba", "Perfilado y toalla caliente", "Barba", 18m),
        new("Diseño y Líneas", "Cortes con diseño personalizado", "Cortes", 35m),
        new("Corte Niño", "Menores de 12", "Cortes", 18m),
    ],
    BusinessType.Service =>
    [
        new("Consulta Inicial", "Evaluación y diagnóstico", "Consultas", 50m),
        new("Servicio Estándar", "Atención completa", "Servicios", 120m),
        new("Mantenimiento", "Revisión periódica", "Mantenimiento", 75m),
        new("Paquete Mensual", "4 sesiones al mes", "Paquetes", 400m),
        new("Emergencia / Urgente", "Disponibilidad 24/7", "Servicios", 200m),
    ],
    BusinessType.Retail =>
    [
        new("Producto Destacado A", "Descripción de tu producto principal", "Destacados", 29.99m),
        new("Producto B", "Descripción corta", "Categoría 1", 19.99m),
        new("Producto C", "Descripción corta", "Categoría 1", 24.99m),
        new("Producto D", "Descripción corta", "Categoría 2", 14.99m),
        new("Producto E", "Descripción corta", "Categoría 2", 9.99m),
    ],
    _ => []
};
```

**File 2**: `Maalca.Application/Services/OnboardingService.cs`, in the demo seeding loop.

```csharp
foreach (var (template, idx) in demoTemplates.Select((t, i) => (t, i)))
{
    _db.Products.Add(new Product
    {
        AffiliateId = affiliate.Id,
        Name = template.Name,
        Description = template.Description,
        Category = template.Category,
        Price = template.Price,
        IsDemo = true,
        IsPubliclyVisible = true,    // ← changed from false
        Status = "Active",
        SortOrder = idx,
    });
}
```

**SEO mitigation** (already in the epic spec, worth re-confirming): the `/{slug}` public page should emit `<meta name="robots" content="noindex">` until the affiliate has the `first_product_added` milestone. This is a maalca-web change, not API — adding here as a reminder.

**Acceptance**:
- New affiliate creation seeds exactly 5 demo items per business type
- All seeded items have `IsPubliclyVisible = true`
- `GET /api/public/affiliates/{slug}/catalog` returns 5 items immediately after onboarding
- `GET /api/space/{slug}` returns 5 items, all flagged `isDemo: true`

---

## Order of operations

These are independent. Suggested order based on impact:

1. **Fix #3** (Correction #1) — biggest UX impact (the "shareable link" promise of the epic depends on this)
2. **Fix #1** (POST catalog-items milestone) — closes the last functional gap in milestones
3. **Fix #2** (transition-only WhatsApp) — polish, skip if time-constrained

**Total effort**: 35–45 min for all three.

---

## What's NOT in scope

These are intentionally deferred:

- **Refactor milestones to live in services rather than HTTP handlers** — current pattern is consistent; refactor when a third use case appears that has this problem
- **Authorization consistency** — `POST /events` uses `active_affiliate_id` claim, other endpoints use `UserAffiliateMap`. Works fine on Free plan (1 affiliate per user). Worth addressing when multi-business support lands.
- **The `Key` column name in `AffiliateMilestone`** — differs from spec's `MilestoneKey` but functional with inline comment. No reason to migrate the column now.

---

## After these fixes land

The Free Plan Workspace Provisioning epic is **functionally complete**. End-to-end smoke test on staging:

1. Fresh Google login → `/onboarding` form
2. Submit "Test Negocio" + Restaurant
3. Land on `/space/test-negocio?new=1` with **5 demo items** in the banner
4. Open `/test-negocio` in incognito — **all 5 items visible** to the public
5. Edit a demo → checklist marks `first_product_added` ✅
6. OR add a new item from scratch → also marks `first_product_added` ✅
7. Configure WhatsApp in settings → checklist marks `whats_app_configured` ✅
8. Click "Copy link" → checklist marks `link_shared` ✅
9. Reload → all three persist
10. Edit another item → no double-fire, no console error

If all 10 pass, the epic ships.
