using System.ComponentModel.DataAnnotations;

namespace Paycrest.Domain;

// Platform-wide support records. They are not company-owned: only the platform owner sees them.

public enum ErrorStatus { New, Investigating, Resolved, Ignored }

/// <summary>One kind of error. Repeats of the same error (same <see cref="Fingerprint"/>) are counted on one row.</summary>
public class ErrorLog
{
    public int Id { get; set; }
    /// <summary>Hash of the exception type, message shape and first app stack frame.</summary>
    [Required, StringLength(40)] public string Fingerprint { get; set; } = "";
    [StringLength(20)] public string Level { get; set; } = "Error";
    [StringLength(200)] public string? Source { get; set; }
    [StringLength(200)] public string? ExceptionType { get; set; }
    [Required, StringLength(2000)] public string Message { get; set; } = "";
    /// <summary>Stack trace and inner exceptions of the latest occurrence.</summary>
    public string? Details { get; set; }
    [StringLength(400)] public string? Path { get; set; }
    [StringLength(10)] public string? Method { get; set; }
    [StringLength(256)] public string? UserName { get; set; }
    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public int Count { get; set; } = 1;
    public ErrorStatus Status { get; set; } = ErrorStatus.New;
    public DateTime? ResolvedUtc { get; set; }
    [StringLength(256)] public string? ResolvedBy { get; set; }
    public List<CaseNote> Notes { get; set; } = [];
}

public enum ContactStatus { New, Replied, Closed, Spam }

/// <summary>A message sent through the public Contact us page.</summary>
public class ContactMessage
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, StringLength(256), EmailAddress] public string Email { get; set; } = "";
    [StringLength(160)] public string? CompanyName { get; set; }
    [StringLength(40)] public string? Phone { get; set; }
    [Required, StringLength(40)] public string Topic { get; set; } = "";
    [Required, StringLength(5000)] public string Message { get; set; } = "";
    /// <summary>Set when the sender was signed in.</summary>
    [StringLength(450)] public string? UserId { get; set; }
    public int? CompanyId { get; set; }
    public ContactStatus Status { get; set; } = ContactStatus.New;
    public DateTime? HandledUtc { get; set; }
    [StringLength(256)] public string? HandledBy { get; set; }
    public List<CaseNote> Notes { get; set; } = [];
}

/// <summary>A dated remark or status change on an error or a contact message.</summary>
public class CaseNote
{
    public int Id { get; set; }
    public int? ErrorLogId { get; set; }
    public int? ContactMessageId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    [StringLength(256)] public string? CreatedBy { get; set; }
    [Required, StringLength(4000)] public string Text { get; set; } = "";
}

/// <summary>A one-time link a new hire uses to enter their own details, W-4 and bank account, and create their login.</summary>
public class OnboardingInvite : ICompanyOwned
{
    public int CompanyId { get; set; }
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    [Required, StringLength(256)] public string Email { get; set; } = "";
    /// <summary>SHA-256 of the link token; the token itself is only ever in the email.</summary>
    [Required, StringLength(64)] public string TokenHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    [StringLength(256)] public string? CreatedBy { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    /// <summary>The name the new hire typed as their electronic signature, and where it came from.</summary>
    [StringLength(160)] public string? SignedName { get; set; }
    [StringLength(45)] public string? SignedIp { get; set; }

    public bool IsOpen(DateTime nowUtc) => CompletedUtc == null && RevokedUtc == null && ExpiresUtc > nowUtc;
}

/// <summary>
/// One page view, for the platform owner's visitor log. Holds no IP address or email: <see cref="VisitorHash"/>
/// is a hash of IP and browser with a salt that changes daily, so it counts unique visitors per day without identifying anyone.
/// </summary>
public class PageVisit
{
    public long Id { get; set; }
    public DateTime TimeUtc { get; set; }
    [Required, StringLength(400)] public string Path { get; set; } = "";
    /// <summary>The page or site the visitor came from, without its query string.</summary>
    [StringLength(400)] public string? Referrer { get; set; }
    [StringLength(16)] public string? VisitorHash { get; set; }
    /// <summary>Desktop, Mobile, Tablet or Bot.</summary>
    [StringLength(10)] public string Device { get; set; } = "Desktop";
    [StringLength(20)] public string? Browser { get; set; }
    /// <summary>Two-letter country code when the host or CDN supplies one (CF-IPCountry header).</summary>
    [StringLength(2)] public string? Country { get; set; }
    [StringLength(10)] public string? Language { get; set; }
    public int StatusCode { get; set; }
    public bool SignedIn { get; set; }
    public int? CompanyId { get; set; }
}
