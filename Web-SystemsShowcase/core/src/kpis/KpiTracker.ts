// KPI tracking. A Kpi completes when progress reaches progressMax; the
// tracker fires a completion event once per KPI and records completed names.

import { EventBus } from "../events/EventBus.js";

export class Kpi {
  progress = 0;
  progressMax = 1;

  constructor(
    public readonly name: string,
    public readonly displayName: string,
    public readonly description: string,
    private readonly tracker: KpiTracker,
  ) { }

  get completed(): boolean {
    return this.progress >= this.progressMax;
  }

  // Notifies once when crossing the completion threshold.
  setProgress(progress: number): void {
    const wasCompleted = this.completed;
    this.progress = progress;
    if (this.completed && !wasCompleted) {
      this.tracker.notifyCompleted(this);
    } else if (!this.completed) {
      this.tracker.notifyProgress(this);
    }
  }

  // Jumps to max and records completion.
  complete(): void {
    this.setProgress(this.progressMax);
  }

  toString(): string {
    return `[KPI] ${this.displayName} (${this.completed})`;
  }
}

export interface KpiEvents {
  kpiProgress: { kpi: Kpi };
  kpiCompleted: { kpi: Kpi };
}

export class KpiTracker {
  readonly events = new EventBus<KpiEvents>();
  private readonly _kpis: Kpi[] = [];
  private readonly completedNames = new Set<string>();

  get kpis(): readonly Kpi[] {
    return this._kpis;
  }

  // Define a KPI - self-registers into this tracker.
  define(name: string, displayName: string, description: string, progressMax = 1): Kpi {
    const kpi = new Kpi(name, displayName, description, this);
    kpi.progressMax = progressMax;
    this._kpis.push(kpi);
    return kpi;
  }

  getByName(name: string): Kpi | null {
    return this._kpis.find((k) => k.name === name) ?? null;
  }

  // Restore completed KPIs by name.
  load(completedNames: string[]): void {
    for (const kpi of this._kpis) {
      kpi.progress = completedNames.includes(kpi.name) ? kpi.progressMax : 0;
      if (kpi.completed) this.completedNames.add(kpi.name);
    }
  }

  // Fires the completion event and records the name.
  notifyCompleted(kpi: Kpi): void {
    this.completedNames.add(kpi.name);
    this.events.emit("kpiCompleted", { kpi });
  }

  notifyProgress(kpi: Kpi): void {
    this.events.emit("kpiProgress", { kpi });
  }

  getCompletedNames(): string[] {
    return [...this.completedNames];
  }
}
