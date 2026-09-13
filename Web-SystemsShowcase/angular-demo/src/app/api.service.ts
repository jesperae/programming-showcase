// ApiService - single injectable service holding all dashboard state as
// signals, with HttpClient for transport and SSE for live domain events.

import { HttpClient } from "@angular/common/http";
import { Injectable, NgZone, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";

const API = "http://localhost:3000/api";

export interface MilestoneDto {
  label: string;
  display: string;
  progress: number;
  required: number;
  isCompleted: boolean;
  isFailed: boolean;
  completionPercentage: number;
}

export interface JobDto {
  id: string;
  name: string;
  description: string;
  status: "available" | "active" | "completed";
  creditReward: number;
  milestones: MilestoneDto[];
}

export interface AssetDto {
  id: string;
  displayName: string;
  stacks: number;
  unitCost: number;
}

export interface ConfigDto {
  key: string;
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
  progress: number;
  progressMax: number;
  completed: boolean;
}

export interface DomainEventDto {
  type: string;
  detail: unknown;
  at: string;
}

@Injectable({ providedIn: "root" })
export class ApiService {
  // State as signals.
  readonly jobs = signal<JobDto[]>([]);
  readonly assets = signal<AssetDto[]>([]);
  readonly credits = signal(0);
  readonly config = signal<ConfigDto[]>([]);
  readonly kpis = signal<KpiDto[]>([]);
  readonly context = signal<Record<string, boolean>>({});
  readonly events = signal<DomainEventDto[]>([]);
  readonly error = signal<string | null>(null);

  private eventSource: EventSource | null = null;

  constructor(
    private readonly http: HttpClient,
    private readonly zone: NgZone,
  ) { }

  async refresh(): Promise<void> {
    try {
      const [jobs, assets, budget, config, kpis, context] = await Promise.all([
        firstValueFrom(this.http.get<JobDto[]>(`${API}/jobs`)),
        firstValueFrom(this.http.get<AssetDto[]>(`${API}/assets`)),
        firstValueFrom(this.http.get<{ credits: number }>(`${API}/budget`)),
        firstValueFrom(this.http.get<ConfigDto[]>(`${API}/config`)),
        firstValueFrom(this.http.get<KpiDto[]>(`${API}/kpis`)),
        firstValueFrom(this.http.get<Record<string, boolean>>(`${API}/context`)),
      ]);
      this.jobs.set(jobs);
      this.assets.set(assets);
      this.credits.set(budget.credits);
      this.config.set(config);
      this.kpis.set(kpis);
      this.context.set(context);
      this.error.set(null);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : String(e));
    }
  }

  // Subscribe to the SSE domain-event stream. EventSource callbacks run
  // outside Angular - re-enter via NgZone so signals update the view.
  connectEvents(): void {
    if (this.eventSource) return;
    this.eventSource = new EventSource(`${API}/events`);
    this.eventSource.onmessage = (msg) => {
      const e = JSON.parse(msg.data) as DomainEventDto;
      if (e.type === "connected") return;
      this.zone.run(() => {
        this.events.update((prev) => [e, ...prev].slice(0, 30));
        void this.refresh();
      });
    };
  }

  // --- actions ---

  giveJob = (id: string) => this.post(`/jobs/${id}/give`);
  tick = () => this.post(`/jobs/tick`);
  failReset = () => this.post(`/jobs/fail-reset`);
  forceComplete = (id: string) => this.post(`/jobs/${id}/force-complete`);
  adjustAsset = (id: string, amount: number) => this.post(`/assets/${id}/adjust`, { amount });
  purchaseAsset = (id: string) => this.post(`/assets/${id}/purchase`, { amount: 1 });
  credit = (amount: number) => this.post(`/budget/credit`, { amount });
  spend = (amount: number) => this.post(`/budget/spend`, { amount });
  setConfig = (key: string, value: number) =>
    this.send("PUT", `/config/${key}`, { value });
  patchContext = (patch: Record<string, boolean>) =>
    this.send("PATCH", `/context`, patch);

  private async post(path: string, body?: unknown): Promise<void> {
    await this.send("POST", path, body);
  }

  private async send(method: "POST" | "PUT" | "PATCH", path: string, body?: unknown): Promise<void> {
    try {
      await firstValueFrom(this.http.request(method, `${API}${path}`, { body }));
      await this.refresh();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : String(e));
    }
  }
}
