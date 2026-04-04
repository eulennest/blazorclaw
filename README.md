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

## Sicherheit & Zugriffskontrolle

### MCP (Model Context Protocol) - Transporte & Zugriff

BlazorClaw implementiert **transportbasierte Zugriffskontrolle** für MCP-Tools:

| Transport | Schema | Zugriff | Registry-Pflicht | Anwendungsfall |
|-----------|--------|--------|------------------|---|
| **HTTP/HTTPS** | `http://`, `https://` | 🔓 Frei (öffentliche APIs) | ❌ Nein | Externe Web-APIs, LLM-Services |
| **Unix Socket** | `unix:///path` | 🔒 Nur Admin (lokales IPC) | ✅ Ja (mcp_set) | Lokale IPC-Services |
| **TCP/UDP** | `tcp://`, `udp://` | 🔒 Nur Admin (Netzwerk) | ✅ Ja (mcp_set) | Interne Netzwerk-Services |
| **Exec** | `exec://` | 🔒 Nur Admin (RCE-Risiko) | ✅ Ja (mcp_set) | Lokale Prozesse (begrenzt) |

### Access Control Regeln

**mcp_set** (Tool zur Service-Registrierung):
- 🔒 **Admin-only** — Nur Admins dürfen neue MCP-Services registrieren
- Speichert Dienste mit Auth-Daten in `/~secure/mcp.json`
- Beispiel: `mcp_set(name="github", serverUri="https://mcp.github.com", authType="bearer", tokenName="GH_TOKEN")`

**mcp_call** (Tool zum Service-Aufruf):
- 🔓 **Für alle User** — Jeder kann registrierte Services nutzen
- Whitelist-Check für lokale Transporte:
  ```
  HTTP/HTTPS:
    - Direkter Zugriff ohne Registry ✅ (öffentliche APIs)
    - mcp_call(serverUri="https://api.example.com", method="search", ...)
  
  Unix/TCP/UDP/Exec:
    - MÜSSEN vorher via mcp_set registriert sein ✅ (Whitelist)
    - mcp_call(serverUri="mcp://github", method="list_issues", ...)
  ```

**Warum Whitelist für Unix/TCP/UDP/Exec?**
- Direkter Zugriff auf lokale Prozesse (RCE-Risiko)
- Interne Netzwerk-Services (Lateral Movement)
- OS-Ressourcen (Datenbanken, File-System)

Admin registriert Service *einmalig*, dann nutzen **alle** User — oft mit Auth:
```
Admin: mcp_set(name="internal-db", serverUri="tcp://db.local:5432", authType="bearer", tokenName="DB_TOKEN")
User:  mcp_call(serverUri="mcp://internal-db", method="query", params={...})
       → Token aus Vault automatisch aufgelöst
```

---

## Kanalanbindung

BlazorClaw unterstützt eine einheitliche Schnittstelle (`IChannelBot`). Um einen neuen Kanal (z.B. Slack) anzubinden, implementieren Sie einfach `IChannelBot` und registrieren Sie den Service in `Program.cs`. 

---

## Development & Tool Creation

**Neue Tools bauen?** Siehe **[CODING.md](CODING.md)** für Entwicklungsstandards:

- **Exception-Based Error Handling**: Tools werfen Exceptions (nicht Error-Strings)
- **Nullable Parameters**: Optional-Parameter müssen nullable sein (`bool?`, `int?`)
- **Variable Resolution**: Automatische `@VAR_NAME`-Substitution via Vault/Env
- **HttpClient Pattern**: Immer `IHttpClientFactory` verwenden (nicht `new HttpClient()`)
- **Socket Tools**: Unified API für Unix-Sockets, TCP, UDP
- **Audit Logging**: Errors werden vom ToolDispatcher in ProblemDetails JSON konvertiert

**Reference Implementation**: `/BlazorClaw.Server/Tools/Mcp/` (MCP-System mit allen Patterns)

---

## Lizenz

MIT

---

*Powered by Eulennest. 🦞*
