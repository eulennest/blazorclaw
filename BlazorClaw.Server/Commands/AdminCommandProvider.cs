using System.CommandLine;
using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Server.Data; // Required for ApplicationDbContext
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; // Required for context.Provider

namespace BlazorClaw.Server.Commands;

public class AdminCommandProvider : ExecutorCommandProvider
{
    public override IEnumerable<ISystemCommand> GetCommands()
    {
        yield return new StatusCommand();
        yield return new RegisterCommand();
        yield return new ChannelCommand();
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
        cmd.Add(new Argument<string>("token") { Description = "Der Registrierungstoken" });
        return cmd;
    }

    public async Task<object?> ExecuteAsync(ParseResult result, CommandContext context)
    {
        var token = result.GetValueForArgument((Argument<string>)result.CommandResult.Command.Arguments[0]);
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

public class ChannelCommand : ISystemCommand, ISystemCommandExecutor
{
    public Command GetCommand()
    {
        var cmd = new Command("channels", "Kanal-Verwaltung");
        var register = new Command("register", "Erzeugt einen neuen Registrierungstoken");
        var list = new Command("list", "Listet alle registrierten Kanäle");
        
        cmd.Add(register);
        cmd.Add(list);
        return cmd;
    }

    public async Task<object?> ExecuteAsync(ParseResult result, CommandContext context)
    {
        var db = context.Provider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == context.UserId) ?? throw new UnauthorizedAccessException("User nicht gefunden");
        
        if (result.CommandResult.Command.Name == "register")
        {
            user.ChannelRegisterToken = Guid.NewGuid().ToString("N");
            user.ChannelRegisterTokenExpiredAt = DateTime.UtcNow.AddMinutes(10);
            await db.SaveChangesAsync();
            return $"Registrierungstoken: {user.ChannelRegisterToken} (gültig für 10 Min)";
        }
        else if (result.CommandResult.Command.Name == "list")
        {
            var logins = await db.UserLogins.Where(l => l.UserId == user.Id).ToListAsync();
            if (!logins.Any()) return "Keine Kanäle registriert.";
            return string.Join("\n", logins.Select(l => $"{l.LoginProvider}: {l.ProviderKey}"));
        }
        return "Befehl nicht gefunden.";
    }
}
