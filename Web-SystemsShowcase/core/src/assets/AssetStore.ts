// Asset and budget tracking. Holds a catalog of asset definitions, the assets
// currently owned (with stack counts), and a credit balance. All mutations
// emit events.

import { EventBus } from "../events/EventBus.js";

// Definition of a purchasable/trackable asset.
export interface AssetDef {
  id: string;
  displayName: string;
  stackable: boolean;
  unitCost: number;
}

// An owned asset: definition + quantity.
export interface AssetStack {
  def: AssetDef;
  stacks: number;
}

export interface AssetEvents {
  assetAdded: { def: AssetDef; amount: number };
  assetRemoved: { def: AssetDef; amount: number };
  assetsChanged: null;
  creditsChanged: { credits: number; delta: number };
}

export class AssetStore {
  readonly events = new EventBus<AssetEvents>();

  // Catalog of known asset definitions.
  private readonly catalog = new Map<string, AssetDef>();
  private readonly _assets: AssetStack[] = [];
  private _credits = 0;

  get assets(): readonly AssetStack[] {
    return this._assets;
  }
  get credits(): number {
    return this._credits;
  }
  // All registered definitions.
  get catalogDefs(): readonly AssetDef[] {
    return [...this.catalog.values()];
  }

  // --- Catalog ---

  registerAsset(def: AssetDef): void {
    this.catalog.set(def.id, def);
  }

  getAssetDef(id: string): AssetDef | null {
    return this.catalog.get(id) ?? null;
  }

  // --- Credits ---

  credit(amount: number): void {
    this._credits += amount;
    this.events.emit("creditsChanged", { credits: this._credits, delta: amount });
  }

  // Returns false when funds are insufficient.
  spend(amount: number): boolean {
    if (this._credits < amount) return false;
    this._credits -= amount;
    this.events.emit("creditsChanged", { credits: this._credits, delta: -amount });
    return true;
  }

  // --- Assets ---

  hasAsset(id: string): boolean {
    return this._assets.some((a) => a.def.id === id);
  }

  getStack(id: string): AssetStack | null {
    return this._assets.find((a) => a.def.id === id) ?? null;
  }

  stackCount(id: string): number {
    return this.getStack(id)?.stacks ?? 0;
  }

  // Grant stock - stacks merge when the def is stackable.
  giveAsset(id: string, amount = 1): boolean {
    const def = this.catalog.get(id);
    if (!def) return false;

    const existing = this.getStack(id);
    if (existing && def.stackable) {
      existing.stacks += amount;
    } else if (existing && !def.stackable && amount === 1) {
      return false; // non-stackable: already owned
    } else {
      this._assets.push({ def, stacks: amount });
    }

    this.events.emit("assetAdded", { def, amount });
    this.events.emit("assetsChanged", null);
    return true;
  }

  // Remove stock - returns false when the asset/quantity isn't held.
  removeAsset(id: string, amount = 1): boolean {
    const stack = this.getStack(id);
    if (!stack || stack.stacks < amount) return false;

    stack.stacks -= amount;
    if (stack.stacks <= 0) this._assets.splice(this._assets.indexOf(stack), 1);

    this.events.emit("assetRemoved", { def: stack.def, amount });
    this.events.emit("assetsChanged", null);
    return true;
  }

  // Spend credits then grant the asset.
  // Refunds on failure so the operation stays atomic.
  purchase(id: string, amount = 1): boolean {
    const def = this.catalog.get(id);
    if (!def) return false;
    const cost = def.unitCost * amount;
    if (!this.spend(cost)) return false;
    if (!this.giveAsset(id, amount)) {
      this.credit(cost);
      return false;
    }
    return true;
  }
}
