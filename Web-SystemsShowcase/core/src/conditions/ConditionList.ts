// Declarative condition evaluation. A ConditionList holds conditions that are
// checked against a shared context object - used to drive job milestone
// progress and failure.

export interface ICondition {
  myObj: unknown;
  invertCondition: boolean;
  initialize(target: unknown): void;
  evaluate(): boolean;
  getDescription(): string;
}

// Conditions implementing this capture Owner/Receiver values that other
// conditions in the same list can read.
export interface IConditionTargetProvider {
  readonly providesTarget: boolean;
  readonly capturedOwner: unknown;
  readonly capturedReceiver: unknown;
}

export abstract class ConditionBase implements ICondition {
  // Target override - when null, initialize() falls back to the master target.
  target: unknown = null;
  invertCondition = false;
  myObj: unknown = null;

  // Back-reference to the owning list, for reading captures.
  parentList: ConditionList | null = null;

  initialize(masterTarget: unknown): void {
    this.myObj = this.target ?? masterTarget;
  }

  // Subclasses may read parentList captures here.
  protected resolveDynamicTarget(): unknown {
    return this.myObj;
  }

  abstract evaluate(): boolean;
  abstract getDescription(): string;

  protected applyInversion(result: boolean): boolean {
    return this.invertCondition ? !result : result;
  }
}

// Convenience condition wrapping a plain predicate.
export class FuncCondition extends ConditionBase {
  constructor(
    private readonly predicate: (target: unknown) => boolean,
    private readonly description = "custom condition",
  ) {
    super();
  }

  evaluate(): boolean {
    return this.applyInversion(this.predicate(this.resolveDynamicTarget()));
  }

  getDescription(): string {
    return this.invertCondition ? `NOT (${this.description})` : this.description;
  }
}

// A list of conditions evaluated with AND or OR logic.
// Supports target capture: provider conditions evaluate first so their
// captures are visible to the rest of the list.
export class ConditionList {
  // If true, conditions are OR'd (any true = pass). Default is AND.
  useOrMode = false;
  conditions: ICondition[] = [];

  // Per-list captures, scoped only to this list.
  capturedOwner: unknown = null;
  capturedReceiver: unknown = null;

  initialize(masterTarget: unknown): void {
    for (const c of this.conditions) {
      c?.initialize(masterTarget);
      if (c instanceof ConditionBase) c.parentList = this;
    }
  }

  get areMet(): boolean {
    return this.evaluate();
  }

  evaluate(): boolean {
    if (this.conditions.length === 0) return false;
    this.capturedOwner = null;
    this.capturedReceiver = null;

    if (this.useOrMode) {
      for (const c of this.conditions) {
        if (c?.evaluate() === true) {
          this.capture(c);
          return true;
        }
      }
      return false;
    }

    // Two-pass: target providers first (so their captures are visible), then everything else.
    for (const c of this.conditions)
      if (ConditionList.isProvider(c) && !this.evalAndCapture(c)) return false;

    for (const c of this.conditions)
      if (!ConditionList.isProvider(c) && !this.evalAndCapture(c)) return false;

    return true;
  }

  getDescription(): string {
    if (this.conditions.length === 0) return "No conditions set";
    if (this.conditions.length === 1) return this.conditions[0].getDescription();
    const joiner = this.useOrMode ? " OR " : " AND ";
    return this.conditions.map((c) => c.getDescription()).join(joiner);
  }

  private evalAndCapture(c: ICondition): boolean {
    if (c?.evaluate() !== true) return false;
    this.capture(c);
    return true;
  }

  private static isProvider(c: ICondition): boolean {
    return (c as Partial<IConditionTargetProvider>).providesTarget === true;
  }

  private capture(c: ICondition): void {
    const p = c as Partial<IConditionTargetProvider>;
    if (p.capturedOwner != null) this.capturedOwner = p.capturedOwner;
    if (p.capturedReceiver != null) this.capturedReceiver = p.capturedReceiver;
  }
}
