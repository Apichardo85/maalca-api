namespace Maalca.Application.Common.DTOs;

/// <summary>
/// Inscripción pública de Comunidad (sin login): Kind = "volunteer" (CausaId, causa de tipo
/// "time") o "event" (ActivityId, PartySize = la persona + acompañantes). Ver CommunitySignup.cs.
/// </summary>
public record CreatePublicSignupRequest(
    string Kind,
    Guid? CausaId,
    Guid? ActivityId,
    string Name,
    string? Phone,
    string? Email,
    int? PartySize,
    string? Notes,
    string? Language
);

public record PublicSignupResultDto(Guid Id, string Kind, string Status, string TargetTitle, int PartySize);
