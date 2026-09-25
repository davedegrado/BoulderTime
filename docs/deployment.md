# Deploying BoulderTime

Architecture: **Cloudflare Pages** (frontend) → **Railway** (ASP.NET Core API, EU region) → **Supabase** (Frankfurt:
PostgreSQL, Auth, Storage). Domain `bouldertime.com` on Cloudflare; the API lives at `api.bouldertime.com`.

Secrets never go in the repository: they are entered in the Railway and Cloudflare dashboards only.

## 1. Supabase (once)

1. **SQL Editor** → run, in order: migrations are applied by Railway's pre-deploy step (or once from a terminal with
   `ConnectionStrings__Database` set), then paste and run `database/supabase/hardening.sql`, then `storage.sql`.
   `psql` works too if you have it; the SQL Editor needs nothing installed.
2. **Authentication → URL Configuration**: Site URL `https://bouldertime.com`; Redirect URLs `https://bouldertime.com/**`.
3. **Authentication → Emails / SMTP**: configure a real sender (e.g. Resend) before inviting users — the built-in sender
   is rate-limited and meant for testing. Add a DMARC record (`_dmarc` TXT `v=DMARC1; p=none;`), keep Resend's click and
   open tracking off, and paste the Italian templates from `docs/email-templates/`.
4. **Authentication → Providers → Google** (optional): client ID and secret from Google Cloud; authorised redirect URI
   `https://<project-ref>.supabase.co/auth/v1/callback`.

## 2. Railway (API)

1. New project → **Deploy from GitHub repo** → authorise the private repository. Region: **EU West**.
2. `railway.json` at the repository root tells Railway to build `backend/Dockerfile`, run migrations before each
   deploy, and health-check `/api/health`.
3. **Variables** (Settings → Variables):

| Variable | Value |
|---|---|
| `ConnectionStrings__Database` | Session pooler, key=value form: `Host=…pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=…;SSL Mode=Require;Trust Server Certificate=true;Maximum Pool Size=10` |
| `Supabase__Url` | `https://<project-ref>.supabase.co` |
| `Supabase__ServiceRoleKey` | the service-role key (**secret**, server only) |
| `Storage__Provider` | `Supabase` |
| `Cors__AllowedOrigins__0` | `https://bouldertime.com` |
| `Geocoding__Email` | a contact address (OpenStreetMap usage policy) |
| `Push__PublicKey`, `Push__PrivateKey` | web push keys, from `dotnet run --project src/BoulderTime.Api -- generate-push-keys` (private one is a **secret**) |
| `Push__Subject` | `mailto:support@bouldertime.com` |

4. **Settings → Networking → Custom domain**: `api.bouldertime.com`; add the CNAME it shows in Cloudflare DNS
   (proxy status **DNS only**, grey cloud, so Railway can issue the certificate).
5. **Usage → set a budget alert.** Billing is usage-based with no spending cap by default.

## 3. Cloudflare Workers (frontend)

Cloudflare Pages is legacy; the app is deployed as a Worker serving static assets, configured by
`frontend/wrangler.jsonc`.

1. Workers & Pages → Create → Workers → **Import a repository** → connect the repository.
2. Build: root directory (**Path**) `frontend`, build command `npm ci && npm run build`,
   deploy command `npx wrangler deploy`. `wrangler.jsonc` says where the built files are and makes every route
   load the app.
3. **Environment variables**: `VITE_SUPABASE_URL` = `https://<project-ref>.supabase.co`, `VITE_SUPABASE_ANON_KEY` =
   the anon/publishable key (public by design), `VITE_API_BASE_URL` = `https://api.bouldertime.com`,
   `VITE_MAP_TILE_URL` and `VITE_MAP_ATTRIBUTION` from your tile provider (e.g. MapTiler).
4. Custom domains: `bouldertime.com` (and `www.bouldertime.com` redirecting to it).
5. `frontend/public/_headers` adds security and caching headers; `_redirects` is kept for anything still on Pages.

## 4. First admin

Register on `https://bouldertime.com`, then from a terminal with `ConnectionStrings__Database` pointing at Supabase:

    cd backend
    dotnet run --project src/BoulderTime.Api -- grant-platform-admin you@example.com

Running `dotnet` by hand in a new Codespace: .NET 8 is installed in `~/.dotnet` by the scripts, so first run
`export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`. `scripts/dev.sh` and `apply.sh` do this themselves.

## Never on production

- `dotnet run -- seed` and `dev.sh promote`: demo data and development shortcuts (the latter refuses outside Development).
