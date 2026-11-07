using ElectroShop.Api.Authentication;
using ElectroShop.Api.Interfaces;
using ElectroShop.Api.Services;

namespace ElectroShop.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShopServices(this IServiceCollection services)
    {
        services.AddScoped<TokenService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<AdminService>();
        services.AddScoped<AdminCatalogService>();
        return services;
    }
}
