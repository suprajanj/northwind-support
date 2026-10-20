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

## Design decisions

### Task 1 — Filter defect (DEFECT-117)

**Root cause:** `useTicketList.ts` listed `debouncedSearch` — not `search` — as a `useEffect` dependency for resetting the page, so rapid typing changed filter state without triggering a page reset. The fix adds `search` (raw) to the reset effect with a 300 ms debounce guard so page resets happen immediately when a non-search filter changes and are debounced for search.

**Server-side filtering:** `TicketQueries.cs` now applies all filter predicates and the count query in SQL before pagination, so `totalCount` always reflects the filtered set.

### Task 2 — Escalation & triage

**SLA windows:** Computed from creation (or escalation) time. Critical = 4 h, High = 8 h, Medium = 24 h, Low = 72 h. Premium customers get 50% of the window (floor: 1 h). All configurable in `appsettings.Development.json` under the `Sla` section.

**Agent assignment (BR-5):** Among active, eligible agents (specialisation-matched where required, strictly below `MaxOpenTickets`), the one with the fewest open tickets wins. Ties broken by agent ID for determinism. No eligible agent is a normal 201 outcome, not an error.

**Escalation actor (AU-8):** The `escalatedBy` field is taken from the JWT claims (`ClaimTypes.Email`), not trusted from the request body, so the caller cannot impersonate another agent.

### Task 3 — Authentication

**Password column vs separate Users table:** `PasswordHash` is placed directly on `Agent` because every system user is a support agent. This avoids a 1:1 join on every login, keeps identity cohesive within one aggregate, and eliminates split-brain update bugs.

**Hashing algorithm:** PBKDF2 with HMAC-SHA256, 128-bit random salt, 100 000 iterations, 256-bit key, constant-time comparison via `CryptographicOperations.FixedTimeEquals`. Chosen because it is built into .NET with no external dependency, cross-platform, and meets NIST SP 800-132 guidance.

**Token lifetime:** 8 hours — one standard support shift. Configurable via `Jwt__ExpiryHours` env var.

**Signing key:** Comes from `JWT_SECRET_KEY` environment variable (listed in `.env.example`). A fallback development value is present in `appsettings.Development.json`; it must never be used in production.

---

## Known limitations

- **No refresh tokens:** Agents must sign in again after 8 hours. Refresh tokens were explicitly out of scope.
- **No lockout / rate limiting:** Brute-force on `/api/auth/login` is not mitigated. Should be added before production.
- **`localStorage` XSS risk:** See Token storage section above.
- **No HTTPS in production:** The dev certificate is self-signed. A real deployment needs a proper TLS certificate.
- **Registration, password reset, roles, OAuth/SSO, MFA:** Out of scope per the spec.

---

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| "401 Unauthorized" after long idle | Token expired (8 h); sign in again |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
