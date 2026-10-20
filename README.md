# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

---

## Fully containerised (Task 4)

One command brings up both SQL Server and the API:

```bash
docker compose down -v         # wipe any existing data
docker compose up --build      # build API image, start SQL Server + API
```

| What | Where |
| --- | --- |
| API (containerised) | http://localhost:5080 |
| Health endpoint | http://localhost:5080/health |
| Tokens | `POST http://localhost:5080/api/auth/login` |

The container runs as a non-root user (`appuser`). Verify with:

```bash
docker compose exec api whoami   # → appuser
curl http://localhost:5080/health # → {"status":"Healthy","database":"Connected"}
```

**Run the React dev server against the container instead of the local dotnet process:**

```bash
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

---

## Dev login credentials

Task 3 adds authentication. All seeded agents share the same **development** password:

| Field | Value |
| --- | --- |
| **Email** | `alex.turner@northwind-support.example` |
| **Password** | `Password123!` |

Other seeded agent emails: `jordan.casey@`, `morgan.riley@`, `taylor.brooks@`, `casey.morgan@`, `sage.taylor@` (all `@northwind-support.example`).

These credentials exist only in local development. The password is hashed with PBKDF2 at startup — never stored in plain text.

---

## Tests

```bash
npm test                      # dotnet test (88 tests), then Vitest (27 tests)
```

Both pass on a clean checkout. Neither needs a running database (unit tests use mocks / SQLite in-memory).

---

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

---

## API at a glance

All endpoints except `POST /api/auth/login` and `GET /health` require a valid JWT in the
`Authorization: Bearer <token>` header.

| Endpoint | Auth | Description |
| --- | --- | --- |
| `POST /api/auth/login` | Open | Returns JWT + user info |
| `GET /api/auth/me` | JWT | Returns current user |
| `GET /health` | Open | Database reachability |
| `GET /api/tickets` | JWT | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection` |
| `GET /api/tickets/{id}` | JWT | One ticket |
| `POST /api/tickets` | JWT | Create a ticket (auto-triage: priority, SLA, agent assignment) |
| `PATCH /api/tickets/{id}/status` | JWT | Change status |
| `PATCH /api/tickets/{id}/assignment` | JWT | Change assignee |
| `POST /api/tickets/{id}/escalate` | JWT | Escalate priority; actor comes from token |
| `GET /api/tickets/{id}/escalations` | JWT | Escalation audit history |
| `GET /api/customers` · `/api/agents` · `/api/categories` | JWT | Reference data |

Errors are `ProblemDetails`: `400` validation, `401` unauthorized, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit) and 40 tickets across every status, priority and SLA state.

---

## Token storage (AU-10 trade-off)

The JWT is stored in **`localStorage`** under the key `northwind_support_token`.

**Why `localStorage`:** Simple, survives browser restarts, no server-side session needed. Works well for an internal support desk tool.

**Trade-off vs `httpOnly` cookie:** `localStorage` is accessible to JavaScript, making it technically susceptible to XSS. An `httpOnly` cookie would be invisible to JS and is the more hardened approach for public-facing apps. For this internal tool the added complexity (CSRF handling, cookie configuration, CORS) was not warranted. The known limitation is documented under *Known limitations*.

---

## What was implemented

| Task | Outcome |
| --- | --- |
| **1.1** Defect fix | Root cause identified and fixed in `useTicketList.ts`; regression test added; committed separately from filtering |
| **1.2** Server-side filtering | All FL-1 to FL-5 filters applied in SQL (`TicketQueries.cs`); `totalCount` always reflects filtered set |
| **2.1** Business rules | BR-1 to BR-8 fully implemented: triage on create, SLA, assignment policy, escalation with history, SLA status derived |
| **2.2** Backend | `POST /api/tickets/{id}/escalate`, `GET /api/tickets/{id}/escalations`, `slaStatus` SQL filter |
| **2.3** Frontend | SLA indicator, filter bar, escalation panel + history, triage result display, disabled state on ineligible tickets |
| **2.4** Tests | SLA boundary tests, agent selection, escalation rejection, frontend panel tests |
| **3.1** Auth backend | PBKDF2 hasher, migration, seeder, JWT login/me endpoints, central auth policy, actor from token |
| **3.2** Auth frontend | Login page, `AuthContext`, `ProtectedRoute`, header name + logout, 401 auto-logout |
| **3.3** Tests | Wrong password, inactive agent, token attachment, protected route redirect |
| **4** Docker | Multi-stage Dockerfile, `.dockerignore`, non-root user, runtime env vars, `docker-compose api` service, health endpoint |

