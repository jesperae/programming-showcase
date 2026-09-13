using System;
using System.Collections.Generic;

namespace Grynsoft.RuleEngine
{
    // A rule engine that drives a collection of ConditionChecks.
    // Manages initialization, periodic evaluation, and reset.
    public class RuleEngine
    {
        // The master object context passed to all conditions.
        public object? MasterObject { get; set; }

        // Actions to run during initialization (before conditions are evaluated).
        public Action? InitActions { get; set; }

        // The list of condition checks (IF/THEN rules).
        public List<ConditionCheck> Conditions { get; set; } = new List<ConditionCheck>();

        // Whether the engine has been initialized.
        public bool IsInitialized { get; private set; }

        // Initialize all conditions with the master object.
        public void Initialize()
        {
            InitActions?.Invoke();

            foreach (var condition in Conditions)
            {
                condition.Initialize(MasterObject!);
            }

            IsInitialized = true;
        }

        // Update all condition checks (call each frame/tick).
        public void Update()
        {
            if (!IsInitialized) return;

            foreach (var condition in Conditions)
            {
                condition.UpdateCondition();
            }
        }

        // Execute all condition checks with a specific context object.
        public void ExecuteWithContext(object? contextObject)
        {
            foreach (var condition in Conditions)
            {
                condition.Execute(contextObject);
            }
        }

        // Reset all condition checks.
        public void Reset()
        {
            foreach (var condition in Conditions)
            {
                condition.Reset();
            }
        }

        // Add a condition check to the engine.
        public void AddCondition(ConditionCheck check)
        {
            Conditions.Add(check);
        }

        // Remove a condition check from the engine.
        public bool RemoveCondition(ConditionCheck check)
        {
            return Conditions.Remove(check);
        }
    }
}
