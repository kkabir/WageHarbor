using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Paycrest.Data;

namespace Paycrest.Services;

/// <summary>Account emails WageHarbor sends itself (beyond Identity's own): confirm a new sign-up, and "set your password" for a new login.</summary>
public sealed class AccountEmails(UserManager<ApplicationUser> users, IEmailSender<ApplicationUser> sender, NavigationManager nav)
{
    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    /// <summary>Welcome email for a new company admin, with a link to confirm their email. Returns null when sent, or what went wrong.</summary>
    public async Task<string?> SendWelcomeAsync(ApplicationUser user, string companyName)
    {
        var link = nav.GetUriWithQueryParameters(nav.ToAbsoluteUri("Account/ConfirmEmail").AbsoluteUri,
            new Dictionary<string, object?> { ["userId"] = user.Id, ["code"] = Encode(await users.GenerateEmailConfirmationTokenAsync(user)) });
        return await SendAsync(user.Email!, "Welcome to WageHarbor: please confirm your email", new EmailMessage
        {
            Eyebrow = "Welcome aboard",
            Heading = $"{companyName} is on WageHarbor",
            Lead = "Your company account is ready, and you're its admin.",
            Preheader = "Confirm your email, then add your team and run your first payroll.",
            Paragraphs = ["Here's how to get going:"],
            ButtonText = "Confirm my email",
            ButtonUrl = link,
            Bullets =
            [
                "Confirm your email with the button below, so password resets and notices reach you",
                "Add your employees, or import them from a spreadsheet",
                "Check your pay schedule, earnings and deductions",
                "Run your first payroll: calculate, review, approve",
                "Invite your team so they can see their own pay stubs",
            ],
            Note = "The company guide in the app (Help & guides) walks you through each step with pictures. The confirm link works for 3 days; if you didn't create a WageHarbor account, you can ignore this email.",
            Reason = $"Sent to {user.Email} because this address was used to register {companyName} on WageHarbor.",
        });
    }

    /// <summary>
    /// "Set your password" email for a login someone else created (a new employee) or reset (by an admin).
    /// The person chooses their own password; nobody else ever sees it. Returns null when sent, or what went wrong.
    /// </summary>
    public async Task<string?> SendSetPasswordAsync(ApplicationUser user, string companyName, bool isNew)
    {
        var link = nav.GetUriWithQueryParameters(nav.ToAbsoluteUri("Account/ResetPassword").AbsoluteUri,
            new Dictionary<string, object?> { ["code"] = Encode(await users.GeneratePasswordResetTokenAsync(user)), ["email"] = user.Email });
        var reason = $"Sent to {user.Email} because you have a WageHarbor login at {companyName}.";
        var message = isNew
            ? new EmailMessage
            {
                Eyebrow = companyName,
                Heading = "Your WageHarbor login is ready",
                Lead = $"{companyName} uses WageHarbor for payroll.",
                Preheader = "Choose your password to see your pay stubs, W-4 and time off.",
                Paragraphs = ["With your own login you can:"],
                Bullets =
                [
                    "See and print every pay stub",
                    "Update your W-4, address and direct deposit",
                    "Clock in and out from any device",
                    "Ask for time off and see your balances",
                ],
                Details = [("Your username", user.Email!)],
                ButtonText = "Choose my password",
                ButtonUrl = link,
                Note = "The link works for 3 days and only once. If it has expired, use \"Forgot your password?\" on the sign-in page. If you weren't expecting this, you can ignore it.",
                Reason = reason,
            }
            : new EmailMessage
            {
                Eyebrow = companyName,
                Heading = "Choose a new password",
                Lead = "Your admin sent you a password reset link.",
                Paragraphs = [$"An admin at {companyName} sent you this link to choose a new password for your WageHarbor login. Until you do, your current password keeps working."],
                Details = [("Your username", user.Email!)],
                ButtonText = "Choose a new password",
                ButtonUrl = link,
                Note = "The link works for 3 days and only once. If you didn't expect this, tell your company's admin.",
                Reason = reason,
            };
        return await SendAsync(user.Email!, isNew ? $"{companyName}: your WageHarbor login is ready" : "Choose a new WageHarbor password", message);
    }

    private async Task<string?> SendAsync(string to, string subject, EmailMessage message) =>
        sender is SmtpEmailSender smtp ? await smtp.TrySendAsync(to, subject, message) : "Email sending isn't available.";
}
