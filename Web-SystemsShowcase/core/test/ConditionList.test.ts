// ConditionList AND/OR evaluation, inversion, empty-list semantics,
// and provider-first capture ordering.

import { describe, expect, it } from "vitest";
import {
  ConditionBase,
  ConditionList,
  FuncCondition,
  type IConditionTargetProvider,
} from "../src/conditions/ConditionList.js";

const flag = (ctx: Record<string, boolean>, key: string) =>
  new FuncCondition((t) => (t as Record<string, boolean>)[key] === true, key);

describe("ConditionList", () => {
  it("returns false when empty", () => {
    expect(new ConditionList().evaluate()).toBe(false);
  });

  it("AND mode: all conditions must pass", () => {
    const list = new ConditionList();
    const ctx = { a: true, b: false };
    list.conditions = [flag(ctx, "a"), flag(ctx, "b")];
    list.initialize(ctx);
    expect(list.evaluate()).toBe(false);

    ctx.b = true;
    expect(list.evaluate()).toBe(true);
  });

  it("OR mode: any single true condition passes", () => {
    const list = new ConditionList();
    list.useOrMode = true;
    const ctx = { a: false, b: true };
    list.conditions = [flag(ctx, "a"), flag(ctx, "b")];
    list.initialize(ctx);
    expect(list.evaluate()).toBe(true);
  });

  it("inverts a condition when invertCondition is set", () => {
    const list = new ConditionList();
    const ctx = { a: false };
    const c = flag(ctx, "a");
    c.invertCondition = true;
    list.conditions = [c];
    list.initialize(ctx);
    expect(list.evaluate()).toBe(true);
  });

  it("evaluates target providers first so captures are visible", () => {
    // Providers run before consumers regardless of list order.
    class Provider extends ConditionBase implements IConditionTargetProvider {
      readonly providesTarget = true;
      capturedOwner: unknown = null;
      capturedReceiver: unknown = null;
      evaluate(): boolean {
        this.capturedOwner = { name: "owner-1" };
        return true;
      }
      getDescription(): string {
        return "provider";
      }
    }
    class Consumer extends ConditionBase {
      seen: unknown = null;
      evaluate(): boolean {
        this.seen = this.parentList?.capturedOwner;
        return this.seen != null;
      }
      getDescription(): string {
        return "consumer";
      }
    }

    const list = new ConditionList();
    // Consumer listed BEFORE provider - two-pass ordering must still work.
    const consumer = new Consumer();
    list.conditions = [consumer, new Provider()];
    list.initialize({});
    expect(list.evaluate()).toBe(true);
    expect(consumer.seen).toEqual({ name: "owner-1" });
  });

  it("getDescription joins with AND/OR", () => {
    const list = new ConditionList();
    list.conditions = [flag({}, "a"), flag({}, "b")];
    expect(list.getDescription()).toBe("a AND b");
    list.useOrMode = true;
    expect(list.getDescription()).toBe("a OR b");
  });
});
