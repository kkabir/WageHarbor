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
    /// <summary>For local testing: save emails as .eml files in this folder instead of sending them.</summary>
    public string? PickupDirectory { get; set; }
    public bool IsConfigured => (!string.IsNullOrWhiteSpace(Host) || !string.IsNullOrWhiteSpace(PickupDirectory)) && !string.IsNullOrWhiteSpace(From);
}

/// <summary>Sends Identity emails (password reset, confirmation) through SMTP, e.g. your SmarterASP mail account.</summary>
public sealed class SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger, IHostEnvironment environment) : IEmailSender<ApplicationUser>
{
    // Identity hands these links over HTML-encoded; EmailLayout encodes them itself, so decode first.
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your email for WageHarbor", EmailLayout.Render(
            "Confirm your email address",
            ["Please confirm that this is your email address. It makes sure password resets and pay stub notices reach you, and nobody else."],
            "Confirm my email", WebUtility.HtmlDecode(confirmationLink),
            "The link works for 3 days. If you didn't sign up for WageHarbor or change your email, you can ignore this email."));

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your WageHarbor password", EmailLayout.Render(
            "Reset your password",
            ["Someone (hopefully you) asked to reset the password for your WageHarbor account. Choose a new password with the button below."],
            "Choose a new password", WebUtility.HtmlDecode(resetLink),
            "The link works for 3 days and only once. If you didn't ask for this, ignore this email: your password stays the same."));

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Your WageHarbor password reset code", EmailLayout.Render(
            "Your password reset code",
            [$"Your code is: {resetCode}", "Enter it on the reset page to choose a new password."],
            note: "If you didn't ask for this, ignore this email: your password stays the same."));

    /// <summary>Sends an HTML email. <paramref name="replyTo"/> makes "Reply" in the mail app answer that address.</summary>
    public async Task SendAsync(string to, string subject, string html, string? replyTo = null) =>
        await TrySendAsync(to, subject, html, replyTo);

    /// <summary>Like <see cref="SendAsync"/>, but returns null when the mail server accepted the message, or what went wrong.</summary>
    public async Task<string?> TrySendAsync(string to, string subject, string html, string? replyTo = null)
    {
        if (!options.IsConfigured)
        {
            var problem = "Email isn't set up: Smtp:Host and Smtp:From must both be set in appsettings.Production.json.";
            // In production this means emails are silently lost, so make it show up in the error log.
            if (environment.IsDevelopment()) logger.LogWarning("{Problem} Email to {To} ('{Subject}') was not sent.", problem, to, subject);
            else logger.LogError("{Problem} Email to {To} ('{Subject}') was not sent.", problem, to, subject);
            return problem;
        }
        try
        {
            using var client = !string.IsNullOrWhiteSpace(options.PickupDirectory)
                ? new SmtpClient { DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory, PickupDirectoryLocation = Directory.CreateDirectory(options.PickupDirectory).FullName }
                : new SmtpClient(options.Host, options.Port) { EnableSsl = options.EnableSsl };
            if (!string.IsNullOrEmpty(options.UserName))
                client.Credentials = new NetworkCredential(options.UserName, options.Password);
            using var msg = new MailMessage(options.From!, to) { Subject = subject };
            // Send both a text and an HTML copy: mail apps pick the one they can show, and HTML-only mail looks spammier.
            msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(EmailLayout.ToPlainText(html), System.Text.Encoding.UTF8, "text/plain"));
            msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, System.Text.Encoding.UTF8, "text/html"));
            // SmtpClient doesn't add a Message-ID; Gmail and others expect one and score mail without it as spammier.
            msg.Headers.Add("Message-ID", $"<{Guid.NewGuid():N}@{msg.From!.Host}>");
            if (!string.IsNullOrWhiteSpace(replyTo)) msg.ReplyToList.Add(replyTo);
            await client.SendMailAsync(msg);
            logger.LogInformation("Email '{Subject}' sent to {To}.", subject, to);
            return null;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            // Don't break the page (or reveal whether an account exists); the log says what to fix.
            logger.LogError(ex, "Sending email '{Subject}' to {To} via {Host}:{Port} failed.", subject, to, options.Host, options.Port);
            return $"{ex.GetType().Name}: {ex.Message}" + (ex.InnerException != null ? $" ({ex.InnerException.Message})" : "");
        }
    }

    /// <summary>The settings in use, without the password, for the platform owner's email test.</summary>
    public string SettingsSummary =>
        $"Server {options.Host ?? "(not set)"}:{options.Port}, encryption {(options.EnableSsl ? "on" : "off")}, " +
        $"sign-in as {options.UserName ?? "(none)"}, password {(string.IsNullOrEmpty(options.Password) ? "NOT set" : "set")}, from {options.From ?? "(not set)"}";
}
