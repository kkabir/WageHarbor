using System.Text;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Paycrest.Domain;

namespace Paycrest.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeBankAccount> EmployeeBankAccounts => Set<EmployeeBankAccount>();
    public DbSet<DeductionType> DeductionTypes => Set<DeductionType>();
    public DbSet<EmployeeDeduction> EmployeeDeductions => Set<EmployeeDeduction>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();

    public DbSet<EarningType> EarningTypes => Set<EarningType>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollAdjustment> PayrollAdjustments => Set<PayrollAdjustment>();
    public DbSet<Paycheck> Paychecks => Set<Paycheck>();
    public DbSet<PaycheckLine> PaycheckLines => Set<PaycheckLine>();
    public DbSet<PaycheckDeposit> PaycheckDeposits => Set<PaycheckDeposit>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<TimeOffPolicy> TimeOffPolicies => Set<TimeOffPolicy>();
    public DbSet<TimeOffBalance> TimeOffBalances => Set<TimeOffBalance>();
    public DbSet<TimeOffRequest> TimeOffRequests => Set<TimeOffRequest>();
    public DbSet<TimeOffTransaction> TimeOffTransactions => Set<TimeOffTransaction>();

    public DbSet<TaxYear> TaxYears => Set<TaxYear>();
    public DbSet<FederalTaxBracket> FederalTaxBrackets => Set<FederalTaxBracket>();
    public DbSet<StateTaxRule> StateTaxRules => Set<StateTaxRule>();
    public DbSet<StateTaxBracket> StateTaxBrackets => Set<StateTaxBracket>();

    /// <summary>User name recorded in the audit log for changes saved through this context.</summary>
    public string? AuditUser { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
        configurationBuilder.Properties<decimal?>().HavePrecision(18, 4);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Employee>(e =>
        {
            e.HasIndex(x => x.EmployeeNumber).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany(d => d.Employees).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<DeductionType>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<EarningType>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<TimeOffBalance>().HasIndex(x => new { x.EmployeeId, x.TimeOffPolicyId }).IsUnique();
        builder.Entity<TimeEntry>().HasIndex(x => new { x.EmployeeId, x.WorkDate });
        builder.Entity<TimeEntry>().HasOne(x => x.EarningType).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TimeOffPolicy>().HasOne(x => x.EarningType).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TimeOffRequest>().HasOne(x => x.TimeOffPolicy).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TimeOffBalance>().HasOne(x => x.TimeOffPolicy).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TimeOffTransaction>().HasOne(x => x.TimeOffPolicy).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TimeOffTransaction>().HasIndex(x => new { x.EmployeeId, x.TimeOffPolicyId });
        builder.Entity<EmployeeDeduction>().HasOne(x => x.DeductionType).WithMany().OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Paycheck>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.PayDate });
            e.HasOne(x => x.Employee).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PaycheckId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Deposits).WithOne().HasForeignKey(d => d.PaycheckId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<PayrollAdjustment>(e =>
        {
            e.HasOne(x => x.Employee).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.EarningType).WithMany().OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PayrollRun>().HasIndex(x => x.PayDate);

        builder.Entity<TaxYear>().HasKey(x => x.Year);
        builder.Entity<FederalTaxBracket>().HasOne<TaxYear>().WithMany(t => t.Brackets).HasForeignKey(b => b.Year);
        builder.Entity<StateTaxRule>().HasIndex(x => new { x.StateCode, x.Year }).IsUnique();
        builder.Entity<AuditLog>().HasIndex(x => x.TimestampUtc);
    }

    // ---------- Audit trail ----------

    private static readonly HashSet<Type> AuditedTypes =
    [
        typeof(Company), typeof(Department), typeof(Employee), typeof(EmployeeBankAccount), typeof(EmployeeDeduction),
        typeof(DeductionType), typeof(EarningType), typeof(PayrollRun), typeof(TimeOffPolicy), typeof(TimeOffRequest),
        typeof(TaxYear), typeof(FederalTaxBracket), typeof(StateTaxRule), typeof(Holiday), typeof(EmployeeDocument)
    ];

    private static readonly HashSet<string> SensitiveProps =
        ["SsnProtected", "AccountNumberProtected", "BankAccountNumberProtected", "Content"];

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var pending = ChangeTracker.Entries()
            .Where(e => AuditedTypes.Contains(e.Entity.GetType()) &&
                        e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (Entry: e, Action: e.State.ToString(), Details: Describe(e)))
            .Where(p => p.Action != nameof(EntityState.Modified) || p.Details != null)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);
        if (pending.Count == 0) return result;

        foreach (var (entry, action, details) in pending)
        {
            AuditLogs.Add(new AuditLog
            {
                UserName = AuditUser ?? "system",
                Action = action,
                EntityType = entry.Entity.GetType().Name,
                EntityId = string.Join(",", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue)),
                Details = details
            });
        }
        await base.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static string? Describe(EntityEntry e)
    {
        if (e.State != EntityState.Modified) return null;
        var sb = new StringBuilder();
        foreach (var p in e.Properties.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)))
        {
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(p.Metadata.Name);
            if (!SensitiveProps.Contains(p.Metadata.Name))
                sb.Append($": {Trim(p.OriginalValue)} → {Trim(p.CurrentValue)}");
            else
                sb.Append(": [changed]");
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static string Trim(object? v)
    {
        var s = v?.ToString() ?? "∅";
        return s.Length > 60 ? s[..60] + "…" : s;
    }
}
