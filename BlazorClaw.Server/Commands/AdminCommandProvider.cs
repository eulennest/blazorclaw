using System.CommandLine;
using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorClaw.Server.Commands;

public class AdminCommandProvider : ExecutorCommandProvider
{
    public override IEnumerable<ISystemCommand> GetCommands()
    {
        yield return new StatusCommand();
        yield return new RegisterCommand();
    }
}

public class StatusCommand : ISystemCommand, ISystemCommandExecutor
{
    public Command GetCommand() => new("status", "Status von BlazorClaw anzeigen");
    public Task<object?> ExecuteAsync(ParseResult result, CommandContext context)
    {
        return Task.FromResult<object?>($"Status: BlazorClaw is running. User: {context.UserId}, Channel: {context.ChannelId}");
    }
}

public class RegisterCommand : ISystemCommand, ISystemCommandExecutor
{
    public Command GetCommand()
    {
        var cmd = new Command("register", "Registriert einen User Channel");
        cmd.Arguments.Add(new Argument<string>("token") { HelpName = "Der Registrierungstoken" });
        return cmd;
    }

    public async Task<object?> ExecuteAsync(ParseResult result, CommandContext context)
    {
        var token = result.GetRequiredValue((Argument<string>)result.CommandResult.Command.Arguments[0]);
        var db = context.Provider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == context.UserId) ?? throw new UnauthorizedAccessException("User nicht gefunden");
        if (!token.Equals(user.ChannelRegisterToken)) throw new UnauthorizedAccessException("Ungültiger Registrierungstoken");
        if ((user.ChannelRegisterTokenExpiredAt ?? DateTime.MinValue) < DateTime.UtcNow) throw new UnauthorizedAccessException("Registrierungstoken ist abgelaufen");
        user.ChannelRegisterToken = null;
        user.ChannelRegisterTokenExpiredAt = null;

        db.UserLogins.Add(new Microsoft.AspNetCore.Identity.IdentityUserLogin<string>
        {
            UserId = user.Id,
            LoginProvider = context.ChannelProvider,
            ProviderKey = context.ChannelId,
            ProviderDisplayName = context.ChannelProvider
        });
        await db.SaveChangesAsync();
        return "User Channel erfolgreich registriert";
    }
}