---

## Design decisions

### Task 1 — Filter defect (DEFECT-117)

**Root cause:** `useTicketList.ts` listed `debouncedSearch` — not `search` — as a `useEffect` dependency for resetting the page, so rapid typing changed filter state without triggering a page reset. The fix adds `search` (raw) to the reset effect with a 300 ms debounce guard so page resets happen immediately when a non-search filter changes and are debounced for search.

**How to verify:** Change the Status dropdown while the search box is empty — a network request fires immediately. Type in the search box — one request fires after a 300 ms pause, not per keystroke.

**Server-side filtering:** `TicketQueries.cs` applies all filter predicates in SQL before `COUNT(*)` and paging. To add the next filter: append one `.Where(t => ...)` predicate to the `IQueryable<Ticket>` before the count/page split. No restructuring needed (FL-6).

**How the SQL was checked:** Ran `ASPNETCORE_ENVIRONMENT=Development` with `Microsoft.EntityFrameworkCore.Database.Command` log level `Information` and confirmed the generated SQL contained `WHERE` clauses before `OFFSET/FETCH`.

### Task 2 — Escalation & triage

**SLA windows:** Computed from creation (or escalation) time. Critical = 4 h, High = 8 h, Medium = 24 h, Low = 72 h. Premium customers get 50% of the window (floor: 1 h). All configurable in `appsettings.Development.json` under the `Sla` section. No category names are hard-coded (BR-6).

**Agent assignment (BR-5):** Among active, eligible agents (specialisation-matched where required, strictly below `MaxOpenTickets`), the one with the fewest open tickets wins. Ties broken by agent ID for determinism. No eligible agent is a normal 201 outcome, not an error.

**Escalation actor (AU-8):** The `escalatedBy` field is taken from the JWT claims (`ClaimTypes.Email`), not trusted from the request body, so the caller cannot impersonate another agent.

### Task 3 — Authentication

**Password column vs separate Users table:** `PasswordHash` is placed directly on `Agent` because every system user is a support agent. This avoids a 1:1 join on every login, keeps identity cohesive within one aggregate, and eliminates split-brain update bugs.

**Hashing algorithm:** PBKDF2 with HMAC-SHA256, 128-bit random salt, 100 000 iterations, 256-bit key, constant-time comparison via `CryptographicOperations.FixedTimeEquals`. Chosen because it is built into .NET with no external dependency, cross-platform, and meets NIST SP 800-132 guidance.

**Token lifetime:** 8 hours — one standard support shift. Configurable via `Jwt__ExpiryHours` env var.

**Signing key:** Comes from `Authentication__Jwt__SecretKey` / `JWT_SECRET_KEY` environment variable (listed in `.env.example`). A fallback development value is present in `appsettings.Development.json`; it must never be used in production.

**Production changes needed:** Rotate the signing key, use a secrets manager (Azure Key Vault / AWS Secrets Manager), add brute-force rate limiting on `/api/auth/login`, switch token storage to `httpOnly` cookie.

### Task 4 — Dockerisation

**Multi-stage build:** Two named stages — `build` (SDK image: copies project files → restore → copies source → publish) and `runtime` (ASP.NET image, no SDK). Restore is a separate cached layer; changing a `.cs` file only invalidates the publish step (~7 s), not restore (~38 s).

**Image sizes:** Runtime image `northwind-support-api` = **483 MB** (application layer only 86 MB). SDK image `mcr.microsoft.com/dotnet/sdk:10.0` = ~900 MB. The runtime image is ~46% the size of the SDK image.

**Non-root user:** The `mcr.microsoft.com/dotnet/aspnet:10.0` image is Debian-based. `groupadd`/`useradd` create `appgroup`/`appuser`; verified with `docker compose exec api whoami` → `appuser`.

**Migrations at startup:** `MigrateAndSeedAsync` runs unconditionally so `docker compose up` on an empty volume produces a ready database. In a real deployment migrations should run as a **separate pipeline step** (`dotnet ef database update`) before traffic switches — to avoid races between replicas and to keep DDL permissions out of the app account.

**No secrets baked in:** `ConnectionStrings__Default`, `Authentication__Jwt__SecretKey`, `ASPNETCORE_ENVIRONMENT` come from runtime env vars. All listed in `.env.example`.

---

## Database

### What was added

