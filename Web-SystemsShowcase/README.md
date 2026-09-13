# Web Systems Showcase

A few TypeScript systems I wrote for tracking work: jobs with milestones, an asset store with a credit balance, validated config settings, and KPI tracking. There's a shared core package, an Express API on top of it with a live event stream (SSE), and React + Angular dashboards that talk to it.

## Layout

```
core/         the systems, no framework deps (@showcase/core)
node-api/     Express REST API + SSE event stream
react-demo/   React dashboard (Vite)
angular-demo/ Angular dashboard, same views through an injectable service
```

## Run it

Node 20+.

```bash
npm install
cd angular-demo && npm install && cd ..

npm test             # vitest specs for the core systems
npm run dev:api      # API on http://localhost:3000
npm run dev:react    # React dashboard on http://localhost:5173
cd angular-demo && npx ng serve   # Angular dashboard on http://localhost:4200
```

## Try it

```bash
curl -X POST http://localhost:3000/api/jobs/client-onboarding/give
curl -X PATCH http://localhost:3000/api/context -H "Content-Type: application/json" -d "{\"contractSigned\": true}"
curl -X POST http://localhost:3000/api/jobs/tick
curl -N http://localhost:3000/api/events   # live event feed
```

## API

- `GET /api/jobs`, `POST /api/jobs/:id/give`, `POST /api/jobs/tick`, `POST /api/jobs/fail-reset`, `POST /api/jobs/:id/force-complete`
- `GET /api/assets`, `GET /api/assets/catalog`, `POST /api/assets/:id/adjust`, `POST /api/assets/:id/purchase`
- `GET /api/budget`, `POST /api/budget/credit`, `POST /api/budget/spend`
- `GET /api/config`, `PUT /api/config/:key`
- `GET /api/kpis`, `POST /api/kpis/:name/progress`
- `GET|PATCH /api/context` -> flags the job conditions check against
- `GET /api/events` -> SSE stream of everything happening

## Notes

- milestone progress is edge-triggered: it fires once when a condition flips false->true, so a flag staying true doesn't double-count
- config values get clamped to their min/max and unchanged writes are skipped
- every system emits events; the API merges them into one SSE stream
