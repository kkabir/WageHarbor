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
    [Inject] private ILoggerFactory LoggerFactory { get; set; } = default!;
    /// <summary>Screen text in the current language; see Lang.</summary>
    [Inject] protected Paycrest.Services.Localization.Lang L { get; set; } = default!;

    protected bool Busy { get; private set; }

    /// <summary>Runs an action, shows a success toast, and turns expected failures into an error toast.</summary>
    protected async Task<bool> RunAsync(Func<Task> action, string? success = null)
    {
        if (Busy) return false;
        Busy = true;
        try
        {
            await action();
            // Messages are English in code; show them in the current language when a translation exists.
            if (success != null) Toast.Success(L[success]);
            return true;
        }
        catch (InvalidOperationException ex) { Toast.Error(L[ex.Message]); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            // Shown to the user, and recorded in the error log for the platform owner.
            LoggerFactory.CreateLogger(GetType()).LogError(ex, "Save failed on {Page}", GetType().Name);
            Toast.Error(L["Could not save: {0}", ex.InnerException?.Message ?? ex.Message]);
        }
        finally { Busy = false; StateHasChanged(); }
        return false;
    }

    protected ValueTask DownloadTextAsync(string fileName, string contentType, string text) =>
        JS.InvokeVoidAsync("paycrest.downloadText", fileName, contentType, text);
}
