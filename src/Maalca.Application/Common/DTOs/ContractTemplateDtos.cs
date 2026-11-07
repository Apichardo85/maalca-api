namespace Maalca.Application.Common.DTOs;

/// <summary>Etiqueta disponible para la plantilla: se escribe como {{Key}} y se reemplaza al generar el documento.</summary>
public record TemplateTagDto(string Key, string LabelEs, string LabelEn);

/// <summary>IsCustom = false cuando el negocio todavía no guardó la suya y se muestra la plantilla base.</summary>
public record ContractTemplateDto(string Title, string Body, bool IsCustom, IReadOnlyList<TemplateTagDto> Tags);

public record SaveContractTemplateRequest(string Title, string Body);

/// <summary>Documento listo para imprimir: texto ya con los datos, más el encabezado del negocio.
/// Title y Body son texto plano (la web los muestra con saltos de línea, sin interpretar HTML).</summary>
public record ContractDocumentDto(
    string Title, string Body, string? ContractNumber, string Date,
    string BusinessName, string? BusinessPhone, string? BusinessAddress, string? BusinessLogoUrl, string? BusinessColor,
    string CustomerName);
