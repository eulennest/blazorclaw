using Matrix.Sdk;
// using Matrix.Sdk.Core.EventTypes.Spec; // Entfernen, wenn SDK dies nicht bereitstellt
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using System.Collections.Concurrent;
using Matrix.Sdk.Core.Domain.RoomEvent;

namespace BlazorClaw.Channels.Services
{
    // ... rest of the file
    // In HandleUpdate change the event handling to be more generic if Spec types are missing
    // or try Matrix.Sdk.Core.EventTypes.Spec; again? 
    // Wait, let me check the SDK for actual text message events

    public class MatrixBotHostedService(IConfiguration configuration, ILogger<MatrixBotHostedService> logger, IServiceProvider serviceProvider) : IHostedService
    {
        private readonly List<IMatrixClient> _clients = [];
        private readonly ConcurrentDictionary<string, Guid> _sessIds = [];

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var matrixConfigs = configuration.GetSection("Channels:Matrix").GetChildren();
            var factory = new MatrixClientFactory();

            foreach (var config in matrixConfigs)
            {
                var homeserver = new Uri(config["Homeserver"] ?? "https://matrix.org");
                var userId = config["UserId"] ?? "";
                var password = config["Password"] ?? "";

                logger.LogInformation("Matrix Bot '{id}' initializing ...", config.Key);

                try
                {
                    IMatrixClient client = factory.Create();
                    await client.LoginAsync(homeserver, userId, password, "BlazorClawBot");

                    client.OnMatrixRoomEventsReceived += HandleUpdate;

                    client.Start();
                    _clients.Add(client);
                    logger.LogInformation("Matrix Bot '{id}' started successfully.", config.Key);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start Matrix Bot '{id}'", config.Key);
                }
            }
        }

        private async void HandleUpdate(object? sender, MatrixRoomEventsEventArgs eventArgs)
        {
            if (sender is not IMatrixClient client) return;
            foreach (var roomEvent in eventArgs.MatrixRoomEvents)
            {
                if (client.UserId != roomEvent.SenderUserId)
                {
                    if (roomEvent is TextMessageEvent textMessageEvent)
                    {
                        logger.LogInformation("Matrix received message in {RoomId}", roomEvent.RoomId);
                        await ProcessMatrixMessage(client, roomEvent.RoomId, roomEvent.SenderUserId, textMessageEvent.Message);
                    }
                }
            }
        }

        private async Task ProcessMatrixMessage(IMatrixClient client, string roomId, string senderUserId, string message)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var sm = scope.ServiceProvider.GetRequiredService<ISessionManager>();

                var user = await userManager.FindByLoginAsync("Matrix", senderUserId);

                Guid? uid = user != null ? Guid.Parse(user.Id) : null;
                if (uid == null)
                {
                    if (!_sessIds.TryGetValue(senderUserId, out var existingUid))
                    {
                        uid = Guid.NewGuid();
                        _sessIds[senderUserId] = uid.Value;
                    }
                    else
                    {
                        uid = existingUid;
                    }
                }
                var sess = await sm.GetOrCreateSessionAsync(uid.Value, "openrouter/google/gemini-3.1-flash-lite-preview");
                sess.MessageHistory.Add(new() { Role = "user", Content = message });

                await foreach (var msg in sm.DispatchToLLMAsync(sess))
                {
                    if (msg.Role != "assistant") continue;
                    var content = Convert.ToString(msg.Content);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        await client.SendMessageAsync(roomId, content);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing matrix message");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var client in _clients) client.Stop();
            return Task.CompletedTask;
        }
    }
}
