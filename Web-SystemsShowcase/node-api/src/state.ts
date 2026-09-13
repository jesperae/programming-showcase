// Wires the @showcase/core systems together with seed data.

import {
  AssetStore,
  ConfigService,
  EventBus,
  FuncCondition,
  Job,
  JobMilestone,
  JobTracker,
  KpiTracker,
} from "@showcase/core";

// ---------------------------------------------------------------------------
// Demo context - the mutable state that job conditions evaluate against.
// Drive it via PATCH /api/context to make milestones progress/fail live.
// ---------------------------------------------------------------------------
export const context: Record<string, boolean> = {
  contractSigned: false,
  kickoffHeld: false,
  docsReceived: false,
  clientCancelled: false,
  auditDocsSubmitted: false,
  auditReviewed: false,
  deadlineMissed: false,
};

// ---------------------------------------------------------------------------
// Domain event stream - every system's EventBus forwards here; the SSE
// endpoint broadcasts it.
// ---------------------------------------------------------------------------
export interface DomainEvent {
  type: string;
  detail: unknown;
  at: string;
}
export const domainEvents = new EventBus<{ domainEvent: DomainEvent }>();
export function broadcast(type: string, detail: unknown): void {
  domainEvents.emit("domainEvent", { type, detail, at: new Date().toISOString() });
}

// ---------------------------------------------------------------------------
// Systems
// ---------------------------------------------------------------------------
export const assets = new AssetStore();
export const config = new ConfigService();
export const kpis = new KpiTracker();
export const jobs = new JobTracker();

// --- Asset catalog ---
for (const def of [
  { id: "laptop", displayName: "Laptop", stackable: true, unitCost: 1200 },
  { id: "monitor", displayName: "Monitor", stackable: true, unitCost: 350 },
  { id: "site-license", displayName: "Site License", stackable: false, unitCost: 5000 },
  { id: "server", displayName: "Rack Server", stackable: true, unitCost: 4200 },
]) {
  assets.registerAsset(def);
}
assets.credit(10000); // starting budget

// --- Config settings ---
config.register({
  key: "NotificationVolume",
  category: "audio",
  displayName: "Notification Volume",
  defaultValue: 80,
  min: 0,
  max: 100,
  increment: 5,
  isPercentage: true,
  isBool: false,
});
config.register({
  key: "AutoApproveSmallJobs",
  category: "gameplay",
  displayName: "Auto-Approve Small Jobs",
  defaultValue: 0,
  min: 0,
  max: 1,
  increment: 1,
  isPercentage: false,
  isBool: true,
});
config.register({
  key: "Region",
  category: "language",
  displayName: "Region",
  defaultValue: 0,
  min: 0,
  max: 2,
  increment: 1,
  isPercentage: false,
  isBool: false,
  optionList: ["US-East", "EU-Central", "APAC"],
});

// --- KPIs ---
const kpiJobsCompleted = kpis.define(
  "JOBS_COMPLETED_5",
  "Five Jobs Completed",
  "Complete five work orders.",
  5,
);
const kpiFirstJob = kpis.define("FIRST_JOB", "First Job Done", "Complete your first work order.");

// --- Jobs ---
function flag(name: keyof typeof context, description: string): FuncCondition {
  return new FuncCondition((t) => (t as Record<string, boolean>)[name] === true, description);
}

const onboarding = new Job("client-onboarding", "Client Onboarding", "Get the new client live.");
onboarding.creditReward = 1500;
onboarding.itemRewards = ["laptop"];
{
  const m1 = new JobMilestone();
  m1.label = "Contract signed";
  m1.progressConditions.conditions = [flag("contractSigned", "contract is signed")];
  onboarding.milestones.push(m1);

  const m2 = new JobMilestone();
  m2.label = "Kickoff meeting held";
  m2.progressConditions.conditions = [flag("kickoffHeld", "kickoff held")];
  onboarding.milestones.push(m2);

  const m3 = new JobMilestone();
  m3.label = "Documents received";
  m3.progressRequired = 2; // toggle docsReceived off/on twice to complete
  m3.progressConditions.conditions = [flag("docsReceived", "docs received")];
  m3.failConditions.conditions = [flag("clientCancelled", "client cancelled")];
  onboarding.milestones.push(m3);
}

const audit = new Job("compliance-audit", "Q3 Compliance Audit", "Finish the quarterly audit.");
audit.creditReward = 3000;
{
  const m1 = new JobMilestone();
  m1.label = "Audit docs submitted";
  m1.progressRequired = 3;
  m1.progressConditions.conditions = [flag("auditDocsSubmitted", "docs submitted")];
  m1.failConditions.conditions = [flag("deadlineMissed", "deadline missed")];
  audit.milestones.push(m1);

  const m2 = new JobMilestone();
  m2.label = "Audit reviewed";
  m2.progressConditions.conditions = [flag("auditReviewed", "audit reviewed")];
  audit.milestones.push(m2);
}

jobs.initialize([onboarding, audit]);
jobs.setTargetProvider(() => context);

// Grant rewards on job completion.
jobs.onReward((job) => {
  if (job.creditReward > 0) assets.credit(job.creditReward);
  for (const itemId of job.itemRewards) assets.giveAsset(itemId);
  kpiFirstJob.complete();
  kpiJobsCompleted.setProgress(kpiJobsCompleted.progress + 1);
});

// ---------------------------------------------------------------------------
// Forward every system event into the domain stream (for SSE + logging).
// ---------------------------------------------------------------------------
jobs.events.on("jobGiven", ({ job }) => broadcast("jobGiven", { id: job.id, name: job.jobName }));
jobs.events.on("jobCompleted", ({ job }) =>
  broadcast("jobCompleted", { id: job.id, name: job.jobName, creditReward: job.creditReward }),
);
jobs.events.on("milestoneProgress", ({ job, milestone }) =>
  broadcast("milestoneProgress", {
    job: job.id,
    milestone: milestone.getDisplayString(),
  }),
);
jobs.events.on("milestoneCompleted", ({ job, milestone }) =>
  broadcast("milestoneCompleted", { job: job.id, milestone: milestone.label }),
);
jobs.events.on("milestoneFailed", ({ job, milestone }) =>
  broadcast("milestoneFailed", { job: job.id, milestone: milestone.label }),
);
assets.events.on("creditsChanged", (e) => broadcast("creditsChanged", e));
assets.events.on("assetAdded", (e) =>
  broadcast("assetAdded", { id: e.def.id, amount: e.amount }),
);
assets.events.on("assetRemoved", (e) =>
  broadcast("assetRemoved", { id: e.def.id, amount: e.amount }),
);
config.events.on("configChanged", (e) => broadcast("configChanged", e));
kpis.events.on("kpiCompleted", ({ kpi }) =>
  broadcast("kpiCompleted", { name: kpi.name, displayName: kpi.displayName }),
);
kpis.events.on("kpiProgress", ({ kpi }) =>
  broadcast("kpiProgress", { name: kpi.name, progress: kpi.progress, max: kpi.progressMax }),
);
