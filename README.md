# BlazorClaw

**Das .NET Agenten-Ökosystem für modulare KI-Kommunikation.**

BlazorClaw ist ein natives .NET-Ökosystem zur Orchestrierung von KI-Agenten über verschiedene Kanäle (Telegram, Matrix, Web). Es verbindet die Flexibilität eines Agenten-Frameworks mit der Stabilität, Typ-Sicherheit und Performance von .NET 8+.

---

## Warum BlazorClaw?

- **Sicherheit by Design**: Zentrale `PolicyProvider` sichern jedes Tool und jede Nachricht.
- **Modulare Architektur**: Dank `MessageDispatcher` und `Plugin-System` lassen sich neue Tools und Kanäle als .NET Assemblies dynamisch laden.
- **Token-Optimierung**: Integriertes *Self-Compacting* für lange Sessions.
- **Deployment-Ready**: Gebaut auf ASP.NET Core – einfach containerisieren und als .NET Service betreiben.

---

## Architektur

```
BlazorClaw/
├── BlazorClaw.Core/       # Interfaces, DTOs & Kern-Logik (Provider, Sessions)
├── BlazorClaw.Channels/   # Kanal-Adapter (Telegram, Matrix, Web)
├── BlazorClaw.Server/     # API-Hosting, Tools, Commands, Dispatcher
└── BlazorClaw.UI/         # Web-Interface (in Entwicklung)
```

---

## Schneller Einstieg

1. **Voraussetzungen**: .NET 8 SDK.
2. **Setup**:
   ```bash
   git clone https://github.com/eulennest/blazorclaw.git
   cd BlazorClaw
   ```
3. **Starten**:
   ```bash
   dotnet run --project BlazorClaw.Server/BlazorClaw.Server.csproj
   ```
   *Hinweis: Datenbank-Migrationen werden beim Start automatisch angewendet.*

---

## Kanalanbindung

BlazorClaw unterstützt eine einheitliche Schnittstelle (`IChannelBot`). Um einen neuen Kanal (z.B. Slack) anzubinden, implementieren Sie einfach `IChannelBot` und registrieren Sie den Service in `Program.cs`. 

---

## Lizenz

MIT

---

*Powered by Eulennest. 🦞*
