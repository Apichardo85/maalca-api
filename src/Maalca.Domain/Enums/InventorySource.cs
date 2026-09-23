namespace Maalca.Domain.Enums;

// Origen de un InventoryItem en MaalCa Comunidad. En la API viaja como snake_case
// ("donation_kind", "purchased_cash", "garden") — ver CommunityService.
public enum InventorySource
{
    DonationKind  = 0,   // donación en especie
    PurchasedCash = 1,   // comprado con dinero recaudado
    Garden        = 2    // huerto propio
}
