using System.Security.Claims;
using ElectroShop.Api.Data;
using ElectroShop.Api.Interfaces;
using ElectroShop.Api.Services;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ElectroShop.Api.Controllers;

[ApiController, Authorize, Route("api/cart")]
public class CartController(ICartService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet] public Task<CartDto> Get(CancellationToken ct) => service.GetAsync(UserId, ct);
    [HttpPost("items")]
    public async Task<IActionResult> Add(CartQuantityRequest r, CancellationToken ct)
    {
        await service.SetAsync(UserId, r.ProductId, r.Quantity, true, ct);
        return NoContent();
    }
    [HttpPut("items/{productId:int}")]
    public async Task<IActionResult> Update(int productId, CartQuantityRequest r, CancellationToken ct)
    {
        await service.SetAsync(UserId, productId, r.Quantity, false, ct);
        return NoContent();
    }
    [HttpDelete("items/{productId:int}")]
    public async Task<IActionResult> Remove(int productId, CancellationToken ct)
    {
        await service.RemoveAsync(UserId, productId, ct);
        return NoContent();
    }
    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await service.RemoveAsync(UserId, null, ct);
        return NoContent();
    }
}
[ApiController, Authorize, Route("api/orders")]
public class OrdersController(IOrderService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet] public Task<PagedResult<OrderDto>> Get(int page = 1, int pageSize = 20, CancellationToken ct = default) => service.ListAsync(UserId, page, pageSize, ct);
    [HttpGet("{id:int}")] public Task<OrderDto> Get(int id, CancellationToken ct) => service.GetAsync(UserId, id, ct);
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Checkout(CheckoutRequest r, CancellationToken ct)
    {
        var order = await service.CheckoutAsync(UserId, r, ct);
        return CreatedAtAction(nameof(Get), new
        {
            id = order.Id
        }, order);
    }
}
[ApiController, Authorize, Route("api/payments")]
public class PaymentsController(IOrderService service, AppDbContext db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpPost("{orderId:int}")] public Task<PaymentDto> Pay(int orderId, SandboxPaymentRequest r, CancellationToken ct) => service.PayAsync(UserId, orderId, r, ct);
    [HttpGet("{orderId:int}")]
    public async Task<List<PaymentDto>> Status(int orderId, CancellationToken ct)
    {
        if (!await db.Orders.AnyAsync(x => x.Id == orderId && x.UserId == UserId, ct))
            throw new ShopException(404, "Không tìm thấy đơn hàng.");
        return (await db.PaymentTransactions.AsNoTracking().Where(x => x.OrderId == orderId).OrderByDescending(x => x.Id).ToListAsync(ct)).Select(x => x.ToDto()).ToList();
    }
}
