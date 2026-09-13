using System;
using System.Collections.Generic;

namespace Grynsoft.RuleEngine
{
    // A list of conditions evaluated with AND or OR logic.
    // Supports target capture: conditions implementing IConditionTargetProvider can
    // capture Owner/Receiver values that other conditions in the same list can read.
    [Serializable]
    public class ConditionList
    {
        // If true, conditions are OR'd (any true = pass). Default is AND (all must be true).
        public bool UseOrMode { get; set; }

        // The list of conditions.
        public List<ICondition> Conditions { get; set; } = new List<ICondition>();

        // Per-list captures, scoped only to this list.
        [NonSerialized] public object? CapturedOwner;
        [NonSerialized] public object? CapturedReceiver;

        // Initialize all conditions with a master target.
        public void Initialize(object masterTarget)
        {
            foreach (var c in Conditions)
            {
                c?.Initialize(masterTarget);
                if (c is ConditionBase cb) cb.ParentList = this;
            }
        }

        // True if all (or any, in OR mode) conditions are met.
        public bool AreMet => Evaluate();

        // Evaluate all conditions with AND or OR logic.
        public bool Evaluate()
        {
            if (Conditions.Count == 0) return false;
            CapturedOwner = null;
            CapturedReceiver = null;

            if (UseOrMode)
            {
                foreach (var c in Conditions)
                {
                    if (c?.Evaluate() == true)
                    {
                        Capture(c);
                        return true;
                    }
                }
                return false;
            }

            // Two-pass: target providers first (so their captures are visible), then everything else.
            foreach (var c in Conditions)
                if (IsProvider(c) && !EvalAndCapture(c)) return false;

            foreach (var c in Conditions)
                if (!IsProvider(c) && !EvalAndCapture(c)) return false;

            return true;
        }

        // Get a human-readable description of all conditions joined by AND/OR.
        public string GetDescription()
        {
            if (Conditions.Count == 0) return "No conditions set";
            if (Conditions.Count == 1) return Conditions[0].GetDescription();

            string joiner = UseOrMode ? " OR " : " AND ";
            var descriptions = new List<string>();
            foreach (var condition in Conditions)
                descriptions.Add(condition.GetDescription());
            return string.Join(joiner, descriptions);
        }

        private bool EvalAndCapture(ICondition c)
        {
            if (c?.Evaluate() != true) return false;
            Capture(c);
            return true;
        }

        private static bool IsProvider(ICondition c) => c is IConditionTargetProvider p && p.ProvidesTarget;

        private void Capture(ICondition c)
        {
            if (c is not IConditionTargetProvider p) return;
            if (p.CapturedOwner != null) CapturedOwner = p.CapturedOwner;
            if (p.CapturedReceiver != null) CapturedReceiver = p.CapturedReceiver;
        }
    }
}
