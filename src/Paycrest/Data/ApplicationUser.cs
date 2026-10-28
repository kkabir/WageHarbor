using Microsoft.AspNetCore.Identity;

namespace Paycrest.Data;

public class ApplicationUser : IdentityUser
{
    /// <summary>The company this login belongs to. Null only for a platform owner without a company of their own.</summary>
    public int? CompanyId { get; set; }
}
