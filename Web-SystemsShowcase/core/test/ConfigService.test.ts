// cached get/set, min/max clamp, bool mode, skip-unchanged, side effects,
// persistence.

import { describe, expect, it } from "vitest";
import {
  ConfigService,
  InMemoryStore,
  type ConfigSettingDef,
} from "../src/config/ConfigService.js";

const volumeDef: ConfigSettingDef = {
  key: "VolumeMaster",
  category: "audio",
  displayName: "Master Volume",
  defaultValue: 80,
  min: 0,
  max: 100,
  increment: 5,
  isPercentage: true,
  isBool: false,
};

const hardModeDef: ConfigSettingDef = {
  key: "IsHardMode",
  category: "difficulty",
  displayName: "Hard Mode",
  defaultValue: 0,
  min: 0,
  max: 1,
  increment: 1,
  isPercentage: false,
  isBool: true,
};

describe("ConfigService", () => {
  it("returns defaults before anything is set", () => {
    const svc = new ConfigService();
    svc.register(volumeDef);
    expect(svc.get("VolumeMaster")).toBe(80);
  });

  it("clamps non-bool values to min/max", () => {
    const svc = new ConfigService();
    svc.register(volumeDef);
    svc.set("VolumeMaster", 250);
    expect(svc.get("VolumeMaster")).toBe(100);
    svc.set("VolumeMaster", -50);
    expect(svc.get("VolumeMaster")).toBe(0);
  });

  it("bool settings round-trip via setBool/getBool", () => {
    const svc = new ConfigService();
    svc.register(hardModeDef);
    expect(svc.getBool("IsHardMode")).toBe(false);
    svc.setBool("IsHardMode", true);
    expect(svc.getBool("IsHardMode")).toBe(true);
  });

  it("skips unchanged values - no event, no side effect", () => {
    const svc = new ConfigService();
    let sideEffects = 0;
    svc.register(volumeDef, () => sideEffects++);
    const events: string[] = [];
    svc.events.on("configChanged", ({ key }) => events.push(key));

    svc.set("VolumeMaster", 80); // same as default -> cached read, then set
    svc.set("VolumeMaster", 80); // unchanged -> skipped
    svc.set("VolumeMaster", 90);
    expect(events).toEqual(["VolumeMaster", "VolumeMaster"]);
    expect(sideEffects).toBe(2);
  });

  it("persists through the KeyValueStore across service instances", () => {
    const store = new InMemoryStore();
    const svc1 = new ConfigService(store);
    svc1.register(volumeDef);
    svc1.set("VolumeMaster", 42);

    const svc2 = new ConfigService(store);
    svc2.register(volumeDef);
    expect(svc2.get("VolumeMaster")).toBe(42); // loaded from store
  });

  it("getValueText maps indices to labels", () => {
    const svc = new ConfigService();
    svc.register({
      ...volumeDef,
      key: "Quality",
      min: 0,
      max: 2,
      isPercentage: false,
      valueTexts: ["Low", "Medium", "High"],
    });
    const setting = svc.find("Quality")!;
    expect(setting.getValueText(2)).toBe("High");
    expect(setting.getValueText(9)).toBeNull();
  });
});
