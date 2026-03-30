# CarChat Platform

A secure, real-time communication bridge that orchestrates bidirectional WebSocket messaging between an **agent service** and an **Android app**. Built with .NET 10 (LTS).

---

## Overview

CarChat Platform acts as a relay/broker — when both sides are connected, messages flow freely between the agent and the app with zero storage. The platform handles authentication, API key management, and session pairing.

```
┌──────────────┐     X-Api-Key      ┌─────────────────┐     JWT (OAuth)    ┌──────────────┐
│ Agent Service│ ◄──── /ws/agent ──► │  CarChat.Api    │ ◄── /ws/app ──────► │ Android App  │
│              │                     │  (WebSocket      │                     │              │
│              │                     │    Relay)        │                     │              │
└──────────────┘                     └────────┬────────┘                     └──────────────┘
                                              │
                                     ┌────────▼────────┐
                                     │  CarChat.Web    │
                                     │  (Blazor Server) │
                                     │  Account & Key   │
                                     │  Management      │
                                     └─────────────────┘
```

---

## Projects

| Project | Type | Port (HTTP) | Port (HTTPS) | Purpose |
|---|---|---|---|---|
| `CarChat.Core` | Class Library | — | — | Shared models, EF Core, services |
| `CarChat.Api` | ASP.NET Core Web API | `11000` | `11001` | WebSocket relay + REST API |
| `CarChat.Web` | Blazor Server | `11002` | `11003` | Account & API key management UI |

---

## Architecture

### Communication Flow

1. **Android App** authenticates via OAuth (Google, GitHub, or Microsoft)
2. App exchanges the OAuth token for a **platform JWT** at `POST /api/auth/token`
3. App opens a WebSocket to `/ws/app?token=<jwt>`
4. **Agent** connects to `/ws/agent` with `X-Api-Key: <key>` header
5. Platform **pairs them 1:1** based on the user account and relays all messages bidirectionally
6. On disconnect, the other side receives a `{"type":"peer_disconnected"}` notification — no messages are stored

### Session Manager

An in-memory `SessionManager` (singleton) maintains two `ConcurrentDictionary` registries:
- `userId → AppWebSocket`
- `userId → AgentWebSocket`

When both sockets are present for a user, the relay is live. This design is horizontally scalable by replacing the in-memory store with a Redis-backed session registry.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- OAuth app credentials for at least one provider (Google, GitHub, or Microsoft)

### 1. Clone & Build

```bash
git clone https://github.com/dylan-mccarthy/car-chat-platform.git
cd car-chat-platform
dotnet build
```

### 2. Configure OAuth Credentials

Use [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) to store credentials safely (never commit secrets to source control).

**For CarChat.Api** (token exchange endpoint):
```bash
cd src/CarChat.Api
dotnet user-secrets set "OAuth:Google:ClientId"       "<your-google-client-id>"
dotnet user-secrets set "OAuth:Google:ClientSecret"   "<your-google-client-secret>"
dotnet user-secrets set "OAuth:GitHub:ClientId"       "<your-github-client-id>"
dotnet user-secrets set "OAuth:GitHub:ClientSecret"   "<your-github-client-secret>"
dotnet user-secrets set "OAuth:Microsoft:ClientId"    "<your-microsoft-client-id>"
dotnet user-secrets set "OAuth:Microsoft:ClientSecret" "<your-microsoft-client-secret>"
dotnet user-secrets set "Jwt:SecretKey"               "<strong-random-secret-min-32-chars>"
```

**For CarChat.Web** (website login):
```bash
cd src/CarChat.Web
dotnet user-secrets set "OAuth:Google:ClientId"       "<your-google-client-id>"
dotnet user-secrets set "OAuth:Google:ClientSecret"   "<your-google-client-secret>"
dotnet user-secrets set "OAuth:GitHub:ClientId"       "<your-github-client-id>"
dotnet user-secrets set "OAuth:GitHub:ClientSecret"   "<your-github-client-secret>"
dotnet user-secrets set "OAuth:Microsoft:ClientId"    "<your-microsoft-client-id>"
dotnet user-secrets set "OAuth:Microsoft:ClientSecret" "<your-microsoft-client-secret>"
```

### 3. Run

In separate terminals:

```bash
# Terminal 1 — API
cd src/CarChat.Api
dotnet run

# Terminal 2 — Website
cd src/CarChat.Web
dotnet run
```

- **API**: http://localhost:11000
- **Website**: http://localhost:11002
- **API Explorer (Scalar)**: http://localhost:11000/scalar/v1

---

## API Reference

### Authentication

#### `POST /api/auth/token`

Exchanges an OAuth provider token for a platform JWT. Used by the Android app before opening a WebSocket connection.

**Request body:**
```json
{
  "provider": "google",
  "identityToken": "<oauth-id-token-or-access-token>"
}
```

| Field | Values |
|---|---|
| `provider` | `"google"` \| `"github"` \| `"microsoft"` |
| `identityToken` | Google: ID token · GitHub: access token · Microsoft: access token |

**Response `200 OK`:**
```json
{
  "token": "<platform-jwt>",
  "expiresAt": "2026-03-31T12:00:00Z"
}
```

**Response `401 Unauthorized`:**
```json
{ "error": "Invalid or expired OAuth token" }
```

---

### WebSocket — Android App

#### `GET /ws/app?token=<jwt>`

Upgrades to a WebSocket connection for the authenticated app user. Pass the platform JWT as a query parameter (required for WebSocket upgrade compatibility).

**Authentication:** Platform JWT via `?token=<jwt>` query param or `Authorization: Bearer <jwt>` header.

**On connect:** The session is registered. If the agent is already connected, the relay becomes active immediately.

