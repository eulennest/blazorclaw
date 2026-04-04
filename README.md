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

```
HTTP/HTTPS:
  - Direkter Zugriff ohne Registry-Eintrag ✅
  - Beispiel: mcp_call(serverUri="https://api.example.com")

Unix/TCP/UDP/Exec:
  - BENÖTIGEN vorherigen Registry-Eintrag via mcp_set ✅
  - Nur Admins dürfen mcp_set ausführen (Tool-Policy)
  - Beispiel: 
    1. mcp_set(name="local-db", serverUri="unix:///tmp/db.sock") [Admin only]
    2. mcp_call(serverUri="mcp://local-db", method="query") [Jeder]
```

**Warum?** HTTP/HTTPS sind sichere externe APIs. Unix/TCP/UDP/Exec haben direkten Zugriff auf:
- Lokale Prozesse (RCE-Risiko)
- Internes Netzwerk (Lateral Movement)
- Betriebssystem-Ressourcen

Durch Registry-Pflicht wird verhindert, dass LLM willkürlich:
- Lokale Binaries ausführt (`exec://`)
- Interne Services anspricht (`tcp://192.168.x.x`)
- Datenbanken zerstört (`unix:///var/db.sock`)

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
