# Architecture

```mermaid
flowchart LR
  subgraph Client["Browser / installed PWA"]
    SPA["React + TypeScript SPA<br/>(Vite, React Router, TanStack Query)"]
  end

  subgraph Supabase
    AUTH["Supabase Auth<br/>email/password · Google OAuth"]
    STORE["Supabase Storage<br/>avatars · gym images · boulder photos · beta · community videos"]
    PG[("PostgreSQL<br/>schema: bouldertime")]
  end

  subgraph Server["ASP.NET Core Web API"]
    API["Api<br/>controllers · JWT verification · ProblemDetails"]
    APP["Application<br/>use cases · authorization rules"]
    DOM["Domain<br/>entities · invariants"]
    INF["Infrastructure<br/>EF Core · Npgsql · storage client"]
  end

  SPA -- "sign in, refresh session" --> AUTH
  SPA -- "REST + Bearer access token" --> API
  SPA -. "upload via short-lived signed URL (Phase 3)" .-> STORE
  API -- "verify token via JWKS" --> AUTH
  API --> APP --> DOM
  APP --> INF
  INF -- "EF Core" --> PG
  INF -. "issue signed URLs (service key, server-only)" .-> STORE
```

## Request flow

1. The SPA signs the user in with Supabase Auth and holds a short-lived access token.
2. Every API call carries `Authorization: Bearer <token>`.
3. The API verifies signature (JWKS, or legacy HS256 secret), issuer, audience and expiry.
4. `UserProvisioningMiddleware` ensures a `bouldertime.users` row exists for the token's `sub`.
5. Controllers call application services; **permissions are always read from the database** (gym staff
   relationships, platform-admin flag), never from token claims or request bodies.
6. Errors leave the API as RFC 7807 ProblemDetails with a stable `code` and `traceId`.

## Backend layers

| Project | Depends on | Contains |
|---|---|---|
| `BoulderTime.Domain` | nothing | Entities and invariants. No EF, no ASP.NET. |
| `BoulderTime.Application` | Domain, EF Core abstractions | Use-case services, validation, authorization checks, DTOs, `IAppDbContext`. |
| `BoulderTime.Infrastructure` | Application | `AppDbContext`, entity configurations, migrations, storage and clock implementations. |
| `BoulderTime.Api` | Application, Infrastructure | HTTP surface, auth wiring, error mapping, operator CLI. |
| `BoulderTime.Tests` | Api | Integration tests against real PostgreSQL (Testcontainers). |

A future native mobile client talks to the same REST API with the same Supabase tokens; nothing in the API is web-specific.

## Frontend structure

```
frontend/src
  app/          router and providers
  auth/         Supabase session context, route guard
  components/   shared UI (shell, states, form controls, logo)
  features/     one folder per domain area: API hooks + feature components
  lib/          API client, error model, Supabase client
  pages/        route-level screens
  styles/       brand tokens and global styles
```
