# Product Requirements Document (PRD): SplitDuo PWA

## 1. Objective

Enable a native-like installation experience on mobile devices (iOS/Android) for the SplitDuo application to improve user retention and accessibility without the overhead of native app development.

## 2. Technical Stack

- **Frontend:** Nuxt 4 (SPA mode), NuxtUI 4.
- **PWA Engine:** `@vite-pwa/nuxt`.
- **Host:** .NET 10 Kestrel (serving static files from `wwwroot`).
- **Deployment:** Single Docker container (multi-stage build).

## 3. Core Requirements

### 3.1 Web App Manifest

The application must provide a `manifest.webmanifest` that defines:

- **Identity:** Name ("SplitDuo"), Short Name ("Split").
- **Display:** `standalone` mode to remove browser UI (URL bar/navigation).
- **Orientation:** `portrait` (omit from manifest — `portrait-primary` is non-standard; browsers ignore it).
- **Theme Integration:** `theme_color` must match the NuxtUI primary brand color — teal-500 (`#14b8a6`). `background_color` should be `#ffffff`.

### 3.2 Service Worker Strategy

- **Installation:** Automatic background registration.
- **Caching:** \* **Assets:** Pre-cache JS, CSS, and NuxtUI icons for immediate shell loading.
  - **Data:** `NetworkOnly` strategy for all `/api/*` calls. Offline data entry is explicitly out of scope; the app will require an active connection for financial transactions.
- **Updates:** Implement a "Prompt to Update" flow. When a new Service Worker is detected, the UI must notify the user to refresh.

### 3.3 .NET Hosting Requirements

- **MIME Mapping:** Kestrel must recognize `.webmanifest` as `application/manifest+json`.
- **SPA Fallback:** All non-file/non-API requests must serve `index.html` to support deep-linked routes like `/groups/[id]/expenses`.

---

## Technical Implementation

> **Note:** `sd-frontend/public/site.webmanifest` was a manually created stub — deleted once `@vite-pwa/nuxt` was added, since the plugin generates and manages the manifest automatically.

