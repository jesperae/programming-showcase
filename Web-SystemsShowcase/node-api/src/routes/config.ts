// Config routes - REST surface over ConfigService.

import { Router } from "express";
import { config } from "../state.js";

export const configRouter = Router();

function serializeSetting(key: string) {
  const s = config.find(key);
  if (!s) return null;
  return {
    key: s.def.key,
    category: s.def.category,
    displayName: s.def.displayName,
    value: s.get(),
    defaultValue: s.def.defaultValue,
    min: s.def.min,
    max: s.def.max,
    increment: s.def.increment,
    isPercentage: s.def.isPercentage,
    isBool: s.def.isBool,
    valueText: s.getValueText(s.get()),
    optionList: s.def.optionList ?? null,
  };
}

// GET /api/config - all settings with current values
configRouter.get("/", (_req, res) => {
  res.json(config.list().map((s) => serializeSetting(s.def.key)));
});

// PUT /api/config/:key { value } - validated set (clamped to min/max)
configRouter.put("/:key", (req, res) => {
  const setting = config.find(req.params.key);
  if (!setting) return res.status(404).json({ error: `setting '${req.params.key}' not found` });

  const value = Number(req.body?.value);
  if (!Number.isFinite(value)) {
    return res.status(400).json({ error: "value must be a number" });
  }
  setting.set(value);
  res.json(serializeSetting(req.params.key));
});
