using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>
/// Muros de pantallas sincronizadas. Regla central (EnsureSlotsAsync): el muro siempre tiene
/// exactamente una TV (Screen) por posición de su grilla Cols×Rows — al crearlo o agrandarlo se
/// crean solas las que faltan ("{muro} · TV n", cada una con su URL fija para Fully Kiosk); al
/// achicarlo, las que quedan fuera se desprenden y siguen como pantallas sueltas (su URL no
/// muere de golpe en una TV que alguien ya configuró).
/// </summary>
public class ScreenWallService : IScreenWallService
{
    public const int MaxCols = 4;
    public const int MaxRows = 3;
    private readonly AppDbContext _db;
    private readonly IPlanLimitService _planLimit;

    public ScreenWallService(AppDbContext db, IPlanLimitService planLimit)
    {
        _db = db;
        _planLimit = planLimit;
    }

    public async Task<List<ScreenWallDto>> GetAllAsync(Guid affiliateId)
    {
        var walls = await _db.ScreenWalls.Where(w => w.AffiliateId == affiliateId)
            .OrderBy(w => w.CreatedAt).ToListAsync();
        var screens = await _db.Screens.Where(s => s.AffiliateId == affiliateId && s.WallId != null).ToListAsync();
        return walls.Select(w => ToDto(w, screens.Where(s => s.WallId == w.Id))).ToList();
    }

    public async Task<ScreenWallDto> CreateAsync(Guid affiliateId, CreateScreenWallRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("El nombre del muro es obligatorio.");
        var wall = new ScreenWall
        {
            AffiliateId = affiliateId,
            Name = request.Name.Trim(),
            Mode = ParseMode(request.Mode) ?? ScreenWallMode.Span,
            Cols = ClampCols(request.Cols ?? 2),
            Rows = ClampRows(request.Rows ?? 1),
            BezelPx = ClampBezel(request.BezelPx ?? 0),
        };
        // Antes de guardar nada: un muro nuevo crea Cols×Rows TVs y todas cuentan para el plan.
        await _planLimit.EnsureScreenCapacityAsync(affiliateId, wall.Cols * wall.Rows);
        _db.ScreenWalls.Add(wall);
        await _db.SaveChangesAsync();
        var screens = await EnsureSlotsAsync(wall);
        return ToDto(wall, screens);
    }

    public async Task<ScreenWallDto> UpdateAsync(Guid affiliateId, Guid wallId, UpdateScreenWallRequest request)
    {
        var wall = await FindAsync(affiliateId, wallId);
        if (!string.IsNullOrWhiteSpace(request.Name)) wall.Name = request.Name.Trim();
        if (ParseMode(request.Mode) is { } mode) wall.Mode = mode;
        if (request.Cols.HasValue) wall.Cols = ClampCols(request.Cols.Value);
        if (request.Rows.HasValue) wall.Rows = ClampRows(request.Rows.Value);
        if (request.BezelPx.HasValue) wall.BezelPx = ClampBezel(request.BezelPx.Value);
        if (request.Calibrate.HasValue) wall.Calibrate = request.Calibrate.Value;
        // Agrandar la grilla crea TVs nuevas: validar el plan antes de guardar el cambio.
        await _planLimit.EnsureScreenCapacityAsync(affiliateId, await MissingSlotsAsync(wall));
        wall.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var screens = await EnsureSlotsAsync(wall);
        return ToDto(wall, screens);
    }

    public async Task<ScreenWallDto> UpdateSlotsAsync(Guid affiliateId, Guid wallId, UpdateScreenWallSlotsRequest request)
    {
        if (request.Slots is null || request.Slots.Count == 0)
            throw new ArgumentException("Faltan las posiciones de las pantallas.");
        var wall = await FindAsync(affiliateId, wallId);
        var members = await _db.Screens.Where(s => s.WallId == wall.Id).ToListAsync();
        var byId = members.ToDictionary(s => s.Id);

        var seen = new HashSet<(int, int)>();
        foreach (var slot in request.Slots)
        {
            if (!byId.ContainsKey(slot.ScreenId))
                throw new ArgumentException("Una de las pantallas no pertenece a este muro.");
            if (slot.Col < 0 || slot.Col >= wall.Cols || slot.Row < 0 || slot.Row >= wall.Rows)
                throw new ArgumentException("Posición fuera del muro.");
            if (!seen.Add((slot.Col, slot.Row)))
                throw new ArgumentException("Dos pantallas no pueden ocupar la misma posición.");
        }
        foreach (var slot in request.Slots)
        {
            byId[slot.ScreenId].WallCol = slot.Col;
            byId[slot.ScreenId].WallRow = slot.Row;
        }
        RenumberAutoNames(wall, members);
        await _db.SaveChangesAsync();
        return ToDto(wall, members);
    }

