namespace MovieVerse.Api.Payments;

public sealed record CheckoutLine(string Name, int UnitAmountCents, int Quantity);

public sealed record CheckoutRequest(
    Guid BookingId,
    IReadOnlyList<CheckoutLine> Lines,
    string? CustomerEmail,
    string SuccessUrl,
    string CancelUrl,
    DateTime ExpiresAtUtc);

public sealed record CheckoutSession(string Id, string Url);

/// <summary>What a checkout session currently says about its payment.</summary>
public sealed record CheckoutState(string SessionId, Guid? BookingId, bool Paid, bool Expired, string? PaymentIntentId);

public sealed record PaymentWebhook(string Type, CheckoutState? Checkout);

/// <summary>The payment provider as the booking flow needs it; Stripe in production, a fake in tests.</summary>
public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct);

    Task<CheckoutState> GetCheckoutAsync(string sessionId, CancellationToken ct);

    Task ExpireCheckoutAsync(string sessionId, CancellationToken ct);

    Task RefundAsync(string paymentIntentId, CancellationToken ct);

    /// <summary>Verifies the signature and reads the event; throws <see cref="PaymentWebhookException"/> if it's not genuine.</summary>
    PaymentWebhook ParseWebhook(string payload, string signature);
}

public sealed class PaymentWebhookException(string message, Exception? inner = null) : Exception(message, inner);
