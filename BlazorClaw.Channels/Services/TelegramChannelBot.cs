using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Utils;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.CommandLine;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BlazorClaw.Channels.Services
{
    public class TelegramBotEntry : BotEntry
    {
        public string Token { get; set; } = string.Empty;
    }

    public class TelegramChannelBot(ICommandProvider cmds, PathHelper pathHelper, ILogger<TelegramChannelBot> logger) : AbstractConfigChannelBot<TelegramBotEntry>("Telegram")
    {
        internal TelegramBotClient? Client { get; private set; }

        private static string EscapeMarkdownV2(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text
                .Replace("_", "\\_")
                .Replace("*", "\\*")
                .Replace("[", "\\[")
                .Replace("]", "\\]")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("~", "\\~")
                .Replace("`", "\\`")
                .Replace(">", "\\>")
                .Replace("#", "\\#")
                .Replace("+", "\\+")
                .Replace("-", "\\-")
                .Replace("=", "\\=")
                .Replace("|", "\\|")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace(".", "\\.")
                .Replace("!", "\\!");
        }

        public override async Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            if (Client == null) throw new InvalidOperationException("Not configured");
            var content = message.Text;

            foreach (var item in message.Contents)
            {
                if (item is UriContent uri)
                {
                    var uriStr = uri.Uri.ToString();
                    if ("audio/ogg".Equals(uri.MediaType))
                    {
                        await Client.SendVoice(channelId.ChannelId, uriStr, cancellationToken: cancellationToken);
                    }
                    else if (uri.MediaType.StartsWith("image/"))
                    {
                        await Client.SendPhoto(channelId.ChannelId, await GetMediaFileAsync(uriStr), content ?? string.Empty, cancellationToken: cancellationToken);
                        content = null;
                    }
                    else if (uri.MediaType.StartsWith("audio/"))
                    {
                        await Client.SendAudio(channelId.ChannelId, await GetMediaFileAsync(uriStr), content ?? string.Empty, cancellationToken: cancellationToken);
                        content = null;
                    }
                    else if (uri.MediaType.StartsWith("video/"))
                    {
                        await Client.SendVideo(channelId.ChannelId, await GetMediaFileAsync(uriStr), content ?? string.Empty, cancellationToken: cancellationToken);
                        content = null;
                    }
                    else
                    {
                        await Client.SendDocument(channelId.ChannelId, await GetMediaFileAsync(uriStr), content ?? string.Empty, cancellationToken: cancellationToken);
                        content = null;
                    }
                }

            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                var text = EscapeMarkdownV2(content);
                await Client.SendMessage(channelId.ChannelId, text, ParseMode.MarkdownV2, cancellationToken: cancellationToken);
            }
        }

        public override Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            return SendChannelAsync(channelId, message, cancellationToken);
            //return Client.SendMessage(channelId.ChannelId, Convert.ToString(message.Content) ?? string.Empty, cancellationToken: cancellationToken);
        }

        public override Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (Client == null || Config == null) throw new InvalidOperationException("Not configured");

            var receiverOptions = new ReceiverOptions
            {
            };

            Client.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cancellationToken
            );
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        protected override async ValueTask<bool> ConfigureAsync()
        {
            if (string.IsNullOrWhiteSpace(Config?.Token) || !Config.Token.Contains(':')) return false;
            Client = new TelegramBotClient(Config.Token);
            var commands = cmds.GetCommands()
                .Select(o => o.GetCommand())
                .Select(c => new BotCommand { Command = c.Name.ToLower(), Description = c.Description ?? string.Empty }).ToArray();
            await Client.SetMyCommands(commands);
            return true;
        }

        protected async Task<InputFile> GetMediaFileAsync(string url)
        {
            var t = await pathHelper.GetMediaFileAsync(url);
            if (t != null) return t.Item1;
            return url;
        }



        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            logger.LogError(exception, "Telegram Bot '{BotId}' error received: {Name} : {Message}", botClient.BotId, exception.GetType().Name, exception.Message);
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {

                // Wir unterstützen jetzt auch Callbacks und andere Typen
                long chatId = 0;
                string? telegramId = null;

                if (update.Message != null)
                {
                    chatId = update.Message.Chat.Id;
                    telegramId = update.Message.From?.Id.ToString();
                }
                else if (update.CallbackQuery != null)
                {
                    chatId = update.CallbackQuery.Message?.Chat.Id ?? 0;
                    telegramId = update.CallbackQuery.From.Id.ToString();
                }

                if (chatId == 0 || telegramId == null) return;

                logger.LogInformation("Incoming Msg/Query From: {telegramId}, Type: {Type}", telegramId, update.Type);
                await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);

                if (update.Message?.Voice != null)
                {
                    var ret = await DownloadVoiceMessage(botClient, update.Message.Voice.FileId);
                    if (ret != null) await OnMessageReceivedAsync(new ChannelSession(this, telegramId), ret);
                }
                else if (update.Message?.Text != null)
                {
                    await OnMessageReceivedAsync(new ChannelSession(this, telegramId), update.Message.Text);
                }
                else if (update.CallbackQuery != null)
                {
                    await OnMessageReceivedAsync(new ChannelSession(this, telegramId), update.CallbackQuery.Data ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error: {Messsage}", ex.Message);
            }
        }

        private async Task<Tuple<Stream, string>?> DownloadVoiceMessage(ITelegramBotClient botClient, string fileId)
        {
            var strm = new TempStream();
            var info = await botClient.GetInfoAndDownloadFile(fileId, strm);
            logger.LogInformation("DownloadVoiceMessage: {FilePath}", info.FilePath);
            strm.Seek(0, SeekOrigin.Begin);
            return Tuple.Create((Stream)strm, "audio/opus");
        }


    }

}