using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;

namespace ElectroShop.Blazor.Services;

public sealed class ApiException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class ShopApiClient(IHttpClientFactory factory, AuthenticationStateProvider authentication, SessionTokenStore tokens)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task<T> GetAsync<T>(string path, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Get, path, null, ct);
    public Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Post, path, body, ct);
    public Task<T> PutAsync<T>(string path, object body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Put, path, body, ct);
    public async Task PostAsync(string path, object body, CancellationToken ct = default) => await SendAsync<object?>(HttpMethod.Post, path, body, ct);
    public async Task DeleteAsync(string path, CancellationToken ct = default) => await SendAsync<object?>(HttpMethod.Delete, path, null, ct);
    public async Task PutAsync(string path, object body, CancellationToken ct = default) => await SendAsync<object?>(HttpMethod.Put, path, body, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        // Trạng thái user thuộc circuit hiện tại, không phụ thuộc HttpContext trong event tương tác.
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        var session = tokens.Get(user.FindFirst("session_id")?.Value);
        if (user.Identity?.IsAuthenticated == true && session is null)
            throw new ApiException("Phiên đăng nhập đã hết hạn. Vui lòng đăng xuất rồi đăng nhập lại.", HttpStatusCode.Unauthorized);
        using var request = new HttpRequestMessage(method, path);
        if (session is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);
        try
        {
            using var client = factory.CreateClient("ShopApi");
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new ApiException(await ErrorAsync(response, ct), response.StatusCode);
            if (response.StatusCode == HttpStatusCode.NoContent)
                return default!;
            return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new ApiException("API trả về dữ liệu trống.");
        }
        catch (HttpRequestException) { throw new ApiException("Không thể kết nối API. Vui lòng kiểm tra backend và cấu hình Api:BaseUrl.", HttpStatusCode.ServiceUnavailable); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException("API phản hồi quá lâu. Vui lòng thử lại.", HttpStatusCode.GatewayTimeout); }
    }

    internal static async Task<string> ErrorAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                return string.Join(" ", errors.EnumerateObject().SelectMany(x => x.Value.EnumerateArray()).Select(x => x.GetString()));
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString()!;
            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString()!;
        }
        catch (JsonException) { }
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Vui lòng đăng nhập lại.",
            HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
            HttpStatusCode.NotFound => "Không tìm thấy dữ liệu.",
            _ => "Không thực hiện được yêu cầu. Vui lòng thử lại."
        };
    }
}