> **Note:** Maskable and all other PWA icons are generated from the SVG source — see [Regenerating PWA Icons](#regenerating-pwa-icons) below.

### 1. Nuxt Configuration

Ensure your `nuxt.config.ts` matches your branding and the Nuxt 4 directory structure.

```typescript
export default defineNuxtConfig({
  ssr: false,
  modules: ["@vite-pwa/nuxt", "@nuxt/ui"],

  pwa: {
    registerType: "prompt", // Required for the manual FAB update alert
    manifest: {
      name: "SplitDuo",
      short_name: "Split",
      description: "Expense splitting app for small groups — couples, housemates, travel companions, or anyone sharing costs.",
      theme_color: "#14b8a6", // teal-500 — matches NuxtUI primary
      background_color: "#ffffff",
      display: "standalone",
      orientation: "portrait",
      icons: [
        {
          src: "pwa-64x64.png",
          sizes: "64x64",
          type: "image/png",
        },
        {
          src: "pwa-192x192.png",
          sizes: "192x192",
          type: "image/png",
        },
        {
          src: "pwa-512x512.png",
          sizes: "512x512",
          type: "image/png",
        },
        {
          src: "maskable-icon-512x512.png",
          sizes: "512x512",
          type: "image/png",
          purpose: "maskable",
        },
      ],
    },
    workbox: {
      navigateFallback: "/index.html",
      globPatterns: ["**/*.{js,css,html,png,svg,ico}"],
      // Ensure API calls are never cached
      runtimeCaching: [
        {
          urlPattern: /^\/api\/.*$/,
          handler: "NetworkOnly",
        },
      ],
    },
  },
});
```

### 2. Update Notification Component

Place this in `app/components/PwaUpdate.vue`. It uses NuxtUI 4 components to provide a non-intrusive update trigger. No TypeScript — plain JS per project convention.

Add `<PwaUpdate />` to `app/app.vue` (or `app/layouts/default.vue`) so it renders globally.

```vue
<script setup>
const { $pwa } = useNuxtApp();

const updateApp = async () => {
  await $pwa?.updateServiceWorker(true);
};
</script>

<template>
  <ClientOnly>
    <div v-if="$pwa?.needRefresh" class="fixed bottom-6 right-6 z-50">
      <UButton
        icon="i-heroicons-arrow-path-20-solid"
        color="primary"
        size="lg"
        class="rounded-full shadow-lg"
        label="Update Available"
        @click="updateApp"
      />
    </div>
  </ClientOnly>
</template>
```

### 3. Kestrel Configuration (`ApiProgramExtensions.cs`)

Add the `.webmanifest` MIME type to `UseStaticFiles()` in `ConfigureServices`. `MapFallbackToFile("index.html")` is already present.

```csharp
// In ConfigureServices (ApiProgramExtensions.cs)
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".webmanifest"] = "application/manifest+json";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

// Already present — no change needed:
app.MapFallbackToFile("index.html");
```

### 4. Docker Multi-Stage Build

Already implemented in `Dockerfile`. The pattern uses pnpm and copies `.output/public` into `wwwroot`:

```dockerfile
# Stage 1: Build Nuxt SPA
FROM node:22-alpine AS frontend-build
RUN npm install -g pnpm@latest-10
WORKDIR /app/frontend
COPY sd-frontend/package.json sd-frontend/pnpm-lock.yaml sd-frontend/.npmrc ./
RUN pnpm install --frozen-lockfile
COPY sd-frontend/ ./
RUN pnpm generate

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
WORKDIR /app/backend
COPY sd-backend/ ./
RUN dotnet publish SplitDuo.Api/SplitDuo.Api.csproj -c Release -o /app/publish

# Stage 3: Final Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /app/frontend/.output/public ./wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "SplitDuo.Api.dll"]
```

---

## Deployment and Verification

1. **SSL Requirement:** PWAs require HTTPS. Ensure your Docker container sits behind a reverse proxy (like Nginx or Caddy) with a valid certificate.
2. **Manifest Validation:** Test via Chrome DevTools (Application > Manifest) to ensure all icons are resolved.
3. **Deep Linking:** Open the installed app and navigate to a nested route. Refresh the app to ensure Kestrel serves the fallback `index.html` and Vue Router takes over.

---

## Regenerating PWA Icons

PWA icons (favicons, apple-touch-icon, maskable, manifest icons) are generated from a single SVG source via [`@vite-pwa/assets-generator`](https://github.com/vite-pwa/assets-generator) — the asset generator from the same org as `@vite-pwa/nuxt`. No web upload, no third-party server; reproducible locally and in CI.

### Source

- **SVG:** `sd-frontend/public/logo.svg` — the SplitDuo S₂ mark on a teal squircle. This is the only file to edit when the logo changes.
- **Config:** `sd-frontend/pwa-assets.config.ts` — uses the `minimal-2023` preset (transparent 64/192/512, maskable 512 with 0.3 safe-zone padding, apple 180, `favicon.ico`).

### Generate

```bash
cd sd-frontend
pnpm gen:pwa-assets
```

This regenerates all icon files in `public/`:

| File | Purpose |
| --- | --- |
| `pwa-64x64.png` | Manifest icon (small) |
| `pwa-192x192.png` | Manifest icon (standard) |
| `pwa-512x512.png` | Manifest icon (large) |
| `maskable-icon-512x512.png` | Maskable icon (adaptive Android icon) |
| `apple-touch-icon-180x180.png` | iOS home screen icon |
| `favicon.ico` | Browser tab favicon |

### Manifest entries

The generator outputs files only — it does **not** write manifest entries. The `icons` array in `nuxt.config.ts` (`pwa.manifest.icons`) is maintained manually and must match the generated filenames above. Only touch it if you change the preset's output names.

### CI note

`sharp` (the image backend) requires its native build script. pnpm blocks build scripts by default — if CI runs `gen:pwa-assets`, ensure `sharp` is in the approved-builds list (`pnpm approve-builds sharp` or the equivalent config in `package.json`).

### Changing the logo

1. Edit `sd-frontend/public/logo.svg`.
2. Run `pnpm gen:pwa-assets`.
3. Commit the updated `logo.svg` + regenerated files in `public/`.
