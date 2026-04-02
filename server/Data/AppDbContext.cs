using Microsoft.EntityFrameworkCore;
using server.Models;

namespace server.Data;

/// <summary>
/// Entity Framework Core database context for NeonDB (PostgreSQL).
/// Manages users, page scores, and payment transactions.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<PageScore> PageScores { get; set; } = null!;
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User entity configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Credits).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // PageScore entity configuration
        modelBuilder.Entity<PageScore>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Url, e.UserId }).IsUnique();
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.Domain).IsRequired().HasMaxLength(255);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PaymentTransaction entity configuration
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.CreditsGranted).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.PaymentMethodId).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(e => e.User)
                .WithMany(u => u.PaymentTransactions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
