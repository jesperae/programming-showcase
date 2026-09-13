using System;

namespace Grynsoft.RuleEngine
{
    // Abstract base class for conditions. Provides target resolution, inversion, and helper methods.
    [Serializable]
    public abstract class ConditionBase : ICondition
    {
        // Target selector — can be a direct reference, a name, or resolved from context.
        public object Target { get; set; }
        public bool InvertCondition { get; set; }

        // Back-reference to owning list, for reading captures via ResolveDynamicTarget.
        [NonSerialized] public ConditionList? ParentList;

        // The owning entity context.
        public object MyObj { get; set; }

        // Initialize with a master target. If Target is null, uses masterTarget.
        public virtual void Initialize(object masterTarget)
        {
            MyObj = Target ?? masterTarget;
        }

        // Resolve target dynamically from condition captures (falls back to MyObj).
        protected object ResolveDynamicTarget()
        {
            // In a full implementation, this would check the TargetResolver type.
            // For the .NET version, ParentList captures are used directly.
            return MyObj;
        }

        // Evaluate the condition. Override in subclasses.
        public abstract bool Evaluate();

        // Human-readable description. Override in subclasses.
        public abstract string GetDescription();

        // Apply inversion if InvertCondition is set.
        protected bool ApplyInversion(bool result) => InvertCondition ? !result : result;
    }
}
