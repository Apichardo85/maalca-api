# Spec: 3 Endpoints Required from maalca-api

> **Status**: Blocking the dual-write remediation in maalca-web
> **Repo**: `maalca-api`
> **Effort**: 3–5h total
> **Owner**: API team
> **Priority**: P0 — blocks staging deploy of corrected frontend

---

## Why these 3 endpoints

The maalca-web team is mid-flight on a forward-fix to eliminate dual-writes to Supabase and route all reads/writes through maalca-api. To complete that work, three endpoints are missing. Each is justified below with the exact frontend call site that needs it.

---

## Endpoint 1: `GET /api/space/{slug}`

### Purpose

Single-shot aggregator for the `/space/{slug}` dashboard page in maalca-web. Replaces three separate Supabase reads that exist today (`businesses`, `catalog_items`, `onboarding_progress`).

### Why one endpoint instead of three

The dashboard page in maalca-web is a Next.js server component that renders once per request. Each separate `fetch` adds round-trip latency (especially relevant since Railway PG is a separate hop from Vercel). One aggregator endpoint:

- p50 page load goes from ~3 round trips → 1
- Removes the need for the frontend to know the affiliate's internal `Guid` (it knows the slug, not the UUID)
- Centralizes the access control check (one ownership verification per page load, not three)

### Frontend call site

```tsx
// src/app/space/[slug]/page.tsx
const apiRes = await fetch(`${process.env.MAALCA_API_URL}/api/space/${slug}`, {
  headers: { Authorization: `Bearer ${session.access_token}` },
  cache: 'no-store',
});

if (apiRes.status === 404) redirect('/onboarding');
if (apiRes.status === 403) redirect('/');
const data: SpaceResponse = await apiRes.json();

return <SpaceDashboard {...data} publicUrl={publicUrl} isNew={isNew === '1'} />;
```

### Required response shape

```csharp
public record SpaceResponse(
    BusinessDto Business,
    IReadOnlyList<SpaceItemDto> Items,
    int ProductCount,        // real items only (IsDemo = false)
    ProgressDto Progress
);

public record BusinessDto(
    Guid Id,
    string Slug,
    string Name,
    string BusinessType,     // "Restaurant" | "Barber" | "Service" | "Retail"
    string Plan,             // "free" | "entrepreneur" — lowercase
    string? Whatsapp,
    string? PrimaryColor
);

public record SpaceItemDto(
    Guid Id,
    string Name,
    string? Category,
    bool IsDemo,
    bool Active              // Status == "Active"
);

public record ProgressDto(
    bool FirstProductAdded,
    bool WhatsAppConfigured,
    bool LinkShared
);
```

JSON serialization should use camelCase (`first_product_added`, `whats_app_configured`, `link_shared` if you've configured snake_case globally — match what the rest of the API does). The frontend already expects `first_product_added` per the existing `SpaceDashboard` prop type, so confirm naming convention.

### Implementation

```csharp
app.MapGet("/api/space/{slug}", async (
    HttpContext ctx,
    AppDbContext db,
    IAffiliateMapService mapService,
    IMilestoneService milestones,   // see addendum Correction #2
    string slug) =>
{
    var supabaseUserId = ctx.User.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(supabaseUserId))
        return Results.Unauthorized();

    var affiliate = await db.Affiliates
        .FirstOrDefaultAsync(a => a.Slug == slug);
    if (affiliate is null)
        return Results.NotFound();

    // Authorization: caller must have a map to this affiliate
    var hasAccess = await db.UserAffiliateMaps
        .AnyAsync(m => m.SupabaseUserId == supabaseUserId
                    && m.AffiliateId == affiliate.Id);
    if (!hasAccess)
        return Results.Forbid();

    var items = await db.Products
        .Where(p => p.AffiliateId == affiliate.Id)
        .OrderBy(p => p.SortOrder)
        .Select(p => new SpaceItemDto(
            p.Id,
            p.Name,
            p.Category,
            p.IsDemo,
            p.Status == "Active"))
        .ToListAsync();

    var realCount = items.Count(i => !i.IsDemo);

    // If MilestoneService isn't deployed yet, return all false — frontend handles it
    var completedKeys = await milestones.GetCompletedKeysAsync(affiliate.Id);

    return Results.Ok(new SpaceResponse(
        new BusinessDto(
            affiliate.Id,
            affiliate.Slug!,
            affiliate.Name,
            affiliate.BusinessType.ToString(),
            affiliate.Plan.ToString().ToLower(),
            affiliate.Whatsapp,
            affiliate.PrimaryColor),
        items,
        realCount,
        new ProgressDto(
            FirstProductAdded: completedKeys.Contains(MilestoneKeys.FirstProductAdded),
            WhatsAppConfigured: completedKeys.Contains(MilestoneKeys.WhatsAppConfigured),
            LinkShared: completedKeys.Contains(MilestoneKeys.LinkShared))));
})
.RequireAuthorization();
```

### Acceptance criteria

- 200 OK with full `SpaceResponse` for the slug's owner
- 401 if no valid JWT
- 403 if caller has no `UserAffiliateMap` row for this affiliate
- 404 if slug doesn't exist
- `Items` array ordered by `SortOrder` ascending
- `ProductCount` reflects items where `IsDemo == false` only
- If `MilestoneService` isn't deployed yet, returns all `progress.*` as `false` (graceful degradation — don't 500)

