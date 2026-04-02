namespace server.Models;

/// <summary>
/// Represents a user account in the system.
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public decimal Credits { get; set; } = 0; // Balance for AI checks
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; }

    // Navigation properties
    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = [];
}
