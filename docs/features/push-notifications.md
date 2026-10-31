# Push Notifications — Feature Overview

Web Push notifications for SplitDuo: the browser receives a notification even when the
app is closed, with no third-party service in the middle. The backend talks to the
browser vendor's push service (FCM, Mozilla autopush, Apple) using **VAPID** keys it
generates itself.

This document explains the feature in plain terms: who the actors are, how a message
travels from a backend event to a notification on a phone, how a user turns it on, and
where the security boundary sits. Operator-facing setup steps live in
[`../self-hosting.md`](../self-hosting.md); manual test recipes live in
[`push-notifications-testing.md`](push-notifications-testing.md).

---

## 1. The one-paragraph version

A browser that "subscribes" to push creates a **subscription**: a URL at the browser
vendor's push service plus two encryption keys. The frontend sends that subscription to
our API, which stores it. When something happens (someone joins your group, a recurring
expense is generated), the API builds a small JSON payload, encrypts it with the
subscription's keys, signs the request with our **VAPID private key**, and POSTs it to
the subscription URL. The push service wakes the browser, the browser wakes our
**service worker**, and the service worker shows the notification. The API never needs
to reach the user's device directly, and the push service cannot read the payload.

---

## 2. Actors

| Actor | Who it is | What it does here |
|---|---|---|
| **Operator** | The person running the instance | Generates VAPID keys, sets `SD_VAPID_*` env vars, restarts |
| **End user** | A logged-in SplitDuo user | Grants notification permission, toggles push on, manages devices |
| **SplitDuo API** | `SplitDuo.Api` + `SplitDuo.Core` | Stores subscriptions, decides when to notify, encrypts and sends |
| **Background jobs** | Quartz (`RecurringExpenseGenerationJob`) | Triggers notifications without a user request |
| **Database** | PostgreSQL, table `push_subscriptions` | One row per device per user; tracks failures |
| **Browser** | Chrome/Firefox/Safari tab or installed PWA | Holds the subscription, enforces the permission prompt |
| **Service worker** | `sd-frontend/app/service-worker/sw.ts` | Receives `push`, shows the notification, handles clicks, caches the app shell |
| **Push service** | FCM / Mozilla autopush / Apple Push | Vendor endpoint that stores and delivers the encrypted message |

```mermaid
flowchart LR
    op["Operator"] -->|"sets SD_VAPID_* env"| api
    user["End user"] -->|"permission + toggle"| web
    web["SplitDuo web app<br/>(Nuxt SPA + usePush)"] -->|"POST /api/v1/push/subscriptions"| api["SplitDuo API"]
    api -->|"reads/writes"| db[("push_subscriptions")]
    job["Quartz job"] -->|"member added /<br/>recurring expense"| api
    api -->|"encrypted POST<br/>+ VAPID signature"| ps["Push service<br/>(FCM / Mozilla / Apple)"]
    ps -->|"wakes"| sw["Service worker<br/>(sw.ts)"]
    sw -->|"showNotification()"| os["OS notification tray"]
    os -->|"user taps"| web
```

---

## 3. Operator setup

Push is **optional**. With no keys the backend still runs: it resolves
`NoopPushNotificationService`, sends nothing, and the API answers
`503 PUSH_NOT_CONFIGURED` on the key endpoint so the UI can show "disabled by the
administrator" instead of a broken toggle.

```mermaid
flowchart TD
    start(["Deploy / upgrade"]) --> keys{"Want push?"}
    keys -->|"No"| noop["Leave SD_VAPID_* unset<br/>NoopPushNotificationService<br/>UI shows 'not configured'"]
    keys -->|"Yes"| gen["npx web-push generate-vapid-keys"]
    gen --> env["Set SD_VAPID_PUBLIC_KEY<br/>SD_VAPID_PRIVATE_KEY<br/>SD_VAPID_SUBJECT (optional, mailto:)"]
    env --> restart["Restart the container<br/>(env is read at startup)"]
    restart --> check{"GET /api/v1/push/vapid-public-key<br/>with a bearer token"}
    check -->|"200 + publicKey"| ready["Push enabled"]
    check -->|"503 PUSH_NOT_CONFIGURED"| noop
```

