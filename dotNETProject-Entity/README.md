Entity
Entity Registry & Lifecycle for .NET

This is a lightweight entity system with static and scoped registries, type-cached lookups, spatial queries, component-style storage, tags, and lifecycle hooks. I converted it from the Unity GrynObject system to plain C#.


HOW IT WORKS

At the core is the Entity base class. When you create an entity, it automatically registers itself in a global static registry. You can then query for entities by type, name, position, or tags without keeping your own lists.

There are two kinds of registries: the static global one (built into Entity itself) and scoped registries (EntityRegistry instances) for isolated scenarios like a single room or level.

Entities can have components attached to them via a simple Dictionary<string, object> storage. This mimics Unity's GetComponent pattern but in plain C#. You can also tag entities with strings and query by tags.

The system includes a TargetResolver that can resolve targets in 6 different ways: direct reference, by name, by context, nearest of a type, by tag, or by searching a context entity's components.

Entities have lifecycle hooks: OnCreated when first made, OnDestroyed when removed, and Reset() which calls IResettable.ResetObject() if the entity implements it. This is useful for resetting enemies or pickups to their starting state.


QUICK EXAMPLE

using Grynsoft.EntitySystem;

// Define a custom entity
public class Enemy : Entity, IResettable, IFactionMember
{
    public float Health { get; set; } = 100f;
    public string Faction { get; set; } = "Hostile";

    public Enemy(string name) : base(name) { }

    public void ResetObject()
    {
        Health = 100f;
    }
}

// Create entities
var goblin = new Enemy("Goblin");
goblin.X = 10f;
goblin.Y = 5f;
goblin.AddTag("Boss");

var orc = new Enemy("Orc");
orc.X = 15f;
orc.Y = 8f;

// Static queries (global)
var allEnemies = Entity.FindAll<Enemy>();
var nearest = Entity.FindNearest<Enemy>(0, 0, 100f);
var inArea = Entity.FindInCircle<Enemy>(10, 5, 20f);
var byName = Entity.FindByName<Enemy>("Goblin");

// Scoped registry (isolated)
var roomRegistry = new EntityRegistry();
roomRegistry.Register(new Enemy("Room1_Goblin"));
var roomEnemies = roomRegistry.FindAll<Enemy>();

// Component storage
var ai = goblin.GetOrAddComponent<SimpleAI>();
var found = goblin.GetComponent<SimpleAI>();

// Lifecycle
goblin.Reset();  // Calls IResettable.ResetObject()
goblin.Destroy(); // Unregisters + calls OnDestroyed()


MAIN API

Entity (static methods):
- FindAll<T>() -- all entities of type T
- FindAllByName<T>(name) -- all entities by normalized name
- FindByName<T>(name) -- first entity by name
- FindNearest<T>(x, y, maxDist) -- nearest to position
- FindInCircle<T>(cx, cy, r) -- all within circle
- FindInRectangle<T>(minX, minY, maxX, maxY) -- all within rect
- ClearStaticCaches() -- reset all caches
- TotalCount -- count of all entities

Entity (instance members):
- Name / UniqueName -- display name / unique ID
- X, Y, Z -- position
- IsActive -- active flag
- Tags -- tag list
- AddTag / RemoveTag / HasTag -- tag management
- GetComponent<T>() -- get attached component
- GetOrAddComponent<T>() -- get or create component
- AddComponent<T>(comp) -- attach a component
- RemoveComponent<T>() -- remove component
- Reset() -- calls IResettable.ResetObject() if implemented
- Destroy() -- unregister + cleanup

EntityRegistry (scoped):
- Register(entity) -- add to registry
- Unregister(entity) -- remove from registry
- FindAll<T>() -- all of type T in this registry
- FindByName<T>(name) -- by name in this registry
- FindNearest<T>(x, y, max) -- nearest in this registry
- FindInCircle<T>(cx, cy, r) -- within circle in this registry
- AllEntities -- all registered
- Clear() -- remove all

TargetResolver modes:
- Direct -- direct entity reference
- ByName -- find by name
- Context -- use runtime context entity
- NearestOfType -- nearest entity to context
- Tagged -- all entities with a tag
- ContextChild -- search context's components

Interfaces:
- IResettable -- ResetObject()
- IPositioned -- X, Y, Z
- IActivatable -- IsActive, Activate(), Deactivate()
- IFactionMember -- Faction
- ITaggable -- Tags, HasTag(tag)


ORIGIN

Converted from Unity GrynObject system. MonoBehaviour became a plain C# class. GetComponent<T>() became Dictionary<string, object> storage. Transform.position became X/Y/Z floats. FindObjectsOfType<T>() became a type-cached dictionary. Spine/animation/combat properties were removed. Core registry + lookup + lifecycle patterns were preserved.


LICENSE

MIT -- see LICENSE file.
