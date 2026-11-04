using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>
/// Levantamientos de prospectos (ProspectIntake). /ops arma la lista de preguntas y comparte el
/// enlace; el prospecto responde en /levantamiento/{token}, con autoguardado y envío final.
///
/// Preguntas y respuestas son JSON, pero se validan y se re-serializan aquí (nunca se guarda el
/// JSON tal como llega): solo pasan las propiedades conocidas, con límites de tamaño, para que
/// la página pública no pueda recibir basura ni nadie pueda inflar la fila.
/// </summary>
public class ProspectIntakeService : IProspectIntakeService
{
    public static readonly string[] QuestionTypes =
        { "text", "textarea", "number", "email", "phone", "select", "multiselect", "yesno", "file" };

    private const int MaxQuestions = 80;
    private const int MaxOptions = 30;
    private const int MaxTextAnswer = 5000;
    private const int MaxFilesPerQuestion = 5;
    private const int MaxAnswersJsonLength = 200_000;
    private static readonly Regex KeyPattern = new(@"^[a-z0-9_]{1,40}$", RegexOptions.Compiled);

    // Acentos y ¿? legibles en la base (sin \u00BF); el JSON igual es válido para cualquier lector.
    private static readonly JsonSerializerOptions StoreJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };

    private readonly AppDbContext _db;

    public ProspectIntakeService(AppDbContext db) => _db = db;

    // ---------------------------------------------------------------- /ops

    public async Task<IReadOnlyList<ProspectIntakeSummaryDto>> GetAllAsync()
    {
        var rows = await _db.ProspectIntakes.AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return rows.Select(i =>
        {
            var questions = ParseQuestions(i.QuestionsJson);
            return new ProspectIntakeSummaryDto(
                i.Id, i.Token, i.BusinessName, i.ContactName, i.Status,
                questions.Count, CountAnswered(questions, ParseObject(i.AnswersJson)),
                i.CreatedAt, i.LastSavedAt, i.SubmittedAt, i.CreatedByEmail);
        }).ToList();
    }

    public async Task<ProspectIntakeDto?> GetAsync(Guid id)
    {
        var intake = await _db.ProspectIntakes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        return intake is null ? null : ToDto(intake);
    }

    public async Task<ProspectIntakeDto> CreateAsync(UpsertProspectIntakeRequest request, string adminEmail)
    {
        var intake = new ProspectIntake { CreatedByEmail = adminEmail ?? string.Empty };
        Apply(intake, request);
        _db.ProspectIntakes.Add(intake);
        await _db.SaveChangesAsync();
        return ToDto(intake);
    }

    public async Task<ProspectIntakeDto?> UpdateAsync(Guid id, UpsertProspectIntakeRequest request)
    {
        var intake = await _db.ProspectIntakes.FirstOrDefaultAsync(i => i.Id == id);
        if (intake is null) return null;
        Apply(intake, request);
        intake.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(intake);
    }

    public async Task<ProspectIntakeDto?> SetStatusAsync(Guid id, string status)
    {
        status = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!ProspectIntakeStatus.All.Contains(status))
            throw new ArgumentException("Estado no válido.");

        var intake = await _db.ProspectIntakes.FirstOrDefaultAsync(i => i.Id == id);
        if (intake is null) return null;

        intake.Status = status;
        if (status == ProspectIntakeStatus.Reviewed) intake.ReviewedAt = DateTime.UtcNow;
        // Reabrir = el prospecto puede corregir y volver a enviar; el envío anterior queda en
        // SubmittedAt hasta que envíe de nuevo.
        intake.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(intake);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var intake = await _db.ProspectIntakes.FirstOrDefaultAsync(i => i.Id == id);
        if (intake is null) return false;
        _db.ProspectIntakes.Remove(intake);
        await _db.SaveChangesAsync();
        return true;
    }

    // ---------------------------------------------------------------- público

    public async Task<PublicProspectIntakeDto?> GetPublicAsync(Guid token)
    {
        var i = await _db.ProspectIntakes.AsNoTracking().FirstOrDefaultAsync(x => x.Token == token);
        if (i is null) return null;
        return new PublicProspectIntakeDto(
            i.BusinessName, i.ContactName, i.Title, i.IntroMessage, i.AdvisorName, i.AdvisorContact,
            i.Language, ToElement(i.QuestionsJson, "[]"), ToNullableElement(i.AnswersJson),
            i.Status, i.LastSavedAt, i.SubmittedAt);
    }

    public async Task<SaveProspectIntakeAnswersResult> SaveAnswersAsync(Guid token, SaveProspectIntakeAnswersRequest request, string? ip)
    {
        var intake = await _db.ProspectIntakes.FirstOrDefaultAsync(i => i.Token == token)
            ?? throw new KeyNotFoundException();

        if (intake.Status != ProspectIntakeStatus.Open)
            throw new InvalidOperationException(intake.Language == "en"
                ? "This form was already submitted. Contact your MaalCa advisor to make changes."
                : "Este formulario ya fue enviado. Escribe a tu asesor de MaalCa si necesitas cambiar algo.");

        var questions = ParseQuestions(intake.QuestionsJson);
        var answers = SanitizeAnswers(request.Answers, questions, intake.Token);

        if (request.Submit)
        {
            var missing = questions
                .Where(q => q.Required && !HasValue(answers[q.Key]))
                .Select(q => q.Label)
                .ToList();
            if (missing.Count > 0)
                throw new ArgumentException((intake.Language == "en" ? "Missing required answers: " : "Faltan respuestas obligatorias: ")
                    + string.Join(", ", missing.Take(5)) + (missing.Count > 5 ? "…" : ""));
        }

        var json = answers.ToJsonString(StoreJson);
        if (json.Length > MaxAnswersJsonLength)
            throw new ArgumentException("Las respuestas son demasiado largas.");

        var now = DateTime.UtcNow;
        intake.AnswersJson = json;
        intake.LastSavedAt = now;
        intake.UpdatedAt = now;
        if (request.Submit)
        {
            intake.Status = ProspectIntakeStatus.Submitted;
            intake.SubmittedAt = now;
            intake.SubmittedIp = ip is { Length: > 64 } ? ip[..64] : ip;
        }
        await _db.SaveChangesAsync();

        return new SaveProspectIntakeAnswersResult(
            intake.Id, intake.BusinessName, intake.ContactName, intake.CreatedByEmail,
            intake.Status, intake.LastSavedAt, intake.SubmittedAt, request.Submit,
            questions.Count, CountAnswered(questions, answers));
    }

    // ---------------------------------------------------------------- validación

    private sealed record Question(string Key, string Label, string Type, bool Required, IReadOnlyList<string> Options);

    private static void Apply(ProspectIntake intake, UpsertProspectIntakeRequest r)
    {
        var name = Clean(r.BusinessName, 160);
        if (name is null) throw new ArgumentException("El nombre del negocio es obligatorio.");

        var language = (r.Language ?? "es").Trim().ToLowerInvariant();
        if (language != "es" && language != "en") language = "es";

        intake.BusinessName = name;
        intake.ContactName = Clean(r.ContactName, 120);
        intake.ContactEmail = Clean(r.ContactEmail, 200);
        intake.ContactPhone = Clean(r.ContactPhone, 40);
        intake.Title = Clean(r.Title, 200)
            ?? (language == "en" ? $"Setting up {name} on MaalCa" : $"Levantamiento de {name} para MaalCa");
        intake.IntroMessage = Clean(r.IntroMessage, 2000);
        intake.AdvisorName = Clean(r.AdvisorName, 120);
        intake.AdvisorContact = Clean(r.AdvisorContact, 200);
        intake.Language = language;
        intake.QuestionsJson = SanitizeQuestions(r.Questions).ToJsonString(StoreJson);
        intake.Notes = Clean(r.Notes, 4000);
    }

    /// <summary>Valida la lista de preguntas y la reconstruye solo con las propiedades conocidas.</summary>
    private static JsonArray SanitizeQuestions(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Las preguntas deben ser una lista.");
        var count = input.GetArrayLength();
        if (count == 0) throw new ArgumentException("Agrega al menos una pregunta.");
        if (count > MaxQuestions) throw new ArgumentException($"Máximo {MaxQuestions} preguntas.");

        var keys = new HashSet<string>();
        var result = new JsonArray();
        foreach (var q in input.EnumerateArray())
        {
            if (q.ValueKind != JsonValueKind.Object) throw new ArgumentException("Pregunta con formato no válido.");

            var label = Clean(Str(q, "label"), 300) ?? throw new ArgumentException("Cada pregunta necesita su texto.");
            var key = (Str(q, "key") ?? string.Empty).Trim().ToLowerInvariant();
            if (!KeyPattern.IsMatch(key))
                throw new ArgumentException($"Clave no válida en \"{label}\" (usa minúsculas, números y _).");
            if (!keys.Add(key)) throw new ArgumentException($"La clave \"{key}\" está repetida.");

            var type = (Str(q, "type") ?? "text").Trim().ToLowerInvariant();
            if (!QuestionTypes.Contains(type)) throw new ArgumentException($"Tipo no válido en \"{label}\".");

            var node = new JsonObject
            {
                ["key"] = key,
                ["label"] = label,
                ["type"] = type,
                ["required"] = q.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.True,
            };
            var section = Clean(Str(q, "section"), 80);
            if (section is not null) node["section"] = section;
            var help = Clean(Str(q, "help"), 500);
            if (help is not null) node["help"] = help;

            if (type is "select" or "multiselect")
            {
                var options = new JsonArray();
                if (q.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
                    foreach (var o in opts.EnumerateArray().Take(MaxOptions))
                        if (o.ValueKind == JsonValueKind.String && Clean(o.GetString(), 120) is { } opt)
                            options.Add(opt);
                if (options.Count == 0) throw new ArgumentException($"\"{label}\" necesita al menos una opción.");
                node["options"] = options;
            }
            result.Add(node);
        }
        return result;
    }

    /// <summary>
    /// Respuestas: solo claves de preguntas existentes, con el tipo que corresponde. Los archivos
    /// solo pueden apuntar a la carpeta de este levantamiento (intake/{token}/...).
    /// </summary>
    private static JsonObject SanitizeAnswers(JsonElement input, List<Question> questions, Guid token)
    {
        if (input.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Respuestas con formato no válido.");

        var result = new JsonObject();
        var filePrefix = $"intake/{token}/";
        foreach (var q in questions)
        {
            if (!input.TryGetProperty(q.Key, out var v) || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                result[q.Key] = null;
                continue;
            }

            switch (q.Type)
            {
                case "multiselect":
                {
                    var list = new JsonArray();
                    if (v.ValueKind == JsonValueKind.Array)
                        foreach (var item in v.EnumerateArray())
                            if (item.ValueKind == JsonValueKind.String && q.Options.Contains(item.GetString()))
                                list.Add(item.GetString());
                    result[q.Key] = list;
                    break;
                }
                case "file":
                {
                    var files = new JsonArray();
                    if (v.ValueKind == JsonValueKind.Array)
                        foreach (var f in v.EnumerateArray().Take(MaxFilesPerQuestion))
                        {
                            if (f.ValueKind != JsonValueKind.Object) continue;
                            var path = Str(f, "path");
                            var fname = Clean(Str(f, "name"), 200);
                            if (path is null || !path.StartsWith(filePrefix, StringComparison.Ordinal) || path.Contains("..")) continue;
                            files.Add(new JsonObject { ["path"] = path, ["name"] = fname ?? path[(path.LastIndexOf('/') + 1)..] });
                        }
                    result[q.Key] = files;
                    break;
                }
                case "select":
                {
                    var s = v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    result[q.Key] = s is not null && q.Options.Contains(s) ? s : null;
                    break;
                }
                case "yesno":
                {
                    var s = v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    result[q.Key] = s is "yes" or "no" ? s : null;
                    break;
                }
                default:
                {
                    var s = v.ValueKind switch
                    {
                        JsonValueKind.String => v.GetString(),
                        JsonValueKind.Number => v.GetRawText(),
                        _ => null,
                    };
                    result[q.Key] = Clean(s, MaxTextAnswer);
                    break;
                }
            }
        }
        return result;
    }

    private static List<Question> ParseQuestions(string? json)
    {
        var list = new List<Question>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
            foreach (var q in doc.RootElement.EnumerateArray())
            {
                var key = Str(q, "key");
                if (key is null) continue;
                var options = q.TryGetProperty("options", out var o) && o.ValueKind == JsonValueKind.Array
                    ? o.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
                    : new List<string>();
                list.Add(new Question(key, Str(q, "label") ?? key, Str(q, "type") ?? "text",
                    q.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.True, options));
            }
        }
        catch (JsonException) { }
        return list;
    }

    private static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { return new JsonObject(); }
    }

    private static int CountAnswered(List<Question> questions, JsonObject answers) =>
        questions.Count(q => answers.TryGetPropertyValue(q.Key, out var v) && HasValue(v));

    private static bool HasValue(JsonNode? v) => v switch
    {
        null => false,
        JsonArray a => a.Count > 0,
        JsonValue val => val.TryGetValue<string>(out var s) ? !string.IsNullOrWhiteSpace(s) : true,
        _ => true,
    };

    private ProspectIntakeDto ToDto(ProspectIntake i)
    {
        var questions = ParseQuestions(i.QuestionsJson);
        return new ProspectIntakeDto(
            i.Id, i.Token, i.BusinessName, i.ContactName, i.ContactEmail, i.ContactPhone,
            i.Title, i.IntroMessage, i.AdvisorName, i.AdvisorContact, i.Language,
            ToElement(i.QuestionsJson, "[]"), ToNullableElement(i.AnswersJson), i.Status,
            questions.Count, CountAnswered(questions, ParseObject(i.AnswersJson)),
            i.CreatedAt, i.LastSavedAt, i.SubmittedAt, i.ReviewedAt, i.CreatedByEmail, i.Notes);
    }

    private static JsonElement ToElement(string? json, string fallback)
    {
        try { return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? fallback : json).RootElement.Clone(); }
        catch (JsonException) { return JsonDocument.Parse(fallback).RootElement.Clone(); }
    }

    private static JsonElement? ToNullableElement(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : ToElement(json, "{}");

    private static string? Str(JsonElement e, string prop) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > max ? s[..max] : s;
    }
}
