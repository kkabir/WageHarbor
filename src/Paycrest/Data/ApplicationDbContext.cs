using System.Reflection;
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
    public DbSet<CompanyLogo> CompanyLogos => Set<CompanyLogo>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<WiseSaying> WiseSayings => Set<WiseSaying>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<CaseNote> CaseNotes => Set<CaseNote>();

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

    /// <summary>
    /// The company this context works for. Company data is filtered to it and new rows are stamped with it.
    /// Null sees no company data at all; platform-wide code opts out with IgnoreQueryFilters().
    /// </summary>
    public int? CurrentCompanyId { get; set; }

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
            e.HasIndex(x => new { x.CompanyId, x.EmployeeNumber }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany(d => d.Employees).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<DeductionType>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        builder.Entity<EarningType>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
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
        builder.Entity<ErrorLog>(e =>
        {
            e.HasIndex(x => x.Fingerprint).IsUnique();
            e.HasIndex(x => new { x.Status, x.LastSeenUtc });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasMany(x => x.Notes).WithOne().HasForeignKey(n => n.ErrorLogId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ContactMessage>(e =>
        {
            e.HasIndex(x => new { x.Status, x.CreatedUtc });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasMany(x => x.Notes).WithOne().HasForeignKey(n => n.ContactMessageId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<CompanyLogo>().HasKey(x => x.CompanyId);
        builder.Entity<CompanyLogo>().Property(x => x.CompanyId).ValueGeneratedNever();

        // ---------- Company isolation ----------
        builder.Entity<ApplicationUser>().HasOne<Company>().WithMany().HasForeignKey(u => u.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Company>().HasQueryFilter(c => c.Id == CurrentCompanyId);
        builder.Entity<AuditLog>().HasQueryFilter(a => a.CompanyId == CurrentCompanyId);
        builder.Entity<AuditLog>().HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);

        var configure = GetType().GetMethod(nameof(ConfigureCompanyOwned), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var type in builder.Model.GetEntityTypes().Select(t => t.ClrType).Where(typeof(ICompanyOwned).IsAssignableFrom).ToList())
            configure.MakeGenericMethod(type).Invoke(this, [builder]);
    }

    private void ConfigureCompanyOwned<T>(ModelBuilder builder) where T : class, ICompanyOwned
    {
        builder.Entity<T>().HasQueryFilter(e => e.CompanyId == CurrentCompanyId);
        builder.Entity<T>().HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>Stamps new company rows with <see cref="CurrentCompanyId"/> and refuses changes to another company's rows.</summary>
    private void ApplyCompanyRules()
    {
        foreach (var entry in ChangeTracker.Entries<ICompanyOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CompanyId == 0)
                entry.Entity.CompanyId = CurrentCompanyId ?? throw new InvalidOperationException($"No company set for new {entry.Entity.GetType().Name}.");
            else if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                     && CurrentCompanyId != null && entry.Entity.CompanyId != CurrentCompanyId)
                throw new InvalidOperationException("That record belongs to a different company.");
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyCompanyRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
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
        ApplyCompanyRules();
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
                CompanyId = entry.Entity switch { ICompanyOwned o => o.CompanyId, Company c => c.Id, _ => null },
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
