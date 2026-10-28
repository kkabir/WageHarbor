using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Paycrest.Data;

/// <summary>Refuses sign-in (password, passkey and two-factor alike) for logins whose company the platform owner turned off.</summary>
public sealed class CompanySignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation,
    IDbContextFactory<ApplicationDbContext> dbFactory)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        if (!await base.CanSignInAsync(user)) return false;
        if (user.CompanyId == null) return true;
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Companies.IgnoreQueryFilters().AnyAsync(c => c.Id == user.CompanyId && c.IsActive);
    }
}
