RuleEngine
Extensible IF/THEN Evaluation for .NET

This is a data-driven rule engine with extensible conditions, AND/OR logic, target capture, and edge detection (when a condition goes from false to true, or true to false). I converted it from the Unity Conditions/GameConditions system.


HOW IT WORKS

The core idea is simple: you define conditions, group them into rules, and the engine checks them every tick. When a rule's conditions become true, it fires its THEN actions. When they become false again, it can fire ELSE actions.

The hierarchy goes like this:

- RuleEngine is the driver. It holds a list of ConditionCheck rules and ticks them all each Update().
- ConditionCheck is one IF/THEN/ELSE rule. It watches for the edge transition (false to true, or true to false) so actions only fire once on the change, not every frame.
- ConditionList holds a list of ICondition objects and evaluates them with AND or OR logic. It can also capture targets from conditions that implement IConditionTargetProvider, so one condition can find an entity and pass it to sibling conditions.
- ICondition / ConditionBase is what you extend to make your own conditions. You implement Evaluate() which returns true or false. You can also invert a condition with InvertCondition.

To make a new condition, you create a class extending ConditionBase, slap a [ConditionInfo] attribute on it with a name/emoji/category, and implement Evaluate(). The ConditionDiscovery system will automatically find it via reflection.

You can also call ExecuteWithContext(obj) to force-evaluate all rules with a specific context object, which is useful for things like "when this enemy dies, check all rules against this enemy."


QUICK EXAMPLE

using Grynsoft.RuleEngine;

// Define a custom condition
[ConditionInfo("Health Below", "heart", "Combat")]
public class HealthBelowCondition : ConditionBase
{
    public float Threshold { get; set; } = 50f;

    public override bool Evaluate()
    {
        if (MyObj is IHasHealth target)
            return ApplyInversion(target.Health < Threshold);
        return false;
    }

    public override string GetDescription() => $"Health < {Threshold}";
}

// Create a rule engine
var engine = new RuleEngine
{
    MasterObject = player,
    Conditions = new List<ConditionCheck>
    {
        new ConditionCheck
        {
            ConditionList = new ConditionList
            {
                Conditions = new List<ICondition>
                {
                    new HealthBelowCondition { Threshold = 30f }
                }
            },
            ThenActions = () => Console.WriteLine("Low health!"),
            UseElse = true,
            ElseActions = () => Console.WriteLine("Health recovered.")
        }
    }
};

engine.Initialize();

// Call each tick/frame
engine.Update();

// Force-trigger with context
engine.ExecuteWithContext(enemy);


MAIN API

RuleEngine:
- MasterObject -- context object passed to all conditions
- InitActions -- run during initialization
- Conditions -- list of ConditionCheck rules
- Initialize() -- initialize all conditions
- Update() -- tick all conditions (edge detection)
- ExecuteWithContext(obj) -- force-evaluate with a context
- Reset() -- reset all condition states
- AddCondition(check) -- add a rule
- RemoveCondition(check) -- remove a rule

ConditionCheck (IF/THEN/ELSE):
- ConditionList -- conditions to evaluate
- ThenActions -- fires on false-to-true transition
- ElseActions -- fires on true-to-false transition (if UseElse is true)
- UpdateCondition() -- edge-detection tick
- ForceTrigger() -- fire THEN immediately
- Reset() -- clear state

ConditionList (AND/OR + Capture):
- UseOrMode -- use OR instead of AND
- Conditions -- List<ICondition>
- AreMet -- evaluate all
- CapturedOwner -- from IConditionTargetProvider
- CapturedReceiver -- from IConditionTargetProvider

ICondition / ConditionBase:
- MyObj -- context entity
- InvertCondition -- invert the result
- Initialize(target) -- set up context
- Evaluate() -- check condition
- GetDescription() -- human-readable description

IConditionTargetProvider:
- ProvidesTarget -- true means evaluate first so siblings can use the captured target
- CapturedOwner -- the performer
- CapturedReceiver -- the victim

ConditionDiscovery:
- DiscoverConditionTypes() -- find all [ConditionInfo] types
- DiscoverByCategory() -- group by category


ADDING A NEW CONDITION

1. Create a class extending ConditionBase
2. Add [ConditionInfo("Name", "emoji", "Category")]
3. Implement Evaluate() and GetDescription()


ORIGIN

Converted from Unity Conditions.cs, ConditionImplementations.cs, and GameConditions.cs. GrynObject was replaced with object. Component lookups were removed. SaveData.IsGameReady was removed. Core AND/OR evaluation and target capture logic was preserved exactly.


LICENSE

MIT -- see LICENSE file.
