namespace Maalca.Domain.Enums;

/// <summary>
/// Nivel de plan para decidir qué módulos se ven (ModuleAccess). Es el nivel EFECTIVO, no el
/// enum Plan: un Starter con el trial vigente cuenta como Professional (el trial se siente como el
/// plan completo); al vencer, como Starter.
/// </summary>
public enum PlanTier
{
    Starter = 0,
    Growth = 1,
    Professional = 2,
}
