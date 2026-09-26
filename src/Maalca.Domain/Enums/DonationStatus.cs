namespace Maalca.Domain.Enums;

public enum DonationStatus
{
    Pending  = 0, // creado, esperando confirmación de pago en Stripe Checkout
    Paid     = 1,
    Canceled = 2,
}
