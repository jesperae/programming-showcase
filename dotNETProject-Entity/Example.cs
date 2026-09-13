using System;
using System.Collections.Generic;
using Grynsoft.EntitySystem;

namespace EntityExample
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("=== Entity System Examples ===\n");

            Entity.ClearStaticCaches();

            // 1. Create entities
            Console.WriteLine("1. Create entities:");
            var goblin = new Enemy("Goblin");
            goblin.X = 10f; goblin.Y = 5f;
            goblin.AddTag("Boss");

            var orc = new Enemy("Orc");
            orc.X = 15f; orc.Y = 8f;

            var npc = new NPC("Merchant");
            npc.X = 0f; npc.Y = 0f;

            Console.WriteLine($"  Created: {goblin}");
            Console.WriteLine($"  Created: {orc}");
            Console.WriteLine($"  Created: {npc}");

            // 2. Static queries
            Console.WriteLine("\n2. Static queries:");
            var allEnemies = Entity.FindAll<Enemy>();
            Console.WriteLine($"  FindAll<Enemy>: {allEnemies.Count} found");

            var nearest = Entity.FindNearest<Enemy>(0, 0, 100f);
            Console.WriteLine($"  FindNearest<Enemy> to (0,0): {nearest?.Name}");

            var inCircle = Entity.FindInCircle<Enemy>(10, 5, 20f);
            Console.WriteLine($"  FindInCircle<Enemy> at (10,5) r=20: {inCircle.Count} found");

            var byName = Entity.FindByName<Enemy>("Goblin");
            Console.WriteLine($"  FindByName<Enemy>(\"Goblin\"): {byName?.Name ?? "not found"}");

            // 3. Tags
            Console.WriteLine("\n3. Tags:");
            Console.WriteLine($"  Goblin has 'Boss' tag: {goblin.HasTag("Boss")}");
            Console.WriteLine($"  Orc has 'Boss' tag: {orc.HasTag("Boss")}");

            // 4. Component storage
            Console.WriteLine("\n4. Component storage:");
            goblin.AddComponent(new SimpleAI { AggroRange = 15f, Damage = 5f });
            var ai = goblin.GetComponent<SimpleAI>();
            Console.WriteLine($"  Goblin AI: AggroRange={ai?.AggroRange}, Damage={ai?.Damage}");

            var orcAi = orc.GetOrAddComponent<SimpleAI>();
            orcAi.AggroRange = 20f;
            Console.WriteLine($"  Orc AI (auto-created): AggroRange={orcAi.AggroRange}");

            // 5. IResettable
            Console.WriteLine("\n5. IResettable:");
            Console.WriteLine($"  Goblin health before reset: {goblin.Health}");
            goblin.Health = 10f;
            Console.WriteLine($"  Goblin health after damage: {goblin.Health}");
            goblin.Reset();
            Console.WriteLine($"  Goblin health after Reset(): {goblin.Health}");

            // 6. Scoped registry
            Console.WriteLine("\n6. Scoped registry:");
            Entity.ClearStaticCaches();

            var room1 = new EntityRegistry();
            var room2 = new EntityRegistry();

            room1.Register(new Enemy("Room1_Goblin"));
            room1.Register(new Enemy("Room1_Orc"));
            room2.Register(new Enemy("Room2_Skeleton"));

            Console.WriteLine($"  Room1 enemies: {room1.FindAll<Enemy>().Count}");
            Console.WriteLine($"  Room2 enemies: {room2.FindAll<Enemy>().Count}");
            Console.WriteLine($"  Room1 total entities: {room1.Count}");

            var room1Nearest = room1.FindNearest<Enemy>(0, 0);
            Console.WriteLine($"  Room1 nearest to origin: {room1Nearest?.Name}");

            // 7. TargetResolver
            Console.WriteLine("\n7. TargetResolver:");
            var resolver = new TargetResolver
            {
                SelectionType = TargetResolver.SelectType.Direct,
                DirectTarget = orc
            };
            Console.WriteLine($"  Direct: {resolver.Resolve()?.Name}");

            var byNameResolver = new TargetResolver
            {
                SelectionType = TargetResolver.SelectType.ByName,
                TargetName = "Room1_Goblin"
            };
            Console.WriteLine($"  ByName: {byNameResolver.Resolve(registry: room1)?.Name}");

            var contextResolver = new TargetResolver
            {
                SelectionType = TargetResolver.SelectType.Context
            };
            Console.WriteLine($"  Context: {contextResolver.Resolve(context: orc)?.Name}");

            Console.WriteLine($"  Display: {byNameResolver.GetDisplayString()}");

            // 8. Lifecycle
            Console.WriteLine("\n8. Lifecycle:");
            var tempEnemy = new Enemy("TempEnemy");
            Console.WriteLine($"  Total entities: {Entity.TotalCount}");
            tempEnemy.Destroy();
            Console.WriteLine($"  After Destroy: {Entity.TotalCount}");

            // 9. Interfaces
            Console.WriteLine("\n9. Interfaces:");
            Console.WriteLine($"  Goblin is IResettable: {goblin is IResettable}");
            Console.WriteLine($"  Goblin is IPositioned: {goblin is IPositioned}");
            Console.WriteLine($"  Goblin is IFactionMember: {goblin is IFactionMember}");
            Console.WriteLine($"  Goblin faction: {((IFactionMember)goblin).Faction}");

            Console.WriteLine("\n=== All examples complete ===");
        }
    }

    // Custom entity: Enemy
    public class Enemy : Entity, IResettable, IFactionMember
    {
        public float Health { get; set; } = 100f;
        public string Faction { get; set; } = "Hostile";

        public Enemy(string name) : base(name) { }

        public void ResetObject()
        {
            Health = 100f;
            Console.WriteLine($"  -> {Name} reset to full health");
        }

        protected override void OnDestroyed()
        {
            Console.WriteLine($"  -> {Name} destroyed");
        }
    }

    // Custom entity: NPC
    public class NPC : Entity, IFactionMember
    {
        public string Faction { get; set; } = "Neutral";

        public NPC(string name) : base(name) { }
    }

    // Component: Simple AI
    public class SimpleAI
    {
        public float AggroRange { get; set; } = 10f;
        public float Damage { get; set; } = 5f;
    }
}
