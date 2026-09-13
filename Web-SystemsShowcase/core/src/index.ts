// @showcase/core - framework-agnostic systems:
// event bus, condition evaluation, job tracking, asset store,
// configuration service, and KPI tracking.

export { EventBus, type EventHandler } from "./events/EventBus.js";

export {
  ConditionList,
  ConditionBase,
  FuncCondition,
  type ICondition,
  type IConditionTargetProvider,
} from "./conditions/ConditionList.js";

export { Job } from "./jobs/Job.js";
export { JobMilestone } from "./jobs/JobMilestone.js";
export { JobTracker, type JobEvents } from "./jobs/JobTracker.js";

export {
  AssetStore,
  type AssetDef,
  type AssetStack,
  type AssetEvents,
} from "./assets/AssetStore.js";

export {
  ConfigService,
  ConfigSetting,
  InMemoryStore,
  type ConfigSettingDef,
  type ConfigCategory,
  type ConfigEvents,
  type KeyValueStore,
} from "./config/ConfigService.js";

export { Kpi, KpiTracker, type KpiEvents } from "./kpis/KpiTracker.js";
