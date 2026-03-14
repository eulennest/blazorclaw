using BlazorClaw.Core.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;

namespace BlazorClaw.UI.Components.Account
{
    // Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
    public sealed class IdentityEmailSender(ILogger<IdentityEmailSender> logger) : IEmailSender<ApplicationUser>
    {
        private readonly IEmailSender emailSender = new ConsoleEmailSender(logger);

        public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
            emailSender.SendEmailAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

        public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
            emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

        public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
            emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");
    }

    public class ConsoleEmailSender(ILogger logger) : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            logger.LogInformation("To: {email}, Subject: {subject}, Message: {htmlMessage}", email, subject, htmlMessage);
            return Task.CompletedTask;
        }
    }
}
