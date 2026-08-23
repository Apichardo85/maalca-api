namespace Maalca.Domain.Enums;

/// <summary>
/// Cómo se presta un Service — determina si el cliente puede/debe elegir modalidad al
/// reservar (ver PublicBookingService y PublicBookingSection.tsx). InPerson es el default
/// para no cambiar el comportamiento de servicios ya existentes.
/// </summary>
public enum ServiceModality
{
    InPerson = 0,
    Virtual  = 1,
    Both     = 2,
}
