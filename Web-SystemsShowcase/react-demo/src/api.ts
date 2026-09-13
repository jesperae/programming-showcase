// Typed fetch wrapper for the showcase REST API (node-api on :3000).

const API = "http://localhost:3000/api";

export interface MilestoneDto {
  label: string;
  display: string;
  progress: number;
  required: number;
  isCompleted: boolean;
  isFailed: boolean;
  completionPercentage: number;
  progressDescription: string;
  failDescription: string | null;
}

export interface JobDto {
  id: string;
  name: string;
  description: string;
  status: "available" | "active" | "completed";
  creditReward: number;
  itemRewards: string[];
  allMilestonesCompleted: boolean;
  milestones: MilestoneDto[];
}

export interface AssetDto {
  id: string;
  displayName: string;
  stacks: number;
  unitCost: number;
  stackable: boolean;
}

export interface ConfigDto {
  key: string;
  category: string;
  displayName: string;
  value: number;
  min: number;
  max: number;
  isBool: boolean;
  isPercentage: boolean;
  valueText: string | null;
  optionList: string[] | null;
}

export interface KpiDto {
  name: string;
  displayName: string;
  description: string;
  progress: number;
  progressMax: number;
  completed: boolean;
}

export interface DomainEventDto {
  type: string;
  detail: unknown;
  at: string;
}

async function req<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API}${path}`, {
    headers: { "Content-Type": "application/json" },
    ...init,
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body.error ?? `${res.status} ${res.statusText}`);
  }
  return res.json() as Promise<T>;
}

const post = <T>(path: string, body?: unknown) =>
  req<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });

export const api = {
  jobs: () => req<JobDto[]>("/jobs"),
  giveJob: (id: string) => post<JobDto>(`/jobs/${id}/give`),
  tick: () => post<JobDto[]>("/jobs/tick"),
  failReset: () => post<JobDto[]>("/jobs/fail-reset"),
  forceComplete: (id: string) => post<JobDto>(`/jobs/${id}/force-complete`),

  assets: () => req<AssetDto[]>("/assets"),
  adjustAsset: (id: string, amount: number) =>
    post<{ id: string; stacks: number }>(`/assets/${id}/adjust`, { amount }),
  purchaseAsset: (id: string, amount = 1) =>
    post<{ id: string; stacks: number; credits: number }>(`/assets/${id}/purchase`, { amount }),

  budget: () => req<{ credits: number }>("/budget"),
  credit: (amount: number) => post<{ credits: number }>("/budget/credit", { amount }),
  spend: (amount: number) => post<{ credits: number }>("/budget/spend", { amount }),

  config: () => req<ConfigDto[]>("/config"),
  setConfig: (key: string, value: number) =>
    req<ConfigDto>(`/config/${key}`, { method: "PUT", body: JSON.stringify({ value }) }),

  kpis: () => req<KpiDto[]>("/kpis"),

  context: () => req<Record<string, boolean>>("/context"),
  patchContext: (patch: Record<string, boolean>) =>
    req<Record<string, boolean>>("/context", { method: "PATCH", body: JSON.stringify(patch) }),
};
