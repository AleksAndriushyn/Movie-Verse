using System.ComponentModel.DataAnnotations;

namespace MovieVerse.Api.Payments;

public sealed class StripeOptions : IValidatableObject
{
    public const string SectionName = "Stripe";

    [Required(ErrorMessage =
        "Stripe is not configured. Locally run: dotnet user-secrets set \"Stripe:SecretKey\" \"sk_test_...\" " +
        "--project backend/src/MovieVerse.Api. On Azure, add the app setting Stripe__SecretKey.")]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Signing secret of the webhook endpoint ("whsec_..."); without it webhooks are refused.</summary>
    public string? WebhookSecret { get; set; }

    // MovieVerse is a demo: refuse to start with a key that could move real money
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(SecretKey) && !SecretKey.StartsWith("sk_test_", StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "Only Stripe test keys (sk_test_...) are allowed; MovieVerse must never take real payments.",
                [nameof(SecretKey)]);
        }
    }
}
