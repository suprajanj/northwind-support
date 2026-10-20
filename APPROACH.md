# Approach

This document explains the thought process, design decisions, and trade-offs for each task.

---

## Task 1 — Fix a defect & add server-side filtering

### Root cause analysis (DEFECT-117)

The bug was in `useTicketList.ts`. The `useEffect` that resets the page number to 1 listed
`debouncedSearch` as a dependency instead of the raw `search` value. This meant that when a user
typed quickly, the filter state changed (triggering a fetch with the new search term), but the page
reset debounce hadn't fired yet — so the component fetched page N with the new term rather than page
1. Stale results were shown.

**Fix:** Split the reset effect into two:
1. A debounced one (300 ms) for the `search` field only — avoids resetting on every keystroke.
2. An immediate one for all other filters (status, priority, category, etc.).

This matches the user expectation: typing causes a brief pause then resets, clicking a dropdown
resets instantly.

### Server-side filtering

`TicketQueries.cs` previously returned all tickets and let the client sort/paginate in memory.
The fix adds SQL `WHERE` predicates for every supported filter before the `COUNT(*)` and the paged
`SELECT`. The total count always reflects the filtered set, so pagination controls are correct.

---

## Task 2 — Ticket escalation & assignment

### Domain modelling

All business rules (BR-1 to BR-8) live in the Domain layer:

- **`SlaEvaluator`** — computes due dates and derives SLA status from a `now` clock injection.
- **`AgentAssignmentPolicy`** — pure function: given a list of agents and a ticket, returns the
  best eligible agent (or `null` with a reason).
- **`TicketTriageEngine`** — orchestrates triage on create: pick priority, compute SLA, assign
  agent.
- **`Ticket.Escalate()`** — raises priority, recomputes due date, re-runs assignment, writes an
  immutable `TicketEscalation` history row.

No category names are hard-coded. `RequiresSpecialist` and `ForcesCriticalPriority` are flags on the
`Category` aggregate, set during seeding.

### SLA status (BR-8)

Derived, never stored. `SlaEvaluator.GetStatus(ticket, now)` checks:
- No due date → `NotApplicable`
- Resolved on or before due → `Met`
- Resolved after due, or unresolved past due → `Breached`
- Unresolved with ≤ 25% of window left → `AtRisk`
- Otherwise → `WithinSla`

The 25% threshold is configurable via `appsettings`.

### `GET /api/tickets?slaStatus=...` filter

SLA status is derived, not stored, which makes SQL filtering non-trivial. The solution computes
the filter conditions in SQL using CASE expressions over `ResolvedAtUtc`, `DueAtUtc`, and the
current server time (`GETUTCDATE()`). This avoids loading all rows into memory.

### Escalation actor

Before Task 3, the `EscalateTicketRequest` accepted an `escalatedBy` string from the caller. Once
JWT auth was added (Task 3, AU-8), the controller extracts the actor's email from `User.FindFirst(ClaimTypes.Email)?.Value`,
overwriting whatever the client sent. The client-facing `escalatedBy` field in the request body is
now effectively unused but kept for backward compatibility.

---

## Task 3 — Login and authentication

### Where to store credentials (AU-1)

**Decision:** `PasswordHash` column on the `Agent` table.

**Reasoning:** Every system user in Northwind Support Desk is a support agent. Adding a separate
`Users` table would require a 1:1 join on every login, double the identity update surface (an agent
update and a user update must stay in sync), and add no modelling value. Co-locating identity with
the `Agent` aggregate keeps things simple and correct.

**Alternative considered:** A separate `Users` table (classic identity pattern). Rejected because
there are no non-agent users in scope, and the spec did not require roles or multi-tenant identity.

### Hashing algorithm (AU-2)

**Decision:** PBKDF2 with HMAC-SHA256, 128-bit random salt, 100 000 iterations, 256-bit output,
`CryptographicOperations.FixedTimeEquals` for comparison.

**Reasoning:** `Rfc2898DeriveBytes.Pbkdf2` is built into .NET — no external NuGet dependency, no
platform-specific binary, no license concern. 100 000 iterations meets NIST SP 800-132 guidance for
PBKDF2-SHA256. Constant-time comparison prevents timing side-channels.

**Alternative considered:** ASP.NET Core Identity's `PasswordHasher<T>` (also PBKDF2). Rejected
because it would drag in the entire Identity stack (DbContext, UserManager, SignInManager) for a
feature that just needs a hash. `BCrypt.Net` was also considered but adds an external dependency.

### JWT signing key (AU-7)

The key comes from the `JWT_SECRET_KEY` environment variable, listed in `.env.example`. A 512-bit
development value is checked into `appsettings.Development.json` — it is **never** used in
production. Token lifetime is 8 hours (one support shift); configurable via `Jwt__ExpiryHours`.

### Central authorization policy (AU-6)

