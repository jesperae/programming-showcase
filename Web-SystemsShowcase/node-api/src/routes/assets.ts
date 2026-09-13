// Asset + budget routes - REST surface over AssetStore.

import { Router } from "express";
import { assets } from "../state.js";

export const assetsRouter = Router();
export const budgetRouter = Router();

// GET /api/assets - owned assets with stack counts
assetsRouter.get("/", (_req, res) => {
  res.json(
    assets.assets.map((a) => ({
      id: a.def.id,
      displayName: a.def.displayName,
      stacks: a.stacks,
      unitCost: a.def.unitCost,
      stackable: a.def.stackable,
    })),
  );
});

// GET /api/assets/catalog - all registered asset definitions
assetsRouter.get("/catalog", (_req, res) => {
  res.json(assets.catalogDefs);
});

// POST /api/assets/:id/adjust { amount } - grant/remove stock
assetsRouter.post("/:id/adjust", (req, res) => {
  const amount = Number(req.body?.amount ?? 1);
  if (!Number.isFinite(amount) || amount === 0) {
    return res.status(400).json({ error: "amount must be a non-zero number" });
  }
  const ok =
    amount > 0 ? assets.giveAsset(req.params.id, amount) : assets.removeAsset(req.params.id, -amount);
  if (!ok) return res.status(409).json({ error: "adjustment failed (unknown asset or insufficient stock)" });
  res.json({ id: req.params.id, stacks: assets.stackCount(req.params.id) });
});

// POST /api/assets/:id/purchase { amount } - spend credits, grant asset (atomic)
assetsRouter.post("/:id/purchase", (req, res) => {
  const amount = Number(req.body?.amount ?? 1);
  if (!assets.purchase(req.params.id, amount)) {
    return res.status(409).json({ error: "purchase failed (insufficient credits or unavailable asset)" });
  }
  res.json({ id: req.params.id, stacks: assets.stackCount(req.params.id), credits: assets.credits });
});

// GET /api/budget - current credits
budgetRouter.get("/", (_req, res) => {
  res.json({ credits: assets.credits });
});

// POST /api/budget/credit { amount }
budgetRouter.post("/credit", (req, res) => {
  const amount = Number(req.body?.amount);
  if (!Number.isFinite(amount) || amount <= 0) {
    return res.status(400).json({ error: "amount must be a positive number" });
  }
  assets.credit(amount);
  res.json({ credits: assets.credits });
});

// POST /api/budget/spend { amount } - 409 on insufficient funds
budgetRouter.post("/spend", (req, res) => {
  const amount = Number(req.body?.amount);
  if (!Number.isFinite(amount) || amount <= 0) {
    return res.status(400).json({ error: "amount must be a positive number" });
  }
  if (!assets.spend(amount)) {
    return res.status(409).json({ error: "insufficient credits", credits: assets.credits });
  }
  res.json({ credits: assets.credits });
});
