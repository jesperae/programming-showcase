// A job definition: milestones to complete plus the rewards granted on
// completion.

import { JobMilestone } from "./JobMilestone.js";

export class Job {
  readonly milestones: JobMilestone[] = [];

  // Rewards granted on completion.
  creditReward = 0;
  itemRewards: string[] = [];

  constructor(
    public readonly id: string,
    public jobName: string,
    public jobDescription = "",
  ) { }

  get uniqueName(): string {
    return this.id;
  }

  get hasRewards(): boolean {
    return this.creditReward > 0 || this.itemRewards.length > 0;
  }

  get allMilestonesCompleted(): boolean {
    if (this.milestones.length === 0) return false;
    return this.milestones.every((m) => m.isCompleted);
  }

  // The shared context flows into each milestone's conditions.
  initializeMilestones(target: unknown): void {
    for (const m of this.milestones) m.initialize(target);
  }

  // Full milestone list as bullet-point text.
  getMilestonesDisplayText(): string {
    if (this.milestones.length === 0) return "";
    if (this.milestones.length === 1) return this.milestones[0].getDisplayStringWithStatus();
    return this.milestones.map((m) => m.getDisplayStringWithStatus()).join("\n\n");
  }

  toString(): string {
    return `${this.jobName}: ${this.jobDescription}`;
  }
}