Notes that bite operators:

- **Generate once, keep forever.** Rotating the VAPID pair invalidates every existing
  subscription: browsers reject the re-signed messages, sends fail with a *non-410*
  error, and rows are purged only after 5 failed sends each. Rotating means asking every
  user to toggle push off and on again.
- **`SD_VAPID_SUBJECT`** must be a `mailto:` or URL — push services use it to contact
  you about abusive traffic. Defaults to `mailto:admin@splitduo.local`.
- **Startup tells you the state.** Exactly one log line at boot: `Web push enabled`
  (with the subject), `Web push disabled` (no keys), or `Web push misconfigured` (keys
  present but the public key is not a 65-byte base64url P-256 point). A malformed key is
  treated as unconfigured rather than half-enabled, so the UI explains the feature is off.
- **HTTPS is required by the browser**, not by us. Push needs a secure context, so
  `http://192.168.1.10:3000` will not work; `http://localhost` is the one exception.
- The key endpoint is authenticated (`[Authorize]`), so a bare `curl` returns **401**;
  a correctly disabled server returns **503** only once a token is sent.

---

## 4. End-user setup (opt-in)

Three gates must all be open before a notification can arrive: the **browser** supports
push, the **user** grants permission, and the **server** has keys configured.

```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant App as Nuxt app (usePush)
    participant SW as Service worker
    participant API as SplitDuo API
    participant PS as Push service

    U->>App: Opens app / creates first group
    App->>API: GET /push/vapid-public-key
    alt keys missing
        API-->>App: 503 PUSH_NOT_CONFIGURED
        App-->>U: Toggle disabled — "disabled by the administrator"
    else keys present
        API-->>App: 200 { publicKey }
        App-->>U: PushOptInModal ("Want push notifications?")
        U->>App: Enable
        App->>App: Notification.requestPermission()  (must be a user gesture)
        alt permission granted
            App->>SW: wait for activation (navigator.serviceWorker.ready)
            App->>PS: pushManager.subscribe({ applicationServerKey: publicKey })
            PS-->>App: subscription { endpoint, keys.p256dh, keys.auth }
            App->>API: POST /push/subscriptions
            API-->>App: 200 (row stored, failure_count = 0)
            App-->>U: "Notifications enabled"
        else denied / dismissed
            App-->>U: Error toast, or modal closed for later
        end
    end
```

Where the prompt appears today:

- **`app.vue` on mount** (authenticated only): initialises state and silently heals a
  drifted subscription (`resubscribeIfNeeded` — idempotent upsert, no toasts).
- **After creating a group** (`groups/add.vue`): contextual modal, still inside the user
  gesture chain, so the browser permission prompt is allowed.
- The dismissal flag is `localStorage['push-prompt-dismissed']`. "Not now" sets it
  (never ask again); closing with X does not (ask next time). The modal never appears
  when permission is already granted or a subscription already exists.
- **iOS:** push only works from **16.4+** and only after *Add to Home Screen*. In a
  Safari tab the settings section shows an install hint instead of a broken toggle.

---

## 5. From event to notification

Two backend events currently notify. Both build the same payload shape
`{ title, body, url, tag }`; the body text is localised from the **recipient's**
`uiLanguage` (en/it), not the caller's.

