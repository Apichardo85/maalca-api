namespace Maalca.Application.Common;

/// <summary>
/// Crear esta(s) pantalla(s) supera las incluidas en el plan + las extra pagadas. El endpoint la
/// traduce a 402 SCREEN_LIMIT_REACHED con los números, para que el dashboard ofrezca "agregar
/// pantallas extra" en vez de un error genérico.
/// </summary>
public class ScreenLimitException : Exception
{
    public int Allowance { get; }
    public int Used { get; }
    public int Requested { get; }

    public ScreenLimitException(int allowance, int used, int requested)
        : base(requested == 1
            ? $"Tu plan incluye {allowance} pantalla(s) y ya usas {used}. Agrega pantallas extra para seguir."
            : $"Este muro necesita {requested} pantallas nuevas: tu plan permite {allowance} y ya usas {used}. Agrega pantallas extra para seguir.")
    {
        Allowance = allowance;
        Used = used;
        Requested = requested;
    }
}
