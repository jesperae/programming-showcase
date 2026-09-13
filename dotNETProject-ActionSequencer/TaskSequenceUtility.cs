using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Grynsoft.TaskSequencing
{
    // Utility class for discovering methods marked for use in the task sequencer.
    public static class TaskSequenceUtility
    {
        // Represents a method that can be used in the task sequencer.
        public class ActionMethod
        {
            // The actual MethodInfo from reflection.
            public MethodInfo Method { get; private set; }

            // Display name to show in the UI.
            public string DisplayName { get; private set; }

            // Description of what the method does, if provided.
            public string Description { get; private set; }

            // Category for grouping methods in the UI, if provided.
            public string Category { get; private set; }

            public ActionMethod(MethodInfo method, TaskSequenceMethod attribute)
            {
                Method = method;
                DisplayName = attribute?.DisplayName ?? method.Name;
                Description = attribute?.Description;
                Category = attribute?.Category ?? "General";
            }

            // Invokes the method on the target object with the given parameters.
            public object Invoke(object target, params object[] parameters)
            {
                return Method.Invoke(target, parameters);
            }
        }

        // Gets all methods available for the task sequencer from the given type.
        // If the type has methods marked with [TaskSequenceMethod], only those are returned.
        // If no methods are marked, all public instance methods are returned.
        public static List<ActionMethod> GetActionMethods(Type type)
        {
            if (type == null)
                return new List<ActionMethod>();

            var markedMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => new
                {
                    Method = m,
                    Attribute = m.GetCustomAttribute<TaskSequenceMethod>(true)
                })
                .Where(m => m.Attribute != null)
                .Select(m => new ActionMethod(m.Method, m.Attribute))
                .ToList();

            if (markedMethods.Count == 0)
            {
                return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName)
                    .Where(m => m.DeclaringType != typeof(object))
                    .Select(m => new ActionMethod(m, null))
                    .ToList();
            }

            return markedMethods;
        }

        // Gets all methods available for the task sequencer from the given object.
        public static List<ActionMethod> GetActionMethods(object obj)
        {
            if (obj == null)
                return new List<ActionMethod>();

            return GetActionMethods(obj.GetType());
        }

        // Gets all action methods from the given object, grouped by category.
        public static Dictionary<string, List<ActionMethod>> GetActionMethodsByCategory(object obj)
        {
            var methods = GetActionMethods(obj);
            return methods.GroupBy(m => m.Category ?? "General")
                         .ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}