---

## Endpoint 2: `PATCH /api/affiliates/{id}/catalog-items/{itemId}`

### Purpose

Edit an existing catalog item — primarily for the "edit demo item" flow where the user updates a seeded item to make it their own. When this happens, the item's `IsDemo` flag flips to `false` and the `FirstProductAdded` milestone fires.

Today maalca-api has `POST`, `GET`, and `DELETE` for catalog items but no `PATCH`. Without it, the maalca-web edit form has nowhere to send updates except Supabase — which is exactly what we're trying to stop.

### Frontend call site

```tsx
// src/app/space/[slug]/catalog/[id]/edit/EditForm.tsx (via proxy)
const apiRes = await fetch(
  `${process.env.MAALCA_API_URL}/api/affiliates/${affiliateId}/catalog-items/${itemId}`,
  {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ name, description, category, price }),
  },
);
```

### Required request shape

```csharp
public record UpdateCatalogItemRequest(
    string? Name,
    string? Description,
    string? Category,
    decimal? Price,
    string? ImageUrl,
    bool? IsPubliclyVisible,
    string? Status           // "Active" | "Inactive"
);
```

All fields optional — PATCH semantics. Only fields present in the request body are updated. Use nullable types to distinguish "not provided" from "explicit null."

### Behavior on demo items

When a demo item is patched, the service MUST:

1. Set `IsDemo = false` (regardless of what was in the request body)
2. Fire the `first_product_added` milestone (idempotent, via `MilestoneService.MarkAsync`)
3. Apply the field updates from the request

This is the "edit your demo item to make it real" UX. The user doesn't see this flag — they just edit the item and it stops looking like a demo.

### Implementation

```csharp
app.MapPatch("/api/affiliates/{id}/catalog-items/{itemId}", async (
    HttpContext ctx,
    ICatalogCrudService catalogCrud,
    IMilestoneService milestones,
    Guid id,
    Guid itemId,
    UpdateCatalogItemRequest request) =>
{
    var supabaseUserId = ctx.User.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(supabaseUserId))
        return Results.Unauthorized();

    try
    {
        var (item, wasDemo) = await catalogCrud.UpdateAsync(
            supabaseUserId, id, itemId, request);

        // Demo → real transition fires the milestone
        if (wasDemo)
        {
            await milestones.MarkAsync(
                id,
                MilestoneKeys.FirstProductAdded,
                metadata: $$"""{"itemId":"{{itemId}}","source":"demo_edited"}""");
        }

        return Results.Ok(item);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.RequireAuthorization();
```

The `ICatalogCrudService.UpdateAsync` signature should return a tuple `(item, wasDemo)` where `wasDemo` is `true` only if `IsDemo` was `true` before the update. This lets the endpoint decide whether to fire the milestone without leaking that logic into the service.

```csharp
// ICatalogCrudService addition
Task<(CatalogItemDto Item, bool WasDemo)> UpdateAsync(
    string supabaseUserId,
    Guid affiliateId,
    Guid itemId,
    UpdateCatalogItemRequest request);
```

Service implementation outline:

```csharp
public async Task<(CatalogItemDto, bool)> UpdateAsync(
    string supabaseUserId, Guid affiliateId, Guid itemId, UpdateCatalogItemRequest request)
{
    // 1. Verify ownership (same pattern as other catalog methods)
    var hasAccess = await _db.UserAffiliateMaps
        .AnyAsync(m => m.SupabaseUserId == supabaseUserId && m.AffiliateId == affiliateId);
    if (!hasAccess) throw new UnauthorizedAccessException();

    // 2. Load item, ensure it belongs to this affiliate
    var product = await _db.Products
        .FirstOrDefaultAsync(p => p.Id == itemId && p.AffiliateId == affiliateId)
        ?? throw new KeyNotFoundException();

    var wasDemo = product.IsDemo;

    // 3. Apply patches (only fields explicitly provided)
    if (request.Name is not null) product.Name = request.Name;
    if (request.Description is not null) product.Description = request.Description;
    if (request.Category is not null) product.Category = request.Category;
    if (request.Price is not null) product.Price = request.Price.Value;
    if (request.ImageUrl is not null) product.ImageUrl = request.ImageUrl;
    if (request.IsPubliclyVisible is not null) product.IsPubliclyVisible = request.IsPubliclyVisible.Value;
    if (request.Status is not null) product.Status = request.Status;

    // 4. If it was a demo, it's not anymore
    if (wasDemo) product.IsDemo = false;

    await _db.SaveChangesAsync();

    return (MapToDto(product), wasDemo);
}
```

### Acceptance criteria

