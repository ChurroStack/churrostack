# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
pnpm dev          # Start Vite dev server (port 5173)
pnpm run build    # Type-check + build (tsc -b && vite build)
pnpm run lint     # ESLint
pnpm run format   # Prettier
pnpm run preview  # Preview production build locally
```

Requires Node >=20 and pnpm@10.27.0. No test framework is set up.

## Environment Setup

Copy `.env.development` to `.env` and configure the OIDC provider:

```
VITE_OIDC_USE_MOCK=true           # Set true for local dev without Keycloak
VITE_OIDC_ISSUER_URI=https://localhost:8001
VITE_OIDC_CLIENT_ID=app
VITE_OIDC_AUDIENCE=api
VITE_OIDC_SCOPES=offline_access api/.default
VITE_API_BASE_URL=                # Optional; defaults to root path
```

The backend API is expected at `/api/*` (proxied by Nginx to `localhost:8080` in production).

## Architecture Overview

ChurroStack UI is a React 19 + TypeScript SPA for managing cloud application deployments ("Deploy apps like churros"). Key tech: Vite, React Router 7, Tailwind CSS 4, Radix UI/shadcn, TanStack Table, Recharts, React Hook Form + Zod, SignalR, OIDC-SPA.

### Data Fetching: Custom Hooks in `src/hooks/data/`

Instead of TanStack Query, the app uses custom hooks built on top of the OIDC token. All hooks auto-inject the Bearer token.

`src/hooks/data/core.tsx` exports the primitives:
- `useGet<T>(path)` → `{ isFetching, isSuccess, isError, data, fetchAsync, reset }`
- `usePost<T>(path)`, `usePut<T>`, `usePatch<T>`, `useDelete` → same shape with mutation methods
- `useStreamingSse<T>(path)` → AbortController-based SSE for streaming logs/events

Domain-specific hooks (e.g. `applications.tsx`, `environments.tsx`) wrap these primitives and export typed helpers.

### Context/Service Providers in `src/services/`

Each major domain has a service provider (e.g. `EnvironmentServiceProvider`) that wraps pages to share fetched state and reload logic. Providers subscribe to real-time SignalR events to trigger re-fetches. Wrap relevant route segments with these providers.

### Real-time: SignalR + SSE

- `src/services/notification-service.tsx`: SignalR hub at `/api/notifications`, uses EventEmitter3 for in-app pub/sub. Filters by target type (application, environment, deployment, apiKey).
- SSE via `useStreamingSse` for live console output and event streams.

### Page Structure in `src/pages/`

Pages are organized by domain. Each domain typically has:
- `index.tsx` — list/gallery view
- `[entity].tsx` — detail view
- `panels/` — sub-pages within the detail view (console, logs, monitor, settings)
- `dialogs/` — modal dialogs for create/edit/delete actions
- `menus/` — context menus

### Layout & Scrolling Architecture

`html, body, #root` have `h-full overflow-hidden` (in `src/index.css`), so **no page-level scrolling exists**. Each content area must manage its own overflow.

**MenuLayout** (`src/layouts/menu-layout.tsx`) wraps all section pages (applications, environments, llms, keys, settings). Structure:

    AppSidebar (searchable sidebar)
    SidebarInset (h-screen flex flex-col)
      └─ header (sticky top-0, breadcrumb bar)
      └─ div.flex.min-h-0.flex-1.overflow-auto  ← scrollable content area
           └─ <Outlet /> (routed content)

**Home page** (`src/pages/home/home.tsx`) does NOT use MenuLayout/SidebarInset. It renders `AppSidebar` directly alongside its content div. The content div needs `h-screen overflow-y-auto` to scroll (use `h-screen` not `h-full`, because the parent `SidebarProvider` uses `min-h-svh` which doesn't constrain height).

**Searchable sidebar pattern** — Each section index page (`pages/*/index.tsx`) uses `MenuLayout` with search state:
- `useState` + `useDebounce(500ms)` for search input
- Pass `searchValue`/`onSearchValueChange` to `MenuLayout`
- `useEffect` calls `reload(queryString)` on debounced value changes
- Sidebar list items are `<Link>` elements with `hover:bg-sidebar-accent` styling

**Tabbed detail pages** — Entity detail views (`application.tsx`, `environment.tsx`, `llm.tsx`) fill remaining height with tabs:

    div.flex.flex-col.min-h-0.w-full.h-full       ← page root
      ├─ header row (title, status, action buttons)
      ├─ Separator
      └─ div.flex.flex-col.min-h-0.w-full.h-full   ← tabs wrapper
           └─ Tabs.flex.flex-col.min-h-0.w-full.h-full
                ├─ TabsList (tab triggers)
                └─ TabsContent.flex.flex-col.min-h-0.w-full.h-full
                     └─ Panel component

`min-h-0` at every flex level is **critical** — without it, flex children won't shrink below content size and `overflow-auto` won't activate.

**Panel components** (`pages/*/panels/*.tsx`) — Each tab panel follows:

    div.overflow-hidden.rounded-md.border.flex.flex-col.min-h-0.w-full.h-full
      ├─ header bar (description + action buttons)
      ├─ error alerts (conditional)
      └─ div.flex.flex-col.h-full.overflow-auto  ← scrollable content
           └─ form fields, tables, editors, etc.

Do NOT use `min-h-*` on the scrollable content div — it prevents the container from shrinking and blocks scroll activation.

### Routing in `src/main.tsx`

React Router 7 nested routes. Layout components (e.g. `EnvironmentLayout`) act as context providers and wrap child routes via `<Outlet />`.

### UI Components in `src/components/ui/`

40+ Radix UI wrappers following the shadcn/ui pattern. All styled with Tailwind 4 utility classes. Theme tokens (colors, sidebar, charts) are CSS custom properties in `src/index.css`. Dark mode via `next-themes`.

### Reusable Component Patterns

- `src/components/data-table.tsx`: Generic TanStack Table wrapper used across all list pages.
- `src/pickers/`: Modal picker components (application, environment, template, identity, size) — used within dialogs for selecting related entities.
- `src/extensions.tsx`: Shared utility functions (date formatting, icon helpers, etc).

## Deployment

Multi-stage Docker build: Node 20 compile → Nginx alpine serve. Runtime env vars injected via `entrypoint.sh` (envsubst). The `nginx.conf` handles SPA fallback, API proxy, WebSocket/SSE passthrough (no buffering), and GZIP compression.