    public async Task DeleteAsync(Guid affiliateId, Guid wallId)
    {
        var wall = await FindAsync(affiliateId, wallId);
        // Las TVs quedan como pantallas sueltas (mismas URLs, mismo contenido), sin posición.
        var members = await _db.Screens.Where(s => s.WallId == wall.Id).ToListAsync();
        foreach (var s in members) { s.WallId = null; s.WallCol = null; s.WallRow = null; }
        _db.ScreenWalls.Remove(wall);
        await _db.SaveChangesAsync();
    }

    /// <summary>Cuántas TVs nuevas crearía EnsureSlotsAsync con la grilla actual (sin tocar nada).</summary>
    private async Task<int> MissingSlotsAsync(ScreenWall wall)
    {
        var positions = await _db.Screens.Where(s => s.WallId == wall.Id && s.WallCol != null && s.WallRow != null)
            .Select(s => new { Col = s.WallCol!.Value, Row = s.WallRow!.Value }).ToListAsync();
        var inside = positions.Where(p => p.Col < wall.Cols && p.Row < wall.Rows).Distinct().Count();
        return wall.Cols * wall.Rows - inside;
    }

    /// <summary>Una TV por posición: crea las que faltan, desprende las que sobran.</summary>
    private async Task<List<Screen>> EnsureSlotsAsync(ScreenWall wall)
    {
        var members = await _db.Screens.Where(s => s.WallId == wall.Id).ToListAsync();

        // Fuera de la grilla (el muro se achicó) o sin posición válida → pantalla suelta.
        var inside = new List<Screen>();
        var taken = new HashSet<(int, int)>();
        foreach (var s in members.OrderBy(s => s.WallRow ?? int.MaxValue).ThenBy(s => s.WallCol ?? int.MaxValue))
        {
            var pos = (s.WallCol ?? -1, s.WallRow ?? -1);
            if (pos.Item1 >= 0 && pos.Item1 < wall.Cols && pos.Item2 >= 0 && pos.Item2 < wall.Rows && taken.Add(pos))
                inside.Add(s);
            else { s.WallId = null; s.WallCol = null; s.WallRow = null; }
        }

        var maxSort = await _db.Screens.Where(s => s.AffiliateId == wall.AffiliateId)
            .Select(s => (int?)s.SortOrder).MaxAsync() ?? -1;
        for (var row = 0; row < wall.Rows; row++)
        for (var col = 0; col < wall.Cols; col++)
        {
            if (taken.Contains((col, row))) continue;
            var n = row * wall.Cols + col + 1;
            var screen = new Screen
            {
                AffiliateId = wall.AffiliateId,
                Name = $"{wall.Name} · TV {n}",
                SortOrder = ++maxSort,
                WallId = wall.Id,
                WallCol = col,
                WallRow = row,
            };
            _db.Screens.Add(screen);
            inside.Add(screen);
        }
        RenumberAutoNames(wall, inside);
        await _db.SaveChangesAsync();
        return inside;
    }

    /// <summary>Los nombres automáticos ("{muro} · TV n") siguen a la posición: al intercambiar dos
    /// TVs, "TV 1" vuelve a ser la de la izquierda. Un nombre que el dueño cambió a mano no se toca.</summary>
    private static void RenumberAutoNames(ScreenWall wall, IEnumerable<Screen> members)
    {
        var prefix = $"{wall.Name} · TV ";
        foreach (var s in members)
        {
            if (s.WallCol is not int col || s.WallRow is not int row) continue;
            if (!s.Name.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(s.Name[prefix.Length..], out _)) continue;
            s.Name = $"{prefix}{row * wall.Cols + col + 1}";
        }
    }

    private async Task<ScreenWall> FindAsync(Guid affiliateId, Guid wallId)
        => await _db.ScreenWalls.FirstOrDefaultAsync(w => w.Id == wallId && w.AffiliateId == affiliateId)
           ?? throw new KeyNotFoundException("Wall not found");

    private static ScreenWallMode? ParseMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return null;
        if (!Enum.TryParse<ScreenWallMode>(mode, ignoreCase: true, out var parsed))
            throw new ArgumentException($"Invalid mode '{mode}'.");
        return parsed;
    }

    private static int ClampCols(int v) => Math.Clamp(v, 1, MaxCols);
    private static int ClampRows(int v) => Math.Clamp(v, 1, MaxRows);
    private static int ClampBezel(int v) => Math.Clamp(v, 0, 400);

    private static ScreenWallDto ToDto(ScreenWall w, IEnumerable<Screen> members) => new(
        w.Id, w.Name, w.Mode.ToString(), w.Cols, w.Rows, w.BezelPx, w.Calibrate,
        members.Where(s => s.WallCol.HasValue && s.WallRow.HasValue)
            .OrderBy(s => s.WallRow).ThenBy(s => s.WallCol)
            .Select(s => new ScreenWallSlotDto(s.Id, s.Name, s.WallCol!.Value, s.WallRow!.Value))
            .ToList());
}
