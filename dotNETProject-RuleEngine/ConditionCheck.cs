using System;

namespace Grynsoft.RuleEngine
{
    // An IF/THEN/ELSE rule: evaluates a ConditionList and executes actions on state transitions.
    // Supports edge detection: fires THEN actions on FALSE→TRUE, ELSE actions on TRUE→FALSE.
    [Serializable]
    public class ConditionCheck
    {
        // The master object context for this rule.
        public object? MasterObject { get; set; }

        // The conditions to evaluate.
        public ConditionList ConditionList { get; set; } = new ConditionList();

        // Actions to execute when conditions become true.
        public Action? ThenActions { get; set; }

        // Whether to use ELSE actions.
        public bool UseElse { get; set; }

        // Actions to execute when conditions become false (if UseElse is true).
        public Action? ElseActions { get; set; }

        [NonSerialized] private bool _lastConditionState;
        [NonSerialized] private bool _hasTriggered;

        // Initialize the condition check. Evaluates initial state and fires if already true.
        public void Initialize()
        {
            ConditionList.Initialize(MasterObject!);
            _lastConditionState = EvaluateCondition();
        }

        // Initialize with a master object.
        public void Initialize(object master)
        {
            MasterObject = master;
            Initialize();
        }

        // Evaluate the condition list.
        public bool EvaluateCondition() => ConditionList.AreMet;

        // Execute the condition check with a specific context object.
        public void Execute(object? contextObject)
        {
            ConditionList.Initialize(contextObject!);
            if (ConditionList.AreMet)
            {
                ThenActions?.Invoke();
            }
            else if (UseElse)
            {
                ElseActions?.Invoke();
            }
        }

        // Update the condition check with edge detection.
        // Fires THEN actions on FALSE→TRUE transition (once).
        // Fires ELSE actions on TRUE→FALSE transition (if enabled), then re-arms.
        public void UpdateCondition()
        {
            bool current = EvaluateCondition();

            // FALSE→TRUE: fire Then actions once.
            if (current && !_lastConditionState && !_hasTriggered)
            {
                ThenActions?.Invoke();
                _hasTriggered = true;
                ClearCaptures();
            }
            // TRUE→FALSE: fire Else actions (if enabled), then re-arm.
            else if (!current && _lastConditionState)
            {
                if (UseElse)
                {
                    ElseActions?.Invoke();
                    ClearCaptures();
                }
                _hasTriggered = false;
            }

            _lastConditionState = current;
        }

        // Force-trigger the THEN actions immediately.
        public void ForceTrigger()
        {
            ThenActions?.Invoke();
            ClearCaptures();
        }

        // Reset the condition check state (does not re-evaluate).
        public void Reset()
        {
            _hasTriggered = false;
            _lastConditionState = false;
        }

        // Get a description of the conditions.
        public string GetConditionDescription() => ConditionList.GetDescription();

        private void ClearCaptures()
        {
            ConditionList.CapturedOwner = null;
            ConditionList.CapturedReceiver = null;
        }
    }
}
