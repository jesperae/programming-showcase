// Dashboard over the showcase REST API - jobs, assets, config, KPIs,
// and a live event feed.

import { useCallback, useEffect, useState } from "react";
import {
  api,
  type AssetDto,
  type ConfigDto,
  type DomainEventDto,
  type JobDto,
  type KpiDto,
} from "./api";

export default function App() {
  const [jobs, setJobs] = useState<JobDto[]>([]);
  const [assets, setAssets] = useState<AssetDto[]>([]);
  const [credits, setCredits] = useState(0);
  const [config, setConfig] = useState<ConfigDto[]>([]);
  const [kpis, setKpis] = useState<KpiDto[]>([]);
  const [context, setContext] = useState<Record<string, boolean>>({});
  const [events, setEvents] = useState<DomainEventDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      const [j, a, b, c, k, ctx] = await Promise.all([
        api.jobs(),
        api.assets(),
        api.budget(),
        api.config(),
        api.kpis(),
        api.context(),
      ]);
      setJobs(j);
      setAssets(a);
      setCredits(b.credits);
      setConfig(c);
      setKpis(k);
      setContext(ctx);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }, []);

  useEffect(() => {
    void refresh();
    // Live domain events via SSE.
    const source = new EventSource("http://localhost:3000/api/events");
    source.onmessage = (msg) => {
      const e = JSON.parse(msg.data) as DomainEventDto;
      if (e.type === "connected") return;
      setEvents((prev) => [e, ...prev].slice(0, 30));
      void refresh(); // any domain event may have changed state
    };
    return () => source.close();
  }, [refresh]);

  const act = (fn: () => Promise<unknown>) => async () => {
    try {
      await fn();
      await refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  return (
    <div className="page">
      <header>
        <h1>Systems Showcase - React</h1>
        <p className="sub">jobs, assets, config, KPIs, live events</p>
      </header>

      {error && <div className="error">API error: {error} - is node-api running on :3000?</div>}

      <div className="grid">
        <section className="card wide">
          <h2>Jobs</h2>
          <div className="row">
            <button onClick={act(() => api.tick())}>Tick all</button>
            <button onClick={act(() => api.failReset())}>Reset failures</button>
          </div>
          {jobs.map((job) => (
            <div key={job.id} className={`job ${job.status}`}>
              <div className="row spread">
                <strong>{job.name}</strong>
                <span className={`badge ${job.status}`}>{job.status}</span>
              </div>
              <p className="dim">{job.description}</p>
              {job.milestones.map((m) => (
                <div key={m.label} className="milestone">
                  <div className="row spread">
                    <span className={m.isFailed ? "failed" : m.isCompleted ? "done" : ""}>
                      {m.display}
                    </span>
                    <span className="dim">
                      {m.progress}/{m.required}
                    </span>
                  </div>
                  <div className="bar">
                    <div
                      className={`fill ${m.isFailed ? "fail" : ""}`}
                      style={{ width: `${m.completionPercentage * 100}%` }}
                    />
                  </div>
                </div>
              ))}
              <div className="row">
                {job.status === "available" && (
                  <button onClick={act(() => api.giveJob(job.id))}>Give job</button>
                )}
                {job.status === "active" && (
                  <button onClick={act(() => api.forceComplete(job.id))}>Force complete</button>
                )}
                {job.creditReward > 0 && <span className="dim">reward: {job.creditReward}cr</span>}
              </div>
            </div>
          ))}
        </section>

        <section className="card">
          <h2>Context flags</h2>
          <p className="dim">Drive job conditions - toggle a flag, then Tick.</p>
          {Object.entries(context).map(([key, value]) => (
            <label key={key} className="flag">
              <input
                type="checkbox"
                checked={value}
                onChange={act(() => api.patchContext({ [key]: !value }))}
              />
              {key}
            </label>
          ))}
        </section>

        <section className="card">
          <h2>Assets &amp; Budget</h2>
          <p>
            <strong>{credits}cr</strong>
          </p>
          <div className="row">
            <button onClick={act(() => api.credit(500))}>+500</button>
            <button onClick={act(() => api.spend(500))}>−500</button>
          </div>
          {assets.map((a) => (
            <div key={a.id} className="row spread">
              <span>
                {a.displayName} ×{a.stacks}
              </span>
              <span className="row">
                <button onClick={act(() => api.adjustAsset(a.id, 1))}>+</button>
                <button onClick={act(() => api.adjustAsset(a.id, -1))}>−</button>
                <button onClick={act(() => api.purchaseAsset(a.id))}>buy {a.unitCost}cr</button>
              </span>
            </div>
          ))}
          {assets.length === 0 && <p className="dim">No assets held - buy one.</p>}
        </section>

        <section className="card">
          <h2>Config</h2>
          {config.map((c) => (
            <div key={c.key} className="row spread">
              <span>{c.displayName}</span>
              {c.isBool ? (
                <input
                  type="checkbox"
                  checked={c.value >= 1}
                  onChange={act(() => api.setConfig(c.key, c.value >= 1 ? 0 : 1))}
                />
              ) : c.optionList ? (
                <select
                  value={c.value}
                  onChange={(e) => {
                    const v = Number(e.target.value);
                    void api.setConfig(c.key, v).then(refresh).catch(() => { });
                  }}
                >
                  {c.optionList.map((o, i) => (
                    <option key={o} value={i}>
                      {o}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  type="range"
                  min={c.min}
                  max={c.max}
                  step={c.isPercentage ? 5 : 1}
                  value={c.value}
                  onChange={(e) => {
                    const v = Number(e.target.value);
                    void api.setConfig(c.key, v).then(refresh).catch(() => { });
                  }}
                />
              )}
              <span className="dim">{c.valueText ?? c.value}</span>
            </div>
          ))}
        </section>

        <section className="card">
          <h2>KPIs</h2>
          {kpis.map((k) => (
            <div key={k.name} className="row spread">
              <span className={k.completed ? "done" : ""}>
                {k.completed ? "★ " : ""}
                {k.displayName}
              </span>
              <span className="dim">
                {k.progress}/{k.progressMax}
              </span>
            </div>
          ))}
        </section>

        <section className="card wide">
          <h2>Event feed (SSE)</h2>
          <div className="feed">
            {events.map((e, i) => (
              <div key={i} className="event">
                <span className="dim">{new Date(e.at).toLocaleTimeString()}</span>{" "}
                <strong>{e.type}</strong> <code>{JSON.stringify(e.detail)}</code>
              </div>
            ))}
            {events.length === 0 && <p className="dim">Waiting for events…</p>}
          </div>
        </section>
      </div>
    </div>
  );
}
