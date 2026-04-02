namespace server.Models;

/// <summary>
/// Represents a payment transaction where users add credits.
/// </summary>
public class PaymentTransaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; } // USD amount
    public decimal CreditsGranted { get; set; }
    public string PaymentMethodId { get; set; } = string.Empty; // Stripe/Stripe payment ID
    public string Status { get; set; } = "pending"; // pending, completed, failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation property
    public virtual User? User { get; set; }
}
