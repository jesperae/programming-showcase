// Serializers - core objects contain functions and circular refs
// (ConditionBase.parentList), so routes map them to plain DTOs.

import type { Job, JobMilestone } from "@showcase/core";
import { jobs } from "./state.js";

export function serializeMilestone(m: JobMilestone) {
  return {
    label: m.label,
    display: m.getDisplayStringWithStatus(),
    progress: m.currentProgress,
    required: m.progressRequired,
    isCompleted: m.isCompleted,
    isFailed: m.isFailed,
    completionPercentage: m.completionPercentage,
    progressDescription: m.progressConditions.getDescription(),
    failDescription:
      m.failConditions.conditions.length > 0 ? m.failConditions.getDescription() : null,
  };
}

export function serializeJob(job: Job) {
  return {
    id: job.id,
    name: job.jobName,
    description: job.jobDescription,
    status: jobs.isJobCompleted(job)
      ? "completed"
      : jobs.isJobActive(job)
        ? "active"
        : "available",
    creditReward: job.creditReward,
    itemRewards: job.itemRewards,
    allMilestonesCompleted: job.allMilestonesCompleted,
    milestones: job.milestones.map(serializeMilestone),
  };
}
