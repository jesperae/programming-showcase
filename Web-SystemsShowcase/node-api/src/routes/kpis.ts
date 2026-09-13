// KPI routes - REST surface over KpiTracker.

import { Router } from "express";
import { kpis } from "../state.js";

export const kpisRouter = Router();

function serializeKpi(name: string) {
  const k = kpis.getByName(name);
  if (!k) return null;
  return {
    name: k.name,
    displayName: k.displayName,
    description: k.description,
    progress: k.progress,
    progressMax: k.progressMax,
    completed: k.completed,
  };
}

// GET /api/kpis - all KPIs with progress
kpisRouter.get("/", (_req, res) => {
  res.json(kpis.kpis.map((k) => serializeKpi(k.name)));
});

// POST /api/kpis/:name/progress { progress }
kpisRouter.post("/:name/progress", (req, res) => {
  const kpi = kpis.getByName(req.params.name);
  if (!kpi) return res.status(404).json({ error: `kpi '${req.params.name}' not found` });

  const progress = Number(req.body?.progress);
  if (!Number.isFinite(progress)) {
    return res.status(400).json({ error: "progress must be a number" });
  }
  kpi.setProgress(progress);
  res.json(serializeKpi(req.params.name));
});
