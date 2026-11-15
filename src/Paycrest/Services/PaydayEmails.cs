using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Paycrest.Data;
using Paycrest.Domain;

namespace Paycrest.Services;

/// <summary>Payrolls just marked paid, waiting for their "pay stub ready" emails.</summary>
public sealed class PaydayEmailQueue
{
    private readonly Channel<(int CompanyId, int RunId)> _channel = Channel.CreateUnbounded<(int, int)>();
    public void Add(int companyId, int runId) => _channel.Writer.TryWrite((companyId, runId));
    public ChannelReader<(int CompanyId, int RunId)> Reader => _channel.Reader;
}

/// <summary>
/// Emails each employee with a login that their pay stub is ready, in the background so marking a payroll paid
/// stays instant. The email has no amounts; the link asks them to sign in.
/// </summary>
public sealed class PaydayEmailSender(
    PaydayEmailQueue queue,
    IDbContextFactory<ApplicationDbContext> factory,
    IEmailSender<ApplicationUser> emailSender,
    IConfiguration config,
    ILogger<PaydayEmailSender> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (companyId, runId) in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try { await SendAsync(companyId, runId, stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Payday emails for payroll {RunId} (company {CompanyId}) failed.", runId, companyId);
            }
        }
    }

    private async Task SendAsync(int companyId, int runId, CancellationToken ct)
    {
        if (emailSender is not SmtpEmailSender smtp) return;
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CurrentCompanyId = companyId;
        var company = await db.Companies.AsNoTracking().FirstAsync(ct);
        if (!company.EmailPayStubs) return;

        var people = await (from p in db.Paychecks
                            join e in db.Employees on p.EmployeeId equals e.Id
                            join u in db.Users on e.UserId equals u.Id
                            where p.PayrollRunId == runId && u.Email != null
                            select new { p.Id, p.PeriodStart, p.PeriodEnd, p.PayDate, Name = e.PreferredName ?? e.FirstName, u.Email }).ToListAsync(ct);

        var baseUrl = (config["Site:BaseUrl"] is { Length: > 0 } b ? b : "https://www.wageharbor.com").TrimEnd('/');
        var h = (string s) => WebUtility.HtmlEncode(s);
        var sent = 0;
        foreach (var p in people)
        {
            var html = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;font-size:15px;color:#111827;max-width:560px">
                  <h2 style="color:#0b2545;margin:0 0 12px">Your pay stub is ready</h2>
                  <p>Hi {h(p.Name)}, your pay from {h(company.Name)} for <strong>{p.PeriodStart:MMM d} – {p.PeriodEnd:MMM d, yyyy}</strong> (pay date <strong>{p.PayDate:MMM d}</strong>) is ready to view.</p>
                  <p style="margin:24px 0"><a href="{baseUrl}/paystub/{p.Id}" style="background:#0e7c86;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:600">View my pay stub</a></p>
                  <p style="color:#6b7280;font-size:13px">For your security, amounts aren't included in this email. You'll be asked to sign in. Questions about your pay? Contact {h(company.Name)}.</p>
                </div>
                """;
            if (await smtp.TrySendAsync(p.Email!, $"Your {company.Name} pay stub is ready", html) == null) sent++;
            await Task.Delay(250, ct); // be gentle with the mail server
        }
        logger.LogInformation("Payday emails for payroll {RunId}: {Sent} of {Total} sent.", runId, sent, people.Count);
    }
}
