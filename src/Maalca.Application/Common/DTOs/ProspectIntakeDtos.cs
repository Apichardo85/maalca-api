using System.Text.Json;

namespace Maalca.Application.Common.DTOs;

/// <summary>Fila de la lista de /ops/levantamientos.</summary>
public record ProspectIntakeSummaryDto(
    Guid Id, Guid Token, string BusinessName, string? ContactName, string Status,
    int QuestionCount, int AnsweredCount, DateTime CreatedAt, DateTime? LastSavedAt,
    DateTime? SubmittedAt, string CreatedByEmail);

/// <summary>Levantamiento completo para /ops (incluye respuestas y notas internas).</summary>
public record ProspectIntakeDto(
    Guid Id, Guid Token, string BusinessName, string? ContactName, string? ContactEmail, string? ContactPhone,
    string Title, string? IntroMessage, string? AdvisorName, string? AdvisorContact, string Language,
    JsonElement Questions, JsonElement? Answers, string Status, int QuestionCount, int AnsweredCount,
    DateTime CreatedAt, DateTime? LastSavedAt, DateTime? SubmittedAt, DateTime? ReviewedAt,
    string CreatedByEmail, string? Notes);

/// <summary>Lo que ve el prospecto en /levantamiento/{token}. Sin notas internas ni correo del admin.</summary>
public record PublicProspectIntakeDto(
    string BusinessName, string? ContactName, string Title, string? IntroMessage,
    string? AdvisorName, string? AdvisorContact, string Language,
    JsonElement Questions, JsonElement? Answers, string Status, DateTime? LastSavedAt, DateTime? SubmittedAt);

/// <summary>Alta y edición desde /ops (PUT reemplaza todos los campos editables).</summary>
public record UpsertProspectIntakeRequest(
    string BusinessName, string? ContactName, string? ContactEmail, string? ContactPhone,
    string? Title, string? IntroMessage, string? AdvisorName, string? AdvisorContact,
    string? Language, JsonElement Questions, string? Notes);

public record SetProspectIntakeStatusRequest(string Status);

/// <summary>Autoguardado (Submit=false) o envío final (Submit=true) desde el formulario público.</summary>
public record SaveProspectIntakeAnswersRequest(JsonElement Answers, bool Submit);

public record SaveProspectIntakeAnswersResult(
    Guid Id, string BusinessName, string? ContactName, string CreatedByEmail,
    string Status, DateTime? LastSavedAt, DateTime? SubmittedAt, bool JustSubmitted,
    int QuestionCount, int AnsweredCount);
