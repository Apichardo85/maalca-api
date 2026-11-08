namespace Maalca.Application.Common.DTOs;

/// <summary>AgingBasis: issue | due. TaxMode: none | added | included. TaxRate en % (0–30).</summary>
public record ReceivablesSettingsDto(string AgingBasis = "issue", string TaxMode = "none", decimal TaxRate = 0m, string TaxLabel = "ITBIS");
