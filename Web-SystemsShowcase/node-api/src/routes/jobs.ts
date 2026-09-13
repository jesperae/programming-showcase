// Job routes - REST surface over JobTracker.

import { Router } from "express";
import { jobs } from "../state.js";
import { serializeJob } from "../serialize.js";

export const jobsRouter = Router();

// GET /api/jobs - all jobs with status + milestone progress
jobsRouter.get("/", (_req, res) => {
  res.json(jobs.allJobs.map(serializeJob));
});

// GET /api/jobs/:id - single job detail
jobsRouter.get("/:id", (req, res) => {
  const job = jobs.getJobByName(req.params.id);
  if (!job) return res.status(404).json({ error: `job '${req.params.id}' not found` });
  res.json(serializeJob(job));
});

// POST /api/jobs/:id/give - activate a job
jobsRouter.post("/:id/give", (req, res) => {
  const job = jobs.getJobByName(req.params.id);
  if (!job) return res.status(404).json({ error: `job '${req.params.id}' not found` });
  if (!jobs.giveJob(job)) {
    return res.status(409).json({ error: "job already active or completed" });
  }
  res.json(serializeJob(job));
});

// POST /api/jobs/tick - evaluate all active jobs once
jobsRouter.post("/tick", (_req, res) => {
  jobs.tick();
  res.json(jobs.allJobs.map(serializeJob));
});

// POST /api/jobs/fail-reset - reset all failed milestones
jobsRouter.post("/fail-reset", (_req, res) => {
  jobs.resetAllFailures();
  res.json(jobs.allJobs.map(serializeJob));
});

// POST /api/jobs/:id/force-complete - skip conditions
jobsRouter.post("/:id/force-complete", (req, res) => {
  const job = jobs.getJobByName(req.params.id);
  if (!job) return res.status(404).json({ error: `job '${req.params.id}' not found` });
  jobs.forceCompleteJob(job);
  res.json(serializeJob(job));
});
