using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Paycrest.Data;

namespace Paycrest.Services;

public sealed class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

/// <summary>Sends Identity emails (password reset, confirmation) through SMTP, e.g. your SmarterASP mail account.</summary>
public sealed class SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your WageHarbor account", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your WageHarbor password", $"Reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Your WageHarbor password reset code", $"Your password reset code is: <b>{WebUtility.HtmlEncode(resetCode)}</b>");

    public async Task SendAsync(string to, string subject, string html)
    {
        if (!options.IsConfigured)
        {
            logger.LogWarning("SMTP not configured; email to {To} with subject '{Subject}' was not sent.", to, subject);
            return;
        }
        using var client = new SmtpClient(options.Host, options.Port) { EnableSsl = options.EnableSsl };
        if (!string.IsNullOrEmpty(options.UserName))
            client.Credentials = new NetworkCredential(options.UserName, options.Password);
        using var msg = new MailMessage(options.From!, to, subject, html) { IsBodyHtml = true };
        await client.SendMailAsync(msg);
    }
}
