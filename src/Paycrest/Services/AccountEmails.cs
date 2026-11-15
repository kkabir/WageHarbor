using System.Net;
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
        var html = EmailLayout.Render(
            $"Welcome to WageHarbor, {companyName}!",
            ["Your company account is ready, and you're its admin. Please confirm your email address so password resets and important notices reach you.",
             "Next steps: add your employees, check your pay settings, then run your first payroll. The company guide in the app walks you through each step."],
            "Confirm my email", link,
            "The link works for 3 days. If you didn't create a WageHarbor account, you can ignore this email.");
        return await SendAsync(user.Email!, "Welcome to WageHarbor: please confirm your email", html);
    }

    /// <summary>
    /// "Set your password" email for a login someone else created (a new employee) or reset (by an admin).
    /// The person chooses their own password; nobody else ever sees it. Returns null when sent, or what went wrong.
    /// </summary>
    public async Task<string?> SendSetPasswordAsync(ApplicationUser user, string companyName, bool isNew)
    {
        var link = nav.GetUriWithQueryParameters(nav.ToAbsoluteUri("Account/ResetPassword").AbsoluteUri,
            new Dictionary<string, object?> { ["code"] = Encode(await users.GeneratePasswordResetTokenAsync(user)), ["email"] = user.Email });
        var html = isNew
            ? EmailLayout.Render(
                $"{companyName} set up your WageHarbor login",
                [$"{companyName} uses WageHarbor for payroll. With your own login you can see and print your pay stubs, update your W-4 and address, clock in and out, and ask for time off.",
                 $"Your username is {user.Email}. Choose your password with the button below to get started."],
                "Choose my password", link,
                "The link works for 3 days and only once. If it has expired, use \"Forgot your password?\" on the sign-in page. If you weren't expecting this, you can ignore it.")
            : EmailLayout.Render(
                "Choose a new password",
                [$"An admin at {companyName} sent you this link to choose a new password for your WageHarbor login ({user.Email}). Until you do, your current password keeps working."],
                "Choose a new password", link,
                "The link works for 3 days and only once. If you didn't expect this, tell your company's admin.");
        return await SendAsync(user.Email!, isNew ? $"{companyName}: set up your WageHarbor login" : "Choose a new WageHarbor password", html);
    }

    private async Task<string?> SendAsync(string to, string subject, string html) =>
        sender is SmtpEmailSender smtp ? await smtp.TrySendAsync(to, subject, html) : "Email sending isn't available.";
}
