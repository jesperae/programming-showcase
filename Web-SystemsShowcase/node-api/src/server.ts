// Express server exposing the @showcase/core systems as a REST API.
// Run: npm run dev -w @showcase/node-api   (from the Web-SystemsShowcase root)

import express from "express";
import { assetsRouter, budgetRouter } from "./routes/assets.js";
import { configRouter } from "./routes/config.js";
import { jobsRouter } from "./routes/jobs.js";
import { kpisRouter } from "./routes/kpis.js";
import { context, domainEvents } from "./state.js";

const app = express();
const PORT = Number(process.env.PORT ?? 3000);

app.use(express.json());

// Minimal CORS for the Vite (5173) and Angular (4200) dev servers.
app.use((req, res, next) => {
  res.header("Access-Control-Allow-Origin", "*");
  res.header("Access-Control-Allow-Methods", "GET,POST,PUT,PATCH,DELETE,OPTIONS");
  res.header("Access-Control-Allow-Headers", "Content-Type");
  if (req.method === "OPTIONS") return res.sendStatus(204);
  next();
});

app.get("/api/health", (_req, res) => res.json({ ok: true }));

app.use("/api/jobs", jobsRouter);
app.use("/api/assets", assetsRouter);
app.use("/api/budget", budgetRouter);
app.use("/api/config", configRouter);
app.use("/api/kpis", kpisRouter);

// PATCH /api/context { flag: boolean, ... } - drive the demo context that job
// conditions evaluate against.
app.patch("/api/context", (req, res) => {
  for (const [key, value] of Object.entries(req.body ?? {})) {
    if (key in context && typeof value === "boolean") {
      context[key] = value;
    }
  }
  res.json(context);
});

app.get("/api/context", (_req, res) => res.json(context));

// GET /api/events - Server-Sent Events stream of all domain events.
app.get("/api/events", (req, res) => {
  res.writeHead(200, {
    "Content-Type": "text/event-stream",
    "Cache-Control": "no-cache",
    Connection: "keep-alive",
  });
  res.write(`data: ${JSON.stringify({ type: "connected", at: new Date().toISOString() })}\n\n`);

  const unsubscribe = domainEvents.on("domainEvent", (e) => {
    res.write(`data: ${JSON.stringify(e)}\n\n`);
  });
  req.on("close", unsubscribe);
});

app.listen(PORT, () => {
  console.log(`[showcase-api] listening on http://localhost:${PORT}`);
  console.log(`[showcase-api] SSE stream: http://localhost:${PORT}/api/events`);
});
