using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Sessions;
using Microsoft.EntityFrameworkCore;
using System.CommandLine;
using System.Reflection;

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
    public async Task<object?> ExecuteAsync(ParseResult result, MessageContext context)
    {
        var host = context.Provider.GetRequiredService<IWebHostEnvironment>();
        var sm = context.Provider.GetRequiredService<ISessionManager>();
        var sessstate = await sm.GetOrCreateSessionAsync(context.Session!.Id);
        // Einfache Mock-Daten für die Anzeige des System-Status
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var commit = host.EnvironmentName;
        var tokens = $"{sessstate?.LastUsage?.PromptTokens} in / {sessstate?.LastUsage?.CompletionTokens} out";

        return
            $"🦞 BlazorClaw {version} ({commit})\r\n" +
            $"🧠 Model: {context.Session?.CurrentModel}\r\n" +
            $"🧮 Tokens: {tokens}\r\n" +
            $"🧮 Cost: {sessstate?.Costs}\r\n" +
            $"🧵 Session: {context.Session?.Id}\r\n" +
            $"🧵 Channel: {context.Channel?.ChannelProvider}:{context.Channel?.ChannelId}";
    }
}

public class RegisterCommand : ISystemCommand, ISystemCommandExecutor
{
    public Command GetCommand()
    {
        var cmd = new Command("register", "Registriert einen User Channel")
        {
            new Argument<string>("token") { Description = "Der Registrierungstoken" }
        };
        return cmd;
    }

    public async Task<object?> ExecuteAsync(ParseResult result, MessageContext context)
    {
        if (context.Channel == null) throw new InvalidOperationException("Dieser Befehl muss in einem Channel ausgeführt werden.");
        var token = result.GetRequiredValue((Argument<string>)result.CommandResult.Command.Arguments[0]);
        var db = context.Provider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.ChannelRegisterToken == token) ?? throw new UnauthorizedAccessException("Ungültiger Registrierungstoken");
        if ((user.ChannelRegisterTokenExpiredAt ?? DateTime.MinValue) < DateTime.UtcNow) throw new UnauthorizedAccessException("Registrierungstoken ist abgelaufen");
        user.ChannelRegisterToken = null;
        user.ChannelRegisterTokenExpiredAt = null;

        db.UserLogins.Add(new Microsoft.AspNetCore.Identity.IdentityUserLogin<string>
        {
            UserId = user.Id,
            LoginProvider = context.Channel.ChannelProvider,
            ProviderKey = context.Channel.ChannelId,
            ProviderDisplayName = context.Channel.ChannelProvider
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

    public async Task<object?> ExecuteAsync(ParseResult result, MessageContext context)
    {
        var db = context.Provider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == context.UserId) ?? throw new UnauthorizedAccessException("User nicht gefunden");

        if (result.CommandResult.Command.Name == "register")
        {
            user.ChannelRegisterToken = Guid.NewGuid().ToString("N");
            user.ChannelRegisterTokenExpiredAt = DateTime.UtcNow.AddMinutes(10);
            await db.SaveChangesAsync();
            return $"Registrierungstoken: {user.ChannelRegisterToken} (gültig für 10 Min)\r\nUse `/register {user.ChannelRegisterToken}`";
        }
        else if (result.CommandResult.Command.Name == "list")
        {
            var logins = await db.UserLogins.Where(l => l.UserId == user.Id).ToListAsync();
            if (logins.Count == 0) return "Keine Kanäle registriert.";
            return string.Join("\n", logins.Select(l => $"{l.LoginProvider}: {l.ProviderKey}"));
        }
        return "Befehl nicht gefunden.";
    }
}
