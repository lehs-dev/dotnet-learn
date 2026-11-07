using System.Data;
using ElectroShop.Api.Data;
using ElectroShop.Api.Entities;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Enums;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ElectroShop.Api.Services;

public class AdminService(AppDbContext db, UserManager<ApplicationUser> users)
{
    private IQueryable<Order> Orders => db.Orders.Include(x => x.User).Include(x => x.Items);

    public async Task<DashboardDto> DashboardAsync(CancellationToken ct) =>
        new(
            await db.Products.CountAsync(ct),
            await db.Users.CountAsync(
                x =>
                    db.UserRoles.Any(ur =>
                        ur.UserId == x.Id
                        && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Customer")
                    ),
                ct
            ),
            await db.Orders.CountAsync(ct),
            await db
                .Orders.Where(x => x.PaymentStatus == PaymentStatus.Success)
                .SumAsync(x => (decimal?)x.TotalAmount, ct)
                ?? 0,
            await db.Orders.CountAsync(x => x.OrderStatus == OrderStatus.Pending, ct),
            (await Orders.AsNoTracking().OrderByDescending(x => x.Id).Take(5).ToListAsync(ct))
                .Select(x => x.ToDto())
                .ToList()
        );

    public async Task<PagedResult<OrderDto>> OrdersAsync(
        string? q,
        OrderStatus? status,
        int page,
        int pageSize,
        CancellationToken ct
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Orders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x =>
                x.OrderCode.Contains(q) || x.User.FullName.Contains(q) || x.User.Email!.Contains(q)
            );
        if (status.HasValue)
            query = query.Where(x => x.OrderStatus == status);
        return new(
            (
                await query
                    .OrderByDescending(x => x.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct)
            )
                .Select(x => x.ToDto())
                .ToList(),
            await query.CountAsync(ct),
            page,
            pageSize
        );
    }

    public async Task<OrderDto> StatusAsync(int id, OrderStatus next, CancellationToken ct)
    {
        var order =
            await Orders.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ShopException(404, "Không tìm thấy đơn hàng.");
        if (!BusinessRules.CanTransition(order.OrderStatus, next, order.PaymentStatus))
            throw new ShopException(
                409,
                "Chuyển trạng thái không hợp lệ. Chỉ xác nhận đơn đã thanh toán; chỉ hủy đơn chưa thanh toán."
            );
        order.OrderStatus = next;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return order.ToDto();
    }

    public async Task<PagedResult<CustomerDto>> CustomersAsync(
        string? q,
        int page,
        int pageSize,
        bool customersOnly,
        CancellationToken ct
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Users.AsNoTracking();
        if (customersOnly)
            query = query.Where(x =>
                db.UserRoles.Any(ur =>
                    ur.UserId == x.Id
                    && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Customer")
                )
            );
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x =>
                x.FullName.Contains(q)
                || x.Email!.Contains(q)
                || (x.PhoneNumber != null && x.PhoneNumber.Contains(q))
            );
        }
        var count = await query.CountAsync(ct);
        // SQLite cannot ORDER BY DateTimeOffset. Use a stable alphabetical order,
        // and keep pagination in SQL instead of loading every account into memory.
        var rows = await query
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CustomerDto(
                x.Id,
                x.FullName,
                x.Email!,
                x.PhoneNumber ?? "",
                x.CreatedAt,
                x.IsActive,
                (
                    from ur in db.UserRoles
                    join r in db.Roles on ur.RoleId equals r.Id
                    where ur.UserId == x.Id
                    select r.Name!
                ).ToList(),
                db.Orders.Count(o => o.UserId == x.Id),
                db.Orders.Where(o => o.UserId == x.Id && o.PaymentStatus == PaymentStatus.Success)
                    .Sum(o => (decimal?)o.TotalAmount)
                    ?? 0
            ))
            .ToListAsync(ct);
        return new(rows, count, page, pageSize);
    }

    public async Task<List<OrderDto>> CustomerOrdersAsync(string id, CancellationToken ct) =>
        (
            await Orders
                .AsNoTracking()
                .Where(x => x.UserId == id)
                .OrderByDescending(x => x.Id)
                .Take(100)
                .ToListAsync(ct)
        )
            .Select(x => x.ToDto())
            .ToList();

    public async Task UpdateProfileAsync(string id, CustomerProfileRequest r, CancellationToken ct)
    {
        var user =
            await db.Users.FindAsync([id], ct)
            ?? throw new ShopException(404, "Không tìm thấy tài khoản.");
        user.FullName = r.FullName.Trim();
        user.PhoneNumber = r.PhoneNumber.Trim();
        await db.SaveChangesAsync(ct);
    }

    public async Task AccessAsync(string id, AccountAccessRequest r, CancellationToken ct)
    {
        // Giữ toàn bộ kiểm tra admin cuối cùng và cập nhật Identity trong cùng transaction.
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct
        );
        var user =
            await users.FindByIdAsync(id)
            ?? throw new ShopException(404, "Không tìm thấy tài khoản.");
        var roles = await users.GetRolesAsync(user);
        if (user.IsActive == r.IsActive && roles.Count == 1 && roles[0] == r.Role)
            return;
        bool wasAdmin = roles.Contains("Admin");
        if (wasAdmin && (!r.IsActive || r.Role != "Admin"))
        {
            var adminRole = await db.Roles.SingleAsync(x => x.Name == "Admin", ct);
            int otherAdmins = await db.Users.CountAsync(
                x =>
                    x.Id != id
                    && x.IsActive
                    && db.UserRoles.Any(ur => ur.UserId == x.Id && ur.RoleId == adminRole.Id),
                ct
            );
            BusinessRules.EnsureAdminRemains(
                wasAdmin,
                r.IsActive && r.Role == "Admin",
                otherAdmins
            );
        }
        user.IsActive = r.IsActive;
        Check(await users.UpdateAsync(user));
        if (!roles.SequenceEqual(new[] { r.Role }))
        {
            Check(await users.RemoveFromRolesAsync(user, roles));
            Check(await users.AddToRoleAsync(user, r.Role));
        }
        // Token đã cấp cũng mất hiệu lực ngay khi role/trạng thái thay đổi.
        Check(await users.UpdateSecurityStampAsync(user));
        await tx.CommitAsync(ct);
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new ShopException(409, "Không thể cập nhật tài khoản. Vui lòng tải lại.");
    }
}
