using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace MovieVerse.Api.Payments;

public sealed class StripePaymentGateway(IOptions<StripeOptions> options) : IPaymentGateway
{
    private readonly StripeClient _client = new(options.Value.SecretKey);

    public async Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        var session = await new SessionService(_client).CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = request.BookingId.ToString(),
            Metadata = new Dictionary<string, string> { ["bookingId"] = request.BookingId.ToString() },
            CustomerEmail = request.CustomerEmail,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ExpiresAt = request.ExpiresAtUtc,
            // New accounts default to Stripe as merchant of record, which demands tax codes; a demo sells for itself
            ManagedPayments = new SessionManagedPaymentsOptions { Enabled = false },
            LineItems = request.Lines.Select(line => new SessionLineItemOptions
            {
                Quantity = line.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = Data.Booking.Currency,
                    UnitAmount = line.UnitAmountCents,
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = line.Name },
                },
            }).ToList(),
        }, cancellationToken: ct);

        return new CheckoutSession(session.Id, session.Url);
    }

    public async Task<CheckoutState> GetCheckoutAsync(string sessionId, CancellationToken ct) =>
        ToState(await new SessionService(_client).GetAsync(sessionId, cancellationToken: ct));

    public async Task ExpireCheckoutAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            await new SessionService(_client).ExpireAsync(sessionId, cancellationToken: ct);
        }
        catch (StripeException ex) when (ex.StripeError?.Code == "checkout_session_not_open" || ex.HttpStatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // already completed or expired; nothing to do
        }
    }

    public Task RefundAsync(string paymentIntentId, CancellationToken ct) =>
        new RefundService(_client).CreateAsync(new RefundCreateOptions { PaymentIntent = paymentIntentId }, cancellationToken: ct);

    public PaymentWebhook ParseWebhook(string payload, string signature)
    {
        var secret = options.Value.WebhookSecret;
        if (string.IsNullOrEmpty(secret))
        {
            throw new PaymentWebhookException("Stripe webhook secret is not configured.");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, secret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            throw new PaymentWebhookException("Invalid Stripe webhook signature.", ex);
        }

        return new PaymentWebhook(
            stripeEvent.Type,
            stripeEvent.Data.Object is Session session ? ToState(session) : null);
    }

    private static CheckoutState ToState(Session session) => new(
        session.Id,
        Guid.TryParse(session.ClientReferenceId, out var bookingId) ? bookingId : null,
        session.PaymentStatus == "paid",
        session.Status == "expired",
        session.PaymentIntentId);
}
