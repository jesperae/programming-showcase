// Typed, validated configuration settings. Each setting has a definition
// (range, type, labels), a cached value backed by a pluggable key-value
// store, and an optional side effect that runs when the value changes.

import { EventBus } from "../events/EventBus.js";

export type ConfigCategory = "audio" | "display" | "gameplay" | "difficulty" | "language" | string;

export interface ConfigSettingDef {
  key: string;
  category: ConfigCategory;
  displayName: string;
  defaultValue: number;
  min: number;
  max: number;
  increment: number;
  isPercentage: boolean;
  isBool: boolean;
  valueTexts?: string[]; // label per index
  optionList?: string[]; // enum-style options
}

// Minimal persistence contract - back it with a JSON file, localStorage,
// or memory.
export interface KeyValueStore {
  load<T>(key: string, defaultValue: T): T;
  save<T>(key: string, value: T): void;
}

export class InMemoryStore implements KeyValueStore {
  private readonly data = new Map<string, unknown>();
  load<T>(key: string, defaultValue: T): T {
    return (this.data.has(key) ? this.data.get(key) : defaultValue) as T;
  }
  save<T>(key: string, value: T): void {
    this.data.set(key, value);
  }
}

export interface ConfigEvents {
  configChanged: { key: string; value: number; displayName: string };
}

// Runtime wrapper around a ConfigSettingDef - cached get/set with
// clamping and side effects.
export class ConfigSetting {
  private cached: number | null = null;

  constructor(
    public readonly def: ConfigSettingDef,
    private readonly store: KeyValueStore,
    private readonly events: EventBus<ConfigEvents>,
    private readonly sideEffect?: (setting: ConfigSetting, value: number) => void,
  ) { }

  get saveKey(): string {
    return `Option_${this.def.key}`;
  }

  // Cached read-through to the store.
  get(): number {
    if (this.cached !== null) return this.cached;
    const value = this.store.load<number>(this.saveKey, this.def.defaultValue);
    this.cached = value;
    return value;
  }

  // Clamp (non-bool only), skip unchanged, persist, run side effects.
  set(value: number): void {
    if (!this.def.isBool) {
      value = Math.min(this.def.max, Math.max(this.def.min, value));
    }

    if (this.cached !== null && this.cached === value) return;
    this.cached = value;

    this.store.save(this.saveKey, value);
    this.events.emit("configChanged", {
      key: this.def.key,
      value,
      displayName: this.def.displayName,
    });
    this.sideEffect?.(this, value);
  }

  getBool(): boolean {
    return this.get() >= 1;
  }
  setBool(v: boolean): void {
    this.set(v ? 1 : 0);
  }
  getInt(): number {
    return Math.round(this.get());
  }

  clearCache(): void {
    this.cached = null;
  }

  // Invoke the side effect directly - used by applyAllSideEffects, which
  // must not go through set()'s skip-unchanged guard.
  applySideEffect(): void {
    this.sideEffect?.(this, this.get());
  }

  // Label for an index into valueTexts.
  getValueText(value: number): string | null {
    const index = Math.round(value - this.def.min);
    const texts = this.def.valueTexts;
    if (texts && index >= 0 && index < texts.length && texts[index]) return texts[index];
    return null;
  }
}

// Registry + typed accessors over ConfigSetting.
export class ConfigService {
  readonly events = new EventBus<ConfigEvents>();
  private readonly settings = new Map<string, ConfigSetting>();

  constructor(private readonly store: KeyValueStore = new InMemoryStore()) { }

  register(
    def: ConfigSettingDef,
    sideEffect?: (setting: ConfigSetting, value: number) => void,
  ): ConfigSetting {
    const setting = new ConfigSetting(def, this.store, this.events, sideEffect);
    this.settings.set(def.key, setting);
    return setting;
  }

  find(key: string): ConfigSetting | null {
    return this.settings.get(key) ?? null;
  }

  get(key: string): number {
    return this.find(key)?.get() ?? 0;
  }
  getBool(key: string): boolean {
    return this.get(key) >= 1;
  }
  getInt(key: string): number {
    return Math.round(this.get(key));
  }
  set(key: string, value: number): void {
    this.find(key)?.set(value);
  }
  setBool(key: string, value: boolean): void {
    this.set(key, value ? 1 : 0);
  }

  // Re-run every setting's side effect.
  applyAllSideEffects(): void {
    for (const s of this.settings.values()) s.applySideEffect();
  }

  clearCache(): void {
    for (const s of this.settings.values()) s.clearCache();
  }

  list(): ConfigSetting[] {
    return [...this.settings.values()];
  }
}
