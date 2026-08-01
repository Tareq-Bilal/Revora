# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Infrastructure

```bash
docker compose up -d          # Postgres :5432, MongoDB :27018, RabbitMQ :5672 (mgmt :15672)
docker compose ps
```

### Backend (.NET 10)

```bash
dotnet build Revora.slnx
dotnet build src/AuctionService/AuctionService.csproj

dotnet run --project src/IdentityService/IdentityService.csproj    # :5001
dotnet run --project src/AuctionService/AuctionService.csproj      # :7001
dotnet run --project src/SearchService/SearchService.csproj        # :7002
```

Seed Identity users (creates the `Identity` database via EF migrations, inserts
`alice` / `bob`, password `Pass123$`, then exits — it does not stay running):

```bash
dotnet run --project src/IdentityService/IdentityService.csproj -- /seed
```

Client secrets live in user-secrets, never in `appsettings.json`. The machine
secret must be identical in IdentityService and SearchService:

```powershell
dotnet user-secrets set "IdentityClients:Machine:ClientSecret" "<machine-secret>" --project src\IdentityService\IdentityService.csproj
dotnet user-secrets set "IdentityClients:Interactive:ClientSecret" "<interactive-secret>" --project src\IdentityService\IdentityService.csproj
dotnet user-secrets set "IdentityService:ClientSecret" "<machine-secret>" --project src\SearchService\SearchService.csproj
```

EF migrations:

```bash
dotnet ef migrations add <Name> -c AuctionDbContext -o Migrations --project src/AuctionService
dotnet ef migrations add <Name> -c ApplicationDbContext -o Data/Migrations --project src/IdentityService
```

There are **no .NET test projects** in this repository yet.

### Frontend (`src/IdentityService.Ui`)

```bash
npm run dev          # private Next.js on 127.0.0.1:3000 — browse via :5001, never :3000 directly
npm run build        # runs tokens:check first, fails on stale tokens
npm run lint         # eslint + stylelint
npm run typecheck
npm test             # vitest
npm run tokens:generate
npm run api:generate # requires IdentityService running; reads its OpenAPI doc
```

Single test:

```bash
npx vitest run test/ui.test.tsx
npx vitest run -t "keeps required scopes selected"
```

### Gotchas

- Building while a service is running fails with `MSB3027` (the running process
  locks its `.exe`). Stop the service, or build to a scratch output path with
  `-p:BaseOutputPath=<temp>/`.
- After deleting an `app/` route, stale `.next` route validators break
  `typecheck`. `rm -rf .next tsconfig.tsbuildinfo` and re-run.
- `Revora.slnx` contains only `.csproj` projects. Do not add `.Ui` projects to it.

## Architecture

### Services

| Path | Role | Store |
|---|---|---|
| `src/IdentityService` | Duende IdentityServer 8 — OAuth 2.0 / OIDC authority | Postgres `Identity` |
| `src/IdentityService.Ui` | Next.js login/consent/grants UI, proxied by IdentityService | — |
| `src/AuctionService` | Auction CRUD, publishes integration events | Postgres `Auctions` |
| `src/SearchService` | Read model over auction events | MongoDB |
| `src/Contracts` | Shared integration events + auth constants | — |

`README.md` documents `WebApp` (Next.js client + BFF), `Gateway`,
`BiddingService`, and `NotificationService`. **None of these exist yet** — that
section describes target architecture, not current state.

### Auth is the backbone — read `OAUTH_GUIDE.md` first

IdentityService is the single authorization server. AuctionService and
SearchService are resource servers that validate JWTs **locally** against cached
JWKS (set by `options.Authority` in each `Program.cs`) — they do not call
IdentityService per request.

`src/Contracts/RevoraAuth.cs` is the single source of truth for scope values,
audiences, and policy names. **Never hardcode `"scope1"`, `"auction-api"`, or a
policy string** — a drifted literal silently fails policy checks at runtime with
no compile error.

Two clients (`src/IdentityService/Config.cs`):
- `m2m.client` — Client Credentials, used by SearchService to call
  AuctionService's `/api/auctions/sync`.
- `interactive` — Authorization Code + PKCE, reserved for the future WebApp/BFF.

`AUTHENTICATION_GUIDE.md` covers claim/policy/audience tables and ownership
rules. `OAUTH_GUIDE.md` explains the protocol and maps it onto these files.

### The Identity UI is proxied, not standalone

IdentityService fronts Next.js with YARP. Adding a UI route requires **two**
changes, and missing either produces a 404 that looks like a Next.js bug:

1. The page under `src/IdentityService.Ui/app/`.
2. A matching `ReverseProxy.Routes` entry in
   `src/IdentityService/appsettings.json` (note `identity-ui-pages` uses a regex
   allowlist of page names).

The proxy strips `Cookie` and `Authorization` headers before forwarding, so Node
never sees the Identity cookie. React talks to `/api/identity-ui/*` minimal-API
endpoints (`IdentityUi/IdentityUiEndpoints.cs`) which perform all authentication
against ASP.NET Identity. React never validates a password, issues a token, or
creates a cookie.

### Event flow and the outbox

