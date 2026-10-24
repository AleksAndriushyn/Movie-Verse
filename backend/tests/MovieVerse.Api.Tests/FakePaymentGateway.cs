using System.Collections.Concurrent;
using System.Text.Json;
using MovieVerse.Api.Payments;

namespace MovieVerse.Api.Tests;

/// <summary>Stands in for Stripe: remembers checkout sessions and lets tests pay or expire them.</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public const string ValidSignature = "valid-signature";

    public ConcurrentDictionary<string, CheckoutRequest> Sessions { get; } = new();
    public ConcurrentBag<string> Expired { get; } = [];
    public ConcurrentBag<string> Refunded { get; } = [];
    public ConcurrentDictionary<string, bool> Paid { get; } = new();
    public bool FailNextCheckout { get; set; }

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        if (FailNextCheckout)
        {
            FailNextCheckout = false;
            throw new HttpRequestException("Stripe is down");
        }

        var id = $"cs_test_{Guid.NewGuid():N}";
        Sessions[id] = request;
        return Task.FromResult(new CheckoutSession(id, $"https://checkout.stripe.test/{id}"));
    }

    public Task<CheckoutState> GetCheckoutAsync(string sessionId, CancellationToken ct) =>
        Task.FromResult(StateOf(sessionId));

    public Task ExpireCheckoutAsync(string sessionId, CancellationToken ct)
    {
        Expired.Add(sessionId);
        return Task.CompletedTask;
    }

    public Task RefundAsync(string paymentIntentId, CancellationToken ct)
    {
        Refunded.Add(paymentIntentId);
        return Task.CompletedTask;
    }

    /// <summary>Test webhooks are { "type": ..., "sessionId": ... } with a fixed signature.</summary>
    public PaymentWebhook ParseWebhook(string payload, string signature)
    {
        if (signature != ValidSignature)
        {
            throw new PaymentWebhookException("bad signature");
        }

        using var document = JsonDocument.Parse(payload);
        var sessionId = document.RootElement.GetProperty("sessionId").GetString()!;
        return new PaymentWebhook(document.RootElement.GetProperty("type").GetString()!, StateOf(sessionId));
    }

    public string Pay(string sessionId)
    {
        Paid[sessionId] = true;
        return sessionId;
    }

    public CheckoutState StateOf(string sessionId) => new(
        sessionId,
        Sessions.TryGetValue(sessionId, out var request) ? request.BookingId : null,
        Paid.ContainsKey(sessionId),
        Expired.Contains(sessionId),
        Paid.ContainsKey(sessionId) ? $"pi_{sessionId}" : null);
}
