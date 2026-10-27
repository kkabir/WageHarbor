using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Paycrest.Services;

namespace Paycrest.Components.Shared;

/// <summary>Base for interactive pages: shared services and a guarded action runner that reports errors as toasts.</summary>
public abstract class AppComponentBase : ComponentBase
{
    [Inject] protected Db Dbs { get; set; } = default!;
    [Inject] protected ToastService Toast { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected CurrentUser Me { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;

    protected bool Busy { get; private set; }

    /// <summary>Runs an action, shows a success toast, and turns expected failures into an error toast.</summary>
    protected async Task<bool> RunAsync(Func<Task> action, string? success = null)
    {
        if (Busy) return false;
        Busy = true;
        try
        {
            await action();
            if (success != null) Toast.Success(success);
            return true;
        }
        catch (InvalidOperationException ex) { Toast.Error(ex.Message); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) { Toast.Error("Could not save: " + (ex.InnerException?.Message ?? ex.Message)); }
        finally { Busy = false; StateHasChanged(); }
        return false;
    }

    protected ValueTask DownloadTextAsync(string fileName, string contentType, string text) =>
        JS.InvokeVoidAsync("paycrest.downloadText", fileName, contentType, text);
}
