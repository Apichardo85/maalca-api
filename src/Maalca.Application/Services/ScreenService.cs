using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class ScreenService : IScreenService
{
    private readonly AppDbContext _db;

    public ScreenService(AppDbContext db) => _db = db;

    public async Task<List<ScreenDto>> GetAllAsync(Guid affiliateId)
        => await _db.Screens
            .Where(s => s.AffiliateId == affiliateId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => ToDto(s))
            .ToListAsync();

    public async Task<ScreenDto> CreateAsync(Guid affiliateId, CreateScreenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("El nombre de la pantalla es obligatorio.");

        var theme = ParseTheme(request.BoardTheme);
        var maxSort = await _db.Screens.Where(s => s.AffiliateId == affiliateId)
            .Select(s => (int?)s.SortOrder).MaxAsync() ?? -1;

        var screen = new Screen
        {
            AffiliateId = affiliateId,
            Name = request.Name.Trim(),
            SortOrder = maxSort + 1,
            Language = NormalizeLanguage(request.Language),
            BoardTheme = theme,
            AdFrequency = request.AdFrequency,
            CategoryFilter = string.IsNullOrWhiteSpace(request.CategoryFilter) ? null : request.CategoryFilter.Trim(),
        };
        _db.Screens.Add(screen);
        await _db.SaveChangesAsync();
        return ToDto(screen);
    }

    public async Task<ScreenDto> UpdateAsync(Guid affiliateId, Guid screenId, UpdateScreenRequest request)
    {
        var screen = await _db.Screens.FirstOrDefaultAsync(s => s.Id == screenId && s.AffiliateId == affiliateId)
            ?? throw new KeyNotFoundException("Screen not found");

        if (!string.IsNullOrWhiteSpace(request.Name)) screen.Name = request.Name.Trim();
        if (request.SortOrder.HasValue) screen.SortOrder = request.SortOrder.Value;

        // Estos cuatro sí se sobreescriben directo (incluyendo a null = "heredar del negocio")
        // porque el form de Pantallas manda su estado completo en cada guardado.
        screen.Language = NormalizeLanguage(request.Language);
        screen.BoardTheme = ParseTheme(request.BoardTheme);
        screen.AdFrequency = request.AdFrequency;
        screen.CategoryFilter = string.IsNullOrWhiteSpace(request.CategoryFilter) ? null : request.CategoryFilter.Trim();

        await _db.SaveChangesAsync();
        return ToDto(screen);
    }

    public async Task DeleteAsync(Guid affiliateId, Guid screenId)
    {
        var screen = await _db.Screens.FirstOrDefaultAsync(s => s.Id == screenId && s.AffiliateId == affiliateId)
            ?? throw new KeyNotFoundException("Screen not found");
        _db.Screens.Remove(screen);
        await _db.SaveChangesAsync();
    }

    private static string? NormalizeLanguage(string? language)
        => string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant();

    private static BoardTheme? ParseTheme(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme)) return null;
        if (!Enum.TryParse<BoardTheme>(theme, ignoreCase: true, out var parsed))
            throw new ArgumentException($"Invalid boardTheme '{theme}'.");
        return parsed;
    }

    private static ScreenDto ToDto(Screen s) => new(
        s.Id, s.Name, s.SortOrder, s.Language, s.BoardTheme?.ToString(), s.AdFrequency, s.CategoryFilter);
}
