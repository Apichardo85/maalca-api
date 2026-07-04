namespace Maalca.Application.Common.DTOs;

public record PublicInteractionEventRequest(
    string Type,
    Guid? CanalId = null
);