AuctionService uses the MassTransit EF Core outbox. The ordering in
`AuctionsController` is deliberate: `publishEndpoint.Publish(...)` is called
**before** `SaveChangesAsync()` so the message and the row commit in one
transaction. Preserve that ordering in new write paths.

SearchService consumes `AuctionCreated` / `AuctionUpdated` / `AuctionDeleted`
from `src/Contracts` into its MongoDB read model. Changing an event shape is a
cross-service contract change — update `Contracts` and every consumer together.

### Design tokens are generated

`src/IdentityService/DESIGN-bmw-m.md` YAML front matter is the canonical design
source **for all UI in the repository** (per `AGENTS.md` and `.codex/config.toml`).
`src/IdentityService.Ui/app/tokens.css` is generated from it.

- Never edit `tokens.css` directly.
- Extend the canonical source before introducing a new token, then regenerate.
- `npm run build` runs `tokens:check` and fails on drift.
- No raw colors, spacing, radii, shadows, or motion values in UI files when a
  token exists.
- No BMW logos, BMW-owned fonts, or automotive photography — use the Revora
  identity and the documented Inter substitute.

`src/IdentityService.Ui/lib/api.generated.ts` is also generated (from the running
service's OpenAPI document) and is currently unreferenced by application code.

## Adding features

### SOLID, grounded in this codebase

These are the patterns already in use — follow them rather than inventing new ones.

**Single responsibility.** `ClientCredentialsTokenService` fetches and caches
tokens; `ClientCredentialsTokenHandler` attaches them to requests; the calling
`AuctionSvcHttpClient` knows about neither. When a class starts doing both
acquisition and transport, split it that way.

**Open/closed.** Authorization extends through named policies
(`AddPolicy(RevoraAuth.AuctionWritePolicy, ...)`) applied as
`[Authorize(Policy = ...)]`, not through inline claim checks scattered in action
bodies. Add a policy; don't add an `if` in a controller.

**Liskov / interface segregation.** Abstractions stay narrow —
`ISearchIndexService` exposes only what consumers call. Prefer a small new
interface over widening an existing one.

**Dependency inversion.** Everything resolves through DI with primary
constructors (`public sealed class RevoraProfileService(UserManager<...> userManager, ...)`).
Bind configuration to an options class validated at startup with
`.ValidateOnStart()` (see `IdentityServiceOptions`) so a missing secret fails on
boot, not on the first live request.

### C# conventions in this repo

- File-scoped namespaces; `Nullable` and `ImplicitUsings` enabled.
- Primary constructors for DI; `sealed record` for DTOs and contracts.
- Explicit `StringComparison.Ordinal` / `StringComparer.Ordinal` on string
  comparison, and `CultureInfo.InvariantCulture` on parsing/formatting.
- Thread `CancellationToken` through async call chains.
- Minimal APIs return `Results.ValidationProblem` / `Results.Problem`;
  controllers return `ActionResult<T>`. Browser-facing mutations validate an
  antiforgery token first.
- `_ =` discards on fluent service-registration chains in IdentityService.
- `.editorconfig` requires a `_` prefix on private fields (suggestion severity).

### Where new work belongs

- Cross-service constants, events, auth strings → `src/Contracts`.
- A new resource server → its own project; copy the `AddJwtBearer` +
  `AddAuthorizationBuilder` shape from `AuctionService/Program.cs`, and take
  scope/audience names from `RevoraAuth`.
- Service-owned admin UI → `src/<Service>.Ui`, proxied by that service.
- The customer-facing app and its BFF → `src/WebApp`, a flat sibling of the
  services (it is not owned by any one service).

## Frontend conventions

Next.js App Router, React 19, **CSS Modules** — there is no Tailwind or CSS-in-JS.

### Self-contained components

- Route files stay thin. `app/Consent/page.tsx` exports `metadata` and renders
  one component; all logic lives in `components/`.
- A component owns its styles via a colocated `*.module.css`. Shared primitives
  live in `components/ui.tsx` + `ui.module.css`.
- Components receive data through props or a hook — not through reaching into
  global state.
- Remove the CSS rule when you remove the component that used it; orphaned
  module classes are invisible to stylelint.

### Avoiding extra renders

- `"use client"` goes on the smallest subtree that actually needs
  interactivity. Anything static stays a server component (see `HomePage`).
- Fetching goes through the `useIdentityData` hook pattern: the request function
  is wrapped in `useCallback` keyed on its inputs, and the `useEffect` depends on
  that stable callback. Do not inline an async call directly in `useEffect`
  without a stable dependency.
- Never pass a freshly-constructed object, array, or arrow function as a prop
  when it can be hoisted or memoized — a new reference each render forces
  children to re-render.
- Gate data fetching by passing `null` as the path until required query params
  are known (the `ready` flag from `useQueryParam`), rather than firing a request
  and discarding the result.
- Give list items a stable domain key (`grant.clientId`, `scope.value`), not an
  array index.

### Styling

Use design tokens exclusively — the generated set is `--color-*`, `--space-*`,
`--type-*`, `--radius-*`, `--motion-*`, and `--component-*`. Preserve focus states, keyboard operation,
reduced-motion support, labels, contrast, and minimum touch targets in every UI
change.

Before any UI work, read `src/IdentityService/DESIGN-bmw-m.md` in full.
