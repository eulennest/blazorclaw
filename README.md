# BlazorClaw

**OpenClaw-Ersatz in .NET 8 / Blazor**

Agent-Host mit Session-Management, Tool-System, LLM-Integration und Multi-Channel-Support.

---

## Prio 1 – MVP

| Feature | Beschreibung |
|---------|--------------|
| **Chat UI (Blazor Web)** | Web-Interface zum Chatten |
| Session RAM + JSON | Speichern/Laden |
| LLM Integration | OpenAI-kompatibel |
| Tools: `read`, `write`, `message` | Basis-Tools |

---

## Prio 2 – Nach MVP

| Feature | Beschreibung |
|---------|--------------|
| Telegram Channel | Erster Channel |
| `exec` Tool | Shell Commands |
| Session Compression | Token-Sparen |
| Basic Security | Tool-Policies |
| OpenAI API Compatibility | Externe Apps |

---

## Architektur

```
BlazorClaw/
├── BlazorClaw.Server/     # MVP: Alles in einem Projekt
├── BlazorClaw.Core/       # Core-Logik (später)
├── BlazorClaw.Channels/   # Telegram, Matrix (später)
└── BlazorClaw.UI/         # Admin-UI (später)
```

---

## Tech Stack

- .NET 8 / Blazor Server
- ASP.NET Identity (Auth)
- EF Core (Datenbank)
- SignalR (Echtzeit)
- OpenAI-kompatible LLM-Integration

---

## Quick Start

```bash
# Clone
git clone https://github.com/eulennest/blazorclaw.git
cd BlazorClaw

# Restore & Build
dotnet restore
dotnet build

# Run
dotnet run --project BlazorClaw.Server
```

---

## Datenspeicherung

| Daten | Speicher |
|-------|----------|
| User, Auth | ASP.NET Identity (EF Core) |
| Channel-Config | EF Core |
| Session | JSON Files |

---

## Lizenz

MIT

---

*Erstellt: 2026-03-05*