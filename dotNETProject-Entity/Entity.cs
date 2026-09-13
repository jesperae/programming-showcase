using System;
using System.Collections.Generic;

namespace Grynsoft.EntitySystem
{
    // Base class for all entities in the system. Provides unique naming, static registry,
    // type-cached lookups, spatial queries, lifecycle hooks, and component-style accessors.
    public class Entity : IPositioned
    {
        #region Static Registry

        private static readonly Dictionary<Type, Dictionary<string, Entity>> _typeCache = new();
        private static readonly object _lock = new();

        // Register an entity in the type cache.
        private static void Register(Entity entity)
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
            }
        }

        // Unregister an entity from the type cache.
        private static void Unregister(Entity entity)
        {
            lock (_lock)
            {
                foreach (var cache in _typeCache.Values)
                    cache.Remove(entity.UniqueName);
            }
        }

        // Find all entities of a specific type.
        public static List<T> FindAll<T>() where T : Entity
        {
            lock (_lock)
            {
                var result = new List<T>();
                foreach (var cache in _typeCache.Values)
                {
                    foreach (var entity in cache.Values)
                    {
                        if (entity is T typed)
                            result.Add(typed);
                    }
                }
                return result;
            }
        }

        // Find all entities of a specific type by name (normalized).
        public static List<T> FindAllByName<T>(string name) where T : Entity
        {
            var normalized = NormalizeName(name);
            var all = FindAll<T>();
            return all.FindAll(e => NormalizeName(e.Name) == normalized);
        }

        // Find the first entity of a specific type by name.
        public static T? FindByName<T>(string name) where T : Entity
        {
            var all = FindAllByName<T>(name);
            return all.Count > 0 ? all[0] : null;
        }

        // Find the nearest entity of a specific type to a position.
        public static T? FindNearest<T>(float x, float y, float maxDistance = float.MaxValue) where T : Entity
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

        // Find all entities of a specific type within a circular area.
        public static List<T> FindInCircle<T>(float centerX, float centerY, float radius) where T : Entity
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

        // Find all entities of a specific type within a rectangular area.
        public static List<T> FindInRectangle<T>(float minX, float minY, float maxX, float maxY) where T : Entity
        {
            var all = FindAll<T>();
            var result = new List<T>();

            foreach (var entity in all)
            {
                if (entity.X >= minX && entity.X <= maxX && entity.Y >= minY && entity.Y <= maxY)
                    result.Add(entity);
            }

            return result;
        }

        // Clear all static caches (for testing or reinitialization).
        public static void ClearStaticCaches()
        {
            lock (_lock) _typeCache.Clear();
        }

        // Get the count of all registered entities.
        public static int TotalCount
        {
            get
            {
                lock (_lock)
                {
                    int count = 0;
                    foreach (var cache in _typeCache.Values)
                        count += cache.Count;
                    return count;
                }
            }
        }

        #endregion

        #region Instance

        private string _name;
        private string _uniqueName;
        private float _x, _y, _z;
        private bool _isActive = true;
        private readonly List<string> _tags = new();
        private readonly Dictionary<string, object> _components = new();

        // The display name of this entity.
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                if (string.IsNullOrEmpty(_uniqueName) || _uniqueName == _name)
                    _uniqueName = value;
            }
        }

        // The unique identifier for this entity (used for save/load and registry).
        public string UniqueName
        {
            get => _uniqueName;
            set => _uniqueName = value;
        }

        // Whether this entity is active.
        public bool IsActive
        {
            get => _isActive;
            set => _isActive = value;
        }

        // Position
        public float X { get => _x; set => _x = value; }
        public float Y { get => _y; set => _y = value; }
        public float Z { get => _z; set => _z = value; }

        // Tags
        public IReadOnlyList<string> Tags => _tags;
        public bool HasTag(string tag) => _tags.Contains(tag);
        public void AddTag(string tag) { if (!_tags.Contains(tag)) _tags.Add(tag); }
        public void RemoveTag(string tag) => _tags.Remove(tag);

        // Component-style storage (for attaching arbitrary data/behavior).
        public T? GetComponent<T>() where T : class
        {
            var key = typeof(T).Name;
            return _components.TryGetValue(key, out var comp) ? comp as T : null;
        }

        public T GetOrAddComponent<T>() where T : class, new()
        {
            var key = typeof(T).Name;
            if (!_components.TryGetValue(key, out var comp) || comp is not T)
            {
                comp = new T();
                _components[key] = comp;
            }
            return (T)comp;
        }

        public void AddComponent<T>(T component) where T : class
        {
            _components[typeof(T).Name] = component;
        }

        public bool RemoveComponent<T>() where T : class
        {
            return _components.Remove(typeof(T).Name);
        }

        // Creates a new entity with the given name.
        public Entity(string name)
        {
            _name = name;
            _uniqueName = name;
            Register(this);
        }

        // Called when the entity is created. Override for custom initialization.
        protected virtual void OnCreated() { }

        // Called when the entity is destroyed. Override for custom cleanup.
        protected virtual void OnDestroyed() { }

        // Destroy this entity — unregisters from static cache and calls OnDestroyed.
        public void Destroy()
        {
            OnDestroyed();
            Unregister(this);
        }

        // Reset this entity if it implements IResettable.
        public void Reset()
        {
            if (this is IResettable resettable)
                resettable.ResetObject();
        }

        // Debug helper — returns a string representation.
        public override string ToString() => $"[{GetType().Name}] {Name} ({UniqueName}) @ ({X}, {Y}, {Z})";

        #endregion

        #region Helpers

        private static string NormalizeName(string name)
        {
            return name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        }

        #endregion
    }
}
