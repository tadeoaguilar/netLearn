namespace EfCoreLoggingAndHealthChecks.Models;

/// <summary>Request body for <c>POST /orders</c>.</summary>
public record CreateOrderRequest(int ProductId, int Quantity);
