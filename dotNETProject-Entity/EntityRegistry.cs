using System;
using System.Collections.Generic;

namespace Grynsoft.EntitySystem
{
    // A scoped registry for entities. Useful when you want isolated entity tracking
    // (e.g., per-scene, per-room, per-match) instead of the global static cache.
    public class EntityRegistry
    {
        private readonly Dictionary<Type, Dictionary<string, Entity>> _typeCache = new();
        private readonly List<Entity> _allEntities = new();
        private readonly object _lock = new();

        // Register an entity in this registry.
        public void Register(Entity entity)
        {
            lock (_lock)
            {
                var type = entity.GetType();
                if (!_typeCache.TryGetValue(type, out var dict))
                {
                    dict = new Dictionary<string, Entity>();
                    _typeCache[type] = dict;
                }
                dict[entity.UniqueName] = entity;
                _allEntities.Add(entity);
            }
        }

        // Unregister an entity from this registry.
        public void Unregister(Entity entity)
        {
            lock (_lock)
            {
                foreach (var cache in _typeCache.Values)
                    cache.Remove(entity.UniqueName);
                _allEntities.Remove(entity);
            }
        }

        // Find all entities of a specific type in this registry.
        public List<T> FindAll<T>() where T : Entity
        {
            lock (_lock)
            {
                var result = new List<T>();
                foreach (var entity in _allEntities)
                {
                    if (entity is T typed)
                        result.Add(typed);
                }
                return result;
            }
        }

        // Find all entities of a specific type by name.
        public List<T> FindAllByName<T>(string name) where T : Entity
        {
            var normalized = name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            var all = FindAll<T>();
            return all.FindAll(e => e.Name.Replace(" ", "").Replace("_", "").ToLowerInvariant() == normalized);
        }

        // Find the first entity by name.
        public T? FindByName<T>(string name) where T : Entity
        {
            var normalized = name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            var all = FindAll<T>();
            return all.Find(e => e.Name.Replace(" ", "").Replace("_", "").ToLowerInvariant() == normalized);
        }

        // Find nearest entity to a position.
        public T? FindNearest<T>(float x, float y, float maxDistance = float.MaxValue) where T : Entity
        {
            var all = FindAll<T>();
            if (all.Count == 0) return null;

            T? nearest = null;
            float nearestDist = maxDistance;

            foreach (var entity in all)
            {
                float dx = entity.X - x;
                float dy = entity.Y - y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = entity;
                }
            }

            return nearest;
        }

        // Find all entities within a circle.
        public List<T> FindInCircle<T>(float centerX, float centerY, float radius) where T : Entity
        {
            var all = FindAll<T>();
            var result = new List<T>();
            float radiusSq = radius * radius;

            foreach (var entity in all)
            {
                float dx = entity.X - centerX;
                float dy = entity.Y - centerY;
                if (dx * dx + dy * dy <= radiusSq)
                    result.Add(entity);
            }

            return result;
        }

        // Get all registered entities.
        public IReadOnlyList<Entity> AllEntities
        {
            get
            {
                lock (_lock) return _allEntities.AsReadOnly();
            }
        }

        // Get count of registered entities.
        public int Count
        {
            get
            {
                lock (_lock) return _allEntities.Count;
            }
        }

        // Clear all entities from this registry.
        public void Clear()
        {
            lock (_lock)
            {
                _typeCache.Clear();
                _allEntities.Clear();
            }
        }
    }
}
