// give/remove/has, stack merging, insufficient-funds spend, and change events.

import { describe, expect, it } from "vitest";
import { AssetStore, type AssetDef } from "../src/assets/AssetStore.js";

const laptop: AssetDef = { id: "laptop", displayName: "Laptop", stackable: true, unitCost: 1200 };
const license: AssetDef = { id: "license", displayName: "Site License", stackable: false, unitCost: 500 };

function makeStore(): AssetStore {
  const store = new AssetStore();
  store.registerAsset(laptop);
  store.registerAsset(license);
  return store;
}

describe("AssetStore", () => {
  it("credit/spend tracks the balance and refuses insufficient funds", () => {
    const store = makeStore();
    store.credit(2000);
    expect(store.credits).toBe(2000);
    expect(store.spend(1500)).toBe(true);
    expect(store.credits).toBe(500);
    expect(store.spend(9999)).toBe(false); // insufficient funds
    expect(store.credits).toBe(500);
  });

  it("stackable assets merge into one stack", () => {
    const store = makeStore();
    store.giveAsset("laptop", 2);
    store.giveAsset("laptop", 3);
    expect(store.stackCount("laptop")).toBe(5);
    expect(store.assets.length).toBe(1);
  });

  it("non-stackable assets can't be owned twice", () => {
    const store = makeStore();
    expect(store.giveAsset("license")).toBe(true);
    expect(store.giveAsset("license")).toBe(false);
  });

  it("removeAsset decrements and deletes empty stacks", () => {
    const store = makeStore();
    store.giveAsset("laptop", 2);
    expect(store.removeAsset("laptop", 1)).toBe(true);
    expect(store.stackCount("laptop")).toBe(1);
    expect(store.removeAsset("laptop", 5)).toBe(false); // not enough held
    expect(store.removeAsset("laptop", 1)).toBe(true);
    expect(store.hasAsset("laptop")).toBe(false);
  });

  it("purchase is atomic - refunds when the grant fails", () => {
    const store = makeStore();
    store.credit(1000);
    store.giveAsset("license"); // already owned, non-stackable
    expect(store.purchase("license")).toBe(false);
    expect(store.credits).toBe(1000); // refunded
  });

  it("emits assetAdded / assetsChanged / creditsChanged", () => {
    const store = makeStore();
    const seen: string[] = [];
    store.events.on("assetAdded", () => seen.push("added"));
    store.events.on("assetsChanged", () => seen.push("changed"));
    store.events.on("creditsChanged", () => seen.push("credits"));

    store.credit(100);
    store.giveAsset("laptop");
    expect(seen).toEqual(["credits", "added", "changed"]);
  });
});