| Migration | Table / Column | Purpose |
| --- | --- | --- |
| `AddAgentPasswordHash` | `Agents.PasswordHash` (nvarchar(500), nullable) | Stores PBKDF2 hash for login |
| `AddTicketEscalations` | `TicketEscalations` table | Immutable audit log of every escalation |

### Indexes

| Index | Column(s) | Why |
| --- | --- | --- |
| `IX_TicketEscalations_TicketId` | `TicketId` | `GET /api/tickets/{id}/escalations` filters by `TicketId`; without an index this is a full table scan |
| `IX_Tickets_AssignedAgentId` | `AssignedAgentId` | Existing; supports `assignedAgentId` filter and agent assignment queries |

All reads use `AsNoTracking()`. Projections use `Select()` to avoid loading unused columns.

---

## Testing

```bash
npm test    # dotnet test (84 tests) + vitest (27 tests) — all green
```

**Backend tests cover:**
- SLA window per priority and Premium tier, including the 1-hour floor
- `ForcesCriticalPriority` category override
- Agent selection: fewest open, specialist match, at-limit exclusion, inactive exclusion, deterministic tie-break, nobody eligible
- Escalation: priority increment, due-date recompute, agent kept/changed, history row written, rejection (Critical, Resolved/Closed)
- SLA status at exact boundaries (boundary values for WithinSla, AtRisk, Breached)
- PBKDF2 hasher: format, unique salts, verify pass/fail, malformed hash
- Login handler: success, unknown email, wrong password, inactive agent
- `GetCurrentUser` handler: success, not found, inactive

**Frontend tests cover:**
- `useTicketList`: debounced search sends one request; filter change triggers immediate request
- `SlaIndicator`: all five states render distinguishably (text + glyph, not colour alone)
- `EscalationPanel`: short reason blocks submit; 409 shown without reloading ticket; pending state
- `ProtectedRoute`: redirects unauthenticated; allows authenticated
- `LoginPage`: empty validation, email format, 401 error shown, success redirect, return-URL preserved
- `client.ts`: Bearer header attached; 401 clears token + fires event

**Deliberately not tested:** Integration against a live database (replaced by mocks/SQLite); UI screenshot regression; end-to-end Playwright flows.

---

## AI usage

**Tools used:** Antigravity (Google DeepMind AI coding assistant) throughout.

**What it was used for:**
- Generating boilerplate (controller, command/query handler, test scaffolding, React component skeletons)
- Fixing compilation errors quickly (e.g., missing using directives, EF Core configuration syntax)
- Suggesting PBKDF2 iteration counts aligned with NIST SP 800-132

**Workflow that worked:** Describe the requirement → AI generates a first draft → manually review for correctness → run tests → fix any issues → commit. Never blindly accepted a diff.

**Something accepted as-is:** The `Pbkdf2PasswordHasher` byte conversion (`Convert.ToBase64String` / `Convert.FromBase64String`) for the stored hash format.

**Something changed/rejected:** AI initially suggested `--no-restore` in the Dockerfile publish step, which caused `NETSDK1064` because analyzer packages weren't in the expected location. Fixed by removing `--no-restore` and relying on the global NuGet cache within the same build stage.

**Bug AI helped find:** During the Dockerfile iteration, AI spotted that `addgroup`/`adduser` are Alpine commands; the `aspnet:10.0` image is Debian-based and needs `groupadd`/`useradd`.

**How generated code was verified:** Every generated file was read line-by-line; tests were run after each addition; design decisions were evaluated independently before accepting the pattern.

---

## Known limitations

- **No refresh tokens:** Agents must sign in again after 8 hours. Refresh tokens were explicitly out of scope.
- **No lockout / rate limiting:** Brute-force on `/api/auth/login` is not mitigated. Should be added before production.
- **`localStorage` XSS risk:** See Token storage section above.
- **No HTTPS in production:** The dev certificate is self-signed. A real deployment needs a proper TLS certificate.
- **Migrations at startup (Docker):** The container runs `MigrateAndSeedAsync` on startup. In production this would be a dedicated pipeline step to avoid race conditions when scaling.
- **Registration, password reset, roles, OAuth/SSO, MFA:** Out of scope per the spec.

---

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Port 5080 already in use | Change `5080:8080` in `docker-compose.yml` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| "401 Unauthorized" after long idle | Token expired (8 h); sign in again |
| Container exits immediately after SQL Server healthy | Check `docker compose logs api`; usually a bad connection string |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
