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
        var sent = 0;
        foreach (var p in people)
        {
            var message = new EmailMessage
            {
                Eyebrow = $"Payday · {company.Name}",
                Tone = EmailTone.Success,
                Heading = "Your pay stub is ready",
                Lead = $"Pay date {p.PayDate:dddd, MMMM d}.",
                Greeting = $"Hi {p.Name},",
                Paragraphs = [$"Your pay from {company.Name} is ready to view, print or save as PDF."],
                Details =
                [
                    ("Pay period", $"{p.PeriodStart:MMM d} – {p.PeriodEnd:MMM d, yyyy}"),
                    ("Pay date", $"{p.PayDate:MMM d, yyyy}"),
                    ("From", company.Name),
                ],
                ButtonText = "View my pay stub",
                ButtonUrl = $"{baseUrl}/paystub/{p.Id}",
                Note = $"For your security, amounts aren't included in this email, and you'll be asked to sign in. Questions about your pay? Contact {company.Name}.",
                Reason = $"Sent because you have a WageHarbor login at {company.Name}, which emails you on payday.",
            };
            if (await smtp.TrySendAsync(p.Email!, $"Your {company.Name} pay stub is ready", message) == null) sent++;
            await Task.Delay(250, ct); // be gentle with the mail server
        }
        logger.LogInformation("Payday emails for payroll {RunId}: {Sent} of {Total} sent.", runId, sent, people.Count);
    }
}
