using Maalca.Application.Common.DTOs;
using Maalca.Domain.Entities;

namespace Maalca.Application.Common;

// Centralizes Product -> CatalogItemDto mapping so the menu-style fields
// (DescriptionEn/Periods/WeekDays/Flags/Featured/Popular) are only wired in one
// place — used by both CatalogCrudService (dashboard) and PublicCatalogService.
public static class CatalogItemMapper
{
    public static CatalogItemDto FromProduct(Product p) => new(
        p.Id, p.Name, p.Description, p.Price, p.Category, p.ImageUrl, p.SortOrder, p.IsDemo,
        null, null, p.Status,
        p.DescriptionEn, TokenList.Parse(p.Periods), TokenList.Parse(p.WeekDays), TokenList.Parse(p.Flags),
        p.Featured, p.Popular);
}
