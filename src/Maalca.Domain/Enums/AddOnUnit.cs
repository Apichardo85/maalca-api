namespace Maalca.Domain.Enums;

/// <summary>
/// Cómo se cobra un extra de MaalCa (PlatformAddOn). Se guarda como entero (default de EF).
/// </summary>
public enum AddOnUnit
{
    OneTime = 0,            // pago único (ej. Kit de pantalla)
    Monthly = 1,            // por mes
    PerLocationMonthly = 2, // por local, por mes (ej. Servicio completo de pantallas)
    Block5h = 3,            // bloque de 5 horas (consultoría)
}
