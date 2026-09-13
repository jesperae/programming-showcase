using System;

namespace Grynsoft.RuleEngine
{
    // Attribute for self-describing conditions. Used by editors/tools for auto-discovery.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ConditionInfoAttribute : Attribute
    {
        public string DisplayName { get; }
        public string Emoji { get; }
        public string Category { get; }

        public ConditionInfoAttribute(string displayName, string emoji, string category = "General")
        {
            DisplayName = displayName;
            Emoji = emoji;
            Category = category;
        }
    }

    // Interface for all conditions.
    public interface ICondition
    {
        // The owning entity context.
        object MyObj { get; set; }
        // Whether to invert the result of Evaluate().
        bool InvertCondition { get; set; }
        // Initialize with a master target.
        void Initialize(object target);
        // Evaluate the condition.
        bool Evaluate();
        // Human-readable description.
        string GetDescription();
    }

    // Conditions implementing this interface capture Owner (performer) and Receiver (victim) targets.
    public interface IConditionTargetProvider
    {
        // True = evaluate this condition first so its captures are available to others.
        bool ProvidesTarget { get; }
        object CapturedOwner { get; }
        object CapturedReceiver { get; }
    }
}
