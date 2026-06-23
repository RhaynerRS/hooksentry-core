# HookSentry API — Roadmap

## Upcoming

| Feature | Priority |
|---------|----------|
| Automatic Circuit Breaker | High |
| HttpClient timeout in Worker | High |
| SSRF protection on destination URLs | High |
| Queue purge per Tenant | Medium |
| SSE — real-time update stream | Medium |
| In-app notification system | Medium |

---

### Automatic Circuit Breaker

Detect destinations with 5 consecutive HTTP failures and pause deliveries for the duration of `circuit_breaker_timer` configured on the Tenant. Auth failures (401, 403, 404) also count.

**State machine:** `Closed → Open → HalfOpen → Closed`

- New `WebhookDestinationState` entity with `failures_count`, `tripped_at`, `next_check_at`
- New `CircuitBreakerState` enum (Closed / Open / HalfOpen)
- Logic integrated into `WebhookDeliveryConsumer`: messages with an `Open` CB are deferred to the delay queue closest to `circuit_breaker_timer` without incrementing `retry_count`
- Probe on `HalfOpen`: success closes the CB, failure reopens with a new timer
- State created lazily on first interaction with the destination (no backfill for existing destinations)
- New `CircuitBreakerTimer` field on `EventMessage`
- `PublishDelayedAsync(message, queueOverride)` overload on `IEventPublisher`
- Migrations: `0021__webhook_destination_states`, `0022__idx_webhook_destination_states_destination`

**MVP limitation:** race condition with multiple Workers is resolved by the `UNIQUE (urls_destino_id)` constraint — the losing insert receives a `UniqueConstraintException` and requeues the message. With a single Worker the race does not occur.

---

### HttpClient timeout in Worker

Set `Timeout` on `SendAsync` to prevent hanging destinations from blocking all `prefetchCount` slots. Without this fix, a destination that accepts connections but never responds can hold up to `prefetchCount` slots for 100 seconds (.NET default).

- New `DeliveryTimeoutSeconds` field on `RabbitMqSettings` (default: 30s)
- Configurable via `RabbitMq:DeliveryTimeoutSeconds` in `appsettings.json` or environment variable
- `TaskCanceledException` bubbles up to `DispatchAsync` like any other failure: exponential backoff and eventual `CriticalFailure`

---

### SSRF protection on destination URLs

Block literal private IPs when registering a `DestinationUrl` to prevent the Worker from being used as a proxy into the internal network.

**Blocked ranges:**

| Range | Reason |
|-------|--------|
| `127.0.0.0/8` | IPv4 loopback |
| `::1/128` | IPv6 loopback |
| `10.0.0.0/8` | RFC 1918 |
| `172.16.0.0/12` | RFC 1918 |
| `192.168.0.0/16` | RFC 1918 |
| `169.254.0.0/16` | Link-local / AWS EC2 metadata |
| `fc00::/7` | IPv6 ULA |
| `fe80::/10` | IPv6 link-local |

- Validation in `DestinationUrl.ValidateUrl` using `IPAddress.TryParse` — literal IPs in the host only; no DNS lookup
- **MVP limitation:** hostnames such as `https://internal-service/` are not blocked (DNS rebinding out of scope)

---

### Queue purge per Tenant

Endpoint for Admins to cancel all `Pending` / `WaitingRetry` events for their Tenant in a single command.

- `POST /api/v1/events/purge` — Admin role only
- Purge via RabbitMQ Management API filtering by `tenant_id` in the message payload
- Events updated to `Cancelled` status in a single transaction
- Irreversible — requires explicit confirmation in the frontend

---

### SSE — real-time update stream

Single multiplexed SSE endpoint per Tenant. The dashboard subscribes once and receives all state changes — no polling anywhere.

- `GET /api/v1/stream` — single connection scoped per Tenant via JWT
- Each message carries a typed `event:` field; clients filter by type:

| Event type | Triggered when |
|------------|----------------|
| `event.created` | New event ingested |
| `event.updated` | Event status changes (`Pending → Succeeded / CriticalFailure / WaitingRetry / …`) |
| `destination.created` | New destination registered |
| `destination.updated` | Destination edited or Circuit Breaker state changes |
| `destination.deleted` | Destination removed |
| `sender.created` | New sender added to a destination |
| `sender.deleted` | Sender removed |
| `apikey.created` | New API key created |
| `apikey.deleted` | API key revoked |
| `user.created` | New user added to the Tenant |
| `user.updated` | User role or details changed |
| `user.deleted` | User removed |
| `tenant.updated` | Tenant settings changed (name, `max_trys`, `circuit_breaker_timer`, etc.) |
| `notification.created` | New notification dispatched to the Tenant — drives the bell badge |

- Implemented via `IAsyncEnumerable` + `Response.Body` with `Content-Type: text/event-stream`
- Keepalive via SSE comment (`: ping`) every 30s to prevent proxy timeouts

---

### In-app notification system

Persistent, per-Tenant notifications for events that are global and require user attention. Separate from the SSE data-sync stream — notifications survive reconnection and stay unread until dismissed.

**Triggers:**

| Trigger | Notification |
|---------|-------------|
| Circuit Breaker opens on a destination | "Circuit breaker opened for *{destination name}*" |
| Circuit Breaker closes / recovers | "Circuit breaker recovered for *{destination name}*" |
| Ingest token rotated (destination or sender) | "Ingest token rotated for *{destination/sender name}*" |
| Webhook secret rotated | "Webhook secret was rotated" |
| API key revoked | "API key *{label}* was revoked" |
| Event reaches `CriticalFailure` | "Delivery permanently failed for event *{id}*" |
| Queue purged | "*{n}* events were cancelled by *{user email}*" |

**Persistence & read state:**

- `Notification` entity: `id`, `tenant_id`, `type`, `title`, `body`, `created_at`, `read_at` (null = unread)
- Notifications are Tenant-wide (visible to all users of the Tenant)
- Delivered in real time via the existing `notification.created` SSE event
- Endpoints:
  - `GET /api/v1/notifications` — paginated list, filterable by `read` status
  - `POST /api/v1/notifications/{id}/read` — mark single notification as read
  - `POST /api/v1/notifications/read-all` — mark all as read

---

## Backlog

| Feature | Motivation |
|---------|------------|
| Per-message jitter | With multiple Workers, retries can cluster at the same instant; real per-message jitter spreads the load |
| Redis-coordinated rate limiting | In-process `SemaphoreSlim` does not coordinate across instances; replace with distributed lock for horizontal scaling |
| DNS rebinding protection | Complete SSRF coverage for hostnames beyond literal IPs |
| Aggregated metrics endpoint | Success rate, average attempts and volume per period for the dashboard — currently computed via N parallel queries on the frontend |
| Critical failure notifications | Trigger a webhook or email when an event reaches `CriticalFailure` |
| SDKs | Ease onboarding: Node.js, Python, Go |
| Protocols beyond HTTPS | gRPC and WebSocket out of scope for now |