- `PATCH` with partial body updates only provided fields
- Patching a demo item flips `IsDemo` to `false` and fires `first_product_added` milestone
- Patching a real item (already `IsDemo = false`) does NOT re-fire the milestone (idempotent — `MarkAsync` no-ops)
- 401 without JWT, 403 without ownership, 404 if item doesn't exist
- 400 on invalid data (e.g. negative price)
- Returns updated item DTO

---

## Endpoint 3: `GET /api/affiliates/by-slug/{slug}`

### Purpose

Resolve a slug to an `affiliateId` (Guid) for callers that have only the slug from URL params. Used by maalca-web proxy routes that receive a slug but need to call existing maalca-api endpoints that take `affiliateId:guid`.

### Why this is the smallest of the three

The proxy routes in maalca-web look like:

```ts
// src/app/api/space/[slug]/catalog/route.ts
export async function POST(req, { params }) {
  const { slug } = await params;
  // Problem: existing endpoint is POST /api/affiliates/{id:guid}/catalog-items
  //          We have the slug, not the GUID.
  // Need: a way to translate slug → affiliateId
}
```

Two options:

**Option A** (current spec): add `GET /api/affiliates/by-slug/{slug}` and let the proxy do one extra hop.

**Option B**: add slug-accepting variants of every catalog endpoint (`POST /api/affiliates/by-slug/{slug}/catalog-items`, etc.). More endpoints, more surface area, more code paths to keep in sync.

Option A is simpler. The extra hop is one cached call (the proxy can cache slug→guid mappings in memory or via a header).

### Frontend call site

```ts
async function resolveAffiliateId(slug: string, token: string): Promise<string> {
  const res = await fetch(
    `${process.env.MAALCA_API_URL}/api/affiliates/by-slug/${slug}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (!res.ok) throw new Error(`Could not resolve slug: ${res.status}`);
  const data = await res.json();
  return data.id;
}
```

### Required response shape

Minimal — just enough for the resolver use case:

```csharp
public record AffiliateSlugLookupDto(Guid Id, string Slug, string Name);
```

### Implementation

```csharp
app.MapGet("/api/affiliates/by-slug/{slug}", async (
    HttpContext ctx,
    AppDbContext db,
    string slug) =>
{
    var supabaseUserId = ctx.User.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(supabaseUserId))
        return Results.Unauthorized();

    var affiliate = await db.Affiliates
        .Where(a => a.Slug == slug)
        .Select(a => new { a.Id, a.Slug, a.Name })
        .FirstOrDefaultAsync();
    if (affiliate is null)
        return Results.NotFound();

    var hasAccess = await db.UserAffiliateMaps
        .AnyAsync(m => m.SupabaseUserId == supabaseUserId
                    && m.AffiliateId == affiliate.Id);
    if (!hasAccess)
        return Results.Forbid();

    return Results.Ok(new AffiliateSlugLookupDto(
        affiliate.Id, affiliate.Slug!, affiliate.Name));
})
.RequireAuthorization();
```

### Acceptance criteria

- 200 with `{ id, slug, name }` for slug owner
- 401 without JWT
- 403 if caller has no map to this affiliate
- 404 if slug doesn't exist
- Should be fast — single indexed query (`Slug` is already unique-indexed)

---

## Summary table

| # | Endpoint | Verb | Effort | Blocks |
|---|---|---|---|---|
| 1 | `/api/space/{slug}` | GET | 1.5–2h | maalca-web Commit 3 (dashboard page read) |
| 2 | `/api/affiliates/{id}/catalog-items/{itemId}` | PATCH | 1.5h | maalca-web edit form (and demo-item flip flow) |
| 3 | `/api/affiliates/by-slug/{slug}` | GET | 30min | maalca-web proxy routes that have slug but need GUID |

**Total**: 3.5–4h of work on maalca-api.

---

## Order of implementation

These can be built in parallel — none depend on the others. If the team has to sequence:

1. **First**: #3 (`by-slug`) — smallest, unblocks the most maalca-web work
2. **Second**: #2 (`PATCH catalog-items`) — unblocks the demo-edit flow
3. **Third**: #1 (`GET /api/space/{slug}`) — biggest payoff, but maalca-web can stub the response with a hardcoded shape while waiting

---

## Dependencies on the addendum

Endpoint #1 references `IMilestoneService.GetCompletedKeysAsync` and endpoint #2 references `IMilestoneService.MarkAsync`. Both are from **addendum Correction #2** (persist milestones in `AffiliateMilestones` table).

If the addendum corrections haven't been deployed yet:
- Endpoint #1 can ship with milestones returning all `false` (graceful)
- Endpoint #2 can ship and skip the milestone call (it'll be a no-op until `IMilestoneService` exists)

But the right order is: addendum corrections first, then these 3 endpoints. Otherwise endpoint #2 has to be retrofitted with the milestone firing later.

---

## What to send the API team

Just this file. Each section is self-contained and pasteable as a sub-issue if your tracker prefers that. The acceptance criteria for each endpoint are testable with curl + a valid Supabase JWT against staging.
