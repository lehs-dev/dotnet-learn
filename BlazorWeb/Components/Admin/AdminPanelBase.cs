using ElectroShop.Blazor.Services;
using Microsoft.AspNetCore.Components;

namespace ElectroShop.Blazor.Components.Admin;

public abstract class AdminPanelBase : ComponentBase
{
    [Inject]
    protected ShopApiClient Api { get; set; } = default!;
    protected string? Error { get; set; }
    protected string? Message { get; set; }
    protected bool Busy { get; set; }

    protected async Task Run(Func<Task> action, string? success = null)
    {
        if (Busy)
            return;
        Busy = true;
        Error = Message = null;
        try
        {
            await action();
            Message = success;
        }
        catch (ApiException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }
}
