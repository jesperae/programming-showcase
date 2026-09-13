using System;

namespace Grynsoft.TaskSequencing
{
    // Marks a method as available for use in the task sequencer.
    // If no methods in a class are marked, all public methods will be available.
    [AttributeUsage(AttributeTargets.Method)]
    public class TaskSequenceMethod : Attribute
    {
        // Display name to show in UI. If null, the method name is used.
        public string DisplayName { get; private set; }

        // Optional description of what the method does.
        public string Description { get; private set; }

        // Optional category for organizing methods in the UI.
        public string Category { get; private set; }

        public TaskSequenceMethod() : this(null, null, null) { }

        public TaskSequenceMethod(string displayName) : this(displayName, null, null) { }

        public TaskSequenceMethod(string displayName, string description) : this(displayName, description, null) { }

        public TaskSequenceMethod(string displayName, string description, string category)
        {
            DisplayName = displayName;
            Description = description;
            Category = category;
        }
    }
}
