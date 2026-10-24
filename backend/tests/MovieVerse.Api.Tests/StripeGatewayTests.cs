using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MovieVerse.Api.Payments;

namespace MovieVerse.Api.Tests;

/// <summary>The real Stripe gateway's offline parts: webhook signatures and the test-key guard.</summary>
public sealed class StripeGatewayTests
{
    private const string Secret = "whsec_test_secret";

    private static StripePaymentGateway Gateway(string? webhookSecret = Secret) =>
        new(Options.Create(new StripeOptions { SecretKey = "sk_test_x", WebhookSecret = webhookSecret }));

    // Stripe signs "{timestamp}.{payload}" with HMAC-SHA256
    private static string Sign(string payload, string secret = Secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    private static string CompletedEvent(Guid bookingId) => """
        {"id":"evt_1","object":"event","api_version":"2024-06-20","type":"checkout.session.completed",
         "data":{"object":{"id":"cs_test_1","object":"checkout.session","client_reference_id":"BOOKING_ID",
         "payment_status":"paid","status":"complete","payment_intent":"pi_1"}}}
        """.Replace("BOOKING_ID", bookingId.ToString());

    [Fact]
    public void SignedWebhook_IsParsedIntoCheckoutState()
    {
        var bookingId = Guid.NewGuid();
        var payload = CompletedEvent(bookingId);

        var webhook = Gateway().ParseWebhook(payload, Sign(payload));

        Assert.Equal("checkout.session.completed", webhook.Type);
        Assert.Equal(new CheckoutState("cs_test_1", bookingId, Paid: true, Expired: false, "pi_1"), webhook.Checkout);
    }

    [Fact]
    public void WebhookSignedWithAnotherSecret_IsRejected()
    {
        var payload = CompletedEvent(Guid.NewGuid());

        Assert.Throws<PaymentWebhookException>(() => Gateway().ParseWebhook(payload, Sign(payload, "whsec_other")));
    }

    [Fact]
    public void Webhook_WithoutConfiguredSecret_IsRejected()
    {
        var payload = CompletedEvent(Guid.NewGuid());

        Assert.Throws<PaymentWebhookException>(() => Gateway(webhookSecret: null).ParseWebhook(payload, Sign(payload)));
    }

    [Theory]
    [InlineData("sk_live_abc", false)]
    [InlineData("rk_live_abc", false)]
    [InlineData("sk_test_abc", true)]
    public void OnlyTestKeys_PassValidation(string key, bool valid)
    {
        var results = new StripeOptions { SecretKey = key }.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(new object()));

        Assert.Equal(valid, !results.Any());
    }
}
