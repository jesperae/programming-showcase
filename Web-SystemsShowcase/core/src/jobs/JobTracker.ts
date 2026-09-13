// Tracks jobs through their lifecycle: available -> active -> completed.
// Call tick() to evaluate every active job's milestone conditions once.
// Rewards are delegated to a reward handler callback so this class stays
// decoupled from AssetStore.

import { EventBus } from "../events/EventBus.js";
import { Job } from "./Job.js";
import { JobMilestone } from "./JobMilestone.js";

export interface JobEvents {
  jobGiven: { job: Job };
  jobCompleted: { job: Job };
  milestoneProgress: { job: Job; milestone: JobMilestone };
  milestoneCompleted: { job: Job; milestone: JobMilestone };
  milestoneFailed: { job: Job; milestone: JobMilestone };
}

export class JobTracker {
  readonly events = new EventBus<JobEvents>();

  // All registered job definitions.
  private readonly _allJobs: Job[] = [];
  private readonly _activeJobs: Job[] = [];
  private readonly _completedJobs: Job[] = [];

  // Supplies the shared context each job's conditions initialize against.
  private _targetProvider: () => unknown = () => null;

  // Called on completion to grant rewards.
  private _rewardHandler: (job: Job) => void = () => { };

  get allJobs(): readonly Job[] {
    return this._allJobs;
  }
  get activeJobs(): readonly Job[] {
    return this._activeJobs;
  }
  get completedJobs(): readonly Job[] {
    return this._completedJobs;
  }
  get activeJobCount(): number {
    return this._activeJobs.length;
  }
  get completedJobCount(): number {
    return this._completedJobs.length;
  }
  get hasActiveJobs(): boolean {
    return this._activeJobs.length > 0;
  }

  // Registers the catalog of job definitions.
  initialize(jobs: Job[]): void {
    this._allJobs.length = 0;
    this._activeJobs.length = 0;
    this._completedJobs.length = 0;
    this._allJobs.push(...jobs);
  }

  setTargetProvider(provider: () => unknown): void {
    this._targetProvider = provider;
  }

  onReward(handler: (job: Job) => void): void {
    this._rewardHandler = handler;
  }

  // Activate a job - refuses jobs already active or completed.
  giveJob(job: Job): boolean {
    if (this.isJobActive(job) || this.isJobCompleted(job)) return false;

    this._activeJobs.push(job);
    job.initializeMilestones(this._targetProvider());
    this.events.emit("jobGiven", { job });
    return true;
  }

  // Activate without emitting jobGiven - used when restoring saved state.
  giveJobSilent(job: Job): boolean {
    if (this.isJobActive(job) || this.isJobCompleted(job)) return false;
    this._activeJobs.push(job);
    job.initializeMilestones(this._targetProvider());
    return true;
  }

  forceCompleteJob(job: Job): void {
    if (!this.isJobActive(job)) return;
    this.completeJob(job);
  }

  isJobActive(job: Job): boolean {
    return this._activeJobs.includes(job);
  }

  isJobCompleted(job: Job): boolean {
    return this._completedJobs.includes(job);
  }

  hasJob(job: Job): boolean {
    return this.isJobActive(job) || this.isJobCompleted(job);
  }

  // Matches on id or jobName.
  getJobByName(name: string): Job | null {
    return this._allJobs.find((j) => j.id === name || j.jobName === name) ?? null;
  }

  // Evaluate all active jobs once. The caller controls cadence
  // (API route, interval, queue worker, etc.).
  tick(): void {
    // Iterate backwards so completed jobs can be removed safely.
    for (let i = this._activeJobs.length - 1; i >= 0; i--) {
      this.tickJob(this._activeJobs[i]);
    }
  }

  private tickJob(job: Job): void {
    let anyMilestoneCompleted = false;

    for (const milestone of job.milestones) {
      if (milestone.isCompleted) continue;

      // CHECK FAIL
      if (milestone.tickFail()) {
        this.events.emit("milestoneFailed", { job, milestone });
      }

      // CHECK PROGRESS (only if not failed)
      if (!milestone.isFailed && milestone.tickProgress()) {
        if (milestone.isCompleted) {
          this.events.emit("milestoneCompleted", { job, milestone });
          anyMilestoneCompleted = true;
        } else {
          this.events.emit("milestoneProgress", { job, milestone });
        }
      }
    }

    // CHECK JOB COMPLETION
    if (anyMilestoneCompleted && job.allMilestonesCompleted) {
      this.completeJob(job);
    }
  }

  private completeJob(job: Job): void {
    this._activeJobs.splice(this._activeJobs.indexOf(job), 1);
    this._completedJobs.push(job);
    this.events.emit("jobCompleted", { job });

    this._rewardHandler(job);
  }

  // Reset all fail states so milestones can be retried.
  resetAllFailures(): void {
    for (const job of this._activeJobs) {
      for (const milestone of job.milestones) milestone.resetFail();
    }
  }

  // Per-milestone progress values - used for save/restore and API output.
  getMilestoneProgressList(job: Job): number[] {
    return job.milestones.map((m) => m.currentProgress);
  }
}
