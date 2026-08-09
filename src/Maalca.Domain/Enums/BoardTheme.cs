namespace Maalca.Domain.Enums;

/// <summary>
/// Tema visual del Menu Board público (Fase 9). Dark = 0 a propósito — es el default de C#
/// para un enum sin setear, así que cualquier afiliado ya existente (creado antes de este
/// campo) se comporta exactamente igual que hoy sin necesidad de un backfill.
/// </summary>
public enum BoardTheme
{
    Dark = 0,
    Light = 1
}
