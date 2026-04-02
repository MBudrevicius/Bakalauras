using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;

namespace server.Endpoints;

/// <summary>
/// Payment endpoints for users to purchase credits.
/// Currently supports processing credit transfers (integrate with Stripe/PayPal as needed).
/// </summary>
public static class PaymentEndpoints
{
    // This constant defines how many credits per dollar spent
    private const decimal CREDITS_PER_DOLLAR = 100;

    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/payment")
            .WithName("Payment Management")
            .WithOpenApi();

        group.MapPost("/credit-packages", GetCreditPackages)
            .WithName("Get Credit Packages")
            .WithSummary("Get available credit packages");

        group.MapPost("/purchase", PurchaseCredits)
            .WithName("Purchase Credits")
            .WithSummary("Purchase credits (requires authentication)");

        group.MapGet("/transactions", GetTransactions)
            .WithName("Get Payment History")
            .WithSummary("Get user's payment transaction history");

        // Admin endpoint to grant credits (development/testing only)
        group.MapPost("/admin/grant-credits", AdminGrantCredits)
            .Produces<object>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("Admin Grant Credits")
            .WithSummary("Admin: Grant credits to a user (development only)");
    }

    private static Task<IResult> GetCreditPackages()
    {
        var packages = new[]
        {
            new { id = 1, dollars = 5, credits = 500, label = "Starter" },
            new { id = 2, dollars = 10, credits = 1100, label = "Popular" }, // 10% bonus
            new { id = 3, dollars = 25, credits = 2875, label = "Professional" }, // 15% bonus
            new { id = 4, dollars = 50, credits = 6000, label = "Enterprise" } // 20% bonus
        };

        return Task.FromResult(Results.Ok(packages));
    }

    private static async Task<IResult> PurchaseCredits(
        PurchaseRequest request,
        ClaimsPrincipal claims,
        AppDbContext context)
    {
        var userIdClaim = claims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Results.Unauthorized();

        var user = await context.Users.FindAsync(userId);
        if (user == null)
            return Results.NotFound();

        if (request.Amount <= 0)
            return Results.BadRequest(new { error = "Amount must be positive." });

        // Calculate credits granted
        var creditsGranted = (decimal)request.Amount * CREDITS_PER_DOLLAR;

        // Create payment transaction (mark as completed for now - integrate Stripe/PayPal later)
        var transaction = new PaymentTransaction
        {
            UserId = userId,
            Amount = request.Amount,
            CreditsGranted = creditsGranted,
            PaymentMethodId = request.PaymentMethodId ?? "manual",
            Status = "completed", // In production, would be "pending" until Stripe confirms
            CompletedAt = DateTime.UtcNow
        };

        context.PaymentTransactions.Add(transaction);

        // Credit the user
        user.Credits += creditsGranted;

        await context.SaveChangesAsync();

        return Results.Ok(new
        {
            transaction.Id,
            transaction.Amount,
            transaction.CreditsGranted,
            userCreditsAfter = user.Credits,
            message = $"Successfully purchased {creditsGranted} credits!"
        });
    }

    private static async Task<IResult> GetTransactions(
        ClaimsPrincipal claims,
        AppDbContext context)
    {
        var userIdClaim = claims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Results.Unauthorized();

        var transactions = await context.PaymentTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.Amount,
                t.CreditsGranted,
                t.Status,
                t.CreatedAt,
                t.CompletedAt
            })
            .ToListAsync();

        return Results.Ok(transactions);
    }

    private static async Task<IResult> AdminGrantCredits(
        AdminGrantRequest request,
        AppDbContext context)
    {
        // TODO: Add proper admin authentication/authorization
        // For now, this is open for development - REMOVE IN PRODUCTION

        if (request.UserId <= 0 || request.Credits <= 0)
            return Results.BadRequest(new { error = "UserId and Credits must be positive." });

        var user = await context.Users.FindAsync(request.UserId);
        if (user == null)
            return Results.NotFound(new { error = "User not found." });

        user.Credits += request.Credits;

        var transaction = new PaymentTransaction
        {
            UserId = request.UserId,
            Amount = 0,
            CreditsGranted = request.Credits,
            PaymentMethodId = "admin-grant",
            Status = "completed",
            CompletedAt = DateTime.UtcNow
        };

        context.PaymentTransactions.Add(transaction);
        await context.SaveChangesAsync();

        return Results.Ok(new
        {
            message = $"Granted {request.Credits} credits to user {request.UserId}",
            userCreditsAfter = user.Credits
        });
    }
}

public class PurchaseRequest
{
    public decimal Amount { get; set; } // USD amount
    public string? PaymentMethodId { get; set; } // Stripe token/ID
}

public class AdminGrantRequest
{
    public int UserId { get; set; }
    public decimal Credits { get; set; }
}
