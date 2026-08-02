namespace Maalca.Application.Common.DTOs;

public record CreateCheckoutSessionRequest(string SuccessUrl, string CancelUrl);

public record CheckoutSessionResponseDto(string Url);
