using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>MaalCa Comunidad (Fase 1): inventario con costo/vencimiento, recetas, combos y la
/// acción de servir platos. Todos los métodos filtran por affiliateId — el ownership check del
/// usuario (active_affiliate_id) se hace en el endpoint. Errores de validación/estado se lanzan
/// como InvalidOperationException (400/409 en el endpoint según el caso).</summary>
public interface ICommunityService
{
    Task<List<CommunityInventoryItemDto>> GetInventoryItemsAsync(Guid affiliateId, DateOnly? expiringBefore = null);
    Task<CommunityInventoryItemDto?> GetInventoryItemAsync(Guid affiliateId, Guid id);
    Task<CommunityInventoryItemDto> CreateInventoryItemAsync(Guid affiliateId, UpsertCommunityInventoryItemRequest request);
    Task<CommunityInventoryItemDto?> UpdateInventoryItemAsync(Guid affiliateId, Guid id, UpsertCommunityInventoryItemRequest request);
    Task<bool> DeleteInventoryItemAsync(Guid affiliateId, Guid id);

    Task<List<RecipeDto>> GetRecipesAsync(Guid affiliateId);
    Task<RecipeDto?> GetRecipeAsync(Guid affiliateId, Guid id);
    Task<RecipeDto> CreateRecipeAsync(Guid affiliateId, UpsertRecipeRequest request);
    Task<RecipeDto?> UpdateRecipeAsync(Guid affiliateId, Guid id, UpsertRecipeRequest request);
    Task<bool> DeleteRecipeAsync(Guid affiliateId, Guid id);

    /// <summary>null = la receta no existe para este afiliado.</summary>
    Task<List<RecipeIngredientDto>?> GetRecipeIngredientsAsync(Guid affiliateId, Guid recipeId);
    Task<RecipeDto?> AddRecipeIngredientAsync(Guid affiliateId, Guid recipeId, UpsertRecipeIngredientRequest request);
    Task<RecipeDto?> UpdateRecipeIngredientAsync(Guid affiliateId, Guid recipeId, Guid ingredientId, UpsertRecipeIngredientRequest request);
    Task<RecipeDto?> DeleteRecipeIngredientAsync(Guid affiliateId, Guid recipeId, Guid ingredientId);

    Task<List<ComboDto>> GetCombosAsync(Guid affiliateId);
    Task<ComboDto?> GetComboAsync(Guid affiliateId, Guid id);
    Task<ComboDto> CreateComboAsync(Guid affiliateId, UpsertComboRequest request);
    Task<ComboDto?> UpdateComboAsync(Guid affiliateId, Guid id, UpsertComboRequest request);
    Task<bool> DeleteComboAsync(Guid affiliateId, Guid id);

    /// <summary>Descuenta inventario por cada receta del combo y registra un ComboServing (fuente
    /// única de "comidas servidas"). null = el combo no existe para este afiliado.</summary>
    Task<ServeComboResponse?> ServeComboAsync(Guid affiliateId, Guid comboId, int quantity);

    Task<CommunityMetricsDto> GetMetricsAsync(Guid affiliateId);
}
