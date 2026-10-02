namespace Maalca.Application.Common.Interfaces;

/// <summary>Logo y color de marca del negocio, para personalizar correos transaccionales a sus clientes.</summary>
public record AffiliateBrand(string? LogoUrl, string? Color, string? Slug);

public interface IAffiliateBrandResolver
{
    /// <summary>Nunca lanza: si el negocio no existe o falla la consulta devuelve una marca vacía.</summary>
    Task<AffiliateBrand> GetAsync(Guid affiliateId);
}
