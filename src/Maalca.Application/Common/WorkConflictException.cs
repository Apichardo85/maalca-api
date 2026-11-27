namespace Maalca.Application.Common;

/// <summary>
/// La ficha de la obra cambió desde que el dashboard la leyó (otra pestaña, otra persona, un script).
/// El endpoint la traduce a 409 STALE_WORK con la revisión actual, para que el dashboard ofrezca
/// cargar la versión vigente en vez de pisarla con datos viejos.
/// </summary>
public class WorkConflictException : Exception
{
    public int CurrentRevision { get; }

    public WorkConflictException(int currentRevision)
        : base("Esta obra cambió desde que la abriste. Carga la versión actual antes de guardar.")
    {
        CurrentRevision = currentRevision;
    }
}
