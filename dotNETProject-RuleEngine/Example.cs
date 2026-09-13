using System;
using System.Collections.Generic;
using Grynsoft.RuleEngine;

namespace RuleEngineExample
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("=== RuleEngine Examples ===\n");

            // 1. Simple condition
            Console.WriteLine("1. Simple condition:");
            var player = new Character { Name = "Hero", Health = 30, MaxHealth = 100 };

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
                                new HealthBelowCondition { Threshold = 50f }
                            }
                        },
                        ThenActions = () => Console.WriteLine("  -> Low health warning!"),
                        UseElse = true,
                        ElseActions = () => Console.WriteLine("  -> Health is fine.")
                    }
                }
            };

            engine.Initialize();
            engine.Update();  // Should fire "Low health warning!" (30 < 50)

            // 2. AND/OR logic
            Console.WriteLine("\n2. AND/OR logic:");
            var enemy = new Character { Name = "Goblin", Health = 80, MaxHealth = 100, IsStunned = true };

            var andEngine = new RuleEngine
            {
                MasterObject = enemy,
                Conditions = new List<ConditionCheck>
                {
                    new ConditionCheck
                    {
                        ConditionList = new ConditionList
                        {
                            Conditions = new List<ICondition>
                            {
                                new HealthBelowCondition { Threshold = 50f },
                                new IsStunnedCondition()
                            }
                        },
                        ThenActions = () => Console.WriteLine("  AND: Low health AND stunned (won't fire - health is 80)"),
                        UseElse = true,
                        ElseActions = () => Console.WriteLine("  AND: Conditions not met (expected)")
                    }
                }
            };
            andEngine.Initialize();
            andEngine.Update();

            var orEngine = new RuleEngine
            {
                MasterObject = enemy,
                Conditions = new List<ConditionCheck>
                {
                    new ConditionCheck
                    {
                        ConditionList = new ConditionList
                        {
                            UseOrMode = true,
                            Conditions = new List<ICondition>
                            {
                                new HealthBelowCondition { Threshold = 50f },
                                new IsStunnedCondition()
                            }
                        },
                        ThenActions = () => Console.WriteLine("  OR: Low health OR stunned (fires - is stunned)")
                    }
                }
            };
            orEngine.Initialize();
            orEngine.Update();

            // 3. Inverted condition
            Console.WriteLine("\n3. Inverted condition:");
            var healthyPlayer = new Character { Name = "Paladin", Health = 100, MaxHealth = 100 };

            var invertEngine = new RuleEngine
            {
                MasterObject = healthyPlayer,
                Conditions = new List<ConditionCheck>
                {
                    new ConditionCheck
                    {
                        ConditionList = new ConditionList
                        {
                            Conditions = new List<ICondition>
                            {
                                new HealthBelowCondition { Threshold = 50f, InvertCondition = true }
                            }
                        },
                        ThenActions = () => Console.WriteLine("  -> Health is NOT below 50 (fires - health is 100)")
                    }
                }
            };
            invertEngine.Initialize();
            invertEngine.Update();

            // 4. Edge detection (fires once on FALSE->TRUE)
            Console.WriteLine("\n4. Edge detection:");
            var edgeChar = new Character { Name = "EdgeTest", Health = 100, MaxHealth = 100 };
            var edgeEngine = new RuleEngine
            {
                MasterObject = edgeChar,
                Conditions = new List<ConditionCheck>
                {
                    new ConditionCheck
                    {
                        ConditionList = new ConditionList
                        {
                            Conditions = new List<ICondition>
                            {
                                new HealthBelowCondition { Threshold = 50f }
                            }
                        },
                        ThenActions = () => Console.WriteLine("  -> Health dropped below 50! (fires once)"),
                        UseElse = true,
                        ElseActions = () => Console.WriteLine("  -> Health recovered above 50!")
                    }
                }
            };
            edgeEngine.Initialize();

            // Simulate health dropping
            edgeChar.Health = 40;
            edgeEngine.Update();  // FALSE->TRUE: fires THEN

            // Update again at same health - should NOT fire again
            edgeEngine.Update();
            Console.WriteLine("  (Second update at same health - no repeat fire)");

            // Simulate health recovering
            edgeChar.Health = 80;
            edgeEngine.Update();  // TRUE->FALSE: fires ELSE

            // 5. ExecuteWithContext
            Console.WriteLine("\n5. ExecuteWithContext:");
            engine.ExecuteWithContext(new Character { Name = "NPC", Health = 10, MaxHealth = 100 });

            Console.WriteLine("\n=== All examples complete ===");
        }
    }

    // Simple character class for examples
    public class Character
    {
        public string Name { get; set; } = "";
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public bool IsStunned { get; set; }
    }

    // Condition: Health below threshold
    [ConditionInfo("Health Below", "❤️", "Combat")]
    [Serializable]
    public class HealthBelowCondition : ConditionBase
    {
        public float Threshold { get; set; } = 50f;

        public override bool Evaluate()
        {
            if (MyObj is Character c)
                return ApplyInversion(c.Health < Threshold);
            return false;
        }

        public override string GetDescription() => $"Health < {Threshold}";
    }

    // Condition: Is stunned
    [ConditionInfo("Is Stunned", "⭐", "Combat")]
    [Serializable]
    public class IsStunnedCondition : ConditionBase
    {
        public override bool Evaluate()
        {
            if (MyObj is Character c)
                return ApplyInversion(c.IsStunned);
            return false;
        }

        public override string GetDescription() => "Is stunned";
    }
}
