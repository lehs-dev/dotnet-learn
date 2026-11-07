using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElectroShop.Mvc.Services;

public sealed class ApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class ShopApiClient(HttpClient http, IHttpContextAccessor context, TokenStore tokens)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task<T> GetAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Get, path, null, ct);

    public Task<T> PostAsync<T>(string path, object value, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Post, path, value, ct);

    public Task<T> PutAsync<T>(string path, object value, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Put, path, value, ct);

    public async Task DeleteAsync(string path, CancellationToken ct = default) =>
        _ = await SendAsync<object?>(HttpMethod.Delete, path, null, ct);

    public async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? value,
        CancellationToken ct = default
    )
    {
        using var request = new HttpRequestMessage(method, path);
        var sessionId = context.HttpContext?.User.FindFirst("session_id")?.Value;
        var auth = tokens.Get(sessionId);
        if (auth is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                auth.AccessToken
            );
        if (value is not null)
            request.Content = JsonContent.Create(value, options: Json);
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new ApiException(
                "Không kết nối được API. Vui lòng kiểm tra backend và thử lại.",
                HttpStatusCode.ServiceUnavailable
            );
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(
                "API phản hồi quá lâu. Vui lòng thử lại.",
                HttpStatusCode.GatewayTimeout
            );
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized && auth is not null)
                    tokens.Remove(sessionId);
                var message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized =>
                        "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                    HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
                    _ => "Không thể thực hiện yêu cầu. Vui lòng thử lại.",
                };
                try
                {
                    using var problem = JsonDocument.Parse(
                        await response.Content.ReadAsStringAsync(ct)
                    );
                    if (
                        problem.RootElement.TryGetProperty("detail", out var detail)
                        && detail.ValueKind == JsonValueKind.String
                    )
                        message = detail.GetString()!;
                    else if (problem.RootElement.TryGetProperty("title", out var title))
                        message = title.GetString() ?? message;
                    if (
                        problem.RootElement.TryGetProperty("errors", out var errors)
                        && errors.ValueKind == JsonValueKind.Object
                    )
                        message = string.Join(
                            " ",
                            errors
                                .EnumerateObject()
                                .SelectMany(p =>
                                    p.Value.EnumerateArray().Select(x => x.GetString())
                                )
                        );
                }
                catch (JsonException) { }
                throw new ApiException(message, response.StatusCode);
            }
            if (response.StatusCode == HttpStatusCode.NoContent)
                return default!;
            return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                ?? throw new ApiException(
                    "API trả về dữ liệu không hợp lệ.",
                    HttpStatusCode.BadGateway
                );
        }
    }
}