```mermaid
sequenceDiagram
    autonumber
    participant T as Trigger
    participant API as PushNotificationService
    participant DB as push_subscriptions
    participant PS as Push service
    participant SW as Service worker
    actor U as User

    alt someone joins a group
        T->>API: InvitationsService: member accepted
        Note over API: recipients = active members<br/>minus the new member and the inviter
    else recurring expense generated
        T->>API: RecurringExpenseGenerationJob
        Note over API: only for periods >= today<br/>(backfill runs stay silent)
    end

    loop per recipient user
        API->>DB: load that user's subscriptions (device rows)
        Note over API: build body in the user's language,<br/>encrypt per subscription (aes128gcm),<br/>sign with VAPID private key
        API->>PS: POST endpoint (parallel, 10s timeout each)
        PS-->>API: 201 success | 404/410 gone | 5xx / timeout
    end
    Note over API: outcomes applied to rows sequentially:<br/>success → reset failures, touch last_used_at<br/>410/404 → delete row<br/>failure → count, delete at 5
    PS-->>SW: wakes the browser (even if the app is closed)
    SW->>SW: push event → parse payload
    SW->>U: showNotification(title, body, icon, tag)
    U->>SW: tap
    SW->>SW: notificationclick → focus a window + postMessage NAVIGATE<br/>(or openWindow when none exists)
    SW-->>U: SPA navigates to /groups/{guid}
```

Why it works with the app closed: the **service worker** is registered at app load and
stays dormant until the push service wakes it. It needs no tab open — but it does need
the browser to be running and the OS to allow notifications.

The frontend half is plain workbox: precache hashed build assets, `NetworkFirst` for
the SPA shell (so a deploy never pins a stale `index.html`), `NetworkOnly` for `/api/*`,
plus no-cache headers on `sw.js`, `manifest.webmanifest` and `index.html` so a new
service worker is picked up immediately. Updates are prompt-based: a waiting SW is told
`SKIP_WAITING` from `PwaUpdate.vue`.

---

## 6. Subscription lifecycle

Every device gets its own row. The row is the unit that fails, gets purged, or gets
re-registered.

```mermaid
stateDiagram-v2
    [*] --> NotSupported: browser lacks Push API
    [*] --> NeverAsked: supported, no subscription
    NeverAsked --> Granted: user enables (permission prompt)
    NeverAsked --> Dismissed: "Not now" (localStorage flag)
    NeverAsked --> Denied: permission denied
    Granted --> Subscribed: POST /subscriptions (row created)
    Subscribed --> Subscribed: resubscribeIfNeeded()<br/>idempotent upsert on mount
    Subscribed --> Subscribed: send succeeds (failures reset)
    Subscribed --> Failing: send fails (5xx / timeout)
    Failing --> Subscribed: later send succeeds
    Failing --> Purged: failure_count reaches 5
    Subscribed --> Purged: push service answers 404/410
    Subscribed --> Unsubscribed: user toggles off (row deleted)
    Subscribed --> Unsubscribed: user removes device from the list
    Purged --> Subscribed: user toggles off/on again
    Unsubscribed --> Granted: user toggles on
    Denied --> [*]
    Dismissed --> Granted: user enables from Profile → Notifications
```

Two healing paths exist because subscriptions drift: browsers rotate keys, clear site
data, or the user reinstalls the PWA.

1. **App mount** (`resubscribeIfNeeded`) — the reliable one: if permission is granted
   and a browser subscription exists, upsert it. Never auto-subscribes.
2. **`pushsubscriptionchange` in the service worker** — best-effort: resubscribes and
   tries to POST, but the SW has no bearer token, so that POST usually 401s. The mount
   path is what actually repairs the row. (Known limitation, documented in `sw.ts`.)

---

## 7. Setup from the user's side, in one screen

The **Profile → Notifications** section is the single place to see and control the
feature after opt-in:

| Control | Meaning |
|---|---|
| Toggle | On = this browser/device is subscribed. Off = browser unsubscribes and the row is deleted |
| Device list | Every subscribed device for the account, removable one by one (e.g. an old phone) |
| "Not configured" alert | Server has no VAPID keys — nothing the user can do |
| "Unsupported" alert | Browser has no Push API (or a non-secure origin) |
| iOS hint | Safari tab on iOS: add to Home Screen first |
| "Test" path | No test button by design; trigger a real event (join a group, let a recurring expense run) |

---

## 8. Security boundary

The trust boundary is the **subscription endpoint**: a value the client chooses and the
server later makes an outbound request to.

