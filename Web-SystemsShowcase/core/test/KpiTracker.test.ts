// progress/max, completion threshold crossing, load/restore by name.

import { describe, expect, it } from "vitest";
import { KpiTracker } from "../src/kpis/KpiTracker.js";

describe("KpiTracker", () => {
  it("completes when progress reaches max, firing the event once", () => {
    const tracker = new KpiTracker();
    const kpi = tracker.define("ONBOARDED_10", "10 Clients Onboarded", "Reach 10 onboardings", 10);

    const completed: string[] = [];
    tracker.events.on("kpiCompleted", ({ kpi }) => completed.push(kpi.name));

    kpi.setProgress(5);
    expect(kpi.completed).toBe(false);
    kpi.setProgress(10);
    expect(kpi.completed).toBe(true);
    kpi.setProgress(10); // already complete - no re-fire
    expect(completed).toEqual(["ONBOARDED_10"]);
  });

  it("complete() jumps straight to max", () => {
    const tracker = new KpiTracker();
    const kpi = tracker.define("FIRST_DEAL", "First Deal", "Close a deal", 3);
    kpi.complete();
    expect(kpi.progress).toBe(3);
    expect(kpi.completed).toBe(true);
  });

  it("load() restores completed KPIs by name", () => {
    const tracker = new KpiTracker();
    const a = tracker.define("A", "A", "a", 5);
    const b = tracker.define("B", "B", "b", 5);
    tracker.load(["A"]);
    expect(a.completed).toBe(true);
    expect(b.completed).toBe(false);
    expect(tracker.getCompletedNames()).toEqual(["A"]);
  });
});
