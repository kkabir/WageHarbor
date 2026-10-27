using Microsoft.EntityFrameworkCore;
using Paycrest.Data;
using Paycrest.Domain;
using Paycrest.Services.Payroll;

namespace Paycrest.Services;

public sealed class TimeService(Db dbs, CurrentUser user, Clock clock)
{
    public async Task<TimeEntry?> GetOpenPunchAsync(int employeeId)
    {
        await using var db = await dbs.OpenAsync();
        return await db.TimeEntries.AsNoTracking()
            .Where(t => t.EmployeeId == employeeId && t.ClockInUtc != null && t.ClockOutUtc == null)
            .OrderByDescending(t => t.ClockInUtc).FirstOrDefaultAsync();
    }

    public async Task ClockInAsync(int employeeId)
    {
        await using var db = await dbs.OpenAsync();
        if (await db.TimeEntries.AnyAsync(t => t.EmployeeId == employeeId && t.ClockInUtc != null && t.ClockOutUtc == null))
            throw new InvalidOperationException("You are already clocked in.");
        var reg = await db.EarningTypes.FirstAsync(e => e.Code == EarningType.Regular);
        db.TimeEntries.Add(new TimeEntry
        {
            EmployeeId = employeeId, WorkDate = await clock.TodayAsync(), EarningTypeId = reg.Id,
            ClockInUtc = DateTime.UtcNow, Source = "Clock", Status = ApprovalStatus.Pending
        });
        await db.SaveChangesAsync();
    }

    public async Task<decimal> ClockOutAsync(int employeeId, string? notes)
    {
        await using var db = await dbs.OpenAsync();
        var open = await db.TimeEntries.Where(t => t.EmployeeId == employeeId && t.ClockInUtc != null && t.ClockOutUtc == null)
                       .OrderByDescending(t => t.ClockInUtc).FirstOrDefaultAsync()
                   ?? throw new InvalidOperationException("You are not clocked in.");
        open.ClockOutUtc = DateTime.UtcNow;
        open.Hours = Math.Min(24, Math.Round((decimal)(open.ClockOutUtc.Value - open.ClockInUtc!.Value).TotalHours, 2));
        open.Notes = notes;
        await db.SaveChangesAsync();
        return open.Hours;
    }

    public async Task<List<TimeEntry>> ListAsync(DateOnly from, DateOnly to, int? employeeId = null, ApprovalStatus? status = null, int? managerEmployeeId = null)
    {
        await using var db = await dbs.OpenAsync();
        var q = db.TimeEntries.AsNoTracking().Include(t => t.Employee).Include(t => t.EarningType)
            .Where(t => t.WorkDate >= from && t.WorkDate <= to);
        if (employeeId.HasValue) q = q.Where(t => t.EmployeeId == employeeId);
        if (status.HasValue) q = q.Where(t => t.Status == status);
        if (managerEmployeeId.HasValue) q = q.Where(t => t.Employee!.ManagerId == managerEmployeeId);
        return await q.OrderBy(t => t.Employee!.LastName).ThenBy(t => t.WorkDate).ThenBy(t => t.ClockInUtc).ToListAsync();
    }

