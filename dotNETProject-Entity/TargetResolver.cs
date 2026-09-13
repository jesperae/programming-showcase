using System;
using System.Collections.Generic;

namespace Grynsoft.EntitySystem
{
    // Flexible target resolution with multiple selection modes.
    // Resolves to an Entity at runtime based on the configured mode.
    public class TargetResolver
    {
        public enum SelectType
        {
            Direct,            // Direct entity reference
            ByName,            // Find by name in registry
            Context,          // Use context entity passed at runtime
            NearestOfType,     // Nearest entity of a given type to context
            Tagged,            // All entities with a specific tag
            ContextChild,      // Search context's children by name
        }

        public SelectType SelectionType { get; set; } = SelectType.Direct;
        public Entity? DirectTarget { get; set; }
        public string? TargetName { get; set; }
        public string? Tag { get; set; }
        public Type? EntityType { get; set; }

        // Resolve a single target entity.
        public Entity? Resolve(Entity? context = null, EntityRegistry? registry = null)
        {
            return SelectionType switch
            {
                SelectType.Direct => DirectTarget,
                SelectType.ByName => registry != null && TargetName != null
                    ? registry.FindByName<Entity>(TargetName)
                    : Entity.FindByName<Entity>(TargetName!),
                SelectType.Context => context,
                SelectType.NearestOfType => context != null
                    ? (registry != null
                        ? registry.FindNearest<Entity>(context.X, context.Y)
                        : Entity.FindNearest<Entity>(context.X, context.Y))
                    : null,
                SelectType.ContextChild => ResolveContextChild(context),
                _ => DirectTarget
            };
        }

        // Resolve multiple targets (for modes that can return more than one).
        public List<Entity> ResolveAll(Entity? context = null, EntityRegistry? registry = null)
        {
            return SelectionType switch
            {
                SelectType.Tagged => registry != null && Tag != null
                    ? registry.FindAll<Entity>().FindAll(e => e.HasTag(Tag))
                    : Entity.FindAll<Entity>().FindAll(e => e.HasTag(Tag!)),
                SelectType.ByName => registry != null && TargetName != null
                    ? registry.FindAllByName<Entity>(TargetName)
                    : Entity.FindAllByName<Entity>(TargetName!),
                _ => new List<Entity> { Resolve(context, registry) }
            };
        }

        // Get a display string for this resolver (for debugging/UI).
        public string GetDisplayString()
        {
            return SelectionType switch
            {
                SelectType.Direct => DirectTarget?.Name ?? "None",
                SelectType.ByName => $"ByName: {TargetName}",
                SelectType.Context => "Context",
                SelectType.NearestOfType => $"Nearest {EntityType?.Name ?? "Entity"}",
                SelectType.Tagged => $"Tagged: {Tag}",
                SelectType.ContextChild => $"Child: {TargetName}",
                _ => SelectionType.ToString()
            };
        }

        private Entity? ResolveContextChild(Entity? context)
        {
            if (context == null || string.IsNullOrEmpty(TargetName))
                return null;

            // In the .NET version, "children" are components stored on the entity.
            return context.GetComponent<Entity>();
        }
    }
}
