using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Grynsoft.RuleEngine
{
    // Utility for discovering condition types via [ConditionInfo] attribute.
    public static class ConditionDiscovery
    {
        // Finds all types implementing ICondition with the [ConditionInfo] attribute.
        public static List<Type> DiscoverConditionTypes(params Assembly[] assemblies)
        {
            var asm = assemblies.Length > 0 ? assemblies : AppDomain.CurrentDomain.GetAssemblies();
            var result = new List<Type>();

            foreach (var assembly in asm)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var type in types)
                {
                    if (typeof(ICondition).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                    {
                        var attr = type.GetCustomAttribute<ConditionInfoAttribute>();
                        if (attr != null)
                            result.Add(type);
                    }
                }
            }

            return result;
        }

        // Groups discovered condition types by their Category.
        public static Dictionary<string, List<Type>> DiscoverByCategory(params Assembly[] assemblies)
        {
            var types = DiscoverConditionTypes(assemblies);
            return types
                .GroupBy(t => t.GetCustomAttribute<ConditionInfoAttribute>()?.Category ?? "General")
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}
