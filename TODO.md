# TODO: BlazorClaw Architecture

- [ ] Implement `IChannelToSessionDispatcher` interface and implementation to centralize session routing.
- [ ] Refactor `TelegramBotHostedService` to use the new `IChannelToSessionDispatcher` instead of inline session logic.
- [ ] Refactor `MatrixBotHostedService` to use the new `IChannelToSessionDispatcher`.
- [ ] Finalize Database lookup/binding for `ChatSessionParticipant` in the dispatcher.