```mermaid
flowchart TD
    subgraph untrusted["Client-controlled (untrusted)"]
        ep["endpoint URL<br/>+ keys.p256dh / keys.auth<br/>+ userAgent (rendered as a device name)"]
    end
    subgraph trusted["SplitDuo backend"]
        val["Upsert validation<br/>(absolute https URL + per-user cap)"]
        store[("push_subscriptions")]
        guard["PushHttpClient<br/>no redirects, public addresses only"]
        send["PushNotificationService<br/>encrypt + sign + POST"]
    end
    subgraph network["Where the POST lands"]
        pub["Public push service<br/>(FCM / Mozilla / Apple)"]
        priv["Internal service<br/>10.x / 127.0.0.1 / 169.254.x"]
    end

    ep --> val --> store --> send
    send --> guard
    guard -->|"intended"| pub
    guard -.->|"refused"| priv
```

What is enforced now:

- **Owner scoping.** Deleting or removing a device only matches rows owned by the
  caller; registering an endpoint that belongs to another user returns `409`.
- **No secrets in logs.** Only a 40-character endpoint prefix is ever logged; VAPID keys
  never appear in a response or a log line.
- **Endpoint scheme.** Only absolute `https` URLs are accepted, rejected at persistence
  time with `400`. Push services never issue anything else.
- **Redirects are not followed.** The `WebPush` client is given a shared `HttpClient`
  whose handler sets `AllowAutoRedirect = false`, so a public HTTPS endpoint cannot 302
  the request onward. A 3xx comes back as a failed send and counts toward the purge.
- **Only public addresses are dialled.** `PushHttpClient` resolves the host itself and
  connects to the exact address it checked, refusing loopback, RFC 1918, CGNAT
  (`100.64/10`, Tailscale), link-local (`169.254/16`, cloud metadata), multicast and
  IPv6 unique-local/link-local — including IPv4-compatible, NAT64 and 6to4 forms that
  embed a private IPv4 address. Resolution happens per connection, so a DNS answer
  cannot be pinned to an internal host after the check.
- **Per-user device cap.** Ten subscriptions per account; a new device beyond that gets
  `400`, while re-registering an endpoint the user already owns still works. Each row is
  one outbound request per event, so the cap bounds the fan-out as well as the table.
- **Dead-row hygiene.** `404`/`410` deletes the row immediately; other failures count
  and delete at 5, so a hostile or broken endpoint does not live forever.
- **Auth on every endpoint**, including the VAPID public key.

The SSRF gap is closed at two layers, and each covers the other's bypass: the
registration check rejects non-`https` endpoints, and the transport guard catches what a
scheme cannot express (a private IP in `https` form, a public name resolving internally,
a redirect to an internal target). This is why the guard lives at connect time rather
than only in the request DTO — validation at write time cannot bind the address that is
eventually dialled.

What remains out of scope is not SSRF: a *genuinely public* HTTPS host that the user
controls and that proxies onward is simply a reachable third party, and TLS certificate
validation still applies to it. The connection guard only refuses addresses that a remote
user could not have reached themselves.

---

## 9. Failure behaviour at a glance

| Situation | What the user sees | What the system does |
|---|---|---|
| No `SD_VAPID_*` keys | Toggle disabled, "disabled by the administrator" | `NoopPushNotificationService`, 503 on key endpoint, nothing sent |
| Browser unsupported / insecure origin | "Unsupported" alert | No subscription attempt |
| Permission denied | Error toast | Nothing stored |
| Server row lost, browser still subscribed | Nothing (silent) | `resubscribeIfNeeded` recreates the row on next mount |
| Browser subscription gone | Stale row, one failed send | Deleted on `404/410`, or after 5 failures |
| Push service 5xx / slow | Nothing | Failure counted, retried on the next event |
| VAPID keys rotated | No notification arrives | Non-410 → failure counted → purged after 5; user must re-enable |
| Offline at click time | Notification still shows | `openWindow` on tap fails until connectivity returns |
| Eleventh device on one account | Error toast from the toggle | `400`, nothing stored; existing devices unaffected |
| Malformed VAPID key on startup | Toggle disabled, "disabled by the administrator" | One startup warning naming the fix; push treated as unconfigured (503), nothing sent |