**On peer disconnect:** Receives `{"type":"peer_disconnected"}`.

---

### WebSocket — Agent Service

#### `GET /ws/agent`

Upgrades to a WebSocket connection for the agent service.

**Authentication:** `X-Api-Key: <raw-key>` header. The key must be active (not revoked).

**On connect:** `LastUsedAt` is updated on the API key. If the app is already connected, the relay becomes active immediately.

**On peer disconnect:** Receives `{"type":"peer_disconnected"}`.

---

### Health

#### `GET /api/health`

```json
{ "status": "ok", "timestamp": "2026-03-30T12:00:00Z" }
```

#### `GET /api/health/sessions`

Returns live WebSocket connection counts.

```json
{
  "appConnections": 1,
  "agentConnections": 1,
  "pairedSessions": 1
}
```

---

## API Keys

API keys are managed via the **CarChat.Web** dashboard at `http://localhost:11002/dashboard`.

- **Format:** `ccp_<base64url(32 random bytes)>` — ~47 characters, URL-safe, prefixed for easy identification
- **Storage:** Only the SHA-256 hash of the key is stored. The raw key is shown **once** on creation.
- **Revocation:** Immediate — revoked keys are rejected on the next connection attempt.

### Key Lifecycle

```
[User creates key on website]
        ↓
  Raw key displayed once
  Hash stored in DB
        ↓
[Agent uses: X-Api-Key: ccp_...]
        ↓
  Platform hashes → looks up → checks not revoked → connects
        ↓
[User revokes key on website]
        ↓
  RevokedAt set → agent rejected on next connect
```

---

## Database

CarChat uses **SQLite** for development and **PostgreSQL** for production. The provider is controlled by configuration:

```json
// appsettings.json
{
  "Database": { "Provider": "sqlite" },
  "ConnectionStrings": { "DefaultConnection": "Data Source=carchat.db" }
}
```

For PostgreSQL, set:
```json
{
  "Database": { "Provider": "postgresql" },
  "ConnectionStrings": { "DefaultConnection": "Host=...;Database=...;Username=...;Password=..." }
}
```

The database schema is created automatically on startup via `EnsureCreated()`.

### Schema

**Users**

| Column | Type | Notes |
|---|---|---|
| `Id` | TEXT (GUID) | Primary key |
| `Email` | TEXT | Unique |
| `DisplayName` | TEXT | |
| `Provider` | TEXT | `google` \| `github` \| `microsoft` |
| `ProviderSubjectId` | TEXT | OAuth `sub` / user ID |
| `CreatedAt` | DATETIME | |

**ApiKeys**

| Column | Type | Notes |
|---|---|---|
| `Id` | TEXT (GUID) | Primary key |
| `UserId` | TEXT | FK → Users.Id (cascade delete) |
| `Name` | TEXT | User-defined label |
| `KeyHash` | TEXT | SHA-256 hex of raw key (unique) |
| `CreatedAt` | DATETIME | |
| `LastUsedAt` | DATETIME? | Updated on agent connect |
| `RevokedAt` | DATETIME? | `null` = active |

---

## Website

The Blazor Server website (`CarChat.Web`) provides:

| Page | Route | Description |
|---|---|---|
| Home | `/` | Landing page with feature overview |
| Sign In | `/login` | OAuth provider selection |
| Dashboard | `/dashboard` | Create, view, and revoke API keys |
| Account | `/account` | Profile info and sign out |

---

## Security Notes

- **JWT secret key** — must be at least 32 characters. Set via user secrets or environment variable in production. Never commit to source control.
- **OAuth credentials** — store via user secrets locally, environment variables or a secrets manager in production.
- **API keys** — only hashes are stored. If a raw key is lost, revoke it and generate a new one.
- **HTTPS** — always use HTTPS in production. The `launchSettings.json` includes HTTPS profiles for development.
- **WebSocket origin** — consider adding `AllowedOrigins` to `WebSocketOptions` in production to restrict connections.

---

## Scaling

The current in-memory `SessionManager` works for a single server instance. To scale horizontally:

1. Replace `SessionManager` with a **Redis-backed** implementation
2. Add a **Redis backplane** (or use Azure SignalR / similar) so WebSocket sessions can be routed across multiple nodes

---

## Project Structure

```
car-chat-platform/
├── CarChatPlatform.slnx
├── .editorconfig
├── .gitignore
└── src/
    ├── CarChat.Core/
    │   ├── Data/
    │   │   └── AppDbContext.cs
    │   ├── Extensions/
    │   │   └── ServiceCollectionExtensions.cs
    │   ├── Models/
    │   │   ├── ApiKey.cs
    │   │   └── User.cs
    │   └── Services/
    │       ├── ApiKeyService.cs
    │       ├── IApiKeyService.cs
    │       ├── ISessionManager.cs
    │       └── SessionManager.cs
    │
    ├── CarChat.Api/
    │   ├── Controllers/
    │   │   ├── AuthController.cs
    │   │   └── HealthController.cs
    │   ├── Models/
    │   │   └── TokenRequest.cs
    │   ├── Services/
    │   │   ├── IJwtService.cs
    │   │   ├── JwtService.cs
    │   │   ├── IOAuthValidationService.cs
    │   │   └── OAuthValidationService.cs
    │   ├── WebSockets/
    │   │   └── RelayMiddleware.cs
    │   └── Program.cs
    │
    └── CarChat.Web/
        ├── Components/
        │   ├── Layout/
        │   │   ├── MainLayout.razor
        │   │   └── NavMenu.razor
        │   └── Pages/
        │       ├── Home.razor
        │       ├── Login.razor
        │       ├── Dashboard.razor
        │       └── Account.razor
        ├── Services/
        │   └── UserSessionService.cs
        └── Program.cs
```
