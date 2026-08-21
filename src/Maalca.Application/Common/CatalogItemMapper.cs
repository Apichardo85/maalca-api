using Maalca.Application.Common.DTOs;
using Maalca.Domain.Entities;

namespace Maalca.Application.Common;

// Centralizes Product/Service/InventoryItem -> CatalogItemDto mapping en un solo lugar —
// usado por CatalogCrudService (dashboard) y PublicCatalogService. Mapea sobre entidades ya
// materializadas (LINQ-to-Objects), nunca dentro de un .Select() de IQueryable — así se puede
// llamar libremente a JsonArrayField.Parse/TokenList.Parse sin que EF Core intente traducirlos
// a SQL (que fallaría en runtime).
public static class CatalogItemMapper
{
    public static CatalogItemDto FromProduct(Product p) => new(
        p.Id, p.Name, p.Description, p.Price, p.Category, p.ImageUrl, p.SortOrder, p.IsDemo,
        null, null, p.Status,
        p.DescriptionEn, TokenList.Parse(p.Periods), TokenList.Parse(p.WeekDays), TokenList.Parse(p.Flags),
        p.Featured, p.Popular, p.VideoUrl, JsonArrayField.Parse<string>(p.Images), p.NameEn);

    public static CatalogItemDto FromService(Service s) => new(
        s.Id, s.Name, s.Description, s.Price, s.Category, s.ImageUrl, s.SortOrder, s.IsDemo,
        s.DurationMinutes, null, s.Status,
        s.DescriptionEn, Images: JsonArrayField.Parse<string>(s.Images), NameEn: s.NameEn);

    public static CatalogItemDto FromInventoryItem(InventoryItem i) => new(
        i.Id, i.Name, i.Description, i.UnitPrice, i.Category, i.ImageUrl, i.SortOrder, i.IsDemo,
        null, i.Quantity, i.Status,
        i.DescriptionEn, Images: JsonArrayField.Parse<string>(i.Images), NameEn: i.NameEn);
}