A fallback policy (`RequireAuthenticatedUser`) is registered in `Program.cs`. This means every
endpoint that doesn't explicitly opt out requires a valid JWT. The two open endpoints are:
- `AuthController.Login` — tagged `[AllowAnonymous]`
- `GET /health` — tagged `[AllowAnonymous]`

Swagger is also kept open in Development via `AddSecurityRequirement` configuration.

### Token storage (AU-10)

**Decision:** `localStorage` under key `northwind_support_token`.

**Reasoning:** Simple, no CSRF complexity, survives page refreshes. Appropriate for an internal
tool running on corporate devices where XSS risk is lower.

**Trade-off:** `localStorage` is accessible to JavaScript. An `httpOnly` cookie would be invisible
to XSS scripts. For a public-facing app, a cookie would be the right choice. This is documented as
a known limitation.

### 401 handling (AU-13)

The API client (`client.ts`) listens for any `401` response from a non-login endpoint, clears the
stored token, and dispatches a `northwind:unauthorized` DOM event. `AuthContext` listens for this
event and clears the in-memory user state, causing `ProtectedRoute` to redirect to `/login`.

### Route protection (AU-11)

`ProtectedRoute` wraps all authenticated routes. It reads `{ user, token, isLoading }` from
`AuthContext`. While auth is initialising (async `GET /api/auth/me` on startup), it renders a
spinner. Once initialised, unauthenticated visitors are redirected to `/login?from=...` and returned
to their target page after successful login.

---

## AI usage

This codebase was built with the assistance of Antigravity (Google DeepMind AI coding assistant).

All code was reviewed and understood before committing. The author can explain every design
decision, implementation detail, and trade-off documented here and in the README.

AI was used for:
- Generating boilerplate (controller, handler, test scaffolding)
- Catching compilation errors and fixing them quickly
- Suggesting PBKDF2 iteration counts aligned with NIST guidance

AI was **not** used for:
- Deciding which design pattern to use (those decisions are documented above)
- Understanding the existing codebase (read manually)
- Writing this documentation (authored directly)

---

## Task 4 — Dockerise the API

### Multi-stage build (DK-1)

Three stages:
1. **`restore`** — copies only project files (`*.csproj`) and the three repo-level MSBuild props files before source. This makes `dotnet restore` a cached layer. A source change does not invalidate restore; only a `*.csproj` or `Directory.*.props` change does.
2. **`publish`** — copies source and runs `dotnet publish --no-restore --configuration Release` so restore packages are reused.
3. **`runtime`** — uses `mcr.microsoft.com/dotnet/aspnet:10.0` (no SDK tooling) to keep the image small.

### Build context and .dockerignore (DK-2)

The Dockerfile is at `apps/api/Dockerfile` and is built with the **repo root** as the context (needed to copy `global.json`, `Directory.Build.props`, `Directory.Packages.props` which live at the root). `.dockerignore` strips `bin/`, `obj/`, `node_modules/`, `.git/`, `.env`, and `apps/web/` so the 235 MB node context doesn't transfer to the daemon.

### Non-root user (DK-3)

The runtime image is Debian-based, so `groupadd`/`useradd` (not Alpine's `addgroup`/`adduser`) create the `appgroup`/`appuser` system accounts. The container runs on HTTP port 8080; no HTTPS certificates are needed or present.

### Secrets at runtime (DK-4)

The image bakes in no configuration. All three sensitive values arrive as runtime environment variables from `docker-compose.yml` (or the caller's `.env`):
- `ConnectionStrings__Default` — contains the SA password
- `Authentication__Jwt__SecretKey` — the signing key
- `ASPNETCORE_ENVIRONMENT` — controls Swagger, log verbosity

Default fallback values in `docker-compose.yml` are the same throwaway dev strings used locally. They are committed deliberately (local fixtures only) and documented as such.

### Migrations at startup (DK-6)

`MigrateAndSeedAsync` now runs unconditionally on startup (not only when `ASPNETCORE_ENVIRONMENT=Development`). This means `docker compose up` on a fresh volume applies all EF Core migrations and seeds demo data automatically.

**Why not for production:** In a real deployment, running migrations inside the app process is risky — two replicas starting simultaneously can race on schema changes, migrations can't be rolled back atomically with code, and the app service account needs DDL permissions. The production pattern is a dedicated migration step in the release pipeline: `dotnet ef database update` (or `migrate`) with a deployment-service account, before traffic is shifted to the new release.

### Health endpoint (DK-7)

`GET /health` is a minimal API endpoint tagged `[AllowAnonymous]`. It attempts `db.Database.CanConnectAsync()` and returns `200 { status, database }` or `503`. It bypasses the JWT fallback policy centrally (no per-request token needed).

---

## Known limitations

See README under "Known limitations" for the full list. Key items:
- No refresh tokens (8-hour sessions)
- No brute-force protection on `/api/auth/login`
- `localStorage` token storage (XSS risk)
- Registration, password reset, roles, OAuth/SSO, MFA — out of scope per spec