    public async Task SaveAsync(TimeEntry entry, bool selfService)
    {
        await using var db = await dbs.OpenAsync();
        if (entry.Hours <= 0 || entry.Hours > 24) throw new InvalidOperationException("Hours must be between 0 and 24.");
        if (entry.Id == 0)
        {
            entry.Source = selfService ? "Self" : "Manual";
            entry.Status = selfService ? ApprovalStatus.Pending : ApprovalStatus.Approved;
            if (!selfService) { entry.ApprovedBy = await user.GetNameAsync(); entry.ApprovedUtc = DateTime.UtcNow; }
            entry.Employee = null; entry.EarningType = null;
            db.TimeEntries.Add(entry);
        }
        else
        {
            var existing = await db.TimeEntries.FirstAsync(t => t.Id == entry.Id);
            if (existing.IsLocked) throw new InvalidOperationException("This entry has been paid and is locked.");
            if (selfService && existing.Status == ApprovalStatus.Approved) throw new InvalidOperationException("Approved entries can only be changed by a manager.");
            existing.WorkDate = entry.WorkDate;
            existing.Hours = entry.Hours;
            existing.EarningTypeId = entry.EarningTypeId;
            existing.Notes = entry.Notes;
            if (selfService) existing.Status = ApprovalStatus.Pending;
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, bool selfService)
    {
        await using var db = await dbs.OpenAsync();
        var e = await db.TimeEntries.FirstAsync(t => t.Id == id);
        if (e.IsLocked) throw new InvalidOperationException("This entry has been paid and is locked.");
        if (selfService && e.Status == ApprovalStatus.Approved) throw new InvalidOperationException("Approved entries can only be removed by a manager.");
        db.TimeEntries.Remove(e);
        await db.SaveChangesAsync();
    }

    public async Task SetStatusAsync(IEnumerable<int> ids, ApprovalStatus status)
    {
        var list = ids.ToList();
        await using var db = await dbs.OpenAsync();
        var who = await user.GetNameAsync();
        // Skip paid entries and punches that are still open.
        var entries = await db.TimeEntries
            .Where(t => list.Contains(t.Id) && t.PayrollRunId == null && !(t.ClockInUtc != null && t.ClockOutUtc == null))
            .ToListAsync();
        foreach (var e in entries)
        {
            e.Status = status;
            e.ApprovedBy = who;
            e.ApprovedUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }
}

public sealed class TimeOffService(Db dbs, CurrentUser user)
{
    public async Task<List<TimeOffBalance>> GetBalancesAsync(int employeeId)
    {
        await using var db = await dbs.OpenAsync();
        return await db.TimeOffBalances.AsNoTracking().Include(b => b.TimeOffPolicy)
            .Where(b => b.EmployeeId == employeeId && b.TimeOffPolicy!.IsActive).OrderBy(b => b.TimeOffPolicy!.Name).ToListAsync();
    }

    public async Task<List<TimeOffRequest>> ListRequestsAsync(int? employeeId = null, ApprovalStatus? status = null, int? managerEmployeeId = null)
    {
        await using var db = await dbs.OpenAsync();
        var q = db.TimeOffRequests.AsNoTracking().Include(r => r.Employee).Include(r => r.TimeOffPolicy).AsQueryable();
        if (employeeId.HasValue) q = q.Where(r => r.EmployeeId == employeeId);
        if (status.HasValue) q = q.Where(r => r.Status == status);
        if (managerEmployeeId.HasValue) q = q.Where(r => r.Employee!.ManagerId == managerEmployeeId);
        return await q.OrderByDescending(r => r.StartDate).Take(500).ToListAsync();
    }

    public static decimal SuggestHours(DateOnly start, DateOnly end, decimal hoursPerDay = 8m) =>
        PayrollCalculator.Weekdays(start, end) * hoursPerDay;

    public async Task SubmitAsync(TimeOffRequest request)
    {
        if (request.EndDate < request.StartDate) throw new InvalidOperationException("End date must be on or after start date.");
        await using var db = await dbs.OpenAsync();
        var policy = await db.TimeOffPolicies.FirstAsync(p => p.Id == request.TimeOffPolicyId);
        var bal = await db.TimeOffBalances.FirstOrDefaultAsync(b => b.EmployeeId == request.EmployeeId && b.TimeOffPolicyId == policy.Id);
        if (!policy.AllowNegativeBalance && (bal?.Balance ?? 0) < request.Hours)
            throw new InvalidOperationException($"Insufficient {policy.Name} balance ({Fmt.Hours(bal?.Balance ?? 0)} hours available).");
        request.Id = 0;
        request.Status = ApprovalStatus.Pending;
        request.RequestedUtc = DateTime.UtcNow;
        request.Employee = null; request.TimeOffPolicy = null;
        db.TimeOffRequests.Add(request);
        await db.SaveChangesAsync();
    }

    /// <summary>Approving deducts the balance and creates approved time entries so hourly employees are paid for the leave.</summary>
    public async Task ReviewAsync(int requestId, bool approve, string? note)
    {
        await using var db = await dbs.OpenAsync();
        await db.InTransactionAsync(async () =>
        {
        var r = await db.TimeOffRequests.Include(x => x.TimeOffPolicy).FirstAsync(x => x.Id == requestId);
        if (r.Status != ApprovalStatus.Pending) throw new InvalidOperationException("Request has already been reviewed.");
        var who = await user.GetNameAsync();
        r.Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        r.ReviewedBy = who;
        r.ReviewedUtc = DateTime.UtcNow;
        r.ReviewNote = note;

        if (approve)
        {
            var bal = await db.TimeOffBalances.FirstOrDefaultAsync(b => b.EmployeeId == r.EmployeeId && b.TimeOffPolicyId == r.TimeOffPolicyId);
            if (bal == null)
            {
                bal = new TimeOffBalance { EmployeeId = r.EmployeeId, TimeOffPolicyId = r.TimeOffPolicyId };
                db.TimeOffBalances.Add(bal);
            }
            if (!r.TimeOffPolicy!.AllowNegativeBalance && bal.Balance < r.Hours)
                throw new InvalidOperationException($"Insufficient balance: {Fmt.Hours(bal.Balance)} hours available.");
            bal.Balance -= r.Hours;
            bal.UsedYtd += r.Hours;
            db.TimeOffTransactions.Add(new TimeOffTransaction
            {
                EmployeeId = r.EmployeeId, TimeOffPolicyId = r.TimeOffPolicyId, Date = r.StartDate, Hours = -r.Hours,
                Kind = "Used", TimeOffRequestId = r.Id, Note = r.Reason, CreatedBy = who
            });

            var days = Enumerable.Range(0, r.EndDate.DayNumber - r.StartDate.DayNumber + 1)
                .Select(i => r.StartDate.AddDays(i))
                .Where(d => d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday).ToList();
            if (days.Count == 0) days.Add(r.StartDate);
            var perDay = Math.Round(r.Hours / days.Count, 2);
            var remainder = r.Hours - perDay * days.Count;
            for (var i = 0; i < days.Count; i++)
            {
                db.TimeEntries.Add(new TimeEntry
                {
                    EmployeeId = r.EmployeeId, WorkDate = days[i], EarningTypeId = r.TimeOffPolicy.EarningTypeId,
                    Hours = perDay + (i == days.Count - 1 ? remainder : 0), Source = "TimeOff", TimeOffRequestId = r.Id,
                    Status = ApprovalStatus.Approved, ApprovedBy = who, ApprovedUtc = DateTime.UtcNow, Notes = r.TimeOffPolicy.Name
                });
            }
        }
        await db.SaveChangesAsync();
        });
    }

    /// <summary>Cancels a request. Approved requests are reversed if none of their time has been paid.</summary>
    public async Task CancelAsync(int requestId)
    {
        await using var db = await dbs.OpenAsync();
        await db.InTransactionAsync(async () =>
        {
        var r = await db.TimeOffRequests.FirstAsync(x => x.Id == requestId);
        if (r.Status is ApprovalStatus.Cancelled or ApprovalStatus.Rejected) return;
        if (r.Status == ApprovalStatus.Approved)
        {
            var entries = await db.TimeEntries.Where(t => t.TimeOffRequestId == r.Id).ToListAsync();
            if (entries.Any(e => e.IsLocked)) throw new InvalidOperationException("Some of this time off has already been paid and cannot be cancelled.");
            db.TimeEntries.RemoveRange(entries);
            var bal = await db.TimeOffBalances.FirstAsync(b => b.EmployeeId == r.EmployeeId && b.TimeOffPolicyId == r.TimeOffPolicyId);
            bal.Balance += r.Hours;
            bal.UsedYtd -= r.Hours;
            db.TimeOffTransactions.Add(new TimeOffTransaction
            {
                EmployeeId = r.EmployeeId, TimeOffPolicyId = r.TimeOffPolicyId, Date = r.StartDate, Hours = r.Hours,
                Kind = "Reversal", TimeOffRequestId = r.Id, Note = "Request cancelled", CreatedBy = await user.GetNameAsync()
            });
        }
        r.Status = ApprovalStatus.Cancelled;
        await db.SaveChangesAsync();
        });
    }

    public async Task AdjustBalanceAsync(int employeeId, int policyId, decimal hours, string? note)
    {
        await using var db = await dbs.OpenAsync();
        var bal = await db.TimeOffBalances.FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.TimeOffPolicyId == policyId);
        if (bal == null)
        {
            bal = new TimeOffBalance { EmployeeId = employeeId, TimeOffPolicyId = policyId };
            db.TimeOffBalances.Add(bal);
        }
        bal.Balance += hours;
        db.TimeOffTransactions.Add(new TimeOffTransaction
        {
            EmployeeId = employeeId, TimeOffPolicyId = policyId, Date = DateOnly.FromDateTime(DateTime.Today), Hours = hours,
            Kind = "Adjustment", Note = note, CreatedBy = await user.GetNameAsync()
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Grants the annual allotment for 'AnnualGrant' policies to every enrolled employee and resets YTD counters.</summary>
    public async Task<int> RunAnnualGrantAsync(int policyId)
    {
        await using var db = await dbs.OpenAsync();
        var policy = await db.TimeOffPolicies.FirstAsync(p => p.Id == policyId);
        var balances = await db.TimeOffBalances.Where(b => b.TimeOffPolicyId == policyId).ToListAsync();
        var who = await user.GetNameAsync();
        foreach (var b in balances)
        {
            var grant = policy.AccrualRate;
            if (policy.MaxBalance.HasValue) grant = Math.Min(grant, Math.Max(0, policy.MaxBalance.Value - b.Balance));
            b.Balance += grant;
            b.AccruedYtd = grant;
            b.UsedYtd = 0;
            db.TimeOffTransactions.Add(new TimeOffTransaction
            {
                EmployeeId = b.EmployeeId, TimeOffPolicyId = policyId, Date = DateOnly.FromDateTime(DateTime.Today),
                Hours = grant, Kind = "Accrual", Note = "Annual grant", CreatedBy = who
            });
        }
        await db.SaveChangesAsync();
        return balances.Count;
    }
}
