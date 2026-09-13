// give/tick/complete lifecycle, edge-triggered progress, fail/reset, rewards.

import { describe, expect, it } from "vitest";
import { FuncCondition } from "../src/conditions/ConditionList.js";
import { Job } from "../src/jobs/Job.js";
import { JobMilestone } from "../src/jobs/JobMilestone.js";
import { JobTracker } from "../src/jobs/JobTracker.js";

function makeJob(ctx: Record<string, boolean>, id = "job-1"): Job {
  const job = new Job(id, "Client Onboarding", "Get the client live");
  job.creditReward = 500;

  const m = new JobMilestone();
  m.label = "Contract signed";
  m.progressRequired = 2;
  m.progressConditions.conditions = [
    new FuncCondition((t) => (t as Record<string, boolean>).contractSigned === true),
  ];
  m.failConditions.conditions = [
    new FuncCondition((t) => (t as Record<string, boolean>).clientCancelled === true),
  ];
  job.milestones.push(m);
  return job;
}

describe("JobTracker", () => {
  it("giveJob activates, initializes, and refuses duplicates", () => {
    const ctx = { contractSigned: false, clientCancelled: false };
    const tracker = new JobTracker();
    tracker.setTargetProvider(() => ctx);
    const job = makeJob(ctx);
    tracker.initialize([job]);

    const given: string[] = [];
    tracker.events.on("jobGiven", ({ job }) => given.push(job.id));

    expect(tracker.giveJob(job)).toBe(true);
    expect(tracker.isJobActive(job)).toBe(true);
    expect(tracker.giveJob(job)).toBe(false); // already active
    expect(given).toEqual(["job-1"]);
  });

  it("progress fires only on FALSE->TRUE edges (edge detection)", () => {
    const ctx = { contractSigned: false, clientCancelled: false };
    const tracker = new JobTracker();
    tracker.setTargetProvider(() => ctx);
    const job = makeJob(ctx);
    tracker.initialize([job]);
    tracker.giveJob(job);

    tracker.tick();
    expect(job.milestones[0].currentProgress).toBe(0);

    ctx.contractSigned = true;
    tracker.tick();
    expect(job.milestones[0].currentProgress).toBe(1);

    // Still true - no edge, no double-count.
    tracker.tick();
    expect(job.milestones[0].currentProgress).toBe(1);

    // FALSE->TRUE again -> second progress.
    ctx.contractSigned = false;
    tracker.tick();
    ctx.contractSigned = true;
    tracker.tick();
    expect(job.milestones[0].currentProgress).toBe(2);
    expect(job.milestones[0].isCompleted).toBe(true);
  });

  it("completes the job when all milestones complete and grants rewards", () => {
    const ctx = { contractSigned: false, clientCancelled: false };
    const tracker = new JobTracker();
    tracker.setTargetProvider(() => ctx);
    const job = makeJob(ctx);
    tracker.initialize([job]);

    const completed: string[] = [];
    const rewards: number[] = [];
    tracker.events.on("jobCompleted", ({ job }) => completed.push(job.id));
    tracker.onReward((j) => rewards.push(j.creditReward));

    tracker.giveJob(job);
    ctx.contractSigned = true;
    tracker.tick();
    ctx.contractSigned = false;
    tracker.tick();
    ctx.contractSigned = true;
    tracker.tick();

    expect(tracker.isJobCompleted(job)).toBe(true);
    expect(completed).toEqual(["job-1"]);
    expect(rewards).toEqual([500]);
  });

  it("fail conditions block progress until resetAllFailures", () => {
    const ctx = { contractSigned: false, clientCancelled: false };
    const tracker = new JobTracker();
    tracker.setTargetProvider(() => ctx);
    const job = makeJob(ctx);
    tracker.initialize([job]);
    tracker.giveJob(job);

    ctx.clientCancelled = true;
    tracker.tick();
    expect(job.milestones[0].isFailed).toBe(true);

    // Progress can't fire while failed.
    ctx.clientCancelled = false;
    ctx.contractSigned = true;
    tracker.tick();
    expect(job.milestones[0].currentProgress).toBe(0);

    tracker.resetAllFailures();
    ctx.contractSigned = false;
    tracker.tick();
    ctx.contractSigned = true;
    tracker.tick();
    expect(job.milestones[0].currentProgress).toBe(1);
  });

  it("forceCompleteJob skips conditions", () => {
    const ctx = { contractSigned: false, clientCancelled: false };
    const tracker = new JobTracker();
    tracker.setTargetProvider(() => ctx);
    const job = makeJob(ctx);
    tracker.initialize([job]);
    tracker.giveJob(job);
    tracker.forceCompleteJob(job);
    expect(tracker.isJobCompleted(job)).toBe(true);
  });
});
