using Microsoft.AspNetCore.Identity;
using Paycrest.Data;

namespace Paycrest.Services;

public enum UserState { Active, Locked, Deactivated }

/// <summary>
/// Deactivating a login uses Identity's lockout with no end date, so sign-in refuses it like any locked account.
/// A lockout after too many wrong passwords ends by itself and shows as <see cref="UserState.Locked"/>.
/// </summary>
public static class UserStates
{
    public static UserState Of(ApplicationUser u) =>
        u.LockoutEnd is not { } end || end <= DateTimeOffset.UtcNow ? UserState.Active
        : end.Year >= 9000 ? UserState.Deactivated
        : UserState.Locked;

    public static async Task SetActiveAsync(UserManager<ApplicationUser> users, ApplicationUser u, bool active)
    {
        await users.SetLockoutEnabledAsync(u, true);
        await users.SetLockoutEndDateAsync(u, active ? null : DateTimeOffset.MaxValue);
        if (active) await users.ResetAccessFailedCountAsync(u);
        // A new security stamp signs the person out of open sessions at the next revalidation.
        else await users.UpdateSecurityStampAsync(u);
    }
}
