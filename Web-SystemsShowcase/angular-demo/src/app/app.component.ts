// Dashboard component - same panels as the React demo, expressed with
// standalone components, signals, and Angular control flow (@for/@if).

import { DatePipe, JsonPipe } from "@angular/common";
import { Component, OnInit, inject } from "@angular/core";
import { ApiService } from "./api.service";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [DatePipe, JsonPipe],
  template: `
    <div class="page">
      <header>
        <h1>Systems Showcase - Angular</h1>
        <p class="sub">jobs, assets, config, KPIs, live events</p>
      </header>

      @if (api.error(); as err) {
        <div class="error">API error: {{ err }} - is node-api running on :3000?</div>
      }

      <div class="grid">
        <section class="card wide">
          <h2>Jobs</h2>
          <div class="row">
            <button (click)="api.tick()">Tick all</button>
            <button (click)="api.failReset()">Reset failures</button>
          </div>
          @for (job of api.jobs(); track job.id) {
            <div [class]="'job ' + job.status">
              <div class="row spread">
                <strong>{{ job.name }}</strong>
                <span class="badge" [class]="'badge ' + job.status">{{ job.status }}</span>
              </div>
              <p class="dim">{{ job.description }}</p>
              @for (m of job.milestones; track m.label) {
                <div class="milestone">
                  <div class="row spread">
                    <span [class.failed]="m.isFailed" [class.done]="m.isCompleted">
                      {{ m.display }}
                    </span>
                    <span class="dim">{{ m.progress }}/{{ m.required }}</span>
                  </div>
                  <div class="bar">
                    <div
                      class="fill"
                      [class.fail]="m.isFailed"
                      [style.width.%]="m.completionPercentage * 100"
                    ></div>
                  </div>
                </div>
              }
              <div class="row">
                @if (job.status === 'available') {
                  <button (click)="api.giveJob(job.id)">Give job</button>
                }
                @if (job.status === 'active') {
                  <button (click)="api.forceComplete(job.id)">Force complete</button>
                }
                @if (job.creditReward > 0) {
                  <span class="dim">reward: {{ job.creditReward }}cr</span>
                }
              </div>
            </div>
          }
        </section>

        <section class="card">
          <h2>Context flags</h2>
          <p class="dim">Drive job conditions - toggle a flag, then Tick.</p>
          @for (entry of contextEntries(); track entry[0]) {
            <label class="flag">
              <input
                type="checkbox"
                [checked]="entry[1]"
                (change)="api.patchContext({ [entry[0]]: !entry[1] })"
              />
              {{ entry[0] }}
            </label>
          }
        </section>

        <section class="card">
          <h2>Assets &amp; Budget</h2>
          <p><strong>{{ api.credits() }}cr</strong></p>
          <div class="row">
            <button (click)="api.credit(500)">+500</button>
            <button (click)="api.spend(500)">−500</button>
          </div>
          @for (a of api.assets(); track a.id) {
            <div class="row spread">
              <span>{{ a.displayName }} ×{{ a.stacks }}</span>
              <span class="row">
                <button (click)="api.adjustAsset(a.id, 1)">+</button>
                <button (click)="api.adjustAsset(a.id, -1)">−</button>
                <button (click)="api.purchaseAsset(a.id)">buy {{ a.unitCost }}cr</button>
              </span>
            </div>
          }
          @if (api.assets().length === 0) {
            <p class="dim">No assets held - buy one.</p>
          }
        </section>

        <section class="card">
          <h2>Config</h2>
          @for (c of api.config(); track c.key) {
            <div class="row spread">
              <span>{{ c.displayName }}</span>
              @if (c.isBool) {
                <input
                  type="checkbox"
                  [checked]="c.value >= 1"
                  (change)="api.setConfig(c.key, c.value >= 1 ? 0 : 1)"
                />
              } @else if (c.optionList) {
                <select [value]="c.value" (change)="onSelect(c.key, $event)">
                  @for (o of c.optionList; track o; let i = $index) {
                    <option [value]="i">{{ o }}</option>
                  }
                </select>
              } @else {
                <input
                  type="range"
                  [min]="c.min"
                  [max]="c.max"
                  [value]="c.value"
                  (change)="onRange(c.key, $event)"
                />
              }
              <span class="dim">{{ c.valueText ?? c.value }}</span>
            </div>
          }
        </section>

        <section class="card">
          <h2>KPIs</h2>
          @for (k of api.kpis(); track k.name) {
            <div class="row spread">
              <span [class.done]="k.completed">
                {{ k.completed ? "★ " : "" }}{{ k.displayName }}
              </span>
              <span class="dim">{{ k.progress }}/{{ k.progressMax }}</span>
            </div>
          }
        </section>

        <section class="card wide">
          <h2>Event feed (SSE)</h2>
          <div class="feed">
            @for (e of api.events(); track $index) {
              <div class="event">
                <span class="dim">{{ e.at | date: "HH:mm:ss" }}</span>
                <strong>{{ e.type }}</strong> <code>{{ e.detail | json }}</code>
              </div>
            }
            @if (api.events().length === 0) {
              <p class="dim">Waiting for events…</p>
            }
          </div>
        </section>
      </div>
    </div>
  `,
})
export class AppComponent implements OnInit {
  protected readonly api = inject(ApiService);

  ngOnInit(): void {
    void this.api.refresh();
    this.api.connectEvents();
  }

  protected contextEntries(): [string, boolean][] {
    return Object.entries(this.api.context());
  }

  protected onSelect(key: string, event: Event): void {
    void this.api.setConfig(key, Number((event.target as HTMLSelectElement).value));
  }

  protected onRange(key: string, event: Event): void {
    void this.api.setConfig(key, Number((event.target as HTMLInputElement).value));
  }
}
