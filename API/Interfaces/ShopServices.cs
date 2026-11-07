using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;

namespace ElectroShop.Api.Interfaces;

public interface ICartService
{
    Task<CartDto> GetAsync(string userId, CancellationToken ct);
    Task SetAsync(string userId, int productId, int quantity, bool add, CancellationToken ct);
    Task RemoveAsync(string userId, int? productId, CancellationToken ct);
}

public interface IOrderService
{
    Task<OrderDto> CheckoutAsync(string userId, CheckoutRequest request, CancellationToken ct);
    Task<PaymentDto> PayAsync(
        string userId,
        int orderId,
        SandboxPaymentRequest request,
        CancellationToken ct
    );
    Task<PagedResult<OrderDto>> ListAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken ct
    );
    Task<OrderDto> GetAsync(string userId, int orderId, CancellationToken ct);
}
