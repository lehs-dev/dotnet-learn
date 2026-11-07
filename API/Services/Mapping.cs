using ElectroShop.Api.Entities;
using ElectroShop.Contracts.DTOs;
using System.Linq.Expressions;
namespace ElectroShop.Api.Services;

public static class Mapping
{
    public static readonly Expression<Func<Product, ProductDto>> ProductProjection = x => new(x.Id, x.CategoryId, x.Category.Name, x.Name, x.Slug, x.SKU, x.Description, x.Price, x.StockQuantity, x.ImageUrl, x.IsActive, Convert.ToBase64String(x.RowVersion));
    public static ProductDto ToDto(this Product x) => new(x.Id, x.CategoryId, x.Category.Name, x.Name, x.Slug, x.SKU, x.Description, x.Price, x.StockQuantity, x.ImageUrl, x.IsActive, Convert.ToBase64String(x.RowVersion));
    public static CategoryDto ToDto(this Category x) => new(x.Id, x.Name, x.Slug, x.Description, x.IsActive);
    public static OrderDto ToDto(this Order x) => new(x.Id, x.OrderCode, x.UserId, x.User.FullName, x.ReceiverName, x.ReceiverPhone, x.ShippingAddress, x.Note, x.TotalAmount, x.OrderStatus, x.PaymentStatus, x.CreatedAt, x.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.ProductSKU, i.UnitPrice, i.Quantity, i.LineTotal)).ToList());
    public static PaymentDto ToDto(this PaymentTransaction x) => new(x.Id, x.OrderId, x.TransactionCode, x.Provider, x.Amount, x.Status, x.CreatedAt, x.PaidAt, x.FailureReason);
}
