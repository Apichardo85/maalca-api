using Maalca.Domain.Common;
using Maalca.Domain.Enums;

namespace Maalca.Domain.Entities;

public class EventoInteraccion : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public EventoTipo Tipo { get; set; }
    public Guid? CanalId { get; set; }

    public Affiliate? Affiliate { get; set; }
    public Canal? Canal { get; set; }
}
