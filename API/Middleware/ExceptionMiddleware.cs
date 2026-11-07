using ElectroShop.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace ElectroShop.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // EF có thể bọc lỗi SQLite trong InvalidOperationException. Vẫn trả 409
            // để frontend thử lại cùng idempotency key khi SQLite gặp busy/lock.
            var sqlite = FindSqliteException(ex);
            var (status, detail) = ex switch
            {
                ShopException shop => (shop.StatusCode, shop.Message),
                _ when sqlite?.SqliteErrorCode is 5 or 6 => (409, "Có thao tác đồng thời. Vui lòng thử lại với cùng mã thao tác."),
                DbUpdateConcurrencyException => (409, "Dữ liệu đã thay đổi. Vui lòng tải lại và thử lại với cùng mã thao tác."),
                DbUpdateException => (409, "Dữ liệu bị trùng hoặc đã thay đổi. Vui lòng tải lại và thử lại."),
                _ when sqlite is not null => (503, "Chưa thể kết nối cơ sở dữ liệu. Vui lòng kiểm tra cấu hình cơ sở dữ liệu."),
                _ => (500, "Có lỗi khi xử lý yêu cầu. Vui lòng thử lại sau.")
            };
            if (status >= 500)
                logger.LogError(ex, "Request failed {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = "Không thể thực hiện yêu cầu", Detail = detail, Extensions = { ["traceId"] = context.TraceIdentifier } }, context.RequestAborted);
        }
    }

    private static SqliteException? FindSqliteException(Exception exception)
    {
        if (exception is SqliteException sql)
            return sql;
        return exception.InnerException is { } inner ? FindSqliteException(inner) : null;
    }
}
