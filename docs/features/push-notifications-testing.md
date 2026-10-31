# Testing PWA Push Notifications

End-to-end manual test guide for the push notifications feature (issue #28, MR !33).
Covers the full chain: VAPID config → subscription → trigger → WebPush delivery → service worker → notification click.

## Prerequisites

- Chrome desktop (two profiles, or Chrome + Firefox/Edge, for the two-user scenarios)
- Docker running (for PostgreSQL)

## 1. Generate VAPID keys (one-time)

Push is disabled unless both keys are set. Generate once and keep them forever — rotating the keys invalidates all existing subscriptions.

```bash
npx web-push generate-vapid-keys
```

(Any VAPID key generator works — the keys are just a base64url P-256 keypair.)

## 2. Configure and start services

```bash
export SD_VAPID_PUBLIC_KEY="<your public key>"
export SD_VAPID_PRIVATE_KEY="<your private key>"

./scripts/dev.sh        # PostgreSQL (5432) + Mailpit (1025/8025)
```

```bash
# terminal 1 — backend (migration auto-applies on startup)
cd sd-backend && dotnet run --project SplitDuo.Api    # :8080

# terminal 2 — frontend
cd sd-frontend && pnpm dev                           # :3000
```

> In production-like Docker: set the same env vars in `docker-compose.yml` (see the commented VAPID placeholders) and `docker compose up -d --build`.

## 3. Sanity checks

- `curl -s http://localhost:8080/health` → OK
- Open `http://localhost:3000` in Chrome, DevTools → **Application → Service Workers** → the SW should register and activate (reload once if it's "waiting"). The update prompt (`PwaUpdate`) must still work as before.

## 4. Subscribe (user A)

1. Log in (`admin@splitduo.local` / `changeme123` by default).
2. Go to **Profile** → the new **Notifications** section sits between Preferences and Security.
3. Flip the toggle → Chrome permission prompt → **Allow**.
4. The device list shows one row: parsed device name (e.g. "Chrome on Linux"), registration date, and a **This device** badge.

Verify it reached the backend:

```bash
docker exec splitduo-db psql -U splitduo -d splitduo -c \
  "SELECT id, user_id, left(endpoint,40) AS endpoint, failure_count FROM push_subscriptions;"
```

(Or DevTools → Application → Background Services → **Push Messaging**.)

## 5. Test vector A — recurring expense materialization (single user, easiest)

This exercises the entire chain: Quartz job → backfill guard → WebPush encryption → SW push handler → `showNotification` → click → deep link.

1. Create a recurring expense template with **anchor date = today** (any recurrence) — so today's period materializes.
2. The generation job runs every **5 minutes** — wait for the next cycle.
3. The notification appears: title = group name, body = `Recurring expense "<title>" was added` (localized per your profile language: `en`/`it`).
4. Click it → the app opens/focuses and navigates to `/groups/{guid}`.

Backfill guard: templates anchored in the past generate historical periods too — those must **not** produce notifications (only `periodDate >= today` fires).

## 6. Test vector B — group member added / invitation accepted (two users)

1. In browser 2 (second profile): create/invite a second user (invite emails land in Mailpit at `http://localhost:8025`).
2. In browser 2: Profile → Notifications → toggle ON (each browser profile prompts for permission separately).
3. In browser 1 (subscribed): add user B to a group → when B accepts:
   - User B's browser: `"<A's first name> joined the group"`
   - User A (the inviter): `"<B's first name> accepted your invitation to the group"`

## 7. Negative paths

| Check | Steps | Expected |
|---|---|---|
| Device removal | Profile → Notifications → trash icon on a row | Row disappears; `push_subscriptions` empty |
| Re-subscribe (B1 fix) | Toggle OFF → toggle ON again | Row reappears — no error (this exact sequence was a fixed deadlock) |
| Disabled mode | Stop backend, `unset SD_VAPID_PUBLIC_KEY SD_VAPID_PRIVATE_KEY`, restart | Toggle disabled with "not configured" message; authenticated `GET /api/v1/push/vapid-public-key` → 503 `PUSH_NOT_CONFIGURED` (the endpoint is `[Authorize]`, so a bare `curl` returns 401 first — send the bearer token) |
| Unsupported browser | e.g. Safari non-installed / private window | Toggle disabled with "unsupported" message |
| Opt-in modal dismissal | Clear `localStorage['push-prompt-dismissed']`, create a group | Modal appears after creation; **Not now** → never again; **X-close** → reappears on next creation (by design) |

## 8. Cache headers (Docker / production path)

```bash
docker compose up -d --build
curl -sI http://localhost:8080/sw.js | grep -i cache-control                 # no-cache
curl -sI http://localhost:8080/manifest.webmanifest | grep -i cache-control   # no-cache
curl -sI http://localhost:8080/ | grep -i cache-control                       # no-cache
```

Other assets (`_nuxt/*.js` etc.) keep default caching.

## 9. iOS (Home Screen install)

Push on iOS requires the PWA to be **added to the Home Screen** (iOS 16.4+) and the permission prompt to be triggered by a user gesture (the Enable button / toggle satisfies this). The profile Notifications section shows the install guidance callout when running in a Safari tab on iOS. Testing requires a real device — not covered by the desktop flow above.

## Troubleshooting

- **No notification after 5 min (recurring vector)**: check backend logs for `Recurring expense generation` errors; confirm the subscription row exists (step 4); check the job actually materialized an expense (today's instance created).
- **503 on toggle**: VAPID keys not set in the backend process environment.
- **SW not registering**: DevTools → Application → Service Workers → `sw.js` must be `activated`; unregister + reload. `Cache-Control: no-cache` on `sw.js` only applies in the Docker deployment path.
- **Permission stuck on "denied"**: Chrome remembers denials per origin — clear via site settings (chrome://settings/content/notifications) or use a fresh profile.