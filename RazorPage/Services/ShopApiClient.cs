using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
namespace ElectroShop.RazorPages.Services;

public sealed class ApiException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed class ShopApiClient(HttpClient http, IHttpContextAccessor context, TokenStore tokens)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public async Task<T> GetAsync<T>(string path, CancellationToken ct = default) => (await SendAsync<T>(HttpMethod.Get, path, null, ct))!;
    public Task<T?> PostAsync<T>(string path, object value, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Post, path, value, ct);
    public Task<T?> PutAsync<T>(string path, object value, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Put, path, value, ct);
    public Task DeleteAsync(string path, CancellationToken ct = default) => SendAsync<object>(HttpMethod.Delete, path, null, ct);
    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        var auth = tokens.Get(context.HttpContext?.User.FindFirst("session")?.Value);
        if (auth is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException) { throw new ApiException("Không kết nối được API. Vui lòng kiểm tra backend và thử lại.", 503); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException("API phản hồi quá lâu. Vui lòng thử lại.", 504); }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string message = "Yêu cầu không thành công. Vui lòng thử lại.";
                try
                {
                    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, ct);
                    message = problem?.Detail ?? problem?.Title ?? message;
                    if (problem?.Extensions.TryGetValue("errors", out var errors) == true)
                        message += " " + errors;
                }
                catch (JsonException) { }
                throw new ApiException(message, (int)response.StatusCode);
            }
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return default;
            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
    }
}
