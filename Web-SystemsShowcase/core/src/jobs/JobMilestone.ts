// A single trackable objective inside a job.
// Progress fires on a FALSE->TRUE edge of progressConditions; fail fires on a
// FALSE->TRUE edge of failConditions. Fail resets via resetFail().

import { ConditionList } from "../conditions/ConditionList.js";

export class JobMilestone {
  // Human-readable label (e.g. "Collect 3 signed contracts").
  label = "New Milestone";
  // How many times the progress condition must fire to complete (1 = single-step).
  progressRequired = 1;

  // When these go TRUE, progress increments by 1.
  progressConditions = new ConditionList();
  // When these go TRUE, this milestone FAILS (resets via resetFail).
  failConditions = new ConditionList();

  // Runtime state.
  private _currentProgress = 0;
  private _isFailed = false;
  private _lastProgressState = false;
  private _lastFailState = false;
  private _initialized = false;

  get currentProgress(): number {
    return this._currentProgress;
  }
  set currentProgress(value: number) {
    this._currentProgress = value;
  }

  get isFailed(): boolean {
    return this._isFailed;
  }
  set isFailed(value: boolean) {
    this._isFailed = value;
  }

  get isCompleted(): boolean {
    return this._currentProgress >= this.progressRequired;
  }

  get completionPercentage(): number {
    return this.progressRequired > 0 ? this._currentProgress / this.progressRequired : 0;
  }

  initialize(target: unknown): void {
    this.progressConditions.initialize(target);
    if (this.failConditions.conditions.length > 0) this.failConditions.initialize(target);

    this._lastProgressState = this.progressConditions.evaluate();
    this._lastFailState =
      this.failConditions.conditions.length > 0 && this.failConditions.evaluate();
    this._initialized = true;
  }

  // Returns true if progress was made this tick (FALSE->TRUE edge detection).
  tickProgress(): boolean {
    if (!this._initialized || this.isCompleted || this._isFailed) return false;

    const currentState = this.progressConditions.evaluate();
    let progressed = false;

    if (currentState && !this._lastProgressState) {
      this._currentProgress++;
      progressed = true;
    }

    this._lastProgressState = currentState;
    return progressed;
  }

  // Returns true if a fail was triggered this tick (FALSE->TRUE edge detection).
  tickFail(): boolean {
    if (!this._initialized || this.isCompleted || this.failConditions.conditions.length === 0)
      return false;

    const currentState = this.failConditions.evaluate();
    let failed = false;

    if (currentState && !this._lastFailState) {
      this._isFailed = true;
      failed = true;
    }

    this._lastFailState = currentState;
    return failed;
  }

  // Reset fail state so the milestone can be retried.
  resetFail(): void {
    this._isFailed = false;
    this._lastFailState = false;
  }

  // Reset all runtime state.
  reset(): void {
    this._currentProgress = 0;
    this._isFailed = false;
    this._lastProgressState = false;
    this._lastFailState = false;
  }

  // "Label" or "Label (2/5)".
  getDisplayString(): string {
    if (this.progressRequired <= 1) return this.label;
    return `${this.label} (${this._currentProgress}/${this.progressRequired})`;
  }

  getDisplayStringWithStatus(): string {
    const prefix = this.isCompleted ? "[X] " : this._isFailed ? "[!] " : "[ ] ";
    return prefix + this.getDisplayString();
  }

  toString(): string {
    return this.getDisplayString();
  }
}
